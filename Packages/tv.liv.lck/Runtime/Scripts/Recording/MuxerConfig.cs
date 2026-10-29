using System;
using System.Runtime.InteropServices;

namespace Liv.Lck.Recorder
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct MuxerConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string outputPath;
        
        public UInt32 videoBitrate;
        public UInt32 audioBitrate;
        public UInt32 width;
        public UInt32 height;
        public UInt32 framerate;
        public UInt32 samplerate;
        public UInt32 channels;
        public UInt32 numberOfTracks;
            
        [MarshalAs(UnmanagedType.I1)]
        public bool realtimeOutput;
    }
}