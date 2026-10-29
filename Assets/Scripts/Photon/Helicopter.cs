using System.Collections.Generic;
using UnityEngine;

public class Helicopter : MonoBehaviour
{
    public Transform propeller;
    [Tooltip("Visual-only tail rotor pivot; local X follows the axle. Keep colliders outside this pivot.")]
    [SerializeField] Transform tailRotor;
    public float propellerRotateSpeed;
    public float propellerLerpSpeed = 2f;

    public float swayRollDegrees = 3f;
    public float swayPitchDegrees = 2f;
    public float swayBobMeters = 0.2f;
    public float swayPeriod = 3f;

    [SerializeField] BoxCollider passengerCabin;
    [Tooltip("Extra height above the cabin for an existing passenger's small jumps, in world meters.")]
    [SerializeField, Min(0f)] float passengerJumpHeadroom = 1f;
    [Tooltip("Maximum rise from exterior support during a small hop, in meters.")]
    [SerializeField, Min(0f)] float supportHopHeight = 0.9f;
    [Tooltip("Maximum time without exterior body/hand contact during a small hop.")]
    [SerializeField, Min(0f)] float supportHopSeconds = 0.9f;

    readonly HashSet<Collider> supportColliders = new HashSet<Collider>();
    bool supportCached, hasSupportBounds;
    Bounds supportBounds;
    int supportMask;
    Collider bodySupport, leftSupport, rightSupport;
    Vector3 bodySupportPoint, lastSupportAnchor;
    double bodySupportTime = double.NegativeInfinity;
    double lastSupportTime = double.NegativeInfinity;
    int handSupportFrame = -2;
    int probeFrame = -1;
    Vector3 probeFoot, probePoint, probeNormal;
    Collider probeCollider;
    float probeLength;

    GR.Player localPlayer;
    NewPlayer vrPassenger;
    DesktopPlayer desktopPassenger;
    bool carryingPassenger;
    int releasedFrame = -1;

    public bool HasLocalPassenger => localPlayer != null && localPlayer == GR.Player.instance
        && carryingPassenger && releasedFrame != Time.frameCount && TryGetAnchor(out Vector3 anchor)
        && (ContainsPassenger(anchor, true) || ExteriorSupport(anchor));

    float currentPropellerSpeed;
    float targetPropellerSpeed;

    void Awake() => CacheSupportColliders();

    void Update()
    {
        currentPropellerSpeed = Mathf.Lerp(currentPropellerSpeed, targetPropellerSpeed, propellerLerpSpeed * Time.deltaTime);
        float rotorAngle = propellerRotateSpeed * Time.deltaTime;
        propeller.Rotate(0f, rotorAngle, 0f, Space.Self);
        if (tailRotor != null) tailRotor.Rotate(rotorAngle, 0f, 0f, Space.Self);
    }

    // Called by the manager before either local movement controller. Resolve the whole
    // pose here: a LateUpdate sway would move the floor again after locomotion finishes.
    public void MoveCabin(Vector3 position, Quaternion rotation, bool continuous)
    {
        CacheLocalPlayer();
        if (!continuous) ClearSupport();
        bool hasPassenger = TryGetAnchor(out Vector3 anchor);
        bool carry = continuous && hasPassenger && releasedFrame != Time.frameCount
            && (ContainsPassenger(anchor, carryingPassenger) || ExteriorSupport(anchor));
        Vector3 localAnchor = carry ? transform.InverseTransformPoint(anchor) : Vector3.zero;
        Vector3 previousPosition = transform.position;
        Quaternion previousRotation = transform.rotation;

        float weight = propellerRotateSpeed > 0f
            ? Mathf.Clamp01(currentPropellerSpeed / propellerRotateSpeed) : 0f;
        if (weight > 0f)
        {
            float t = Time.time * (Mathf.PI * 2f / Mathf.Max(0.01f, swayPeriod));
            float roll = Mathf.Sin(t) * swayRollDegrees;
            float pitch = Mathf.Sin(t * 0.73f + 1.1f) * swayPitchDegrees;
            float bob = Mathf.Sin(t * 1.17f + 0.4f) * swayBobMeters;
            position += Vector3.up * (bob * weight);
            rotation *= Quaternion.Euler(pitch * weight, 0f, roll * weight);
        }
        bool moved = !previousPosition.Equals(position) || !previousRotation.Equals(rotation);
        if (moved) transform.SetPositionAndRotation(position, rotation);
        carryingPassenger = carry;
        if (carry && moved)
        {
            Vector3 delta = transform.TransformPoint(localAnchor) - anchor;
            if (delta.sqrMagnitude > 0f)
            {
                // Translate the rig without tilting the headset or injecting flight
                // into the player's jump velocity. Ghosts use this same live rig.
                if (vrPassenger != null && vrPassenger.isActiveAndEnabled)
                    vrPassenger.ApplyPassengerDisplacement(delta);
                else
                    desktopPassenger.ApplyPassengerDisplacement(delta);
            }
        }

        // Auto-sync stays off. Flush once, after moving both cabin and passenger,
        // only when the cabin moved; upcoming player casts must see the new floor.
        if (moved)
            Physics.SyncTransforms();
    }

