using Liv.Lck.Tablet;
using Liv.Lck.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Liv.Lck
{
    /// <summary>
    /// Manages the "follow" behavior for the LCK tablet, making it automatically move to maintain a
    /// consistent position relative to the given target. This is primarily used in Selfie mode
    /// to keep the tablet accessible without it being rigidly attached to the user.
    /// 
    /// The component uses a Rigidbody for smooth, physics-based movement and provides user-configurable
    /// options for follow distance and smoothing via UI elements.
    /// </summary>
    public class LckTabletFollow : MonoBehaviour
    {
        [Header("Settings")]
        
        [Tooltip("An offset applied to the HMD's position to estimate the player's head/neck position. A small downward offset is typical.")]
        [SerializeField]
        private float _heightOffsetForPlayerHead;
        
        [Tooltip("The minimum smoothing value, preventing the tablet from becoming too rigid even at the lowest user setting.")]
        [SerializeField]
        private float _minFollowSmoothing = 0.2f;
        
        [Tooltip("A multiplier applied to the value from the follow distance UI button to determine the actual follow distance in world units.")]
        [SerializeField]
        private float _minFollowDistanceMultiplier = 0.75f;

        [Header("References")]
        [Tooltip("A reference to the main camera controller to get access to the HMD transform.")]
        [SerializeField]
        private LCKCameraController _controller;
        
        [Tooltip("The UI toggle that enables or disables the follow behavior.")]
        [SerializeField]
        private Toggle _isFollowingToggle;
        
        [Tooltip("A reference to the transform of the virtual selfie camera. The tablet will orient itself based on this camera's position.")]
        [SerializeField]
        private Transform _selfieCamera;        
        
        [Tooltip("An optional, specific transform for the tablet to follow. If this is not set, it will default to following the user's HMD (player head).")]
        [SerializeField]
        private Transform _followTarget;
        
        [Tooltip("The UI button used to adjust the follow smoothing.")]
        [SerializeField]
        private LckDoubleButton _smoothingDoubleButton;
        
        [Tooltip("The UI button used to adjust the minimum follow distance.")]
        [SerializeField]
        private LckDoubleButton _followDistanceDoubleButton;

        [Tooltip("The root Rigidbody of the tablet. All movement is applied to this component.")]
        [SerializeField]
        private Rigidbody _rigidbodyRoot;

        private bool _isInCorrectCameraMode = true;
        private bool _isFollowToggleOn;
        private bool _held;
        
        private Vector3 _followVelocity;
        private Vector3 _targetPosition;
        
        private float _minFollowDistance;
        private float _followSmoothing;

        private RigidbodyInterpolation _defaultInterpolation;
        
        #region UNITY METHODS

        private void OnEnable()
        {
            _isFollowToggleOn = _isFollowingToggle.isOn;
            _isFollowingToggle.onValueChanged.AddListener(OnIsFollowToggled);
            _followDistanceDoubleButton.OnValueChanged += OnFollowDistanceChanged;
            _smoothingDoubleButton.OnValueChanged += OnSmoothingChanged;
            _controller.OnCameraModeChanged += OnCameraModeChanged;
        }      

        private void OnDisable()
        {
            _isFollowingToggle.onValueChanged.RemoveListener(OnIsFollowToggled);
            _followDistanceDoubleButton.OnValueChanged -= OnFollowDistanceChanged;
            _smoothingDoubleButton.OnValueChanged -= OnSmoothingChanged;
            _controller.OnCameraModeChanged -= OnCameraModeChanged;
        }


        private void Start()
        {
            SetInitialValuesFromDoubleButtons();
            _isInCorrectCameraMode = true; 
            _targetPosition = transform.position;
            
            if (_rigidbodyRoot != null)
                _defaultInterpolation = _rigidbodyRoot.interpolation;
        }    
        
        private void SetInitialValuesFromDoubleButtons()
        {
            _minFollowDistance = _followDistanceDoubleButton.Value * _minFollowDistanceMultiplier;
            _followSmoothing = CalculateFollowSmoothing(_smoothingDoubleButton.Value);
        }

        private void FixedUpdate()
        {
            ProcessTabletFollowingWithRigidbody();
        }

        #endregion
        
        #region PUBLIC METHODS

        /// <summary>
        /// Allows another script to dynamically set the transform that the tablet should follow.
        /// </summary>
        /// <param name="target">The transform to follow.</param>
        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        /// <summary>While held, follow does not move the tablet. Releasing clears the leftover follow velocity.</summary>
        public void SetHeld(bool held)
        {
            _held = held;
            if (!held)
                _followVelocity = Vector3.zero;
        }
        
        #endregion
        
        #region PRIVATE METHODS

        private void ProcessTabletFollowingWithRigidbody()
        {
            // Do nothing if not in Selfie mode or if the follow toggle is off.
            if (_held || !_isInCorrectCameraMode || !_isFollowToggleOn) return;

            // Determine the position to follow. Use the HMD position with an offset by default, or the specified _followTarget.
            var headPos = !_followTarget ? _controller.HmdTransform.position + Vector3.down * _heightOffsetForPlayerHead : _followTarget.position;

            var tabletPos = _rigidbodyRoot.position;
            var dirFromHeadToTablet = tabletPos - headPos;

            // Check if the tablet is already closer than the minimum desired distance.
            var isClose = dirFromHeadToTablet.magnitude < _minFollowDistance;

            // Calculate the ideal target position: a point along the vector from the head to the tablet, at the minimum distance.
            _targetPosition = headPos + dirFromHeadToTablet.normalized * _minFollowDistance;

            // Calculate the new position using SmoothDamp. If the tablet is too close, we pass its own position as the target
            // to prevent it from moving, effectively creating a "dead zone".
            var smoothTargetPos = Vector3.SmoothDamp(
                current: tabletPos,
                target: isClose ? tabletPos : _targetPosition,
                currentVelocity: ref _followVelocity,
                smoothTime: _followSmoothing);
            
            // Apply the calculated position to the Rigidbody.
            _rigidbodyRoot.MovePosition(smoothTargetPos);

            // Make the tablet look towards the user's head.
            var selfieCameraPosition = _selfieCamera.transform.position;
            _rigidbodyRoot.LookAtFromPivotPoint(
                selfieCameraPosition,
                headPos - selfieCameraPosition,
                smoothTargetPos,
                _rigidbodyRoot.rotation);
        }

        private void OnCameraModeChanged(CameraMode mode)
        {
            _isInCorrectCameraMode = (mode == CameraMode.Selfie);
        }

        private void OnIsFollowToggled(bool value)
        {
            _isFollowToggleOn = value;
            
            // Use Rigidbody interpolation when following for smoother visuals.
            // When not following, revert to the default setting to avoid potential physics conflicts.
            if(_rigidbodyRoot != null)
                _rigidbodyRoot.interpolation = _isFollowToggleOn ? RigidbodyInterpolation.Interpolate : _defaultInterpolation;
        }

        private void OnSmoothingChanged(float value)
        {
            _followSmoothing = CalculateFollowSmoothing(value);
        }

        private float CalculateFollowSmoothing(float value)
        {
            // The value is scaled down and clamped to a minimum to ensure good behavior.
            float calculatedSmoothing = value / 10f;
            return Mathf.Max(calculatedSmoothing, _minFollowSmoothing);
        }

        private void OnFollowDistanceChanged(float value)
        {
            _minFollowDistance = value * _minFollowDistanceMultiplier;
        }
        #endregion
    }
}
