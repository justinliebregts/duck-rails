using Liv.Lck.Core;
using Liv.Lck.DependencyInjection;
using Liv.Lck.Tablet;
using System.Threading;
using System.Threading.Tasks;
using Liv.Lck.Core.Cosmetics;
using UnityEngine;
using UnityEngine.Events;

namespace Liv.Lck.Streaming
{
    /// <summary>
    /// The central controller for managing the user-facing streaming workflow.
    /// This class operates as a state machine to guide the user through the necessary steps
    /// for starting a stream with LIV Hub, such as logging in and confirming configuration.
    /// It is designed to be a reference implementation that developers can use directly or extend to
    /// create custom UI flows that adhere to the recommended LCK user experience.
    /// 
    /// To use, place this component on a GameObject in your scene and link the required dependencies in the Inspector.
    /// </summary>
    public class LckStreamingController : MonoBehaviour
    {
        [Tooltip("Enable this to see detailed logs from this controller in the Unity console. Recommended for development.")]
        [SerializeField]
        private bool _showDebugLogs = false;

        /// <summary>
        /// Provides access to the core LCK services for starting/stopping streams.
        /// This field is automatically populated by the LCK Dependency Injection system at runtime.
        /// </summary>
        [InjectLck]
        private ILckService _lckService;
        
        /// <summary>
        /// Provides access to the low-level LCK Core API for authentication and user status checks.
        /// This field is automatically populated by the LCK Dependency Injection system at runtime.
        /// </summary>
        [InjectLck]
        public ILckCore LckCore { get; private set; }

        [InjectLck]
        public ILckCosmeticsCoordinator LckCosmeticsCoordinator { get; private set; }

        /// <summary>
        /// A publicly accessible flag indicating if the entire user setup process is complete.
        /// When <c>true</c>, the streaming button will function as a start/stop toggle.
        /// When <c>false</c>, pressing the button will re-initiate the setup flow.
        /// </summary>
        public bool IsConfiguredCorrectly { get; private set; } = false;

        [Tooltip("A reference to the controller responsible for displaying UI notifications (e.g., 'Enter this code:', 'Please subscribe'). Assign this in the Inspector.")]
        [SerializeField]
        private LckNotificationController _notificationController;
        [Tooltip("Reference to the Top Buttons Controller which handles switching to Camera or Stream modes on the tablet UI")]
        [SerializeField]
        private LckTopButtonsController _topButtonsController;

        [Tooltip("This event is invoked when the user presses the stream button but the setup is not yet complete. Use this to trigger visual feedback, like a button shake or an error icon.")]
        [SerializeField]
        private UnityEvent _onStreamButtonError;
        
        [Tooltip("This event is invoked when the user presses the stream button and the setup is complete, just before streaming starts. Use this to trigger positive feedback, like a button color change.")]
        [SerializeField]
        private UnityEvent _onStreamButtonPressWithCorrectConfig;

        [Header("Game Objects disabled when streaming package removed")]
        [SerializeField]
        private GameObject _topButtonsControllerGameObject;
        [SerializeField]
        private GameObject _livHubButton;

        #region State Machine Properties
        /// <summary>
        /// The currently active state in the state machine.
        /// </summary>
        public LckStreamingBaseState CurrentState { get; private set; } = null;
        public LckStreamingGetCurrentState GetCurrentState { get; private set; } = new LckStreamingGetCurrentState();

        public LckStreamingShowCodeState ShowCodeState { get; private set; } = new LckStreamingShowCodeState();

        public LckStreamingWaitingForConfigureState WaitingForConfigureState { get; private set; } = new LckStreamingWaitingForConfigureState();       

        public LckStreamingConfiguredCorrectlyState ConfiguredCorrectlyState { get; private set; } = new LckStreamingConfiguredCorrectlyState();

        public LckInternalErrorState InternalErrorState { get; private set; } = new LckInternalErrorState();

        public LckMissingTrackingIdState MissingTrackingIdState { get; private set; } = new LckMissingTrackingIdState();

        public LckInvalidArgumentState InvalidArgumentState { get; private set; } = new LckInvalidArgumentState();

        public LckRateLimiterBackoffState RateLimiterBackoffState { get; private set; } = new LckRateLimiterBackoffState();

        public LckServiceUnavailableState ServiceUnavailableState { get; private set; } = new LckServiceUnavailableState();
        #endregion

