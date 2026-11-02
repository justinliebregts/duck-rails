using Liv.Lck.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Liv.Lck.Utilities;
using UnityEngine;
using Unity.Profiling;
using System.Threading.Tasks;
using Liv.Lck.Core;
using Liv.Lck.Encoding;
using Liv.Lck.Telemetry;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;
using Debug = UnityEngine.Debug;

namespace Liv.Lck.Recorder
{
    internal class LckRecorder : ILckRecorder
    {
        private readonly ILckNativeRecordingService _nativeRecordingService;
        private readonly ILckStorageWatcher _storageWatcher;
        private readonly ILckEncoder _encoder;
        private readonly ILckOutputConfigurer _outputConfigurer;
        private readonly ILckEventBus _eventBus;
        private readonly ILckTelemetryClient _telemetryClient;
        private readonly ILckTelemetryContextProvider _telemetryContextProvider;

        private static readonly ProfilerMarker _copyOutputFileToNativeGalleryMarker = new ProfilerMarker("LckRecorder.CopyOutputFileToNativeGallery");
        
        private MuxerConfig _muxerConfig;
        private float _recordingStartTime;
        private float _accumulatedRecordingDuration;
        private float _lastActiveSegmentStartTime;
        private string _lastRecordingFilePath;
        private LckService.StopReason _stopReason;
        private CameraTrackDescriptor _currentRecordingDescriptor;
        Dictionary<string, object> _recordingTelemetryContext = new Dictionary<string, object>();
        private bool _disposed;

        public LckCaptureState CurrentCaptureState { get; private set; } = LckCaptureState.Idle;
        
        [Preserve]
        public LckRecorder(
            ILckNativeRecordingService nativeRecordingService, 
            ILckEncoder encoder, 
            ILckOutputConfigurer outputConfigurer, 
            ILckStorageWatcher storageWatcher, 
            ILckEventBus eventBus,
            ILckTelemetryClient telemetryClient,
            ILckTelemetryContextProvider telemetryContextProvider)
        {
            _nativeRecordingService = nativeRecordingService;
            _encoder = encoder;
            _outputConfigurer = outputConfigurer;
            _storageWatcher = storageWatcher;
            _eventBus = eventBus;
            _telemetryClient = telemetryClient;
            _telemetryContextProvider = telemetryContextProvider;

            _eventBus.AddListener<LowStorageSpaceDetectedEvent>(OnLowStorageSpaceDetected);
            _eventBus.AddListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.AddListener<CaptureErrorEvent>(OnCaptureError);
        }

        public LckResult<bool> IsRecording()
        {
            return LckResult<bool>.NewSuccess(CurrentCaptureState == LckCaptureState.InProgress);
        }

        public LckResult<bool> IsPaused()
        {
            return LckResult<bool>.NewSuccess(CurrentCaptureState == LckCaptureState.Paused);
        }

        public void SetLogLevel(NGFX.LogLevel logLevel)
        {
            _nativeRecordingService.SetNativeMuxerLogLevel(logLevel);
        }

        public LckResult StartRecording()
        {
            if (CurrentCaptureState != LckCaptureState.Idle)
            {
                return LckResult.NewError(LckError.CaptureAlreadyStarted, "Recording already started.");
            }

            if (!_storageWatcher.HasEnoughFreeStorage())
            {
                return LckResult.NewError(LckError.NotEnoughStorageSpace, "Not enough storage space.");
            }

            StartRecordingProcess();
            return LckResult.NewSuccess();
        }
        
        public LckResult StopRecording(LckService.StopReason stopReason)
        {
            LckLog.Log($"LCK {nameof(StopRecording)} triggered with stop reason: {stopReason}");
            
            if (CurrentCaptureState != LckCaptureState.InProgress)
            {
                return LckResult.NewError(LckError.NotCurrentlyRecording, "No recording currently in progress to stop.");
            }
            LckLog.Log($"LCK StopRecording triggered with stopreason: {stopReason}");

            _stopReason = stopReason;
            StopRecordingProcess();
            return LckResult.NewSuccess();
        }

