using System;
using System.Collections.Generic;
using Game.Audio;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public enum BagState : byte { Stowed, Held, Dropped }

// One bag in the world. Bags are room objects (the master spawns them, see RoomRpc.SpawnBag) so they outlive whoever bought them.
// Whoever holds or wears a bag owns its PhotonView and is the only one who changes it. Nobody can take a bag that is held
// or on a back: the owner only grants ownership requests while the bag is dropped.
// A slot keeps the item's prefab name, not the object. Putting an item in destroys it, taking it out spawns a new one.
// Runs after the bodies are posed so a bag on a back follows the chest without lag.
[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(PhotonView), typeof(Rigidbody))]
public partial class BagObject : MonoBehaviourPunCallbacks, IPunObservable, IPunOwnershipCallbacks, IOnPhotonViewControllerChange, IHandLocomotionShape
{
    public enum Target : byte { LeftHand, RightHand, Back, Dropped }

    public static readonly List<BagObject> Active = new List<BagObject>();
    public static event Action<BagObject> OwnershipChanged;

    [Header("Back")]
    [Tooltip("Where the bag sits on a back, in the wearer's chest space (chest faces where the head looks, without tilt).")]
    [SerializeField] Vector3 backOffset = new Vector3(0f, -0.12f, -0.22f);
    [SerializeField] Vector3 backEuler;

    [Header("Slots")]
    [Tooltip("An item's longest side in a slot, before its BagItem Mini Scale.")]
    [SerializeField] float slotSize = DefaultSlotSize;
    [Tooltip("Slots and the items in them only show when the local player is this close.")]
    [SerializeField] float slotShowDistance = 10f;

    [Header("Dropped")]
    [Tooltip("A dropped bag nobody picks up for this long is removed.")]
    [SerializeField] float droppedLifetimeSeconds = 600f;
    [SerializeField] float remoteFollowSharpness = 15f;
    [Tooltip("How quickly someone else's bag flies toward your hand while you wait for them to hand it over.")]
    [SerializeField] float pullSharpness = 20f;

    [Header("Audio")]
    [SerializeField] SoundEvent grabSound;
    [SerializeField] SoundEvent stowSound;
    ItemSounds impactSounds;
    [SerializeField] SoundEvent putSound;
    [Tooltip("Played when a hand starts targeting a slot.")]
    [SerializeField] SoundEvent hoverSound;
    SoundEvent grabOverride, stowOverride, putOverride, hoverOverride;
    SoundEvent GrabSound => grabOverride != null ? grabOverride : grabSound;
    SoundEvent StowSound => stowOverride != null ? stowOverride : stowSound;

    public const float DefaultSlotSize = 0.065f;
    const float SlotCheckInterval = 1f;
    const float UnclaimedTimeout = 10f;
    const float RemoteSnapDistance = 2f;

    static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    readonly List<BagSlot> slots = new List<BagSlot>();
    BagVisuals visuals;
    Rigidbody rb;
    BoxCollider box;
    PlayerHand holdingHand;
    PlayerHand pullHand;
    Vector3[] locoSamples = Array.Empty<Vector3>();
    float locoRadius;
    Transform grabLeft;
    Transform grabRight;
    byte bagId;

    BagState state = BagState.Dropped;
    int holderActor;
    bool heldLeft;
    Vector3 heldOffsetPos;
    Quaternion heldOffsetRot = Quaternion.identity;
    Vector3 netPos;
    Quaternion netRot = Quaternion.identity;
    bool netPoseReceived;
    double droppedAt;
    bool stateReceived;

    int requester;
    int requestLife;
    int requestHandEpoch;
    Target requestedTarget;
    bool awaitingClaim;
    bool claimCancelled;
    bool cancelledClaimGrant;
    bool contentsAuthorityKnown;
    float spawnTime;

    Transform localBackAnchor;
    bool localBackAnchorIsChest;
    Vector3 prevPosition;
    Vector3 velocity;

    int grantedTo;
    bool orphaned;
    bool lastControl;
    int holdVersion, releasedActor, releasedVersion;
    public int HoldVersion => holdVersion;

    bool slotsShown = true;
    bool near;
    float nextNearCheck;

