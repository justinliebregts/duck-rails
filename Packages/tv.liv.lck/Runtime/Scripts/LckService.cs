using System;
using Liv.Lck.Core;
using Liv.Lck.DependencyInjection;
using Liv.Lck.Encoding;
using Liv.NGFX;
using Liv.Lck.Recorder;
using Liv.Lck.Settings;
using Liv.Lck.Streaming;
using Liv.Lck.Telemetry;
using Liv.NativeAudioBridge;
using UnityEngine;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    /// <summary>
    /// Describes the configuration for the LCK service.
    /// </summary>
    public class LckDescriptor
    {
        /// <summary>
        /// The descriptor for the active camera track.
        /// </summary>
        public CameraTrackDescriptor cameraTrackDescriptor;
    }
    
    /// <summary>
    /// The primary public API implementation for the LCK in-game capturing system.
    /// This service provides all necessary functionality for recording, streaming, and capturing in-game content.
    /// </summary>
    [Preserve]
    public class LckService : ILckService
    {
        private readonly ILckOutputConfigurer _outputConfigurer;
        private ILckEncodeLooper _encodeLooper;
        private INativeAudioPlayer _nativeAudioPlayer;
        private ILckEncoder _encoder;

        private bool _disposed = false;
        private ILckRecorder _recorder;
        private ILckStreamer _streamer;
        private ILckPhotoCapture _photoCapture;
        private ILckStorageWatcher _storageWatcher;
        private ILckVideoMixer _videoMixer;
        private ILckAudioMixer _audioMixer;
        private ILckPreviewer _previewer;
        private ILckEventBus _eventBus;
        private ILckVideoCapturer _videoCapturer;
        private readonly ILckTelemetryClient _telemetryClient;
        private readonly LckPublicApiEventBridge _eventBridge;
        private readonly LckEventErrorLogger _eventErrorLogger;
        
        public event Action<LckResult> OnRecordingStarted;
        public event Action<LckResult> OnRecordingStopped;
        public event Action<LckResult> OnRecordingPaused;
        public event Action<LckResult> OnRecordingResumed;
        public event Action<LckResult> OnStreamingStarted;
        public event Action<LckResult> OnStreamingStopped;
        public event Action<LckResult> OnLowStorageSpace;
        public event Action<LckResult<RecordingData>> OnRecordingSaved;
        public event Action<LckResult> OnPhotoSaved;
        public event Action<LckResult<ILckCamera>> OnActiveCameraSet;
                
        /// <summary>
        /// Specifies the reason for a recording or streaming session stopping.
        /// </summary>
        public enum StopReason
        {
            /// <summary>The user manually stopped the session.</summary>
            UserStopped,
            /// <summary>The session was stopped automatically due to low storage space.</summary>
            LowStorageSpace,
            /// <summary>The session was stopped due to an unexpected error.</summary>
            Error,
            /// <summary>The session was stopped due to an application lifecycle event (e.g., app closing).</summary>
            ApplicationLifecycle
        }

        [Preserve]
        internal LckService(
            ILckEncoder encoder, 
            ILckRecorder recorder, 
            ILckStreamer streamer, 
            ILckEncodeLooper encodeLooper, 
            ILckPhotoCapture photoCapture, 
            ILckStorageWatcher storageWatcher, 
            ILckVideoCapturer videoCapturer, 
            ILckVideoMixer videoMixer, 
            ILckAudioMixer audioMixer, 
            ILckOutputConfigurer outputConfigurer, 
            ILckPreviewer previewer, 
            INativeAudioPlayer nativeAudioPlayer, 
            ILckEventBus eventBus,
            ILckTelemetryClient telemetryClient)
        {
            _encodeLooper = encodeLooper;
            _nativeAudioPlayer = nativeAudioPlayer;
            _encoder = encoder;
            _recorder = recorder;
            _streamer = streamer;
            _photoCapture = photoCapture;
            _storageWatcher = storageWatcher;
            _videoMixer = videoMixer;
            _audioMixer = audioMixer;
            _outputConfigurer = outputConfigurer;
            _previewer = previewer;
            _eventBus = eventBus;
            _telemetryClient = telemetryClient;
            _videoCapturer = videoCapturer;
            
            // Register public Actions with internal event bus for forwarding.
            _eventBridge = new LckPublicApiEventBridge(_eventBus);
            _eventBridge.Forward<RecordingStartedEvent, LckResult>(r => LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() => OnRecordingStarted?.Invoke(r)));
            _eventBridge.Forward<RecordingPausedEvent, LckResult>(r => OnRecordingPaused?.Invoke(r));
            _eventBridge.Forward<RecordingResumedEvent, LckResult>(r => OnRecordingResumed?.Invoke(r));
            _eventBridge.Forward<RecordingStoppedEvent, LckResult>(r => OnRecordingStopped?.Invoke(r));
            _eventBridge.Forward<StreamingStartedEvent, LckResult>(r => LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() => OnStreamingStarted?.Invoke(r)));
            _eventBridge.Forward<StreamingStoppedEvent, LckResult>(r => OnStreamingStopped?.Invoke(r));
            _eventBridge.Forward<PhotoCaptureSavedEvent, LckResult>(r => OnPhotoSaved?.Invoke(r));
            _eventBridge.Forward<LowStorageSpaceDetectedEvent, LckResult>(r => OnLowStorageSpace?.Invoke(r));
            _eventBridge.Forward<RecordingSavedEvent, LckResult<RecordingData>>(
                evt => evt.SaveResult, 
                r => OnRecordingSaved?.Invoke(r)
            );            
            _eventBridge.Forward<ActiveCameraChangedEvent, LckResult<ILckCamera>>(
                evt => evt.CameraResult, 
                r => OnActiveCameraSet?.Invoke(r)
            );
            
            // Monitor for errors in event results and log them.
            _eventErrorLogger = new LckEventErrorLogger(_eventBus,
                result => LckLog.LogError($"{result.Error}: {result.Message}"));
            _eventErrorLogger.Monitor<RecordingStartedEvent, LckResult>();
            _eventErrorLogger.Monitor<RecordingPausedEvent, LckResult>();
            _eventErrorLogger.Monitor<RecordingResumedEvent, LckResult>();
            _eventErrorLogger.Monitor<RecordingStoppedEvent, LckResult>();
            _eventErrorLogger.Monitor<StreamingStartedEvent, LckResult>();
            _eventErrorLogger.Monitor<StreamingStoppedEvent, LckResult>();
            _eventErrorLogger.Monitor<PhotoCaptureSavedEvent, LckResult>();
            _eventErrorLogger.Monitor<LowStorageSpaceDetectedEvent, LckResult>();

            var nativeLogLevel = LckSettings.Instance.NativeLogLevel;
            NI.SetGlobalLogLevel(nativeLogLevel, LckSettings.Instance.ShowOpenGLMessages);
            _encoder.SetLogLevel(nativeLogLevel);
            _recorder.SetLogLevel(nativeLogLevel);
            _streamer?.SetLogLevel(nativeLogLevel);
            
            _videoCapturer.StartCapturing();
            
            if (VerifyGraphicsApi() && !Application.isEditor)
                LckLog.Log($"LCK version is v{LckSettings.Version}#{LckSettings.Build}");
        }

        /// <summary>
        /// Gets an instance of the <see cref="LckService"/>.
        /// The LckService can also be injected into MonoBehaviours (recommended).
        /// <example>
        /// To inject the LckService into a MonoBehaviour, use the [InjectLck] attribute.
        /// <code>
        /// [InjectLck] ILckService _lckService;
        ///
        /// private void StartRecording()
        /// {
        ///     _lckService.StartRecording();
        /// }
        /// </code>
        /// </example>
        ///
        /// <example>
        /// To gain access to the LckService from a C# class, use the LckDiContainer directly.
        /// <code>
        /// private void StartRecording()
        /// {
        ///     var service = (LckService)LckDiContainer.Instance.GetService<ILckService>();
        ///     service.StartRecording();
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public static LckResult<LckService> GetService()
        {
            var service = (LckService)LckDiContainer.Instance.GetService<ILckService>();
            
            if(service == null)
            {
                return LckResult<LckService>.NewError(LckError.ServiceNotCreated, "Service not created");
            }
        
            return LckResult<LckService>.NewSuccess(service);
        }

        public LckResult StartRecording()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.StartRecording();
        }

        public LckResult PauseRecording()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.PauseRecording();
        }

        public LckResult ResumeRecording()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.ResumeRecording();
        }

        public LckResult StopRecording()
        {
            return StopRecording(StopReason.UserStopped);
        }
        
        public LckResult StartStreaming()
        {
            if (_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (_streamer == null || _streamer is NullLckStreamer)
            {
                return LckResult.NewError(LckError.StreamerNotImplemented, "LCK streaming package not available");
            }
            
            return _streamer.StartStreaming();
        }

        public LckResult StopStreaming()
        {
            return StopStreaming(StopReason.UserStopped);
        }
        
        public LckResult StopStreaming(StopReason stopReason)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (_streamer == null || _streamer is NullLckStreamer)
            {
                return LckResult.NewError(LckError.StreamerNotImplemented, "LCK streaming package not available");
            }
            
            return _streamer.StopStreaming(stopReason);
        }
        
        public LckResult<TimeSpan> GetRecordingDuration()
        {
            return _recorder.GetRecordingDuration();
        }
        
        public LckResult<TimeSpan> GetStreamDuration()
        {
            return _streamer.GetStreamDuration();
        }

        public LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change resolution while capturing.");
            }
            
            return _outputConfigurer.SetActiveResolution(cameraResolutionDescriptor);
        }

        public LckResult SetCameraOrientation(LckCameraOrientation orientation)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change camera orientation while capturing.");
            }
            
            return _outputConfigurer.SetCameraOrientation(orientation);
        }
        
        public LckResult SetTrackFramerate(uint framerate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change framerate while capturing.");
            }
            
            return _outputConfigurer.SetActiveVideoFramerate(framerate);
        }

        public LckResult SetPreviewActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _previewer.IsPreviewActive = isActive;

            return LckResult.NewSuccess();
        }
        
        public LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change track descriptor while capturing.");
            }
            
            return _outputConfigurer.SetActiveCameraTrackDescriptor(cameraTrackDescriptor);
        }
        
        public LckResult SetTrackDescriptor(LckCaptureType captureType, CameraTrackDescriptor cameraTrackDescriptor)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change track descriptor while capturing.");
            }
            
            return _outputConfigurer.SetCameraTrackDescriptor(captureType, cameraTrackDescriptor);
        }

        public LckResult SetTrackBitrate(uint bitrate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change video bitrate while capturing.");
            }
            
            return _outputConfigurer.SetActiveVideoBitrate(bitrate);
        }

        public LckResult SetTrackAudioBitrate(uint audioBitrate)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            if (IsCapturing().Result)
            {
                return LckResult.NewError(LckError.CantEditSettingsWhileCapturing, 
                    "Can't change audio bitrate while capturing.");
            }
            
            return _outputConfigurer.SetActiveAudioBitrate(audioBitrate);
        }

        public LckResult<bool> IsRecording()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.IsRecording();
        }

        public LckResult<bool> IsStreaming()
        {
            if (_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            if (_streamer == null || _streamer is NullLckStreamer)
            {
                return LckResult<bool>.NewError(LckError.StreamerNotImplemented, "LCK streaming package not available");
            }
            
            return LckResult<bool>.NewSuccess(_streamer.IsStreaming);
        }
        
        public LckResult<bool> IsPaused()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.IsPaused();
        }

        public LckResult<bool> IsCapturing()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            return LckResult<bool>.NewSuccess(_encoder.IsActive());
        }

        public LckResult SetGameAudioCaptureActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _audioMixer.SetGameAudioMute(!isActive);
        }

        public LckResult SetMicrophoneCaptureActive(bool isActive)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _audioMixer.SetMicrophoneCaptureActive(isActive);
        }

        public LckResult<float> GetMicrophoneOutputLevel()
        {
            if(_disposed)
            {
                return LckResult<float>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<float>.NewSuccess(_audioMixer.GetMicrophoneOutputLevel());
        }

        public LckResult SetMicrophoneGain(float gain)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _audioMixer.SetMicrophoneGain(gain);

            return LckResult.NewSuccess();
        }

        public LckResult SetGameAudioGain(float gain)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _audioMixer.SetGameAudioGain(gain);

            return LckResult.NewSuccess();
        }

        public LckResult<float> GetGameOutputLevel()
        {
            if(_disposed)
            {
                return LckResult<float>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return LckResult<float>.NewSuccess(_audioMixer.GetGameOutputLevel());
        }

        public LckResult<bool> IsGameAudioMute()
        {
            if(_disposed)
            {
                return LckResult<bool>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _audioMixer.IsGameAudioMute();
        }

        public LckResult SetActiveCamera(string cameraId, string monitorId = null)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _videoMixer.ActivateCameraById(cameraId, monitorId);
        }
        
        public LckResult<ILckCamera> GetActiveCamera()
        {
            if(_disposed)
            {
                return LckResult<ILckCamera>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _videoMixer.GetActiveCamera();
        }

        public LckResult PreloadDiscreetAudio(AudioClip audioClip, float volume, bool forceReload = false)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.PreloadAudioClip(audioClip, volume, forceReload);
            return LckResult.NewSuccess();  //TODO: Approach to error handling
        }

        public LckResult PlayDiscreetAudioClip(AudioClip audioClip)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.PlayAudioClip(audioClip, 1);
            return LckResult.NewSuccess();
        }

        public LckResult StopAllDiscreetAudio()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            _nativeAudioPlayer?.StopAllAudio();
            return LckResult.NewSuccess();
        }

        public LckResult<LckDescriptor> GetDescriptor()
        {
            if(_disposed)
            {
                return LckResult<LckDescriptor>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            var activeCaptureTypeResult = GetActiveCaptureType();
            if (!activeCaptureTypeResult.Success)
            {
                return LckResult<LckDescriptor>.NewError(LckError.UnknownError, 
                    "Failed to get active capture type");
            }
            
            var captureType = activeCaptureTypeResult.Result;
            var getCameraTrackDescriptorResult = _outputConfigurer.GetCameraTrackDescriptor(captureType);
            if (!getCameraTrackDescriptorResult.Success)
            {
                return LckResult<LckDescriptor>.NewError(LckError.UnknownError,
                    "Failed to get camera track descriptor");
            }
            
            var descriptor = new LckDescriptor
            {
                cameraTrackDescriptor = getCameraTrackDescriptorResult.Result
            };
            return LckResult<LckDescriptor>.NewSuccess(descriptor);
        }
        
        public LckResult CapturePhoto()
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            if (_photoCapture == null)
            {
                return LckResult.NewError(LckError.PhotoCaptureError, 
                    "Failed to Capture Photo, LckPhotoCapture is null");
            }
            
            return _photoCapture.Capture();
        }

        public LckResult<LckCaptureType> GetActiveCaptureType()
        {
            if(_disposed)
            {
                return LckResult<LckCaptureType>.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _outputConfigurer.GetActiveCaptureType();
        }
        
        public LckResult SetActiveCaptureType(LckCaptureType captureType)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }
            
            return _outputConfigurer.SetActiveCaptureType(captureType);
        }
        

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _encodeLooper?.Dispose();
                    _encodeLooper = null;
                    
                    _videoCapturer?.Dispose();
                    _videoCapturer = null;
            
                    _encoder?.Dispose();
                    _encoder = null;

                    _recorder?.Dispose();
                    _recorder = null;
            
                    _streamer?.Dispose();
                    _streamer = null;
            
                    _audioMixer?.Dispose();
                    _audioMixer = null;
            
                    _videoMixer?.Dispose();
                    _videoMixer = null;

                    _storageWatcher?.Dispose();
                    _storageWatcher = null;
                    
                    _nativeAudioPlayer?.Dispose();
                    _nativeAudioPlayer = null;

                    _previewer?.Dispose();
                    _previewer = null;
                    
                    _photoCapture?.Dispose();
                    _photoCapture = null;

                    _eventBridge?.Dispose();
                    
                    LckMonoBehaviourMediator.StopAllActiveCoroutines();
                }
                
                _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.ServiceDisposed));
                LckLog.Log("LCK service disposed.");

                _disposed = true;
            }
        }

        ~LckService()
        {
            Dispose(false);
        }
        
        internal LckResult StopRecording(StopReason stopReason = StopReason.UserStopped)
        {
            if(_disposed)
            {
                return LckResult.NewError(LckError.ServiceDisposed, "Service has been disposed");
            }

            return _recorder.StopRecording(stopReason);
        }
        
        internal static bool VerifyGraphicsApi()
        {
            var graphicsApi = SystemInfo.graphicsDeviceType;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    if (graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Vulkan || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                    {
                        return true;
                    }
                    LckLog.LogError("LCK requires Vulkan or OpenGLES3 graphics API on Android. Any other api is not supported.");
                    return false;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    if (graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Vulkan || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                        || graphicsApi == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore)
                    {
                        return true;
                    }
                    LckLog.LogError("LCK requires the Vulkan, OpenGLCore or DirectX 11 graphics API on Windows. Any other api is not supported.");
                    return false;
            }
            return false;
        }

        internal static bool VerifyPlatform()
        {
            var isValidPlatform = (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor);
            if(!isValidPlatform)
            {
                LckLog.LogError($"LCK is not supported on {Application.platform}.");
            }

            return isValidPlatform;
        }
    }
}
