using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    static readonly List<Grabbable> surfaceItems = new List<Grabbable>(64);
    public static IReadOnlyList<Grabbable> SurfaceItems => surfaceItems;
    [SerializeField, HideInInspector] GrabSurfaceData grabSurface;
    HeadEquipment grabEquipment;
    bool surfaceRegistered;
    const float GripContactTolerance = 0.001f;
    public GrabSurfaceData GrabSurface => grabSurface;
    public virtual bool CanSelectSurface => CanBeGrabbed && (grabEquipment == null || !grabEquipment.IsEquipped);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSurfaceRegistry()
    {
        foreach (Grabbable item in surfaceItems) if (item != null) item.surfaceRegistered = false;
        surfaceItems.Clear();
    }

    protected virtual void OnEnable()
    {
        if (surfaceRegistered || grabSurface == null || !grabSurface.IsValid) return;
        surfaceRegistered = true;
        surfaceItems.Add(this);
    }

    public bool TryGetSurfacePoint(Vector3 query, out Vector3 closest)
    {
        closest = default;
        return grabSurface != null && grabSurface.TryClosestPoint(transform.localToWorldMatrix, query, out closest);
    }

    public bool TryGetSurfaceDistance(Vector3 query, float maxDistanceSquared, out float distanceSquared)
    {
        distanceSquared = float.PositiveInfinity;
        if (!CanSelectSurface || grabSurface == null) return false;
        Matrix4x4 matrix = transform.localToWorldMatrix;
        if (!grabSurface.WithinReach(matrix, query, maxDistanceSquared) ||
            !grabSurface.TryClosestPoint(matrix, query, out Vector3 closest)) return false;
        distanceSquared = (closest - query).sqrMagnitude;
        return distanceSquared <= maxDistanceSquared;
    }

    public const int MaxLocomotionSamples = 24;
    public const int DefaultLocomotionSamples = 8;

    [SerializeField] bool canGrab = true;
    [Tooltip("Multiplies world-space horizontal release velocity. 1 preserves the original throw; vertical velocity and spin are unchanged.")]
    [Min(0f)] [SerializeField] float horizontalThrowMultiplier = 1f;
    [SerializeField] bool locomotionWhileHeld;
    [SerializeField] Collider locomotionCollider;
    [Tooltip("Optional rigid compound shape. Include the primary collider. Child poses are baked when samples rebuild; do not animate these colliders independently.")]
    [SerializeField] BoxCollider[] locomotionBoxes = Array.Empty<BoxCollider>();
    [Tooltip("Optional shared hit/locomotion centers in the primary collider's local space, for broad shapes. Overrides automatic box sampling.")]
    [SerializeField] Vector3[] locomotionSampleOffsets = Array.Empty<Vector3>();
    [Min(.01f)] [SerializeField] float locomotionSampleRadius = .05f;
    [SerializeField] bool drawLocomotionGizmos = true;
    [SerializeField] Color locomotionGizmoColor = new Color(0.2f, 0.85f, 1f, 0.35f);
    protected Rigidbody rb;
    ItemSounds sounds;
    PlayerHand holder;
    bool cachedDetectCollisions;
    RigidbodyInterpolation cachedInterpolation;

    const float PickupGrowthDuration = 0.15f;
    Vector3 pickupStartScale;
    Vector3 pickupFullScale;
    Vector3 pickupLocalGrip;
    double pickupStarted;
    bool pickupGrowing;
    protected virtual bool OwnsPickupPose => true;
    protected virtual double PickupTime => Time.timeAsDouble;
    protected bool PickupGrowing => pickupGrowing;
    // Offset from the full-size root pose, used by network serialization/reconstruction.
    protected Vector3 PickupOffset => pickupGrowing
        ? transform.rotation * Vector3.Scale(pickupFullScale - transform.lossyScale, pickupLocalGrip)
        : Vector3.zero;

    public void BeginPickupGrowth(Vector3 startScale, Vector3 fullScale, Vector3 localGrip, double started)
    {
        pickupStartScale = startScale;
        pickupFullScale = fullScale;
        pickupLocalGrip = localGrip;
        pickupStarted = started;
        pickupGrowing = true;
        ApplyPickupGrowth();
    }

    protected virtual void Update()
    {
        if (OwnsPickupPose && pickupGrowing)
            ApplyPickupGrowth();
    }

    protected void FinishPickupGrowth()
    {
        if (!pickupGrowing) return;
        pickupStarted = PickupTime - PickupGrowthDuration - 1d;
        ApplyPickupGrowth();
    }

    // Input is always the full-size root pose. Remote clients call this after reconstructing it.
    protected void ApplyPickupGrowth(bool fromFullPose = false)
    {
        if (!pickupGrowing) return;
        Vector3 fullPosition = transform.position - (fromFullPose ? Vector3.zero : PickupOffset);
        float t = Mathf.Clamp01((float)(PickupTime - pickupStarted) / PickupGrowthDuration);
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 scale = Vector3.Lerp(pickupStartScale, pickupFullScale, eased);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        transform.position = fullPosition + PickupOffset;
        if (t >= 1f)
        {
            pickupGrowing = false;
            RebuildLocomotionSamples();
            if (ProvidesLocomotion && NewPlayer.instance != null)
                NewPlayer.instance.SnapHandLocomotionAnchor(holder.IsControllerRTouch);
        }
    }

    Vector3[] localSampleOffsets = Array.Empty<Vector3>();
    int sampleCount;
    protected int SwingSampleRevision { get; private set; }
    protected Collider SwingSampleCollider => locomotionCollider != null && locomotionCollider.enabled && sampleCount > 0
        ? locomotionCollider : null;
    float locoRadius;

    bool stashed;
    Renderer[] stashRenderers;
    bool[] stashRendererStates;
    Collider[] stashColliders;
    bool[] stashColliderStates;

    public bool CanBeGrabbed => canGrab && isActiveAndEnabled && !stashed;
    public bool IsStashed => stashed;
    public virtual bool CanBeStashed => !stashed;
    public bool IsHeld => holder != null;
    public PlayerHand Holder => holder;
    public bool ProvidesLocomotion =>
        locomotionWhileHeld && locomotionCollider != null && sampleCount > 0 && IsHeld && !pickupGrowing;
    public float LocomotionRadius => locoRadius;
    public int LocomotionSampleCount => sampleCount;

    public event Action<PlayerHand> OnGrabbed;
    public event Action<PlayerHand> OnReleased;

    // Reconciliation is not a gameplay release: no throw, sound, or equipment events.
    public void CancelLocalHold()
    {
        PlayerHand hand = holder;
        holder = null;
        if (hand != null && hand.Held == this) hand.NotifyStolen();
        if (hand == null) return;
        if (gameObject.activeInHierarchy) transform.SetParent(null, true);
        if (rb != null)
        {
            rb.interpolation = cachedInterpolation;
            rb.detectCollisions = cachedDetectCollisions;
        }
    }

    protected virtual void OnDisable()
    {
        if (surfaceRegistered) { surfaceItems.Remove(this); surfaceRegistered = false; }
        CancelLocalHold();
    }

    /// <summary>Availability cleanup bypasses throw/equip/stash release events.</summary>
    public virtual void DropForUnavailablePlayer()
    {
        if (!IsHeld) return;
        FinishPickupGrowth();
        CancelLocalHold();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        PrepareDropImpact();
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sounds = GetComponent<ItemSounds>();
        grabEquipment = GetComponent<HeadEquipment>();
        RebuildLocomotionSamples();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RebuildLocomotionSamples();
    }

    void OnDrawGizmos()
    {
        if (!drawLocomotionGizmos || !locomotionWhileHeld || locomotionCollider == null)
            return;

        if (!Application.isPlaying || sampleCount <= 0 || localSampleOffsets == null || localSampleOffsets.Length != sampleCount)
            RebuildLocomotionSamples();

        if (sampleCount <= 0 || locoRadius <= 0f)
            return;

        Transform t = locomotionCollider.transform;
        Color fill = locomotionGizmoColor;
        Color wire = fill;
        wire.a = Mathf.Clamp01(fill.a + 0.45f);

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 world = t.TransformPoint(localSampleOffsets[i]);
            Gizmos.color = fill;
            Gizmos.DrawSphere(world, locoRadius);
            Gizmos.color = wire;
            Gizmos.DrawWireSphere(world, locoRadius);
        }
    }
