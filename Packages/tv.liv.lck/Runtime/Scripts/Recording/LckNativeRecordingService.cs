using System;
using System.Runtime.InteropServices;
using Liv.Lck.Encoding;
using UnityEngine.Scripting;

namespace Liv.Lck.Recorder
{
    internal class LckNativeRecordingService : ILckNativeRecordingService
    {
        private const string RecordingLib = "qck";
        
        private IntPtr _nativeMuxerContext = IntPtr.Zero;
        private NGFX.LogLevel _logLevel =  NGFX.LogLevel.Error;
        
        #region NativeImports
        [DllImport(RecordingLib)]
        private static extern IntPtr GetMuxerCallbackFunction();
        
        [DllImport(RecordingLib)]
        private static extern IntPtr CreateMuxer();

        [DllImport(RecordingLib)]
        private static extern void DestroyMuxer(IntPtr muxerContext);
        
        [DllImport(RecordingLib)]
        private static extern bool StartMuxer(IntPtr muxerContext, ref MuxerConfig config);

        [DllImport(RecordingLib)]
        private static extern void StopMuxer(IntPtr muxerContext);
        
        [DllImport(RecordingLib)]
        private static extern void SetMuxerLogLevel(IntPtr muxerContext, uint level);
        #endregion

        [Preserve]
        public LckNativeRecordingService() {}

        public bool CreateNativeMuxer()
        {
            _nativeMuxerContext = CreateMuxer();
            if (!HasNativeMuxer())
                return false;

            UpdateNativeMuxerLogLevel();
            return true;
        }

        public void DestroyNativeMuxer()
        {
            if (!HasNativeMuxer())
                return;
            
            DestroyMuxer(_nativeMuxerContext);
            _nativeMuxerContext = IntPtr.Zero;
        }

        public bool HasNativeMuxer()
        {
            return _nativeMuxerContext != IntPtr.Zero;
        }

        public bool StartNativeMuxer(ref MuxerConfig config)
        {
            return StartMuxer(_nativeMuxerContext, ref config);
        }

        public bool StopNativeMuxer()
        {
            StopMuxer(_nativeMuxerContext);
            return true;
        }

        public void SetNativeMuxerLogLevel(NGFX.LogLevel logLevel)
        {
            _logLevel = logLevel;

            if (HasNativeMuxer())
                UpdateNativeMuxerLogLevel();
        }

        public LckEncodedPacketCallback GetMuxPacketCallback()
        {
            return new LckEncodedPacketCallback(_nativeMuxerContext, GetMuxerCallbackFunction());
        }

        private void UpdateNativeMuxerLogLevel()
        {
            SetMuxerLogLevel(_nativeMuxerContext, (uint)_logLevel);
        }
    }
}