    public byte BagId => bagId;
    public BagState State => state;
    public int HolderActor => holderActor;
    public bool HeldInLeftHand => heldLeft;
    public IReadOnlyList<BagSlot> Slots => slots;
    public float SlotSize => slotSize;
    public SoundEvent HoverSound => hoverOverride != null ? hoverOverride : hoverSound;
    public SoundEvent PutSound => putOverride != null ? putOverride : putSound;
    public Vector3 Velocity => velocity;
    public bool AwaitingClaim => awaitingClaim;
    public bool IsCurrentLocalClaim => requester == LocalActor && requestLife == BagInventory.Life
        && ((requestedTarget != Target.LeftHand && requestedTarget != Target.RightHand) || requestHandEpoch == BagInventory.HandClaimEpoch);
    public bool PendingHandClaim => awaitingClaim && (requestedTarget == Target.LeftHand || requestedTarget == Target.RightHand);
    public int DropActor => state == BagState.Held ? holderActor : awaitingClaim ? requester : photonView.OwnerActorNr;

    /// <summary>True when this client may change the bag: its owner, or anyone when offline.</summary>
    public bool CanControl => !PhotonNetwork.InRoom || photonView.IsMine;

    // Reserve a granted handoff until it resolves. Authority still
    // simulates the dropped bag, but cannot pick it back up during the transfer.
    bool GrantPending => grantedTo != 0;
    public bool CanInteract => CanControl && !GrantPending;

    public static int LocalActor => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

    static double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    /// <summary>Raised on the owner when the slot contents change.</summary>
    public event Action<BagObject> ContentsChanged;

    // ---------- Spawn ----------

    /// <summary>Instantiation data: bag id, slot keys, requester, target, and requester's life at request time.</summary>
    public static object[] SpawnData(byte bagId, string[] keys, int requester, Target target, int life, int handEpoch = -1) =>
        new object[] { bagId, keys ?? new string[0], requester, (byte)target, life, handEpoch < 0 ? BagInventory.HandClaimEpoch : handEpoch };

    void Awake()
    {
        impactSounds = GetComponent<ItemSounds>();
        rb = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();
        visuals = BagVisuals.Attach(gameObject);
        spawnTime = Time.unscaledTime;

        object[] data = photonView != null ? photonView.InstantiationData : null;
        if (data != null)
            Init(data);
    }

    /// <summary>Sets up a bag spawned without Photon (offline). Online bags read the same data from instantiation.</summary>
    public void Init(object[] data)
    {
        bagId = data.Length > 0 && data[0] is byte id ? id : (byte)0;
        string[] keys = data.Length > 1 ? data[1] as string[] : null;
        requester = data.Length > 2 && data[2] is int actor ? actor : 0;
        requestLife = data.Length > 4 && data[4] is int life ? life : -1;
        requestHandEpoch = data.Length > 5 && data[5] is int epoch ? epoch : 0;
        requestedTarget = data.Length > 3 && data[3] is byte t ? (Target)t : Target.Dropped;
        awaitingClaim = requestedTarget != Target.Dropped;

        UseLook();
        ApplyKeys(keys);
        if (!awaitingClaim)
            droppedAt = Now;
        ApplyPhysics();
        DuckzLog.Log($"[Bag] Spawned bag {bagId}, view {photonView.ViewID} ({slots.Count} slots) for actor {requester}, target {requestedTarget}: [{string.Join(", ", Keys())}]", this);
    }

    void UseLook()
    {
        ApplyAudioOverrides();
        visuals.Apply(bagId);
        slots.Clear();
        slots.AddRange(visuals.Slots);
        grabLeft = visuals.GrabLeft;
        grabRight = visuals.GrabRight;
        FitCollider();
        SetSlotsShown(slotsShown, true);
    }

    void ApplyAudioOverrides()
    {
        // Resolve once per configuration, including clearing overrides when reusing a bag.
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        StoreItem item = catalog != null ? catalog.FindBag(bagId) : null;
        grabOverride = item?.bagGrabOverride;
        stowOverride = item?.bagStowOverride;
        putOverride = item?.bagPutOverride;
        hoverOverride = item?.bagHoverOverride;
        if (impactSounds != null) impactSounds.SetDropOverride(item?.bagDropOverride);
    }

    // Cancellation survives a delayed ownership transfer, including a respawn after death.
    public void CancelIncomingClaim()
    {
        if (!awaitingClaim || requester != LocalActor) return;
        claimCancelled = true;
        if (CanControl)
        {
            if (PhotonNetwork.InRoom) PhotonHeldItems.Drop(this);
            else DropLocal(Vector3.zero);
        }
    }

