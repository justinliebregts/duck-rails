using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Liv.Lck.Core
{
    public static class InteropUtilities
    {
        /// <summary>
        /// Allocates memory for a collection of strings from the unmanaged memory of the process
        /// <br/>
        /// <b>WARNING</b>: Must free the memory allocated for each string using <see cref="Marshal.FreeHGlobal"/>
        /// </summary>
        /// <param name="strings">The collection of strings to allocate memory for</param>
        /// <param name="targetEncoding">
        /// The target encoding of the strings which will be allocated in unmanaged memory
        /// </param>
        /// <returns>A collection of <see cref="IntPtr"/>s to the newly allocated strings in unmanaged memory</returns>
        public static IReadOnlyCollection<IntPtr> AllocateUnmanagedStringPointers(
            IEnumerable<string> strings, Encoding targetEncoding)
        {
            var stringPtrs = new List<IntPtr>();
            foreach (var str in strings)
            {
                var stringBytes = targetEncoding.GetBytes(str + "\0");
                IntPtr stringPtr = Marshal.AllocHGlobal(stringBytes.Length);
                Marshal.Copy(stringBytes, 0, stringPtr, stringBytes.Length);
                stringPtrs.Add(stringPtr);
            }
            return stringPtrs;
        }

        /// <summary>
        /// Allocates memory for an array of pointers from the unmanaged memory of the process
        /// <br/>
        /// <b>WARNING</b>: Must free the memory allocated for the array using <see cref="Marshal.FreeHGlobal"/>
        /// </summary>
        /// <param name="ptrs">The collection of <see cref="IntPtr"/>s to include in the newly allocated array</param>
        /// <returns>An <see cref="IntPtr"/> to the newly allocated array in unmanaged memory</returns>
        public static IntPtr AllocateUnmanagedArray(IReadOnlyCollection<IntPtr> ptrs)
        {
            IntPtr arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * ptrs.Count);
            for (var i = 0; i < ptrs.Count; i++)
            {
                Marshal.WriteIntPtr(arrayPtr, i * IntPtr.Size, ptrs.ElementAt(i));
            }
            return arrayPtr;
        }

        /// <summary>
        /// Copies an unmanaged byte array into a new managed one
        /// </summary>
        /// <param name="byteArrayStartPtr">Pointer to the start of the unmanaged byte array</param>
        /// <param name="byteArrayLength">The length of the unmanaged byte array</param>
        /// <returns>The copied managed byte array</returns>
        public static byte[] CopyUnmanagedByteArray(IntPtr byteArrayStartPtr, int byteArrayLength)
        {
            var byteArray = new byte[byteArrayLength];
            Marshal.Copy(byteArrayStartPtr, byteArray, 0, byteArrayLength);
            return byteArray;
        }
        
        /// <summary>
        /// Creates a <see cref="string"/> from an <see cref="IntPtr"/> to a UTF-8 encoded string in unmanaged memory
        /// </summary>
        /// <param name="ptr">Pointer to a UTF-8 encoded string</param>
        /// <returns>A <see cref="string"/> representation of the string at the pointer</returns>
        public static string UTF8PointerToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;

            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            
            return Encoding.UTF8.GetString(bytes);
        }
        
        /// <summary>
        /// Allocates unmanaged memory for a UTF-8 encoded string from a <see cref="string"/>, providing an
        /// <see cref="IntPtr"/> to it.
        /// </summary>
        /// <param name="str">The <see cref="string"/> to create a pointer to</param>
        /// <returns>An <see cref="IntPtr"/> to the UTF-8 string in unmanaged memory</returns>
        /// <remarks>
        /// IMPORTANT: The allocated memory must be freed using <see cref="Free"/> / <see cref="Marshal.FreeHGlobal"/>,
        /// providing the returned <see cref="IntPtr"/> as a parameter.
        /// </remarks>
        public static IntPtr StringToUTF8Pointer(string str)
        {
            if (str == null)
                return IntPtr.Zero;

            var bytes = Encoding.UTF8.GetBytes(str + "\0");
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }
        
        /// <summary>
        /// <inheritdoc cref="Marshal.FreeHGlobal"/>
        /// </summary>
        /// <param name="ptr">The pointer to the memory to be freed</param>
        public static void Free(IntPtr ptr) => Marshal.FreeHGlobal(ptr);
    }
}
