using System.Collections.Generic;
using ExitGames.Client.Photon;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DefaultExecutionOrder(10000)] // Apply held poses after PhotonPlayer interpolates its hands.
public partial class PhotonGrabbable : Grabbable, IPunObservable, IInRoomCallbacks, IPunInstantiateMagicCallback, IPunOwnershipCallbacks, IOnPhotonViewControllerChange
{
    public static readonly List<PhotonGrabbable> HeldItems = new List<PhotonGrabbable>(24);
    bool trackedHeld, cancelledPickup;
    int holdVersion, releasedActor, releasedVersion;
    public int HeldActor => IsHeld ? PhotonNetwork.LocalPlayer.ActorNumber : remoteHeld ? remoteHolderActorNumber : 0;
    public int HoldVersion => holdVersion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHeldRegistry()
    {
        foreach (PhotonGrabbable item in HeldItems) if (item != null) item.trackedHeld = false;
        HeldItems.Clear();
    }

    void TrackHeld()
    {
        bool held = IsHeldByAnyPlayer;
        if (trackedHeld == held) return;
        trackedHeld = held;
        if (held) HeldItems.Add(this); else HeldItems.Remove(this);
    }

    public override void DropForUnavailablePlayer()
    {
        if (!IsHeld) return;
        if (PhotonNetwork.InRoom) PhotonHeldItems.Drop(this);
        else { base.DropForUnavailablePlayer(); TrackHeld(); }
    }

    public bool ApplyUnavailableDrop(int actor, int version, Vector3 position, Quaternion rotation)
    {
        // A newer hold may have been removed from a bag/head before any snapshot was sent.
        // Equal/older releases must still leave currently stored/worn items alone.
        if ((IsStashed || (headEquipment != null && headEquipment.IsEquipped)) && holdVersion >= version) return false;
        if (releasedActor == actor && releasedVersion == version) return false;
        // A reliable release can arrive before the newest unreliable held snapshot.
        if (HeldActor != 0 ? HeldActor != actor || holdVersion > version
            : holdVersion > version || (photonView.OwnerActorNr != actor && photonView.ControllerActorNr != actor)) return false;
        cancelledPickup |= pickupPending && IsHeld;
        releasedActor = actor; releasedVersion = holdVersion = version;
        if (headEquipment != null) headEquipment.CancelHeldRestore();
        ApplyRemoteStash(false);
        FinishPickupGrowth();
        CancelLocalHold();
        remoteHeld = false; remoteHolderActorNumber = 0; remoteOwner = null;
        pickupPending = false;
        _hasSamplePrev = false; _thrownIgnore = null;
        hasBeenPickedUp = true;
        dropDeadline = PhotonNetwork.Time + Mathf.Max(0f, droppedLifetimeSeconds);
        TrackHeld();
        ParentToItemContainer();
        transform.SetPositionAndRotation(position, rotation);
        PhotonHeldItems.ReturnToRoom(photonView);
        if (transformView != null) transformView.ResetNetworkState();
        SetTransformSync(true);
        // A remote held checkpoint disables collisions without a local hand to restore them.
        if (!rb.detectCollisions) { rb.detectCollisions = true; PhysicsWrites++; }
        SetBodyState(!photonView.IsMine, photonView.IsMine);
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        PrepareDropImpact();
        RecoveryChanged();
        return true;
    }

    public void OnControllerChange(Player newController, Player previousController)
    {
        recovery?.Reconcile();
        if (!photonView.IsMine && !pickupPending && IsHeld) CancelLocalOwnership();
        if (transformView != null) transformView.ResetNetworkState();
        _hasSamplePrev = false;
    }
    float droppedLifetimeSeconds = 360f;
    static readonly Unity.Profiling.ProfilerMarker PoseMarker = new("Item.Pose");
    static readonly Unity.Profiling.ProfilerMarker SwingMarker = new("Item.Swing");
    static readonly Unity.Profiling.ProfilerMarker FallbackMarker = new("Item.FallbackSearch");
    static readonly Unity.Profiling.ProfilerMarker CombatMarker = new("Item.Combat");
    public static long PoseCalls, SwingCalls, IdleSkips, SamplePoints, FallbackSearches, PhysicsWrites, SyncWrites;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetProfiling() => PoseCalls = SwingCalls = IdleSkips = SamplePoints = FallbackSearches = PhysicsWrites = SyncWrites = 0;
    readonly List<Collider> fallbackCandidates = new List<Collider>(8);
    Collider lastSampleSource;
    Matrix4x4 lastSampleMatrix;
    int lastSampleRevision;

