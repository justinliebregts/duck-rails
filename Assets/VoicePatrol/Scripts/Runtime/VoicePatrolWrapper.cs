using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime
{
    [HelpURL("https://dashboard.voicepatrol.dev/")]
    [AddComponentMenu("VoicePatrol/VoicePatrol Wrapper")]
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    public class VoicePatrolWrapper : MonoBehaviour
    {
        private const string LIB_NAME = "VoicePatrol_SDK";

        public enum Label
        {
            Hate = 1,
            Hate_Threatening = 2,
            Harassment = 3,
            Harassment_Threatening = 4,
            Self_Harm = 5,
            Self_Harm_Intent = 6,
            Self_Harm_Instructions = 7,
            Sexual = 8,
            Sexual_Minors = 9,
            Violence = 10,
            Violence_Graphic = 11,
            Discrimination_Sexism = 12,
            Discrimination_Racism = 13,
            Discrimination_Homophobia = 14,
            Discrimination_Other = 15,
            Custom = 16
        }

        public struct ViolationEventData
        {
            public string UniqueId;
            public string Priority;
            public Label[] Labels;
        }

        public struct AgeVerificationData
        {
            public string UniqueId;
            public uint MinAge;
            public uint MaxAge;
        }

        public struct InputDeviceInfo
        {
            public string Name;
            public bool IsDefault;
        }

        [Serializable]
        private class InputDeviceDto
        {
            public string name;
            public bool isDefault;
        }

        [Serializable]
        private class InputDevicesResponseDto
        {
            public InputDeviceDto[] devices;
            public string error;
        }

        public static event Action<ViolationEventData> OnViolationDetected;
        public static event Action<AgeVerificationData> OnAgeVerified;

        private static VoicePatrolWrapper _instance;

        private delegate void LogCallbackDelegate(string logMessage);

        private delegate void ServerCallbackDelegate(
            [MarshalAs(UnmanagedType.LPStr)] string deviceId,
            [MarshalAs(UnmanagedType.LPStr)] string priority,
            IntPtr labelsPtr,
            int labelsSize
        );

        private delegate void AgeVerificationDelegate(
            [MarshalAs(UnmanagedType.LPStr)] string deviceId,
            uint minAge,
            uint maxAge
        );

        private static readonly Queue<ViolationEventData> ViolationEventQueue = new();
        private static readonly Queue<AgeVerificationData> AgeVerificationQueue = new();
        private static readonly object QueueLock = new();

        private AudioCaptureProxy _audioCaptureProxy;
        private const string LOG_PREFIX = "<color=#00D5FF>[VoicePatrol]</color>";

        [DllImport(LIB_NAME)]
        private static extern IntPtr start_sdk(
            [MarshalAs(UnmanagedType.LPStr)] string token,
            [MarshalAs(UnmanagedType.LPStr)] string deviceID,
            bool ageVerification,
            bool useCdn,
            bool disableMicrophone,
            int inputSampleRate,
            LogCallbackDelegate logCallback,
            ServerCallbackDelegate serverCallback,
            AgeVerificationDelegate ageCallback
        );

        [DllImport(LIB_NAME)]
        private static extern bool is_sdk_running();

        [DllImport(LIB_NAME)]
        private static extern IntPtr on_joined_room(
            [MarshalAs(UnmanagedType.LPStr)] string roomID
        );

        [DllImport(LIB_NAME)]
        private static extern IntPtr on_left_room();

        [DllImport(LIB_NAME)]
        private static extern IntPtr stop_sdk();

        [DllImport(LIB_NAME)]
        private static extern IntPtr set_muted(
            bool value
        );

        [DllImport(LIB_NAME)]
        private static extern bool is_muted();

        [DllImport(LIB_NAME)]
        private static extern bool is_microphone_disabled();

        [DllImport(LIB_NAME)]
        private static extern IntPtr process_audio_file_data(
            [MarshalAs(UnmanagedType.LPArray)] float[] audioData,
            int length,
            int sampleRate,
            int channels
        );

        [DllImport(LIB_NAME)]
        private static extern void push_game_audio_data(
            [MarshalAs(UnmanagedType.LPArray)] float[] audioData,
            int length,
            int channels
        );

        [DllImport(LIB_NAME)]
        private static extern IntPtr submit_player_report(
            [MarshalAs(UnmanagedType.LPStr)] string offenderID,
            [MarshalAs(UnmanagedType.LPStr)] string reason
        );

        [DllImport(LIB_NAME)]
        private static extern IntPtr set_metadata(
            [MarshalAs(UnmanagedType.LPStr)] string metadata
        );

        [DllImport(LIB_NAME)]
        private static extern IntPtr get_input_devices();

        [DllImport(LIB_NAME)]
        private static extern IntPtr set_input_device(
            [MarshalAs(UnmanagedType.LPStr)] string deviceName
        );

        [DllImport(LIB_NAME)]
        private static extern IntPtr get_current_input_device();

        [DllImport(LIB_NAME)]
        private static extern void unregister_callbacks();

        [DllImport(LIB_NAME)]
        private static extern void push_audio(
            [MarshalAs(UnmanagedType.LPArray)] float[] audioData,
            int length,
            int inputSampleRate,
            int channels
        );

        [DllImport(LIB_NAME)]
        private static extern float get_input_level();

        // -------------------------------------------------------------------
        // Editor / application teardown
        // -------------------------------------------------------------------
        // The Unity Editor keeps native plugins loaded between play sessions, so the SDK's
        // worker threads (capture + gRPC) and the open microphone survive leaving Play Mode
        // unless stopped explicitly. These hooks fire regardless of whether a
        // VoicePatrolWrapper or any integration component exists in the scene, so every
        // entry point — including the static API — is covered.

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardown()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        private static void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                StopSdkIfRunning();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntimeTeardown()
        {
            Application.quitting -= StopSdkIfRunning;
            Application.quitting += StopSdkIfRunning;
        }

        // Stops the native SDK if it is running. Idempotent and safe to call from any
        // teardown path (Play Mode exit, app quit, component destroy).
        private static void StopSdkIfRunning()
        {
            try
            {
                if (!is_sdk_running()) return;

                try
                {
                    unregister_callbacks();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_PREFIX} Error calling unregister_callbacks(): {ex.Message}");
                }

                Debug.Log($"{LOG_PREFIX} {StopVoicePatrol()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Error stopping SDK on teardown: {ex.Message}");
            }
        }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static void OnAudioData(float[] data, int channels)
        {
            push_game_audio_data(data, data.Length, channels);
        }

        private void Update()
        {
            if (is_sdk_running())
            {
                if (!_audioCaptureProxy)
                {
                    AudioListener listener = FindObjectOfType<AudioListener>();
                    if (listener && !listener.GetComponent<AudioCaptureProxy>())
                    {
                        _audioCaptureProxy = listener.gameObject.AddComponent<AudioCaptureProxy>();
                        AudioCaptureProxy.OnAudioFilterReadEvent -= OnAudioData;
                        AudioCaptureProxy.OnAudioFilterReadEvent += OnAudioData;
                    }
                }
            }

            lock (QueueLock)
            {
                while (ViolationEventQueue.Count > 0)
                {
                    ViolationEventData eventData = ViolationEventQueue.Dequeue();
                    OnViolationDetected?.Invoke(eventData);
                }

                while (AgeVerificationQueue.Count > 0)
                {
                    AgeVerificationData ageData = AgeVerificationQueue.Dequeue();
                    OnAgeVerified?.Invoke(ageData);
                }
            }
        }

        private void OnApplicationQuit()
        {
            AudioCaptureProxy.OnAudioFilterReadEvent -= OnAudioData;

            try
            {
                unregister_callbacks();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Error calling unregister_callbacks(): {ex.Message}");
            }

            try
            {
                StopVoicePatrol();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Error calling StopVoicePatrol(): {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                AudioCaptureProxy.OnAudioFilterReadEvent -= OnAudioData;

                try
                {
                    unregister_callbacks();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_PREFIX} Error calling unregister_callbacks(): {ex.Message}");
                }

                try
                {
                    StopVoicePatrol();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_PREFIX} Error calling StopVoicePatrol(): {ex.Message}");
                }
            }
        }

        [MonoPInvokeCallback(typeof(ServerCallbackDelegate))]
        private static void ServerMessageHandler(string uniqueId, string priority, IntPtr labelsPtr, int labelsSize)
        {
            try
            {
                Label[] labelsEnum = new Label[labelsSize];
                int[] labels = new int[labelsSize];
                Marshal.Copy(labelsPtr, labels, 0, labelsSize);

                for (int i = 0; i < labelsSize; i++) labelsEnum[i] = (Label)labels[i];

                lock (QueueLock)
                {
                    ViolationEventQueue.Enqueue(new ViolationEventData
                        { UniqueId = uniqueId, Priority = priority, Labels = labelsEnum });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Error invoking OnViolationDetected: {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(AgeVerificationDelegate))]
        private static void AgeVerificationHandler(string uniqueId, uint minAge, uint maxAge)
        {
            try
            {
                lock (QueueLock)
                {
                    AgeVerificationQueue.Enqueue(new AgeVerificationData
                    {
                        UniqueId = uniqueId,
                        MinAge = minAge,
                        MaxAge = maxAge
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Error handling age verification: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the VoicePatrol SDK with the specified configuration.
        /// </summary>
        /// <param name="token">API token for authentication. Obtain this from your VoicePatrol dashboard.</param>
        /// <param name="uniqueID"> Custom user identifier used to identify players in violation reports and analytics.
        /// If null, defaults to SystemInfo.deviceUniqueIdentifier.
        /// For server-side (headless) voice streaming with Photon or Vivox, set your voice provider's
        /// identity (Photon Client.UserId / Vivox PlayerId) to this same value at login — both providers
        /// allow this before joining a room — so audio streamed directly from the provider's servers to
        /// VoicePatrol is associated with this player. </param>
        /// <param name="ageVerification">Enable age verification by analyzing the first 60 seconds of speech.</param>
        /// <param name="useCdn"> Route connections through a CDN for improved reliability in regions with poor network connectivity. Recommended for players experiencing connection timeouts or high latency. </param>
        /// <param name="disableMicrophone"> When true, the SDK does not open the device microphone. Use this for
        /// voice-engine integrations (Photon, Vivox), where the player's audio comes from the engine instead of a
        /// local mic — either supplied via <see cref="PushAudio"/>, or delivered to VoicePatrol from the engine's
        /// servers. This avoids moderating the same audio twice. Default is false (the SDK opens the microphone). </param>
        /// <param name="debugLogs">Enable detailed SDK debug logging to the Unity console.</param>
        /// <returns>Status message indicating initialization result.</returns>
        public static string StartVoicePatrol(string token, string uniqueID = null, bool ageVerification = false, bool useCdn = false, bool disableMicrophone = false, bool debugLogs = false)
        {
            if (string.IsNullOrEmpty(token)) return "Invalid API token";

            if (uniqueID == null) uniqueID = SystemInfo.deviceUniqueIdentifier;

            if (!disableMicrophone && !VoicePatrolPermissionManager.HasMicrophonePermission())
            {
                VoicePatrolPermissionManager.RequestMicrophonePermission(
                    _instance,
                    () => InitializeSDK(token, uniqueID, ageVerification, useCdn, disableMicrophone, debugLogs, AudioSettings.outputSampleRate),
                    () => Debug.LogWarning($"{LOG_PREFIX} Cannot initialize SDK without microphone permission")
                );
                return "Requesting microphone permission...";
            }

            return InitializeSDK(token, uniqueID, ageVerification, useCdn, disableMicrophone, debugLogs, AudioSettings.outputSampleRate);
        }

        private static string InitializeSDK(string token, string deviceID, bool ageVerification, bool useCdn, bool disableMicrophone, bool debugLogs, int inputSampleRate)
        {
            ServerCallbackDelegate serverCallback = new(ServerMessageHandler);
            AgeVerificationDelegate ageCallback = new(AgeVerificationHandler);
            string result = Marshal.PtrToStringAnsi(start_sdk(
                token,
                deviceID,
                ageVerification,
                useCdn,
                disableMicrophone,
                inputSampleRate,
                debugLogs ? new LogCallbackDelegate(LogMessageHandler) : null,
                serverCallback,
                ageCallback
            ));

            return result;
        }

        // ---------------------------------------------------------------------
        // External audio ingest — public API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Feeds audio tapped from a voice engine on the client (e.g. a Photon Voice
        /// outgoing-stream processor) into the SDK for moderation. Use this when the
        /// engine's audio is captured on the client rather than forwarded to VoicePatrol
        /// server-to-server. No-op unless the SDK was started with the microphone disabled.
        /// Mirrors the resample and mute handling of the SDK's own capture loop.
        /// </summary>
        /// <param name="audioData">PCM float samples (interleaved if multi-channel).</param>
        /// <param name="length">Number of valid samples in <paramref name="audioData"/>.</param>
        /// <param name="sampleRate">Sample rate of the pushed audio (e.g. 48000 for Photon Voice).</param>
        /// <param name="channels">Channel count of the pushed audio (1 for a mono source).</param>
        public static void PushAudio(float[] audioData, int length, int sampleRate, int channels)
        {
            if (audioData == null || length <= 0) return;
            push_audio(audioData, length, sampleRate, channels);
        }

        /// <summary>
        /// Checks if the VoicePatrol SDK is currently running.
        /// </summary>
        /// <returns>True if the SDK is running and processing audio, false otherwise.</returns>
        public static bool IsSdkRunning()
        {
            return is_sdk_running();
        }

        /// <summary>
        /// Live input level (0..1 peak) of audio currently reaching the SDK — the internal
        /// microphone or audio supplied via <see cref="PushAudio"/>. Returns 0 when the SDK
        /// isn't running or no audio has arrived recently. Intended for diagnostics/meters.
        /// </summary>
        public static float GetInputLevel()
        {
            return get_input_level();
        }

        /// <summary>
        /// True when the SDK is running but not capturing from its own microphone — audio is
        /// supplied via <see cref="PushAudio"/> by a voice provider (e.g. Photon). Device
        /// selection does not apply in this mode. Returns false when the SDK isn't running.
        /// </summary>
        public static bool IsMicrophoneDisabled()
        {
            return is_sdk_running() && is_microphone_disabled();
        }

        /// <summary>
        /// Notifies the SDK when the local player joins a multiplayer room.
        /// Call this to associate voice activity with specific game sessions or lobbies.
        /// </summary>
        /// <param name="roomId">The unique identifier for the room (e.g., Photon room name, Vivox channel).</param>
        /// <returns>Status message indicating room join result.</returns>
        public static string OnJoinedRoom(string roomId)
        {
            return Marshal.PtrToStringAnsi(on_joined_room(roomId));
        }

        /// <summary>
        /// Notifies the SDK when the local player leaves a multiplayer room.
        /// </summary>
        /// <returns>Status message indicating room leave result.</returns>
        public static string OnLeftRoom()
        {
            return Marshal.PtrToStringAnsi(on_left_room());
        }

        /// <summary>
        /// Stops the VoicePatrol SDK and releases all resources.
        /// WARNING: Only call this when completely done with voice moderation.
        /// Use SetMuted(true) instead to temporarily pause monitoring without the overhead of reinitialization.
        /// </summary>
        /// <returns>Status message indicating shutdown result.</returns>
        public static string StopVoicePatrol()
        {
            return Marshal.PtrToStringAnsi(stop_sdk());
        }

        /// <summary>
        /// Sets the VoicePatrol muted state.
        /// When muted, voice analysis is paused and usage time is not accumulated.
        /// Use this to pause monitoring during menus, loading screens, or when the player mutes themselves.
        ///
        /// <b>Note:</b> When using a VoicePatrolBase integration (Photon, etc.), call
        /// <c>VoicePatrolBase.Instance.SetForceMuted(true)</c> instead — the integration
        /// re-evaluates the mute state every second and will revert a direct call to this method.
        /// </summary>
        /// <param name="value">True to mute (pause monitoring), false to unmute (resume monitoring).</param>
        /// <returns>Status message indicating mute state change result.</returns>
        public static string SetMuted(bool value)
        {
            return Marshal.PtrToStringAnsi(set_muted(value));
        }

        /// <summary>
        /// Checks if VoicePatrol is currently muted.
        /// </summary>
        /// <returns>True if muted, false otherwise.</returns>
        public static bool IsMuted()
        {
            return is_muted();
        }

        /// <summary>
        /// Processes an audio clip for voice moderation or age verification testing.
        /// Useful for testing your integration without requiring live microphone input.
        /// </summary>
        /// <param name="audioClip">
        /// The audio clip to process. Must be uncompressed or have 'Decompress On Load' enabled in import settings.
        /// </param>
        /// <returns>Status message indicating processing result.</returns>
        public static string ProcessAudioFile(AudioClip audioClip)
        {
            float[] audioData = new float[audioClip.samples * audioClip.channels];
            return !audioClip.GetData(audioData, 0)
                ? $"Failed to process audio clip '{audioClip.name}' - Audio clip must be uncompressed or marked as 'Decompress On Load'"
                : Marshal.PtrToStringAnsi(process_audio_file_data(
                    audioData,
                    audioData.Length,
                    audioClip.frequency,
                    audioClip.channels
                ));
        }

        /// <summary>
        /// Submits a player report with audio evidence.
        /// Captures the most recent 120 seconds of everything the local player said and heard,
        /// then uploads it for moderation review associated with the reported player.
        /// </summary>
        /// <param name="offenderID">
        /// The unique identifier of the player being reported.
        /// Should match the identifier used in your game's player system.
        /// </param>
        /// <param name="reason">A brief description of why the player is being reported.</param>
        /// <returns>Status message indicating whether the report submission started successfully.</returns>
        public static string SubmitPlayerReport(string offenderID, string reason)
        {
            if (string.IsNullOrEmpty(offenderID))
            {
                return "Cannot submit player report: Offender ID is required";
            }
            if (string.IsNullOrEmpty(reason))
            {
                return "Cannot submit player report: Reason is required";
            }
            return Marshal.PtrToStringAnsi(submit_player_report(offenderID, reason));
        }

        /// <summary>
        /// Sets custom metadata to be included with voice analysis events.
        /// Use this to attach game context (player name, level, game mode, etc.) to moderation events.
        /// </summary>
        /// <typeparam name="T">Any JSON-serializable type.</typeparam>
        /// <param name="metadata">
        /// The metadata object to attach. Must serialize to under 200 bytes of JSON.
        /// </param>
        /// <returns>Status message indicating whether metadata was set successfully.</returns>
        public static string SetMetadata<T>(T metadata)
        {
            try
            {
                string jsonMetadata = JsonUtility.ToJson(metadata);
                return Marshal.PtrToStringAnsi(set_metadata(jsonMetadata));
            }
            catch (Exception e)
            {
                return $"Failed to set metadata: {e.Message}";
            }
        }

        /// <summary>
        /// Enumerates the available microphone input devices on the current platform.
        /// </summary>
        /// <remarks>
        /// Returns an empty array on Android (single fixed device), when the microphone is disabled,
        /// or before the SDK has been started. Use this to populate a microphone-selection UI.
        /// </remarks>
        /// <returns>The available input devices, or an empty array if enumeration is not available.</returns>
        public static InputDeviceInfo[] GetInputDevices()
        {
            if (!is_sdk_running()) return Array.Empty<InputDeviceInfo>();

            string json = Marshal.PtrToStringAnsi(get_input_devices());
            if (string.IsNullOrEmpty(json)) return Array.Empty<InputDeviceInfo>();

            try
            {
                InputDevicesResponseDto response = JsonUtility.FromJson<InputDevicesResponseDto>(json);
                if (response == null) return Array.Empty<InputDeviceInfo>();

                if (!string.IsNullOrEmpty(response.error))
                {
                    Debug.Log($"{LOG_PREFIX} GetInputDevices: {response.error}");
                    return Array.Empty<InputDeviceInfo>();
                }

                if (response.devices == null) return Array.Empty<InputDeviceInfo>();

                InputDeviceInfo[] devices = new InputDeviceInfo[response.devices.Length];
                for (int i = 0; i < response.devices.Length; i++)
                {
                    devices[i] = new InputDeviceInfo
                    {
                        Name = response.devices[i].name,
                        IsDefault = response.devices[i].isDefault
                    };
                }
                return devices;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Failed to parse input devices JSON: {ex.Message}");
                return Array.Empty<InputDeviceInfo>();
            }
        }

        /// <summary>
        /// Switches the active input (microphone) device at runtime.
        /// </summary>
        /// <remarks>
        /// Pass the Name field from one of the entries returned by <see cref="GetInputDevices"/>,
        /// or an empty string / null to revert to the system default. This is a no-op on Android,
        /// and requires the SDK to be running. The SDK briefly stops capturing while it switches
        /// devices; the audio stream and moderation pipeline are not interrupted.
        /// </remarks>
        /// <param name="deviceName">The name of the device to switch to, or null/empty for system default.</param>
        /// <returns>Status message indicating the result of the switch.</returns>
        public static string SetInputDevice(string deviceName)
        {
            return Marshal.PtrToStringAnsi(set_input_device(deviceName ?? string.Empty));
        }

        /// <summary>
        /// Returns the name of the currently selected input device.
        /// </summary>
        /// <remarks>
        /// Returns an empty string if the system default is in use, on Android, or before the
        /// SDK has been started.
        /// </remarks>
        /// <returns>The current device name, or an empty string when the system default is active.</returns>
        public static string GetCurrentInputDevice()
        {
            return Marshal.PtrToStringAnsi(get_current_input_device());
        }

        [MonoPInvokeCallback(typeof(LogCallbackDelegate))]
        private static void LogMessageHandler(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }
    }
#else
    public class VoicePatrolWrapper : MonoBehaviour { }
#endif
}