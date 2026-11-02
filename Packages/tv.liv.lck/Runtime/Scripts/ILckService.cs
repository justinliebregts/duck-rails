using System;
using Liv.Lck.Recorder;
using UnityEngine;

namespace Liv.Lck
{
    public enum LckCaptureType
    {
        Recording,
        Streaming
    }
    
    public enum LckCameraOrientation
    {
        Portrait,
        Landscape
    }
    
    /// <summary>
    /// The primary public interface for the LCK in-game capturing system.
    /// This service provides all necessary functionality for recording, streaming, and capturing in-game content.
    /// </summary>
    public interface ILckService : IDisposable
    {
        /// <summary>
        /// Event fired when a recording session has successfully started.
        /// </summary>
        event Action<LckResult> OnRecordingStarted;
        
        /// <summary>
        /// Event fired when an ongoing recording is paused.
        /// </summary>
        event Action<LckResult> OnRecordingPaused;
        
        /// <summary>
        /// Event fired when a paused recording is resumed.
        /// </summary>
        event Action<LckResult> OnRecordingResumed;
        
        /// <summary>
        /// Event fired after a recording session has stopped.
        /// </summary>
        event Action<LckResult> OnRecordingStopped;
        
        /// <summary>
        /// Event fired when a streaming session has successfully started.
        /// </summary>
        event Action<LckResult> OnStreamingStarted;
        
        /// <summary>
        /// Event fired after a streaming session has stopped.
        /// </summary>
        event Action<LckResult> OnStreamingStopped;
        
        /// <summary>
        /// Event fired when the system detects that available storage space is low.
        /// </summary>
        event Action<LckResult> OnLowStorageSpace;
        
        /// <summary>
        /// Event fired when a completed recording has been saved to storage.
        /// Includes data about the saved recording, see <see cref="RecordingData"/>.
        /// </summary>
        event Action<LckResult<RecordingData>> OnRecordingSaved;
        
        /// <summary>
        /// Event fired when a captured photo has been saved to storage.
        /// </summary>
        event Action<LckResult> OnPhotoSaved;

        /// <summary>
        /// Event fired when the active camera has been changed.
        /// Includes a reference to the new active camera, see <see cref="ILckCamera"/>.
        /// </summary>
        event Action<LckResult<ILckCamera>> OnActiveCameraSet;
        
        /// <summary>
        /// Starts a new recording session with the current settings.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult StartRecording();
        
        /// <summary>
        /// Pauses the currently active recording session.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult PauseRecording();
        
        /// <summary>
        /// Checks if the current recording session is paused.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing true if paused, otherwise false.</returns>
        LckResult<bool> IsPaused();
        
        /// <summary>
        /// Resumes a previously paused recording session.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult ResumeRecording();
        
        /// <summary>
        /// Stops the currently active recording session.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult StopRecording();
        
        /// <summary>
        /// Starts a new streaming session with the current settings.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult StartStreaming();
        
        /// <summary>
        /// Stops the currently active streaming session.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult StopStreaming();

        /// <summary>
        /// Gets the elapsed time of the current ongoing recording.
        /// If no recording is active, an error will be returned.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the duration of the recording.</returns>
        LckResult<TimeSpan> GetRecordingDuration();
        
        /// <summary>
        /// Gets the elapsed time of the current or most recent stream.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the duration of the stream.</returns>
        LckResult<TimeSpan> GetStreamDuration();
        
        /// <summary>
        /// Sets the output framerate for the active capture track.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="framerate">The target framerate in frames per second.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackFramerate(uint framerate);
        
        /// <summary>
        /// Sets the track descriptor for the currently active capture type.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="cameraTrackDescriptor">The descriptor defining the camera track properties. See <see cref="CameraTrackDescriptor"/>.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackDescriptor(CameraTrackDescriptor cameraTrackDescriptor);
        
        /// <summary>
        /// Sets the output resolution for the active capture track.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="cameraResolutionDescriptor">The desired resolution settings. See <see cref="CameraResolutionDescriptor"/>.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackResolution(CameraResolutionDescriptor cameraResolutionDescriptor);
        
        /// <summary>
        /// Sets the video bitrate for the active capture track.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="bitrate">The target video bitrate in bits per second.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackBitrate(uint bitrate);
        
        /// <summary>
        /// Sets the audio bitrate for the active capture track.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="audioBitrate">The target audio bitrate in bits per second.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackAudioBitrate(uint audioBitrate);
        
        /// <summary>
        /// Sets the output orientation (Landscape, Portrait) for the camera.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="orientation">The desired camera orientation. See <see cref="LckCameraOrientation"/>.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetCameraOrientation(LckCameraOrientation orientation);
        
        /// <summary>
        /// Sets the track descriptor for a specific capture type.
        /// Note: Cannot be changed while recording or streaming.
        /// </summary>
        /// <param name="captureType">The capture type to which the descriptor will be applied. See <see cref="LckCaptureType"/>.</param>
        /// <param name="cameraTrackDescriptor">The descriptor defining the camera track properties. See <see cref="CameraTrackDescriptor"/>.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetTrackDescriptor(LckCaptureType captureType, CameraTrackDescriptor cameraTrackDescriptor);
        
