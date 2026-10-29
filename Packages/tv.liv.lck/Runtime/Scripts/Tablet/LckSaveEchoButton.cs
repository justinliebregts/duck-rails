using System;
using System.Threading.Tasks;
using Liv.Lck.DependencyInjection;
using Liv.Lck.Recorder;
using Liv.Lck.UI;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Updates the save echo button label to reflect the current echo buffer duration.
    /// Shows "ECHO STARTING..." (disabled) until the buffer has at least 1 second,
    /// then "SAVE LAST N SECONDS" up to the native maximum.
    /// Resets to "ECHO STARTING..." when echo is not enabled.
    /// Place this component on the same GameObject as the save echo <see cref="LckButton"/>.
    /// </summary>
    public class LckSaveEchoButton : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;

        [SerializeField]
        private LckButton _button;

        [SerializeField]
        private LckDiscreetAudioController _audioController;

        private const string _echoStartingString = "ECHO STARTING...";
        private const string _lowStorageString = "LOW STORAGE";
        private const string _errorString = "ERROR";
        private const int EchoStartingPeriodSeconds = 2;

        private int _lastDisplayedSeconds = -1;
        private int _maxBufferSeconds;
        private bool _shouldPollEchoDuration;

        private enum State
        {
            EchoStarting,
            Ready,
            LowStorage,
            Error
        }

        private State _state = State.EchoStarting;

        private void OnEchoEnabled(LckResult result)
        {
            if (!result.Success) return;
            StartEchoPolling();
        }

        private void StartEchoPolling()
        {
            if (_state == State.Error) return;

            var maxBuffer = _lckService.GetEchoMaxBufferDuration();
            _maxBufferSeconds = maxBuffer.Success ? (int)maxBuffer.Result.TotalSeconds : 0;
            _state = State.EchoStarting;
            _lastDisplayedSeconds = -1;
            UpdateVisualState();
            _shouldPollEchoDuration = true;
        }

        private void OnEchoDisabled(LckResult result, EchoDisableReason reason)
        {
            switch (reason)
            {
                case EchoDisableReason.LowStorage:
                    _shouldPollEchoDuration = false;
                    _state = State.LowStorage;
                    UpdateVisualState();
                    return;
                case EchoDisableReason.Error:
                    OnError();
                    return;
            }

            if (_state == State.Error) return;
            if (_state == State.LowStorage) return;

            _shouldPollEchoDuration = false;
            _state = State.EchoStarting;
            _lastDisplayedSeconds = -1;
            UpdateVisualState();
        }

        private void OnError()
        {
            _shouldPollEchoDuration = false;
            _state = State.Error;
            UpdateVisualState();

            _ = ResetAfterError();
        }

        private async Task ResetAfterError()
        {
            await Task.Delay(2000);

            _lastDisplayedSeconds = -1;

            if (_lckService == null)
            {
                _state = State.EchoStarting;
                UpdateVisualState();
                return;
            }

            // Show "ECHO STARTING..." then re-enable echo so the buffer fills from scratch
            _state = State.EchoStarting;
            UpdateVisualState();

            var result = await _lckService.SetEchoEnabledAsync(true);
            if (!result.Success)
            {
                OnError();
            }
        }

        private void UpdateVisualState()
        {
            if (_button == null) return;

            switch (_state)
            {
                case State.EchoStarting:
                    _button.SetLabelText(_echoStartingString);
                    _button.SetIsDisabled(true);
                    break;
                case State.Ready:
                    // Label text is set by UpdateBufferDurationText
                    _button.SetIsDisabled(false);
                    break;
                case State.LowStorage:
                    _button.SetLabelText(_lowStorageString);
                    _button.SetIsDisabled(true);
                    break;
                case State.Error:
                    _button.SetLabelText(_errorString);
                    _button.SetIsDisabled(true);
                    break;
            }
        }

        private void UpdateBufferDurationText()
        {
            var bufferDuration = _lckService.GetEchoBufferDuration();
            if (!bufferDuration.Success) return;

            int seconds = Math.Min((int)bufferDuration.Result.TotalSeconds, _maxBufferSeconds);

            if (seconds <= _lastDisplayedSeconds)
            {
                // Only update when seconds increases — the buffer only grows,
                // so ignore any jitter that would cause the value to momentarily drop.
                return;
            }

            _lastDisplayedSeconds = seconds;
            if (seconds < EchoStartingPeriodSeconds)
            {
                _state = State.EchoStarting;
                UpdateVisualState();
            }
            else
            {
                _state = State.Ready;
                UpdateVisualState();
                _button.SetLabelText($"SAVE LAST\n{seconds} SECONDS");
            }

            // Stop polling after reaching max buffer duration since it won't change anymore.
            if (seconds >= _maxBufferSeconds)
                _shouldPollEchoDuration = false;
        }

        private void OnEchoSaved(LckResult<RecordingData> result)
        {
            if (!result.Success) return;

            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.RecordingSaved);
        }

        private void EnsureLckService()
        {
            if (_lckService == null)
            {
                LckLog.LogWarning($"LCK Could not get Service");
            }
        }

        private void Start()
        {
            EnsureLckService();

            if (_lckService != null)
            {
                _lckService.OnEchoEnabled += OnEchoEnabled;
                _lckService.OnEchoDisabled += OnEchoDisabled;
                _lckService.OnEchoSaved += OnEchoSaved;

                // Echo may have been enabled between OnEnable and Start (before
                // the subscription above existed), so catch up with current state.
                var echoEnabled = _lckService.IsEchoEnabled();
                if (echoEnabled.Success && echoEnabled.Result)
                {
                    StartEchoPolling();
                }
            }
        }

        private void OnEnable()
        {
            if (_lckService != null)
            {
                // Resume polling if echo is active. Update doesn't run while
                // disabled so polling must be restarted here.
                var echoEnabled = _lckService.IsEchoEnabled();
                if (echoEnabled.Success && echoEnabled.Result)
                {
                    var maxBuffer = _lckService.GetEchoMaxBufferDuration();
                    _maxBufferSeconds = maxBuffer.Success ? (int)maxBuffer.Result.TotalSeconds : 0;
                    _lastDisplayedSeconds = -1;
                    _shouldPollEchoDuration = true;
                }
            }

            UpdateVisualState();
        }

        private void OnDisable()
        {
            _shouldPollEchoDuration = false;
        }

        private void OnDestroy()
        {
            if (_lckService != null)
            {
                _lckService.OnEchoEnabled -= OnEchoEnabled;
                _lckService.OnEchoDisabled -= OnEchoDisabled;
                _lckService.OnEchoSaved -= OnEchoSaved;
            }
        }

        private void Update()
        {
            EnsureLckService();

            if (_shouldPollEchoDuration && _lckService != null)
            {
                UpdateBufferDurationText();
            }
        }
    }
}