    void CacheLocalPlayer()
    {
        if (localPlayer == GR.Player.instance) return;
        localPlayer = GR.Player.instance;
        vrPassenger = localPlayer != null ? localPlayer.GetComponent<NewPlayer>() : null;
        desktopPassenger = localPlayer != null ? localPlayer.GetComponent<DesktopPlayer>() : null;
        carryingPassenger = false;
        ClearSupport();
    }

    bool TryGetAnchor(out Vector3 anchor)
    {
        if (vrPassenger != null && vrPassenger.isActiveAndEnabled) { anchor = vrPassenger.bodyMidpoint.position; return true; }
        if (desktopPassenger != null && desktopPassenger.isActiveAndEnabled) { anchor = desktopPassenger.PassengerAnchor; return true; }
        anchor = default;
        return false;
    }

    // These reports come from existing movement/physics hits, never trigger overlap.
    public void ReportBodySupport(Transform source, Collider collider, Vector3 point, Vector3 normal)
    {
        CacheLocalPlayer();
        if (localPlayer == null || source != localPlayer.transform || normal.y < 0.5f) return;
        bodySupport = OwnsSupport(collider) ? collider : null;
        if (bodySupport == null) return;
        bodySupportPoint = collider.transform.InverseTransformPoint(point);
        bodySupportTime = Time.timeAsDouble;
    }

    public void ReportHandSupport(Transform source, Collider left, Collider right)
    {
        CacheLocalPlayer();
        if (localPlayer == null || source != localPlayer.transform) return;
        leftSupport = OwnsSupport(left) ? left : null;
        rightSupport = OwnsSupport(right) ? right : null;
        handSupportFrame = Time.frameCount;
    }

    public void RefreshPassenger()
    {
        CacheLocalPlayer();
        carryingPassenger = isActiveAndEnabled && releasedFrame != Time.frameCount && TryGetAnchor(out Vector3 anchor)
            && (ContainsPassenger(anchor, carryingPassenger) || ExteriorSupport(anchor));
    }

    public bool OwnsSupport(Collider collider)
    {
        CacheSupportColliders();
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy
            && supportColliders.Contains(collider) && collider.transform.IsChildOf(transform)
            && collider.attachedRigidbody == null;
    }

