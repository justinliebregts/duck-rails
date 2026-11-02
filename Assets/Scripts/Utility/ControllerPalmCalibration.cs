using Game.Animation;
using UnityEngine;

/// <summary>Shared VR controller-to-palm mapping. Raw tracking/locomotion anchors are never modified.</summary>
public sealed class ControllerPalmCalibration
{
    // Metres in each controller's local frame; both hands use the same Y offset.
    public static readonly Vector3 ControllerLocalOffset = new Vector3(0f, -.028f, 0f);
    readonly LocalBodyDriver owner;
    Transform left, right;
    bool Applicable => owner != null && owner.Body != null && owner.Body.TracksPalms
        && owner.controllerInput != null && !owner.controllerInput.mouseGrip;

    public ControllerPalmCalibration(LocalBodyDriver driver) => owner = driver;

    public Transform Source(bool isLeft)
    {
        Transform raw = isLeft ? owner.LeftHandAnchor : owner.RightHandAnchor;
        if (raw == null) return null;
        // Once created, keep source identity stable for already-bound Photon/held-item consumers.
        Vector3 offset = Applicable ? ControllerLocalOffset : Vector3.zero;
        if (isLeft) return UpdateSource(ref left, raw, offset);
        return UpdateSource(ref right, raw, offset);
    }

    public void Refresh()
    {
        if (left != null) Source(true);
        if (right != null) Source(false);
    }

    Transform UpdateSource(ref Transform source, Transform raw, Vector3 value)
    {
        if (source == null) {
            // Desktop and NPC rigs never need an offset source of their own.
            if (!Applicable) return raw;
            source = new GameObject("Calibrated VR palm source").transform;
        }
        if (source.parent != raw) {
            source.SetParent(raw, false);
            source.localRotation = Quaternion.identity;
        }
        Vector3 scale = raw.lossyScale;
        Vector3 localPosition = new Vector3(scale.x != 0 ? value.x / scale.x : 0,
            scale.y != 0 ? value.y / scale.y : 0, scale.z != 0 ? value.z / scale.z : 0);
        if (source.localPosition != localPosition) source.localPosition = localPosition;
        return source;
    }

    public void Dispose()
    {
        DestroySource(left); DestroySource(right);
        left = right = null;
    }

    static void DestroySource(Transform source)
    {
        if (source == null) return;
        if (Application.isPlaying) Object.Destroy(source.gameObject);
        else Object.DestroyImmediate(source.gameObject);
    }
}
