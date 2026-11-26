using Liv.Lck;
using Liv.Lck.DependencyInjection;
using Liv.Lck.Tablet;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
    // Only the owner's real tablet registers here. Remote cameras are visual-only.
    public static TabletGrab Local { get; private set; }
    public bool IsVisible { get; private set; } = true;

    void OnEnable() => Local = this;
    void OnDisable() { if (Local == this) Local = null; }

    [SerializeField] BoxCollider grabCollider;

    Grabbable grabbable;
    public Grabbable GrabSource => grabbable;
    LckTabletFollow follow;
    [InjectLck] ILckService lckService;
    Renderer[] visualRenderers;
    Canvas[] canvases;
    bool[] rendererHiddenStates, canvasEnabledStates;
    Rigidbody body;
    XRGrabInteractable xrGrab;
    bool grabEnabled, xrGrabEnabled, detectCollisions;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        follow = GetComponent<LckTabletFollow>();
        body = GetComponent<Rigidbody>();
        xrGrab = GetComponent<XRGrabInteractable>();
        visualRenderers = GetComponentsInChildren<Renderer>(true);
        canvases = GetComponentsInChildren<Canvas>(true);
        rendererHiddenStates = new bool[visualRenderers.Length];
        canvasEnabledStates = new bool[canvases.Length];
        FitGrabCollider();
        grabbable.OnGrabbed += OnGrabbed;
        grabbable.OnReleased += OnReleased;
    }

    void Start() => lckService?.SetPreviewActive(IsVisible);

    // Visibility belongs to the tablet, not the capture rig. Keep LIV's cameras,
    // tracking and UI subscriptions alive throughout a recording (including startup).
    public void SetVisible(bool visible)
    {
        if (IsVisible == visible) return;
        if (!visible && grabbable.IsHeld && grabbable.Holder != null)
            grabbable.Holder.Release();

        IsVisible = visible;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] == null) continue;
            if (!visible) rendererHiddenStates[i] = visualRenderers[i].forceRenderingOff;
            visualRenderers[i].forceRenderingOff = !visible || rendererHiddenStates[i];
        }
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null) continue;
            if (!visible) canvasEnabledStates[i] = canvases[i].enabled;
            canvases[i].enabled = visible && canvasEnabledStates[i];
        }

        if (!visible)
        {
            grabEnabled = grabbable.enabled;
            xrGrabEnabled = xrGrab != null && xrGrab.enabled;
            detectCollisions = body.detectCollisions;
            // Disabling the body removes all its colliders from physics, including
            // button triggers. LIV can still update their enabled state while hidden.
            LCKCameraController.ColliderButtonsInUse = false;
        }
        grabbable.enabled = visible && grabEnabled;
        if (xrGrab != null) xrGrab.enabled = visible && xrGrabEnabled;
        body.detectCollisions = visible && detectCollisions;

        // Hidden idle tablets need no preview render. LIV keeps capturing whenever
        // its encoder is active, independently of this preview setting.
        lckService?.SetPreviewActive(visible);
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
