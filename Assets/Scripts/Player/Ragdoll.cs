using UnityEngine;

/// <summary>World-space velocities the corpse starts with, so a death mid-run or mid-swing keeps its motion.</summary>
public struct RagdollLaunch
{
    public Vector3 body;
    public Vector3 leftHand;
    public Vector3 rightHand;
    // Authored killing blow, separate from tracking speeds and their glitch clamp.
    public Vector3 impact;
    public Vector3 rotation;
}

public class Ragdoll : MonoBehaviour
{
    // Tracking glitches can report huge speeds for a frame.
    const float MaxLaunchSpeed = 8f;

    [Header("Targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform targetHip;

    [Header("Source")]
    public Transform leftHandSource;
    public Transform rightHandSource;
    public Transform sourceHip;

    Transform[] bones;
    Vector3[] restPositions;
    Quaternion[] restRotations;
    Vector3[] restScales;
    Rigidbody[] bodies;
    Rigidbody torso;
    Rigidbody leftUpperArm, rightUpperArm;
    Vector3[] bodyPositions;
    Quaternion[] bodyRotations;
    Transform collisionOwner;
    Collider[] corpseColliders, ownerColliders;
    int ownerColliderCount;

    void Awake() => CacheRestPose();
    public void Prepare() => CacheRestPose();
    public void Prepare(Transform owner)
    {
        CacheRestPose();
        if (owner == null || collisionOwner == owner) return;
        collisionOwner = owner;
        corpseColliders = GetComponentsInChildren<Collider>(true);
        ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        ownerColliderCount = 0;
        foreach (var collider in ownerColliders)
        {
            // A local corpse starts under its player. Droppable equipment is not
            // part of the owner's body and must retain its normal collisions.
            if (collider.transform.IsChildOf(transform) || collider.GetComponentInParent<Grabbable>(true) != null) continue;
            ownerColliders[ownerColliderCount++] = collider;
        }
    }

    void IgnoreOwnerCollisions()
    {
        if (collisionOwner == null || ownerColliderCount == 0) return;
        // Activation can reset physics ignore state. Reapply once per death,
        // after activation, using only the collider arrays cached at setup.
        foreach (var corpse in corpseColliders)
        {
            if (corpse == null || !corpse.gameObject.activeInHierarchy) continue;
            for (int i = 0; i < ownerColliderCount; i++)
            {
                var owner = ownerColliders[i];
                if (owner != null && owner.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(corpse, owner, true);
            }
        }
    }
    public void Park()
    {
        CacheRestPose();
        foreach (var body in bodies)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
        }
        gameObject.SetActive(false);
    }

    /// <summary>Dress the corpse as the duck that died. Call before PoseToPlayer.</summary>
    public void ApplySkin(byte skinId, int colorActor = 0)
    {
        DeathProfile.Trace($"[Ragdoll] '{name}' dressing as skin {skinId}", this);
        var visuals = DuckSkinVisuals.Attach(gameObject, DuckSkinSource.Manual);
        visuals.Apply(skinId);
        visuals.BindColor(colorActor);
    }

    public void ApplyCompanionAppearance(Transform companionRig, bool redEyes = false)
    {
        var visuals = DuckSkinVisuals.Attach(gameObject, DuckSkinSource.Manual);
        if (!visuals.CopyCompanionAppearance(companionRig, redEyes))
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write("[Ragdoll] Could not copy companion appearance onto the corpse rig.", this);
            }
    }

    void CacheRestPose()
    {
        if (bones != null) return;
        bones = GetComponentsInChildren<Transform>(true);
        restPositions = new Vector3[bones.Length];
        restRotations = new Quaternion[bones.Length];
        restScales = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            restPositions[i] = bones[i].localPosition;
            restRotations[i] = bones[i].localRotation;
            restScales[i] = bones[i].localScale;
        }
        bodies = GetComponentsInChildren<Rigidbody>(true);
        // Include inactive bones; never pick up the owning player's Rigidbody.
        var candidate = targetHip != null ? targetHip.GetComponentInParent<Rigidbody>(true) : null;
        if (candidate != null && candidate.transform.IsChildOf(transform)) torso = candidate;
        leftUpperArm = FindUpperArm(leftHandTarget);
        rightUpperArm = FindUpperArm(rightHandTarget);
        bodyPositions = new Vector3[bodies.Length];
        bodyRotations = new Quaternion[bodies.Length];
    }

