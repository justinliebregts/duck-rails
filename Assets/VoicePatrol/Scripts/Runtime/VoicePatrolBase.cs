using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime
{
    /// <summary>
    /// Abstract base class for VoicePatrol voice provider integrations.
    /// Contains all shared logic: SDK lifecycle, violation handling, progressive mute rules,
    /// encrypted local storage, and events.
    ///
    /// <b>Events (subscribe via code — these are DontDestroyOnLoad singletons):</b>
    /// <code>
    /// VoicePatrolBase.OnViolationCountChanged += (count) =&gt; { };
    /// VoicePatrolBase.OnMuteApplied += (endTimeUtc, reason) =&gt; { };
    /// VoicePatrolBase.OnMuteExpired += () =&gt; { };
    /// VoicePatrolBase.OnWarningIssued += (violationInfo) =&gt; { };
    /// VoicePatrolBase.OnUnmuteBlockedByPunishment += (reason, remaining) =&gt; { };
    /// </code>
    ///
    /// <b>Storage:</b>
    /// Violation data is saved to encrypted local storage (PlayerPrefs + AES-128) by default.
    /// Override <see cref="SaveViolationData"/> and <see cref="LoadViolationData"/> in a subclass
    /// to integrate your own backend (PlayFab, Firebase, etc.).
    /// </summary>
    [HelpURL("https://dashboard.voicepatrol.dev/")]
    [RequireComponent(typeof(VoicePatrolWrapper))]
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    public abstract class VoicePatrolBase : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("Your VoicePatrol project API key from dashboard.voicepatrol.dev")]
        [SerializeField] private string _apiKey;

        [Tooltip("Enable age verification by analyzing the first 60 seconds of speech")]
        [SerializeField] private bool _ageVerification;

        [Tooltip("Enable detailed SDK debug logs in the console")]
        [SerializeField] private bool _debugLogs;

        [Tooltip("If enabled, rooms whose name contains the private room identifier will not be moderated")]
        [SerializeField] private bool _enablePrivateRoomFiltering;

        [Tooltip("Room names containing this string are treated as private and won't be moderated. " +
                 "Case-insensitive. Example: '_private' would match 'lobby_private_123'")]
        [SerializeField] private string _privateRoomIdentifier = "_private";

        [Tooltip("Enable progressive automated muting based on violations")]
        [SerializeField] private bool _enableAutomatedMuting;

        [Tooltip("Progressive mute rules applied in order by violation count")]
        [SerializeField] private List<MuteRule> _muteRules = new()
        {
            new MuteRule { action = MuteAction.LogOnly,  muteDurationMinutes = 0 },
            new MuteRule { action = MuteAction.WarnOnly, muteDurationMinutes = 0 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 60 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 120 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 180 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 240 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 300 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 360 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 420 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 480 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 540 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 600 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 660 },
            new MuteRule { action = MuteAction.Mute,     muteDurationMinutes = 720 },
        };

        [Tooltip("Maximum mute duration in minutes. Once the rules list is exhausted, " +
                 "all subsequent offenses receive this duration.")]
        [SerializeField] private int _maxMuteDurationMinutes = 720;

        #endregion

        #region Types

        public enum MuteAction
        {
            LogOnly,
            WarnOnly,
            Mute
        }

        /// <summary>
        /// Describes why VoicePatrol is currently muted. Evaluated in priority order.
        /// </summary>
        public enum MuteReason
        {
            /// <summary>VoicePatrol is not muted — actively monitoring.</summary>
            NotMuted,
            /// <summary>SDK has not been initialized yet.</summary>
            NotInitialized,
            /// <summary>Player is under a timed punishment mute from a violation.</summary>
            PunishmentMute,
            /// <summary>Currently in a private room — moderation intentionally disabled.</summary>
            PrivateRoom,
            /// <summary>Voice provider is not connected to a room.</summary>
            NotConnected,
            /// <summary>Microphone/recorder is not transmitting (self-muted or disabled).</summary>
            NotTransmitting,
            /// <summary>Not enough players in the room (requires 2+).</summary>
            InsufficientPlayers,
            /// <summary>Force-muted on demand via <see cref="SetForceMuted"/>.</summary>
            ForceMuted
        }

        [Serializable]
        public class MuteRule
        {
            [Tooltip("Action to take for this offense number")]
            public MuteAction action;

            [Tooltip("Duration in minutes (only used when action is Mute)")]
            public int muteDurationMinutes;
        }

        /// <summary>
        /// Serializable container for violation state. Used by the default Save/Load implementation.
        /// Subclasses may reuse this for their own serialization.
        /// </summary>
        [Serializable]
        protected class ViolationSaveData
        {
            public int violationCount;
            public long muteEndTimeTicks;
            public string muteReason;
        }

        #endregion

        #region Events

        /// <summary>Fired whenever the violation count changes. Passes the new count.</summary>
        public static event Action<int> OnViolationCountChanged;

        /// <summary>Fired when a timed mute is applied. Passes end time (UTC) and reason.</summary>
        public static event Action<DateTime, string> OnMuteApplied;

        /// <summary>Fired when a timed mute expires naturally.</summary>
        public static event Action OnMuteExpired;

        /// <summary>Fired when a warning is issued (MuteAction.WarnOnly). Passes violation info string.</summary>
        public static event Action<string> OnWarningIssued;

        /// <summary>Fired when the player tries to unmute while punishment-muted.</summary>
        public static event Action<string, TimeSpan> OnUnmuteBlockedByPunishment;

        #endregion

        #region Private State

        public static VoicePatrolBase Instance { get; private set; }

        protected string LogPrefix { get; private set; }

        /// <summary>
        /// Whether debug logging is enabled. Subclasses can use this or call <see cref="Log"/> directly.
        /// </summary>
        protected bool DebugLogsEnabled => _debugLogs;

        /// <summary>
        /// Logs a message only when Debug Logs is enabled in the inspector.
        /// Warnings and errors, use Debug.LogWarning/Debug.LogError directly — those should always be visible.
        /// </summary>
        [System.Diagnostics.Conditional("DUCKZ_DIAGNOSTICS")]
        protected void Log(string message)
        {
            if (_debugLogs) Debug.Log($"{LogPrefix} {message}");
        }

        // Violation tracking — protected so Save/LoadViolationData overrides can access them
        protected int ViolationCount;
        protected DateTime MuteEndTime = DateTime.MinValue;
        protected string MuteReasonText = "";

        // Monotonic backstop for the punishment mute. MuteEndTime (wall clock) is what
        // persists, but on its own it can be defeated by shifting the device clock forward.
        // While the app is running the deadline is therefore also tracked against
        // Time.realtimeSinceStartup (OS monotonic time, immune to clock changes) and the
        // mute stays active while EITHER clock says so. When the wall clock claims expiry
        // but the monotonic clock disagrees, the persisted record is re-anchored so the
        // correction also survives a restart. -1 = no anchored deadline. Known limits:
        // a clock shifted forward while the app is fully closed can't be detected locally
        // (needs a backend time source via the Save/LoadViolationData override), and if the
        // OS suspends the monotonic counter during device sleep, time slept may not count
        // toward the mute (bounded by the mute's remaining duration).
        private double _monotonicMuteEnd = -1;
        protected bool IsDataLoaded;
        private bool _isInitialized;

        /// <summary>
        /// The effective unique player ID the SDK was initialized with (the uniqueId passed
        /// to <see cref="Initialize"/>, or the device unique identifier fallback). Null
        /// before initialization. Integrations use this to warn when a headless setup's
        /// voice-provider identity doesn't match — it is never written to the provider.
        /// </summary>
        protected string UniqueId { get; private set; }

        // Room tracking
        private string _currentRoomName;
        private bool _isInPrivateRoom;

        /// <summary>The current reason VoicePatrol is muted, or NotMuted if actively monitoring.</summary>
        public MuteReason CurrentMuteReason { get; private set; } = MuteReason.NotInitialized;

        // VoicePatrol unmutes when in a voice room with 2+ players.
        private const int MIN_PLAYERS_TO_UNMUTE = 1;

        /// <summary>
        /// Minimum room player count required before VoicePatrol unmutes and
        /// begins monitoring. Defaults to 2 (no point moderating when alone).
        /// Override to change the threshold — e.g. a measurement mode that needs
        /// to run with a single player present.
        /// </summary>
        protected virtual int MinPlayersToUnmute => MIN_PLAYERS_TO_UNMUTE;

        // Encryption — AES-128-CBC
        private const string SAVE_KEY = "VoicePatrol_Data";
        private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("VP_LocalKey_0123");
        private static readonly byte[] EncryptionIV  = Encoding.UTF8.GetBytes("VP_LocalIV__0123");

        #endregion

        #region Abstract Methods — Implement per voice provider

        /// <summary>
        /// Display name of the voice provider, used in log messages.
        /// Example: "Photon", "Vivox", "Dissonance"
        /// </summary>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Called once during <see cref="Initialize"/>. Subscribe to your voice provider's
        /// state change events here (e.g., Photon Client.StateChanged, Vivox login events).
        /// </summary>
        protected abstract void SubscribeToVoiceProvider();

        /// <summary>
        /// Called during <see cref="OnDestroy"/>. Unsubscribe from your voice provider's events.
        /// </summary>
        protected abstract void UnsubscribeFromVoiceProvider();

        /// <summary>
        /// Returns true if the local player's microphone is currently transmitting
        /// through the voice provider (i.e., recorder is enabled and connected).
        /// </summary>
        protected abstract bool IsVoiceTransmitting();

        /// <summary>
        /// Returns the current player count in the voice room, or 0 if not in a room.
        /// </summary>
        protected abstract int GetCurrentPlayerCount();

        /// <summary>
        /// Called when a punishment mute is applied. Use this to force-disable the voice
        /// provider's recorder/transmitter to prevent any audio transmission.
        /// </summary>
        protected abstract void ForceProviderMute();

        /// <summary>
        /// Called when a punishment mute expires. Use this to re-enable the voice
        /// provider's recorder/transmitter so the player can speak again.
        /// </summary>
        protected abstract void RestoreProviderMute();

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LogPrefix = $"<color=#00D5FF>[VoicePatrol:{ProviderName}]</color>";
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                VoicePatrolWrapper.OnViolationDetected -= HandleViolation;
                VoicePatrolWrapper.OnAgeVerified -= HandleAgeVerification;
                UnsubscribeFromVoiceProvider();
            }
        }

        protected virtual void Update()
        {
            if (!_isInitialized || !IsDataLoaded) return;

            MaintainMonotonicMuteDeadline();

            if (HasActiveMuteRecord() && !IsPunishmentMuted())
            {
                Log("Punishment mute expired");
                ClearPunishmentMute();
            }
        }

        #endregion

        #region Public API — Initialization

        /// <summary>
        /// Initializes VoicePatrol and begins monitoring.
        /// Call this once after the player has logged in / authenticated.
        /// </summary>
        /// <param name="uniqueId">
        /// Unique player ID used in violation reports. If null, falls back to device ID.
        /// Passed through to the SDK unchanged — VoicePatrol never alters it or pushes it
        /// onto the voice provider.
        ///
        /// <b>Headless (server-to-server) setups:</b> audio delivered to VoicePatrol by the
        /// voice engine's servers is keyed by the engine's own identity, so your Photon
        /// Client.UserId / Vivox PlayerId must equal this ID for callbacks to match this
        /// player. Both providers allow setting their identity at login, before joining any
        /// room — do that, or pass the provider's identity here. The integration warns at
        /// runtime if headless mode is on and the two don't match.
        /// </param>
        public void Initialize(string uniqueId = null)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"{LogPrefix} Already initialized");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogError($"{LogPrefix} API key is not set. Please configure it in the inspector.");
                return;
            }

            _isInitialized = true;
            UniqueId = uniqueId ?? SystemInfo.deviceUniqueIdentifier; // mirror the SDK's fallback

            VoicePatrolWrapper.OnViolationDetected += HandleViolation;
            VoicePatrolWrapper.OnAgeVerified += HandleAgeVerification;

            LoadViolationData();
            SubscribeToVoiceProvider();

            string result = StartSdk(_apiKey, uniqueId, _ageVerification, _debugLogs);
            Log($"SDK started: {result}");

            InvokeRepeating(nameof(UpdateMutedState), 1f, 1f);
        }

        /// <summary>
        /// Starts the underlying SDK. Override to start in an alternate mode
        /// (e.g. an integration that supplies a voice engine's audio instead of opening
        /// the microphone). The default starts the standard moderation path with the
        /// inspector configuration.
        /// </summary>
        protected virtual string StartSdk(string apiKey, string uniqueId, bool ageVerification, bool debugLogs)
        {
            return VoicePatrolWrapper.StartVoicePatrol(
                apiKey,
                uniqueID: uniqueId,
                ageVerification: ageVerification,
                debugLogs: debugLogs
            );
        }

        /// <summary>
        /// Call when the player attempts to unmute themselves via your game's UI.
        /// Returns true if unmute is allowed, false if blocked by punishment mute.
        /// </summary>
        public bool TryUnmute()
        {
            if (IsPunishmentMuted())
            {
                TimeSpan remaining = GetRemainingPunishmentDuration();
                Debug.LogWarning($"{LogPrefix} Unmute blocked — punishment active. " +
                                 $"{remaining.TotalMinutes:F0} min remaining. Reason: {MuteReasonText}");

                ForcePunishmentMuteState();
                OnUnmuteBlockedByPunishment?.Invoke(MuteReasonText, remaining);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Mutes or unmutes VoicePatrol on demand, independent of the voice provider's state.
        /// While set, the SDK stays muted even when the provider is actively transmitting —
        /// e.g. the player remains in a Photon Voice room with the recorder enabled. Use this
        /// instead of calling <c>VoicePatrolWrapper.SetMuted</c> directly, which the automatic
        /// mute evaluation would revert within a second.
        ///
        /// The voice provider's recorder is not touched, so players keep talking to each other
        /// while moderation is paused. Passing false returns control to the automatic
        /// evaluation; it does not force an unmute (a punishment mute, private room, etc.
        /// still apply).
        /// </summary>
        public void SetForceMuted(bool muted)
        {
            if (IsForceMuted == muted) return;

            IsForceMuted = muted;
            Log(muted ? "Force mute applied" : "Force mute cleared");
            UpdateMutedState();
        }

        #endregion

        #region Public API — Queries

        /// <summary>True while a force mute set via <see cref="SetForceMuted"/> is active.</summary>
        public bool IsForceMuted { get; private set; }

        public bool IsPunishmentMuted() =>
            MuteEndTime > DateTime.UtcNow ||
            (_monotonicMuteEnd >= 0 && Time.realtimeSinceStartup < _monotonicMuteEnd);

        public TimeSpan GetRemainingPunishmentDuration()
        {
            if (!IsPunishmentMuted()) return TimeSpan.Zero;

            TimeSpan remaining = MuteEndTime - DateTime.UtcNow;

            if (_monotonicMuteEnd >= 0)
            {
                TimeSpan monotonic = TimeSpan.FromSeconds(_monotonicMuteEnd - Time.realtimeSinceStartup);
                if (monotonic > remaining) remaining = monotonic;
            }

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public string GetPunishmentMuteReason() => MuteReasonText;

        public int GetViolationCount() => ViolationCount;

        public bool IsCurrentRoomPrivate() => _isInPrivateRoom;

        #endregion

        #region Public API — Debug

        public void ResetViolationData()
        {
            bool wasMuted = IsPunishmentMuted();

            ViolationCount = 0;
            MuteEndTime = DateTime.MinValue;
            _monotonicMuteEnd = -1;
            MuteReasonText = "";
            SaveViolationData();

            if (wasMuted)
                RestoreProviderMute();

            OnViolationCountChanged?.Invoke(0);
            Log("Violation data reset");
        }

        #endregion

        #region Room State — Called by subclasses

        /// <summary>
        /// Call from subclass when the local player joins a voice room/channel.
        /// </summary>
        protected void NotifyRoomJoined(string roomName)
        {
            _currentRoomName = roomName;
            _isInPrivateRoom = IsPrivateRoom(roomName);

            if (_isInPrivateRoom)
            {
                Log($"Joined private room '{roomName}' — moderation disabled");
            }
            else
            {
                VoicePatrolWrapper.OnJoinedRoom(roomName);
                Log($"Joined room '{roomName}'");
            }

            UpdateMutedState();
        }

        /// <summary>
        /// Call from subclass when the local player leaves a voice room/channel.
        /// </summary>
        protected void NotifyRoomLeft()
        {
            if (!_isInPrivateRoom && !string.IsNullOrEmpty(_currentRoomName))
            {
                VoicePatrolWrapper.OnLeftRoom();
                Log($"Left room '{_currentRoomName}'");
            }

            _currentRoomName = null;
            _isInPrivateRoom = false;
            UpdateMutedState();
        }

        #endregion

        #region Muted State Management

        private void UpdateMutedState()
        {
            if (!_isInitialized) return;

            MuteReason reason = EvaluateMuteReason();

            // Force mute is a floor, not an override: more specific reasons still win,
            // but the SDK can't unmute while it's set — including when a provider-specific
            // EvaluateMuteReason (e.g. Photon) reports NotMuted because the recorder is
            // still active in a voice room.
            if (reason == MuteReason.NotMuted && IsForceMuted)
                reason = MuteReason.ForceMuted;

            CurrentMuteReason = reason;
            VoicePatrolWrapper.SetMuted(CurrentMuteReason != MuteReason.NotMuted);

            if (CurrentMuteReason == MuteReason.PunishmentMute)
                ForceProviderMute();
        }

        protected virtual MuteReason EvaluateMuteReason()
        {
            if (_enableAutomatedMuting && IsPunishmentMuted())
                return MuteReason.PunishmentMute;

            if (_isInPrivateRoom)
                return MuteReason.PrivateRoom;

            if (!IsVoiceTransmitting())
            {
                // Distinguish between "not connected" and "connected but not transmitting"
                if (GetCurrentPlayerCount() == 0)
                    return MuteReason.NotConnected;

                return MuteReason.NotTransmitting;
            }

            if (GetCurrentPlayerCount() < MinPlayersToUnmute)
                return MuteReason.InsufficientPlayers;

            return MuteReason.NotMuted;
        }

        private bool IsPrivateRoom(string roomName)
        {
            if (!_enablePrivateRoomFiltering || string.IsNullOrEmpty(_privateRoomIdentifier))
                return false;

            return roomName != null &&
                   roomName.IndexOf(_privateRoomIdentifier, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Violation Handling

        private void HandleViolation(VoicePatrolWrapper.ViolationEventData violationData)
        {
            if (!IsDataLoaded)
            {
                Debug.LogWarning($"{LogPrefix} Violation received before data loaded — ignoring");
                return;
            }

            string labels = string.Join(", ", violationData.Labels);
            string violationInfo = $"Priority: {violationData.Priority}, Labels: {labels}";

            if (!_enableAutomatedMuting)
            {
                Log($"Violation detected (automated muting disabled) — {violationInfo}");
                return;
            }

            ViolationCount++;

            Log($"Violation #{ViolationCount} — {violationInfo}");
            OnViolationCountChanged?.Invoke(ViolationCount);

            MuteRule rule = GetRuleForViolation(ViolationCount);

            switch (rule.action)
            {
                case MuteAction.LogOnly:
                    Log($"Offense #{ViolationCount} — Log only");
                    SaveViolationData();
                    break;

                case MuteAction.WarnOnly:
                    Log($"Offense #{ViolationCount} — Warning issued");
                    SaveViolationData();
                    OnWarningIssued?.Invoke(violationInfo);
                    break;

                case MuteAction.Mute:
                    int minutes = Mathf.Min(rule.muteDurationMinutes, _maxMuteDurationMinutes);
                    Log($"Offense #{ViolationCount} — Muting for {minutes} min");
                    ApplyPunishmentMute(minutes, violationInfo);
                    break;
            }
        }

        private void HandleAgeVerification(VoicePatrolWrapper.AgeVerificationData data)
        {
            Log($"Age verification: {data.UniqueId} — age range {data.MinAge}-{data.MaxAge}");
        }

        private MuteRule GetRuleForViolation(int violationNumber)
        {
            int index = violationNumber - 1;

            if (index < _muteRules.Count)
                return _muteRules[index];

            return new MuteRule
            {
                action = MuteAction.Mute,
                muteDurationMinutes = _maxMuteDurationMinutes
            };
        }

        #endregion

        #region Punishment Mute

        private void ApplyPunishmentMute(int minutes, string reason)
        {
            MuteEndTime = DateTime.UtcNow.AddMinutes(minutes);
            _monotonicMuteEnd = Time.realtimeSinceStartup + minutes * 60.0;
            MuteReasonText = $"Violation: {reason}";

            Log($"Muted until {MuteEndTime:u} — {MuteReasonText}");

            SaveViolationData();
            ForcePunishmentMuteState();

            OnMuteApplied?.Invoke(MuteEndTime, MuteReasonText);
        }

        /// <summary>
        /// Forces the mute state on both the SDK and voice provider.
        /// Call from <see cref="LoadViolationData"/> overrides when restoring an active punishment mute.
        /// </summary>
        protected void ForcePunishmentMuteState()
        {
            if (VoicePatrolWrapper.IsSdkRunning())
                VoicePatrolWrapper.SetMuted(true);

            ForceProviderMute();
        }

        private void ClearPunishmentMute()
        {
            MuteEndTime = DateTime.MinValue;
            _monotonicMuteEnd = -1;
            MuteReasonText = "";
            SaveViolationData();
            RestoreProviderMute();

            OnMuteExpired?.Invoke();
            Log("Punishment mute expired and cleared");
        }

        private bool HasActiveMuteRecord() => MuteEndTime != DateTime.MinValue;

        /// <summary>
        /// Keeps the monotonic mute deadline in sync with the wall-clock record. Runs every
        /// frame from Update: anchors the deadline once a mute record is present (covering
        /// mutes restored by any LoadViolationData implementation, not just ApplyPunishmentMute),
        /// and when the wall clock claims expiry while monotonic time disagrees — i.e. the
        /// device clock was moved forward — re-anchors and re-saves the persisted record so
        /// the mute keeps its true remaining duration even across a restart.
        /// </summary>
        private void MaintainMonotonicMuteDeadline()
        {
            if (!HasActiveMuteRecord())
            {
                _monotonicMuteEnd = -1;
                return;
            }

            bool wallClockActive = MuteEndTime > DateTime.UtcNow;

            if (_monotonicMuteEnd < 0)
            {
                if (wallClockActive)
                    _monotonicMuteEnd = Time.realtimeSinceStartup + (MuteEndTime - DateTime.UtcNow).TotalSeconds;
                return;
            }

            // Tolerance guards the natural-expiry race: both clocks run out within the same
            // frame, and sub-second skew between them must not read as a clock jump. Real
            // jumps are minutes or more, so seconds of slack sacrifices nothing.
            const double CLOCK_JUMP_TOLERANCE_SECONDS = 5.0;

            if (!wallClockActive && Time.realtimeSinceStartup < _monotonicMuteEnd - CLOCK_JUMP_TOLERANCE_SECONDS)
            {
                double remainingSeconds = _monotonicMuteEnd - Time.realtimeSinceStartup;
                MuteEndTime = DateTime.UtcNow.AddSeconds(remainingSeconds);
                SaveViolationData();
                Log($"Device clock jump detected — punishment mute re-anchored " +
                    $"({remainingSeconds / 60.0:F0} min remaining)");
            }
        }

        #endregion

        #region Local Storage (Override to integrate your own backend)

        /// <summary>
        /// Saves violation data locally with AES-128 encryption.
        /// Override to save to your own database/backend instead.
        ///
        /// When overriding, read the current state from the protected fields:
        /// <see cref="ViolationCount"/>, <see cref="MuteEndTime"/>, and <see cref="MuteReasonText"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void SaveViolationData()
        /// {
        ///     MyBackend.Save(ViolationCount, MuteEndTime.Ticks, MuteReasonText);
        /// }
        /// </code>
        /// </example>
        protected virtual void SaveViolationData()
        {
            try
            {
                ViolationSaveData data = new()
                {
                    violationCount = ViolationCount,
                    muteEndTimeTicks = MuteEndTime.Ticks,
                    muteReason = MuteReasonText ?? ""
                };

                string json = JsonUtility.ToJson(data);
                string encrypted = Encrypt(json);
                PlayerPrefs.SetString(SAVE_KEY, encrypted);
                PlayerPrefs.Save();

                Log($"Violation data saved (count={ViolationCount})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to save violation data: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads violation data from local encrypted storage.
        /// Override to load from your own database/backend instead.
        ///
        /// When overriding, set the protected fields directly:
        /// <see cref="ViolationCount"/>, <see cref="MuteEndTime"/>, <see cref="MuteReasonText"/>.
        ///
        /// <b>Important:</b> You MUST set <see cref="IsDataLoaded"/> = true when loading
        /// completes (including on failure), or violations will be silently ignored.
        /// If the player has an active mute, call <c>ForcePunishmentMuteState()</c> after loading.
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void LoadViolationData()
        /// {
        ///     MyBackend.Load(data =>
        ///     {
        ///         ViolationCount = data.violations;
        ///         MuteEndTime = data.muteEndUtc;
        ///         MuteReasonText = data.reason;
        ///         IsDataLoaded = true;
        ///
        ///         if (IsPunishmentMuted())
        ///             ForcePunishmentMuteState();
        ///     },
        ///     onError: _ => { IsDataLoaded = true; });
        /// }
        /// </code>
        /// </example>
        protected virtual void LoadViolationData()
        {
            try
            {
                if (!PlayerPrefs.HasKey(SAVE_KEY))
                {
                    Log("No saved violation data found — starting fresh");
                    IsDataLoaded = true;
                    return;
                }

                string raw = PlayerPrefs.GetString(SAVE_KEY);
                string json = Decrypt(raw);

                if (json == null)
                {
                    Debug.LogWarning($"{LogPrefix} Failed to decrypt violation data — resetting");
                    IsDataLoaded = true;
                    return;
                }

                ViolationSaveData data = JsonUtility.FromJson<ViolationSaveData>(json);

                ViolationCount = data.violationCount;
                MuteReasonText = data.muteReason ?? "";

                if (data.muteEndTimeTicks > 0)
                    MuteEndTime = new DateTime(data.muteEndTimeTicks, DateTimeKind.Utc);

                IsDataLoaded = true;

                Log($"Loaded violation data: count={ViolationCount}, " +
                    $"muteEnd={MuteEndTime:u}, isMuted={IsPunishmentMuted()}");

                if (IsPunishmentMuted())
                    ForcePunishmentMuteState();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to load violation data: {ex.Message}");
                IsDataLoaded = true;
            }
        }

        #endregion

        #region Encryption Helpers

        private static string Encrypt(string plainText)
        {
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = EncryptionKey;
                aes.IV = EncryptionIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoicePatrol] Encryption failed: {ex.Message}");
                return plainText;
            }
        }

        private static string Decrypt(string cipherText)
        {
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = EncryptionKey;
                aes.IV = EncryptionIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
#else
    public abstract class VoicePatrolBase : MonoBehaviour { }
#endif
}