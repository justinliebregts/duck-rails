// VoicePatrol — Photon Voice Legacy Integration
//
// Requires the Photon Voice PUN integration (Photon.Voice.PUN.PhotonVoiceNetwork). Uses
// the legacy PUN-based PhotonVoiceNetwork singleton instead of the newer VoiceConnection
// approach.
//
// Gated on VOICEPATROL_PHOTON_VOICE_PUN, a voice-capability symbol managed by
// VoicePatrolDefineSymbols. With no asmdefs, add it under Project Settings > Player >
// Scripting Define Symbols when the Photon Voice PUN integration is present.
//
// Maintainers: this must stay a *voice* symbol, never a networking one
// (PUN_2_OR_NEWER / FUSION_WEAVER) — see VoicePatrolDefineSymbols for the rationale.

#if VOICEPATROL_PHOTON_VOICE_PUN
using Photon.Realtime;
using Photon.Voice.PUN;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Integrations.PhotonVoicePun
{
    /// <summary>
    /// VoicePatrol integration for Photon Voice Legacy (PUN-based PhotonVoiceNetwork).
    ///
    /// Uses <see cref="PhotonVoiceNetwork.Instance"/> singleton to detect room join/leave
    /// and manage voice state.
    ///
    /// <b>Setup:</b>
    /// 1. Add VoicePatrolWrapper and this component to a persistent GameObject (or use the prefab).
    /// 2. Set your API key in the inspector.
    /// 3. Call <see cref="VoicePatrolBase.Initialize"/> once the player has logged in.
    ///
    /// No manual room event forwarding is required — Photon state changes are detected automatically.
    /// </summary>
    [AddComponentMenu("VoicePatrol/VoicePatrol Photon Legacy")]
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    public class VoicePatrolPhotonLegacy : VoicePatrolBase
    {
        private bool _subscribedToPhoton;

        // When false (the default), the SDK opens the device microphone and moderates its
        // audio (the legacy integration has no outgoing-stream tap). When true (headless /
        // server-to-server), the microphone stays closed and no audio is sent from this
        // client — Photon's servers deliver voice to VoicePatrol directly, matched to this
        // player by unique ID (Photon's Client.UserId must equal it).
        [Tooltip("Server-to-server mode: no audio is sent from this client; Photon's servers " +
                 "stream voice directly to VoicePatrol. Set Photon's Client.UserId to the same " +
                 "unique ID you pass to Initialize (Photon allows this before joining a room) " +
                 "so the delivered audio matches this player.")]
        [SerializeField] private bool _headless;

        // One-shot guard for the headless identity warning; re-arms if Photon's UserId changes.
        private string _warnedHeadlessMismatchId;

        /// <summary>
        /// Server-to-server (headless) mode. Set before <see cref="VoicePatrolBase.Initialize"/>;
        /// changing it after the SDK has started has no effect on the running session.
        /// </summary>
        public bool Headless
        {
            get => _headless;
            set => _headless = value;
        }

        public override string ProviderName => "PhotonLegacy";

        protected override string StartSdk(string apiKey, string uniqueId, bool ageVerification, bool debugLogs)
        {
            // In headless mode the microphone stays closed and no audio is sent from this
            // client — Photon's servers deliver it. Otherwise the legacy integration opens
            // the SDK microphone as it always has (it predates the outgoing-stream tap).
            return VoicePatrolWrapper.StartVoicePatrol(
                apiKey, uniqueID: uniqueId, ageVerification: ageVerification,
                disableMicrophone: _headless, debugLogs: debugLogs);
        }

        #region Voice Provider Implementation

        protected override void SubscribeToVoiceProvider()
        {
            TrySubscribeToPhoton();
        }

        protected override void UnsubscribeFromVoiceProvider()
        {
            if (!_subscribedToPhoton) return;

            var instance = PhotonVoiceNetwork.Instance;
            if (instance == null || instance.Client == null) return;

            instance.Client.StateChanged -= OnPhotonStateChanged;
            _subscribedToPhoton = false;
        }

        protected override bool IsVoiceTransmitting()
        {
            var instance = PhotonVoiceNetwork.Instance;

            if (instance == null ||
                instance.ClientState != ClientState.Joined)
                return false;

            return instance.PrimaryRecorder != null &&
                   instance.PrimaryRecorder.IsRecording;
        }

        protected override int GetCurrentPlayerCount()
        {
            var instance = PhotonVoiceNetwork.Instance;

            if (instance == null ||
                instance.Client == null ||
                instance.Client.CurrentRoom == null)
                return 0;

            return instance.Client.CurrentRoom.PlayerCount;
        }

        protected override void ForceProviderMute()
        {
            var instance = PhotonVoiceNetwork.Instance;

            if (instance != null && instance.PrimaryRecorder != null)
                instance.PrimaryRecorder.IsRecording = false;
        }

        protected override void RestoreProviderMute()
        {
            var instance = PhotonVoiceNetwork.Instance;

            if (instance != null && instance.PrimaryRecorder != null)
                instance.PrimaryRecorder.IsRecording = true;
        }

        #endregion

        #region Photon State Tracking

        protected override void Update()
        {
            base.Update();

            if (_subscribedToPhoton && PhotonVoiceNetwork.Instance == null)
            {
                _subscribedToPhoton = false;
                Log("PhotonVoiceNetwork lost — will re-subscribe when available");
            }

            if (!_subscribedToPhoton)
                TrySubscribeToPhoton();

            if (_headless)
                WarnIfHeadlessIdentityMismatch();
        }

        // In headless mode, Photon's servers deliver this player's audio to VoicePatrol
        // keyed by Photon's Client.UserId — if that doesn't equal the VoicePatrol unique ID,
        // the audio can't be matched to this player. Never forces anything onto Photon;
        // warns once per mismatched value so the misconfiguration is loud instead of silent.
        private void WarnIfHeadlessIdentityMismatch()
        {
            if (string.IsNullOrEmpty(UniqueId)) return; // not initialized yet

            var instance = PhotonVoiceNetwork.Instance;
            string providerId = instance != null && instance.Client != null
                ? instance.Client.UserId
                : null;
            if (string.IsNullOrEmpty(providerId)) return; // not connected yet

            if (providerId == UniqueId)
            {
                _warnedHeadlessMismatchId = null;
                return;
            }

            if (providerId == _warnedHeadlessMismatchId) return;
            _warnedHeadlessMismatchId = providerId;

            Debug.LogWarning($"{LogPrefix} Headless mode: Photon UserId ('{providerId}') doesn't match the " +
                             $"VoicePatrol unique ID ('{UniqueId}'). Audio delivered by Photon's servers won't be " +
                             "matched to this player — set Client.UserId to the same unique ID at login, " +
                             "before joining a room.");
        }

        private void TrySubscribeToPhoton()
        {
            if (_subscribedToPhoton) return;

            var instance = PhotonVoiceNetwork.Instance;
            if (instance == null || instance.Client == null) return;

            instance.Client.StateChanged += OnPhotonStateChanged;
            _subscribedToPhoton = true;

            Log("Subscribed to Photon Voice Legacy state changes");

            if (instance.ClientState == ClientState.Joined &&
                instance.Client.CurrentRoom != null)
            {
                NotifyRoomJoined(instance.Client.CurrentRoom.Name);
            }
        }

        private void OnPhotonStateChanged(ClientState fromState, ClientState toState)
        {
            if (toState == ClientState.Joined)
            {
                var instance = PhotonVoiceNetwork.Instance;
                if (instance != null && instance.Client.CurrentRoom != null)
                {
                    NotifyRoomJoined(instance.Client.CurrentRoom.Name);
                }
            }
            else if (fromState == ClientState.Joined)
            {
                NotifyRoomLeft();
            }
        }

        #endregion
    }
#else
    public class VoicePatrolPhotonLegacy : VoicePatrolBase { }
#endif
}
#endif