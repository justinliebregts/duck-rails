using System;
using System.Runtime.InteropServices;
using Liv.Lck.Recorder;

namespace Liv.Lck.Echo
{
    internal static class LckNativeEchoApi
    {
        private const string EncodingLib = "lck_rs";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void EchoCompletionCallback(
            uint status,
            [MarshalAs(UnmanagedType.LPStr)] string outputPath);

        [DllImport(EncodingLib)]
        internal static extern IntPtr CreateEchoMemoryBuffer();

        [DllImport(EncodingLib)]
        internal static extern IntPtr CreateEchoDiskBuffer(
            [MarshalAs(UnmanagedType.LPStr)] string storageDir);

        [DllImport(EncodingLib)]
        internal static extern void DestroyEchoBuffer(IntPtr echoBufferContext);

        [DllImport(EncodingLib)]
        internal static extern void SetEchoBufferEnabled(IntPtr echoBufferContext, bool enabled);

        [DllImport(EncodingLib)]
        internal static extern bool IsEchoBufferEnabled(IntPtr echoBufferContext);

        [DllImport(EncodingLib)]
        internal static extern void SetEchoMuxerConfig(IntPtr echoBufferContext, ref MuxerConfig config);

        [DllImport(EncodingLib)]
        internal static extern bool TriggerEchoSave(IntPtr echoBufferContext,
            [MarshalAs(UnmanagedType.LPStr)] string outputPath);

        [DllImport(EncodingLib)]
        internal static extern IntPtr GetEchoCallbackFunction();

        [DllImport(EncodingLib)]
        internal static extern void SetEchoCompletionCallback(IntPtr echoBufferContext,
            EchoCompletionCallback callback);

        [DllImport(EncodingLib)]
        internal static extern ulong GetEchoBufferDurationUs(IntPtr echoBufferContext);

        [DllImport(EncodingLib)]
        internal static extern ulong GetEchoBufferDataSizeBytes(IntPtr echoBufferContext);

        [DllImport(EncodingLib)]
        internal static extern void ClearEchoBuffer(IntPtr echoBufferContext);

        [DllImport(EncodingLib)]
        internal static extern ulong GetEchoBufferMaxDuration(IntPtr echoBufferContext);
    }
}
