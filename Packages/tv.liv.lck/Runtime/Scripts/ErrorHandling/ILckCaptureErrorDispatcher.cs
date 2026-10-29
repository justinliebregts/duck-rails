using System;

namespace Liv.Lck.ErrorHandling
{
    /// <summary>
    /// Service which dispatches <see cref="LckEvents.CaptureErrorEvent"/>s to be handled elsewhere.
    /// </summary>
    internal interface ILckCaptureErrorDispatcher : IDisposable
    {
        /// <summary>
        /// Pushes an <see cref="LckCaptureError"/> to be distributed via an <see cref="LckEvents.CaptureErrorEvent"/>
        /// </summary>
        /// <param name="error">The <see cref="LckCaptureError"/> that occurred</param>
        public void PushError(LckCaptureError error);
    }
}

