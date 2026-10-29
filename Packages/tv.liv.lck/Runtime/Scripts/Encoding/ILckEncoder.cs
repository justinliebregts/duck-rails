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
        public bool IsActive();

        /// <summary>
        /// Checks whether the encoder is paused or not
        /// </summary>
        /// <returns><c>true</c> when the encoder is paused, <c>false</c> otherwise</returns>
        public bool IsPaused();

        /// <summary>
        /// Acquire the encoder for a consumer. If the encoder is not yet running,
        /// starts it with the given descriptor. Adds the consumer's packet handlers.
        /// </summary>
        /// <param name="consumer">The consumer acquiring the encoder</param>
        /// <param name="descriptor">Track descriptor for encoding configuration</param>
        /// <param name="handlers">Packet handlers for this consumer</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        public LckResult AcquireEncoder(EncoderConsumer consumer, CameraTrackDescriptor descriptor,
            IEnumerable<LckEncodedPacketHandler> handlers);

        /// <summary>
        /// Release the encoder for a consumer. Removes the consumer's packet handlers.
        /// If this is the last consumer, stops the encoder.
        /// </summary>
        /// <param name="consumer">The consumer releasing the encoder</param>
        /// <param name="handlers">Packet handlers to remove for this consumer</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        public Task<LckResult> ReleaseEncoderAsync(EncoderConsumer consumer,
            IEnumerable<LckEncodedPacketHandler> handlers);

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
