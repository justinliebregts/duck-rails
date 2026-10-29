using System;

namespace Liv.Lck.Encoding
{
    /// <summary>
    /// Data structure defining a callback which should be invoked when a packet is encoded
    /// </summary>
    internal struct LckEncodedPacketCallback
    {
        /// <summary>
        /// Pointer to the object that the callback function belongs to
        /// </summary>
        public IntPtr CallbackObjectPtr { get; set; }

        /// <summary>
        /// Pointer to the callback function to invoke when a packet is encoded
        /// </summary>
        public IntPtr CallbackFunctionPtr { get; set; }

        /// <summary>
        /// Whether the <see cref="LckEncodedPacketCallback"/> is valid
        /// </summary>
        public bool IsValid => CallbackObjectPtr != IntPtr.Zero && CallbackFunctionPtr != IntPtr.Zero;

        public LckEncodedPacketCallback(IntPtr callbackObjectPtr, IntPtr callbackFunctionPtr)
        {
            CallbackObjectPtr = callbackObjectPtr;
            CallbackFunctionPtr = callbackFunctionPtr;
        }
    }
}