#endif

    public bool TryGetLocomotionSamples(Vector3[] worldBuffer, out int count, out float radius)
    {
        count = 0;
        radius = locoRadius;
        if (!ProvidesLocomotion || worldBuffer == null || worldBuffer.Length == 0)
            return false;

        count = Mathf.Min(sampleCount, worldBuffer.Length);
        Transform t = locomotionCollider.transform;
        for (int i = 0; i < count; i++)
            worldBuffer[i] = t.TransformPoint(localSampleOffsets[i]);
        return true;
    }

    /// <summary>World sample positions along the collider. Valid while held or thrown. Returns 0 if this grabbable has no samples.</summary>
    public int CopySampleWorldPositions(Vector3[] worldBuffer)
    {
        if (worldBuffer == null || locomotionCollider == null || !locomotionCollider.enabled || sampleCount <= 0 || localSampleOffsets == null)
            return 0;

        int count = sampleCount < worldBuffer.Length ? sampleCount : worldBuffer.Length;
        Transform sampleTransform = locomotionCollider.transform;
        for (int i = 0; i < count; i++)
            worldBuffer[i] = sampleTransform.TransformPoint(localSampleOffsets[i]);
        return count;
    }

    // The caller already captured the collider matrix for idle detection. Reuse it
    // instead of crossing into Transform.TransformPoint for every weapon sample.
    protected int CopySwingSampleWorldPositions(Vector3[] worldBuffer, Matrix4x4 matrix)
    {
        int count = Mathf.Min(sampleCount, worldBuffer.Length);
        for (int i = 0; i < count; i++) worldBuffer[i] = matrix.MultiplyPoint3x4(localSampleOffsets[i]);
        return count;
    }

    void RebuildLocomotionSamples()
    {
        SwingSampleRevision++;
        sampleCount = 0;
        locoRadius = 0f;
        localSampleOffsets = Array.Empty<Vector3>();

        if (locomotionCollider == null || !locomotionCollider.enabled)
            return;

        if (locomotionSampleOffsets != null && locomotionSampleOffsets.Length > 0)
        {
            sampleCount = Mathf.Min(locomotionSampleOffsets.Length, MaxLocomotionSamples);
            localSampleOffsets = new Vector3[sampleCount];
            Array.Copy(locomotionSampleOffsets, localSampleOffsets, sampleCount);
            Vector3 scale = locomotionCollider.transform.lossyScale;
            locoRadius = Mathf.Max(.01f, locomotionSampleRadius) *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return;
        }

        localSampleOffsets = locomotionBoxes != null && locomotionBoxes.Length > 0
            ? LocomotionSamples.BuildCompound(locomotionBoxes, locomotionCollider.transform, out locoRadius)
            : LocomotionSamples.Build(locomotionCollider, out locoRadius);
        sampleCount = localSampleOffsets.Length;
    }

    public virtual void Grab(PlayerHand hand)
    {
        if (hand == null || !hand.CanGrab || !CanBeGrabbed) return;
        if (holder == hand) return;

        bool takingFromOtherHand = holder != null;
        if (takingFromOtherHand) FinishPickupGrowth();
        if (takingFromOtherHand)
            holder.NotifyStolen();
        else
        {
            cachedInterpolation = rb.interpolation;
            cachedDetectCollisions = rb.detectCollisions;
        }

        holder = hand;

        // Worn items and items taken from another hand are already kinematic.
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.detectCollisions = false;

        Transform hold = hand.HoldPoint;

        // Bag/store spawns already carry a corrected, networked growth anchor.
        // Ordinary grabs translate once, preserving orientation and any free grip location.
        if (!pickupGrowing && TryGetSurfacePoint(hold.position, out Vector3 surface))
        {
            Vector3 correction = hold.position - surface;
            if (correction.sqrMagnitude > GripContactTolerance * GripContactTolerance)
                transform.position += correction;
        }

        // Preserve the pose at pickup; the player's grip defines the offset.
        transform.SetParent(hold, true);
        if (sounds != null)
        {
            sounds.ResetImpactState();
            sounds.ResetSwingState();
            sounds.Play(ItemSound.Grab);
        }
        OnGrabbed?.Invoke(hand);
    }

    public virtual void Release(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (holder == null) return;

        float multiplier = Mathf.Max(0f, horizontalThrowMultiplier);
        linearVelocity.x *= multiplier;
        linearVelocity.z *= multiplier;

        FinishPickupGrowth();

        PlayerHand hand = holder;
        holder = null;

        transform.SetParent(null, true);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = cachedInterpolation;
        rb.detectCollisions = cachedDetectCollisions;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;
        }

        OnReleased?.Invoke(hand);
        if (sounds != null) sounds.PlayThrow(linearVelocity);
        PrepareDropImpact();
    }

    /// <summary>Suppress release-time player overlaps, including when worn equipment falls.</summary>
    public void PrepareDropImpact()
    {
        if (sounds != null && !IsHeld && !stashed && rb != null && !rb.isKinematic)
            sounds.PrepareRelease();
    }

    /// <summary>Hides the item inside a bag. It stays alive (destroying gold bars triggers respawns) and follows the anchor.</summary>
    public virtual void Stash(Transform anchor)
    {
        if (stashed) return;

        if (holder != null)
        {
            holder.NotifyStolen();
            Release(Vector3.zero, Vector3.zero);
        }

        stashed = true;
        if (sounds != null) sounds.ResetImpactState();
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;

        if (anchor != null)
        {
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
        }
        SetHidden(true);
    }

    /// <summary>Takes the item back out of a bag and places it at the given pose with physics restored.</summary>
    public virtual void Unstash(Vector3 position, Quaternion rotation)
    {
        if (!stashed) return;

        stashed = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.detectCollisions = true;
        SetHidden(false);
    }

    /// <summary>Applies the hidden look without touching ownership/parenting (used for remote copies).</summary>
    protected void SetHidden(bool hidden)
    {
        if (hidden)
        {
            stashRenderers = GetComponentsInChildren<Renderer>(true);
            stashRendererStates = new bool[stashRenderers.Length];
            for (int i = 0; i < stashRenderers.Length; i++)
            {
                stashRendererStates[i] = stashRenderers[i].enabled;
                stashRenderers[i].enabled = false;
            }

            stashColliders = GetComponentsInChildren<Collider>(true);
            stashColliderStates = new bool[stashColliders.Length];
            for (int i = 0; i < stashColliders.Length; i++)
            {
                stashColliderStates[i] = stashColliders[i].enabled;
                stashColliders[i].enabled = false;
            }
            return;
        }

        if (stashRenderers != null)
            for (int i = 0; i < stashRenderers.Length; i++)
                if (stashRenderers[i] != null)
                    stashRenderers[i].enabled = stashRendererStates[i];

        if (stashColliders != null)
            for (int i = 0; i < stashColliders.Length; i++)
                if (stashColliders[i] != null)
                    stashColliders[i].enabled = stashColliderStates[i];

        stashRenderers = null;
        stashColliders = null;
    }

    protected void SetStashedFlag(bool value) => stashed = value;
}

