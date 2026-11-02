using System.Collections;
using System.Diagnostics;
using Liv.Lck.Encoding;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    internal class LckVideoCapturer : ILckVideoCapturer
    {
        private readonly ILckVideoTextureProvider _videoTextureProvider;
        private readonly ILckActiveCameraConfigurer _activeCameraConfigurer;
        private readonly ILckPreviewer _previewer;
        private readonly ILckEncoder _encoder;
        private readonly ILckEventBus _eventBus;
        private readonly Stopwatch _captureStopwatch = new Stopwatch();
        
        private bool _frameHasBeenRendered;
        private double _captureTimeOverflow;
        private double _targetSecondsPerCapture;
        
        private const string CaptureLoopCoroutineName = "LckCaptureLooper:CaptureLoopCoroutine";

        [Preserve]
        public LckVideoCapturer(
            ILckVideoTextureProvider videoTextureProvider, 
            ILckActiveCameraConfigurer activeCameraConfigurer, 
            ILckPreviewer previewer,
            ILckEncoder encoder,
            ILckOutputConfigurer outputConfigurer,
            ILckEventBus eventBus)
        {
            _videoTextureProvider = videoTextureProvider;
            _activeCameraConfigurer = activeCameraConfigurer;
            _previewer = previewer;
            _encoder = encoder;
            _eventBus = eventBus;
            
            var targetCaptureFramerate = outputConfigurer.GetActiveCameraTrackDescriptor().Result.Framerate;
            SetTargetCaptureFramerate(targetCaptureFramerate);
            
            _eventBus.AddListener<CameraFramerateChangedEvent>(OnCameraFramerateChanged);
        }

        private void OnCameraFramerateChanged(CameraFramerateChangedEvent cameraFramerateChangedEvent)
        {
            var eventResult = cameraFramerateChangedEvent.Result;
            if (eventResult.Success)
            {
                SetTargetCaptureFramerate(eventResult.Result);
            }
        }

        public bool ForceCaptureAllFrames { get; set; } = false;

        public bool IsCapturing { get; private set; }

        public void StartCapturing()
        {
            IsCapturing = true;
            
            LckMonoBehaviourMediator.StartCoroutine(CaptureLoopCoroutineName, 
                CaptureLoopCoroutine());
        }

        public void StopCapturing()
        {
            IsCapturing = false;
            LckMonoBehaviourMediator.StopCoroutineByName(CaptureLoopCoroutineName);
        }

        public bool HasCurrentFrameBeenCaptured()
        {
            return _frameHasBeenRendered;
        }
        
        private void SetTargetCaptureFramerate(uint targetCaptureFramerate)
        {
            _targetSecondsPerCapture = 1.0 / targetCaptureFramerate;
        }

        private IEnumerator CaptureLoopCoroutine()
        {
            _captureStopwatch.Start();
            _captureTimeOverflow = 0;
            
            while (IsCapturing)
            {
                HandleCameraFrame();
                yield return null;
            }
        }

        private void PrepareCameraForCapture(ILckCamera camera)
        {
            if (CaptureCanBeCulled())
            {
                _frameHasBeenRendered = false;
                camera.DeactivateCamera();
            }
            else
            {
                _frameHasBeenRendered = true;
                camera.ActivateCamera(_videoTextureProvider.CameraTrackTexture);
            }
        }
        
        private void HandleCameraFrame(ILckCamera activeCamera)
        {
            var secondsSinceLastCapture = _captureStopwatch.Elapsed.TotalSeconds;
            var targetCaptureTimeElapsed = secondsSinceLastCapture + _captureTimeOverflow >= _targetSecondsPerCapture;
            if (ForceCaptureAllFrames || targetCaptureTimeElapsed)
            {
                var captureTimeDiffFromTarget = secondsSinceLastCapture - _targetSecondsPerCapture;
                _captureTimeOverflow = (_captureTimeOverflow + captureTimeDiffFromTarget) % _targetSecondsPerCapture;
                _captureStopwatch.Restart();
                
                PrepareCameraForCapture(activeCamera);
            }
            else
            {
                _frameHasBeenRendered = false;
                activeCamera.DeactivateCamera();
            }
        }
        
        private void HandleCameraFrame()
        {
            var getActiveCameraResult = _activeCameraConfigurer.GetActiveCamera();
            if (!getActiveCameraResult.Success)
                return;
            
            var activeCamera = getActiveCameraResult.Result;
            if (activeCamera == null)
                return;
            
            HandleCameraFrame(activeCamera);
        }
        
        private bool CaptureCanBeCulled()
        {
            if (_encoder.IsActive())
                return false;

            if (_previewer.IsPreviewActive)
                return false;
            
            return true;
        }
        
        public void Dispose()
        {
            if (IsCapturing)
                StopCapturing();
            
            _eventBus.RemoveListener<CameraFramerateChangedEvent>(OnCameraFramerateChanged);
        }
    }
}
