using System.Collections.Generic;
using Game.Animation;
using UnityEngine;

public partial class PlayerHand
{
    // Metres. Enclose the fingers around the model's calibrated palm.
    const float HookPalmRadius = .105f;
    const float HookSkin = .004f;
    internal bool HookProtected { get; private set; }
    internal Transform SafeHookPalm { get; private set; }
    internal Vector3 SafeHookContact { get; private set; }
    internal bool TouchingHookSurface => HookProtected && (supportNormal != Vector3.zero || trackedContactBlocked);
    bool hasSafeHookPose;
    bool trackedContactBlocked;
    Vector3 safeCenter, safeWrist, supportNormal;
    float safeSupport;
    Quaternion safeRotation;
    Transform hookGear;
    Bounds gearLocalBounds;
    Pose safeGearPose;
    bool hasSafeGearPose;
    Transform pendingHookDrop;
    static List<MeshFilter> hookMeshes;
    static List<Collider> hookColliders;

    internal void BeginHookProtection()
    {
        if (HookProtected) return;
        if (SafeHookPalm == null)
        {
            SafeHookPalm = new GameObject("Surface constrained palm").transform;
            SafeHookPalm.SetParent(transform, false);
        }
        HookProtected = true;
        sampleFilled = sampleWrite = 0;
    }

    internal void EndHookProtection()
    {
        if (!HookProtected) return;
        HookProtected = hasSafeHookPose = hasSafeGearPose = trackedContactBlocked = false;
        sampleFilled = sampleWrite = 0;
        hookGear = null;
        supportNormal = Vector3.zero;
        if (!holdPointOnSkin && holdPoint != null) holdPoint.localPosition = holdLocalPosition;
    }

    // Rails: passenger carry translates the rig between frames. The stored safe
    // poses are world-space sweep origins and must travel with it.
    internal void ShiftHookPose(Vector3 delta)
    {
        if (!HookProtected) return;
        safeCenter += delta;
        safeWrist += delta;
        SafeHookContact += delta;
        if (hasSafeGearPose) safeGearPose.position += delta;
    }

    internal Vector3 ThrowMotionOffset => HookProtected && NewPlayer.instance != null
        ? NewPlayer.instance.transform.position : RailCargo.PassengerCarryOffset;

    internal bool ProtectHookRelease(Transform gear, ref Vector3 velocity)
    {
        Vector3 spin = Vector3.zero;
        return ProtectHookRelease(gear, ref velocity, ref spin);
    }

    internal bool ProtectHookRelease(Transform gear, ref Vector3 velocity, ref Vector3 spin)
    {
        if (!HookProtected || NewPlayer.instance == null) return true;
        var motor = NewPlayer.instance;
        ResolveHookHand(motor, IsControllerRTouch, true, releasing: gear);
        // The object may have grown to full size on this very release, or a
        // moving collider may have entered the previous grip between pose updates.
        Quaternion frame = gear.rotation;
        Bounds shape = GrabSurfaceData.TransformBounds(gearLocalBounds,
            Matrix4x4.Rotate(Quaternion.Inverse(frame)) * gear.localToWorldMatrix);
        Vector3 center = frame * shape.center;
        Vector3 head = motor.headCollider.transform.position;
        int mask = motor.locomotionEnabledLayers;
        bool acrossSurface = Physics.Linecast(head, center, out var face, mask, QueryTriggerInteraction.Ignore);
        if (acrossSurface || Physics.CheckBox(center, shape.extents, frame, mask, QueryTriggerInteraction.Ignore))
        {
            Vector3 target = acrossSurface
                ? face.point + face.normal * (Support(shape.extents, Quaternion.Inverse(frame) * face.normal) + HookSkin)
                : center;
            Vector3 normal = Vector3.zero;
            if (!Physics.CheckBox(head, shape.extents, frame, mask, QueryTriggerInteraction.Ignore))
                target = SlideBox(head, target, shape.extents, frame, mask, ref normal);
            if (!Physics.CheckBox(target, shape.extents, frame, mask, QueryTriggerInteraction.Ignore))
            {
                gear.position += target - center;
                if (acrossSurface || normal != Vector3.zero) supportNormal = acrossSurface ? face.normal : normal;
            }
            else
            {
                // A closing door can occupy both the desired and last-known grip.
                // Recheck the old pose before handing the drop to physics/Photon.
                bool usable = false;
                if (hasSafeGearPose)
                {
                    Quaternion delta = safeGearPose.rotation * Quaternion.Inverse(gear.rotation);
                    Vector3 oldCenter = safeGearPose.position + delta * (center - gear.position);
                    usable = !Physics.Linecast(head, oldCenter, mask, QueryTriggerInteraction.Ignore)
                        && !Physics.CheckBox(oldCenter, shape.extents, safeGearPose.rotation, mask, QueryTriggerInteraction.Ignore);
                    if (usable) gear.SetPositionAndRotation(safeGearPose.position, safeGearPose.rotation);
                }
                if (!usable)
                {
                    pendingHookDrop = gear;
                    return false;
                }
            }
        }
        // A rapidly turning bat can drive its far tip through a thin floor on
        // the first free physics step even when its center is moving sideways.
        if (spin.sqrMagnitude > .0001f)
        {
            Vector3 dropCenter = gear.TransformPoint(gearLocalBounds.center);
            float reach = shape.extents.magnitude + Vector3.Distance(dropCenter, gear.position);
            float tipTravel = Mathf.Min(spin.magnitude * Time.fixedDeltaTime, 2f) * reach;
            float downward = Mathf.Max(0f, -velocity.y * Time.fixedDeltaTime);
            if (Physics.BoxCast(dropCenter, shape.extents, Vector3.down, out var ground, gear.rotation,
                tipTravel + downward + HookSkin, mask, QueryTriggerInteraction.Ignore))
                supportNormal = ground.normal;
            if (supportNormal != Vector3.zero) spin = Vector3.Project(spin, supportNormal);
        }
        float intoSurface = Vector3.Dot(velocity, supportNormal);
        if (intoSurface < 0f) velocity -= supportNormal * intoSurface;
        return true;
    }

