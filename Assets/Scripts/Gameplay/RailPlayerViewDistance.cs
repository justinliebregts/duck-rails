using UnityEngine;

/// <summary>Local XR/simulated player cameras follow expedition dawn. Other cameras stay independent.</summary>
[DisallowMultipleComponent]
public sealed class RailPlayerViewDistance : MonoBehaviour
{
    Camera[] cameras;

    void LateUpdate() => ApplyCurrentDistance();

    public void ApplyCurrentDistance()
    {
        if (cameras == null) cameras = GetComponentsInChildren<Camera>(true);
        float distance = RailAtmosphere.PlayerViewDistance;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit benchmark requests retain control of their experimental distance.
        if (RailQuestPerformance.FarClipOverride > 0) distance = RailQuestPerformance.FarClipOverride;
#endif
        foreach (var camera in cameras)
            if (camera && camera.stereoTargetEye != StereoTargetEyeMask.None)
                camera.farClipPlane = distance;
    }
}