        /// <summary>
        /// Manages the lifecycle of asynchronous operations within the states. It is used to cancel
        /// any pending tasks (like polling for login completion) when switching states or when the object is destroyed.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; private set; } = new CancellationTokenSource();

        private void Start()
        {
            if (_lckService != null)
            {
                _lckService.OnStreamingStarted += OnStreamingStarted;
            }

            LckMonoBehaviourMediator.OnApplicationLifecycleEvent += OnApplicationLifecycle;
        }

        private void OnApplicationLifecycle(LckMonoBehaviourMediator.ApplicationLifecycleEventType eventType)
        {
            switch (eventType)
            {
                case LckMonoBehaviourMediator.ApplicationLifecycleEventType.Pause:
                    OnSystemPaused();
                    break;
                case LckMonoBehaviourMediator.ApplicationLifecycleEventType.Resume:
                    OnSystemResumed();
                    break;
                case LckMonoBehaviourMediator.ApplicationLifecycleEventType.HMDIdle:
                    OnHMDIdle();
                    break;
                case LckMonoBehaviourMediator.ApplicationLifecycleEventType.HMDActive:
                    OnHMDActive();
                    break;
            }
        }

        private void OnSystemPaused()
        {
            Log("[LCK Streaming Controller] System paused - stopping any active polling");
            StopCheckingStates();

            // Stop streaming if active to prevent encoder corruption during system pause
            // During system pause, the GPU resources and media codecs may become invalid
            if (_lckService != null && _lckService.IsStreaming().Result)
            {
                Log("[LCK Streaming Controller] Stopping streaming due to system pause");
                _lckService.StopStreaming();

                // Show notification to inform user why streaming stopped
                _notificationController.ShowNotification(NotificationType.InternalError);
            }
        }

        private void OnSystemResumed()
        {
            Log("[LCK Streaming Controller] System resumed");

            // Safety check: ensure streaming state is synchronized after resume
            if (_lckService != null)
            {
                var isActuallyStreaming = _lckService.IsStreaming().Result;
                Log($"[LCK Streaming Controller] Post-resume streaming state: {isActuallyStreaming}");
            }

            // Refresh the current state to ensure UI reflects actual state
            if (IsConfiguredCorrectly)
            {
                // Re-check configuration state in case anything changed during pause
                CheckCurrentState();
            }
        }

        private void OnHMDIdle()
        {
            Log("[LCK Streaming Controller] HMD idle detected - stopping polling but keeping stream active");
            // Only stop polling, don't stop streaming since the encoder is still valid
            StopCheckingStates();
        }

        private void OnHMDActive()
        {
            Log("[LCK Streaming Controller] HMD active again");
            // Resume polling if we were configured
            if (IsConfiguredCorrectly)
            {
                CheckCurrentState();
            }
        }        

        /// <summary>
        /// Begins the user setup and streaming validation process.
        /// This should be called when the user first interacts with the streaming UI, such as by pressing a
        /// 'Stream' button when not yet configured. Typically, this is hooked up to a UnityEvent in the Inspector.
        /// </summary>
        public void CheckCurrentState()
        {
            SwitchState(GetCurrentState);
        }

        /// <summary>
        /// Cancels any ongoing polling or asynchronous checks within the current state.
        /// This is used for when the user navigates away from the streaming UI tab, ensuring that
        /// background tasks are properly stopped.
        /// </summary>
        public void StopCheckingStates()
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Transitions the controller to a new state. This method ensures that the previous state is
        /// cleaned up correctly and the new state is initialized. It also manages the cancellation of any running tasks.
        /// </summary>
        /// <param name="state">The new state to transition to.</param>
        public void SwitchState(LckStreamingBaseState state)
        {
            if (CurrentState == state)
            {
                Log($"[LCK Streaming Controller] tried switching to the same state! Current State: <color=#FF0000>{CurrentState.GetType().Name}</color>" +
                    $" to: <color=#FF0000>{state.GetType().Name}</color>");
                return;
            }

            // Cancel any pending operations from the previous state before switching.
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();

            Log(CurrentState != null
                ? $"[LCK Streaming Controller] changing states from: <color=#42f542>{CurrentState.GetType().Name}</color> to: <color=#42f542>{state.GetType().Name}</color>"
                : $"[LCK Streaming Controller] changing states from: <color=#42f542>null</color> to: <color=#42f542>{state.GetType().Name}</color>");

            CurrentState = state;
 
            // Update the public-facing configuration flag based on the new state.
            IsConfiguredCorrectly = CurrentState is LckStreamingConfiguredCorrectlyState;
            
            CurrentState.EnterState(this);
        }

