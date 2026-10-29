using System;

namespace Liv.Lck.Recorder
{
    internal interface ILckRecorder : ILckCaptureStateProvider, IDisposable
    {
        /// <summary>
        /// Start recording
        /// </summary>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult StartRecording();
        
        /// <summary>
        /// Stop recording
        /// </summary>
        /// <param name="stopReason">The reason for stopping the recording</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult StopRecording(LckService.StopReason stopReason);

        /// <summary>
        /// Pauses the recording
        /// </summary>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult PauseRecording();
        
        /// <summary>
        /// Resumes the recording
        /// </summary>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult ResumeRecording();
        
        /// <summary>
        /// Gets the current recording duration
        /// </summary>
        /// <returns>
        /// The current recording duration <see cref="TimeSpan"/>, contained in an <see cref="LckResult{TimeSpan}"/>
        /// indicating success / failure
        /// </returns>
        public LckResult<TimeSpan> GetRecordingDuration();

        /// <summary>
        /// Checks whether recording is currently in progress
        /// </summary>
        /// <returns>
        /// A <c>bool</c> indicating whether recording is currently in progress or not, contained in an
        /// <see cref="LckResult{TimeSpan}"/> indicating success / failure
        /// </returns>
        public LckResult<bool> IsRecording();
        
        /// <summary>
        /// Set the recorder log level
        /// </summary>
        /// <param name="logLevel">The new log level</param>
        public void SetLogLevel(NGFX.LogLevel logLevel);
    }
}
