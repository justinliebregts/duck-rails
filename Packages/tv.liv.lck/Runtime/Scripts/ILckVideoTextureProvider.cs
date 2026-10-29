using UnityEngine;

/// <summary>
/// Interface providing video track texture references
/// </summary>
internal interface ILckVideoTextureProvider
{
    /// <summary>
    /// The <see cref="RenderTexture"/> of the camera track
    /// </summary>
    RenderTexture CameraTrackTexture { get; }
}
