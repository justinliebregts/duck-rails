// VoicePatrol — Vivox Integration
//
// Requires the Vivox Unity package (com.unity.services.vivox). Uses the modern
// VivoxService.Instance API (15.1.6+); the outgoing-audio tap additionally requires
// Vivox's audio-tap engine (com.unity.services.vivox 16.0.0+).
//
// Gated on VOICEPATROL_VIVOX, a voice-capability symbol managed by
// VoicePatrolDefineSymbols. With no asmdefs, add it under
// Project Settings > Player > Scripting Define Symbols when Vivox is present.
//
// Maintainers: this must stay a *voice* symbol, never a networking one — see
// VoicePatrolDefineSymbols for the rationale.

#if VOICEPATROL_VIVOX
using System;
using System.Collections;
using System.Reflection;
using Unity.Services.Vivox;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Integrations.Vivox
{
    /// <summary>
    /// VoicePatrol integration for Vivox (Unity Gaming Services).
    /// Automatically subscribes to VivoxService login/channel events to detect room join/leave.
    ///
    /// <b>Setup:</b>
    /// 1. Add VoicePatrolWrapper and this component to a persistent GameObject (or use the prefab).
    /// 2. Set your API key in the inspector.
    /// 3. Call <see cref="VoicePatrolBase.Initialize"/> once the player has logged in, passing
    ///    the player's unique ID (ideally the Vivox/Authentication PlayerId).
    ///
    /// No manual channel event forwarding is required — Vivox state changes are detected automatically.
    ///
    /// The device microphone is never opened by VoicePatrol. The player's outgoing Vivox
    /// capture stream is read directly from Vivox's audio-tap engine (the same mechanism
    /// behind Vivox's VivoxCaptureSourceTap component, minus its AudioSource playback) and
    /// sent to VoicePatrol for moderation — whatever the player transmits to Vivox is what
    /// gets moderated, and nothing is ever played back locally.
    /// </summary>
    [AddComponentMenu("VoicePatrol/VoicePatrol Vivox")]
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    public class VoicePatrolVivox : VoicePatrolBase
    {
        private bool _subscribedToVivox;
        private string _activeChannelName;

        // When false (the default), the player's outgoing Vivox capture is tapped and sent
        // to VoicePatrol for moderation. When true (headless / server-to-server), no audio
        // is sent from this client — Vivox's servers deliver voice to VoicePatrol directly,
        // matched to this player by unique ID (the Vivox PlayerId must equal it). The
        // device microphone is never opened either way.
        [Tooltip("Server-to-server mode: no audio is sent from this client; Vivox's servers " +
                 "stream voice directly to VoicePatrol. The unique ID you pass to Initialize " +
                 "must equal the Vivox/Authentication PlayerId — simplest is to pass " +
                 "AuthenticationService PlayerId as the unique ID after login.")]
        [SerializeField] private bool _headless;

        // One-shot guard for the headless identity warning; re-arms if the Vivox id changes.
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

        // Vivox outgoing-stream tap state. The SDK never opens its own microphone; capture
        // audio is pulled from Vivox's internal AudioTapBridge (resolved reflectively — it
        // only exists in Vivox 16.0.0+) on a 20 ms cadence, mirroring Vivox's own
        // VivoxAudioProcessor, and pushed into the SDK. No AudioSource is involved, so the
        // tapped audio can never be heard locally.
        private int _tapId = -1;
        private bool _tapAttemptedForChannelState; // re-armed by channel events / logout
        private float[] _tapBuffer;
        private Coroutine _tapPump;

        // Reflection handles into Unity.Services.Vivox internals (verified against 16.3.0).
        private static bool _bridgeResolved;
        private static bool _bridgeUnavailable;
        private static MethodInfo _registerCaptureTap;   // AudioTapBridge.RegisterTapForCaptureSource(uint, string)
        private static MethodInfo _unregisterTap;        // AudioTapBridge.UnregisterTap(int)
        private static Func<int, float[], int, int, int, int> _getCaptureAudio; // AudioTapBridge.GetCaptureSourceAudio
        private static PropertyInfo _lastChannelJoinedUri; // IVivoxServiceInternal.LastChannelJoinedUri
        private static int _noMoreDataCode;              // VivoxCoreInstance.VxErrorNoMoreData

        // Same buffer duration Vivox's own capture tap registers with, and the same 20 ms
        // pull cadence its processor uses. Each pull drains everything buffered since the
        // last one, so frame-rate hiccups don't lose audio.
        private const uint TAP_BUFFER_DURATION = 80000;
        private const int TAP_SAMPLE_RATE = 48000;
        private const int TAP_CHANNELS = 1;
        private const int TAP_FRAMES_PER_PULL = 960; // 20 ms at 48 kHz

        public override string ProviderName => "Vivox";

        #region Voice Provider Implementation

        protected override void SubscribeToVoiceProvider()
        {
            TrySubscribeToVivox();
        }

        protected override void UnsubscribeFromVoiceProvider()
        {
            UnregisterCaptureTap("shutdown");

            if (!_subscribedToVivox) return;

            IVivoxService service = SafeVivox();
            if (service != null)
            {
                service.LoggedIn -= OnVivoxLoggedIn;
                service.LoggedOut -= OnVivoxLoggedOut;
                service.ChannelJoined -= OnVivoxChannelJoined;
                service.ChannelLeft -= OnVivoxChannelLeft;
            }

            _subscribedToVivox = false;
        }

        protected override bool IsVoiceTransmitting()
        {
            IVivoxService service = SafeVivox();

            if (service == null || !service.IsLoggedIn) return false;
            if (service.ActiveChannels == null || service.ActiveChannels.Count == 0) return false;

            return !service.IsInputDeviceMuted;
        }

        protected override int GetCurrentPlayerCount()
        {
            IVivoxService service = SafeVivox();

            if (service == null || !service.IsLoggedIn || string.IsNullOrEmpty(_activeChannelName))
                return 0;

            if (service.ActiveChannels != null &&
                service.ActiveChannels.TryGetValue(_activeChannelName, out var participants) &&
                participants != null)
            {
                return participants.Count;
            }

            return 0;
        }

        protected override void ForceProviderMute()
        {
            IVivoxService service = SafeVivox();
            if (service != null && service.IsLoggedIn && !service.IsInputDeviceMuted)
                service.MuteInputDevice();
        }

        protected override void RestoreProviderMute()
        {
            IVivoxService service = SafeVivox();
            if (service != null && service.IsLoggedIn && service.IsInputDeviceMuted)
                service.UnmuteInputDevice();
        }

        protected override string StartSdk(string apiKey, string uniqueId, bool ageVerification, bool debugLogs)
        {
            // Vivox never opens the device microphone. By default the player's outgoing
            // Vivox capture is pulled from Vivox's audio-tap engine and sent to VoicePatrol
            // for moderation; in headless mode, nothing is sent from this client. Either
            // way the mic stays off.
            return VoicePatrolWrapper.StartVoicePatrol(
                apiKey, uniqueID: uniqueId, ageVerification: ageVerification,
                disableMicrophone: true, debugLogs: debugLogs);
        }

        #endregion

        #region Vivox State Tracking

        protected override void Update()
        {
            base.Update();

            if (!_subscribedToVivox)
                TrySubscribeToVivox();

            // Headless mode: no audio is sent from this client, so the capture tap is
            // never registered. Defaults off, so the tap runs as normal.
            if (!_headless && !_bridgeUnavailable && _tapId < 0 && !_tapAttemptedForChannelState)
                TryRegisterCaptureTap();

            if (_headless)
                WarnIfHeadlessIdentityMismatch();
        }

        // In headless mode, Vivox's servers deliver this player's audio to VoicePatrol
        // keyed by the Vivox PlayerId — if that doesn't equal the VoicePatrol unique ID,
        // the audio can't be matched to this player. Never forces anything onto Vivox;
        // warns once per mismatched value so the misconfiguration is loud instead of silent.
        private void WarnIfHeadlessIdentityMismatch()
        {
            if (string.IsNullOrEmpty(UniqueId)) return; // not initialized yet

            IVivoxService service = SafeVivox();
            string providerId = service != null && service.IsLoggedIn ? service.SignedInPlayerId : null;
            if (string.IsNullOrEmpty(providerId)) return; // not logged in yet

            if (providerId == UniqueId)
            {
                _warnedHeadlessMismatchId = null;
                return;
            }

            if (providerId == _warnedHeadlessMismatchId) return;
            _warnedHeadlessMismatchId = providerId;

            Debug.LogWarning($"{LogPrefix} Headless mode: Vivox PlayerId ('{providerId}') doesn't match the " +
                             $"VoicePatrol unique ID ('{UniqueId}'). Audio delivered by Vivox's servers won't be " +
                             "matched to this player — pass the Vivox/Authentication PlayerId as the unique ID " +
                             "to Initialize.");
        }

        private void TrySubscribeToVivox()
        {
            if (_subscribedToVivox) return;

            IVivoxService service = SafeVivox();
            if (service == null) return;

            service.LoggedIn += OnVivoxLoggedIn;
            service.LoggedOut += OnVivoxLoggedOut;
            service.ChannelJoined += OnVivoxChannelJoined;
            service.ChannelLeft += OnVivoxChannelLeft;
            _subscribedToVivox = true;

            Log("Subscribed to Vivox state changes");

            // Already in a channel when VoicePatrol initialized — sync up.
            if (service.IsLoggedIn && service.ActiveChannels != null)
            {
                foreach (string channelName in service.ActiveChannels.Keys)
                {
                    OnVivoxChannelJoined(channelName);
                    break;
                }
            }
        }

        private void OnVivoxLoggedIn()
        {
            Log("Vivox logged in");
        }

        private void OnVivoxLoggedOut()
        {
            UnregisterCaptureTap("logged out");

            if (string.IsNullOrEmpty(_activeChannelName)) return;

            _activeChannelName = null;
            NotifyRoomLeft();
        }

        private void OnVivoxChannelJoined(string channelName)
        {
            // The capture tap follows the latest joined channel (same behaviour as Vivox's
            // AutoAcquireChannel) — registration is per-channel even though the captured
            // microphone stream is the same. Update re-registers on the next frame, by
            // which time Vivox's LastChannelJoinedUri points at this channel.
            UnregisterCaptureTap("channel changed");

            // VoicePatrol tracks one room at a time; the first joined channel wins
            // (multi-channel setups keep monitoring against that channel until it's left).
            if (!string.IsNullOrEmpty(_activeChannelName)) return;

            _activeChannelName = channelName;
            NotifyRoomJoined(channelName);
        }

        private void OnVivoxChannelLeft(string channelName)
        {
            // Drop the tap on any channel exit; Update re-registers if a channel remains.
            UnregisterCaptureTap("left channel");

            if (channelName != _activeChannelName) return;

            _activeChannelName = null;
            NotifyRoomLeft();

            // Still in another channel (multi-channel setup) — continue monitoring there.
            IVivoxService service = SafeVivox();
            if (service?.ActiveChannels == null) return;

            foreach (string remaining in service.ActiveChannels.Keys)
            {
                if (remaining == channelName) continue;
                _activeChannelName = remaining;
                NotifyRoomJoined(remaining);
                break;
            }
        }

        // VivoxService.Instance can throw before Unity Services / Vivox initialization
        // completes — treat "not ready yet" as null and retry from Update.
        private static IVivoxService SafeVivox()
        {
            try { return VivoxService.Instance; }
            catch { return null; }
        }

        #endregion

        #region Vivox Outgoing-Stream Tap

        /// <summary>
        /// Registers a capture-source tap with Vivox's audio-tap engine once logged in and
        /// in a voice channel, then pumps its audio into the SDK every 20 ms. This is what
        /// Vivox's own VivoxCaptureSourceTap component does internally, minus the
        /// AudioSource playback (which would make the player audible to themselves, and
        /// which Unity silences only by also starving the audio data — hence reading the
        /// bridge directly). One registration attempt is made per channel state; channel
        /// join/leave and logout events re-arm it.
        /// </summary>
        private void TryRegisterCaptureTap()
        {
            IVivoxService service = SafeVivox();
            if (service == null || !service.IsLoggedIn ||
                service.ActiveChannels == null || service.ActiveChannels.Count == 0)
                return;

            _tapAttemptedForChannelState = true;

            if (!ResolveBridge()) return;

            string channelUri = GetLastChannelJoinedUri(service);
            if (string.IsNullOrEmpty(channelUri))
            {
                Log("Vivox capture tap: no channel URI available yet — will retry on the next channel change");
                return;
            }

            try
            {
                _tapId = (int)_registerCaptureTap.Invoke(null, new object[] { TAP_BUFFER_DURATION, channelUri });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Vivox capture tap registration threw: {ex.Message}");
                _tapId = -1;
                return;
            }

            if (_tapId < 0)
            {
                Debug.LogWarning($"{LogPrefix} Vivox capture tap registration failed (code {_tapId}) — " +
                                 "will retry on the next channel change.");
                _tapId = -1;
                return;
            }

            _tapBuffer ??= new float[TAP_FRAMES_PER_PULL * TAP_CHANNELS];
            _tapPump ??= StartCoroutine(PumpCaptureAudio());

            Log($"Vivox capture tap registered (id {_tapId}, {TAP_SAMPLE_RATE} Hz)");
        }

        private void UnregisterCaptureTap(string reason)
        {
            _tapAttemptedForChannelState = false;

            if (_tapId < 0) return;

            try
            {
                _unregisterTap?.Invoke(null, new object[] { _tapId });
                Log($"Vivox capture tap unregistered ({reason})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Vivox capture tap unregister failed: {ex.Message}");
            }

            _tapId = -1;
        }

        /// <summary>
        /// Pulls capture audio from the tap on the same cadence as Vivox's own processor:
        /// every 20 ms, drain the tap until it reports no more data, pushing each block to
        /// the SDK. Runs for the component's lifetime and idles while no tap is registered;
        /// Unity stops it automatically on destroy.
        /// </summary>
        private IEnumerator PumpCaptureAudio()
        {
            // Cached to avoid a GC alloc per yield.
            var wait = new WaitForSecondsRealtime(0.02f);

            while (true)
            {
                if (_tapId >= 0)
                {
                    int result = 0;
                    while (result == 0)
                    {
                        result = _getCaptureAudio(_tapId, _tapBuffer, TAP_FRAMES_PER_PULL,
                                                  TAP_CHANNELS, TAP_SAMPLE_RATE);

                        // Mirrors Vivox's VivoxAudioProcessor: anything other than the
                        // no-more-data sentinel is a filled buffer.
                        if (result != _noMoreDataCode)
                            VoicePatrolWrapper.PushAudio(_tapBuffer, _tapBuffer.Length,
                                                         TAP_SAMPLE_RATE, TAP_CHANNELS);
                    }
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Resolves Vivox's internal audio-tap entry points once. All members are internal
        /// to Unity.Services.Vivox and only exist in 16.0.0+ (verified against 16.3.0):
        /// AudioTapBridge (register/get/unregister), VivoxCoreInstance.VxErrorNoMoreData,
        /// and IVivoxServiceInternal.LastChannelJoinedUri.
        /// </summary>
        private bool ResolveBridge()
        {
            if (_bridgeResolved) return true;
            if (_bridgeUnavailable) return false;

            try
            {
                Assembly vivoxAssembly = typeof(VivoxService).Assembly;

                Type bridge = vivoxAssembly.GetType("Unity.Services.Vivox.AudioTapBridge");
                Type core = vivoxAssembly.GetType("Unity.Services.Vivox.VivoxCoreInstance");
                Type serviceInternal = vivoxAssembly.GetType("Unity.Services.Vivox.IVivoxServiceInternal");

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

                _registerCaptureTap = bridge?.GetMethod("RegisterTapForCaptureSource", flags);
                _unregisterTap = bridge?.GetMethod("UnregisterTap", flags);
                MethodInfo getAudio = bridge?.GetMethod("GetCaptureSourceAudio", flags);
                FieldInfo noMoreData = core?.GetField("VxErrorNoMoreData", flags);
                _lastChannelJoinedUri = serviceInternal?.GetProperty("LastChannelJoinedUri");

                if (_registerCaptureTap == null || _unregisterTap == null || getAudio == null ||
                    noMoreData == null || _lastChannelJoinedUri == null)
                {
                    MarkBridgeUnavailable("required members not found");
                    return false;
                }

                // Direct delegate for the hot path — no boxing on the 20 ms cadence.
                _getCaptureAudio = (Func<int, float[], int, int, int, int>)Delegate.CreateDelegate(
                    typeof(Func<int, float[], int, int, int, int>), getAudio);
                _noMoreDataCode = (int)noMoreData.GetValue(null);

                _bridgeResolved = true;
                return true;
            }
            catch (Exception ex)
            {
                MarkBridgeUnavailable(ex.Message);
                return false;
            }
        }

        private void MarkBridgeUnavailable(string detail)
        {
            _bridgeUnavailable = true;
            Debug.LogError($"{LogPrefix} Vivox capture tap unavailable ({detail}). " +
                           "Tapping the outgoing Vivox stream requires com.unity.services.vivox " +
                           "16.0.0 or newer. No audio will reach VoicePatrol.");
        }

        private static string GetLastChannelJoinedUri(IVivoxService service)
        {
            try { return _lastChannelJoinedUri.GetValue(service) as string; }
            catch { return null; }
        }

        #endregion
    }
#else
    public class VoicePatrolVivox : VoicePatrolBase { }
#endif
}
#endif
