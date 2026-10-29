using Liv.Lck;
using UnityEngine;

/// <summary>
/// Holds the camera tablet without using it for locomotion. The solid grab shape
/// turns off while held so the finger buttons still receive triggers. Releasing
/// leaves the tablet where it is, and follow can resume from that pose.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Grabbable))]
[RequireComponent(typeof(Rigidbody))]
public class TabletGrab : MonoBehaviour
{
    [SerializeField] BoxCollider grabCollider;

    Grabbable grabbable;
    LckTabletFollow follow;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        follow = GetComponent<LckTabletFollow>();
        FitGrabCollider();
        grabbable.OnGrabbed += OnGrabbed;
        grabbable.OnReleased += OnReleased;
    }

    void OnDestroy()
    {
        if (grabbable == null)
            return;
        grabbable.OnGrabbed -= OnGrabbed;
        grabbable.OnReleased -= OnReleased;
    }

    void OnGrabbed(PlayerHand hand)
    {
        if (follow != null)
            follow.SetHeld(true);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
            body.detectCollisions = true;
        if (grabCollider != null)
            grabCollider.enabled = false;
    }

    public void StopFollowMotion()
    {
        if (follow != null)
            follow.SetHeld(false);
    }

    void OnReleased(PlayerHand hand)
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        if (grabCollider != null)
            grabCollider.enabled = true;
        if (follow != null)
            follow.SetHeld(false);
    }

    void FitGrabCollider()
    {
        if (grabCollider == null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds world = renderer.bounds;
            Vector3 extents = world.extents;
            Vector3 center = world.center;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 local = transform.InverseTransformPoint(worldCorner);
                if (!any)
                {
                    min = local;
                    max = local;
                    any = true;
                }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        if (!any)
            return;

        grabCollider.center = (min + max) * 0.5f;
        grabCollider.size = max - min;
    }
}
