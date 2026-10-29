using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Liv.Lck.Core;
using Liv.Lck.Encoding;
using Liv.Lck.Telemetry;
using UnityEngine;
using static Liv.Lck.LckEvents;

namespace Liv.Lck.Streaming
{
    internal class LckStreamer : ILckStreamer
    {
        private readonly ILckNativeStreamingService _nativeStreamingService;
        private readonly ILckEncoder _encoder;
        private readonly ILckOutputConfigurer _outputConfigurer;
        private readonly ILckEventBus _eventBus;
        private readonly ILckTelemetryClient _telemetryClient;
        private readonly ILckTelemetryContextProvider _telemetryContextProvider;
        
        private const EncoderConsumer ConsumerName = EncoderConsumer.Streaming;

        private float _streamStartTime;
        private bool _disposed;
        private CameraTrackDescriptor _currentStreamDescriptor;
        private LckEncodedPacketHandler _streamingPacketHandler;
        private Dictionary<string, object> _streamingTelemetryContext = new Dictionary<string, object>();
        
        public bool IsStreaming => CurrentCaptureState != LckCaptureState.Idle;

        public LckResult<bool> IsPaused()
        {
            return LckResult<bool>.NewSuccess(CurrentCaptureState == LckCaptureState.Paused);
        }
        
        public LckCaptureState CurrentCaptureState { get; private set; }
        
        private float CurrentStreamDurationSeconds => Time.time - _streamStartTime;