/// <summary>Spheres along a box or capsule collider's long axis. The locomotion sweeps them so a held thing pushes the player.</summary>
public static class LocomotionSamples
{
    // Keep the existing eight-point budget for both combat and held locomotion.
    // Offsets share the primary collider's space, preserving the cached matrix path.
    public static Vector3[] BuildCompound(BoxCollider[] boxes, Transform sampleSpace, out float radius)
    {
        radius = 0f;
        var starts = new System.Collections.Generic.List<Vector3>();
        var ends = new System.Collections.Generic.List<Vector3>();
        foreach (BoxCollider box in boxes)
        {
            if (box == null || !box.enabled || !box.gameObject.activeSelf) continue;
            if (starts.Count >= Grabbable.DefaultLocomotionSamples / 2)
                throw new ArgumentException("Compound locomotion supports at most four boxes.");
            Vector3 size = box.size;
            int axis = size.y >= size.x && size.y >= size.z ? 1 : size.z >= size.x && size.z >= size.y ? 2 : 0;
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            Vector3 scale = box.transform.lossyScale;
            float localRadius = Mathf.Min(size[a], size[b]) * .5f;
            radius = Mathf.Max(radius, localRadius * Mathf.Min(Mathf.Abs(scale[a]), Mathf.Abs(scale[b])));
            Vector3 offset = Vector3.zero;
            offset[axis] = Mathf.Max(0f, size[axis] * .5f - localRadius);
            starts.Add(box.transform.TransformPoint(box.center - offset));
            ends.Add(box.transform.TransformPoint(box.center + offset));
        }
        if (starts.Count == 0) return Array.Empty<Vector3>();
        var counts = new int[starts.Count];
        for (int i = 0; i < counts.Length; i++) counts[i] = 2;
        for (int remaining = Grabbable.DefaultLocomotionSamples - counts.Length * 2; remaining > 0; remaining--)
        {
            int widest = 0;
            for (int i = 1; i < counts.Length; i++)
                if (Vector3.Distance(starts[i], ends[i]) / (counts[i] - 1) > Vector3.Distance(starts[widest], ends[widest]) / (counts[widest] - 1)) widest = i;
            counts[widest]++;
        }
        var offsets = new Vector3[Grabbable.DefaultLocomotionSamples];
        int index = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            radius = Mathf.Max(radius, Vector3.Distance(starts[i], ends[i]) / (counts[i] - 1) * .5f);
            for (int j = 0; j < counts[i]; j++)
                offsets[index++] = sampleSpace.InverseTransformPoint(Vector3.Lerp(starts[i], ends[i], j / (float)(counts[i] - 1)));
        }
        radius = Mathf.Max(radius, .01f);
        return offsets;
    }

    /// <summary>Sample centers in the collider's local space, and the sphere radius in world units. Empty for other collider types.</summary>
    public static Vector3[] Build(Collider collider, out float worldRadius)
    {
        worldRadius = 0f;
        if (collider is BoxCollider box)
            return BuildBox(box, out worldRadius);
        if (collider is CapsuleCollider capsule)
            return BuildCapsule(capsule, out worldRadius);
        return Array.Empty<Vector3>();
    }

    static Vector3[] BuildBox(BoxCollider box, out float radius)
    {
        Vector3 size = box.size;
        Vector3 center = box.center;

        int axis = 0;
        if (size.y >= size.x && size.y >= size.z) axis = 1;
        else if (size.z >= size.x && size.z >= size.y) axis = 2;

        int axisA = (axis + 1) % 3;
        int axisB = (axis + 2) % 3;
        float cross = Mathf.Min(size[axisA], size[axisB]);
        float localRadius = cross * 0.5f;
        float halfLong = size[axis] * 0.5f;

        Vector3 lossy = box.transform.lossyScale;
        float worldRadius = localRadius * Mathf.Min(Mathf.Abs(lossy[axisA]), Mathf.Abs(lossy[axisB]));
        float longScale = Mathf.Abs(lossy[axis]);

        return BuildAxis(center, axis, halfLong, localRadius, worldRadius, longScale, out radius);
    }

    static Vector3[] BuildCapsule(CapsuleCollider capsule, out float radius)
    {
        int axis = Mathf.Clamp(capsule.direction, 0, 2);
        float localRadius = capsule.radius;
        // Capsule height includes hemispheres; sphere centers span height - 2*radius.
        float halfLong = capsule.height * 0.5f;

        Vector3 lossy = capsule.transform.lossyScale;
        int axisA = (axis + 1) % 3;
        int axisB = (axis + 2) % 3;
        float radialScale = Mathf.Max(Mathf.Abs(lossy[axisA]), Mathf.Abs(lossy[axisB]));
        float worldRadius = localRadius * radialScale;
        float longScale = Mathf.Abs(lossy[axis]);

        return BuildAxis(capsule.center, axis, halfLong, localRadius, worldRadius, longScale, out radius);
    }

    static Vector3[] BuildAxis(Vector3 center, int axis, float halfLong, float localRadius, float worldRadius, float longAxisWorldScale, out float radius)
    {
        float start = -halfLong + localRadius;
        float end = halfLong - localRadius;
        if (end < start)
        {
            radius = Mathf.Max(worldRadius, 0.01f);
            return new[] { center };
        }

        float usable = end - start;
        float spacing = Mathf.Max(localRadius, 1e-4f);
        int count = Mathf.Clamp(Mathf.CeilToInt(usable / spacing) + 1, 1, Grabbable.DefaultLocomotionSamples);
        var offsets = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : Mathf.Lerp(start, end, i / (float)(count - 1));
            Vector3 p = center;
            p[axis] = center[axis] + t;
            offsets[i] = p;
        }

        // If max sample count forces wider spacing, grow radius so spheres still touch along the shaft.
        float worldSpacing = count <= 1 ? 0f : (usable * Mathf.Max(longAxisWorldScale, 1e-4f)) / (count - 1);
        radius = Mathf.Max(Mathf.Max(worldRadius, worldSpacing * 0.5f), 0.01f);
        return offsets;
    }
}