    public void CancelIncomingHandClaim()
    {
        if (requestedTarget == Target.LeftHand || requestedTarget == Target.RightHand) CancelIncomingClaim();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        photonView.AddCallbackTarget(this);
        photonView.OwnershipTransferRejected += OnTransferRejected;
        Active.Add(this);
        if (recoveryStarted) EnsureRecovery();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        photonView.RemoveCallbackTarget(this);
        photonView.OwnershipTransferRejected -= OnTransferRejected;
        Active.Remove(this);
        recovery?.Dispose(); recovery = null;
    }

    // ---------- Local actions (owner only) ----------

    public void HoldLocal(PlayerHand hand)
    {
        if (!CanInteract || hand == null || !PhotonMasterHandoff.LocalAvailable) return;
        unchecked { holdVersion++; }
        awaitingClaim = false;
        SetState(BagState.Held, LocalActor, hand.IsControllerLTouch);
        transform.SetParent(null, true);
        ApplyPhysics();
        Transform hold = hand.HoldPoint;
        PlaceAtGrab(transform, GrabFor(heldLeft), hold);
        transform.SetParent(hold, true);
        prevPosition = transform.position;
        velocity = Vector3.zero;
        holdingHand = hand;
        if (impactSounds != null) impactSounds.ResetSwingState();
        hand.HoldShape(this);
        Play(GrabSound);
        RecoveryChanged();
    }

    public bool TryGetLocomotionSamples(PlayerHand hand, Vector3[] worldBuffer, out int count, out float radius)
    {
        count = 0;
        radius = locoRadius;
        if (!this || hand != holdingHand || state != BagState.Held || !CanControl || worldBuffer == null)
            return false;
        count = Mathf.Min(locoSamples.Length, worldBuffer.Length);
        for (int i = 0; i < count; i++)
            worldBuffer[i] = transform.TransformPoint(locoSamples[i]);
        return count > 0;
    }

    /// <summary>Puts the bag on the local player's back. With a chest, the bag sits at its back offset from it; otherwise exactly on the anchor.</summary>
    public void StowLocal(Transform anchor, bool anchorIsChest)
    {
        if (!CanInteract || anchor == null) return;
        if (PhotonNetwork.InRoom && photonView.OwnerActorNr != LocalActor)
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        awaitingClaim = false;
        SetState(BagState.Stowed, LocalActor, false);
        transform.SetParent(null, true);
        localBackAnchor = anchor;
        localBackAnchorIsChest = anchorIsChest;
        ApplyPhysics();
        PinToBack(anchor, anchorIsChest);
        Play(StowSound);
        RecoveryChanged();
    }

    public void DropLocal(Vector3 throwVelocity)
    {
        if (!CanControl) return;
        bool wasHeld = state == BagState.Held && CanControl;
        awaitingClaim = false;
        transform.SetParent(null, true);
        SetState(BagState.Dropped, 0, false);
        localBackAnchor = null;
        droppedAt = Now;
        ApplyPhysics();
        if (!rb.isKinematic)
        {
            rb.linearVelocity = throwVelocity;
            rb.angularVelocity = Vector3.zero;
        }
        if (impactSounds != null) impactSounds.PrepareRelease();
        if (wasHeld && impactSounds != null) impactSounds.PlayThrow(throwVelocity);
        RecoveryChanged();
    }

    public bool ApplyUnavailableDrop(int actor, int version, Vector3 position, Quaternion rotation)
    {
        if (state == BagState.Stowed && holdVersion >= version) return false;
        if (releasedActor == actor && releasedVersion == version)
        {
            // A late grant can restore the sleeping owner after the original drop.
            // Repair only that ownership; never replay the drop pose or its lifetime.
            if (state != BagState.Dropped || awaitingClaim || holdVersion != version || photonView.OwnerActorNr != actor) return false;
            grantedTo = 0;
            PhotonHeldItems.ReturnToRoom(photonView);
            ApplyPhysics();
            OwnershipChanged?.Invoke(this);
            RecoveryChanged();
            return true;
        }
        // A reliable release can arrive before the newest unreliable held snapshot.
        if (state == BagState.Held ? holderActor != actor || holdVersion > version
            : holdVersion > version || (!awaitingClaim || requester != actor) && (photonView.OwnerActorNr != actor && photonView.ControllerActorNr != actor)) return false;
        releasedActor = actor; releasedVersion = holdVersion = version;
        contentsAuthorityKnown = true;
        cancelledClaimGrant |= awaitingClaim && requester == LocalActor && !CanControl;
        grantedTo = 0; orphaned = false;
        claimCancelled = true; awaitingClaim = false;
        SetState(BagState.Dropped, 0, false);
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        netPos = position; netRot = rotation; netPoseReceived = true;
        droppedAt = Now;
        PhotonHeldItems.ReturnToRoom(photonView);
        ApplyPhysics();
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (impactSounds != null) impactSounds.PrepareRelease();
        // Slots/contents are deliberately untouched. Also clear stale inventory hand references.
        OwnershipChanged?.Invoke(this);
        RecoveryChanged();
        return true;
    }

