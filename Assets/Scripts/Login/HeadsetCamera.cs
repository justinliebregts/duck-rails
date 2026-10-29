using UnityEngine;
using UnityEngine.XR;

public class HeadsetCamera : MonoBehaviour
{
    void OnEnable()
    {
        Application.onBeforeRender += ApplyHeadPose;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyHeadPose;
    }

    void Update()
    {
        ApplyHeadPose();
    }

    void ApplyHeadPose()
    {
        if (!OVRManager.OVRManagerinitialized || !OVRNodeStateProperties.IsHmdPresent())
            return;

        if (OVRNodeStateProperties.GetNodeStatePropertyVector3(
                XRNode.CenterEye,
                NodeStatePropertyType.Position,
                OVRPlugin.Node.EyeCenter,
                OVRPlugin.Step.Render,
                out Vector3 position))
        {
            transform.localPosition = position;
        }

        if (OVRNodeStateProperties.GetNodeStatePropertyQuaternion(
                XRNode.CenterEye,
                NodeStatePropertyType.Orientation,
                OVRPlugin.Node.EyeCenter,
                OVRPlugin.Step.Render,
                out Quaternion rotation))
        {
            transform.localRotation = rotation;
        }
    }
}
