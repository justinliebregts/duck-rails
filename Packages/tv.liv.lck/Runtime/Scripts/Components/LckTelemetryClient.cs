using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Liv.Lck.Core;
using Liv.Lck.Core.Serialization;
using UnityEngine.Scripting;

namespace Liv.Lck.Telemetry
{
    internal class LckTelemetryClient : ILckTelemetryClient
    {
        private readonly ILckSerializer _serializer;
        
        [Preserve]
        public LckTelemetryClient(ILckSerializer serializer)
        {
            _serializer = serializer;
        }

        public void SendTelemetry(LckTelemetryEvent lckTelemetryEvent)
        {
            if (Application.isEditor)
            {
                // Skip sending telemetry events in editor
                LckLog.LogTrace($"Telemetry event sent in editor: {lckTelemetryEvent}");
                return;
            }

            SerializeAndSend(lckTelemetryEvent);
        }

        private void SerializeAndSend(LckTelemetryEvent lckTelemetryEvent)
        {
            var serializedContextBytes = _serializer.Serialize(lckTelemetryEvent.Context);

            IntPtr serializedContextPtr = Marshal.AllocHGlobal(serializedContextBytes.Length);
            try
            {
                Marshal.Copy(serializedContextBytes, 0, serializedContextPtr, serializedContextBytes.Length);

                var returnCode = LckCoreTelemetryNative.send_telemetry_event_with_context(
                    lckTelemetryEvent.EventType,
                    serializedContextPtr,
                    (UIntPtr)serializedContextBytes.Length,
                    _serializer.SerializationType
                );

                if (returnCode != TelemetryReturnCode.Ok)
                {
                    LckLog.LogError($"Failed to send telemetry event: {lckTelemetryEvent.EventType} (return code={returnCode})");
                }
            }
            catch (Exception e)
            {
                LckLog.LogError($"Failed to send telemetry event: {lckTelemetryEvent.EventType}. Exception: {e}");
            }
            finally
            {
                Marshal.FreeHGlobal(serializedContextPtr);
            }
        }
    }
}
