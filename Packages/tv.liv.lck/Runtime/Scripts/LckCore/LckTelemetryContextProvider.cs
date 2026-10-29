using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Liv.Lck.Core.Serialization;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace Liv.Lck.Core
{
    [Preserve]
    internal class LckTelemetryContextProvider : ILckTelemetryContextProvider
    {
        private readonly ILckSerializer _serializer = new LckMsgPackSerializer();

        [Preserve]
        public LckTelemetryContextProvider() {}

        public void SetTelemetryContext(LckTelemetryContextType contextType, Dictionary<string, object> context)
        {
            if (context == null || !context.Any())
            {
                ClearTelemetryContext(contextType);
                return;
            }

            var serializedContextBytes = _serializer.Serialize(context);
            IntPtr unmanagedPtr = Marshal.AllocHGlobal(serializedContextBytes.Length);
            try
            {
                Marshal.Copy(serializedContextBytes, 0, unmanagedPtr, serializedContextBytes.Length);

                var returnCode = LckCoreTelemetryNative.set_telemetry_context_from_serialized_data(
                    contextType,
                    unmanagedPtr,
                    (UIntPtr)serializedContextBytes.Length,
                    _serializer.SerializationType
                );

                if (returnCode != TelemetryReturnCode.Ok)
                {
                    Debug.LogError($"Failed to set telemetry context (return code={returnCode})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to set telemetry context: {e}");
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedPtr);
            }
        }

        public void ClearTelemetryContext(LckTelemetryContextType contextType)
        {
            var returnCode = LckCoreTelemetryNative.clear_context(contextType);
            if (returnCode != TelemetryReturnCode.Ok)
            {
                Debug.LogError($"Failed to clear telemetry context (return code={returnCode})");
            }
        }
    }
}
