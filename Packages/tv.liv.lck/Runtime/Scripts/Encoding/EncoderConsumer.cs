namespace Liv.Lck.Encoding
{
    /// <summary>
    /// Identifies a consumer that can acquire and release the shared encoder.
    /// </summary>
    internal enum EncoderConsumer
    {
        Recording,
        Streaming,
        Echo
    }
}
