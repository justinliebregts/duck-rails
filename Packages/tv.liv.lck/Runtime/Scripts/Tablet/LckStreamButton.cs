using Liv.Lck.DependencyInjection;
using Liv.Lck.Settings;
using Liv.Lck.Streaming;
using Liv.Lck.UI;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Controls the functionality and visual aspects of the Stream button, reacting
    /// to both external state change and user input.
    /// </summary>
    public class LckStreamButton : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [InjectLck]
        private ILckService _lckService;

        [Header("References")]
        [SerializeField]
        private LckStreamingController _streamingController;
        [SerializeField]
        private LckDiscreetAudioController _audioController;
        [SerializeField]
        private TMP_Text _streamButtonText;
        [SerializeField]
        private Renderer _renderer;
        [SerializeField]
        private RectTransform _visuals;

        [Header("Settings")]
        [SerializeField]
        private LckButtonColors _defaultColors;

        [SerializeField]
        private LckButtonColors _streamingColors;

        [SerializeField]
        private Vector3 _buttonPressedPosition = new Vector3(0, 0, 40f);      

        private bool _collided;
        private GameObject _clickedObject;

        private enum State
        {
            Idle,
            WaitingForStreamingStart,
            Streaming,
            DoingStoppingAnimation,
            StoppingAnimationCompleted,
            WaitUntilTriggerExitOrDelay,
            Error,
        }

        private State _state = State.Idle;

        private void Start()
        {
            if (_lckService != null)
            {
                _lckService.OnStreamingStarted += OnStreamingStarted;
                _lckService.OnStreamingStopped += OnStreamingStopped;
            }

            ValidateMeshColors();
        }

        private void Update()
        {
            if (_state == State.Streaming && _lckService != null)
            {
                UpdateStreamDurationText();
            }
        }

        private void UpdateStreamDurationText()
        {
            _streamButtonText.text = "00:00";

            var getRecordingDuration = _lckService.GetStreamDuration();
            if (!getRecordingDuration.Success)
            {
                return;
            }

            var span = getRecordingDuration.Result;

            int hours = Mathf.FloorToInt(span.Hours);
            int minutes = Mathf.FloorToInt(span.Minutes);
            int seconds = Mathf.FloorToInt(span.Seconds);

            _streamButtonText.text =
                hours == 0 ? $"{minutes:00}:{seconds:00}" : $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        [ContextMenu("test error")]
        public void OnError()
        {
            _state = State.Error;
            _streamButtonText.text = "ERROR";
            ValidateMeshColors();
            
            _ = ResetAfterError();
        }

        private async Task ResetAfterError()
        {
            await Task.Delay(2000);

            if (_lckService != null)
            {
                if (_lckService.IsStreaming().Result)
                {
                    _state = State.Streaming;
                    ValidateMeshColors();
                }
                else
                {
                    _state = State.Idle;
                    ValidateMeshColors();
                    _streamButtonText.text = "GO LIVE";
                }
            }
            else
            {
                _state = State.Idle;
                ValidateMeshColors();
                _streamButtonText.text = "GO LIVE";
            }          
        }

        private void OnStreamingStarted(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            SetStoppingAnimationValue(1);

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.StreamingStarted);
            _state = State.Streaming;
        }
        
        private void OnStreamingStopped(LckResult result)
        {
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.StreamingStopped);

            // Only go to error state on actual failures, not expected stops (e.g., app lifecycle)
            if (!result.Success)
            {
                SetStoppingAnimationValue(0);
                _streamingController.GoToErrorState();
                OnError();
                return;
            }

            // Streaming stopped successfully - reset button to idle state
            // This handles both user-initiated stops and lifecycle stops (e.g., app pause)
            SetStoppingAnimationValue(0);

            if (_state == State.StoppingAnimationCompleted)
            {
                // User pressed the stop button - wait for trigger exit
                _state = State.WaitUntilTriggerExitOrDelay;
                _ = WaitForTriggerExitOrDelay();
            }
            else
            {
                // Streaming stopped externally (lifecycle, etc.) - just reset to idle
                _state = State.Idle;
            }

            ValidateMeshColors();
            _streamButtonText.text = "GO LIVE";
        }

        private async Task WaitForTriggerExitOrDelay()
        {
            await Task.Delay(3000);

            if (_state == State.WaitUntilTriggerExitOrDelay)
            {
                _state = State.Idle;
                ValidateMeshColors();
                _streamButtonText.text = "GO LIVE";
            }
        }

        private void OnDestroy()
        {
            if (_lckService != null)
            {
                _lckService.OnStreamingStarted -= OnStreamingStarted;
                _lckService.OnStreamingStopped -= OnStreamingStopped;
            }
        }

        #region LckToggle Logic

        #region Using ray and poke
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_state == State.Error) return;

            ValidateMeshColors(isHovering: true);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.HoverSound);     
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_state == State.Error) return;

            if (_state == State.Streaming)
            {
                StopAllCoroutines();
                SetStoppingAnimationValue(1);
                StartCoroutine(StoppingAnimationVisual());
            }     

            ValidateMeshColors(isPressed: true);

            _visuals.anchoredPosition3D = _buttonPressedPosition;
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);

            if (eventData != null)
                _clickedObject = eventData.pointerEnter;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_state == State.Error) return;

            if (_state == State.Idle)
            {
                _state = State.WaitingForStreamingStart;
                _streamButtonText.text = "STARTING...";
                _streamingController.StartStreaming();
            }
            else if (_state == State.DoingStoppingAnimation)
            {
                StopAllCoroutines();
                SetStoppingAnimationValue(1);
                _state = State.Streaming;
            }
            else if (_state == State.WaitUntilTriggerExitOrDelay)
            {
                _state = State.Idle;
                ValidateMeshColors();
                _streamButtonText.text = "GO LIVE";
            }
            

            _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);

            if (eventData != null)
            {
                // If you release away from the button on the way up, visuals should update isHovering false
                if (_clickedObject != eventData.pointerEnter)
                {
                    ValidateMeshColors();
                    return;
                }
            }

            ValidateMeshColors(isHovering: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_state == State.Error) return;

            // need to reset position when using poke and releasing away from button
            _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);

            // just in case still holding trigger down but moved pointer away from button
            if (_state == State.WaitUntilTriggerExitOrDelay)
            {
                _state = State.Idle;
                ValidateMeshColors();
                _streamButtonText.text = "GO LIVE";
            }

            ValidateMeshColors();
        }
        #endregion

        #region Using colliders
        private void OnTriggerEnter(Collider other)
        {

            if (other.gameObject.CompareTag(LckSettings.Instance.TriggerEnterTag)
                && IsValidTap(other.ClosestPoint(transform.position))
                && LCKCameraController.ColliderButtonsInUse == false)
            {
                LCKCameraController.ColliderButtonsInUse = true;
                _collided = true;

                OnPointerDown(null);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_collided == true)
            {
                OnPointerUp(null);
                OnPointerExit(null);
                _collided = false;
                LCKCameraController.ColliderButtonsInUse = false;
            }
        }

        private bool IsValidTap(Vector3 tapPosition)
        {
            Vector3 direction = tapPosition - transform.position;
            float angle = Vector3.Angle(-transform.forward, direction);
            return angle < 90;
        }
        #endregion

        private void OnApplicationFocus(bool focus)
        {
            if (focus == true)
            {
                _collided = false;

                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);

                // reset the button visuals
                ValidateMeshColors();
            }
        }

        private void ValidateMeshColors(bool isPressed = false, bool isHovering = false)
        {
            if (!_renderer) return;

            if (_state == State.Error)
            {
                SetDefaultColor(_streamingColors.NormalColor);
                SetStreamingColor(_streamingColors.NormalColor);
                return;
            }

            if (isPressed == false)
            {
                if (isHovering == false)
                {
                    SetDefaultColor(_defaultColors.NormalColor);
                    SetStreamingColor(_streamingColors.NormalColor);
                }
                else if (isHovering == true)
                {
                    SetDefaultColor(_defaultColors.HighlightedColor);
                    SetStreamingColor(_streamingColors.HighlightedColor);
                }
            }
            else if (isPressed == true)
            {
                SetDefaultColor(_defaultColors.PressedColor);
                SetStreamingColor(_streamingColors.PressedColor);
            }
        }

        private void SetStoppingAnimationValue(float value)
        {
            float clampedValue = Mathf.Clamp01(value);

            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.SetFloat("_ProgressValue", clampedValue);
            }
        }

        private void SetDefaultColor(Color color)
        {
            _renderer.material.SetColor("_DefaultColor", color);
        }

        private void SetStreamingColor(Color color)
        {
            _renderer.material.SetColor("_StreamingColor", color);
        }

        private IEnumerator StoppingAnimationVisual()
        {
            float startTime = Time.time;
            float currentProgress = 1f;
            float stoppingDuration = 2f;

            _state = State.DoingStoppingAnimation;
            _streamButtonText.text = "STOPPING...";

            while (currentProgress > 0f)
            {
                float elapsedTime = Time.time - startTime;

                currentProgress = Mathf.Lerp(1f, 0f, elapsedTime / stoppingDuration);

                if (_renderer != null)
                {
                    _renderer.material.SetFloat("_ProgressValue", currentProgress);
                }

                yield return null;
            }

            if (_renderer != null)
            {
                _renderer.material.SetFloat("_ProgressValue", 0f);
            }

            _state = State.StoppingAnimationCompleted;
            _streamingController.StopStreaming();
            ValidateMeshColors();
            _streamButtonText.text = "GO LIVE";
            _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
        }
        #endregion
    }
}
