using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Liv.Lck.Collections;

namespace Liv.Lck.Encoding
{
    internal interface ILckEncoder : IDisposable
    {
        /// <summary>
        /// Checks whether the encoder is currently active or not
        /// </summary>
        /// <returns><c>true</c> when the encoder is active, <c>false</c> otherwise</returns>
        /// <seealso cref="StartEncoding"/>
        /// <seealso cref="StopEncodingAsync"/>
        public bool IsActive();

        /// <summary>
        /// Checks whether the encoder is paused or not
        /// </summary>
        /// <returns><c>true</c> when the encoder is paused, <c>false</c> otherwise</returns>
        public bool IsPaused();

        /// <summary>
        /// Start encoding with the given <see cref="LckEncodedPacketHandler"/>s
        /// </summary>
        /// <param name="cameraTrackDescriptor">The <see cref="CameraTrackDescriptor"/> to start encoding with</param>
        /// <param name="encodedPacketHandlers">
        /// The collection of <see cref="LckEncodedPacketHandler"/>s that will be used to process encoded packets
        /// </param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        public LckResult StartEncoding(CameraTrackDescriptor cameraTrackDescriptor, 
            IEnumerable<LckEncodedPacketHandler> encodedPacketHandlers);
        
        /// <summary>
        /// Stops encoding
        /// </summary>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        public Task<LckResult> StopEncodingAsync();

        /// <summary>
        /// Encode a frame
        /// </summary>
        /// <param name="videoTimeSeconds">The timestamp of the frame in seconds</param>
        /// <param name="audioData">The audio data to encode</param>
        /// <param name="encodeVideo">Whether to encode video data or not</param>
        /// <returns>A <c>bool</c> indicating success (<c>true</c>) / failure (<c>false</c>)</returns>
        public bool EncodeFrame(float videoTimeSeconds, AudioBuffer audioData, bool encodeVideo);
        
        /// <summary>
        /// Set the encoder log level
        /// </summary>
        /// <param name="logLevel">The new log level</param>
        public void SetLogLevel(NGFX.LogLevel logLevel);

        /// <summary>
        /// Get a data structure with information about the current encoding session
        /// </summary>
        /// <returns>
        /// <see cref="EncoderSessionData"/> containing information about the current encoding session
        /// </returns>
        public EncoderSessionData GetCurrentSessionData();
    }
}