        public LckStreamer(
            ILckNativeStreamingService nativeStreamingService, 
            ILckEncoder encoder, 
            ILckOutputConfigurer outputConfigurer, 
            ILckEventBus eventBus,
            ILckTelemetryClient telemetryClient,
            ILckTelemetryContextProvider telemetryContextProvider)
        {
            _nativeStreamingService = nativeStreamingService;
            _encoder = encoder;
            _outputConfigurer = outputConfigurer;
            _eventBus = eventBus;
            _telemetryClient = telemetryClient;
            _telemetryContextProvider = telemetryContextProvider;

            _eventBus.AddListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.AddListener<CaptureErrorEvent>(OnCaptureError);
            
            var context = new Dictionary<string, object> { { "service", nameof(LckStreamer) } };
            _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.ServiceCreated, context));
        }

        public LckResult StartStreaming()
        {
            if (IsStreaming)
            {
                LckLog.LogError("Streaming already started");
                return LckResult.NewError(LckError.StreamingError, "Streaming already started");
            }
            
            if (!_nativeStreamingService.HasNativeStreamer())
            {
                var setUpNativeStreamerResult = SetUpNativeStreamer();
                if (!setUpNativeStreamerResult.Success)
                {
                    // Telemetry is sent within SetUpNativeStreamer
                    return setUpNativeStreamerResult;
                }
            }
            
            CurrentCaptureState = LckCaptureState.Starting;
            _ = StartStreamingAsync();
            
            return LckResult.NewSuccess();
        }
        
        public LckResult StopStreaming(LckService.StopReason stopReason)
        {
            LckLog.Log($"LCK {nameof(StopStreaming)} triggered with stop reason: {stopReason}");

            if (!_nativeStreamingService.HasNativeStreamer())
            {
                LckLog.LogError("Native streamer does not exist");
                return LckResult.NewError(LckError.StreamingError, "Native streamer does not exist");
            }

            if (_encoder == null)
            {
                LckLog.LogError("Encoder is null");
                return LckResult.NewError(LckError.StreamingError, "Encoder is null");
            }

            if (!IsStreaming)
            {
                LckLog.LogError("Streaming is already stopped");
                return LckResult.NewError(LckError.StreamingError, "Streaming is already stopped");
            }
            
            LckLog.Log("LCK Stopping Streaming");
            CurrentCaptureState = LckCaptureState.Stopping;

            _ = StopStreamingAsync(stopReason);
            
            return LckResult.NewSuccess();
        }

        public LckResult<TimeSpan> GetStreamDuration()
        {
            if (!IsStreaming)
            {
                return LckResult<TimeSpan>.NewError(LckError.StreamingError, "Stream has not been started.");
            }
            
            return LckResult<TimeSpan>.NewSuccess(TimeSpan.FromSeconds(CurrentStreamDurationSeconds));
        }

        public void SetLogLevel(NGFX.LogLevel logLevel)
        {
            _nativeStreamingService.SetNativeStreamerLogLevel(logLevel);
        }

        private async Task<LckResult> StartNativeStreamerAsync(int width, int height)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_nativeStreamingService.StartNativeStreamer(width, height))
                        return LckResult.NewSuccess();

                    LckLog.LogError($"Failed to start native streamer (resolution: {width}x{height})");
                    return LckResult.NewError(LckError.StreamingError, "Failed to start native streamer");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    LckLog.LogError($"Failed to start native streamer (resolution: {width}x{height}): {ex.Message}");
                    return LckResult.NewError(LckError.StreamingError, ex.Message);
                }
            });
        }

        private async Task StartStreamingAsync()
        {
            _outputConfigurer.SetActiveCaptureType(LckCaptureType.Streaming);

            var getCameraTrackDescriptorResult = _outputConfigurer.GetActiveCameraTrackDescriptor();
            if (!getCameraTrackDescriptorResult.Success)
            {
                CurrentCaptureState = LckCaptureState.Idle;
                var error = LckResult.NewError(LckError.UnknownError, getCameraTrackDescriptorResult.Message);
                TriggerStreamingStartedEvent(error);
                return;
            }

            _currentStreamDescriptor = getCameraTrackDescriptorResult.Result;
            UpdateStreamingTelemetryContext();
            
            var cameraResolutionDescriptor = _currentStreamDescriptor.CameraResolutionDescriptor;
            var width = (int)cameraResolutionDescriptor.Width;
            var height = (int)cameraResolutionDescriptor.Height;

            var startNativeStreamerResult = await StartNativeStreamerAsync(width, height);
            if (!startNativeStreamerResult.Success)
            {
                CurrentCaptureState = LckCaptureState.Idle;
                TriggerStreamingStartedEvent(startNativeStreamerResult);
                return;
            }

            _streamingPacketHandler = new LckEncodedPacketHandler(this,
                _nativeStreamingService.GetStreamPacketCallback());

            var acquireResult = _encoder.AcquireEncoder(ConsumerName, _currentStreamDescriptor,
                new[] { _streamingPacketHandler });
            if (!acquireResult.Success)
            {
                CurrentCaptureState = LckCaptureState.Idle;
                LckLog.LogError($"Failed to start encoding for streaming (resolution: {width}x{height}): {acquireResult.Message}");
                TriggerStreamingStartedEvent(acquireResult);
                return;
            }

            CurrentCaptureState = LckCaptureState.InProgress;
            _streamStartTime = Time.time;

            LckLog.Log($"Streaming started with dimensions {width}x{height}");
            TriggerStreamingStartedEvent(LckResult.NewSuccess());
        }

        private async Task<LckResult> StopNativeStreamerAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _nativeStreamingService.StopNativeStreamer()
                        ? LckResult.NewSuccess()
                        : LckResult.NewError(LckError.StreamingError, "Failed to stop native streamer");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return LckResult.NewError(LckError.StreamingError, ex.Message);
                }
            });
        }
        
 		private async Task StopStreamingAsync(LckService.StopReason stopReason)
        {
            try
            {
                var releaseResult = await _encoder.ReleaseEncoderAsync(ConsumerName,
                    new[] { _streamingPacketHandler });
                if (!releaseResult.Success)
                {
                    TriggerStreamingStoppedEvent(releaseResult);
                    return;
                }

                var stopNativeStreamerResult = await StopNativeStreamerAsync();
                if (!stopNativeStreamerResult.Success)
                {
                    TriggerStreamingStoppedEvent(stopNativeStreamerResult);
                    return;
                }

                _nativeStreamingService.DestroyNativeStreamer();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                TriggerStreamingStoppedEvent(LckResult.NewError(LckError.StreamingError, ex.Message));
                return;
            }
            
            CurrentCaptureState = LckCaptureState.Idle;
            _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.StreamingStopped,
                _streamingTelemetryContext));
            TriggerStreamingStoppedEvent(LckResult.NewSuccess());
        }
        
        private LckResult SetUpNativeStreamer()
        {
            if (_nativeStreamingService.HasNativeStreamer())
            {
                LckLog.LogError("Streamer already exists");
                return LckResult.NewError(LckError.StreamingError, "Streamer already exists");
            }

            if (!_nativeStreamingService.CreateNativeStreamer())
            {
                LckLog.LogError("LCK Failed to create native streamer");
                return LckResult.NewError(LckError.StreamingError, "LCK Failed to create native streamer");
            }

            if (!_nativeStreamingService.GetStreamPacketCallback().IsValid)
            {
                _nativeStreamingService.DestroyNativeStreamer();
                LckLog.LogError("LCK Failed to get streamer callback function");
                return LckResult.NewError(LckError.StreamingError, "LCK Failed to get streamer callback function");
            }

            return LckResult.NewSuccess();
        }

        private void OnEncoderStopped(EncoderStoppedEvent encoderStoppedEvent)
        {
            if (CurrentCaptureState != LckCaptureState.InProgress)
                return;

            LckLog.LogError($"Encoder stopped unexpectedly during streaming (duration: {CurrentStreamDurationSeconds}s) - stopping stream");
            StopStreaming(LckService.StopReason.Error);
        }
        
        private void TriggerStreamingStoppedEvent(LckResult result)
        {
            _eventBus.Trigger(new StreamingStoppedEvent(result));
        }

        private void TriggerStreamingStartedEvent(LckResult result)
        {
            _eventBus.Trigger(new StreamingStartedEvent(result));
        }
        
        private void UpdateStreamingTelemetryContext()
        {
            _streamingTelemetryContext = new Dictionary<string, object>
            {
                { "streaming.durationSeconds", CurrentStreamDurationSeconds },
                { "streaming.targetResolutionX", _currentStreamDescriptor.CameraResolutionDescriptor.Width },
                { "streaming.targetResolutionY", _currentStreamDescriptor.CameraResolutionDescriptor.Height },
                { "streaming.targetFramerate", _currentStreamDescriptor.Framerate },
                { "streaming.targetBitrate", _currentStreamDescriptor.Bitrate },
                { "streaming.targetAudioBitrate", _currentStreamDescriptor.AudioBitrate }
            };

            var audioChannelsResult = _outputConfigurer.GetNumberOfAudioChannels();
            if (audioChannelsResult.Success)
            {
                _streamingTelemetryContext.Add("streaming.audioChannels", audioChannelsResult.Result);
            }
            var audioSampleRateResult = _outputConfigurer.GetAudioSampleRate();
            if (audioSampleRateResult.Success)
            {
                _streamingTelemetryContext.Add("streaming.audioSampleRate", audioSampleRateResult.Result);
            }
            
            _telemetryContextProvider.SetTelemetryContext(LckTelemetryContextType.StreamingContext, 
                _streamingTelemetryContext);
        }
        
        private void OnCaptureError(CaptureErrorEvent captureErrorEvent)
        {
            if (CurrentCaptureState == LckCaptureState.Idle || CurrentCaptureState == LckCaptureState.Stopping)
                return;

            var errorMsg = $"Stopping stream because a capture error occurred: {captureErrorEvent.Error.Message}";
            LckLog.LogError(errorMsg);

            StopStreaming(LckService.StopReason.Error);
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            if (IsStreaming && !StopStreaming(LckService.StopReason.ApplicationLifecycle).Success)
            {
                LckLog.LogError($"Failed to stop streaming while disposing {nameof(LckStreamer)}");
            }
            
            _eventBus.RemoveListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.RemoveListener<CaptureErrorEvent>(OnCaptureError);
            
            _nativeStreamingService.DestroyNativeStreamer();
            
            _disposed = true;
            var disposeContext = new Dictionary<string, object> { { "service", nameof(LckStreamer) } };
            _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.ServiceDisposed, disposeContext));
            LckLog.Log($"{nameof(LckStreamer)} disposed");
        }
    }
}