    void RetryHookDrop()
    {
        Transform pending = pendingHookDrop;
        pendingHookDrop = null;
        if (held != null && held.transform == pending && held.Holder == this)
        {
            var item = held;
            item.Release(Vector3.zero, Vector3.zero);
            if (held == item && item.Holder != this) NotifyStolen();
        }
        else if (heldShape is BagObject bag && bag != null && bag.transform == pending
            && bag.State == BagState.Held && pending.parent == HoldPoint)
            bag.DropLocal(Vector3.zero);
    }

    internal void ResolveHookHand(NewPlayer motor, bool right, bool keep, Transform trackedPalm = null, Transform releasing = null)
    {
        if (!HookProtected) return;
        Transform wrist = holdPointOnSkin ? holdPoint.parent : HoldPoint;
        // Production IK supplies a fresh wrist. Rigs with a controller-mounted grip
        // use its authored local offset as the uncorrected target instead.
        if (!holdPointOnSkin) wrist.localPosition = holdLocalPosition;
        Vector3 contact = transform.position + transform.rotation * (right ? motor.rightHandOffset : motor.leftHandOffset);
        Vector3 palm = holdPointOnSkin ? wrist.TransformPoint(RemotePlayerBody.PlayerPalmPoint) : contact;
        Transform gear = releasing;
        if (gear == null && held != null) gear = held.transform;
        if (gear == null && heldShape is BagObject bag && bag != null && bag.State == BagState.Held
            && bag.transform.parent == holdPoint) gear = bag.transform;
        if (gear != hookGear)
        {
            hookGear = gear;
            hasSafeGearPose = false;
            if (gear != null) gearLocalBounds = HookGearBounds(gear);
        }
        Quaternion frame = gear != null ? gear.rotation : Quaternion.identity;
        Quaternion inverse = Quaternion.Inverse(frame);
        // Align the cast to the item so a diagonal bat fits under a low ceiling.
        var bounds = new Bounds(inverse * palm, Vector3.one * (2f * HookPalmRadius));
        if (gear != null) bounds.Encapsulate(GrabSurfaceData.TransformBounds(gearLocalBounds,
            Matrix4x4.Rotate(inverse) * gear.localToWorldMatrix));
        Vector3 target = frame * bounds.center;
        Vector3 half = bounds.extents;
        int mask = motor.locomotionEnabledLayers;
        Vector3 origin = hasSafeHookPose ? safeCenter : motor.headCollider.transform.position;
        // When a bat rotates or a grip changes size, yesterday's center can be
        // inside today's larger box. Recover on the player's side of the surface.
        if (hasSafeHookPose && supportNormal != Vector3.zero)
            origin += supportNormal * Mathf.Max(0f, Support(half, inverse * supportNormal) - safeSupport);
        Vector3 head = motor.headCollider.transform.position;
        bool clearStart = !Physics.CheckBox(origin, half, frame, mask, QueryTriggerInteraction.Ignore);
        if (!clearStart)
        {
            origin = head;
            // Low ceilings can rule out the head as an origin for a long vertical weapon.
            // Recover from the near side of the hit plane before attempting the sweep.
            Vector3 path = target - origin;
            if (Physics.Raycast(origin, path.normalized, out var plane, path.magnitude + half.magnitude,
                mask, QueryTriggerInteraction.Ignore))
                origin = plane.point + plane.normal * (Support(half, inverse * plane.normal) + HookSkin);
            clearStart = !Physics.CheckBox(origin, half, frame, mask, QueryTriggerInteraction.Ignore);
            // A long item may touch the ceiling at head height, while the space
            // between head and grip fits it. Search only the reachable side.
            Vector3 end = target;
            if (Physics.Linecast(head, end, out var obstruction, mask, QueryTriggerInteraction.Ignore))
                end = obstruction.point + obstruction.normal * (Support(half, inverse * obstruction.normal) + HookSkin);
            for (int step = 0; !clearStart && step <= 4; step++)
            {
                origin = Vector3.Lerp(head, end, step * .25f);
                clearStart = !Physics.CheckBox(origin, half, frame, mask, QueryTriggerInteraction.Ignore);
            }
        }
        Vector3 normal = Vector3.zero;
        Vector3 accepted = clearStart ? SlideBox(origin, target, half, frame, mask, ref normal) : origin;
        bool valid = clearStart && !Physics.CheckBox(accepted, half, frame, mask, QueryTriggerInteraction.Ignore);
        if (!valid)
        {
            if (hasSafeHookPose)
            {
                wrist.SetPositionAndRotation(safeWrist, safeRotation);
                return;
            }
            // An enclosing box may not fit around a valid compound grip in a tight space.
            // Keep the grip on the head-facing side of the surface until it can be swept.
            accepted = origin;
        }
        if (releasing != null && normal == Vector3.zero && supportNormal != Vector3.zero
            && Physics.BoxCast(accepted, half, -supportNormal, out var support, frame,
                HookSkin * 3f, mask, QueryTriggerInteraction.Ignore))
            normal = support.normal;
        Vector3 correction = accepted - target;
        Vector3 correctedContact = contact + correction;
        // The model's wrist can stop at arm reach while a raw controller keeps
        // moving. Constrain that separate locomotion contact without enlarging a
        // weapon's collision shape to span the entire tracking error.
        Vector3 contactHalf = Vector3.one * motor.minimumRaycastDistance;
        Vector3 localContact = inverse * contact;
        Vector3 contactNormal = Vector3.zero;
        SafeHookContact = bounds.Contains(localContact - contactHalf) && bounds.Contains(localContact + contactHalf)
            ? correctedContact : SlideBox(hasSafeHookPose ? SafeHookContact : head,
                correctedContact, contactHalf, Quaternion.identity, mask, ref contactNormal);
        trackedContactBlocked = contactNormal != Vector3.zero;
        Quaternion rotation = trackedPalm != null ? trackedPalm.rotation
            : releasing != null && hasSafeHookPose ? SafeHookPalm.rotation : transform.rotation;
        if (correction.sqrMagnitude > 1e-10f)
        {
            if (holdPointOnSkin && motor.HookBody != null)
                motor.HookBody.PoseConstrainedWrist(right, wrist.position + correction, rotation);
            else wrist.position += correction;
        }
        if (SafeHookPalm.parent != wrist) SafeHookPalm.SetParent(wrist, true);
        SafeHookPalm.SetPositionAndRotation(palm + correction, rotation);
        safeCenter = accepted;
        safeWrist = wrist.position;
        safeRotation = wrist.rotation;
        hasSafeHookPose = valid;
        supportNormal = normal;
        safeSupport = Support(half, inverse * normal);
        if (gear != null && valid)
        {
            safeGearPose = new Pose(gear.position, gear.rotation);
            hasSafeGearPose = true;
        }
        if (valid && !keep && correction.sqrMagnitude < 1e-8f && (SafeHookContact - contact).sqrMagnitude < 1e-8f)
            EndHookProtection();
    }

