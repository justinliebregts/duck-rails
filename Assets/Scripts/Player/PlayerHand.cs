using Game.Animation;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Gets first refusal on grip presses/releases (e.g. the bag). Return true to consume the event.</summary>
public interface IHandGripHandler
{
    bool OnGripPressed(PlayerHand hand);
    bool OnGripReleased(PlayerHand hand);
}

/// <summary>Something held outside the Grabbable system (a bag) that pushes the player like a held item.</summary>
public interface IHandLocomotionShape
{
    /// <summary>World sample spheres while hand holds this. Returning false tells the hand it has let go.</summary>
    bool TryGetLocomotionSamples(PlayerHand hand, Vector3[] worldBuffer, out int count, out float radius);
}

[DefaultExecutionOrder(50)] // after LocalBodyDriver poses hand.L / hand.R
public class PlayerHand : MonoBehaviour
{
    const float MinSegmentDt = 1e-5f;

    public static IHandGripHandler GripHandler;

    public OVRInput.Controller controller;

    [SerializeField] ControllerInput input;
    [SerializeField] float grabRadius = 0.06f;
    [SerializeField] LayerMask grabMask = 1 << 8; // Grabbable
    [SerializeField] AudioClip grabHaptic;
    [SerializeField] Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.06f);
    [SerializeField] Vector3 holdLocalEuler = Vector3.zero;
    [SerializeField] int velocitySamples = 5;
    [SerializeField] float throwLinearScale = 1f;
    [SerializeField] float throwAngularScale = 1f;
    [SerializeField] float maxThrowSpeed = 12f;

    readonly Collider[] overlapBuffer = new Collider[8];
    readonly List<Collider> grabColliders = new List<Collider>(8);

    Transform holdPoint;
    bool holdPointOnSkin;
    Transform buttonTouchRoot;
    SphereCollider[] buttonTouchColliders;

    // DuckPlayer's finger colliders live on the posed skin, not under PlayerHand.
    public bool TouchesButton(Collider button)
    {
        var driver = GetComponentInParent<LocalBodyDriver>();
        var body = driver != null ? driver.Body : null;
        Transform skin = body == null ? null : IsControllerLTouch ? body.LeftHand : body.RightHand;
        if (skin == null) return false;
        if (skin != buttonTouchRoot)
        {
            buttonTouchRoot = skin;
            buttonTouchColliders = skin.GetComponentsInChildren<SphereCollider>(true);
        }
        foreach (var finger in buttonTouchColliders)
            if (ButtonContact(button, finger)) return true;
        return false;
    }

    public static bool ButtonContact(Collider button, SphereCollider finger)
    {
        if (button == null || !button.enabled || finger == null || !finger.enabled || !finger.gameObject.activeInHierarchy) return false;
        // Explicit poses follow this frame's animated fingers without relying on the
        // physics layer matrix, rigidbodies or a FixedUpdate trigger callback.
        return Physics.ComputePenetration(button, button.transform.position, button.transform.rotation,
            finger, finger.transform.position, finger.transform.rotation, out _, out _);
    }
    Grabbable held;
    IHandLocomotionShape heldShape;
    Vector3[] posSamples;
    Quaternion[] rotSamples;
    float[] timeSamples;
    int sampleWrite;
    int sampleFilled;

    public bool IsControllerLTouch => controller == OVRInput.Controller.LTouch;
    public bool IsControllerRTouch => controller == OVRInput.Controller.RTouch;
    public bool IsHolding => held != null;
    public bool IsDesktopInput => input != null && input.mouseGrip;
    public bool IsGripHeld => input != null && input.ReturnButtonPress(ButtonName.Grip, this).down;
    RailControl driveControl;
    public Grabbable Held => held;
    public Transform HoldPoint => holdPoint != null ? holdPoint : transform;
    public float GrabTolerance
    {
        get => grabRadius;
        set => grabRadius = Mathf.Max(0f, value);
    }
    public bool CanGrab => PhotonMasterHandoff.LocalAvailable && !RailExpedition.LocalDown && (GR.Player.instance == null || !GR.Player.instance.IsDead);

    /// <summary>True on the frame any of this hand's use buttons goes down: A, B or trigger on the right, X, Y or trigger on the left.</summary>
    public bool WasUsePressed
    {
        get
        {
            if (input == null) return false;
            return IsControllerLTouch
                ? input.xButton.wasPressed || input.yButton.wasPressed || input.leftTriggerButton.wasPressed
                : input.aButton.wasPressed || input.bButton.wasPressed || input.rightTriggerButton.wasPressed;
        }
    }

    /// <summary>True while any of this hand's use buttons is held: A, B or trigger on the right, X, Y or trigger on the left.</summary>
    public bool IsUseHeld
    {
        get
        {
            if (input == null) return false;
            return IsControllerLTouch
                ? input.xButton.down || input.yButton.down || input.leftTriggerButton.down
                : input.aButton.down || input.bButton.down || input.rightTriggerButton.down;
        }
    }

    /// <summary>True while this hand's index trigger is held.</summary>
    public bool IsTriggerHeld
    {
        get
        {
            if (input == null) return false;
            return IsControllerLTouch ? input.leftTriggerButton.down : input.rightTriggerButton.down;
        }
    }

    public bool TryGetLocomotionShape(Vector3[] worldBuffer, out int count, out float radius)
    {
        count = 0;
        radius = 0f;
        if (held != null && held.ProvidesLocomotion)
            return held.TryGetLocomotionSamples(worldBuffer, out count, out radius);
        if (heldShape == null) return false;
        if (heldShape.TryGetLocomotionSamples(this, worldBuffer, out count, out radius)) return true;
        heldShape = null;
        return false;
    }

    /// <summary>Starts pushing the player with shape, like a held locomotion item. It stops on its own once shape reports it isn't held.</summary>
    public void HoldShape(IHandLocomotionShape shape)
    {
        heldShape = shape;
        if (NewPlayer.instance != null)
            NewPlayer.instance.SnapHandLocomotionAnchor(IsControllerRTouch);
    }

    void Awake()
    {
        if (input == null)
            input = GetComponentInParent<ControllerInput>();

        EnsureHoldPoint();

        int n = Mathf.Max(2, velocitySamples);
        velocitySamples = n;
        posSamples = new Vector3[n];
        rotSamples = new Quaternion[n];
        timeSamples = new float[n];
    }

    void EnsureHoldPoint()
    {
        holdPoint = transform.Find("HoldPoint");
        if (holdPoint == null)
        {
            var go = new GameObject("HoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(transform, false);
        }

        holdPoint.localPosition = holdLocalPosition;
        holdPoint.localRotation = Quaternion.Euler(holdLocalEuler);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled && holdPoint == null)
            holdPoint = transform.Find("HoldPoint");
        if (holdPoint != null && holdPoint.parent == transform)
        {
            holdPoint.localPosition = holdLocalPosition;
            holdPoint.localRotation = Quaternion.Euler(holdLocalEuler);
        }
    }