    void SetState(BagState newState, int actor, bool left)
    {
        if (newState != BagState.Dropped && impactSounds != null)
            impactSounds.ResetImpactState();
        state = newState;
        holderActor = actor;
        heldLeft = left;
        holdingHand = null;
        pullHand = null;
        if (newState != BagState.Stowed)
            localBackAnchor = null;
        SetSlotsShown(newState != BagState.Stowed && near, false);
    }

    void ApplyPhysics()
    {
        if (rb == null)
            return;
        bool simulate = state == BagState.Dropped && CanControl && !awaitingClaim;
        if (!rb.isKinematic && !simulate)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = !simulate;
        rb.useGravity = simulate;
        rb.detectCollisions = state == BagState.Dropped;
        lastControl = CanControl;
    }

    // ---------- Ownership ----------

    /// <summary>Asks the owner for this bag. Granted only while it is dropped. Returns true if it is already ours.</summary>
    public bool RequestControl()
    {
        cancelledClaimGrant = false; // A new deliberate pickup supersedes the cancelled delivery.
        if (CanControl)
        {
            if (GrantPending) return false;
            if (PhotonNetwork.InRoom && photonView.OwnerActorNr != LocalActor)
                photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
            return true;
        }
        photonView.RequestOwnership();
        return false;
    }

    /// <summary>While waiting for the owner to hand this bag over, shows it flying into hand on this screen only.</summary>
    public void PullToward(PlayerHand hand) => pullHand = hand;
    public void CancelLocalPull() => pullHand = null;

    public void StopPull(PlayerHand hand)
    {
        if (pullHand == hand)
            pullHand = null;
    }

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        if (targetView != photonView || !photonView.AmController || requestingPlayer == null)
            return;

        bool granting = GrantPending;
        if (state != BagState.Dropped || awaitingClaim || granting)
        {
            return;
        }