        public LckResult PauseRecording()
        {
            LckResult result;
            if (CurrentCaptureState != LckCaptureState.InProgress)
            {
                result = LckResult.NewError(LckError.NotCurrentlyRecording,
                    "Cannot pause because recording is not in progress.");
            }
            else
            {
                // Accumulate the duration of the current active segment before pausing
                _accumulatedRecordingDuration += Time.time - _lastActiveSegmentStartTime;
                result = LckResult.NewSuccess();
                CurrentCaptureState = LckCaptureState.Paused;
                LckLog.Log("LCK Recording paused.");
            }

            TriggerRecordingPausedEvent(result);
            return result;
        }

        public LckResult ResumeRecording()
        {
            LckResult result;
            if (CurrentCaptureState != LckCaptureState.Paused)
            {
                result = LckResult.NewError(LckError.NotPaused,
                    "Cannot resume because recording is not paused.");
            }
            else
            {
                _lastActiveSegmentStartTime = Time.time;
                result = LckResult.NewSuccess();
                CurrentCaptureState = LckCaptureState.InProgress;
                LckLog.Log("LCK Recording resumed.");
            }

            TriggerRecordingResumedEvent(result);
            return result;
        }

        public LckResult<TimeSpan> GetRecordingDuration()
        {
            if (CurrentCaptureState == LckCaptureState.Idle)
            {
                return LckResult<TimeSpan>.NewError(LckError.NotCurrentlyRecording, "Recording has not been started.");
            }

            return LckResult<TimeSpan>.NewSuccess(TimeSpan.FromSeconds(ActualRecordingDurationSeconds));
        }