    public void PoseToPlayer(Transform player, RagdollLaunch launch = default)
    {
        using var sample = DeathProfile.Ragdoll.Auto();
        // Inactive local ragdolls may not have received Awake yet.
        CacheRestPose();
        foreach (Rigidbody body in bodies)
            body.isKinematic = true;

        // Detach before posing so subsequent player motion cannot move the corpse.
        transform.SetParent(null, true);
        for (int i = 0; i < bones.Length; i++)
        {
            // Skin attachments cached here can be removed by a later skin change.
            if (bones[i] == null || bones[i] == transform) continue;
            bones[i].localPosition = restPositions[i];
            bones[i].localRotation = restRotations[i];
            bones[i].localScale = restScales[i];
        }
        transform.SetPositionAndRotation(player.position, player.rotation);
        if (targetHip != null && sourceHip != null)
        {
            targetHip.SetPositionAndRotation(sourceHip.position, sourceHip.rotation);
            CopyRotations(sourceHip, targetHip);
        }
        // Preserve bone lengths: never teleport an end bone to a tracking target.
        if (leftHandTarget != null && leftHandSource != null)
            leftHandTarget.rotation = leftHandSource.rotation;
        if (rightHandTarget != null && rightHandSource != null)
            rightHandTarget.rotation = rightHandSource.rotation;

        // Snapshot all world poses before Rigidbody setters can affect child transforms.
        for (int i = 0; i < bodies.Length; i++)
        {
            bodyPositions[i] = bodies[i].transform.position;
            bodyRotations[i] = bodies[i].transform.rotation;
        }
        gameObject.SetActive(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].position = bodyPositions[i];
            bodies[i].rotation = bodyRotations[i];
        }
        Physics.SyncTransforms();
        Vector3 bodyVelocity = Vector3.ClampMagnitude(launch.body, MaxLaunchSpeed);
        foreach (Rigidbody body in bodies)
        {
            body.solverIterations = Mathf.Max(body.solverIterations, 12);
            body.solverVelocityIterations = Mathf.Max(body.solverVelocityIterations, 4);
            body.isKinematic = false;
            body.linearVelocity = bodyVelocity + launch.impact;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }
        Launch(leftHandTarget, launch.leftHand, launch.impact);
        Launch(rightHandTarget, launch.rightHand, launch.impact);
        if (torso != null) torso.angularVelocity = launch.rotation;
        // Deterministic world-space kicks: reuse the replicated rotation, with no
        // new randomness or network fields. Keep arms gentle even for strong hits.
        Vector3 armSpin = Vector3.ClampMagnitude(launch.rotation, 10f) * 0.2f;
        Vector3 armTwist = Vector3.Cross(Vector3.up, armSpin) * 0.5f;
        if (leftUpperArm != null) leftUpperArm.angularVelocity = armSpin + armTwist;
        if (rightUpperArm != null) rightUpperArm.angularVelocity = armSpin - armTwist;
        IgnoreOwnerCollisions();
        DeathProfile.Trace($"[Ragdoll] '{name}' posed at {player.name}, {bodies.Length} bodies, launch body {launch.body.magnitude:0.00} m/s " +
                  $"(clamped {bodyVelocity.magnitude:0.00}), left hand {launch.leftHand.magnitude:0.00}, right hand {launch.rightHand.magnitude:0.00}", this);
    }

    Rigidbody FindUpperArm(Transform hand)
    {
        if (torso == null || hand == null || !hand.IsChildOf(transform)) return null;
        // Resolve once, while the corpse may be inactive. The shoulder joint
        // connects its upper-arm body directly to the torso.
        for (var bone = hand; bone != null && bone != transform && bone != torso.transform; bone = bone.parent)
            if (bone.TryGetComponent<CharacterJoint>(out var joint) && joint.connectedBody == torso)
                return bone.GetComponent<Rigidbody>();
        return null;
    }

    static void Launch(Transform hand, Vector3 velocity, Vector3 impact)
    {
        Rigidbody body = hand != null ? hand.GetComponentInParent<Rigidbody>() : null;
        if (body != null)
            body.linearVelocity = Vector3.ClampMagnitude(velocity, MaxLaunchSpeed) + impact;
    }

    static void CopyRotations(Transform source, Transform target)
    {
        // Both avatars use the same rig. Match siblings by name, retaining the
        // ragdoll's authored offsets/scales even if the tracked rig was stretched.
        for (int i = 0; i < target.childCount; i++)
        {
            Transform child = target.GetChild(i);
            Transform sourceChild = source.Find(child.name);
            if (sourceChild == null) continue;
            child.localRotation = sourceChild.localRotation;
            CopyRotations(sourceChild, child);
        }
    }
}