    static float Support(Vector3 half, Vector3 normal) =>
        half.x * Mathf.Abs(normal.x) + half.y * Mathf.Abs(normal.y) + half.z * Mathf.Abs(normal.z);

    static Vector3 SlideBox(Vector3 start, Vector3 target, Vector3 half, Quaternion frame, int mask, ref Vector3 normal)
    {
        Vector3 position = start;
        Vector3 motion = target - start;
        // Two contacts allow a floor slide followed by a wall/step stop.
        for (int i = 0; i < 2; i++)
        {
            float distance = motion.magnitude;
            if (distance <= .00001f) break;
            Vector3 direction = motion / distance;
            if (!Physics.BoxCast(position, half, direction, out var hit, frame,
                distance, mask, QueryTriggerInteraction.Ignore)) return position + motion;
            normal = hit.normal;
            position += direction * Mathf.Max(0f, hit.distance - HookSkin) + normal * HookSkin;
            motion = Vector3.ProjectOnPlane(target - position, normal);
        }
        return position;
    }

    internal static Bounds HookGearBounds(Transform gear)
    {
        // Build once per grip. Held rigidbodies disable collision detection, so
        // Collider.bounds and a Rigidbody sweep are unreliable at this point.
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        var toRoot = gear.worldToLocalMatrix;
        if (gear.TryGetComponent(out Grabbable item) && item.GrabSurface != null && item.GrabSurface.IsValid)
            bounds = item.GrabSurface.LocalBounds;
        else
        {
            hookMeshes ??= new List<MeshFilter>(4);
            gear.GetComponentsInChildren(false, hookMeshes);
            foreach (var mesh in hookMeshes)
                if (mesh.sharedMesh != null)
                    bounds.Encapsulate(GrabSurfaceData.TransformBounds(mesh.sharedMesh.bounds, toRoot * mesh.transform.localToWorldMatrix));
            hookMeshes.Clear();
        }
        hookColliders ??= new List<Collider>(4);
        gear.GetComponentsInChildren(false, hookColliders);
        foreach (var collider in hookColliders)
        {
            if (!collider.enabled || collider.isTrigger) continue;
            Bounds local;
            if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (collider is SphereCollider ball) local = new Bounds(ball.center, Vector3.one * (ball.radius * 2f));
            else if (collider is CapsuleCollider capsule)
            {
                Vector3 size = Vector3.one * (capsule.radius * 2f);
                size[capsule.direction] = Mathf.Max(size[capsule.direction], capsule.height);
                local = new Bounds(capsule.center, size);
            }
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else continue;
            bounds.Encapsulate(GrabSurfaceData.TransformBounds(local, toRoot * collider.transform.localToWorldMatrix));
        }
        hookColliders.Clear();
        return bounds;
    }
}

