using System.Collections.Generic;
using System.Linq;
using Liv.Lck.Core;

namespace Liv.Lck.Telemetry
{
    /// <summary>
    /// Data structure representing a telemetry event
    /// </summary>
    public class LckTelemetryEvent
    {
        /// <summary>
        /// The type of telemetry event
        /// </summary>
        public LckTelemetryEventType EventType { get; set; } 
        
        /// <summary>
        /// A dictionary providing context for the telemetry event, where keys are <see cref="string"/>s and values can
        /// be any serializable object.
        /// </summary>
        public Dictionary<string, object> Context { get; set; }
        
        public LckTelemetryEvent(LckTelemetryEventType eventType)
        {
            EventType = eventType;
        }

        public LckTelemetryEvent(LckTelemetryEventType eventType, Dictionary<string, object> context)
        {
            EventType = eventType;
            Context = context;
        }

        public override string ToString()
        {
            var context = string.Join(", ", Context.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            return $"{nameof(EventType)}={EventType} | {nameof(Context)}={{{context}}}";
        }
    }
}
