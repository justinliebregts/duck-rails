// VoicePatrol — Photon Voice Integration
//
// Requires Photon Voice (the PhotonVoice / PhotonRealtime assemblies). Uses the modern
// VoiceConnection-based integration; independent of your networking layer (Fusion, PUN2,
// Quantum, or none).
//
// Gated on VOICEPATROL_PHOTON_VOICE, a voice-capability symbol managed by
// VoicePatrolDefineSymbols. With no asmdefs, add it under
// Project Settings > Player > Scripting Define Symbols when Photon Voice is present.
//
// Maintainers: this must stay a *voice* symbol, never a networking one
// (FUSION_WEAVER / PUN_2_OR_NEWER) — see VoicePatrolDefineSymbols for the rationale.

#if VOICEPATROL_PHOTON_VOICE
using System;
using System.Reflection;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Integrations.PhotonVoice
{
    /// <summary>
    /// VoicePatrol integration for Photon Voice.
    /// Automatically subscribes to Photon Voice's Client.StateChanged to detect room join/leave.
    ///
    /// <b>Setup:</b>
    /// 1. Add VoicePatrolWrapper and this component to a persistent GameObject (or use the prefab).
    /// 2. Set your API key in the inspector.
    /// 3. Call <see cref="VoicePatrolBase.Initialize"/> once the player has logged in.
    ///
    /// No manual room event forwarding is required — Photon state changes are detected automatically.
    /// </summary>
    [AddComponentMenu("VoicePatrol/VoicePatrol Photon")]
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    public class VoicePatrolPhoton : VoicePatrolBase
    {
        private VoiceConnection _voiceConnection;
        private bool _subscribedToPhoton;

        // When false (the default), the player's outgoing Photon voice is tapped (in Update)
        // and sent to VoicePatrol for moderation. When true (headless / server-to-server),
        // no audio is sent from this client — Photon's servers deliver voice to VoicePatrol
        // directly, matched to this player by unique ID (Photon's Client.UserId must equal
        // it). The device microphone is never opened either way.
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

        // Photon outgoing-stream tap state. The SDK never opens its own microphone; these
        // track the tap attached to Photon's outgoing local voice. The tap itself is owned
        // by Photon's processor pipeline once added (AddPostProcessor keeps a strong
        // reference for the life of the local voice), so we deliberately don't hold one.
        private Func<object> _localVoiceGetter;      // cached accessor for the recorder's local voice
        private bool _tapAttached;
        private object _tappedVoice;                 // instance the tap was added to; RestartRecording replaces it

        public override string ProviderName => "Photon";

        #region Voice Provider Implementation

        protected override void SubscribeToVoiceProvider()
        {
            TrySubscribeToPhoton();
        }

        protected override void UnsubscribeFromVoiceProvider()
        {
            if (!_subscribedToPhoton || _voiceConnection == null || _voiceConnection.Client == null) return;

            _voiceConnection.Client.StateChanged -= OnPhotonStateChanged;
            _subscribedToPhoton = false;
        }

        protected override bool IsVoiceTransmitting()
        {
            if (_voiceConnection == null ||
                _voiceConnection.Client == null ||
                _voiceConnection.Client.State != ClientState.Joined)
                return false;

            return _voiceConnection.PrimaryRecorder != null &&
                   _voiceConnection.PrimaryRecorder.RecordingEnabled;
        }

        protected override int GetCurrentPlayerCount()
        {
            if (_voiceConnection == null ||
                _voiceConnection.Client == null ||
                _voiceConnection.Client.CurrentRoom == null)
                return 0;

            return _voiceConnection.Client.CurrentRoom.PlayerCount;
        }

        protected override void ForceProviderMute()
        {
            if (_voiceConnection != null && _voiceConnection.PrimaryRecorder != null)
                _voiceConnection.PrimaryRecorder.RecordingEnabled = false;
        }

        protected override void RestoreProviderMute()
        {
            if (_voiceConnection != null && _voiceConnection.PrimaryRecorder != null)
                _voiceConnection.PrimaryRecorder.RecordingEnabled = true;
        }

        protected override string StartSdk(string apiKey, string uniqueId, bool ageVerification, bool debugLogs)
        {
            // Photon never opens the device microphone. By default the player's outgoing
            // Photon voice is tapped (in Update) and sent to VoicePatrol for moderation;
            // in headless mode, nothing is sent from this client. Either way the mic
            // stays off.
            return VoicePatrolWrapper.StartVoicePatrol(
                apiKey, uniqueID: uniqueId, ageVerification: ageVerification,
                disableMicrophone: true, debugLogs: debugLogs);
        }

        // Photon ingests whatever reaches the Photon servers, so VoicePatrol applies no
        // player-count or transmission gate of its own — the developer stays in control of
        // when an outgoing stream exists (e.g. only above a player threshold). The only
        // monitoring exceptions are a private room, an active punishment mute, and a
        // force mute (SetForceMuted — applied by the base after this evaluation,
        // so it holds even while the recorder is active in a voice room).
        protected override MuteReason EvaluateMuteReason()
        {
            if (IsPunishmentMuted())
                return MuteReason.PunishmentMute;

            if (IsCurrentRoomPrivate())
                return MuteReason.PrivateRoom;

            return MuteReason.NotMuted;
        }

        #endregion

        #region Photon State Tracking

        protected override void Update()
        {
            base.Update();

            if (_subscribedToPhoton && _voiceConnection == null)
            {
                _subscribedToPhoton = false;
                ResetTap(); // recorder/local voice went with the old connection — re-tap the new one
                Log("VoiceConnection lost — will re-subscribe when available");
            }

            if (!_subscribedToPhoton)
                TrySubscribeToPhoton();

            // Headless mode: no audio is sent from this client, so the tap is never
            // attached. Defaults off, so the tap attaches as normal.
            // RestartRecording replaces the local voice and leaves the old tap behind.
            if (!_headless && _tapAttached && !IsTapOnCurrentVoice())
            {
                Log("Photon tap dropped — local voice was replaced");
                ResetTap();
            }
            if (!_headless && !_tapAttached)
                TryAttachTap();

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

            string providerId = _voiceConnection != null && _voiceConnection.Client != null
                ? _voiceConnection.Client.UserId
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

            FindVoiceConnection();

            if (_voiceConnection == null || _voiceConnection.Client == null) return;

            _voiceConnection.Client.StateChanged += OnPhotonStateChanged;
            _subscribedToPhoton = true;

            Log("Subscribed to Photon Voice state changes");

            if (_voiceConnection.Client.State == ClientState.Joined &&
                _voiceConnection.Client.CurrentRoom != null)
            {
                NotifyRoomJoined(_voiceConnection.Client.CurrentRoom.Name);
            }
        }

        private void OnPhotonStateChanged(ClientState fromState, ClientState toState)
        {
            if (toState == ClientState.Joined && _voiceConnection.Client.CurrentRoom != null)
            {
                NotifyRoomJoined(_voiceConnection.Client.CurrentRoom.Name);
            }
            else if (fromState == ClientState.Joined)
            {
                ResetTap(); // leaving the room tears down the local voice — re-tap on the next join
                NotifyRoomLeft();
            }
        }

        private void FindVoiceConnection()
        {
            if (_voiceConnection == null)
                _voiceConnection = FindObjectOfType<VoiceConnection>();
        }

        #endregion

        #region Photon Outgoing-Stream Tap

        /// <summary>
        /// Passive Photon Voice processor for a float pipeline: copies each outgoing
        /// PCM frame into the VoicePatrol SDK via the supplied push and returns it
        /// unchanged, so it never affects what Photon transmits. Process() runs on
        /// Photon's audio thread.
        /// </summary>
        private sealed class PhotonTapFloat : Photon.Voice.IProcessor<float>
        {
            private readonly int _sampleRate;
            private readonly Action<float[], int, int, int> _push;
            public PhotonTapFloat(int sampleRate, Action<float[], int, int, int> push)
            {
                _sampleRate = sampleRate;
                _push = push;
            }

            public float[] Process(float[] buf)
            {
                _push(buf, buf.Length, _sampleRate, 1);
                return buf;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// Same tap for a short pipeline (Photon's default sample type on many
        /// setups). Converts to normalized float before forwarding; passes the
        /// original short buffer through untouched.
        /// </summary>
        private sealed class PhotonTapShort : Photon.Voice.IProcessor<short>
        {
            private readonly int _sampleRate;
            private readonly Action<float[], int, int, int> _push;
            public PhotonTapShort(int sampleRate, Action<float[], int, int, int> push)
            {
                _sampleRate = sampleRate;
                _push = push;
            }

            public short[] Process(short[] buf)
            {
                // Exact-sized buffer: the P/Invoke marshals the whole array and the
                // SDK reads `length` samples from it.
                var f = new float[buf.Length];
                for (int i = 0; i < buf.Length; i++) f[i] = buf[i] / 32768f;
                _push(f, f.Length, _sampleRate, 1);
                return buf;
            }

            public void Dispose() { }
        }

        // Drops the tap so Update re-attaches it to the next local voice. Called when the
        // VoiceConnection is destroyed (Fusion recreates it per room) or the room is left:
        // the old recorder/local voice — and the processor we added to it — are gone, and
        // _localVoiceGetter is bound to that old recorder, so it must be re-resolved.
        private void ResetTap()
        {
            _tapAttached = false;
            _tappedVoice = null;
            _localVoiceGetter = null;
        }

        // True only while the recorder is still using the voice instance we tapped.
        private bool IsTapOnCurrentVoice()
        {
            FindVoiceConnection();
            if (_voiceConnection == null || _voiceConnection.PrimaryRecorder == null)
                return false;

            object current = GetRealLocalVoice(_voiceConnection.PrimaryRecorder);
            return current != null && ReferenceEquals(current, _tappedVoice);
        }

        /// <summary>
        /// Attaches the tap to the recorder's outgoing local voice once Photon has
        /// created a real one. Retried every Update: the recorder only creates a
        /// real LocalVoiceAudio[Float|Short] when it actually starts transmitting —
        /// before that, Recorder.LocalVoice returns a LocalVoiceAudioDummy, which we
        /// skip and keep waiting on. The accessor + AddPostProcessor are resolved
        /// reflectively (version-independent); the IProcessor&lt;T&gt; shapes are stable.
        /// </summary>
        private void TryAttachTap()
        {
            FindVoiceConnection();
            if (_voiceConnection == null) return;

            var recorder = _voiceConnection.PrimaryRecorder;
            if (recorder == null) return;

            object localVoice = GetRealLocalVoice(recorder);
            if (localVoice == null) return; // still the dummy — not transmitting yet; retry next frame

            int sampleRate = ResolveRecorderSampleRate(recorder);
            Type sampleType = GetLocalVoiceSampleType(localVoice);

            // Feed Photon's outgoing frames into the SDK for moderation.
            Action<float[], int, int, int> push = VoicePatrolWrapper.PushAudio;

            try
            {
                var addPostProcessor = localVoice.GetType().GetMethod("AddPostProcessor");
                if (addPostProcessor == null)
                {
                    Debug.LogError($"{LogPrefix} Photon tap: AddPostProcessor not found on " +
                                   $"{localVoice.GetType().Name}. Verify your Photon Voice version.");
                    _tapAttached = true; // genuinely unexpected on a real voice — stop retrying
                    _tappedVoice = localVoice;
                    return;
                }

                if (sampleType == typeof(float))
                {
                    var tap = new PhotonTapFloat(sampleRate, push);
                    addPostProcessor.Invoke(localVoice,
                        new object[] { new Photon.Voice.IProcessor<float>[] { tap } });
                }
                else if (sampleType == typeof(short))
                {
                    var tap = new PhotonTapShort(sampleRate, push);
                    addPostProcessor.Invoke(localVoice,
                        new object[] { new Photon.Voice.IProcessor<short>[] { tap } });
                }
                else
                {
                    Debug.LogError($"{LogPrefix} Photon tap: unsupported local voice sample type " +
                                   $"'{sampleType?.Name ?? "unknown"}' on {localVoice.GetType().Name}.");
                    _tapAttached = true;
                    _tappedVoice = localVoice;
                    return;
                }

                _tapAttached = true;
                _tappedVoice = localVoice;
                Log($"Photon tap attached to {localVoice.GetType().Name} ({sampleRate} Hz)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Photon tap attach failed: {ex.Message}");
                _tapAttached = true;
                _tappedVoice = localVoice;
            }
        }

        // Returns the recorder's CURRENT real local voice, or null if it's still
        // the dummy (not transmitting yet). The member exposing the voice is
        // located once — the dummy itself reveals which member it is — then cached,
        // so the per-frame poll while waiting to transmit stays cheap.
        private object GetRealLocalVoice(object recorder)
        {
            if (_localVoiceGetter == null)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var prop in recorder.GetType().GetProperties(flags))
                {
                    if (prop.GetIndexParameters().Length != 0) continue;
                    if (LooksLikeLocalVoice(SafeGet(() => prop.GetValue(recorder))))
                    {
                        var p = prop;
                        _localVoiceGetter = () => SafeGet(() => p.GetValue(recorder));
                        break;
                    }
                }

                if (_localVoiceGetter == null)
                {
                    foreach (var field in recorder.GetType().GetFields(flags))
                    {
                        if (LooksLikeLocalVoice(SafeGet(() => field.GetValue(recorder))))
                        {
                            var fi = field;
                            _localVoiceGetter = () => SafeGet(() => fi.GetValue(recorder));
                            break;
                        }
                    }
                }
            }

            object current = _localVoiceGetter?.Invoke();
            if (current == null) return null;

            // Real voice = LocalVoiceAudioFloat / LocalVoiceAudioShort. The dummy
            // (LocalVoiceAudioDummy) means "not transmitting yet" — keep waiting.
            return current.GetType().Name.Contains("Dummy") ? null : current;
        }

        // Matches any LocalVoiceAudio* including the dummy — used only to locate the
        // accessor member, not to decide whether it's a real voice.
        private static bool LooksLikeLocalVoice(object o)
        {
            return o != null && o.GetType().Name.StartsWith("LocalVoiceAudio");
        }

        // Walks the type hierarchy for LocalVoiceAudio<T> and returns T (float/short).
        private static Type GetLocalVoiceSampleType(object localVoice)
        {
            for (Type t = localVoice.GetType(); t != null; t = t.BaseType)
            {
                if (t.IsGenericType)
                {
                    var args = t.GetGenericArguments();
                    if (args.Length == 1 && (args[0] == typeof(float) || args[0] == typeof(short)))
                        return args[0];
                }
            }
            // Fallback to the concrete type name if the generic base isn't visible.
            string n = localVoice.GetType().Name;
            if (n.Contains("Float")) return typeof(float);
            if (n.Contains("Short")) return typeof(short);
            return null;
        }

        private static int ResolveRecorderSampleRate(object recorder)
        {
            // Recorder.SamplingRate is an enum whose values equal the Hz
            // (Sampling48000 == 48000). Fall back to Photon's 48000 default.
            var prop = recorder.GetType().GetProperty("SamplingRate");
            object val = prop != null ? SafeGet(() => prop.GetValue(recorder)) : null;
            if (val != null)
            {
                try { return Convert.ToInt32(val); } catch { /* fall through to default */ }
            }
            return 48000;
        }

        private static object SafeGet(Func<object> getter)
        {
            try { return getter(); } catch { return null; }
        }

        #endregion
    }
#else
    public class VoicePatrolPhoton : VoicePatrolBase { }
#endif
}
#endif