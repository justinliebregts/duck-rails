using UnityEngine;
using TMPro;
using Liv.Lck.UI;
using Liv.Lck.Recorder;
using System.Threading.Tasks;
using Liv.Lck.DependencyInjection;
using UnityEngine.UI;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Controls the functionality and visual aspects of the Record button, reacting
    /// to both external state change and user input.
    /// </summary>
    public class LckRecordButton : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;
        [Header("References")]
        [SerializeField]
        private LckDiscreetAudioController _audioController;
        
        [SerializeField]
        private TMP_Text _recordButtonText;
        [SerializeField]
        private LckToggle _recordLckToggle;
        [SerializeField]
        private Toggle _recordToggle;
        [Header("Toggle collider when using Direct Tablet")]
        [SerializeField]
        private BoxCollider _collider;

        private enum State
        {
            Idle,
            Saving,
            Paused,
            Recording,
            Error,
        }

        private State _state = State.Idle;

        private void Start()
        {
            EnsureLckService();

            if (_lckService != null)
            {
                _lckService.OnRecordingStarted += OnRecordingStarted;
                _lckService.OnRecordingStopped += OnRecordingStopped;
                _lckService.OnRecordingPaused += OnRecordingPaused;
                _lckService.OnRecordingResumed += OnRecordingResumed;
                _lckService.OnRecordingSaved += OnRecordingSaved;
            }
        }

        private void Update()
        {
            EnsureLckService();

            if (_state == State.Recording && _lckService != null)
            {
                UpdateRecordDurationText();
            }
        }

        private void UpdateRecordDurationText()
        {
            var getRecordingDuration = _lckService.GetRecordingDuration();
            if (!getRecordingDuration.Success)
            {
                return;
            }

            var span = getRecordingDuration.Result;

            int hours = Mathf.FloorToInt(span.Hours);
            int minutes = Mathf.FloorToInt(span.Minutes);
            int seconds = Mathf.FloorToInt(span.Seconds);

            _recordButtonText.text =
                hours == 0 ? $"{minutes:00}:{seconds:00}" : $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private void OnError()
        {
            _state = State.Error;
            _recordButtonText.text = "ERROR";
            _recordLckToggle.enabled = false;
            _recordToggle.interactable = false;
            if (_collider)
            {
                _collider.enabled = false;
            }

            _ = ResetAfterError();
        }

        private async Task ResetAfterError()
        {
            await Task.Delay(2000);
            _state = State.Idle;
            if (_collider)
            {
                _collider.enabled = true;
            }

            ResetButtonVisuals();
        }

        private void OnRecordingStarted(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.RecordingStart);
            _state = State.Recording;
        }

        private void OnRecordingPaused(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Paused;

            if (_recordButtonText == null || _recordLckToggle == null)
                return;
            
            _recordButtonText.text = "PAUSED";
        }

        private void OnRecordingResumed(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Recording;
        }

        private void OnRecordingStopped(LckResult result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Saving;

            if (_recordButtonText == null || _recordLckToggle == null)
                return;
            
            _recordButtonText.text = "SAVING";
            _recordLckToggle.SetToggleVisualsOff();
            _recordLckToggle.enabled = false;
            _recordToggle.interactable = false;
        }

        private void OnRecordingSaved(LckResult<RecordingData> result)
        {
            if (!result.Success)
            {
                OnError();
                return;
            }

            _state = State.Idle;
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.RecordingSaved);

            if (_recordButtonText == null || _recordLckToggle == null)
                return;

            ResetButtonVisuals();
        }

        private void ResetButtonVisuals()
        {
            _recordButtonText.text = "RECORD";
            _recordLckToggle.enabled = true;
            _recordToggle.interactable = true;
            _recordLckToggle.SetToggleVisualsOff();
        }

        private void EnsureLckService()
        {
            if (_lckService == null)
            {
                LckLog.LogWarning($"LCK Could not get Service");
            }
        }

        private void OnDestroy()
        {
            if (_lckService != null)
            {
                _lckService.OnRecordingStarted -= OnRecordingStarted;
                _lckService.OnRecordingStopped -= OnRecordingStopped;
                _lckService.OnRecordingPaused -= OnRecordingPaused;
                _lckService.OnRecordingResumed -= OnRecordingResumed;
                _lckService.OnRecordingSaved -= OnRecordingSaved;
            }
        }
    }
}