    void CacheSupportColliders()
    {
        if (supportCached) return;
        supportCached = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            // Independently moving rotors and loose physics items cannot use the cabin frame.
            if (collider.isTrigger || collider.attachedRigidbody != null
                || (propeller != null && collider.transform.IsChildOf(propeller))) continue;
            supportColliders.Add(collider);
            supportMask |= 1 << collider.gameObject.layer;
            Bounds bounds = collider.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                if (!hasSupportBounds) { supportBounds = new Bounds(point, Vector3.zero); hasSupportBounds = true; }
                else supportBounds.Encapsulate(point);
            }
        }
        // Broad-phase only. Exact support still requires a contact or a short ray.
        supportBounds.Expand(4f);
    }

    bool ExteriorSupport(Vector3 anchor)
    {
        double now = Time.timeAsDouble;
        if (vrPassenger != null && vrPassenger.isActiveAndEnabled && !vrPassenger.disableMovement
            // The previous locomotion frame is still valid across a slow frame.
            && handSupportFrame >= Time.frameCount - 1
            && ((vrPassenger.wasLeftHandTouching && OwnsSupport(leftSupport))
                || (vrPassenger.wasRightHandTouching && OwnsSupport(rightSupport))))
            return RememberSupport(anchor, now);

        Vector3 foot;
        float radius;
        if (vrPassenger != null && vrPassenger.isActiveAndEnabled && vrPassenger.bodyCollider != null)
        {
            foot = vrPassenger.PassengerFoot;
            radius = vrPassenger.PassengerRadius;
        }
        else if (desktopPassenger != null && desktopPassenger.isActiveAndEnabled)
        {
            foot = desktopPassenger.PassengerFoot;
            radius = desktopPassenger.PassengerRadius;
        }
        else return false;

        if (now - bodySupportTime <= 0.06d && OwnsSupport(bodySupport))
        {
            Vector3 offset = foot - bodySupport.transform.TransformPoint(bodySupportPoint);
            if (Mathf.Abs(offset.y) <= 0.08f && offset.x * offset.x + offset.z * offset.z <= (radius + 0.02f) * (radius + 0.02f))
                return RememberSupport(anchor, now);
        }

        CacheSupportColliders();
        if (!hasSupportBounds || !supportBounds.Contains(transform.InverseTransformPoint(foot))) return EndHop();
        Vector3 rise = anchor - transform.TransformPoint(lastSupportAnchor);
        bool hopping = now - lastSupportTime <= supportHopSeconds && rise.y >= -0.15f && rise.y <= supportHopHeight;
        float length = hopping ? supportHopHeight + 0.16f : 0.16f;
        Vector3 localFoot = transform.InverseTransformPoint(foot);
        // Carry and serialization often evaluate the same unchanged foot in one
        // frame. Reuse the query, including misses, across those calls.
        if (probeFrame != Time.frameCount || (localFoot - probeFoot).sqrMagnitude > 0.000001f || probeLength != length)
        {
            probeFrame = Time.frameCount; probeFoot = localFoot; probeLength = length;
            probeCollider = null;
            if (Physics.Raycast(foot + Vector3.up * 0.08f, Vector3.down, out RaycastHit hit, length, supportMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= 0.5f && OwnsSupport(hit.collider))
            {
                probeCollider = hit.collider;
                probePoint = hit.collider.transform.InverseTransformPoint(hit.point);
                probeNormal = hit.collider.transform.InverseTransformDirection(hit.normal);
            }
        }
        if (!OwnsSupport(probeCollider) || probeCollider.transform.TransformDirection(probeNormal).y < 0.5f) return EndHop();
        float gap = foot.y - probeCollider.transform.TransformPoint(probePoint).y;
        if (gap >= -0.08f && gap <= 0.08f) return RememberSupport(anchor, now);
        if (hopping && gap > 0f && gap <= supportHopHeight + 0.08f) return true;
        return EndHop();
    }

    bool RememberSupport(Vector3 anchor, double now)
    {
        lastSupportTime = now;
        lastSupportAnchor = transform.InverseTransformPoint(anchor);
        return true;
    }

    bool EndHop() { lastSupportTime = double.NegativeInfinity; return false; }

    void ClearSupport()
    {
        bodySupport = leftSupport = rightSupport = probeCollider = null;
        bodySupportTime = lastSupportTime = double.NegativeInfinity;
        handSupportFrame = -2;
        probeFrame = -1;
    }

    bool ContainsPassenger(Vector3 anchor, bool alreadyAboard)
    {
        if (passengerCabin == null || !passengerCabin.enabled || !passengerCabin.gameObject.activeInHierarchy)
            return false;
        Vector3 point = passengerCabin.transform.InverseTransformPoint(anchor) - passengerCabin.center;
        Vector3 half = passengerCabin.size * 0.5f;
        float extraHeight = alreadyAboard
            ? passengerJumpHeadroom / Mathf.Max(0.0001f, Mathf.Abs(passengerCabin.transform.lossyScale.y)) : 0f;
        // No horizontal expansion: walking/jumping out of a door releases carry.
        return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.z) <= half.z
            && point.y >= -half.y && point.y <= half.y + extraHeight;
    }

    public void ReleasePassenger()
    {
        carryingPassenger = false;
        ClearSupport();
        // A warp may happen before the manager's Update in the same frame.
        releasedFrame = Time.frameCount;
    }

    void OnDisable() => ReleasePassenger();

    public void StartPropeller()
    {
        targetPropellerSpeed = propellerRotateSpeed;
    }

    public void StopPropeller()
    {
        targetPropellerSpeed = 0f;
    }
}