        private async Task<LckResult> StartNativeMuxerAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _nativeRecordingService.StartNativeMuxer(ref _muxerConfig)
                        ? LckResult.NewSuccess()
                        : LckResult.NewError(LckError.RecordingError, "Failed to start native muxer");
                }
                catch (Exception ex)
                {
                    LckLog.LogError($"Failed to start recording - exception occurred while starting muxer: {ex}");
                    CurrentCaptureState = LckCaptureState.Idle;
                    return LckResult.NewError(LckError.RecordingError, ex.Message);
                }
            });
        }
        
        private async Task StartRecordingAsync()
        {
            try
            {
                LckLog.Log("LCK Starting Recording");
                
                // If not already capturing with recording settings, change capture settings before stream starts
                _outputConfigurer.SetActiveCaptureType(LckCaptureType.Recording);
                
                var getCameraTrackDescriptorResult = _outputConfigurer.GetActiveCameraTrackDescriptor();
                if (!getCameraTrackDescriptorResult.Success)
                {
                    CurrentCaptureState = LckCaptureState.Idle;
                    var error = LckResult.NewError(LckError.UnknownError, getCameraTrackDescriptorResult.Message);
                    TriggerRecordingStartedEvent(error);
                    return;
                }
                
                _currentRecordingDescriptor = getCameraTrackDescriptorResult.Result;
                UpdateRecordingTelemetryContext();
                
                if (!_nativeRecordingService.CreateNativeMuxer())
                {
                    var result = LckResult.NewError(LckError.RecordingError, "Failed to create native muxer");
                    CurrentCaptureState = LckCaptureState.Idle;
                    TriggerRecordingStartedEvent(result);
                    return;
                }

                _muxerConfig = CreateMuxerConfig();
                
                var startNativeMuxerResult = await StartNativeMuxerAsync();
                if (!startNativeMuxerResult.Success)
                {
                    LckLog.LogError("LCK Recording could not be started");
                    CurrentCaptureState = LckCaptureState.Idle;
                    TriggerRecordingStartedEvent(startNativeMuxerResult);
                    return;
                }

                CurrentCaptureState = LckCaptureState.InProgress;
                _recordingStartTime = Time.time;
                _accumulatedRecordingDuration = 0f;
                _lastActiveSegmentStartTime = Time.time;
                _storageWatcher.SetRecordingContext(_currentRecordingDescriptor, () => ActualRecordingDurationSeconds);

                var muxPacketHandler = new LckEncodedPacketHandler(this,
                    _nativeRecordingService.GetMuxPacketCallback());
                
                var startEncodingResult = _encoder.StartEncoding(_currentRecordingDescriptor, new[] { muxPacketHandler });
                if (!startEncodingResult.Success)
                {
                    CurrentCaptureState = LckCaptureState.Idle;
                    TriggerRecordingStartedEvent(startEncodingResult);
                    return;
                }
                
                TriggerRecordingStartedEvent(LckResult.NewSuccess());
                LckLog.Log("Recording started successfully");
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Start Recording Task failed: " + e.Message);
                
                if (_encoder.IsActive())
                    await _encoder.StopEncodingAsync();
                
                TriggerRecordingStartedEvent(LckResult.NewError(LckError.RecordingError, e.Message));
            }
        }

        private float ActualRecordingDurationSeconds
        {
            get
            {
                if (CurrentCaptureState == LckCaptureState.InProgress)
                {
                    return _accumulatedRecordingDuration + (Time.time - _lastActiveSegmentStartTime);
                }
                // When paused or stopping, return accumulated duration
                return _accumulatedRecordingDuration;
            }
        }

        private async Task<LckResult> StopNativeMuxerAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _nativeRecordingService.StopNativeMuxer()
                        ? LckResult.NewSuccess()
                        : LckResult.NewError(LckError.RecordingError, "Failed to stop native muxer");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return LckResult.NewError(LckError.RecordingError, ex.Message);
                }
            });
        }
        
        private async Task StopRecordingAsync()
        {
            try
            {
                var stopEncodingResult = await _encoder.StopEncodingAsync();
                if (!stopEncodingResult.Success)
                {
                    TriggerRecordingStoppedEvent(stopEncodingResult);
                    return;
                }

                var stopNativeMuxerResult = await StopNativeMuxerAsync();
                if (!stopNativeMuxerResult.Success)
                {
                    TriggerRecordingStoppedEvent(stopNativeMuxerResult);
                    return;
                }

                CurrentCaptureState = LckCaptureState.Idle;
                _storageWatcher.ClearRecordingContext();
                LckMonoBehaviourMediator.StartCoroutine("CopyRecordingToGalleryWhenReady", CopyRecordingToGalleryWhenReady());
                TriggerRecordingStoppedEvent(LckResult.NewSuccess());
                
                _nativeRecordingService.DestroyNativeMuxer();
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Stop Recording failed: " + e.Message);
                TriggerRecordingStoppedEvent(LckResult.NewError(LckError.RecordingError, e.Message));
            }
        }

        WaitForSeconds _copyVideoSpinWait = new WaitForSeconds(0.1f);
        private IEnumerator CopyRecordingToGalleryWhenReady()
        {
            while (FileUtility.IsFileLocked(_lastRecordingFilePath) && File.Exists(_lastRecordingFilePath))
            {
                yield return _copyVideoSpinWait;
            }
            
            using (_copyOutputFileToNativeGalleryMarker.Auto())
            {
                Task task = FileUtility.CopyToGallery(_lastRecordingFilePath, LckSettings.Instance.RecordingAlbumName,
                    (success, path) =>
                    {
                        LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() =>
                        {
                            if (success)
                            {
                                LckLog.Log("LCK Recording saved to gallery: " + path);
                                var recordingData = new RecordingData
                                {
                                    RecordingFilePath = path,
                                    RecordingDuration = ActualRecordingDurationSeconds
                                };
                                TriggerRecordingSavedEvent(LckResult<RecordingData>.NewSuccess(recordingData));
                            }
                            else
                            {
                                TriggerRecordingSavedEvent(LckResult<RecordingData>.NewError(LckError.FailedToCopyRecordingToGallery,
                                    "Failed to copy recording to Gallery"));
                                LckLog.LogError("LCK Failed to save recording to gallery");
                            }
                        });
                    });

                yield return new WaitUntil(() => task.IsCompleted);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (CurrentCaptureState == LckCaptureState.InProgress)
            {
                _ = StopRecordingAsync();
            }

            _storageWatcher.ClearRecordingContext();
            _eventBus.RemoveListener<LowStorageSpaceDetectedEvent>(OnLowStorageSpaceDetected);
            _eventBus.RemoveListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.RemoveListener<CaptureErrorEvent>(OnCaptureError);

            _disposed = true;
        }
        
        private void StartRecordingProcess()
        {
            CurrentCaptureState = LckCaptureState.Starting;
            _ = StartRecordingAsync();
        }
        
        private void StopRecordingProcess()
        {
            LckLog.Log("LCK Stopping Recording");
            if (CurrentCaptureState == LckCaptureState.InProgress)
            {
                _accumulatedRecordingDuration += Time.time - _lastActiveSegmentStartTime;
            }
            CurrentCaptureState = LckCaptureState.Stopping;
            SendRecordingStoppedTelemetry();
            _ = StopRecordingAsync();
        }

        private void SendRecordingStoppedTelemetry()
        {
            // Append context specific to stopping a recording
            var encoderSessionData = _encoder.GetCurrentSessionData();
            var encodedVideoFrameCount = encoderSessionData.EncodedVideoFrames;
            var actualDuration = ActualRecordingDurationSeconds;
            var actualFramerate = (actualDuration > 0 && encodedVideoFrameCount > 0)
                ? (encodedVideoFrameCount / actualDuration)
                : 0f;
            _recordingTelemetryContext.Add("recording.duration", actualDuration);
            _recordingTelemetryContext.Add("recording.encodedFrames", encodedVideoFrameCount);
            _recordingTelemetryContext.Add("recording.stopReason", _stopReason.ToString());
            _recordingTelemetryContext.Add("recording.actualFramerate", actualFramerate);

            _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.RecordingStopped, _recordingTelemetryContext));
        }

        private void UpdateRecordingTelemetryContext()
        {
            _recordingTelemetryContext = new Dictionary<string, object>
            {
                { "recording.targetFramerate", _currentRecordingDescriptor.Framerate },
                { "recording.targetBitrate", _currentRecordingDescriptor.Bitrate },
                { "recording.targetAudioBitrate", _currentRecordingDescriptor.AudioBitrate },
                { "recording.targetResolutionX", _currentRecordingDescriptor.CameraResolutionDescriptor.Width },
                { "recording.targetResolutionY", _currentRecordingDescriptor.CameraResolutionDescriptor.Height }
            };
            
            _telemetryContextProvider.SetTelemetryContext(LckTelemetryContextType.RecordingContext, 
                _recordingTelemetryContext);
        }

        private MuxerConfig CreateMuxerConfig()
        {
            var filename = FileUtility.GenerateFilename("mp4");
            _lastRecordingFilePath = Path.Combine(Application.temporaryCachePath, filename);

            var cameraTrackDescriptor = _outputConfigurer.GetActiveCameraTrackDescriptor().Result;
            return new MuxerConfig
            {
                outputPath = _lastRecordingFilePath,
                videoBitrate = cameraTrackDescriptor.Bitrate,
                audioBitrate = cameraTrackDescriptor.AudioBitrate,
                width = cameraTrackDescriptor.CameraResolutionDescriptor.Width,
                height = cameraTrackDescriptor.CameraResolutionDescriptor.Height,
                framerate = cameraTrackDescriptor.Framerate,
                samplerate = _outputConfigurer.GetAudioSampleRate().Result,
                channels = _outputConfigurer.GetNumberOfAudioChannels().Result,
                numberOfTracks = 2,
                realtimeOutput = false
            };
        }
        
        private void OnEncoderStopped(EncoderStoppedEvent encoderStoppedEvent)
        {
            if (CurrentCaptureState != LckCaptureState.InProgress)
                return;
            
            LckLog.LogError("Encoder stopped while recording - stopping recording");
            StopRecording(LckService.StopReason.Error);
        }

        private void OnLowStorageSpaceDetected(LowStorageSpaceDetectedEvent lowStorageSpaceDetectedEvent)
        {
            StopRecording(LckService.StopReason.LowStorageSpace);
        }
        
        private void TriggerRecordingStartedEvent(LckResult result)
        {
            _eventBus.Trigger(new RecordingStartedEvent(result));
        }
        
        private void TriggerRecordingPausedEvent(LckResult result)
        {
            _eventBus.Trigger(new RecordingPausedEvent(result));
        }

        private void TriggerRecordingResumedEvent(LckResult result)
        {
            _eventBus.Trigger(new RecordingResumedEvent(result));
        }
        
        private void TriggerRecordingStoppedEvent(LckResult result)
        {
            _eventBus.Trigger(new RecordingStoppedEvent(result));
        }

        private void TriggerRecordingSavedEvent(LckResult<RecordingData> result)
        {
            _eventBus.Trigger(new RecordingSavedEvent(result));
        }
        
        private void OnCaptureError(CaptureErrorEvent captureErrorEvent)
        {
            if (CurrentCaptureState == LckCaptureState.Idle || CurrentCaptureState == LckCaptureState.Stopping)
                return;

            var errorMsg = $"Stopping recording because a capture error occurred: {captureErrorEvent.Error.Message}";
            LckLog.LogError(errorMsg);

            StopRecording(LckService.StopReason.Error);
        }
    }
}
