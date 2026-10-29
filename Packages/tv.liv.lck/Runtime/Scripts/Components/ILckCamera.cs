using UnityEngine;

namespace Liv.Lck
{
    public interface ILckCamera
    {
        /// <summary>
        /// Unique identifier for this camera. Set to a GUID if none is provided.
        /// </summary>
        string CameraId { get; }
        /// <summary>
        /// An internal call to activate the camera for capturing.
        /// Should not be called directly. Instead, activate a camera via <see cref="LckService.SetActiveCamera"/>.
        /// </summary>
        /// <param name="renderTexture"></param>
        void ActivateCamera(RenderTexture renderTexture);
        /// <summary>
        /// An internal call to activate the camera for capturing.
        /// Should not be called directly. Instead, the active camera is deactivated via <see cref="LckService.SetActiveCamera"/>.
        /// </summary>
        /// <param name="renderTexture"></param>
        void DeactivateCamera();
        /// <summary>
        /// Gets the <see cref="Camera"/> component for the LckCamera.
        /// </summary>
        /// <returns></returns>
        Camera GetCameraComponent();
    }
}
