using System;
using Liv.Lck.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck.Settings
{
    public class LckSettings : ScriptableObject
    {
        public const string SettingsPath = "Assets/Resources/LckSettings.asset";

        /// <summary>
        /// The minimum required Android API level for LCK.
        /// </summary>
        public const int RequiredAndroidApiLevel = 29;

        [SerializeField]
        public bool ShowSetupWizard = true;

        /// <summary>
        /// The version that the user dismissed the update notification for.
        /// If this matches the latest available version, we won't show the update page on startup.
        /// </summary>
        [SerializeField, HideInInspector]
        public string DismissedUpdateVersion = "";

        /// <summary>
        /// The last version where the Overview page was shown on startup.
        /// Used to detect fresh installs or version updates to show the Overview page.
        /// </summary>
        [SerializeField, HideInInspector]
        public string LastShownOverviewVersion = "";

        /// <summary>
        /// The version that the user dismissed the notification bar for.
        /// If this matches the latest available version, we won't show the notification bar on settings pages (except Overview).
        /// </summary>
        [SerializeField, HideInInspector]
        public string DismissedNotificationBarVersion = "";

        [SerializeField]
        public string TrackingId = "";

        [SerializeField]
        public string GameName = "MyGame";

        [Space(10)]
        [SerializeField]
        public string RecordingFilenamePrefix = "MyGamePrefix";

        [SerializeField]
        public string RecordingAlbumName = "MyGameAlbum";

        [SerializeField]
        public string RecordingDateSuffixFormat = "yyyy-MM-dd_HH-mm-ss";

        [Space(10)]
        [Header("Advanced")]
        [SerializeField]
        [Tooltip("When should the user be asked for microphone access permission in Android builds.")]
        public MicPermissionAskType MicPermissionType = MicPermissionAskType.OnAppStartup;
        [SerializeField]
        [Tooltip("Allow LCK to modify the AndroidManifest.xml file to add Microphone permissions. Disable if you want to manually add permissions.")]
        public bool AddMicPermissionsToAndroidManifest = true;
        [SerializeField]
        [Tooltip("Allow LCK to modify the AndroidManifest.xml file to allow the LIV Control Center app (for streaming) to be launched and queried. Disable if you want to remove the permission.")]
        public bool AddControlCenterPermissionsToAndroidManifest = true;
        [SerializeField]
        [Tooltip("Allow LCK to modify the AndroidManifest.xml file to add Internet permissions. Disable if you want to manually add permissions.")]
        public bool AddInternetPermissionsToAndroidManifest = true;

        [SerializeField]
        [Tooltip(
            "Enabling stencil buffer support allows for advanced rendering effects, such as masking and outlining, to be recorded in the recording. "
                + "UI elements may often utilise the stencil buffer and may otherwise appear incorrect in the recordings. "
                + "Disable to optimise performance if stencil effects are not needed."
        )]
        public bool EnableStencilSupport = true;

        [Space(10)]
        [Header("Logging")]
        [SerializeField]
        public LogLevel BaseLogLevel = LogLevel.Error;

        [SerializeField]
        public Liv.Lck.NativeMicrophone.LogLevel MicrophoneLogLevel = NativeMicrophone.LogLevel.Error;

        [SerializeField]
        public Liv.NGFX.LogLevel NativeLogLevel = Liv.NGFX.LogLevel.Error;

        [SerializeField]
        public LevelFilter CoreLogLevel = LevelFilter.Error;

        [SerializeField]
        [Tooltip("OpenGL messages can be useful to debug errors happening at graphics API level.")]
        public bool ShowOpenGLMessages = false;

        [Header("Audio")]
        [SerializeField]
        [Tooltip(
            "Game audio may appear ahead or behind the game visuals in your game recordings. This property allows for Game Audio to be shifted forward or backwards " +
            "by the provided milliseconds. Positive values will move the audio forward in time, negative backwards."
        )]
        public float GameAudioSyncTimeOffsetInMS = 250;

        [SerializeField]
        [Tooltip("Enabling the audio limiter results in limiter compression applied to the recordings audio.")]
        public LimiterType AudioLimiter = LimiterType.SoftClip;

        [Serializable]
        public enum LimiterType
        {
            SoftClip,
            None,
        }

        [Serializable]
        public enum MicPermissionAskType
        {
            OnAppStartup,
            OnTabletSpawn,
            OnMicUnmute,
            NeverAskFromLck,
        }

        [SerializeField]
        [Tooltip(
            "The sample rate used by LCK if it can't get the samplerate from other sources"
        )]
        public int FallbackSampleRate = 48000;

        [Header("Photo")]
        [SerializeField]
        [Tooltip(
            "The format Photo images will be saved in."
        )]
        public ImageFileFormat ImageCaptureFileFormat = ImageFileFormat.PNG;
        [Serializable]
        public enum ImageFileFormat
        {
            EXR = 0,
            JPG = 1,
            TGA = 2,
            PNG = 3
        }

        [Space(10)]
        [Header("Tablet Using Collider Settings")]
        [Tooltip(
            "When using the 'LCK Tablet Using Collider' prefab. Trigger events will check this tag. "
                + "Make sure to add this tag on your XR Rig Direct Interactors for both controllers"
        )]
        [SerializeField]
        public string TriggerEnterTag = "Hand";

        [HideInInspector]
    public const string Version = "1.4.5";

        [HideInInspector]
        public const int Build = 3618;

        private static LckSettings _instance;

        public static LckSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<LckSettings>("LckSettings");
                    if (_instance != null)
                    {
#if !UNITY_EDITOR
                        Debug.Log($"LCK Settings loaded from Resources");
#endif
                    }
#if !UNITY_EDITOR
                    else
                    {
                        Debug.LogError(
                            "LCK not able to load settings. LckSettings.asset expected to exist in Resources"
                        );
                    }
#endif
                }

#if UNITY_EDITOR
                if (_instance == null)
                {
                    try
                    {
                        LckSettings scriptableObject =
                            ScriptableObject.CreateInstance<LckSettings>();

                        var parentFolder = System.IO.Path.GetDirectoryName(SettingsPath);
                        if (!System.IO.Directory.Exists(parentFolder))
                        {
                            System.IO.Directory.CreateDirectory(parentFolder);
                        }

                        UnityEditor.AssetDatabase.CreateAsset(scriptableObject, SettingsPath);
                        UnityEditor.AssetDatabase.SaveAssets();
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<LckSettings>(
                            SettingsPath
                        );
                        if (_instance != null)
                        {
                            Debug.Log("LCK settings asset created at " + SettingsPath);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("LCK failed to create settings asset: " + ex.Message);
                    }
                }
#else
                if (_instance == null)
                {
                    _instance = CreateInstance<LckSettings>();

                    Debug.LogError("LCK using default settings because LckSettings.asset not found");
                }
#endif

                return _instance;
            }
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(TrackingId))
            {
                TrackingId = TrackingId.Trim();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
    }
}
