using Liv.Lck.Encoding;

namespace Liv.Lck.Recorder
{
    internal interface ILckNativeRecordingService
    {
        /// <summary>
        /// Creates a native muxer
        /// </summary>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool CreateNativeMuxer();

        /// <summary>
        /// Destroys the native muxer
        /// </summary>
        void DestroyNativeMuxer();

        /// <summary>
        /// Check whether a native muxer currently exists
        /// </summary>
        /// <returns><c>true</c> if a native muxer exists, <c>false</c> otherwise</returns>
        bool HasNativeMuxer();

        /// <summary>
        /// Starts the native muxer
        /// </summary>
        /// <param name="config">The <see cref="MuxerConfig"/> to use to configure the native muxer</param>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool StartNativeMuxer(ref MuxerConfig config);
        
        /// <summary>
        /// Stops the native muxer
        /// </summary>
        /// <returns><c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        bool StopNativeMuxer();
        
        /// <summary>
        /// Sets the log level of the native muxer
        /// </summary>
        /// <param name="logLevel">The <see cref="LogLevel"/> that the native muxer should use</param>
        void SetNativeMuxerLogLevel(NGFX.LogLevel logLevel);

        /// <summary>
        /// Gets an <see cref="LckEncodedPacketCallback"/> for muxing an encoded packet 
        /// </summary>
        /// <returns>The <see cref="LckEncodedPacketCallback"/> for muxing an encoded packet</returns>
        LckEncodedPacketCallback GetMuxPacketCallback();
    }
}
