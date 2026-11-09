using System.Collections.Generic;
using UnityEngine;

/// <summary>One physical poke across menu changes; desktop clicks do not use this gate.</summary>
public static class VRButtonPressLock
{
    static readonly List<Collider> fingers = new List<Collider>();
    static Vector3 center, right, up, front, half;
    static int pressedFrame = -1;
    const float Clearance = .03f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { fingers.Clear(); pressedFrame = -1; }

    public static bool Blocked
    {
        get {
            if (pressedFrame == Time.frameCount) return true;
            for (int i = fingers.Count - 1; i >= 0; i--) {
                var finger = fingers[i];
                if (finger == null || !finger.enabled || !finger.gameObject.activeInHierarchy || Clear(finger.bounds))
                    fingers.RemoveAt(i);
            }
            return fingers.Count > 0;
        }
    }

    static bool Clear(Bounds bounds)
    {
        Vector3 p = bounds.center - center;
        float Radius(Vector3 axis) => Vector3.Dot(new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)), bounds.extents);
        // Going through the back never rearms. Pull out in front or clear the sides.
        return Vector3.Dot(p, front) - Radius(front) > half.z + Clearance ||
            Mathf.Abs(Vector3.Dot(p, right)) - Radius(right) > half.x + Clearance ||
            Mathf.Abs(Vector3.Dot(p, up)) - Radius(up) > half.y + Clearance;
    }

    public static bool TryPress(BoxCollider button, Collider contact = null)
    {
        if (Blocked || button == null) return false;
        center = button.transform.TransformPoint(button.center);
        right = button.transform.right; up = button.transform.up; front = button.transform.forward;
        if (Camera.main != null && Vector3.Dot(Camera.main.transform.position - center, front) < 0) front = -front;
        Vector3 scale = button.transform.lossyScale;
        half = Vector3.Scale(button.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        fingers.Clear();
        if (contact != null) fingers.Add(contact);
        var player = GR.Player.instance;
        if (player != null) {
            player.leftPlayerHand?.CollectButtonContacts(button, fingers);
            player.rightPlayerHand?.CollectButtonContacts(button, fingers);
        }
        pressedFrame = Time.frameCount;
        return true;
    }
}
