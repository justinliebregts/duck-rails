using System;

namespace Liv.Lck.Streaming
{
    internal interface ILckStreamer : ILckCaptureStateProvider, IDisposable
    {
        /// <summary>
        /// Gets whether streaming is currently active
        /// </summary>
        public bool IsStreaming { get; }

        /// <summary>
        /// Starts streaming
        /// </summary>
        /// <returns>True if streaming started successfully</returns>
        public LckResult StartStreaming();
    
        /// <summary>
        /// Stops streaming
        /// </summary>
        public LckResult StopStreaming(LckService.StopReason stopReason);
        
        /// <summary>
        /// Gets the current stream duration
        /// </summary>
        /// <returns>The current stream duration as a <see cref="TimeSpan"/></returns>
        public LckResult<TimeSpan> GetStreamDuration();

        /// <summary>
        /// Sets the log level for the streamer
        /// </summary>
        /// <param name="logLevel">The new <see cref="NGFX.LogLevel"/> to use</param>
        public void SetLogLevel(NGFX.LogLevel logLevel);
    }
}
