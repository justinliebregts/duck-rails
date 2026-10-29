using System;

namespace Liv.Lck
{
    /// <summary>
    /// Service which captures video from the active <see cref="ILckCamera"/>
    /// </summary>
    internal interface ILckVideoCapturer : IDisposable
    {
        /// <summary>
        /// Whether to capture all frames (when <c>true</c>), or to aim to capture frames at the target output framerate
        /// (when <c>false</c>)
        /// </summary>
        bool ForceCaptureAllFrames { get; set; }
    
        /// <summary>
        /// Whether video frames are currently being captured from the active camera
        /// </summary>
        bool IsCapturing { get; }
    
        /// <summary>
        /// Start capturing video frames from the active camera
        /// </summary>
        void StartCapturing();

        /// <summary>
        /// Stop capturing video frames from the active camera
        /// </summary>
        void StopCapturing();

        /// <summary>
        /// Check whether the current frame has been captured
        /// </summary>
        /// <returns></returns>
        bool HasCurrentFrameBeenCaptured();
    }
}