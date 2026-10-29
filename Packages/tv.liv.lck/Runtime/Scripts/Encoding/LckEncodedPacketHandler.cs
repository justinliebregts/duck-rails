namespace Liv.Lck.Encoding
{
    /// <summary>
    /// Data structure defining an encoded packet handler which invokes some callback when packets are encoded
    /// </summary>
    internal struct LckEncodedPacketHandler
    {
        /// <summary>
        /// The <see cref="ILckCaptureStateProvider"/> of the encoded packet handler
        /// </summary>
        public ILckCaptureStateProvider CaptureStateProvider { get; }
        
        /// <summary>
        /// The <see cref="LckEncodedPacketCallback"/> to invoke when a packet is encoded
        /// </summary>
        public LckEncodedPacketCallback EncodedPacketCallback { get; }

        public LckEncodedPacketHandler(ILckCaptureStateProvider captureStateProvider,
            LckEncodedPacketCallback encodedPacketCallback)
        {
            CaptureStateProvider = captureStateProvider;
            EncodedPacketCallback = encodedPacketCallback;
        }
    }
}