        /// <summary>
        /// Determines if the user is correctly configured in the Liv Hub to start streaming and will then start the stream.
        /// Currently called from LckStreamButton.
        /// </summary>
        public void StartStreaming()
        {
            if (IsConfiguredCorrectly == false)
            {
                _onStreamButtonError.Invoke();
            }
            else
            {
                _ = StartStreamIfNoLivHubChanges();
            }
        }

        private async Task StartStreamIfNoLivHubChanges()
        {
             var result = await LckCore.HasUserConfiguredStreaming();

             if (result.IsOk == true && result.Ok)
             {
                 // stream config is still good, start stream
                 _onStreamButtonPressWithCorrectConfig.Invoke();
                 _lckService.StartStreaming();
                 return;
             }
             else
             {
                if (result.IsOk == false && result.Err != CoreError.UserNotLoggedIn)
                {
                    // something went wrong, go to internal error state
                    SwitchState(InternalErrorState);
                    _onStreamButtonError.Invoke();
                    return;
                }
                else
                {
                    // something went wrong, check stream configuration again
                    CheckCurrentState();
                    _onStreamButtonError.Invoke();
                    return;
                }                      
             }
        }

        /// <summary>
        /// A dedicated method to force the stream to stop if it is currently active.
        /// </summary>
        public void StopStreaming()
        {
            if (_lckService == null)
            {
                LckLog.LogWarning("LCK Could not get Service");
                return;
            }

            if (_lckService.IsStreaming().Result)
            {
                _lckService.StopStreaming();
            }
        }

        private void OnStreamingStarted(LckResult result)
        {
            if (result.Success == false)
            {
                _notificationController.ShowNotification(NotificationType.UnknownStreamingError);
            }
        }

        public void GoToErrorState()
        {
            SwitchState(InternalErrorState);
        }

        #region Helper Methods
        public void LogError(string error)
        {
            if (_showDebugLogs) Debug.LogError("[LCK Streaming Controller] " + error);
        }
        
        public void Log(string message)
        {
            if (_showDebugLogs) Debug.Log("[LCK Streaming Controller] " + message);
        }

        /// <summary>
        /// Shows a notification of a specific type using the linked LckNotificationController.
        /// </summary>
        /// <param name="type">The type of notification to display.</param>
        public void ShowNotification(NotificationType type)
        {
            _notificationController.ShowNotification(type);
        }

        /// <summary>
        /// Hides any currently visible notifications via the LckNotificationController.
        /// </summary>
        public void HideNotifications()
        {
            _notificationController.HideNotifications();
        }

        /// <summary>
        /// Sets the login code text on the notification UI.
        /// </summary>
        /// <param name="code">The login code to display.</param>
        public void SetNotificationStreamCode(string code)
        {
            _notificationController.SetNotificationStreamCode(code);
        }

        /// <summary>
        /// Opens the Camera Page on the tablet and sets the correct visuals manually
        /// </summary>
        public void ToggleCameraPage()
        {
            //NOTE: this will also trigger a StopCheckingStates from the Top Buttons Controller Unity Event
            _topButtonsController.ToggleCameraPage(true);
            _topButtonsController.SetCameraPageVisualsManually();
        }

        private void OnValidate()
        {
#if HAS_LCK_STREAMING
            if (_topButtonsControllerGameObject && _topButtonsControllerGameObject.activeSelf == false)
            {
                _topButtonsControllerGameObject.SetActive(true);
            }

            if (_livHubButton && _livHubButton.activeSelf == false)
            {
                _livHubButton.SetActive(true);
            }
#else
            if (_topButtonsControllerGameObject && _topButtonsControllerGameObject.activeSelf == true)
            {
                _topButtonsControllerGameObject.SetActive(false);
            }

            if (_livHubButton && _livHubButton.activeSelf == true)
            {
                _livHubButton.SetActive(false);
            }
#endif
        }
        #endregion

        private void OnDestroy()
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();

            LckMonoBehaviourMediator.OnApplicationLifecycleEvent -= OnApplicationLifecycle;

            if (_lckService != null)
            {
                if (_lckService.IsStreaming().Result == true)
                {
                    _lckService.StopStreaming();
                }

                _lckService.OnStreamingStarted -= OnStreamingStarted;
            }
        }
    }   
}
