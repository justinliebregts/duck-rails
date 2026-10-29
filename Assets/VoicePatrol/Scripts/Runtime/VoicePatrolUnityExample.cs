using System.Collections;
using UnityEngine;

namespace VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime
{
    [HelpURL("https://dashboard.voicepatrol.dev/")]
    [AddComponentMenu("VoicePatrol/VoicePatrol Unity Example")]
    [RequireComponent(typeof(VoicePatrolWrapper))]
    public class VoicePatrolUnityExample : MonoBehaviour
    {
        private VoicePatrolUnityExample _instance;
        [SerializeField] private string _voicePatrolAPIKey;
        [SerializeField] private bool _ageVerification;
        [SerializeField] private bool _useCdn;
        [SerializeField] private bool _debugLogs;
        [SerializeField] private AudioClip _testAudioClip;
        [SerializeField] private string _offenderID;
        [SerializeField] private string _reportReason;
        [SerializeField] private string _inputDeviceName;

        private readonly string _logPrefix = $"<color=#00D5FF>[{nameof(VoicePatrolUnityExample)}]</color>";

        // Photon Voice
        //private Photon.Voice.Unity.VoiceConnection _voiceConnection;

        // Dissonance
        //private Dissonance.DissonanceComms _dissonanceComms;

#if UNITY_EDITOR
        public static bool IsSdkRunning => VoicePatrolWrapper.IsSdkRunning();
        public static bool IsMuted => VoicePatrolWrapper.IsMuted();
#endif

#if UNITY_EDITOR || UNITY_ANDROID
        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_voicePatrolAPIKey))
            {
                Debug.LogWarning($"{_logPrefix} API key is not set");
                return;
            }

            StartCoroutine(InitializeSDK());
        }

        private IEnumerator InitializeSDK()
        {
            yield return new WaitForSeconds(1f);
            StartVoicePatrol();
            InvokeRepeating(nameof(SetVoicePatrolMuted), 1, 1);
        }

        private void OnEnable()
        {
            VoicePatrolWrapper.OnViolationDetected += AutomatedModeration;
            VoicePatrolWrapper.OnAgeVerified += HandleAgeVerification;
        }

        private void OnDisable()
        {
            VoicePatrolWrapper.OnViolationDetected -= AutomatedModeration;
            VoicePatrolWrapper.OnAgeVerified -= HandleAgeVerification;
        }

        private void SetVoicePatrolMuted()
        {
            bool isMuted = false;

            // Vivox
            /*isMuted = Unity.Services.Vivox.VivoxService.Instance == null ||
                      Unity.Services.Vivox.VivoxService.Instance.ActiveChannels.Count == 0 ||
                      Unity.Services.Vivox.VivoxService.Instance.IsInputDeviceMuted;*/

            // Photon Voice
            /*if (_voiceConnection == null)
            {
                _voiceConnection = FindObjectOfType<Photon.Voice.Unity.VoiceConnection>();
            }
            isMuted = _voiceConnection == null ||
                      _voiceConnection.Client == null ||
                      _voiceConnection.Client.State != Photon.Realtime.ClientState.Joined ||
                      !_voiceConnection.PrimaryRecorder.RecordingEnabled;*/

            // Photon Voice Legacy
            /*isMuted = Photon.Voice.PUN.PhotonVoiceNetwork.Instance == null ||
                      Photon.Voice.PUN.PhotonVoiceNetwork.Instance.ClientState != Photon.Realtime.ClientState.Joined ||
                      !Photon.Voice.PUN.PhotonVoiceNetwork.Instance.PrimaryRecorder.IsRecording;*/

            // Dissonance
            /*if (_dissonanceComms == null)
            {
                _dissonanceComms = FindObjectOfType<Dissonance.DissonanceComms>();

                if (_dissonanceComms == null)
                {
                    Debug.Log($"{_logPrefix} unable to find DissonanceComms");
                }
            }
            isMuted = _dissonanceComms == null ||
                      _dissonanceComms.IsMuted;*/

            VoicePatrolWrapper.SetMuted(isMuted);
        }

        private void AutomatedModeration(VoicePatrolWrapper.ViolationEventData obj)
        {
            Debug.Log($"{_logPrefix} Violation detected for {obj.UniqueId} with priority {obj.Priority} and labels {string.Join(", ", obj.Labels)}");
        }

        private void HandleAgeVerification(VoicePatrolWrapper.AgeVerificationData data)
        {
            Debug.Log($"{_logPrefix} Age verification result for {data.UniqueId}: " +
                      $"Age range {data.MinAge}-{data.MaxAge}");
        }
