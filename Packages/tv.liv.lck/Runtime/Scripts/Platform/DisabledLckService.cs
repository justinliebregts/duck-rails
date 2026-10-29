using System;
using System.Threading.Tasks;
using Liv.Lck.Recorder;
using UnityEngine;

namespace Liv.Lck
{
    /// <summary>
    /// A no-op <see cref="ILckService"/> used on platforms where LCK is not supported
    /// (currently the macOS Unity Editor, which has no native binaries deployed).
    ///
    /// Capture actions (start recording/streaming, capture photo, enable/save echo) return an
    /// <see cref="LckError.UnsupportedPlatform"/> error so callers that check the result can react.
    /// Configuration setters, stops, and "is..." queries return benign successes so a game integrating
    /// LCK keeps running normally in the editor without throwing <see cref="DllNotFoundException"/>.
    ///
    /// Registered by <see cref="LckPlatformGuard"/> before scene load, which causes
    /// <see cref="LckServiceInitializer"/> to skip building the real (native-backed) service chain.
    /// </summary>
    public sealed class DisabledLckService : ILckService
    {
        private const string DisabledMessage = "LIV/LCK is disabled on this platform (macOS Unity Editor).";

        // These events are part of the contract but are never raised by the disabled service.
#pragma warning disable CS0067
        public event Action<LckResult> OnRecordingStarted;
        public event Action<LckResult> OnRecordingPaused;
        public event Action<LckResult> OnRecordingResumed;
        public event Action<LckResult> OnRecordingStopped;
        public event Action<LckResult> OnStreamingStarted;
        public event Action<LckResult> OnStreamingStopped;
        public event Action<LckResult> OnLowStorageSpace;
        public event Action<LckResult<RecordingData>> OnRecordingSaved;
        public event Action<LckResult> OnPhotoSaved;
        public event Action<LckResult<ILckCamera>> OnActiveCameraSet;
        public event Action<LckResult<RecordingData>> OnEchoSaved;
        public event Action<LckResult> OnEchoEnabled;
        public event Action<LckResult, EchoDisableReason> OnEchoDisabled;
#pragma warning restore CS0067

        // Capture actions: report unsupported.
        public LckResult StartRecording() => Disabled();
        public LckResult PauseRecording() => Disabled();
        public LckResult ResumeRecording() => Disabled();
        public LckResult StartStreaming() => Disabled();
        public LckResult CapturePhoto() => Disabled();
        public LckResult SetEchoEnabled(bool enabled) => Disabled();
        public Task<LckResult> SetEchoEnabledAsync(bool enabled) => Task.FromResult(Disabled());
        public LckResult TriggerEchoSave() => Disabled();

        // Stops are safe no-ops (nothing is running to stop).
        public LckResult StopRecording() => Success();
        public LckResult StopStreaming() => Success();

        // Configuration setters: accept silently so integrating game code keeps working.
        public LckResult SetTrackFramerate(uint framerate) => Success();
        public LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor) => Success();
        public LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor) => Success();
        public LckResult SetTrackBitrate(uint bitrate) => Success();
        public LckResult SetTrackAudioBitrate(uint audioBitrate) => Success();
        public LckResult SetCameraOrientation(LckCameraOrientation orientation) => Success();
        public LckResult SetTrackDescriptor(LckCaptureType captureType, CameraTrackDescriptor cameraTrackDescriptor) => Success();
        public LckResult SetActiveCaptureType(LckCaptureType captureType) => Success();
        public LckResult SetPreviewActive(bool isActive) => Success();
        public LckResult SetGameAudioCaptureActive(bool isActive) => Success();
        public LckResult SetMicrophoneCaptureActive(bool isActive) => Success();
        public LckResult SetMicrophoneGain(float gain) => Success();
        public LckResult SetGameAudioGain(float gain) => Success();
        public LckResult SetActiveCamera(string cameraId, string monitorId = null) => Success();
        public LckResult PreloadDiscreetAudio(AudioClip audioClip, float volume, bool forceReload = false) => Success();
        public LckResult PlayDiscreetAudioClip(AudioClip audioClip) => Success();
        public LckResult StopAllDiscreetAudio() => Success();

        // Queries: return inert, valid values.
        public LckResult<TimeSpan> GetRecordingDuration() => Success(TimeSpan.Zero);
        public LckResult<TimeSpan> GetStreamDuration() => Success(TimeSpan.Zero);
        public LckResult<LckCaptureType> GetActiveCaptureType() => Success(LckCaptureType.Recording);
        public LckResult<bool> IsPaused() => Success(false);
        public LckResult<bool> IsRecording() => Success(false);
        public LckResult<bool> IsStreaming() => Success(false);
        public LckResult<bool> IsCapturing() => Success(false);
        public LckResult<float> GetMicrophoneOutputLevel() => Success(0f);
        public LckResult<float> GetGameOutputLevel() => Success(0f);
        public LckResult<bool> IsGameAudioMute() => Success(true);
        public LckResult<bool> IsEchoEnabled() => Success(false);
        public LckResult<TimeSpan> GetEchoBufferDuration() => Success(TimeSpan.Zero);
        public LckResult<TimeSpan> GetEchoMaxBufferDuration() => Success(TimeSpan.Zero);
        public LckResult<ILckCamera> GetActiveCamera() => Disabled<ILckCamera>();
        public LckResult<LckDescriptor> GetDescriptor() => Disabled<LckDescriptor>();

        public void Dispose()
        {
        }

        private static LckResult Success() => LckResult.NewSuccess();

        private static LckResult Disabled() => LckResult.NewError(LckError.UnsupportedPlatform, DisabledMessage);

        private static LckResult<T> Success<T>(T result) => LckResult<T>.NewSuccess(result);

        private static LckResult<T> Disabled<T>() => LckResult<T>.NewError(LckError.UnsupportedPlatform, DisabledMessage);
    }
}