#endif

    void LateUpdate()
    {
        if (RailWorld.Instance != null && (PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady)) { Release(); return; }
        if (!holdPointOnSkin)
            TryAttachHoldPointToSkin();

        if (!CanGrab)
        {
            Release();
            return;
        }

        // After OVRCameraRig updates anchors so throw samples match the held pose.
        if (held != null)
            SamplePose();

        if (input == null) return;

        ButtonStatus grip = input.ReturnButtonPress(ButtonName.Grip, this);
        if (driveControl != null)
        {
            if (!driveControl.HeldBy(this)) driveControl = null;
            else { if (!grip.down) { driveControl.ReleaseDrive(); driveControl = null; } return; }
        }
        if (held == null)
        {
            if (grip.wasPressed && (GripHandler == null || !GripHandler.OnGripPressed(this)))
            {
                driveControl = RailControl.TryGripDrive(this);
                if (driveControl == null) TryGrab();
            }
            else if (grip.wasReleased && GripHandler != null)
                GripHandler.OnGripReleased(this);
        }
        else if (grip.wasReleased)
        {
            if (GripHandler == null || !GripHandler.OnGripReleased(this))
                Release();
        }
    }

    /// <summary>Grabs a specific item regardless of distance. Returns false if the hand is busy or the grab was refused.</summary>
    public bool ForceGrab(Grabbable item)
    {
        if (!CanGrab || item == null || held != null || driveControl != null) return false;

        item.Grab(this);
        if (item.Holder != this) return false;

        held = item;
        sampleFilled = 0;
        sampleWrite = 0;

        if (held.ProvidesLocomotion && NewPlayer.instance != null)
            NewPlayer.instance.SnapHandLocomotionAnchor(IsControllerRTouch);

        if (grabHaptic != null && VibrationManager.instance != null)
            VibrationManager.instance.TriggerVibration(grabHaptic, this);
        return true;
    }

    /// <summary>Forgets the held item without releasing it; the caller takes over (e.g. stashing it in the bag).</summary>
    public Grabbable DetachHeld()
    {
        Grabbable item = held;
        held = null;
        sampleFilled = 0;
        sampleWrite = 0;
        return item;
    }

    public void Release()
    {
        if (driveControl != null) { driveControl.ReleaseDrive(); driveControl = null; }
        if (held == null) return;

        ComputeThrowVelocity(out Vector3 lin, out Vector3 ang);
        Grabbable item = held;
        held = null;
        sampleFilled = 0;
        sampleWrite = 0;
        item.Release(lin * throwLinearScale, ang * throwAngularScale);
    }

    /// <summary>Called by Grabbable when another hand takes ownership.</summary>
    public void NotifyStolen()
    {
        held = null;
        sampleFilled = 0;
        sampleWrite = 0;
    }

    void TryGrab()
    {
        Grabbable best = FindGrabCandidate();
        if (best != null) TryGrab(best);
    }

    // Event-only. Baked bounds also cover visible parts outside an item's physics boxes.
    public Grabbable FindGrabCandidate()
    {
        Vector3 handPos = HoldPoint.position;
        float radiusSq = grabRadius * grabRadius;
        Grabbable best = null;
        float bestDistSq = radiusSq;
        var items = Grabbable.SurfaceItems;
        for (int i = 0; i < items.Count; i++)
        {
            Grabbable item = items[i];
            if (item == null || (grabMask.value & (1 << item.gameObject.layer)) == 0) continue;
            if (!item.TryGetSurfaceDistance(handPos, bestDistSq, out float distanceSq)) continue;
            bestDistSq = distanceSq;
            best = item;
        }

        // Preserve collider-based interactions for UI/store displays without a baked item surface.
        int count = Physics.OverlapSphereNonAlloc(
            handPos, grabRadius, overlapBuffer, grabMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            Grabbable g = col.GetComponentInParent<Grabbable>();
            if (g == null || !g.CanBeGrabbed || g.GrabSurface != null) continue;

            float d = (col.ClosestPoint(handPos) - handPos).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = g;
            }
        }

        // Held bodies disable detectCollisions, so the overlap query misses them.
        ConsiderOtherHandHeld(handPos, ref best, ref bestDistSq);

        return best;
    }

    void ConsiderOtherHandHeld(Vector3 handPos, ref Grabbable best, ref float bestDistSq)
    {
        PlayerHand other = OtherHand();
        if (other == null) return;

        Grabbable heldItem = other.Held;
        if (heldItem == null || !heldItem.CanBeGrabbed || heldItem.GrabSurface != null) return;
        if (!TryGetGrabDistanceSq(heldItem, handPos, out float distanceSq)) return;
        if (distanceSq >= bestDistSq) return;

        bestDistSq = distanceSq;
        best = heldItem;
    }

    PlayerHand OtherHand()
    {
        GR.Player player = GR.Player.instance;
        if (player == null) return null;
        return IsControllerRTouch ? player.leftPlayerHand : player.rightPlayerHand;
    }

    bool TryGetGrabDistanceSq(Grabbable item, Vector3 handPos, out float distanceSq)
    {
        distanceSq = float.MaxValue;
        item.GetComponentsInChildren(grabColliders);
        float radiusSq = grabRadius * grabRadius;
        bool found = false;

        for (int i = 0; i < grabColliders.Count; i++)
        {
            Collider col = grabColliders[i];
            if (col == null || !col.enabled || col.isTrigger) continue;

            Vector3 closest = col.ClosestPoint(handPos);
            float d = (closest - handPos).sqrMagnitude;
            if (d < distanceSq)
            {
                distanceSq = d;
                found = true;
            }
        }

        return found && distanceSq <= radiusSq;
    }

    public bool TryGrab(Grabbable item)
    {
        if (!CanGrab || held != null || driveControl != null || item == null || !item.CanBeGrabbed)
            return false;

        item.Grab(this);
        if (item.Holder != this)
            return false;

        held = item;
        sampleFilled = 0;
        sampleWrite = 0;

        if (held.ProvidesLocomotion && NewPlayer.instance != null)
            NewPlayer.instance.SnapHandLocomotionAnchor(IsControllerRTouch);

        if (grabHaptic != null && VibrationManager.instance != null)
            VibrationManager.instance.TriggerVibration(grabHaptic, this);

        return true;
    }

    void TryAttachHoldPointToSkin()
    {
        LocalBodyDriver driver = GetComponentInParent<LocalBodyDriver>();
        RemotePlayerBody body = driver != null ? driver.Body : null;
        if (body == null || holdPoint == null)
            return;

        Transform skinHand = IsControllerLTouch ? body.LeftHand : body.RightHand;
        if (skinHand == null)
            return;

        Quaternion worldRot = transform.rotation * Quaternion.Euler(holdLocalEuler);
        Vector3 worldOffset = transform.rotation * holdLocalPosition;

        holdPoint.SetParent(skinHand, false);
        holdPoint.localRotation = Quaternion.Inverse(skinHand.rotation) * worldRot;
        holdPoint.localPosition = skinHand.InverseTransformVector(worldOffset);
        Vector3 scale = skinHand.lossyScale;
        holdPoint.localScale = new Vector3(
            scale.x != 0f ? 1f / scale.x : 1f,
            scale.y != 0f ? 1f / scale.y : 1f,
            scale.z != 0f ? 1f / scale.z : 1f);
        holdPointOnSkin = true;
    }

    void SamplePose()
    {
        Transform t = HoldPoint;
        int i = sampleWrite;
        posSamples[i] = t.position;
        rotSamples[i] = t.rotation;
        timeSamples[i] = Time.time;
        sampleWrite = (sampleWrite + 1) % velocitySamples;
        if (sampleFilled < velocitySamples)
            sampleFilled++;
    }

    void ComputeThrowVelocity(out Vector3 linear, out Vector3 angular)
    {
        linear = Vector3.zero;
        angular = Vector3.zero;

        if (sampleFilled < 2) return;

        int segments = 0;
        Vector3 linSum = Vector3.zero;
        Vector3 angSum = Vector3.zero;

        int start = (sampleWrite - sampleFilled + velocitySamples) % velocitySamples;
        for (int s = 1; s < sampleFilled; s++)
        {
            int prev = (start + s - 1) % velocitySamples;
            int curr = (start + s) % velocitySamples;
            float dt = timeSamples[curr] - timeSamples[prev];
            if (dt < MinSegmentDt) continue;

            linSum += (posSamples[curr] - posSamples[prev]) / dt;

            // World-space rotation delta from hand transform
            Quaternion delta = rotSamples[curr] * Quaternion.Inverse(rotSamples[prev]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsInfinity(axis.x) || axis.sqrMagnitude < 1e-8f)
                continue;
            if (angle > 180f) angle -= 360f;
            angSum += axis.normalized * (angle * Mathf.Deg2Rad / dt);
            segments++;
        }

        if (segments == 0) return;

        float inv = 1f / segments;
        linear = linSum * inv;
        angular = angSum * inv;

        float speed = linear.magnitude;
        if (speed > maxThrowSpeed && speed > 0f)
            linear *= maxThrowSpeed / speed;
    }
}
