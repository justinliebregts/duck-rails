using System;
using System.Runtime.InteropServices;
using Liv.Lck.Encoding;

namespace Liv.Lck.Streaming
{
    internal class LckNativeStreamingService : ILckNativeStreamingService
    {
        private const string StreamingLib = "lck_streaming_rs";
        
        private IntPtr _streamerContext = IntPtr.Zero;
        private NGFX.LogLevel _logLevel =  NGFX.LogLevel.Error;
        
        #region NativeImports
        [DllImport(StreamingLib)]
        private static extern IntPtr CreateStreamer();

        [DllImport(StreamingLib)]
        private static extern void DestroyStreamer(IntPtr streamerContext);
        
        [DllImport(StreamingLib)]
        private static extern bool StartStreamer(IntPtr streamerContext, int width, int height);
        
        [DllImport(StreamingLib)]
        private static extern void StopStreamer(IntPtr streamerContext);
        
        [DllImport(StreamingLib)]
        private static extern void SetStreamerLogLevel(IntPtr streamerContext, uint level);
        
        [DllImport(StreamingLib)]
        private static extern IntPtr GetStreamerCallbackFunction();
        
        [DllImport(StreamingLib)]
        private static extern void SetPacketInterleaverEnabled(IntPtr streamerContext, bool enabled);
        #endregion
        
        public bool CreateNativeStreamer()
        {
            _streamerContext = CreateStreamer();
            if (!HasNativeStreamer())
                return false;

            UpdateNativeStreamerLogLevel();
            return true;
        }

        public void DestroyNativeStreamer()
        {
            if (!HasNativeStreamer())
                return;
            
            DestroyStreamer(_streamerContext);
            _streamerContext = IntPtr.Zero;
        }

        public bool HasNativeStreamer()
        {
            return _streamerContext != IntPtr.Zero;
        }

        public bool StartNativeStreamer(int width, int height)
        {
            return StartStreamer(_streamerContext, width, height);
        }

        public bool StopNativeStreamer()
        {
            StopStreamer(_streamerContext);
            return true;
        }

        public void SetNativeStreamerLogLevel(NGFX.LogLevel logLevel)
        {
            _logLevel = logLevel;

            if (HasNativeStreamer())
                UpdateNativeStreamerLogLevel();
        }

        public LckEncodedPacketCallback GetStreamPacketCallback()
        {
            return new LckEncodedPacketCallback(_streamerContext, GetStreamerCallbackFunction());
        }

        private void UpdateNativeStreamerLogLevel()
        {
            SetStreamerLogLevel(_streamerContext, (uint)_logLevel);
        }
    }
}
