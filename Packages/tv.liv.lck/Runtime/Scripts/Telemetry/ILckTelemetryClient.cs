namespace Liv.Lck.Telemetry
{
    public interface ILckTelemetryClient
    {
        /// <summary>
        /// Send a <see cref="LckTelemetryEvent"/>
        /// </summary>
        /// <param name="lckTelemetryEvent">The <see cref="LckTelemetryEvent"/> to send</param>
        void SendTelemetry(LckTelemetryEvent lckTelemetryEvent);
    }
}
