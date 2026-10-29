using Liv.Lck.DependencyInjection;
using System.Collections;
using Liv.Lck.Recorder;
using System.Collections.Generic;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Liv.Lck.Core;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// A helper class used to configure notifications.
    /// It creates an association between a specific NotificationType and the GameObject prefab
    /// that should be displayed for it.
    /// </summary>
    [System.Serializable]
    public class InitializerNotification
    {
        // Used internally by the OnValidate method to give each element in the Inspector
        // a readable name based on its type.
        [HideInInspector] public string Name;

        /// <summary>
        /// The unique type of this notification.
        /// </summary>
        public NotificationType Type;
        
        /// <summary>
        /// The prefab to instantiate when this notification is shown.
        /// The root of the prefab must have a component that inherits from LckBaseNotification.
        /// </summary>
        public GameObject prefab;
    }

    /// <summary>
    /// Defines the different types of notifications that can be displayed by the system.
    /// This enum is used to identify and request specific notifications.
    /// </summary>
    [System.Serializable]
    public enum NotificationType
    {
        VideoSaved = 0,
        PhotoSaved = 1,
        EnterStreamCode = 2,
        ConfigureStream = 3,
        InternalError = 4,
        MissingTrackingId = 5,
        InvalidTrackingId = 6,
        InvalidArgument = 7,
        UnknownStreamingError = 8,
        ServiceUnavailable = 9,
        RateLimiterBackoff = 10,
        EchoInfo = 11,
        EchoLowStorage = 12,
        EchoError = 13,
        HeadsetView = 14,
    }
    
    /// <summary>
    /// Acts as a centralised manager for displaying all user-facing notifications.
    /// This class instantiates, shows, and hides notification prefabs based on events from the LCK service
    /// or direct calls from other UI controllers. It serves as the bridge between backend events (like a video being saved)
    /// and the corresponding UI feedback.
    /// 
    /// To use, attach this to a GameObject and configure the list of notification prefabs in the Inspector.
    /// </summary>
    public class LckNotificationController : MonoBehaviour
    {
        /// <summary>
        /// Provides access to the LCK services to subscribe to events like recording/streaming status changes.
        /// This field is automatically populated by the LCK Dependency Injection system at runtime.
        /// </summary>
        [InjectLck] 
        private ILckService _lckService;
        
        [Tooltip("Configure the list of all possible notifications. Drag your notification prefabs here and assign them a type.")]
        [SerializeField]
        private List<InitializerNotification> _notificationsInitializer = new List<InitializerNotification>();
        
        // Runtime dictionary that holds the instantiated notification objects, keyed by their type for quick access.
        private readonly Dictionary<NotificationType, LckBaseNotification> _notifications = new Dictionary<NotificationType, LckBaseNotification>();

        // A reference to the currently active notification, if any.
        private LckBaseNotification _currentNotification = null;

        [Tooltip("The default duration in seconds that a notification will remain on screen before automatically hiding. This can be overridden by the notification itself.")]
        [SerializeField] private float _notificationShowDuration = 3f;

        [Tooltip("The parent Transform under which all notification prefabs will be instantiated.")]
        [SerializeField] private Transform _notificationsTransform;
        
        [Tooltip("A reference to a higher-level UI controller that may need to react when notifications appear or disappear (e.g., to adjust layout).")]
        [SerializeField]
        private LckOnScreenUIController _onScreenUIController;

        private void Awake()
        {
            // Instantiate all the configured notification prefabs at startup.
            InitializeNotifications();
        }

        private void Start()
        {
            CheckInitializationAfterDelay();
        }

        /// <summary>
        /// Waits for a few seconds before checking the LckCore initialization result.
        /// </summary>
        private async void CheckInitializationAfterDelay()
        {
            await Task.Delay(1000);

            if (this == null) return;

            var initResult = LckCoreHandler.LckCoreInitializationResult;

            if (initResult?.IsOk == false)
            {
                if (initResult.Err == CoreError.MissingTrackingId)
                {
                    StartCoroutine(CreateNotification(NotificationType.MissingTrackingId));
                }
                else if (initResult.Err == CoreError.InvalidTrackingId)
                {
                    StartCoroutine(CreateNotification(NotificationType.InvalidTrackingId));
                }
            }
        }

        /// <summary>
        /// Editor-only method to improve the Inspector experience by naming list elements
        /// based on the selected NotificationType.
        /// </summary>
        private void OnValidate()
        {
            foreach (var notification in _notificationsInitializer)
            {
                notification.Name = notification.prefab != null ? notification.Type.ToString() : null;
            }
        }

        /// <summary>
        /// Unity lifecycle method. Subscribes to LCK service events to automatically trigger notifications.
        /// </summary>
        private void OnEnable()
        {
            _lckService.OnRecordingStarted += OnCaptureStarted;
            _lckService.OnStreamingStarted += OnCaptureStarted;
            _lckService.OnRecordingSaved += OnRecordingSaved;
            _lckService.OnEchoSaved += OnEchoSaved;
            _lckService.OnEchoDisabled += OnEchoDisabled;

            if (_currentNotification != null)
            {
                if (_currentNotification.RemainOnScreen == true)
                {
                    //hide on screen buttons if a notification remains on screen when re-enabled
                    _onScreenUIController.OnNotificationStarted();
                }
            }
        }

        /// <summary>
        /// Unity lifecycle method. Unsubscribes from events to prevent memory leaks and errors.
        /// </summary>
        private void OnDisable()
        {
            if(_lckService == null) return;
            
            _lckService.OnRecordingStarted -= OnCaptureStarted;
            _lckService.OnStreamingStarted -= OnCaptureStarted;
            _lckService.OnRecordingSaved -= OnRecordingSaved;
            _lckService.OnEchoSaved -= OnEchoSaved;
            _lckService.OnEchoDisabled -= OnEchoDisabled;

            if (_currentNotification != null)
            {
                if (_currentNotification.RemainOnScreen == false)
                {
                    HideNotifications();
                }
            }
            
        }

        /// <summary>
        /// A specific method to update the text of the 'EnterStreamCode' notification.
        /// This is used by the streaming workflow to display the user's login code.
        /// </summary>
        /// <param name="code">The stream code to display, typically in "123-456" format.</param>
        public void SetNotificationStreamCode(string code)
        {
            if (_notifications.TryGetValue(NotificationType.EnterStreamCode, out LckBaseNotification notif))
            {
                // This assumes the notification prefab has a LckNormalNotification component to display text.
                if (notif is LckNormalNotification codeNotif)
                {
                    codeNotif.Text.text = code;
                }
            }
            else
            {
                Debug.LogError("No 'EnterStreamCode' notification prefab is configured in the LckNotificationController.");
            }
        }

        /// <summary>
        /// Event handler that hides any active notification when a recording or stream begins,
        /// ensuring a clean view for the capture.
        /// </summary>
        private void OnCaptureStarted(LckResult result)
        {
            if (result.Success)
            {
                HideNotifications();
            }
        }

        /// <summary>
        /// Event handler that shows the 'VideoSaved' notification when a recording is successfully saved.
        /// </summary>
        private void OnRecordingSaved(LckResult<RecordingData> result)
        {
            if (result.Success)
            {
                ShowNotification(NotificationType.VideoSaved);
            }
            else
            {
                Debug.LogWarning($"Failed to show 'VideoSaved' notification. Error: {result.Error}, Message: {result.Message}");
            }
        }

        private void OnEchoDisabled(LckResult result, EchoDisableReason reason)
        {
            switch (reason)
            {
                case EchoDisableReason.LowStorage:
                    ShowNotification(NotificationType.EchoLowStorage);
                    break;
                case EchoDisableReason.Error:
                    ShowNotification(NotificationType.EchoError);
                    break;
            }
        }

        private void OnEchoSaved(LckResult<RecordingData> result)
        {
            if (result.Success)
            {
                ShowNotification(NotificationType.VideoSaved);
            }
            else
            {
                Debug.LogWarning($"Failed to show 'VideoSaved' notification for echo save. Error: {result.Error}, Message: {result.Message}");
            }
        }

        /// <summary>
        /// Immediately hides any currently displayed notification and stops any pending auto-hide timers.
        /// This is the primary method for clearing notifications from the screen.
        /// </summary>
        public void HideNotifications()
        {
            StopAllCoroutines();
            if (_currentNotification != null)
            {
                _onScreenUIController.OnNotificationEnded();
            }
            _currentNotification = null;
            foreach (var pair in _notifications)
            {
                pair.Value.HideNotification();
            }
        }

        /// <summary>
        /// Instantiates all notification prefabs defined in the `_notificationsInitializer` list
        /// and populates the internal dictionary for runtime access. This is called on Awake.
        /// </summary>
        public void InitializeNotifications()
        {
            DestroyNotifications();

            foreach (InitializerNotification notification in _notificationsInitializer)
            {
                // Instantiate the prefab, handling both Editor and runtime instantiation paths.
                #if UNITY_EDITOR
                var notif = (GameObject)PrefabUtility.InstantiatePrefab(notification.prefab);
                #else
                var notif = Instantiate(notification.prefab);
                #endif
                
                notif.SetActive(false);
                notif.transform.SetParent(_notificationsTransform, false);

                var notificationComponent = notif.GetComponent<LckBaseNotification>();
                if (notificationComponent != null)
                {
                    _notifications.Add(notification.Type, notificationComponent);
                    notificationComponent.SetSpawnedGameObject(notif);
                }
                else
                {
                    Debug.LogError($"Prefab for notification type '{notification.Type}' is missing a component that inherits from LckBaseNotification.", notif);
                }
            }
        }

        /// <summary>
        /// Destroys all previously instantiated notification GameObjects. Used for cleanup.
        /// </summary>
        public void DestroyNotifications()
        {
            _notifications.Clear();

            if (_notificationsTransform.childCount > 0)
            {
                for (int i = _notificationsTransform.childCount - 1; i >= 0; i--)
                {
                    var child = _notificationsTransform.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }
        }

        /// <summary>
        /// Shows a notification of a specific type. If another notification is already visible, it will be hidden first.
        /// This is the main public method for triggering a notification.
        /// </summary>
        /// <param name="type">The type of notification to display.</param>
        public void ShowNotification(NotificationType type)
        {
            if (LckCoreHandler.LckCoreInitializationResult?.IsOk == false)
            {
                Debug.LogError("Failed to show notification: " + type.ToString() + " LckCore failed initialization");
                return;
            }

            HideNotifications();           
            StartCoroutine(CreateNotification(type));
        }

        private IEnumerator CreateNotification(NotificationType type)
        {
            _onScreenUIController.OnNotificationStarted();

            if (_notifications.TryGetValue(type, out _currentNotification))
            {
                _currentNotification.ShowNotification();
            }
            else
            {
                Debug.LogError("No notification found with type: " + type.ToString());
                _onScreenUIController.OnNotificationEnded();
                yield break;
            }

            // If the notification is marked to 'RemainOnScreen', the coroutine exits and lets
            // something else (like user input) hide it later.
            if (_currentNotification.RemainOnScreen)
            {
                yield break;
            }

            // Otherwise, wait for the specified duration and then automatically hide it.
            if (_currentNotification.ShowDuration != _notificationShowDuration)
            {
                yield return new WaitForSeconds(_currentNotification.ShowDuration);
            }
            else
            {
                yield return new WaitForSeconds(_notificationShowDuration);
            }
                
            _currentNotification.HideNotification();
            _onScreenUIController.OnNotificationEnded();
            _currentNotification = null;
        }
    }
}