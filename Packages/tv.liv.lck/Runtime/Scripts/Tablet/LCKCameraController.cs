using Liv.Lck.Smoothing;
using Liv.Lck.UI;
using System;
using System.Collections.Generic;
using Liv.Lck.DependencyInjection;
using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// A comprehensive camera controller that manages multiple camera modes (Selfie, First Person, Third Person)
    /// for the LCK recording system. This class serves as a central hub, connecting UI controls (like buttons for FOV,
    /// smoothing, and mode switching) to the underlying camera components with the LCK service.
    /// 
    /// It is designed to be a reference implementation, demonstrating how to:
    /// - Switch between different virtual cameras.
    /// - Apply stabilisation and smoothing.
    /// - Handle user input to adjust camera parameters in real-time.
    /// - Manage camera orientation (Landscape/Portrait) and its effect on FOV.
    /// - Interact with the ILckService to control recording, streaming, and track settings.
    /// - Automatically configure rendering layers to hide certain objects (like the tablet itself) in specific camera views.
    /// </summary>
    [DefaultExecutionOrder(-890)] // Execute early to ensure camera setup is ready for other scripts.
    public class LCKCameraController : MonoBehaviour
    {
        // Injected reference to the core LCK service. This is the main interface for controlling recording,
        // streaming, photo capture, and camera settings.
        [InjectLck]
        private ILckService _lckService;
        
        [Header("Options")]
        [SerializeField]
        [Tooltip("If true, the script will automatically manage layers and culling masks. It will move objects in the 'ObjectsHiddenFromSelfieCamera' list to the 'Tablet Rendering Layer' and adjust camera culling masks to hide this layer in Selfie mode and show it in other modes.")]
        private bool _modifyRenderLayerAndCullingMasks = true;
        
        [SerializeField]
        [Tooltip("The name of the Unity layer used to tag objects that should be hidden from the selfie camera. This layer must exist in the project's Tag and Layer Manager.")]
        private string _tabletRenderingLayer = "LCK Tablet";

        [FormerlySerializedAs("_objectsOnTabletRenderingLayer")]
        [SerializeField] 
        [Tooltip("A list of all GameObjects (e.g., the tablet model itself) that should be made invisible to the selfie camera.")]
        private List<GameObject> _objectsHiddenFromSelfieCamera = new List<GameObject>();

        [SerializeReference]
        [Tooltip("A ScriptableObject that implements the ILckQualityConfig interface. This object defines the available quality levels (resolution, bitrate, etc.) for recording and streaming.")]
        private ScriptableObject _qualityConfig;

        [SerializeField]
        [Tooltip("The transform representing the user's head or HMD. This is the primary anchor for first-person and third-person camera positioning. If null, it will default to the main camera's transform.")]
        private Transform _hmdTransform;
        
        /// <summary>
        /// Public accessor for the HMD transform, with a fallback to the main camera.
        /// This is the primary reference point for camera positioning relative to the player.
        /// </summary>
        public Transform HmdTransform
        {
            get
            {
                if (_hmdTransform == null)
                {
                    _hmdTransform = Camera.main.transform;
                }
                return _hmdTransform;
            }
            set 
            {
                _hmdTransform = value;
            }
        }
        
        [SerializeField]
        [Tooltip("A multiplier applied to the value from the third-person distance UI button to determine the actual camera distance.")]
        private float _thirdPersonDistanceMultiplier = 0.75f;
        
        [SerializeField]
        [Tooltip("The default angle (in degrees) of the third-person camera, looking down at the player.")]
        private float _thirdPersonHeightAngle = 25;

        [SerializeField]
        [Tooltip("Mode that is used to determine when the active camera's position is updated. " +
                 "Depending on update order / movement setup, changing this can fix tablet jitter in captures.")]
        private UpdateTimingMode _cameraPositionUpdateTimingMode = UpdateTimingMode.LateUpdate;

        /// <summary>
        /// Controls when the camera position is updated.
        /// </summary>
        /// <remarks>
        /// By default, <see cref="LateUpdate"/> is used for camera movement to ensure it happens after all other game
        /// logic and physics for the frame (like player movement) have been completed, preventing jitter. Depending on
        /// the update order of other scripts / how movement is handled, it may be necessary to change this to avoid
        /// tablet jitter in captures.
        /// </remarks>
        public UpdateTimingMode CameraPositionUpdateTimingMode
        {
            get => _cameraPositionUpdateTimingMode;
            set => _cameraPositionUpdateTimingMode = value;
        }

        [Header("Main References")]
        [SerializeField]
        private LCKSettingsButtonsController _settingsButtonsController; // Manages which set of UI buttons is visible.
        [SerializeField]
        private LckTopButtonsController _topButtonsController;
        [SerializeField]
        private RectTransform _monitorTransform; // The UI panel that displays the camera's output. Used for flipping and resizing.
        [SerializeField]
        private LckQualitySelector _qualitySelector; // The UI component for changing video quality.

        // --- UI Button References for each camera mode ---
        [Header("Button References")]
        [Header("Selfie")]
        [SerializeField]
        private LckDoubleButton _selfieFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _selfieSmoothingDoubleButton;

        [Header("First Person")]
        [SerializeField]
        private LckDoubleButton _firstPersonFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _firstPersonSmoothingDoubleButton;

        [Header("Third Person")]
        [SerializeField]
        private LckDoubleButton _thirdPersonFOVDoubleButton;
        [SerializeField]
        private LckDoubleButton _thirdPersonSmoothingDoubleButton;
        [SerializeField]
        private LckDoubleButton _thirdPersonDistanceDoubleButton;

        [Header("Portrait Landscape Toggle")]
        [SerializeField]
        private LckButton _orientationButton;

        // --- Camera and Stabilizer References for each mode ---
        [Header("Camera Modes")]
        [Header("Selfie")]
        [SerializeField]
        private LckCamera _selfieCamera;
        [SerializeField]
        private LckStabilizer _selfieStabilizer; // Handles smoothing for the selfie camera.

        [Header("First Person")]
        [SerializeField]
        private LckCamera _firstPersonCamera;
        [SerializeField]
        private LckStabilizer _firstPersonStabilizer;

        [Header("Third Person")]
        [SerializeField]
        private LckCamera _thirdPersonCamera;

        [SerializeField]
        private LckStabilizer _thirdPersonStabilizer;
        
        // --- Internal State Variables ---
        private float _thirdPersonDistance = 1;
        private bool _isThirdPersonFront = true;
        
        private bool _isSelfieFront = true;
        private LckCameraOrientation _currentCameraOrientation = LckCameraOrientation.Landscape;
        private bool _justTransitioned = false; // Flag to force instant camera snapping after a mode change.
        private bool _gameAudioRecordingEnabled = true;

        private CameraMode _currentCameraMode = CameraMode.Selfie;
        public Action<CameraMode> OnCameraModeChanged;

        private void OnValidate()
        {
            // Editor-time check to ensure the assigned quality config is valid.
            if(_qualityConfig != null && !(_qualityConfig is ILckQualityConfig))
            {
                Debug.LogError($"LCK Quality Config must implement ILckQualityConfig interface");
            }
        }
        
        /// <summary>
        /// A global flag indicating that a user is currently interacting with a collider-based button.
        /// Set by external button/interactables and can be read by other systems to prevent conflicting actions.
        /// It is also reset when the application resumes focus.
        /// </summary>
        public static bool ColliderButtonsInUse = false;

        private void OnApplicationFocus(bool focus)
        {
            if (focus == true)
            {
                ColliderButtonsInUse = false;
            }
        }

        private void Start()
        {
            if(_modifyRenderLayerAndCullingMasks)
            {
                SetTabletLayer();
            }

            // Initialise the quality selector UI with options from the config file.
            _qualitySelector.OnQualityOptionChanged += OnQualityOptionSelected;
            _qualitySelector.InitializeOptions((_qualityConfig as ILckQualityConfig).GetQualityOptionsForSystem());

            // Set initial camera state.
            SetActiveLckCamera(_selfieCamera.CameraId);
            SetSelfieCameraOrientation(Vector3.zero, Vector3.zero);        
            
            // Subscribe to LCK service events to react to capture state changes.
            _lckService.OnRecordingStarted += OnCaptureStart;
            _lckService.OnRecordingStopped += OnCaptureStopped;
            _lckService.OnStreamingStarted += OnCaptureStart;
            _lckService.OnStreamingStopped += OnCaptureStopped;
        }

        #region UNITY METHODS
        
        private void Awake()
        {
            // Manually initialise the LCK service if it hasn't been injected.
            // This provides a fallback if the LckServiceHelper isn't present in the scene.
            bool hasLckService = LckDiContainer.Instance.HasService<ILckService>();
            if(!hasLckService)
            {
                var lckDiContainer = LckDiContainer.Instance;
                LckServiceInitializer.ConfigureServices(lckDiContainer, (LckQualityConfig)_qualityConfig);
                _lckService = LckDiContainer.Instance.GetService<ILckService>();
            }
        }

        /// <summary>
        /// Configures the rendering layers and camera culling masks to hide specified objects from the selfie camera.
        /// </summary>
        private void SetTabletLayer()
        {
            int tabletLayer = LayerMask.NameToLayer(_tabletRenderingLayer);
            if (tabletLayer == -1)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LckLog.LogError($"LCK Tablet layer '{_tabletRenderingLayer}' not found. Please add it to Project Settings > Tags and Layers.");
#endif
                return;
            }

            // Assign all specified objects to the tablet layer.
            foreach (var objectToHide in _objectsHiddenFromSelfieCamera)
            {
                objectToHide.layer = tabletLayer;
            }

            // Modify camera culling masks:
            // - Selfie camera: Remove the tablet layer (so it doesn't see the tablet).
            _selfieCamera.GetCameraComponent().cullingMask &= ~(1 << tabletLayer);
            // - Other cameras: Add the tablet layer (so they do see the tablet).
            _firstPersonCamera.GetCameraComponent().cullingMask |= 1 << tabletLayer;
            _thirdPersonCamera.GetCameraComponent().cullingMask |= 1 << tabletLayer;
        }

        /// <summary>
        /// Callback for when a new quality option is selected in the UI.
        /// It updates the track descriptors for both recording and streaming in the LCK service.
        /// </summary>
        private void OnQualityOptionSelected(QualityOption qualityOption)
        {
            var recordingDescriptor = GetDescriptorForCurrentOrientation(qualityOption.RecordingCameraTrackDescriptor);
            _lckService.SetTrackDescriptor(LckCaptureType.Recording, recordingDescriptor);
            
            var streamingDescriptor = GetDescriptorForCurrentOrientation(qualityOption.StreamingCameraTrackDescriptor);
            _lckService.SetTrackDescriptor(LckCaptureType.Streaming, streamingDescriptor);
        }

        /// <summary>
        /// Adjusts a CameraTrackDescriptor's resolution based on the current camera orientation (Landscape vs. Portrait).
        /// </summary>
        private CameraTrackDescriptor GetDescriptorForCurrentOrientation(CameraTrackDescriptor descriptor)
        {
            var resolution = descriptor.CameraResolutionDescriptor;
            descriptor.CameraResolutionDescriptor = resolution.GetResolutionInOrientation(_currentCameraOrientation);
            return descriptor;
        }

        /// <summary>
        /// Updates the position of the current camera.
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (_lckService == null) return;

            // Dispatch to the appropriate method to update the camera's position and rotation for the current mode.
            switch (_currentCameraMode)
            {
                case CameraMode.FirstPerson:
                    ProcessFirstCameraPosition();
                    break;
                case CameraMode.ThirdPerson:
                    ProcessThirdCameraPosition();
                    break;
                case CameraMode.Selfie:
                    // Selfie camera is parented to the tablet, so it doesn't need continuous position updates here.
                    break;
            }
        }

        /// <summary>
        /// Subscribes to events from all UI control buttons.
        /// </summary>
        private void OnEnable()
        {
            _settingsButtonsController.OnCameraModeChanged += CameraModeChanged;

            // Selfie
            _selfieFOVDoubleButton.OnValueChanged += ProcessSelfieFov;
            _selfieSmoothingDoubleButton.OnValueChanged += ProcessSelfieSmoothness;

            // First Person
            _firstPersonFOVDoubleButton.OnValueChanged += ProcessFirstPersonFov;
            _firstPersonSmoothingDoubleButton.OnValueChanged += ProcessFirstPersonSmoothness;

            // Third Person
            _thirdPersonFOVDoubleButton.OnValueChanged += ProcessThirdPersonFov;
            _thirdPersonSmoothingDoubleButton.OnValueChanged += ProcessThirdPersonSmoothness;
            _thirdPersonDistanceDoubleButton.OnValueChanged += ProcessThirdPersonDistance;
        }

        /// <summary>
        /// Unsubscribes from all events to prevent errors and memory leaks.
        /// </summary>
        private void OnDisable()
        {
            _settingsButtonsController.OnCameraModeChanged -= CameraModeChanged;

            // Selfie
            _selfieFOVDoubleButton.OnValueChanged -= ProcessSelfieFov;
            _selfieSmoothingDoubleButton.OnValueChanged -= ProcessSelfieSmoothness;

            // First Person
            _firstPersonFOVDoubleButton.OnValueChanged -= ProcessFirstPersonFov;
            _firstPersonSmoothingDoubleButton.OnValueChanged -= ProcessFirstPersonSmoothness;

            // Third Person
            _thirdPersonFOVDoubleButton.OnValueChanged -= ProcessThirdPersonFov;
            _thirdPersonSmoothingDoubleButton.OnValueChanged -= ProcessThirdPersonSmoothness;
            _thirdPersonDistanceDoubleButton.OnValueChanged -= ProcessThirdPersonDistance;
        }

        /// <summary>
        /// Cleans up resources, especially stopping any active recording and unsubscribing from service events.
        /// </summary>
        private void OnDestroy()
        {
            _qualitySelector.OnQualityOptionChanged -= OnQualityOptionSelected;

            if (_lckService != null)
            {
                if (_lckService.IsRecording().Result == true)
                {
                    _lckService.StopRecording();
                } 
                _lckService.OnRecordingStarted -= OnCaptureStart;
                _lckService.OnRecordingStopped -= OnCaptureStopped;
                _lckService.OnStreamingStarted -= OnCaptureStart;
                _lckService.OnStreamingStopped -= OnCaptureStopped;     
            }
        }
        
        /// <summary>
        /// Depending on the <see cref="CameraPositionUpdateTimingMode"/>, may update the position of the current camera.
        /// </summary>
        private void LateUpdate()
        {
            if (CameraPositionUpdateTimingMode == UpdateTimingMode.LateUpdate)
                UpdateCameraPosition();
        }
        
        /// <summary>
        /// Depending on the <see cref="CameraPositionUpdateTimingMode"/>, may update the position of the current camera.
        /// </summary>
        private void Update()
        {
            if (CameraPositionUpdateTimingMode == UpdateTimingMode.Update)
                UpdateCameraPosition();
        }
        
        /// <summary>
        /// Depending on the <see cref="CameraPositionUpdateTimingMode"/>, may update the position of the current camera.
        /// </summary>
        private void FixedUpdate()
        {
            if (CameraPositionUpdateTimingMode == UpdateTimingMode.FixedUpdate)
                UpdateCameraPosition();
        }
        #endregion

        #region SELFIE METHODS
        
        /// <summary>
        /// Sets the local position and rotation of the selfie camera stabiliser. Used for flipping the camera.
        /// </summary>
        private void SetSelfieCameraOrientation(Vector3 position, Vector3 rotation)
        {
            _selfieStabilizer.transform.localPosition = position;
            _selfieStabilizer.transform.localRotation = Quaternion.Euler(rotation);
            _selfieStabilizer.ReachTargetInstantly(); // Instantly snap to the new orientation.
        }

        private void ProcessSelfieFov(float value)
        {
            _selfieCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessSelfieSmoothness(float value)
        {
            // Scale the raw UI value (e.g., 1-10) into appropriate smoothing values for the stabiliser.
            // These multipliers are chosen empirically to feel good.
            _selfieStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _selfieStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        #endregion

        #region FIRST PERSON METHODS
        private void ProcessFirstPersonFov(float value)
        {
            _firstPersonCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessFirstPersonSmoothness(float value)
        {
            _firstPersonStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _firstPersonStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        /// <summary>
        /// Updates the position and rotation of the first-person camera to match the HMD.
        /// </summary>
        private void ProcessFirstCameraPosition()
        {
            // Position the stabiliser at the HMD's location, with a slight forward offset to avoid clipping.
            _firstPersonStabilizer.transform.position = HmdTransform.position + (HmdTransform.forward * 0.05f);
            _firstPersonStabilizer.transform.rotation = HmdTransform.rotation;

            // If we just switched to this mode, snap the camera instantly to avoid a jarring lerp from its old position.
            if (_justTransitioned)
            {
                _firstPersonStabilizer.ReachTargetInstantly();
                _justTransitioned = false;
            }
        }

        #endregion

        #region THIRD PERSON METHODS
        private void ProcessThirdPersonFov(float value)
        {
            _thirdPersonCamera.GetCameraComponent().fieldOfView = CalculateCorrectFOV(value);
        }

        private void ProcessThirdPersonSmoothness(float value)
        {
            _thirdPersonStabilizer.PositionalSmoothing = (value * 0.1f * 0.3f);
            _thirdPersonStabilizer.RotationalSmoothing = (value * 0.1f * 0.8f);
        }

        private void ProcessThirdPersonDistance(float value)
        {
            _thirdPersonDistance = value;
            _justTransitioned = true; // Force an instant snap to the new distance.
        }

        /// <summary>
        /// Calculates the orbital position and rotation for the third-person camera.
        /// </summary>
        private void ProcessThirdCameraPosition()
        {
            // Get the HMD's forward direction, but flattened onto the horizontal plane (y=0).
            Vector3 forward = new Vector3(HmdTransform.forward.x, 0, HmdTransform.forward.z);
            forward.Normalize();

            // Flip the direction if the camera is in "front" mode.
            if (!_isThirdPersonFront)
            {
                forward *= -1;
            }

            // Calculate the angle of the forward vector relative to the world's forward direction.
            float forwardAngle = Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);

            // Calculate the camera's offset from the player. This is done by creating a base offset vector
            // and then rotating it to match the player's orientation and the desired height angle.
            Vector3 offset =
                Quaternion.AngleAxis(forwardAngle, Vector3.up) // Yaw rotation to follow the player
                * Quaternion.AngleAxis(_thirdPersonHeightAngle, -Vector3.right) // Pitch rotation for height
                * new Vector3(0, 0, _thirdPersonDistance * _thirdPersonDistanceMultiplier); // Base distance offset

            // Position the stabiliser at the target location and make it look at the HMD.
            _thirdPersonStabilizer.transform.position = HmdTransform.position + offset;
            _thirdPersonStabilizer.transform.LookAt(HmdTransform.position);

            if (_justTransitioned)
            {
                _thirdPersonStabilizer.ReachTargetInstantly();
                _justTransitioned = false;
            }
        }
        #endregion

        /// <summary>
        /// A helper function to set the FOV on a specific camera.
        /// </summary>
        private void SetFOV(CameraMode mode, float fov)
        {
            switch (mode)
            {
                case CameraMode.Selfie:
                    _selfieCamera.GetCameraComponent().fieldOfView = fov;
                    break;
                case CameraMode.FirstPerson:
                    _firstPersonCamera.GetCameraComponent().fieldOfView = fov;
                    break;
                case CameraMode.ThirdPerson:
                    _thirdPersonCamera.GetCameraComponent().fieldOfView = fov;
                    break;
            }
        }

        /// <summary>
        /// Toggles microphone capture on or off via the LCK service.
        /// </summary>
        public void ToggleMicrophoneRecording(bool isMicOn)
        {
            if (_lckService == null)
            {
                LckLog.LogError("No Lck Service found when trying to set mic state to: " + isMicOn);
                return;
            }

            var micResult = _lckService.SetMicrophoneCaptureActive(isMicOn);
            if (!micResult.Success)
            {
                LckLog.LogError($"LCK Could not enable microphone capture: {micResult.Error}");
            }

        }

        /// <summary>
        /// Toggles game audio capture on or off via the LCK service.
        /// </summary>
        public void ToggleGameAudio()
        {
            _gameAudioRecordingEnabled = !_gameAudioRecordingEnabled;
            _lckService.SetGameAudioCaptureActive(_gameAudioRecordingEnabled);
        }

        /// <summary>
        /// Starts or stops recording based on the current state.
        /// </summary>
        public void ToggleRecording()
        {
            if (_lckService == null) return;

            if (_lckService.IsRecording().Result)
            {
                _lckService.StopRecording();
            }
            else
            {
                // Disable orientation, quality and top buttons immediately to prevent changes during capture startup.
                SetOrientationQualityAndTopButtonsIsDisabledState(true);
                _lckService.StartRecording();
            }
        }
        
        /// <summary>
        /// Callback executed when a capture (recording or streaming) starts.
        /// </summary>
        public void OnCaptureStart(LckResult result)
        {
            // If the capture failed to start, re-enable the UI so the user can try again.
            if (!result.Success)
            {

                // if streaming or recording fails to start, re-enable buttons
                SetOrientationQualityAndTopButtonsIsDisabledState(false);
            }
        }

        /// <summary>
        /// Callback executed when a capture stops. Re-enables the UI.
        /// </summary>
        public void OnCaptureStopped(LckResult result)
        {
            SetOrientationQualityAndTopButtonsIsDisabledState(false);
        }

        /// <summary>
        /// Disables or enables the orientation, quality and top buttons UI. This is used to prevent
        /// changes to resolution or orientation while a recording is in progress.
        /// </summary>
        public void SetOrientationQualityAndTopButtonsIsDisabledState(bool state)
        {
            _topButtonsController.SetTopButtonsIsDisabledState(state);
            _qualitySelector.SetQualityButtonIsDisabledState(state);
            _orientationButton.SetIsDisabled(state);
        }
        
        /// <summary>
        /// Toggles the camera between Landscape and Portrait orientation.
        /// </summary>
        public void ToggleOrientation()
        {
            // Prevent orientation changes while capturing.
            if (_lckService.IsCapturing().Result) return;

            _currentCameraOrientation = _currentCameraOrientation == LckCameraOrientation.Landscape
                ? LckCameraOrientation.Portrait : LckCameraOrientation.Landscape;

            _lckService.SetCameraOrientation(_currentCameraOrientation); 
            
            // Resize the preview monitor to match the new aspect ratio.
            // These values are based on a scaled 16:9 aspect ratio.
            _monitorTransform.sizeDelta = _currentCameraOrientation == LckCameraOrientation.Landscape
                ? new Vector2(1109, 624) // 16:9
                : new Vector2(352, 624); // 9:16
            
            // Recalculate the FOV for the new orientation.
            GetCurrentModeCamera().fieldOfView = CalculateCorrectFOV(GetCurrentModeFOV());
        }

        /// <summary>
        /// Gets the Unity Camera component for the currently active mode.
        /// </summary>
        private Camera GetCurrentModeCamera()
        {
            switch (_currentCameraMode)
            {
                case CameraMode.Selfie: return _selfieCamera.GetCameraComponent();
                case CameraMode.FirstPerson: return _firstPersonCamera.GetCameraComponent();
                case CameraMode.ThirdPerson: return _thirdPersonCamera.GetCameraComponent();
                default: throw new System.Exception("Invalid Camera Mode");
            }
        }

        /// <summary>
        /// Calculates the correct vertical field-of-view (FOV) based on the current orientation.
        /// In Landscape, the incoming value is used directly.
        /// In Portrait, the incoming value is treated as the desired *vertical* FOV for a landscape view,
        /// and this function calculates the new vertical FOV needed to maintain the same *horizontal* FOV
        /// in the new, narrower aspect ratio. This prevents the view from feeling "zoomed in" in portrait mode.
        /// </summary>
        /// <param name="incomingVerticalFOV">The desired vertical FOV for a standard landscape aspect ratio.</param>
        /// <returns>The adjusted vertical FOV for the current orientation.</returns>
        private float CalculateCorrectFOV(float incomingVerticalFOV)
        {
            if (_currentCameraOrientation == LckCameraOrientation.Landscape)
            {
                return incomingVerticalFOV;
            }
            else
            {
                // For Portrait, we want to maintain the horizontal field of view.
                var currentResolution = _lckService.GetDescriptor().Result.cameraTrackDescriptor.CameraResolutionDescriptor;
                float portraitAspect = currentResolution.Height / (float)currentResolution.Width; // e.g., 1920 / 1080
                
                // Convert the desired vertical FOV to its horizontal equivalent in portrait aspect ratio.
                float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(incomingVerticalFOV, portraitAspect);
                
                // Unity's camera.fieldOfView is always vertical, so we return the new vertical FOV.
                return horizontalFOV;
            }
        }

        /// <summary>
        /// Gets the raw FOV value from the UI button for the current camera mode.
        /// </summary>
        private float GetCurrentModeFOV()
        {
            switch (_currentCameraMode)
            {
                case CameraMode.Selfie: return _selfieFOVDoubleButton.Value;
                case CameraMode.FirstPerson: return _firstPersonFOVDoubleButton.Value;
                case CameraMode.ThirdPerson: return _thirdPersonFOVDoubleButton.Value;
                default: throw new System.Exception("Invalid Camera Mode");
            }
        }

        /// <summary>
        /// Flips the selfie camera 180 degrees to switch between front-facing and rear-facing views.
        /// </summary>
        public void ProcessSelfieFlip()
        {
            _isSelfieFront = !_isSelfieFront;
            SetMonitorScale(CameraMode.Selfie); // Flip the preview monitor to match.

            if (_isSelfieFront)
            {
                SetSelfieCameraOrientation(Vector3.zero, Vector3.zero);
            }
            else
            {
                SetSelfieCameraOrientation(Vector3.zero, new Vector3(0, 180, 0));          
            }

            _selfieStabilizer.ReachTargetInstantly();
        }
        
        /// <summary>
        /// Flips the local scale of the preview monitor so that the image is not mirrored when the camera is "facing" the user.
        /// </summary>
        private void SetMonitorScale(CameraMode mode)
        {
            Vector3 negative = new Vector3(-1, 1, 1);
            Vector3 positive = Vector3.one;

            switch (mode)
            {
                case CameraMode.Selfie:
                    _monitorTransform.localScale = _isSelfieFront ? positive : negative;
                    break;
                case CameraMode.FirstPerson:
                    // First person is always "looking out", so it's mirrored on the tablet screen.
                    _monitorTransform.localScale = negative;
                    break;
                case CameraMode.ThirdPerson:
                    _monitorTransform.localScale = _isThirdPersonFront ? positive : negative;
                    break;
            }
        }

        /// <summary>
        /// Toggles the third-person camera between being behind the player and in front of the player.
        /// </summary>
        public void ProcessThirdPersonPosition()
        {
            _isThirdPersonFront = !_isThirdPersonFront;
            _justTransitioned = true;
            SetMonitorScale(CameraMode.ThirdPerson);
        }

        /// <summary>
        /// The main callback for when the camera mode is changed via the UI.
        /// </summary>
        private void CameraModeChanged(CameraMode mode)
        {
            _currentCameraMode = mode;
            _justTransitioned = true; // Flag that we need to snap the camera on the next LateUpdate.

            // Recalculate and apply the correct FOV for the new mode and current orientation.
            float adjustedFOV = CalculateCorrectFOV(GetCurrentModeFOV());
            SetFOV(_currentCameraMode, adjustedFOV);

            SetMonitorScale(mode); // Ensure the monitor preview is correctly oriented.
            
            // Tell the LCK service which camera is now active.
            switch (mode)
            {
                case CameraMode.Selfie:
                    SetActiveLckCamera(_selfieCamera.CameraId);
                    break;
                case CameraMode.FirstPerson:
                    SetActiveLckCamera(_firstPersonCamera.CameraId);
                    break;
                case CameraMode.ThirdPerson:
                    SetActiveLckCamera(_thirdPersonCamera.CameraId);
                    break;
            }

            // Fire event to notify other systems (like the tablet follow script) of the mode change.
            OnCameraModeChanged?.Invoke(_currentCameraMode);
        }

        /// <summary>
        /// Communicates the active camera's ID to the LCK service, which then uses that camera's output for recording/streaming.
        /// </summary>
        private void SetActiveLckCamera(string cameraId)
        {
            if (_lckService == null)
            {
                LckLog.LogWarning($"LCK: {nameof(SetActiveLckCamera)}(\"{cameraId}\") called before LCK service is " + 
                                  "initialized - Active camera will not be set");
                return;
            }

            var result = _lckService.SetActiveCamera(cameraId);
            if (!result.Success)
            {
                LckLog.LogError($"LCK: Failed to set active camera (id=\"{cameraId}\"): {result.Message}");
            }
        }
    }
}