// The owner simulates the loose item. On observers, interpolating two clear
// rotations and positions independently can briefly swing a long tip through
// the ground. Correct only the displayed pose on the actual supporting mesh.
internal struct RemoteHookDropSurface
{
    Collider floor;
    internal bool Active => floor != null;
    Bounds shape;
    Vector3 localNormal, localPoint;
    double expires;

    internal void Begin(Transform gear, int actor)
    {
        floor = null;
        var owner = PhotonPlayer.ForActor(actor);
        if (owner == null || !owner.RecentHookImpact) return;
        shape = PlayerHand.HookGearBounds(gear);
        Bounds world = GrabSurfaceData.TransformBounds(shape, gear.localToWorldMatrix);
        if (!Physics.Raycast(world.center + Vector3.up * .1f, Vector3.down, out var hit,
            world.extents.y + .55f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)) return;
        floor = hit.collider;
        localNormal = floor.transform.InverseTransformDirection(hit.normal);
        localPoint = floor.transform.InverseTransformPoint(hit.point);
        expires = Time.unscaledTimeAsDouble + 1.5;
    }

    internal void Correct(Transform gear, bool looseRemote)
    {
        if (!looseRemote || !floor.enabled || Time.unscaledTimeAsDouble >= expires) { floor = null; return; }
        var frame = floor.transform;
        Vector3 normal = frame.TransformDirection(localNormal);
        var matrix = gear.localToWorldMatrix;
        Vector3 axis = matrix.transpose.MultiplyVector(normal);
        Vector3 e = shape.extents;
        Vector3 corner = matrix.MultiplyPoint3x4(shape.center - new Vector3(
            Mathf.Sign(axis.x) * e.x, Mathf.Sign(axis.y) * e.y, Mathf.Sign(axis.z) * e.z));
        float depth = Vector3.Dot(frame.TransformPoint(localPoint) - corner, normal);
        if (depth < -.02f) return;
        float above = Mathf.Max(.04f, depth + .04f);
        if (floor.Raycast(new Ray(corner + normal * above, -normal), out var contact, above + .02f))
        {
            float correction = above - contact.distance + .002f;
            if (correction > 0f) gear.position += normal * correction;
        }
    }
}
