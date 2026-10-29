using System;

namespace Liv.Lck
{
    internal interface ILckPreviewer : IDisposable
    {
        /// <summary>
        /// If the preview in the viewfinder is actively capturing.
        /// Set to 'false' to disable the active camera, minimising performance impact
        /// on enabled but inactive recording devices.
        /// </summary>
        public bool IsPreviewActive { get; set; }
    }
}
