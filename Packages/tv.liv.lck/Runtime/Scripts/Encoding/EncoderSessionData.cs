namespace Liv.Lck.Encoding
{
    /// <summary>
    /// Data representing an encoder session
    /// </summary>
    internal struct EncoderSessionData
    {
        /// <summary>
        /// The number of encoded video frames
        /// </summary>
        public ulong EncodedVideoFrames { get; set; }
        
        /// <summary>
        /// The number of encoded audio samples per channel
        /// </summary>
        public ulong EncodedAudioSamplesPerChannel { get; set; }
        
        /// <summary>
        /// The latest capture time in seconds
        /// </summary>
        public float CaptureTimeSeconds { get; set; }
    }
}