#endif

        [ContextMenu("Start VoicePatrol")]
        public void StartVoicePatrol()
        {
            // uniqueID null falls back to the device unique identifier.
            // Pass your player's unique ID here
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.StartVoicePatrol(_voicePatrolAPIKey, uniqueID: null, ageVerification: _ageVerification, useCdn: _useCdn, debugLogs: _debugLogs)}");
        }

        [ContextMenu("VoicePatrol On Joined Room")]
        public void OnJoinedRoom()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.OnJoinedRoom("yourRoomName")}");
        }

        [ContextMenu("VoicePatrol On Left Room")]
        public void OnLeftRoom()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.OnLeftRoom()}");
        }

        /// <summary>
        /// Do not call this unless explicitly required
        /// Use SetMuted to control the state of VoicePatrol
        /// </summary>
        [ContextMenu("Stop VoicePatrol")]
        public void StopVoicePatrol()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.StopVoicePatrol()}");
        }

        [ContextMenu("Mute VoicePatrol")]
        public void MuteVoicePatrol()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.SetMuted(true)}");
        }

        [ContextMenu("Unmute VoicePatrol")]
        public void UnmuteVoicePatrol()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.SetMuted(false)}");
        }

        [ContextMenu("Get VoicePatrol muted state")]
        public void GetVoicePatrolMutedState()
        {
            Debug.Log($"{_logPrefix} IsMuted: {VoicePatrolWrapper.IsMuted()}");
        }

        [ContextMenu("Report another player with audio evidence")]
        public void ReportPlayer()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.SubmitPlayerReport(_offenderID, _reportReason)}");
        }

        [ContextMenu("Set VoicePatrol metadata")]
        public void SetMetadata()
        {
            // Any JSON-serializable data under 200 bytes can be used
            // This example uses: ~162 bytes
            Metadata metadata = new Metadata
            {
                userName = "PlayerName",
                playerLevel = 42,
                mode = "Survival",
                isInCombat = true,
                position = new Position
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = transform.position.z
                }
            };

            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.SetMetadata(metadata)}");
        }

        [ContextMenu("List Input Devices")]
        public void ListInputDevices()
        {
            VoicePatrolWrapper.InputDeviceInfo[] devices = VoicePatrolWrapper.GetInputDevices();
            if (devices.Length == 0)
            {
                Debug.Log($"{_logPrefix} No input devices available (Android, headless mode, or SDK not running)");
                return;
            }

            Debug.Log($"{_logPrefix} {devices.Length} input device(s):");
            foreach (VoicePatrolWrapper.InputDeviceInfo device in devices)
            {
                Debug.Log($"{_logPrefix}   - {device.Name}{(device.IsDefault ? " [DEFAULT]" : "")}");
            }
        }

        [ContextMenu("Set Input Device")]
        public void SetInputDevice()
        {
            Debug.Log($"{_logPrefix} {VoicePatrolWrapper.SetInputDevice(_inputDeviceName)}");
        }

        [ContextMenu("Get Current Input Device")]
        public void GetCurrentInputDevice()
        {
            string current = VoicePatrolWrapper.GetCurrentInputDevice();
            Debug.Log($"{_logPrefix} Current input device: {(string.IsNullOrEmpty(current) ? "(system default)" : current)}");
        }

        [System.Serializable]
        public class Metadata
        {
            public string userName;
            public int playerLevel;
            public string mode;
            public bool isInCombat;
            public Position position;
        }

        [System.Serializable]
        public class Position
        {
            public float x;
            public float y;
            public float z;
        }
    }
}