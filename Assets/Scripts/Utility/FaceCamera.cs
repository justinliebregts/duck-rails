using UnityEngine;

/// <summary>
/// Turns this object so its forward axis points at the local player's camera.
/// </summary>
[DefaultExecutionOrder(10000)]
public class FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        var camera = Camera.main;
        if (camera == null)
            return;

        var towardCamera = camera.transform.position - transform.position;
        if (towardCamera.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(towardCamera);
    }
}