        /// <summary>
        /// Gets the currently active capture type (recording, streaming).
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the active <see cref="LckCaptureType"/>.</returns>
        LckResult<LckCaptureType> GetActiveCaptureType();
        
        /// <summary>
        /// Sets the active capture type, which determines which set of track settings to use.
        /// </summary>
        /// <param name="captureType">The capture type to make active. See <see cref="LckCaptureType"/>.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetActiveCaptureType(LckCaptureType captureType);
        
        /// <summary>
        /// Activates or deactivates the on-screen preview of the capture output.
        /// </summary>
        /// <param name="isActive">True to activate the preview, false to deactivate it.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetPreviewActive(bool isActive);
        
        /// <summary>
        /// Checks if a recording session is currently in progress.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing true if recording, otherwise false.</returns>
        LckResult<bool> IsRecording();
        
        /// <summary>
        /// Checks if a streaming session is currently in progress.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing true if streaming, otherwise false.</returns>
        LckResult<bool> IsStreaming();
        
        /// <summary>
        /// Checks if the service is actively capturing video (recording or streaming).
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing true if capturing, otherwise false.</returns>
        LckResult<bool> IsCapturing();
        
        /// <summary>
        /// Enables or disables the capture of in-game audio.
        /// </summary>
        /// <param name="isActive">True to capture game audio, false to mute it.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetGameAudioCaptureActive(bool isActive);
        
        /// <summary>
        /// Enables or disables the capture of audio from the microphone.
        /// </summary>
        /// <param name="isActive">True to capture microphone audio, false to disable it.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetMicrophoneCaptureActive(bool isActive);
        
        /// <summary>
        /// Gets the current output signal level of the microphone.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the microphone level (typically from 0.0 to 1.0).</returns>
        LckResult<float> GetMicrophoneOutputLevel();
        
        /// <summary>
        /// Sets the gain (volume) for the microphone input.
        /// Note: Does not affect the microphone audio output to other systems.
        /// </summary>
        /// <param name="gain">The desired gain level. 1.0 is normal volume, values greater than 1.0 amplify, and values less than 1.0 attenuate.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetMicrophoneGain(float gain);
        
        /// <summary>
        /// Sets the gain (volume) for the in-game audio capture.
        /// Note: Does not affect the audio heard by the player in-game.
        /// </summary>
        /// <param name="gain">The desired gain level. 1.0 is normal volume, values greater than 1.0 amplify, and values less than 1.0 attenuate.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetGameAudioGain(float gain);
        
        /// <summary>
        /// Gets the current output signal level of the in-game audio.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the game audio level (typically from 0.0 to 1.0).</returns>
        LckResult<float> GetGameOutputLevel();
        
        /// <summary>
        /// Checks if the in-game audio capture is currently muted.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing true if the game audio is muted, otherwise false.</returns>
        LckResult<bool> IsGameAudioMute();
        
        /// <summary>
        /// Sets the active camera for video capture by its unique identifier.
        /// </summary>
        /// <param name="cameraId">The unique ID of the camera to activate.</param>
        /// <param name="monitorId">Optional ID of a specific monitor to use, for cameras with multiple outputs.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult SetActiveCamera(string cameraId, string monitorId = null);
        
        /// <summary>
        /// Gets the currently active camera instance.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing a reference to the active <see cref="ILckCamera"/> interface.</returns>
        LckResult<ILckCamera> GetActiveCamera();

        /// <summary>
        /// Preloads an audio clip for low-latency playback. This is intended for sound effects or other on-demand audio events.
        /// </summary>
        /// <param name="audioClip">The Unity AudioClip to preload.</param>
        /// <param name="volume">The volume at which the clip will be played.</param>
        /// <param name="forceReload">If true, the clip will be reloaded even if it's already in the cache.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult PreloadDiscreetAudio(AudioClip audioClip, float volume, bool forceReload = false);
        
        /// <summary>
        /// Plays a preloaded audio clip as a one-shot sound. Does not affect the main game or microphone audio tracks.
        /// </summary>
        /// <param name="audioClip">The Unity <see cref="AudioClip"/> to play.</param>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult PlayDiscreetAudioClip(AudioClip audioClip);
        
        /// <summary>
        /// Stops all currently playing "discreet" audio clips that were started with <see cref="PlayDiscreetAudioClip"/>.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the operation was successful.</returns>
        LckResult StopAllDiscreetAudio();
        
        /// <summary>
        /// Gets the full configuration descriptor for the currently active capture type.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object containing the <see cref="LckDescriptor"/> with the current settings.</returns>
        LckResult<LckDescriptor> GetDescriptor();
        
        /// <summary>
        /// Captures a single still photo using the current camera and settings.
        /// The result is returned via the <see cref="OnPhotoSaved"/> event.
        /// </summary>
        /// <returns>A <see cref="LckResult"/> object indicating whether the capture command was successfully initiated.</returns>
        LckResult CapturePhoto();
    }
}
