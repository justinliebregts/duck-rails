using UnityEngine;

// Physics remains authoritative; only the displayed pose is interpolated.
public partial class NewPlayer
{
    void InitializeMovementPresentation()
    {
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        EnsureMovementPoseLoop();
    }

    // Lifecycle discontinuities discard presentation history, never restore a test mode.
    void OnEnable() => InitializeValues();

    void OnDisable()
    {
        ClearLaunchLock();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ResetDevelopmentMovement();
#endif
        movementRenderCount = 0;
        movementPoseRenderCancelled = true;
        if ((movementPoseQueued || movementPoseAwaitingRender) && rb != null)
            transform.SetPositionAndRotation(rb.position, rb.rotation);
        NewPlayer previous = null;
        for (var p = pendingMovementPose; !ReferenceEquals(p, null); p = p.nextMovementPose)
        {
            if (ReferenceEquals(p, this))
            {
                if (ReferenceEquals(previous, null)) pendingMovementPose = p.nextMovementPose;
                else previous.nextMovementPose = p.nextMovementPose;
                break;
            }
            previous = p;
        }
        previous = null;
        for (var p = restoredMovementPose; !ReferenceEquals(p, null); p = p.nextRestoredMovementPose)
        {
            if (ReferenceEquals(p, this))
            {
                if (ReferenceEquals(previous, null)) restoredMovementPose = p.nextRestoredMovementPose;
                else previous.nextRestoredMovementPose = p.nextRestoredMovementPose;
                break;
            }
            previous = p;
        }
        nextMovementPose = nextRestoredMovementPose = null;
        movementPoseQueued = movementPoseAwaitingRender = movementPoseSyncProtected = false;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) ClearLaunchLock();
        if (paused) movementRenderCount = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (paused) ResetDevelopmentMovement();
#endif
    }

    // A turn/carry can intentionally leave a rendered pose different from physics.
    // Unity's next interpolation reset must see the authoritative pose, including on
    // frames with no FixedUpdate. Restore at that boundary, after the previous frame
    // was rendered, rather than publishing early and visibly stepping the headset.
    static NewPlayer pendingMovementPose;
    static NewPlayer restoredMovementPose;
    static bool movementPoseLoopInstalled;
    NewPlayer nextMovementPose;
    NewPlayer nextRestoredMovementPose;
    bool movementPoseQueued;
    bool movementPoseAwaitingRender;
    bool movementPoseRenderCancelled;
    Pose movementPoseBeforeSync;
    bool movementPoseSyncProtected;
    Pose movementDisplayedPose, movementPhysicsPose;
    double movementPoseTime, movementPoseFixedTime;
    int movementPoseFrame;
    struct MovementPoseRestoreLoop { }
    struct MovementZeroStepRenderLoop { }
    struct MovementRenderSample
    {
        public Vector3 position;
        public double time;
        public bool contact;
    }
    MovementRenderSample movementRender0, movementRender1, movementRender2, movementRender3;
    int movementRenderCount;

    Vector3 MovementAnchorPosition(Vector3 position)
    {
        if (rb == null || rb.interpolation != RigidbodyInterpolation.Interpolate)
            return position;
        return rb.position + rb.rotation * (Quaternion.Inverse(transform.rotation) * (position - transform.position));
    }

    void ShiftMovementRenderHistory(Vector3 delta)
    {
        if (movementRenderCount == 0) return;
        movementRender0.position += delta; movementRender1.position += delta;
        movementRender2.position += delta; movementRender3.position += delta;
    }

    Vector3 ContactRenderPosition(Vector3 nativePosition)
    {
        double now = Time.timeAsDouble;
        if (movementRenderCount == 0 || now > movementRender0.time)
        {
            movementRender3 = movementRender2; movementRender2 = movementRender1;
            movementRender1 = movementRender0;
            movementRenderCount = Mathf.Min(movementRenderCount + 1, 4);
        }
        movementRender0 = new MovementRenderSample {
            position = rb.position, time = now, contact = leftHandColliding || rightHandColliding
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                || developmentMoving
#endif
        };
        if (movementRenderCount < 2) return nativePosition;
        double target = now - Time.fixedDeltaTime;
        var newer = movementRender0;
        bool contact = newer.contact;
        for (int i = 1; i < movementRenderCount; i++)
        {
            var older = i == 1 ? movementRender1 : i == 2 ? movementRender2 : movementRender3;
            contact |= older.contact;
            if (target >= older.time || i == movementRenderCount - 1)
            {
                if (!contact) return nativePosition;
                // Native interpolation cannot see hand corrections made between
                // physics steps. Interpolate the accepted poses at the same one-step
                // display delay, including the transition out of contact. Four value
                // samples cover 72/144 Hz without allocation or extrapolation.
                float fraction = newer.time > older.time
                    ? Mathf.Clamp01((float)((target - older.time) / (newer.time - older.time))) : 1f;
                return Vector3.LerpUnclamped(older.position, newer.position, fraction);
            }
            newer = older;
        }
        return nativePosition;
    }

    // Collision anchors and launch history belong to physics, never to the older
    // rendered Transform. Keep native interpolation as the free-motion baseline
    // and blend this frame's accepted solver correction into its current endpoint.
    bool BeginPhysicalMovement(out Pose displayed, out Vector3 physicsStart)
    {
        displayed = default;
        physicsStart = default;
        if (rb == null || rb.interpolation != RigidbodyInterpolation.Interpolate)
            return false;
        displayed = new Pose(transform.position, transform.rotation);
        physicsStart = rb.position;
        // Copy the authoritative pose without republishing the body. With the offset
        // compound colliders, repeated PublishTransform calls accumulated tiny pose
        // drift that could tip a planted hand's cast across its contact threshold.
        transform.SetPositionAndRotation(physicsStart, rb.rotation);
        return true;
    }

    void EndPhysicalMovement(Pose displayed, Vector3 physicsStart, bool blockingCollision)
    {
        // A head/body safety correction must be visible immediately. Blending an
        // older root with today's tracked head could otherwise show through a wall.
        if (blockingCollision)
        {
            movementRenderCount = 0;
            QueueMovementPoseRestore();
            return;
        }
        Vector3 correction = rb.position - physicsStart;
        // Script-driven simulation has no automatic interpolation clock. Keep its
        // explicit physical pose; the real-loop checks exercise the render path.
        float fraction = Physics.simulationMode == SimulationMode.FixedUpdate && Time.fixedDeltaTime > 0f
            ? Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime)) : 1f;
        Vector3 position = displayed.position + correction * fraction;
        if (Physics.simulationMode == SimulationMode.FixedUpdate)
            position = ContactRenderPosition(position);
        transform.SetPositionAndRotation(position, displayed.rotation);
        QueueMovementPoseRestore();
    }

    void QueueMovementPoseRestore()
    {
        if (rb == null || rb.interpolation == RigidbodyInterpolation.None
            || movementPoseQueued) return;
        EnsureMovementPoseLoop();
        movementPoseQueued = true;
        movementPoseTime = Time.timeAsDouble;
        movementPoseFixedTime = Time.fixedTimeAsDouble;
        movementPoseFrame = Time.frameCount;
        nextMovementPose = pendingMovementPose;
        pendingMovementPose = this;
    }

    static void EnsureMovementPoseLoop()
    {
        if (!movementPoseLoopInstalled)
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            bool restore = InstallMovementPoseLoop(ref loop,
                typeof(UnityEngine.PlayerLoop.EarlyUpdate.PhysicsResetInterpolatedTransformPosition),
                typeof(MovementPoseRestoreLoop), RestoreMovementPoses, true);
            bool render = InstallMovementPoseLoop(ref loop,
                typeof(UnityEngine.PlayerLoop.PreUpdate.PhysicsUpdate),
                typeof(MovementZeroStepRenderLoop), RestoreZeroStepRenderedPoses, false);
            if (!restore || !render)
                throw new System.InvalidOperationException("Movement interpolation reset stage not found.");
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
            movementPoseLoopInstalled = true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMovementPoseState()
    {
        RestoreMovementPoses();
        while (!ReferenceEquals(restoredMovementPose, null))
        {
            var p = restoredMovementPose;
            restoredMovementPose = p.nextRestoredMovementPose;
            p.nextRestoredMovementPose = null;
            p.movementPoseAwaitingRender = false;
        }
        movementPoseLoopInstalled = false;
    }

    static bool InstallMovementPoseLoop(ref UnityEngine.LowLevel.PlayerLoopSystem loop,
        System.Type anchor, System.Type marker, UnityEngine.LowLevel.PlayerLoopSystem.UpdateFunction update, bool before)
    {
        var children = loop.subSystemList;
        if (children == null) return false;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].type == marker) return true;
            // When inserting after an anchor, check for an existing adjacent marker first.
            if (children[i].type == anchor)
            {
                if (!before && i + 1 < children.Length && children[i + 1].type == marker) return true;
                int at = before ? i : i + 1;
                var updated = new UnityEngine.LowLevel.PlayerLoopSystem[children.Length + 1];
                System.Array.Copy(children, 0, updated, 0, at);
                updated[at] = new UnityEngine.LowLevel.PlayerLoopSystem {
                    type = marker, updateDelegate = update
                };
                System.Array.Copy(children, at, updated, at + 1, children.Length - at);
                loop.subSystemList = updated;
                return true;
            }
            if (InstallMovementPoseLoop(ref children[i], anchor, marker, update, before)) return true;
        }
        return false;
    }

    static void RestoreMovementPoses()
    {
        while (!ReferenceEquals(pendingMovementPose, null))
        {
            var player = pendingMovementPose;
            pendingMovementPose = player.nextMovementPose;
            player.nextMovementPose = null;
            player.movementPoseQueued = false;
            // Read the current Rigidbody pose: a later hand correction or warp wins.
            if (player != null && player.rb != null
                && player.rb.interpolation != RigidbodyInterpolation.None)
            {
                player.movementDisplayedPose = new Pose(player.transform.position, player.transform.rotation);
                player.movementPhysicsPose = new Pose(player.rb.position, player.rb.rotation);
                player.movementPoseRenderCancelled = false;
                player.transform.SetPositionAndRotation(player.movementPhysicsPose.position, player.movementPhysicsPose.rotation);
                if (!player.movementPoseAwaitingRender)
                {
                    player.nextRestoredMovementPose = restoredMovementPose;
                    restoredMovementPose = player;
                    player.movementPoseAwaitingRender = true;
                }
            }
        }
    }

    static void RestoreZeroStepRenderedPoses()
    {
        while (!ReferenceEquals(restoredMovementPose, null))
        {
            var p = restoredMovementPose;
            restoredMovementPose = p.nextRestoredMovementPose;
            p.nextRestoredMovementPose = null;
            p.movementPoseAwaitingRender = false;
            if (p == null || p.rb == null
                || p.rb.interpolation != RigidbodyInterpolation.Interpolate
                || Time.fixedTimeAsDouble != p.movementPoseFixedTime
                || Time.frameCount == p.movementPoseFrame || p.movementPoseRenderCancelled) continue;
            double elapsed = Time.timeAsDouble - p.movementPoseTime;
            double remaining = p.movementPoseFixedTime + Time.fixedDeltaTime - p.movementPoseTime;
            if (elapsed < 0 || remaining <= 0) continue;
            // A modified body can suppress Unity's interpolation on a zero-step
            // frame. Advance only between its already-known display/physics endpoints.
            // A real physics step leaves rendering entirely to native interpolation.
            float fraction = Mathf.Clamp01((float)(elapsed / remaining));
            p.transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(p.movementDisplayedPose.position, p.movementPhysicsPose.position, fraction),
                Quaternion.SlerpUnclamped(p.movementDisplayedPose.rotation, p.movementPhysicsPose.rotation, fraction));
            p.rb.position = p.movementPhysicsPose.position;
            p.rb.rotation = p.movementPhysicsPose.rotation;
            p.QueueMovementPoseRestore();
        }
    }

    // Replace existing global sync sites, never add another sync. Protect only bodies
    // whose two poses differ intentionally; return their displayed poses afterward.
    public static void SyncTransformsPreservingMovementPose()
    {
        for (var p = pendingMovementPose; !ReferenceEquals(p, null); p = p.nextMovementPose)
        {
            p.movementPoseSyncProtected = p != null && p.rb != null
                && p.rb.interpolation != RigidbodyInterpolation.None;
            if (!p.movementPoseSyncProtected) continue;
            p.movementPoseBeforeSync = new Pose(p.transform.position, p.transform.rotation);
            p.transform.SetPositionAndRotation(p.rb.position, p.rb.rotation);
        }
        try { Physics.SyncTransforms(); }
        finally
        {
            for (var p = pendingMovementPose; !ReferenceEquals(p, null); p = p.nextMovementPose)
            {
                if (!p.movementPoseSyncProtected) continue;
                p.transform.SetPositionAndRotation(p.movementPoseBeforeSync.position, p.movementPoseBeforeSync.rotation);
                p.movementPoseSyncProtected = false;
            }
        }
    }
}
