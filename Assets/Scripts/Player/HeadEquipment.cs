using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Handles equipping a networked grabbable item to a head or eyes slot.
/// Item-specific effects should react to the local equip events.
/// </summary>
[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(Grabbable))]
public sealed partial class HeadEquipment : MonoBehaviour, IPunObservable, IInRoomCallbacks, IPunInstantiateMagicCallback
{
    public enum EquipmentSlot { Head, Eyes }

    [SerializeField] EquipmentSlot slot;
    [Tooltip("Point used to remove the worn item. Defaults to the root when unassigned.")]
    [SerializeField] Transform grabPoint;
    [Tooltip("Point used for equip proximity. Defaults to the root when unassigned; does not change the worn pose.")]
    [SerializeField] Transform equipPoint;
    [SerializeField] float headSnapRadius = 0.25f;
    [Tooltip("How close a hand must be to remove the worn item. Backpack grab zones are excluded.")]
    [SerializeField, Min(0f)] float headGrabRadius = 0.22f;

    Grabbable grabbable;
    PhotonGrabbable networkGrabbable;
    ItemSounds sounds;
    Rigidbody rb;
    PhotonView photonView;
    Transform equippedAnchor;
    bool equipped;
    bool remoteEquipped;
    bool subscribed;
    bool callbacksRegistered;
    bool destroyAfterOwnershipFallback;
    int equippedActorNumber;
    int restoreActor;
    static int lastGrabFrame = -1;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data != null && data.Length == 2 && data[0] is string tag &&
            tag == "equipment-restore-v1" && data[1] is int actor)
            restoreActor = actor;
    }

    public bool IsEquipped => equipped;
    public bool IsLocallyEquipped => equipped && !remoteEquipped;
    internal bool AwaitingWearerCleanup => destroyAfterOwnershipFallback;
    public EquipmentSlot Slot => slot;
    bool CanRemove => isActiveAndEnabled && IsLocallyEquipped && grabbable != null &&
        grabbable.CanBeGrabbed && !grabbable.IsHeld &&
        (networkGrabbable == null || !networkGrabbable.IsHeldByAnyPlayer);

    public event Action LocallyEquipped;
    public event Action LocallyUnequipped;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        networkGrabbable = grabbable as PhotonGrabbable;
        sounds = GetComponent<ItemSounds>();
        rb = GetComponent<Rigidbody>();
        photonView = GetComponent<PhotonView>();
    }

    void OnEnable()
    {
        SubscribeToGrabEvents();
        if (!callbacksRegistered)
        {
            PhotonNetwork.AddCallbackTarget(this);
            callbacksRegistered = true;
        }
    }

    void OnDisable()
    {
        CancelLocalEquipment();
        UnsubscribeFromGrabEvents();
        if (callbacksRegistered)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            callbacksRegistered = false;
        }
    }

    // Ownership loss/destruction must not run the ordinary release/equip path.
    public void CancelLocalEquipment()
    {
        if (!IsLocallyEquipped) return;
        bool replacement = equippedAnchor != null && HasEquippedItem(equippedAnchor);
        equipped = false;
        remoteEquipped = false;
        equippedAnchor = null;
        equippedActorNumber = 0;
        restoreActor = 0;
        // OnDisable may run before Photon disconnects during application shutdown.
        if (!replacement && !ConnectionHandler.AppQuits && PhotonNetwork.InRoom && gameObject.scene.isLoaded &&
            GR.Player.instance != null && GR.Player.instance.helmetAnchor != null)
            EquipmentInventory.Clear(slot);
        LocallyUnequipped?.Invoke();
        if (gameObject.activeInHierarchy) transform.SetParent(null, true);
    }

    void LateUpdate()
    {
        // The room checkpoint can arrive before the local rig/anchor exists.
        if (remoteEquipped && photonView.IsMine && equippedActorNumber == PhotonNetwork.LocalPlayer?.ActorNumber &&
            GR.Player.instance != null && GR.Player.instance.helmetAnchor != null)
            ApplyRecoveryWearer(equippedActorNumber);
        if (restoreActor != 0 && PhotonNetwork.LocalPlayer != null &&
            restoreActor == PhotonNetwork.LocalPlayer.ActorNumber && photonView.IsMine)
        {
            var player = GR.Player.instance;
            if (player == null || player.helmetAnchor == null || EquipmentInventory.Local == null) return;
            restoreActor = 0;
            if (EquipmentInventory.Local.AcceptRestore(this) && !HasEquippedItem(player.helmetAnchor))
                Equip(player.helmetAnchor);
            else
            {
                PhotonNetwork.Destroy(gameObject);
                return;
            }
        }
        if (destroyAfterOwnershipFallback)
        {
            ApplyWornPhysics();
            if (PhotonNetwork.InRoom && photonView != null && photonView.IsMine)
            {
                // Keep the request pending until destruction removes this instance.
                PhotonNetwork.Destroy(gameObject);
            }
            return;
        }

        if (grabbable != null && (grabbable.IsHeld ||
            (networkGrabbable != null && networkGrabbable.IsHeldByAnyPlayer)))
            return;

        if (!equipped)
            return;

        if (equippedAnchor == null)
        {
            if (remoteEquipped)
            {
                equippedAnchor = FindHeadAnchor(equippedActorNumber);
                if (equippedAnchor == null)
                    return;
            }
            else
            {
                UnequipAsDropped();
                return;
            }
        }

        if (!remoteEquipped) TryGrabEquippedItems();
        if (!equipped)
            return;

        ApplyEquippedState();
    }

    static void TryGrabEquippedItems()
    {
        var player = GR.Player.instance;
        if (player == null || player.helmetAnchor == null || lastGrabFrame == Time.frameCount)
            return;
        lastGrabFrame = Time.frameCount;
        bool left = player.ReturnButtonPress_Left(ButtonName.Grip).wasPressed;
        bool right = player.ReturnButtonPress_Right(ButtonName.Grip).wasPressed;
        if (!left && !right) return;

        var items = player.helmetAnchor.GetComponentsInChildren<HeadEquipment>();
        // Update every worn pose before comparing distances, regardless of component update order.
        foreach (var item in items)
            if (item.CanRemove)
                item.ApplyEquippedState();
        if (left) TryGrabNearest(player.leftPlayerHand, items);
        if (right) TryGrabNearest(player.rightPlayerHand, items);
    }

    static void TryGrabNearest(PlayerHand hand, HeadEquipment[] items)
    {
        if (hand == null || !hand.CanGrab || hand.IsHolding ||
            (BagInventory.Local != null && BagInventory.Local.BlocksHelmetGrab(hand))) return;
        HeadEquipment nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var item in items)
        {
            if (!item.CanRemove) continue;
            Vector3 point = item.grabPoint != null ? item.grabPoint.position : item.transform.position;
            float distance = (hand.transform.position - point).sqrMagnitude;
            if (distance > item.headGrabRadius * item.headGrabRadius || distance >= nearestDistance) continue;
            nearest = item;
            nearestDistance = distance;
        }
        if (nearest != null) hand.TryGrab(nearest.grabbable);
    }

    void HandleGrabbed(PlayerHand hand)
    {
        restoreActor = 0;
        bool wasLocallyEquipped = IsLocallyEquipped;
        equipped = false;
        remoteEquipped = false;
        equippedAnchor = null;
        equippedActorNumber = 0;

        if (wasLocallyEquipped)
        {
            EquipmentInventory.Clear(slot);
            LocallyUnequipped?.Invoke();
        }
    }

    void HandleReleased(PlayerHand hand)
    {
        Transform headAnchor = GR.Player.instance != null
            ? GR.Player.instance.helmetAnchor
            : null;

        if (hand != null && hand.CanGrab && headAnchor != null &&
            ((equipPoint != null ? equipPoint.position : transform.position) - headAnchor.position).sqrMagnitude
                <= headSnapRadius * headSnapRadius &&
            !HasEquippedItem(headAnchor))
        {
            Equip(headAnchor);
            return;
        }

        UnequipAsDropped();
    }

    bool HasEquippedItem(Transform headAnchor)
    {
        foreach (HeadEquipment item in headAnchor.GetComponentsInChildren<HeadEquipment>(true))
        {
            if (item != this && item.slot == slot && item.equipped && item.equippedAnchor == headAnchor)
                return true;
        }

        return false;
    }

    void Equip(Transform headAnchor)
    {
        equipped = true;
        remoteEquipped = false;
        equippedAnchor = headAnchor;
        equippedActorNumber = PhotonNetwork.LocalPlayer != null
            ? PhotonNetwork.LocalPlayer.ActorNumber
            : 0;
        ApplyEquippedState();
        PlaySound(ItemSound.Equip);
        networkGrabbable?.RecoveryChanged();
        EquipmentInventory.Save(this);
        LocallyEquipped?.Invoke();
    }

    void ApplyEquippedState()
    {
        ApplyWornPhysics();
        if (transform.parent != equippedAnchor)
            transform.SetParent(equippedAnchor, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    void ApplyWornPhysics()
    {
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (rb.useGravity) rb.useGravity = false;
            if (rb.detectCollisions) rb.detectCollisions = false;
        }
    }

    void UnequipAsDropped()
    {
        bool wasLocallyEquipped = IsLocallyEquipped;
        equipped = false;
        remoteEquipped = false;
        equippedAnchor = null;
        equippedActorNumber = 0;

        if (wasLocallyEquipped)
        {
            // Room teardown must preserve the saved loadout.
            if (!ConnectionHandler.AppQuits && PhotonNetwork.InRoom &&
                GR.Player.instance != null && GR.Player.instance.helmetAnchor != null)
                EquipmentInventory.Clear(slot);
            LocallyUnequipped?.Invoke();
        }

        if (rb == null || (grabbable != null && grabbable.IsHeld))
            return;

        transform.SetParent(null, true);
        bool simulate = photonView == null || photonView.IsMine;
        rb.isKinematic = !simulate;
        rb.useGravity = simulate;
        rb.detectCollisions = true;
        if (simulate && grabbable != null)
            grabbable.PrepareDropImpact();
    }

    public void DropOnDeath()
    {
        if (!IsLocallyEquipped || grabbable == null)
            return;
        if (photonView != null && !photonView.IsMine)
            return;

        UnequipAsDropped();
        if (grabbable is PhotonGrabbable networkItem)
            networkItem.NotifyEquipmentDropped();
    }

    Transform FindHeadAnchor(int actorNumber)
    {
        for (int i = 0; i < PhotonPlayer.Active.Count; i++)
        {
            PhotonPlayer player = PhotonPlayer.Active[i];
            if (player == null ||
                player.photonView == null ||
                player.photonView.OwnerActorNr != actorNumber)
                continue;

            return player.helmetAnchor != null
                ? player.helmetAnchor.transform
                : null;
        }

        return null;
    }

    void ApplyRemoteEquippedState(bool isEquipped, int actorNumber)
    {
        if (!isEquipped)
        {
            if (remoteEquipped)
                UnequipAsDropped();
            return;
        }

        if (!remoteEquipped || equippedActorNumber != actorNumber)
            equippedAnchor = null;

        equipped = true;
        remoteEquipped = true;
        equippedActorNumber = actorNumber;
        // Late join can restore worn state before the wearer's anchor exists.
        ApplyWornPhysics();
        if (equippedAnchor == null)
            equippedAnchor = FindHeadAnchor(actorNumber);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(equipped);
            stream.SendNext(equipped ? equippedActorNumber : 0);
            return;
        }

        bool incomingEquipped = (bool)stream.ReceiveNext();
        int incomingActor = (int)stream.ReceiveNext();
        if (photonView != null && (photonView.IsMine ||
            (info.Sender != null && info.Sender != photonView.Controller))) return;
        if (networkGrabbable != null && (networkGrabbable.PickupPending || !networkGrabbable.AcceptRecoveryPacket)) return;
        ApplyRemoteEquippedState(incomingEquipped, incomingActor);
        // Observed after held state and before the transform, including drops
        // whose brief intermediate grab was never sent over the network.
        if (networkGrabbable != null) networkGrabbable.RefreshRemoteTransformSync();
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Hand-held equipment follows the shared drop path, not worn-item persistence cleanup.
        if (networkGrabbable != null && networkGrabbable.IsHeldByAnyPlayer) return;
        if (otherPlayer == null ||
            otherPlayer.IsInactive ||
            (otherPlayer.ActorNumber != equippedActorNumber && otherPlayer.ActorNumber != restoreActor))
            return;

        destroyAfterOwnershipFallback = true;
    }

    internal void CancelHeldRestore()
    {
        restoreActor = 0;
        destroyAfterOwnershipFallback = false;
        ApplyRemoteEquippedState(false, 0);
    }

    public void OnPlayerEnteredRoom(Player newPlayer) { }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

    public void OnMasterClientSwitched(Player newMasterClient) { }

    void SubscribeToGrabEvents()
    {
        if (grabbable == null || subscribed)
            return;

        grabbable.OnGrabbed += HandleGrabbed;
        grabbable.OnReleased += HandleReleased;
        subscribed = true;
    }

    void UnsubscribeFromGrabEvents()
    {
        if (grabbable == null || !subscribed)
            return;

        grabbable.OnGrabbed -= HandleGrabbed;
        grabbable.OnReleased -= HandleReleased;
        subscribed = false;
    }

    void PlaySound(ItemSound sound)
    {
        if (sounds != null)
            sounds.Play(sound);
    }
}