    // Exact source-matrix comparison includes moving parents and collider child motion.
    // Retain stationary samples so the very first moving frame still sweeps from them.
    bool CanReuseSwingSamples(Collider source, Matrix4x4 matrix)
    {
        if (!_hasSamplePrev || source == null || source != lastSampleSource || _thrownIgnore != null
            || lastSampleRevision != SwingSampleRevision) return false;
        return lastSampleMatrix.Equals(matrix);
    }

    void SetBodyState(bool kinematic, bool gravity)
    {
        if (rb.isKinematic != kinematic) { rb.isKinematic = kinematic; PhysicsWrites++; }
        if (rb.useGravity != gravity) { rb.useGravity = gravity; PhysicsWrites++; }
    }

    [Tooltip("Multiplies the enemy's launch speed. Tune this per weapon.")]
    [SerializeField] float knockbackScale = 1f;
    [Tooltip("Launch speed in m/s at a full swing. 0 does not push players.")]
    [SerializeField] float playerLaunchSpeed = 0f;
    public float PlayerLaunchSpeed => playerLaunchSpeed;

    PhotonView photonView;
    PhotonTransformView transformView;
    ItemSounds itemSounds;
    HeadEquipment headEquipment;
    public bool IsHeldByAnyPlayer => IsHeld || remoteHeld;
    bool remoteHeld;
    int remoteHolderActorNumber;
    bool remoteLeftHand;
    Vector3 remoteGripPosition;
    Quaternion remoteGripRotation = Quaternion.identity;
    bool stashedLocally;
    PhotonPlayer remoteOwner;
    bool hasBeenPickedUp;
    bool callbacksRegistered;
    bool destroyAfterOwnershipFallback;
    bool keepLocalPhysics;
    double dropDeadline;
    bool pickupPending;
    public bool PickupPending => pickupPending;

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer) { }
    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        if (targetView == photonView && senderOfFailedRequest == PhotonNetwork.LocalPlayer && !photonView.IsMine)
            CancelLocalOwnership();
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView != photonView) return;
        if (cancelledPickup && photonView.IsMine)
        {
            cancelledPickup = false;
            PhotonHeldItems.ReturnToRoom(photonView);
            RecoveryChanged();
            return;
        }
        // A competitor's transfer can precede our own reliable transfer. Keep the
        // local grab/throw until ours arrives; only subsequent transfers are losses.
        if (pickupPending && !photonView.IsMine) return;
        pickupPending = false;
        if (transformView != null) transformView.ResetNetworkState();
        _hasSamplePrev = false;
        remoteOwner = null;
        if (!photonView.IsMine)
            CancelLocalOwnership();
        else if (IsHeld || !remoteHeld)
        {
            // A grab may already have been released. Never grab again or replay velocity.
            remoteHeld = false;
            remoteHolderActorNumber = 0;
        }
        TrackHeld();
        RecoveryChanged();
    }

    void CancelLocalOwnership()
    {
        pickupPending = false;
        CancelLocalHold();
        if (headEquipment != null) headEquipment.CancelLocalEquipment();
        stashedLocally = false;
        _hasSamplePrev = false;
        _thrownIgnore = null;
        if (rb != null) SetBodyState(true, false);
        SetTransformSync(!remoteHeld);
        TrackHeld();
    }

    /// <summary>Offline copies are not owned, so LateUpdate would freeze them. Keep the rigidbody simulated.</summary>
    public void KeepLocalPhysics()
    {
        keepLocalPhysics = true;
    }
    protected override bool OwnsPickupPose => photonView == null || photonView.IsMine || pickupPending;
    protected override double PickupTime => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length != 9 || !(data[0] is string tag) || tag != "pickup-pose-v1") return;
        BeginPickupGrowth((Vector3)data[1], (Vector3)data[2], (Vector3)data[3], (double)data[4]);
        if (!photonView.IsMine)
        {
            holdVersion = 1; // The spawn's immediate first grab.
            remoteHeld = true;
            remoteHolderActorNumber = (int)data[5];
            remoteLeftHand = (bool)data[6];
            remoteGripPosition = (Vector3)data[7];
            remoteGripRotation = (Quaternion)data[8];
            SetTransformSync(false);
            TrackHeld();
        }
    }

    readonly Vector3[] _sampleNow = new Vector3[Grabbable.MaxLocomotionSamples];
    readonly Vector3[] _samplePrev = new Vector3[Grabbable.MaxLocomotionSamples];
    int _samplePrevCount;
    bool _hasSamplePrev;
    Collider fallbackHitCollider;
    Vector3[] fallbackHitOffsets;
    float fallbackHitRadius;
    Enemy _thrownIgnore;
    const float SwingMotionGate = 0.25f;
    const float SwingTeleportDistance = 3f;
    const float ThrowGlanceTangent = 0.3f;
    const float ThrowGlanceBounce = 0.25f;
    const float ThrowGlanceOutward = 1.5f;
    const float ThrowGlanceMaxSpeed = 4f;
    const float ThrowGlanceSpin = 6f;
    const float ThrowSeparateSkin = 0.05f;

    protected override void Awake()
    {
        photonView = GetComponent<PhotonView>();
        transformView = GetComponent<PhotonTransformView>();
        itemSounds = GetComponent<ItemSounds>();
        headEquipment = GetComponent<HeadEquipment>();
        base.Awake();
    }

    public override bool CanSelectSurface => base.CanSelectSurface && !remoteHeld &&
        (headEquipment == null || !headEquipment.AwaitingWearerCleanup);

    protected override void OnEnable()
    {
        base.OnEnable();
        if (callbacksRegistered) return;
        PhotonNetwork.AddCallbackTarget(this);
        photonView.AddCallbackTarget(this);
        callbacksRegistered = true;
        TrackHeld(); // Re-enabled remote holds may have no further snapshots while their owner sleeps.
        if (recoveryStarted) EnsureRecovery();
    }

    protected override void OnDisable()
    {
        recovery?.Dispose(); recovery = null;
        base.OnDisable();
        pickupPending = false;
        if (trackedHeld) { HeldItems.Remove(this); trackedHeld = false; }
        _hasSamplePrev = false;
        if (!callbacksRegistered) return;
        PhotonNetwork.RemoveCallbackTarget(this);
        photonView.RemoveCallbackTarget(this);
        callbacksRegistered = false;
    }

    public override void Grab(PlayerHand hand)
    {
        if (hand == null || !hand.CanGrab || !CanBeGrabbed) return;
        if (headEquipment != null && headEquipment.AwaitingWearerCleanup) return;
        if (Holder == hand || remoteHeld) return;
        if (!PhotonMasterHandoff.LocalAvailable) return;
        cancelledPickup = false;
        unchecked { holdVersion++; }

        hasBeenPickedUp = true;
        dropDeadline = 0d;

        if (photonView != null &&
            !pickupPending &&
            PhotonNetwork.LocalPlayer != null &&
            photonView.OwnerActorNr != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            pickupPending = PhotonNetwork.InRoom;
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }

        SetTransformSync(false);
        base.Grab(hand);
        TrackHeld();
        RecoveryChanged();
        _hasSamplePrev = false;
    }

    public override void Release(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        bool wasHeld = IsHeld;
        base.Release(linearVelocity, angularVelocity);
        TrackHeld();

        if (wasHeld)
        {
            dropDeadline = headEquipment != null && headEquipment.IsEquipped
                ? 0d
                : PhotonNetwork.Time + Mathf.Max(0f, droppedLifetimeSeconds);
        }

        remoteOwner = null;
        SetTransformSync(!pickupPending && (headEquipment == null || !headEquipment.IsEquipped));
        if (wasHeld) RecoveryChanged();
    }

    public override void Stash(Transform anchor)
    {
        if (photonView != null && !photonView.IsMine)
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        base.Stash(anchor);
        stashedLocally = IsStashed;
        RecoveryChanged();
        SetTransformSync(true);
        _hasSamplePrev = false;
    }

    public void NotifyEquipmentDropped()
    {
        NotifyPlayerThrown();
    }

    /// <summary>Registers a player-launched item even when it bypasses hand pickup (desktop test throws).</summary>
    public void NotifyPlayerThrown()
    {
        if (photonView == null || !photonView.IsMine) return;
        hasBeenPickedUp = true;
        dropDeadline = PhotonNetwork.Time + Mathf.Max(0f, droppedLifetimeSeconds);
        SetTransformSync(true);
        RecoveryChanged();
    }

    public override void Unstash(Vector3 position, Quaternion rotation)
    {
        stashedLocally = false;
        base.Unstash(position, rotation);
        SetTransformSync(true);

        Transform itemParent = ItemParent();
        if (itemParent != null)
            transform.SetParent(itemParent, true);
        _hasSamplePrev = false;
        RecoveryChanged();
    }

    void ApplyRemoteStash(bool inBag)
    {
        if (inBag == IsStashed) return;
        SetStashedFlag(inBag);
        SetHidden(inBag);
    }

    void LateUpdate()
    {
        RecoverDetachedHand();
        UpdateItemPose();
        if (checkpointWhenSettled && photonView.IsMine && !IsHeldByAnyPlayer && rb.IsSleeping())
        { recovery?.Changed(); checkpointWhenSettled = false; }
        if (!destroyAfterOwnershipFallback) TickSwing();
    }

    void UpdateItemPose()
    {
        using var sample = PoseMarker.Auto();
        PoseCalls++;
        if (destroyAfterOwnershipFallback)
        {
            if (PhotonNetwork.InRoom && photonView != null && photonView.IsMine)
            {
                destroyAfterOwnershipFallback = false;
                PhotonNetwork.Destroy(gameObject);
            }
            return;
        }

        if (photonView.IsMine &&
            hasBeenPickedUp &&
            dropDeadline > 0d &&
            PhotonNetwork.Time >= dropDeadline)
        {
            dropDeadline = 0d;
            PhotonNetwork.Destroy(gameObject);
            return;
        }

        // HeadEquipment owns the worn pose and physics, regardless of network ownership.
        // Keep lifecycle handling above this branch, and let hands take precedence during transitions.
        if (!IsHeldByAnyPlayer && !IsStashed && headEquipment != null && headEquipment.IsEquipped)
        {
            SetTransformSync(false);
            return;
        }

        bool held = IsHeldByAnyPlayer;
        SetTransformSync(!held && !pickupPending);

        if (keepLocalPhysics && !PhotonNetwork.InRoom)
        {
            if (!IsHeld && !IsStashed)
            {
                SetBodyState(false, true);
            }
            return;
        }

        // Ownership came back to us while another player's bag held it (they left): drop it where it is.
        if (IsStashed && photonView.IsMine && !stashedLocally)
            Unstash(transform.position, transform.rotation);

        if (IsStashed)
        {
            SetBodyState(true, false);
            if (!photonView.IsMine)
                ParentToItemContainer();
            return;
        }

        if (photonView.IsMine || pickupPending)
        {
            if (!IsHeld)
            {
                SetBodyState(false, true);
            }
            return;
        }

        SetBodyState(true, false);

        if (IsHeld)
            return;

        if (!remoteHeld)
        {
            ParentToItemContainer();
            remoteOwner = null;
            ApplyPickupGrowth();
            return;
        }

        ParentToItemContainer();
        if (remoteOwner == null || remoteOwner.photonView == null ||
            remoteOwner.photonView.OwnerActorNr != remoteHolderActorNumber)
            remoteOwner = PhotonPlayer.ForActor(remoteHolderActorNumber);
        if (remoteOwner != null &&
            remoteOwner.TryGetHandPose(remoteLeftHand, out Vector3 handPosition, out Quaternion handRotation))
        {
            transform.SetPositionAndRotation(
                handPosition + handRotation * remoteGripPosition,
                handRotation * remoteGripRotation);
            ApplyPickupGrowth(true);
        }
    }

    void ParentToItemContainer()
    {
        Transform itemParent = ItemParent();
        if (itemParent == null)
        {
            if (transform.parent != null)
                transform.SetParent(null, true);
            return;
        }

        if (transform.parent == itemParent)
            return;

        transform.SetParent(itemParent, true);
    }

    static Transform ItemParent()
    {
        return PhotonItemManager.instance != null ? PhotonItemManager.instance.itemParent : null;
    }

    void SetTransformSync(bool enabled)
    {
        if (transformView != null && transformView.enabled != enabled)
        { transformView.enabled = enabled; SyncWrites++; }
    }

    // Equipment packets refresh this after both held and equipped state are read.
    internal void RefreshRemoteTransformSync()
    {
        SetTransformSync(!IsHeldByAnyPlayer && !pickupPending &&
            (IsStashed || headEquipment == null || !headEquipment.IsEquipped));
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        EnsureRecovery();
        if (stream.IsWriting) stream.SendNext(recovery?.WireRevision ?? RoomObjectRecovery.ZeroRevision);
        else
        {
            object header = stream.ReceiveNext();
            AcceptRecoveryPacket = recovery == null ? header is int revision && revision == 0 : recovery.Accept(header);
            if (transformView != null) transformView.AcceptNextSnapshot = AcceptRecoveryPacket;
        }
        if (stream.IsWriting)
        {
            stream.SendNext(IsHeld);
            stream.SendNext(IsHeld && PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : 0);
            stream.SendNext(IsHeld && Holder != null && Holder.IsControllerLTouch);
            stream.SendNext(holdVersion);
            stream.SendNext(hasBeenPickedUp);
            stream.SendNext(dropDeadline);
            stream.SendNext(IsStashed);
            stream.SendNext(PickupGrowing);
            if (IsHeld && Holder != null)
            {
                // Use the physics hand sent by PhotonPlayer, not the skin's HoldPoint.
                PhotonPlayer player = PhotonPlayer.Local;
                Transform hand = player != null ? player.HandSource(Holder.IsControllerLTouch) : null;
                if (hand == null) hand = Holder.transform;
                Quaternion inverse = Quaternion.Inverse(hand.rotation);
                stream.SendNext(inverse * (transform.position - PickupOffset - hand.position));
                stream.SendNext(inverse * transform.rotation);
            }
            return;
        }

        // Consume the whole packet even when it came from the previous controller.
        bool incomingHeld = (bool)stream.ReceiveNext();
        int incomingActor = (int)stream.ReceiveNext();
        bool incomingLeft = (bool)stream.ReceiveNext();
        int incomingVersion = (int)stream.ReceiveNext();
        bool incomingPickedUp = (bool)stream.ReceiveNext();
        double incomingDeadline = (double)stream.ReceiveNext();
        bool incomingStash = (bool)stream.ReceiveNext();
        bool incomingGrowth = (bool)stream.ReceiveNext();
        Vector3 grip = default;
        Quaternion rotation = Quaternion.identity;
        if (incomingHeld)
        {
            grip = (Vector3)stream.ReceiveNext();
            rotation = (Quaternion)stream.ReceiveNext();
        }
        if (!AcceptRecoveryPacket || photonView.IsMine || pickupPending ||
            (info.Sender != null && info.Sender != photonView.Controller)) return;
        if (incomingHeld && incomingActor == releasedActor && incomingVersion <= releasedVersion) return;
        bool heldChanged = remoteHeld != incomingHeld;
        remoteHeld = incomingHeld;
        remoteHolderActorNumber = incomingActor;
        holdVersion = incomingVersion;
        remoteLeftHand = incomingLeft;
        hasBeenPickedUp = incomingPickedUp;
        dropDeadline = incomingDeadline;
        ApplyRemoteStash(incomingStash);
        if (!incomingGrowth) FinishPickupGrowth();
        remoteGripPosition = grip;
        remoteGripRotation = rotation;
        // Grabbable precedes the transform in the observed stream. Accept the
        // release pose now: queued packets may include the final stationary pose
        // before LateUpdate runs, with no further updates until the item moves.
        if (heldChanged && headEquipment == null) RefreshRemoteTransformSync();
        TrackHeld();
    }

    void TickSwing()
    {
        using var sample = SwingMarker.Auto();
        SwingCalls++;
        // Growing geometry must not register as a weapon swing.
        if (PickupGrowing)
        {
            _hasSamplePrev = false;
            return;
        }
        bool observeCompanion = photonView != null && PhotonNetwork.InRoom && !photonView.IsMine;
        if (observeCompanion && !PhotonNetwork.IsMasterClient)
        {
            _hasSamplePrev = false;
            return;
        }
        if (IsStashed)
        {
            _hasSamplePrev = false;
            return;
        }

        Collider primary = SwingSampleCollider;
        Collider source = primary;
        if (source == null && fallbackHitCollider != null && fallbackHitCollider.enabled)
            source = fallbackHitCollider;
        Matrix4x4 matrix = source != null ? source.transform.localToWorldMatrix : default;
        if (CanReuseSwingSamples(source, matrix)) { IdleSkips++; return; }

        int count = primary != null ? CopySwingSampleWorldPositions(_sampleNow, matrix) : 0;
        bool companionOnly = count == 0 || observeCompanion;
        float radius = LocomotionRadius;
        if (count == 0) count = CopyCompanionHitSamples(_sampleNow, out radius);
        if (count <= 0)
        {
            _hasSamplePrev = false;
            return;
        }
        SamplePoints += count;
        lastSampleSource = primary != null ? primary : fallbackHitCollider;
        lastSampleMatrix = source == lastSampleSource ? matrix : lastSampleSource.transform.localToWorldMatrix;
        lastSampleRevision = SwingSampleRevision;
        if (!_hasSamplePrev || _samplePrevCount != count)
        {
            StoreSamples(count);
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 1e-5f)
        {
            StoreSamples(count);
            return;
        }

        float maxDistanceSq = 0f;
        for (int i = 0; i < count; i++)
        {
            float distanceSq = (_sampleNow[i] - _samplePrev[i]).sqrMagnitude;
            if (distanceSq > maxDistanceSq) maxDistanceSq = distanceSq;
        }
        if (IsHeld)
            _thrownIgnore = null;
        else if (_thrownIgnore != null && !OverlapsEnemy(_thrownIgnore, count, radius))
            _thrownIgnore = null;

        float gateDistance = SwingMotionGate * dt;
        float teleportDistanceSq = SwingTeleportDistance * SwingTeleportDistance;
        if (maxDistanceSq < gateDistance * gateDistance || maxDistanceSq > teleportDistanceSq)
        {
            StoreSamples(count);
            return;
        }

        using var combatSample = CombatMarker.Auto();
        List<Enemy> enemies = Enemy.Active;
        for (int e = 0; !companionOnly && e < enemies.Count; e++)
        {
            Enemy enemy = enemies[e];
            if (enemy == null) continue;
            if (!IsHeld && enemy == _thrownIgnore) continue;
            if (enemy.SwingLocked && (itemSounds == null || !itemSounds.HasFleshImpact)) continue;
            float minDistance = enemy.MinHitSpeed * dt;
            float minDistanceSq = minDistance * minDistance;
            bool hit = false;
            float bestSq = 0f;
            Vector3 bestDelta = default;
            Vector3 hitPosition = default;
            for (int i = 0; i < count; i++)
            {
                Vector3 delta = _sampleNow[i] - _samplePrev[i];
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq < minDistanceSq || (hit && distanceSq < bestSq)) continue;
                if (!enemy.OverlapsSwing(_samplePrev[i], _sampleNow[i], radius)) continue;
                hit = true;
                bestSq = distanceSq;
                bestDelta = delta;
                hitPosition = _sampleNow[i];
            }
            if (!hit) continue;
            if (itemSounds != null) itemSounds.PlayFleshImpact(hitPosition);
            if (enemy.SwingLocked) continue;
            enemy.LockSwing();
            Vector3 velocity = bestDelta / dt;
            hitPosition = enemy.HitBloodPosition(hitPosition - bestDelta, hitPosition, radius);
            if (IsHeld)
            {
                enemy.SubmitSwing(velocity, knockbackScale, hitPosition);
                VibrationManager.instance?.TriggerHeldItemHit(Holder);
            }
            else
            {
                enemy.SubmitThrownHit(velocity, hitPosition);
                DeflectThrown(enemy, radius, velocity);
                break;
            }
        }

        TickPlayerSwings(count, dt, radius, companionOnly);
        StoreSamples(count);
    }

    // Some items have no locomotion collider (or use a sphere/mesh). They can still
    // hit Duck. Sample their physical shape without changing human/enemy combat.
    int CopyCompanionHitSamples(Vector3[] buffer, out float radius)
    {
        radius = 0f;
        if (fallbackHitCollider == null || !fallbackHitCollider.enabled)
        {
            fallbackHitCollider = null;
            using var fallbackSample = FallbackMarker.Auto();
            FallbackSearches++;
            // Retry misses using retained storage: collider additions/enables can occur
            // without a hierarchy callback, so a permanent negative cache is unsafe.
            GetComponentsInChildren(false, fallbackCandidates);
            foreach (Collider candidate in fallbackCandidates)
            {
                if (!candidate.enabled || candidate.isTrigger || candidate.attachedRigidbody != rb) continue;
                fallbackHitOffsets = LocomotionSamples.Build(candidate, out fallbackHitRadius);
                if (fallbackHitOffsets.Length == 0)
                {
                    Vector3 center, size;
                    if (candidate is SphereCollider sphere)
                    { center = sphere.center; size = Vector3.one * (sphere.radius * 2f); }
                    else if (candidate is MeshCollider mesh && mesh.sharedMesh != null)
                    { center = mesh.sharedMesh.bounds.center; size = mesh.sharedMesh.bounds.size; }
                    else continue;
                    Vector3 scale = candidate.transform.lossyScale;
                    Vector3 worldSize = Vector3.Scale(size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    fallbackHitRadius = Mathf.Max(0.01f, Mathf.Min(Mathf.Min(worldSize.x, worldSize.y), worldSize.z) * 0.5f);
                    int axis = worldSize.y > worldSize.x ? 1 : 0;
                    if (worldSize.z > worldSize[axis]) axis = 2;
                    int samples = Mathf.Clamp(Mathf.CeilToInt(worldSize[axis] / fallbackHitRadius), 1, buffer.Length);
                    fallbackHitOffsets = new Vector3[samples];
                    float half = Mathf.Max(0, size[axis] * 0.5f - fallbackHitRadius / Mathf.Max(Mathf.Abs(scale[axis]), 0.001f));
                    for (int i = 0; i < samples; i++)
                    {
                        Vector3 point = center;
                        point[axis] += samples == 1 ? 0 : Mathf.Lerp(-half, half, i / (float)(samples - 1));
                        fallbackHitOffsets[i] = point;
                    }
                }
                fallbackHitCollider = candidate;
                break;
            }
        }
        if (fallbackHitCollider == null) return 0;
        radius = fallbackHitRadius;
        int count = Mathf.Min(buffer.Length, fallbackHitOffsets.Length);
        for (int i = 0; i < count; i++) buffer[i] = fallbackHitCollider.transform.TransformPoint(fallbackHitOffsets[i]);
        return count;
    }

    bool OverlapsEnemy(Enemy enemy, int count, float radius)
    {
        for (int i = 0; i < count; i++)
            if (enemy.OverlapsSwing(_samplePrev[i], _sampleNow[i], radius))
                return true;
        return false;
    }

    void DeflectThrown(Enemy enemy, float itemRadius, Vector3 incoming)
    {
        _thrownIgnore = enemy;
        if (rb == null || rb.isKinematic) return;

        enemy.ThrownBodyContact(rb.position, itemRadius, out Vector3 normal, out float penetration);
        if (penetration > 0f)
            rb.position += normal * (penetration + ThrowSeparateSkin);

        float into = Vector3.Dot(incoming, normal);
        Vector3 tangent = incoming - normal * into;
        float bounce = into < 0f ? -into * ThrowGlanceBounce : 0f;
        if (bounce < ThrowGlanceOutward) bounce = ThrowGlanceOutward;
        Vector3 outgoing = tangent * ThrowGlanceTangent + normal * bounce;
        if (outgoing.magnitude > ThrowGlanceMaxSpeed)
            outgoing = outgoing.normalized * ThrowGlanceMaxSpeed;
        rb.linearVelocity = outgoing;

        Vector3 spin = Vector3.Cross(normal, incoming);
        if (spin.sqrMagnitude > 1e-4f)
            rb.angularVelocity = spin.normalized * ThrowGlanceSpin;
    }

    void TickPlayerSwings(int count, float dt, float radius, bool companionOnly = false)
    {
        int ownerActor = photonView != null ? photonView.OwnerActorNr : 0;
        List<PhotonPlayer> players = PhotonPlayer.Active;
        for (int p = 0; p < players.Count; p++)
        {
            PhotonPlayer player = players[p];
            if (player == null || player.IsDead) continue;
            if (companionOnly && !player.IsCompanion) continue;
            if (player.SwingLocked && (itemSounds == null || !itemSounds.HasFleshImpact)) continue;
            PhotonView victim = player.photonView;
            if (!player.IsCompanion && victim != null && victim.OwnerActorNr == ownerActor) continue;

            float minDistance = (player.Companion != null ? player.Companion.HitReactionMinSpeed : player.MinHitSpeed) * dt;
            float minDistanceSq = minDistance * minDistance;
            bool hit = false;
            float bestSq = 0f;
            Vector3 bestDelta = default;
            Vector3 hitPosition = default;
            for (int i = 0; i < count; i++)
            {
                Vector3 delta = _sampleNow[i] - _samplePrev[i];
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq < minDistanceSq || (hit && distanceSq < bestSq)) continue;
                if (!player.OverlapsSwing(_samplePrev[i], _sampleNow[i], radius)) continue;
                hit = true;
                bestSq = distanceSq;
                bestDelta = delta;
                hitPosition = _sampleNow[i];
            }
            if (!hit) continue;

            if (itemSounds != null) itemSounds.PlayFleshImpact(hitPosition);
            if (player.IsCompanion)
            {
                SubmitCompanionHit(player, bestDelta / dt, hitPosition);
                continue;
            }
            if (player.SwingLocked) continue;

            player.LockSwing();
            if (victim == null || victim.ViewID == 0 || !PhotonNetwork.InRoom || victim.Owner == null)
                continue;
            victim.RPC(nameof(PhotonPlayer.ReceiveItemHit), victim.Owner, photonView.ViewID, bestDelta / dt, hitPosition);
            if (IsHeld)
                VibrationManager.instance?.TriggerHeldItemHit(Holder);
        }
    }

    // The master also observes remote item sweeps. A missed/late owner report must
    // not make Duck invulnerable. PhotonPlayer's hit cooldown deduplicates both paths.
    void SubmitCompanionHit(PhotonPlayer player, Vector3 velocity, Vector3 hitPosition)
    {
        if (player == null || !player.IsCompanion || player.IsDead || player.SwingLocked
            || PickupGrowing || IsStashed || !(hasBeenPickedUp || IsHeld || remoteHeld)
            || !PhotonNetwork.InRoom || photonView == null || photonView.ViewID == 0) return;
        int actor = remoteHeld ? remoteHolderActorNumber : photonView.ControllerActorNr;
        if (actor <= 0) return;
        if (player.Companion.HasAuthority)
        {
            if (!player.AcceptItemHit(actor, velocity, hitPosition, 0f)) return;
        }
        else
        {
            if (!photonView.IsMine || player.photonView.ViewID == 0) return;
            player.photonView.RPC(nameof(PhotonPlayer.ReceiveCompanionHit), RpcTarget.MasterClient, photonView.ViewID, velocity, hitPosition);
        }
        player.LockSwing();
        if (IsHeld) VibrationManager.instance?.TriggerHeldItemHit(Holder);
    }

    void OnTriggerEnter(Collider other)
    {
        // A fast thrown object can enter and bounce between render samples.
        // Physics contacts complement sweeps; held items still use the sweep as
        // their rigidbody deliberately has detectCollisions disabled.
        if (rb == null || IsHeld || rb.isKinematic || !photonView.IsMine) return;
        PhotonPlayer player = other.GetComponentInParent<PhotonPlayer>();
        if (player == null || !player.IsCompanion) return;
        Vector3 hitPosition = other.ClosestPoint(rb.worldCenterOfMass);
        SubmitCompanionHit(player, rb.GetPointVelocity(hitPosition), hitPosition);
    }

    void StoreSamples(int count)
    {
        for (int i = 0; i < count; i++)
            _samplePrev[i] = _sampleNow[i];
        _samplePrevCount = count;
        _hasSamplePrev = true;
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Availability recovery drops held items; a departure no longer destroys them.
    }

    public void OnPlayerEnteredRoom(Player newPlayer) { }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

    public void OnMasterClientSwitched(Player newMasterClient) { }
}
