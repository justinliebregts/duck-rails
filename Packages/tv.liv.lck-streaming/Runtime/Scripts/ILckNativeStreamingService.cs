using Liv.Lck.Encoding;

namespace Liv.Lck.Streaming
{
    internal interface ILckNativeStreamingService
    {
        /// <summary>
        /// Creates a native streamer
        /// </summary>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool CreateNativeStreamer();
        
        /// <summary>
        /// Destroys the native streamer
        /// </summary>
        void DestroyNativeStreamer();
        
        /// <summary>
        /// Check whether a native streamer currently exists
        /// </summary>
        /// <returns><c>true</c> if a native streamer exists, <c>false</c> otherwise</returns>
        bool HasNativeStreamer();
        
        /// <summary>
        /// Starts the native streamer
        /// </summary>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool StartNativeStreamer(int width, int height);
        
        /// <summary>
        /// Stops the native streamer
        /// </summary>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool StopNativeStreamer();
        
        /// <summary>
        /// Sets the log level of the native streamer
        /// </summary>
        /// <param name="logLevel">The <see cref="LogLevel"/> that the native streamer should use</param>
        void SetNativeStreamerLogLevel(NGFX.LogLevel logLevel);

        /// <summary>
        /// Gets an <see cref="LckEncodedPacketCallback"/> for streaming an encoded packet 
        /// </summary>
        /// <returns>The <see cref="LckEncodedPacketCallback"/> for streaming an encoded packet</returns>
        LckEncodedPacketCallback GetStreamPacketCallback();
    }
}
