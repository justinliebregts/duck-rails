using System;
using System.Runtime.InteropServices;
using Liv.Lck.ErrorHandling;

namespace Liv.Lck.Encoding
{
    internal static class LckNativeEncodingApi
    {
        private const string EncodingLib = "qck";
        
        public enum TrackType : UInt32
        {
            Video,
            Audio,
            Metadata,
        };
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CaptureErrorCallback(
            CaptureErrorType errorType, 
            [MarshalAs(UnmanagedType.LPStr)] string errorMessage);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TrackInfo
        {
            public TrackType type;
            public UInt32 bitrate;
            public UInt32 width;
            public UInt32 height;
            public UInt32 framerate;
            public UInt32 samplerate;
            public UInt32 channels;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct FrameTexture
        {
            public UInt32 id;
            public UInt32 trackIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct AudioTrack
        {
            public UInt32 trackIndex;
            public UInt64 timestampSamples;
            public UInt32 dataSize;
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct FrameSubmission
        {
            public IntPtr encoderContext;
            public IntPtr textureIDs;
            public UInt32 textureIDsSize;
            public UInt64 videoTimestampMilli;

            public UInt32 audioTracksSize;
            public IntPtr audioTracks;

            public UInt32 readyFramesSize;
            public IntPtr readyFrames;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct ResourceData
        {
            public IntPtr encoderContext;
        }
        
        [DllImport(EncodingLib)]
        internal static extern IntPtr CreateEncoder();

        [DllImport(EncodingLib)]
        internal static extern void DestroyEncoder(IntPtr encoderContext);
        
        [DllImport(EncodingLib)]
        internal static extern bool StartEncoder(IntPtr encoderContext,
            [MarshalAs(UnmanagedType.LPArray)] TrackInfo[] tracks, uint tracksCount);

        [DllImport(EncodingLib)]
        internal static extern void StopEncoder(IntPtr encoderContext);
        
        [DllImport(EncodingLib)]
        internal static extern void AddEncoderPacketCallback(IntPtr encoderContext, IntPtr objectPtr, IntPtr functionPtr);

        [DllImport(EncodingLib)]
        internal static extern void RemoveEncoderPacketCallback(IntPtr encoderContext, IntPtr objectPtr, IntPtr functionPtr);
        
        [DllImport(EncodingLib)]
        internal static extern IntPtr GetResourceContext(IntPtr encoderContext);

        [DllImport(EncodingLib)]
        internal static extern IntPtr AllocateFrameSubmission(
            [MarshalAs(UnmanagedType.LPStruct)] FrameSubmission frame,
            [MarshalAs(UnmanagedType.LPArray)] AudioTrack[] audioTracks,
            [MarshalAs(UnmanagedType.LPArray)] bool[] readyFrames);
        
        [DllImport(EncodingLib)]
        internal static extern IntPtr GetPluginUpdateFunction();

        [DllImport(EncodingLib)]
        internal static extern IntPtr GetInitResourcesFunction();

        [DllImport(EncodingLib)]
        internal static extern IntPtr GetReleaseResourcesFunction();
        
        [DllImport(EncodingLib)]
        internal static extern UInt32 GetAudioTrackFrameSize(IntPtr encoderContext, UInt32 track_index);
        
        [DllImport(EncodingLib)]
        internal static extern void SetEncoderLogLevel(IntPtr encoderContext, uint level);
        
        [DllImport(EncodingLib)]
        internal static extern bool SetCaptureErrorCallback(IntPtr encoderContext, CaptureErrorCallback errorCallback);
        
        [DllImport(EncodingLib)]
        internal static extern void SetAllowBFrames(IntPtr encoderContext, bool allowBFrames);
    }
}
