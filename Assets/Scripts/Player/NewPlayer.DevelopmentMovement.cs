#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Local development controls only. Accepted head/hand poses use the existing Photon stream.
public partial class NewPlayer
{
    const float DevelopmentDeadzone = .18f;
    const float DevelopmentSpeed = 4.5f;
    const float DevelopmentLiftSpeed = 3f;
    const float DevelopmentAcceleration = 30f;
    const float DevelopmentBraking = 60f;
    const float DevelopmentSkin = .003f;
    const int DevelopmentSlideLimit = 3;
    const int DevelopmentQueryCapacity = 16;

    bool developmentMoving;
#if UNITY_EDITOR
    bool developmentWasStick;
#endif
    Vector3 developmentVelocity;
    RaycastHit[] developmentHits;
    Collider[] developmentOverlaps;

    struct DevelopmentShape
    {
        public Collider collider;
        public Vector3 a, b;
        public float radius;
        public bool capsule;
        public DevelopmentShape Shift(Vector3 delta)
        {
            var shifted = this; shifted.a += delta; shifted.b += delta; return shifted;
        }
    }

    void ResetDevelopmentMovement()
    {
        developmentMoving = false;
        developmentVelocity = Vector3.zero;
    }

    bool DevelopmentMovement(float deltaTime)
    {
        // Shared keyboard input already moves the tracked poses through the normal solver.
        if (controllerInput != null && controllerInput.simulation != null && controllerInput.simulation.IsSimulating)
        { ResetDevelopmentMovement(); return false; }
        Vector2 input = stickMove ? controllerInput.leftThumbstickValue : Vector2.zero;
        float lift = stickMove ? controllerInput.rightThumbstickValue.y : 0f;
        bool active = stickMove;
        float speed = DevelopmentSpeed;
#if UNITY_EDITOR
        if (controllerInput == null || controllerInput.simulation == null) {
        Vector2 keyboard = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        bool space = Input.GetKey(KeyCode.Space);
        input = Vector2.ClampMagnitude(input + keyboard, 1f);
        if (space) lift = 1f;
        active |= keyboard != Vector2.zero || space || (developmentMoving && !developmentWasStick && developmentVelocity.sqrMagnitude > .000001f);
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= 2.5f;
        }
#endif
        if (!active)
        {
            if (developmentMoving)
            {
                InitializeValues();
                lastBodyMidpoint = MovementAnchorPosition(bodyMidpoint.position);
                wasLeftHandTouching = wasRightHandTouching = wasHeadTouching = false;
            }
            return false;
        }
        if (!developmentMoving)
        {
            InitializeValues();
            lastBodyMidpoint = MovementAnchorPosition(bodyMidpoint.position);
            wasLeftHandTouching = wasRightHandTouching = wasHeadTouching = false;
            leftHandColliding = rightHandColliding = false;
            developmentMoving = true;
            if (developmentHits == null)
            {
                developmentHits = new RaycastHit[DevelopmentQueryCapacity];
                developmentOverlaps = new Collider[DevelopmentQueryCapacity];
            }
        }
#if UNITY_EDITOR
        developmentWasStick = stickMove;
#endif
        MoveDevelopmentPlayer(input, lift, speed, deltaTime);
        return true;
    }

    static Vector2 DevelopmentStickInput(Vector2 input)
    {
        float magnitude = input.magnitude;
        return magnitude <= DevelopmentDeadzone ? Vector2.zero
            : input / magnitude * Mathf.Clamp01((magnitude - DevelopmentDeadzone) / (1f - DevelopmentDeadzone));
    }

