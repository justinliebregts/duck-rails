using System;
using UnityEngine.Scripting;

namespace Liv.Lck.Streaming
{
    internal class NullLckStreamer : ILckStreamer
    {
        public LckResult<bool> IsPaused()
        {
            return LckResult<bool>.NewError(LckError.StreamerNotImplemented, "LCK: No valid streamer module has been loaded. Ensure tv.liv.lck-streaming is installed.");
        }

        public LckCaptureState CurrentCaptureState => LckCaptureState.Idle;

        [Preserve]
        public NullLckStreamer()
        {
        }

        public void Dispose()
        {
        }

        public bool IsStreaming => false;
        public LckResult StartStreaming()
        {
            return LckResult.NewError(LckError.StreamerNotImplemented, "LCK: No valid streamer module has been loaded. Ensure tv.liv.lck-streaming is installed.");
        }

        public LckResult StopStreaming(LckService.StopReason stopReason)
        {
            return LckResult.NewError(LckError.StreamerNotImplemented, "LCK: No valid streamer module has been loaded. Ensure tv.liv.lck-streaming is installed.");
        }

        public LckResult<TimeSpan> GetStreamDuration()
        {
            return LckResult<TimeSpan>.NewError(LckError.StreamerNotImplemented, "LCK: No valid streamer module has been loaded. Ensure tv.liv.lck-streaming is installed.");
        }

        public void SetLogLevel(NGFX.LogLevel logLevel)
        {
        }
    }
}