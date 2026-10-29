using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Liv.Lck.Core;
using Liv.Lck.Encoding;
using Liv.Lck.Recorder;
using Liv.Lck.Settings;
using Liv.Lck.Telemetry;
using Liv.Lck.Utilities;
using UnityEngine;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck.Echo
{
    internal class LckEcho : ILckEcho, ILckCaptureStateProvider
    {
        private readonly ILckEncoder _encoder;
        private readonly ILckOutputConfigurer _outputConfigurer;
        private readonly ILckEventBus _eventBus;
        private readonly ILckTelemetryClient _telemetryClient;
        private readonly ILckStorageWatcher _storageWatcher;

        private IntPtr _echoContext = IntPtr.Zero;
        private LckEncodedPacketHandler _echoPacketHandler;
        private bool _isSaving;
        private bool _disposed;
        private TimeSpan _lastSaveDuration;
        private Dictionary<string, object> _echoTelemetryContext = new Dictionary<string, object>();
        private readonly WaitForSeconds _copyEchoSpinWait = new WaitForSeconds(0.1f);

        /// <summary>
        /// Must be stored as a field to prevent GC collection of the delegate.
        /// Static because the native completion callback is a static
        /// <see cref="AOT.MonoPInvokeCallbackAttribute"/> method that cannot carry
        /// instance context through user data.
        /// </summary>
        /// <remarks>
        /// This means only a single <see cref="LckEcho"/> instance can be active at a time.
        /// The DI container enforces this — there is exactly one ILckEcho binding.
        /// </remarks>
        private static LckNativeEchoApi.EchoCompletionCallback _completionCallbackDelegate;

        /// <summary>
        /// Static reference for the static <see cref="OnNativeEchoCompleted"/> callback.
        /// Required because <see cref="AOT.MonoPInvokeCallbackAttribute"/> methods are static
        /// and cannot receive instance context via the native callback's parameters.
        /// </summary>
        /// <remarks>
        /// Only one <see cref="LckEcho"/> instance may be active at a time.
        /// </remarks>
        private static LckEcho _activeInstance;

        public bool IsEnabled => _echoContext != IntPtr.Zero &&
                                 LckNativeEchoApi.IsEchoBufferEnabled(_echoContext);
        public bool IsSaving => _isSaving;

        public LckCaptureState CurrentCaptureState =>
            IsEnabled ? LckCaptureState.InProgress : LckCaptureState.Idle;

        [Preserve]
        public LckEcho(
            ILckEncoder encoder,
            ILckOutputConfigurer outputConfigurer,
            ILckEventBus eventBus,
            ILckTelemetryClient telemetryClient,
            ILckStorageWatcher storageWatcher)
        {
            _encoder = encoder;
            _outputConfigurer = outputConfigurer;
            _eventBus = eventBus;
            _telemetryClient = telemetryClient;
            _storageWatcher = storageWatcher;

            _eventBus.AddListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.AddListener<CaptureErrorEvent>(OnCaptureError);
            _eventBus.AddListener<LowStorageSpaceDetectedEvent>(OnLowStorageSpaceDetected);
        }

        public LckResult<bool> IsPaused()
        {
            // Echo is never paused - always collecting while enabled
            return LckResult<bool>.NewSuccess(false);
        }

        public async Task<LckResult> SetEnabledAsync(bool enabled)
        {
            if (_disposed)
                return LckResult.NewError(LckError.ServiceDisposed, "Echo service has been disposed");

            try
            {
                if (enabled)
                    return Enable();

                return await DisableAsync();
            }
            catch (Exception ex)
            {
                LckLog.LogError($"Echo {(enabled ? "enable" : "disable")} failed: {ex.Message}");
                DestroyNativeContext();
                return LckResult.NewError(LckError.EncodingError, $"Echo {(enabled ? "enable" : "disable")} failed: {ex.Message}");
            }
        }

        public LckResult TriggerSave()
        {
            if (_disposed)
                return LckResult.NewError(LckError.ServiceDisposed, "Echo service has been disposed");

            if (!IsEnabled)
                return LckResult.NewError(LckError.RecordingError, "Echo is not enabled");

            if (_isSaving)
                return LckResult.NewError(LckError.CaptureAlreadyStarted, "Echo save already in progress");

            if (!_storageWatcher.HasEnoughFreeStorage())
                return LckResult.NewError(LckError.NotEnoughStorageSpace, "Not enough storage space to save echo.");

            var filename = FileUtility.GenerateEchoFilename("mp4");
            var outputPath = Path.Combine(Application.temporaryCachePath, filename);

            _lastSaveDuration = GetBufferDuration();
            _isSaving = true;
            var result = LckNativeEchoApi.TriggerEchoSave(_echoContext, outputPath);

            if (!result)
            {
                _isSaving = false;
                return LckResult.NewError(LckError.RecordingError,
                    "Failed to trigger echo save - buffer may be empty or missing keyframe");
            }

            LckLog.Log($"Echo save triggered to: {outputPath}");
            return LckResult.NewSuccess();
        }

        public TimeSpan GetBufferDuration()
        {
            if (_echoContext == IntPtr.Zero)
                return TimeSpan.Zero;

            var durationUs = LckNativeEchoApi.GetEchoBufferDurationUs(_echoContext);
            return TimeSpan.FromMilliseconds(durationUs / 1000.0);
        }

        public TimeSpan GetMaxBufferDuration()
        {
            var durationUs = LckNativeEchoApi.GetEchoBufferMaxDuration(_echoContext);
            return TimeSpan.FromMilliseconds(durationUs / 1000.0);
        }

        private LckResult Enable()
        {
            if (IsEnabled)
                return LckResult.NewSuccess();

            if (!_storageWatcher.HasEnoughFreeStorage())
                return LckResult.NewError(LckError.NotEnoughStorageSpace, "Not enough storage space to enable echo.");

            // Create native echo context with disk-backed storage.
            // Packet data is written to a temp file to reduce heap usage.
            // Falls back to in-memory if disk creation fails.
            _echoContext = LckNativeEchoApi.CreateEchoDiskBuffer(Application.temporaryCachePath);
            if (_echoContext == IntPtr.Zero)
                return LckResult.NewError(LckError.EncodingError, "Failed to create native echo buffer");

            // Set completion callback
            _activeInstance = this;
            _completionCallbackDelegate = OnNativeEchoCompleted;
            LckNativeEchoApi.SetEchoCompletionCallback(_echoContext, _completionCallbackDelegate);

            // Build and set muxer config from current recording settings
            var configResult = BuildMuxerConfig();
            if (!configResult.Success)
            {
                DestroyNativeContext();
                return LckResult.NewError(LckError.EncodingError, configResult.Message);
            }

            var muxerConfig = configResult.Result;
            LckNativeEchoApi.SetEchoMuxerConfig(_echoContext, ref muxerConfig);

            // Build echo packet handler
            var echoCallback = new LckEncodedPacketCallback(
                _echoContext,
                LckNativeEchoApi.GetEchoCallbackFunction());

            _echoPacketHandler = new LckEncodedPacketHandler(this, echoCallback);

            // Get current track descriptor
            var descriptorResult = _outputConfigurer.GetActiveCameraTrackDescriptor();
            if (!descriptorResult.Success)
            {
                DestroyNativeContext();
                return LckResult.NewError(LckError.EncodingError, descriptorResult.Message);
            }

            // Acquire encoder with echo as consumer
            var acquireResult = _encoder.AcquireEncoder(EncoderConsumer.Echo, descriptorResult.Result,
                new[] { _echoPacketHandler });

            if (!acquireResult.Success)
            {
                DestroyNativeContext();
                return acquireResult;
            }

            // Enable buffer collection
            LckNativeEchoApi.SetEchoBufferEnabled(_echoContext, true);

            LckLog.Log("Echo enabled");

            _echoTelemetryContext = new Dictionary<string, object>
            {
                { "echo.targetResolutionX", descriptorResult.Result.CameraResolutionDescriptor.Width },
                { "echo.targetResolutionY", descriptorResult.Result.CameraResolutionDescriptor.Height },
                { "echo.targetFramerate", descriptorResult.Result.Framerate },
                { "echo.targetBitrate", descriptorResult.Result.Bitrate },
                { "echo.targetAudioBitrate", descriptorResult.Result.AudioBitrate },
                { "echo.bufferDuration", LckNativeEchoApi.GetEchoBufferMaxDuration(_echoContext) / 1_000_000.0 }
            };
            _telemetryClient.SendTelemetry(new LckTelemetryEvent(LckTelemetryEventType.EchoEnabled, _echoTelemetryContext));

            _eventBus.Trigger(new LckEvents.EchoEnabledEvent(LckResult.NewSuccess()));
            return LckResult.NewSuccess();
        }

        private async Task<LckResult> DisableAsync()
        {
            if (!IsEnabled)
            {
                _eventBus.Trigger(new LckEvents.EchoDisabledEvent(LckResult.NewSuccess()));
                return LckResult.NewSuccess();
            }

            LckNativeEchoApi.SetEchoBufferEnabled(_echoContext, false);

            var echoContext = _echoContext;
            _echoContext = IntPtr.Zero;
            _activeInstance = null;

            await _encoder.ReleaseEncoderAsync(EncoderConsumer.Echo, new[] { _echoPacketHandler });
            DestroyEchoBuffer(echoContext);

            LckLog.Log("Echo disabled");
            _eventBus.Trigger(new LckEvents.EchoDisabledEvent(LckResult.NewSuccess()));
            return LckResult.NewSuccess();
        }

        private void DestroyNativeContext()
        {
            DestroyEchoBuffer(_echoContext);
            _echoContext = IntPtr.Zero;
            _activeInstance = null;
        }

        private static void DestroyEchoBuffer(IntPtr echoContext)
        {
            if (echoContext != IntPtr.Zero)
                LckNativeEchoApi.DestroyEchoBuffer(echoContext);
        }

        private LckResult<MuxerConfig> BuildMuxerConfig()
        {
            var descriptorResult = _outputConfigurer.GetActiveCameraTrackDescriptor();
            if (!descriptorResult.Success)
                return LckResult<MuxerConfig>.NewError(LckError.EncodingError,
                    "Failed to get camera track descriptor");

            var sampleRateResult = _outputConfigurer.GetAudioSampleRate();
            if (!sampleRateResult.Success)
                return LckResult<MuxerConfig>.NewError(LckError.EncodingError,
                    "Failed to get audio sample rate");

            var channelsResult = _outputConfigurer.GetNumberOfAudioChannels();
            if (!channelsResult.Success)
                return LckResult<MuxerConfig>.NewError(LckError.EncodingError,
                    "Failed to get number of audio channels");

            var descriptor = descriptorResult.Result;

            return LckResult<MuxerConfig>.NewSuccess(new MuxerConfig
            {
                outputPath = "", // Will be overridden by TriggerEchoSave
                videoBitrate = descriptor.Bitrate,
                audioBitrate = descriptor.AudioBitrate,
                width = descriptor.CameraResolutionDescriptor.Width,
                height = descriptor.CameraResolutionDescriptor.Height,
                framerate = descriptor.Framerate,
                samplerate = sampleRateResult.Result,
                channels = channelsResult.Result,
                numberOfTracks = 2,
                realtimeOutput = false
            });
        }

        [AOT.MonoPInvokeCallback(typeof(LckNativeEchoApi.EchoCompletionCallback))]
        private static void OnNativeEchoCompleted(uint status, string outputPath)
        {
            LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() =>
            {
                var instance = _activeInstance;
                if (instance == null || instance._disposed)
                    return;

                if (status == 0) // EchoSaveStatus::Success
                {
                    LckLog.Log($"Echo save completed: {outputPath}");
                    LckMonoBehaviourMediator.StartCoroutine("CopyEchoToGalleryWhenReady",
                        instance.CopyEchoToGalleryWhenReady(outputPath));
                }
                else
                {
                    instance._isSaving = false;
                    var errorMsg = $"Echo save failed with status {status}";
                    LckLog.LogError(errorMsg);
                    instance._eventBus.Trigger(new EchoSavedEvent(
                        LckResult<RecordingData>.NewError(LckError.RecordingError, errorMsg)));
                }
            });
        }

        private IEnumerator CopyEchoToGalleryWhenReady(string outputPath)
        {
            while (FileUtility.IsFileLocked(outputPath) && File.Exists(outputPath))
            {
                yield return _copyEchoSpinWait;
            }

            Task task = FileUtility.CopyToGallery(outputPath, LckSettings.Instance.RecordingAlbumName,
                (success, path) =>
                {
                    LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() =>
                    {
                        if (_disposed) return;

                        _isSaving = false;

                        if (success)
                        {
                            LckLog.Log("LCK Echo saved to gallery: " + path);
                            var recordingData = new RecordingData
                            {
                                RecordingFilePath = path,
                                RecordingDuration = (float)_lastSaveDuration.TotalSeconds
                            };
                            _eventBus.Trigger(new EchoSavedEvent(
                                LckResult<RecordingData>.NewSuccess(recordingData)));
                            var echoSavedContext = new Dictionary<string, object>(_echoTelemetryContext)
                            {
                                { "echo.duration", _lastSaveDuration.TotalSeconds }
                            };

                            var encoderSessionData = _encoder.GetCurrentSessionData();
                            var encodedVideoFrameCount = encoderSessionData.EncodedVideoFrames;
                            var clipDuration = (float)_lastSaveDuration.TotalSeconds;
                            var actualFramerate = (clipDuration > 0 && encodedVideoFrameCount > 0)
                                ? (encodedVideoFrameCount / clipDuration)
                                : 0f;
                            echoSavedContext.Add("echo.encodedFrames", encodedVideoFrameCount);
                            echoSavedContext.Add("echo.actualFramerate", actualFramerate);

                            _telemetryClient.SendTelemetry(new LckTelemetryEvent(
                                LckTelemetryEventType.EchoSaved, echoSavedContext));
                        }
                        else
                        {
                            LckLog.LogError("LCK Failed to save echo to gallery");
                            _eventBus.Trigger(new EchoSavedEvent(
                                LckResult<RecordingData>.NewError(
                                    LckError.FailedToCopyRecordingToGallery,
                                    "Failed to copy echo recording to Gallery")));
                        }
                    });
                });

            yield return new WaitUntil(() => task.IsCompleted);
        }

        private void OnLowStorageSpaceDetected(LowStorageSpaceDetectedEvent lowStorageSpaceDetectedEvent)
        {
            if (!IsEnabled)
                return;

            LckLog.Log("Low storage space detected - disabling echo");
            _eventBus.Trigger(new LckEvents.EchoDisabledEvent(
                LckResult.NewSuccess(), EchoDisableReason.LowStorage));
            _ = DisableAsync();
        }

        private void OnEncoderStopped(EncoderStoppedEvent encoderStoppedEvent)
        {
            if (!IsEnabled)
                return;

            LckLog.Log("Encoder stopped while echo was active - disabling echo");
            _ = DisableAsync();
        }

        private void OnCaptureError(CaptureErrorEvent captureErrorEvent)
        {
            if (!IsEnabled)
                return;

            LckLog.LogError($"Echo capture error: {captureErrorEvent.Error.Message}");
            _eventBus.Trigger(new LckEvents.EchoDisabledEvent(
                LckResult.NewSuccess(), EchoDisableReason.Error));
            _ = DisableAsync();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (IsEnabled)
                _ = DisableAsync();

            _eventBus.RemoveListener<EncoderStoppedEvent>(OnEncoderStopped);
            _eventBus.RemoveListener<CaptureErrorEvent>(OnCaptureError);
            _eventBus.RemoveListener<LowStorageSpaceDetectedEvent>(OnLowStorageSpaceDetected);

            _disposed = true;
        }
    }
}