        grantedTo = requestingPlayer.ActorNumber;
        DuckzLog.Log($"[Bag] Giving dropped bag {bagId} to {requestingPlayer.NickName}#{requestingPlayer.ActorNumber}", this);
        photonView.TransferOwnership(requestingPlayer);
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView != photonView)
            return;
        contentsAuthorityKnown = true;
        if (cancelledClaimGrant && CanControl)
        {
            cancelledClaimGrant = false;
            unchecked { holdVersion++; }
            PhotonHeldItems.Drop(this);
            PhotonNetwork.SendAllOutgoingCommands();
            return;
        }
        // A room object's earlier self-transfer can echo while our outgoing
        // grant is in flight. That echo must not unlock the reservation.
        if (!CanControl || photonView.OwnerActorNr == grantedTo)
            grantedTo = 0;
        if (awaitingClaim)
            DuckzLog.Log($"[Bag] Claim ownership arrived: view {photonView.ViewID}, requester {requester}, controller {photonView.ControllerActorNr}", this);
        if (impactSounds != null) impactSounds.ResetImpactState();
        recovery?.Reconcile();
        ReconcileOwnership();
        ApplyPhysics();
        OwnershipChanged?.Invoke(this);
        RecoveryChanged();
    }

    void ReconcileOwnership()
    {
        if (!CanControl)
        {
            transform.SetParent(null, true);
            holdingHand = null;
            localBackAnchor = null;
            return;
        }

        // A received worn/held snapshot has no local attachment. Recover before
        // pending pickup processing so a valid waiting grab still completes now.
        // Preserve live local attachments and the separate spawn delivery path.
        if (awaitingClaim)
        {
            if (claimCancelled)
            {
                if (PhotonNetwork.InRoom) PhotonHeldItems.Drop(this);
                else DropLocal(Vector3.zero);
            }
            return;
        }
        if (state == BagState.Stowed)
        {
            BagInventory.Local?.ReattachRecoveredBack(this);
            return;
        }
        if (state != BagState.Dropped &&
            (holderActor != LocalActor ||
             (state == BagState.Stowed ? localBackAnchor == null : holdingHand == null)))
            DropLocal(Vector3.zero);
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        if (targetView == photonView && senderOfFailedRequest != null &&
            senderOfFailedRequest.ActorNumber == grantedTo)
            grantedTo = 0;
    }

    void OnTransferRejected(int recipientActor, int senderActor)
    {
        if (recipientActor == grantedTo && PhotonNetwork.LocalPlayer != null &&
            senderActor == PhotonNetwork.LocalPlayer.ActorNumber)
            grantedTo = 0;
    }

    public void OnControllerChange(Player newController, Player previousController)
    {
        recovery?.Reconcile();
        BagInventory.Local?.ReattachRecoveredBack(this);
        // Control may change without an ownership-transfer callback (master
        // migration, disconnect, rejoin). A former controller's grant is invalid.
        // Do not reset on gain/self echoes: they may precede our outgoing grant.
        if (!photonView.IsMine) grantedTo = 0;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (recovery == null && CanControl && newPlayer != null)
            photonView.RPC(nameof(RpcContents), newPlayer, Keys(), false, photonView.SpawnToken);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer != null && otherPlayer.ActorNumber == grantedTo)
            grantedTo = 0;
        if (otherPlayer != null && otherPlayer.ActorNumber == holderActor && state != BagState.Dropped)
            orphaned = true;
    }

    // ---------- Frame ----------

    void LateUpdate()
    {
        RecoverDetachedHand();
        bool control = CanControl;
        if (control != lastControl)
            ApplyPhysics();

        if (control)
            UpdateOwned();
        else
            UpdateRemote();

        UpdateSlotVisibility();
        if (checkpointWhenSettled && CanControl && state == BagState.Dropped && rb.IsSleeping())
        { recovery?.Changed(); checkpointWhenSettled = false; }
    }

    void UpdateOwned()
    {
        // The wearer or holder left and the master took over. A worn bag is saved with its wearer; a held one falls.
        if (orphaned)
        {
            orphaned = false;
            if (state == BagState.Stowed && PhotonNetwork.InRoom)
            {
                DuckzLog.Log($"[Bag] Wearer #{holderActor} left with bag {bagId} on their back; it went with them", this);
                PhotonNetwork.Destroy(gameObject);
                return;
            }
            if (state == BagState.Held)
            {
                DuckzLog.Log($"[Bag] Holder #{holderActor} left holding bag {bagId}; dropping it", this);
                if (PhotonNetwork.InRoom) PhotonHeldItems.Drop(this);
                else DropLocal(Vector3.zero);
            }
        }

        if (awaitingClaim)
        {
            // A valid saved backpack can attach while the OS menu is open; only hands require focus.
            if (claimCancelled || (requester == LocalActor && (!IsCurrentLocalClaim || (PendingHandClaim && !PhotonMasterHandoff.LocalAvailable))) ||
                Time.unscaledTime - spawnTime > UnclaimedTimeout)
            {
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write($"[Bag] Resolving unclaimed bag {bagId}, view {photonView.ViewID}, requester {requester}, cancelled {claimCancelled}; dropping it", this);
                }
                if (PhotonNetwork.InRoom) PhotonHeldItems.Drop(this);
                else DropLocal(Vector3.zero);
            }
            else if (requester == LocalActor && BagInventory.Local != null)
                BagInventory.Local.Receive(this, requestedTarget);
        }

        switch (state)
        {
            case BagState.Held:
                float dt = Time.deltaTime;
                if (dt > 1e-5f)
                    velocity = Vector3.Lerp(velocity, (transform.position - prevPosition) / dt, 0.5f);
                prevPosition = transform.position;
                break;
            case BagState.Stowed:
                if (localBackAnchor != null)
                    PinToBack(localBackAnchor, localBackAnchorIsChest);
                break;
            case BagState.Dropped:
                if (!awaitingClaim && droppedAt > 0d && Now - droppedAt > droppedLifetimeSeconds)
                {
                    DuckzLog.Log($"[Bag] Dropped bag {bagId} sat untouched for {droppedLifetimeSeconds:0}s; removing it", this);
                    if (PhotonNetwork.InRoom)
                        PhotonNetwork.Destroy(gameObject);
                    else
                        Destroy(gameObject);
                }
                break;
        }
    }

    void UpdateRemote()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);

        PhotonPlayer holder = state != BagState.Dropped ? PhotonPlayer.ForActor(holderActor) : null;
        switch (state)
        {
            case BagState.Stowed:
                if (holder != null && holder.Chest != null)
                    PinToBack(holder.Chest, true);
                break;
            case BagState.Held:
                if (holder != null && holder.TryGetHandPose(heldLeft, out Vector3 handPos, out Quaternion handRot))
                    transform.SetPositionAndRotation(handPos + handRot * heldOffsetPos, handRot * heldOffsetRot);
                break;
            case BagState.Dropped:
                if (pullHand != null && pullHand.isActiveAndEnabled)
                {
                    GrabPose(transform, GrabFor(pullHand.IsControllerLTouch), pullHand.HoldPoint, out Vector3 pullPos, out Quaternion pullRot);
                    float kp = 1f - Mathf.Exp(-pullSharpness * Time.deltaTime);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(transform.position, pullPos, kp),
                        Quaternion.Slerp(transform.rotation, pullRot, kp));
                    break;
                }
                if (!netPoseReceived)
                    break;
                if ((transform.position - netPos).sqrMagnitude > RemoteSnapDistance * RemoteSnapDistance)
                    transform.SetPositionAndRotation(netPos, netRot);
                else
                {
                    float k = 1f - Mathf.Exp(-remoteFollowSharpness * Time.deltaTime);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(transform.position, netPos, k),
                        Quaternion.Slerp(transform.rotation, netRot, k));
                }
                break;
        }
    }

    void PinToBack(Transform anchor, bool isChest)
    {
        if (anchor == null)
            return;
        if (isChest)
            transform.SetPositionAndRotation(anchor.TransformPoint(backOffset), anchor.rotation * Quaternion.Euler(backEuler));
        else
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    // ---------- Network ----------

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        EnsureRecovery();
        bool current = true;
        if (stream.IsWriting) stream.SendNext(recovery?.WireRevision ?? RoomObjectRecovery.ZeroRevision);
        else
        {
            object header = stream.ReceiveNext();
            current = recovery == null ? header is int revision && revision == 0 : recovery.Accept(header);
        }
        if (stream.IsWriting)
        {
            stream.SendNext((byte)state);
            stream.SendNext(holderActor);
            stream.SendNext(holdVersion);
            if (state == BagState.Held)
            {
                MeasureHeldOffset();
                stream.SendNext(heldLeft);
                stream.SendNext(heldOffsetPos);
                stream.SendNext(heldOffsetRot);
            }
            else if (state == BagState.Dropped)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
                stream.SendNext(droppedAt);
                stream.SendNext(awaitingClaim);
            }
            return;
        }

        // Ownership and pose messages can arrive at different times. Never let an old
        // controller's snapshot undo a local claim or overwrite the current authority.
        // Consume our fields even when rejecting them, preserving the stream layout.
        bool accept = current && !CanControl && info.Sender == photonView.Controller;
        var newState = (BagState)(byte)stream.ReceiveNext();
        int newHolder = (int)stream.ReceiveNext();
        int newVersion = (int)stream.ReceiveNext();
        bool newLeft = false;
        Vector3 newOffsetPos = Vector3.zero;
        Quaternion newOffsetRot = Quaternion.identity;
        Vector3 newPos = Vector3.zero;
        Quaternion newRot = Quaternion.identity;
        double newDroppedAt = 0d;
        bool newAwaitingClaim = false;
        if (newState == BagState.Held)
        {
            newLeft = (bool)stream.ReceiveNext();
            newOffsetPos = (Vector3)stream.ReceiveNext();
            newOffsetRot = (Quaternion)stream.ReceiveNext();
        }
        else if (newState == BagState.Dropped)
        {
            newPos = (Vector3)stream.ReceiveNext();
            newRot = (Quaternion)stream.ReceiveNext();
            newDroppedAt = (double)stream.ReceiveNext();
            newAwaitingClaim = (bool)stream.ReceiveNext();
        }
        if (!accept) return;
        contentsAuthorityKnown = true;
        if (newVersion < holdVersion || (newVersion <= releasedVersion &&
            ((newState != BagState.Dropped && newHolder == releasedActor) || (newAwaitingClaim && requester == releasedActor)))) return;
        holdVersion = newVersion;
        awaitingClaim = newAwaitingClaim;
        if (newState == BagState.Held)
        {
            heldLeft = newLeft;
            heldOffsetPos = newOffsetPos;
            heldOffsetRot = newOffsetRot;
        }
        else if (newState == BagState.Dropped)
        {
            netPos = newPos;
            netRot = newRot;
            droppedAt = newDroppedAt;
            if (!netPoseReceived || state != BagState.Dropped)
                transform.SetPositionAndRotation(netPos, netRot);
            netPoseReceived = true;
        }

        // The first packet is the bag's current state, not something that just happened.
        if (stateReceived && newState != state)
            PlayStateSound(state, newState);
        stateReceived = true;

        bool left = heldLeft;
        SetState(newState, newHolder, left);
        ApplyPhysics();
    }

    // The bag's pose relative to the hand transform the pose stream sends, so remote copies sit exactly where the holder sees it.
    void MeasureHeldOffset()
    {
        PhotonPlayer me = PhotonPlayer.Local;
        Transform hand = me != null ? me.HandSource(heldLeft) : null;
        if (hand == null)
            return;
        Quaternion inv = Quaternion.Inverse(hand.rotation);
        heldOffsetPos = inv * (transform.position - hand.position);
        heldOffsetRot = inv * transform.rotation;
    }

    void PlayStateSound(BagState from, BagState to)
    {
        if (to == BagState.Held)
            Play(GrabSound);
        else if (to == BagState.Stowed && from == BagState.Held)
            Play(StowSound);
    }

    // ---------- Contents ----------

    public string[] Keys()
    {
        var keys = new string[slots.Count];
        for (int i = 0; i < slots.Count; i++)
            keys[i] = slots[i].ItemKey ?? "";
        return keys;
    }

    void ApplyKeys(string[] keys)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            BagSlot slot = slots[i];
            string key = keys != null && i < keys.Length && !string.IsNullOrEmpty(keys[i]) ? keys[i] : null;
            if (slot.ItemKey == key)
                continue;
            if (!slot.IsEmpty)
                slot.Take();
            if (key != null)
                slot.Fill(key, ResolvePrefab(key), slotSize);
        }
        if (keys != null && keys.Length > slots.Count)
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write($"[Bag] Bag {bagId} has {slots.Count} slots but got {keys.Length} keys; extra items are lost", this);
            }
        SetSlotsShown(slotsShown, true);
    }

    [PunRPC]
    void RpcContents(string[] keys, bool put, string spawnToken, PhotonMessageInfo info)
    {
        // Initial contents may precede Start/ownership delivery, but must belong to this spawn.
        if (spawnToken != photonView.SpawnToken) return;
        // A sleeping former owner must not overwrite the recovered bag's contents.
        // On joining, the owner's contents can precede the master's ownership update.
        if (PhotonNetwork.InRoom && contentsAuthorityKnown && info.Sender != photonView.Controller) return;
        // Retained state carries contents and disposition atomically. Legacy contents-only
        // RPCs may arrive after a newer checkpoint and must not resurrect removed slots.
        if (recovery == null) ApplyKeys(keys);
        if (put)
            Play(PutSound);
    }

    void BroadcastContents(bool put)
    {
        RecoveryChanged();
        if (PhotonNetwork.InRoom && recovery == null)
            photonView.RPC(nameof(RpcContents), RpcTarget.Others, Keys(), put, photonView.SpawnToken);
        else if (PhotonNetwork.InRoom && put)
            photonView.RPC(nameof(RpcPutSound), RpcTarget.Others);
        ContentsChanged?.Invoke(this);
    }

    [PunRPC]
    void RpcPutSound(PhotonMessageInfo info)
    {
        if (info.Sender == photonView.Controller) Play(PutSound);
    }

    /// <summary>Puts an item in and shrinks it from where the real item was. Owner only.</summary>
    public void Put(BagSlot slot, string key, Vector3 fromWorld, float realSize)
    {
        if (!CanControl || slot == null || !slot.IsEmpty)
            return;
        slot.Fill(key, ResolvePrefab(key), slotSize, fromWorld, realSize);
        SetSlotsShown(slotsShown, true);
        Play(PutSound);
        BroadcastContents(true);
    }

    /// <summary>Empties a slot and returns the item's prefab key. Owner only.</summary>
    public string Take(BagSlot slot)
    {
        if (!CanControl || slot == null || slot.IsEmpty)
            return null;
        string key = slot.Take();
        BroadcastContents(false);
        return key;
    }

    public BagSlot FirstEmptySlot()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].IsEmpty)
                return slots[i];
        return null;
    }

    /// <summary>Closest empty (or filled) slot to either point within radius. bestSq is shared across bags.</summary>
    public BagSlot NearestSlot(Vector3 a, Vector3 b, bool wantEmpty, ref float bestSq)
    {
        BagSlot result = null;
        for (int i = 0; i < slots.Count; i++)
        {
            BagSlot slot = slots[i];
            if (slot.IsEmpty != wantEmpty)
                continue;
            Vector3 p = slot.transform.position;
            float d = Mathf.Min((a - p).sqrMagnitude, (b - p).sqrMagnitude);
            if (d <= bestSq)
            {
                bestSq = d;
                result = slot;
            }
        }
        return result;
    }

    // ---------- Shape ----------

    public Transform GrabFor(bool left)
    {
        Transform grab = left ? grabLeft : grabRight;
        if (grab != null)
            return grab;
        return left ? grabRight : grabLeft;
    }

    /// <summary>True if the point is on (or within pad of) the bag's mesh.</summary>
    public bool Touches(Vector3 point, float pad)
    {
        bool sawMesh = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.GetComponentInParent<BagSlot>() != null)
                continue;
            sawMesh = true;
            Bounds bounds = renderer.bounds;
            bounds.Expand(pad * 2f);
            if (bounds.Contains(point))
                return true;
        }
        return !sawMesh && (point - transform.position).sqrMagnitude <= pad * pad;
    }

    void FitCollider()
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<BagSlot>() != null)
                continue;
            // Local bounds stay tight however the bag is rotated; world bounds grow with rotation.
            Bounds b = renderer.localBounds;
            Vector3 c = b.center, e = b.extents;
            Matrix4x4 toBag = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = toBag.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    any = true;
                }
                else
                    bounds.Encapsulate(local);
            }
        }
        if (!any)
            bounds = new Bounds(Vector3.zero, new Vector3(0.22f, 0.28f, 0.08f));
        box.center = bounds.center;
        box.size = bounds.size;
        locoSamples = LocomotionSamples.Build(box, out locoRadius);
    }

    /// <summary>Moves bag so grab's position and rotation match hold. Grab forward should point out of the opening.</summary>
    public static void PlaceAtGrab(Transform bag, Transform grab, Transform hold)
    {
        if (bag == null || hold == null)
            return;
        GrabPose(bag, grab, hold, out Vector3 pos, out Quaternion rot);
        bag.SetPositionAndRotation(pos, rot);
    }

    /// <summary>The bag pose that puts grab on hold, without moving the bag.</summary>
    static void GrabPose(Transform bag, Transform grab, Transform hold, out Vector3 pos, out Quaternion rot)
    {
        if (grab == null)
        {
            pos = hold.position;
            rot = hold.rotation * Quaternion.Euler(0f, 180f, 0f);
            return;
        }

        Quaternion grabLocalRot = Quaternion.Inverse(bag.rotation) * grab.rotation;
        Vector3 grabLocalPos = bag.InverseTransformPoint(grab.position);
        rot = hold.rotation * Quaternion.Inverse(grabLocalRot);
        pos = hold.position - rot * Vector3.Scale(grabLocalPos, bag.lossyScale);
    }

    // ---------- Slot visibility ----------

    void UpdateSlotVisibility()
    {
        if (Time.time >= nextNearCheck)
        {
            nextNearCheck = Time.time + SlotCheckInterval;
            Camera cam = Camera.main;
            near = cam != null && (cam.transform.position - transform.position).sqrMagnitude <= slotShowDistance * slotShowDistance;
        }
        SetSlotsShown(state != BagState.Stowed && near, false);
    }

    void SetSlotsShown(bool show, bool force)
    {
        if (show == slotsShown && !force)
            return;
        slotsShown = show;
        foreach (BagSlot slot in slots)
            foreach (Renderer r in slot.GetComponentsInChildren<Renderer>(true))
                r.enabled = show;
    }

    // ---------- Helpers ----------

    void Play(SoundEvent sound)
    {
        if (sound != null && AudioManager.instance != null)
            AudioManager.instance.Play(sound, transform.position);
    }

    public static GameObject ResolvePrefab(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (prefabCache.TryGetValue(key, out GameObject cached))
            return cached;

        GameObject prefab = null;
        if (PhotonNetwork.PrefabPool is DefaultPool pool)
            pool.ResourceCache.TryGetValue(key, out prefab);
        if (prefab == null)
        {
            StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
            if (catalog != null)
                foreach (StoreItem item in catalog.items)
                    if (item.prefab != null && item.prefab.name == key)
                        prefab = item.prefab;
        }
        if (prefab == null)
            prefab = Resources.Load<GameObject>(key);

        if (prefab != null)
            prefabCache[key] = prefab;
        return prefab;
    }
}
