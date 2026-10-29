using System;

namespace Liv.Lck
{
    /// <summary>
    /// Interface defining the API of a video mixer service
    /// </summary>
    internal interface ILckVideoMixer : ILckVideoTextureProvider, ILckActiveCameraConfigurer, IDisposable
    {
        
    }
}
