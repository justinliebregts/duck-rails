using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck.ErrorHandling
{
    /// <summary>
    /// <see cref="ILckCaptureErrorDispatcher"/> which uses a <see cref="ConcurrentQueue{T}"/> to store
    /// <see cref="LckCaptureError"/>s received on any thead and then dispatch corresponding
    /// <see cref="LckEvents.CaptureErrorEvent"/>s on the main thread.
    /// </summary>
    internal class MainThreadCaptureErrorDispatcher : ILckCaptureErrorDispatcher
    {
        private static readonly string _updateCoroutineName = 
            $"{nameof(MainThreadCaptureErrorDispatcher)}:{nameof(Update)}";
        
        private readonly ILckEventBus _eventBus;
        private readonly ConcurrentQueue<LckCaptureError> _errorQueue = new ConcurrentQueue<LckCaptureError>();

        private bool _isMonitoringErrors;
        
        [Preserve]
        public MainThreadCaptureErrorDispatcher(ILckEventBus eventBus)
        {
            _eventBus = eventBus;
            
            _eventBus.AddListener<EncoderStartedEvent>(OnEncoderStarted);
            _eventBus.AddListener<EncoderStoppedEvent>(OnEncoderStopped);
        }
        
        public void PushError(LckCaptureError error)
        {
            // Log warning rather than error at this stage since severity is not understood.
            // Specific error event handlers can react accordingly / do further logging based on context if needed.
            LckLog.LogWarning($"Capture error occurred: {error.Message}");
            _errorQueue.Enqueue(error);
        }
        
        private void OnEncoderStarted(EncoderStartedEvent encoderStartedEvent)
        {
            if (encoderStartedEvent.Result.Success)
                StartMonitoringErrors();
        }
        
        private void OnEncoderStopped(EncoderStoppedEvent encoderStoppedEvent)
        {
            StopMonitoringErrors();
        }

        private void StartMonitoringErrors()
        {
            LckLog.LogTrace("Starting capture error monitoring");
            _isMonitoringErrors = true;
            LckMonoBehaviourMediator.StartCoroutine(_updateCoroutineName , Update());
        }

        private void StopMonitoringErrors()
        {
            LckLog.LogTrace("Stopping capture error monitoring");
            _isMonitoringErrors = false;
            LckMonoBehaviourMediator.StopCoroutineByName(_updateCoroutineName);
        }
        
        private IEnumerable<LckCaptureError> DrainErrors()
        {
            while (_errorQueue.TryDequeue(out var error))
            {
                yield return error;
            }
        }
        
        private IEnumerator Update()
        {
            while (_isMonitoringErrors)
            {
                foreach (var error in DrainErrors())
                {
                    _eventBus.Trigger(new CaptureErrorEvent(error));
                }
                
                yield return null;
            }
        }

        public void Dispose()
        {
            _eventBus.RemoveListener<EncoderStartedEvent>(OnEncoderStarted);
            _eventBus.RemoveListener<EncoderStoppedEvent>(OnEncoderStopped);
            
            if (_isMonitoringErrors)
                LckMonoBehaviourMediator.StopCoroutineByName(_updateCoroutineName);
        }
    }
}
