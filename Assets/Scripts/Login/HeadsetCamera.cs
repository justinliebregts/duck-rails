using UnityEngine;
using UnityEngine.XR;

public class HeadsetCamera : MonoBehaviour
{
    public event System.Action TrackedPoseApplied;

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

        bool hasPosition = OVRNodeStateProperties.GetNodeStatePropertyVector3(
                XRNode.CenterEye,
                NodeStatePropertyType.Position,
                OVRPlugin.Node.EyeCenter,
                OVRPlugin.Step.Render,
                out Vector3 position);
        if (hasPosition)
            transform.localPosition = position;

        bool hasRotation = OVRNodeStateProperties.GetNodeStatePropertyQuaternion(
                XRNode.CenterEye,
                NodeStatePropertyType.Orientation,
                OVRPlugin.Node.EyeCenter,
                OVRPlugin.Step.Render,
                out Quaternion rotation);
        if (hasRotation)
            transform.localRotation = rotation;

        // Notify after both pose components are applied, including a first pose
        // arriving just before render. Presence alone does not imply tracking.
        if (hasPosition && hasRotation
            && OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)
            && OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter))
            TrackedPoseApplied?.Invoke();
    }
}