    void MoveDevelopmentPlayer(Vector2 input, float liftInput, float speed, float deltaTime)
    {
        // Limit powered travel after a hitch; historical physics/tracking sweeps still cover the full displacement.
        float dt = Mathf.Min(deltaTime, .05f);
        bool separate = BeginPhysicalMovement(out Pose displayed, out Vector3 physicsStart);
        float eyeOffset = headCollider.transform.position.y - headEyeOffset.position.y;
        Quaternion bodyHeading = BodyForwardRot();
        bodyCollider.transform.position = headCollider.transform.position + bodyHeading * headBodyOffset + Vector3.up * eyeOffset;

        bool blocked = false;
        // These sweeps also run with the sticks released: gravity and room-scale tracking must not tunnel.
        var head = DevelopmentHeadShape();
        Vector3 headDelta = headCollider.transform.position - lastHeadPosition;
        Vector3 accepted = SweepDevelopment(head.Shift(-headDelta), default, false, headDelta, ref blocked);
        TranslateDevelopment(accepted - headDelta);
        var body = DevelopmentBodyShape();
        Vector3 bodyDelta = bodyMidpoint.position - lastBodyMidpoint;
        accepted = SweepDevelopment(body.Shift(-bodyDelta), default, false, bodyDelta, ref blocked);
        TranslateDevelopment(accepted - bodyDelta);
        bool clear = RecoverDevelopmentOverlap(ref blocked);

        Vector3 forward = Vector3.ProjectOnPlane(headCollider.transform.forward, Vector3.up);
        // The existing body heading remains defined at straight up/down and follows snap turns/warps.
        forward = forward.sqrMagnitude > .01f ? forward.normalized : bodyHeading * Vector3.forward;
        input = DevelopmentStickInput(input);
        Vector3 target = (forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x) * speed;
        Vector3 previous = developmentVelocity;
        float rate = target.sqrMagnitude < previous.sqrMagnitude ? DevelopmentBraking : DevelopmentAcceleration;
        float changeTime = Mathf.Min(dt, (target - previous).magnitude / rate);
        developmentVelocity = Vector3.MoveTowards(previous, target, rate * dt);
        // Integrate the ramp, including the constant-speed remainder, independent of render frequency.
        Vector3 travel = (previous + developmentVelocity) * (.5f * changeTime) + developmentVelocity * (dt - changeTime);
        if (clear)
        {
            accepted = SweepDevelopment(DevelopmentHeadShape(), DevelopmentBodyShape(), true, travel, ref blocked);
            TranslateDevelopment(accepted);
        }
        else developmentVelocity = Vector3.zero; // Saturated/unresolved geometry fails closed.

        float throttle = Mathf.Clamp01((liftInput - DevelopmentDeadzone) / (1f - DevelopmentDeadzone));
        if (throttle > 0f && clear)
        {
            // Approach the requested speed from actual physics velocity, including falls and external launches.
            // Releasing the stick leaves velocity and gravity alone.
            Vector3 velocity = rb.linearVelocity;
            float liftVelocity = Mathf.MoveTowards(velocity.y, throttle * DevelopmentLiftSpeed, DevelopmentAcceleration * dt);
            float lookahead = Mathf.Max(Time.fixedDeltaTime, dt);
            float rise = Mathf.Max(0f, liftVelocity) * lookahead;
            if (rise > .000001f)
            {
                // A clearance query must not clip horizontal motion. Approach the ceiling without
                // repeatedly switching thrust off/on; physics gravity/contact handles the last gap.
                float clearance = rise;
                if (CastDevelopment(DevelopmentHeadShape(), Vector3.up, rise, out RaycastHit headHit))
                    clearance = Mathf.Min(clearance, Mathf.Max(0f, headHit.distance - DevelopmentSkin));
                if (CastDevelopment(DevelopmentBodyShape(), Vector3.up, rise, out RaycastHit bodyHit))
                    clearance = Mathf.Min(clearance, Mathf.Max(0f, bodyHit.distance - DevelopmentSkin));
                liftVelocity = Mathf.Min(liftVelocity, clearance / lookahead);
            }
            velocity.y = liftVelocity;
            rb.linearVelocity = velocity;
        }

        lastHeadPosition = headCollider.transform.position;
        lastBodyMidpoint = bodyMidpoint.position;
        wasHeadTouching = blocked;
        StoreVelocities(deltaTime);
        if (separate)
        {
            if (blocked) { movementRenderCount = 0; QueueMovementPoseRestore(); }
            else
            {
                float fraction = Physics.simulationMode == SimulationMode.FixedUpdate && Time.fixedDeltaTime > 0f
                    ? Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime)) : 1f;
                Vector3 position = displayed.position + (rb.position - physicsStart) * fraction;
                if (Physics.simulationMode == SimulationMode.FixedUpdate)
                {
                    float nativeY = position.y;
                    position = ContactRenderPosition(position);
                    // Lift/gravity are physics velocity, not scripted root steps. Preserve their native interpolation.
                    position.y = nativeY;
                }
                transform.SetPositionAndRotation(position, displayed.rotation);
                QueueMovementPoseRestore();
            }
        }
    }

    void TranslateDevelopment(Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-14f) return;
        Vector3 position = rb.position + delta;
        transform.position = position;
        rb.position = position;
    }

    DevelopmentShape DevelopmentHeadShape()
    {
        Vector3 scale = headCollider.transform.lossyScale;
        return new DevelopmentShape { collider = headCollider, a = headCollider.transform.TransformPoint(headCollider.center),
            radius = headCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) };
    }

    DevelopmentShape DevelopmentBodyShape()
    {
        Transform t = bodyCollider.transform;
        Vector3 scale = t.lossyScale;
        int axis = bodyCollider.direction;
        float radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
        Vector3 direction = axis == 0 ? t.right : axis == 1 ? t.up : t.forward;
        Vector3 extent = direction * Mathf.Max(0f, bodyCollider.height * Mathf.Abs(scale[axis]) * .5f - radius);
        Vector3 center = t.TransformPoint(bodyCollider.center);
        return new DevelopmentShape { collider = bodyCollider, a = center + extent, b = center - extent, radius = radius, capsule = true };
    }

    bool DevelopmentObstacle(Collider collider)
        => collider != null && collider.attachedRigidbody != rb && !collider.transform.IsChildOf(transform);

    bool CastDevelopment(DevelopmentShape shape, Vector3 direction, float distance, out RaycastHit nearest)
    {
        int count = shape.capsule
            ? Physics.CapsuleCastNonAlloc(shape.a, shape.b, shape.radius, direction, developmentHits, distance + DevelopmentSkin, locomotionEnabledLayers, QueryTriggerInteraction.Ignore)
            : Physics.SphereCastNonAlloc(shape.a, shape.radius, direction, developmentHits, distance + DevelopmentSkin, locomotionEnabledLayers, QueryTriggerInteraction.Ignore);
        nearest = default;
        if (count == developmentHits.Length) return true; // No unbounded retry or uncertain free path.
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = developmentHits[i];
            if (!DevelopmentObstacle(hit.collider) || Vector3.Dot(direction, hit.normal) >= -.00001f || hit.distance >= closest) continue;
            closest = hit.distance; nearest = hit;
        }
        return closest < float.PositiveInfinity;
    }

    Vector3 SweepDevelopment(DevelopmentShape first, DevelopmentShape second, bool both, Vector3 travel, ref bool blocked)
    {
        Vector3 moved = Vector3.zero, remaining = travel, previousNormal = Vector3.zero;
        for (int iteration = 0; iteration < DevelopmentSlideLimit && remaining.sqrMagnitude > 1e-12f; iteration++)
        {
            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;
            bool hit = CastDevelopment(first.Shift(moved), direction, distance, out RaycastHit nearest);
            if (both && CastDevelopment(second.Shift(moved), direction, distance, out RaycastHit other)
                && (!hit || other.distance < nearest.distance)) { nearest = other; hit = true; }
            if (!hit) { moved += remaining; break; }
            blocked = true;
            if (nearest.normal.sqrMagnitude < .5f) { developmentVelocity = Vector3.zero; break; }
            float advance = Mathf.Min(distance, Mathf.Max(0f, nearest.distance - DevelopmentSkin));
            moved += direction * advance;
            remaining -= direction * advance;
            remaining = Vector3.ProjectOnPlane(remaining, nearest.normal);
            if (iteration > 0 && Vector3.Dot(remaining, previousNormal) < 0f)
            {
                Vector3 crease = Vector3.Cross(previousNormal, nearest.normal);
                remaining = crease.sqrMagnitude > .00001f ? Vector3.Project(remaining, crease) : Vector3.zero;
            }
            previousNormal = nearest.normal;
            ClipDevelopmentVelocity(nearest.normal);
        }
        return moved;
    }

    void ClipDevelopmentVelocity(Vector3 normal)
    {
        float into = Vector3.Dot(developmentVelocity, normal);
        if (into < 0f) developmentVelocity -= normal * into;
        Vector3 velocity = rb.linearVelocity;
        into = Vector3.Dot(velocity, normal);
        if (into < 0f) rb.linearVelocity = velocity - normal * into;
    }

    bool RecoverDevelopmentOverlap(ref bool blocked)
    {
        // Two bounded passes accommodate moving obstacles and initial overlaps, without global SyncTransforms.
        bool unresolved = false;
        for (int pass = 0; pass < 2; pass++)
        {
            unresolved = false;
            for (int part = 0; part < 2; part++)
            {
                var shape = part == 0 ? DevelopmentHeadShape() : DevelopmentBodyShape();
                int count = shape.capsule
                    ? Physics.OverlapCapsuleNonAlloc(shape.a, shape.b, shape.radius, developmentOverlaps, locomotionEnabledLayers, QueryTriggerInteraction.Ignore)
                    : Physics.OverlapSphereNonAlloc(shape.a, shape.radius, developmentOverlaps, locomotionEnabledLayers, QueryTriggerInteraction.Ignore);
                if (count == developmentOverlaps.Length) { blocked = true; return false; }
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = developmentOverlaps[i]; developmentOverlaps[i] = null;
                    if (!DevelopmentObstacle(obstacle)) continue;
                    if (!Physics.ComputePenetration(shape.collider, shape.collider.transform.position, shape.collider.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out Vector3 direction, out float depth) || depth <= .00001f) continue;
                    blocked = unresolved = true;
                    TranslateDevelopment(direction * (depth + DevelopmentSkin));
                    ClipDevelopmentVelocity(direction);
                }
            }
            if (!unresolved) return true;
        }
        return !unresolved;
    }
}
#endif
