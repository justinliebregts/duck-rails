using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>Event-only drops and a shared heartbeat sweep over held objects, never the scene.</summary>
public static class PhotonHeldItems
{
    public const byte DropEvent = 106;
    static readonly RaiseEventOptions Others = new RaiseEventOptions { Receivers = ReceiverGroup.Others };

    public static void DropLocalHands()
    {
        if (BagInventory.Local != null) BagInventory.Local.DropHandsForUnavailablePlayer();
        else BagInventory.InvalidateHandClaims();
        // Includes a hand whose rig/inventory has already been disabled.
        for (int i = PhotonGrabbable.HeldItems.Count - 1; i >= 0; i--)
        {
            PhotonGrabbable item = PhotonGrabbable.HeldItems[i];
            if (item != null && item.IsHeld) item.DropForUnavailablePlayer();
            else if (item != null && PhotonNetwork.InRoom && item.HeldActor == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                // A retained hold can outlive its hand. A newer owner's grant can
                // also precede their pose, so do not revoke that grant on our wake.
                int owner = item.GetComponent<PhotonView>().OwnerActorNr;
                if (owner == 0 || owner == item.HeldActor) Drop(item);
            }
        }
        for (int i = BagObject.Active.Count - 1; i >= 0; i--)
        {
            BagObject bag = BagObject.Active[i];
            if (bag != null) bag.CancelIncomingHandClaim();
            if (bag != null && !bag.AwaitingClaim && bag.State != BagState.Stowed && bag.DropActor == BagObject.LocalActor)
                Drop(bag);
        }
        var player = GR.Player.instance;
        if (player == null) return;
        player.leftPlayerHand?.Held?.DropForUnavailablePlayer();
        player.rightPlayerHand?.Held?.DropForUnavailablePlayer();
    }

    public static void RecoverUnavailable(PhotonMasterHandoff availability, int departedActor = 0)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        bool changed = false;
        for (int i = PhotonGrabbable.HeldItems.Count - 1; i >= 0; i--)
        {
            PhotonGrabbable item = PhotonGrabbable.HeldItems[i];
            if (item == null) continue;
            int actor = item.HeldActor;
            if (actor <= 0) continue;
            PhotonView view = item.GetComponent<PhotonView>();
            if (!CanRecover(view, actor, item.HoldVersion, availability, departedActor)) continue;
            availability.NoteHeldRecovery(view, actor, departedActor);
            changed |= Drop(item);
        }
        for (int i = BagObject.Active.Count - 1; i >= 0; i--)
        {
            BagObject bag = BagObject.Active[i];
            if (bag == null) continue;
            int actor = bag.DropActor;
            // Dropped bags use Request ownership too: an unavailable owner cannot grant a pickup.
            if (actor > 0 && (bag.State == BagState.Held || bag.PendingHandClaim || (bag.State == BagState.Dropped && !bag.AwaitingClaim))
                && CanRecover(bag.photonView, actor, bag.HoldVersion, availability, departedActor))
            {
                availability.NoteHeldRecovery(bag.photonView, actor, departedActor);
                changed |= Drop(bag);
            }
        }
        if (changed) { RoomObjectRecovery.Flush(); PhotonNetwork.SendAllOutgoingCommands(); }
    }

    internal static void CaptureHolds(int actor, HashSet<(PhotonView, string, int, int, int)> holds)
    {
        holds.Clear();
        foreach (var item in PhotonGrabbable.HeldItems)
        {
            if (item == null) continue;
            var view = item.GetComponent<PhotonView>();
            if (item.HeldActor == actor || view.OwnerActorNr == actor)
                holds.Add((view, view.SpawnToken, item.HeldActor, item.HoldVersion, view.OwnershipGeneration));
        }
        foreach (var bag in BagObject.Active)
        {
            if (bag == null || bag.State == BagState.Stowed) continue;
            var view = bag.photonView;
            if (bag.DropActor == actor || view.OwnerActorNr == actor)
                holds.Add((view, view.SpawnToken, bag.DropActor, bag.HoldVersion, view.OwnershipGeneration));
        }
    }

    // Heartbeat silence alone no longer releases a hold: CanRelease confirms an
    // explicit standby or an unanswered direct holder query (PhotonMasterHandoff.Holders).
    static bool CanRecover(PhotonView view, int holder, int version, PhotonMasterHandoff availability, int departedActor)
    {
        // Ownership may arrive before the new holder's pose. A waking master must
        // not reclaim that healthy owner's pickup using the old held snapshot.
        int owner = view.OwnerActorNr;
        return (holder == departedActor || availability.CanRelease(holder, view, holder, version))
            && (owner == 0 || owner == holder || owner == departedActor || availability.CanRelease(owner, view, holder, version));
    }

    public static bool Drop(PhotonGrabbable item)
    {
        if (!item.ProtectUnavailableHookDrop()) return false;
        return Send(item.GetComponent<PhotonView>(), item.HeldActor, item.HoldVersion);
    }
    public static bool Drop(BagObject bag)
    {
        if (!PhotonNetwork.InRoom) { bag.DropLocal(Vector3.zero); return true; }
        if (!bag.ProtectUnavailableHookDrop()) return false;
        return Send(bag.photonView, bag.DropActor, bag.HoldVersion);
    }

    static bool Send(PhotonView view, int actor, int version)
    {
        if (!PhotonNetwork.InRoom || view == null || view.ViewID == 0 || actor <= 0) return false;
        int sender = PhotonNetwork.LocalPlayer.ActorNumber;
        if (sender != actor && !PhotonNetwork.IsMasterClient) return false;
        Vector3 position = view.transform.position;
        Quaternion rotation = view.transform.rotation;
        // Queue before applying: the local drop can remove the item from the held registry.
        PhotonNetwork.RaiseEvent(DropEvent, new object[] { view.ViewID, actor, version, position, rotation, view.SpawnToken }, Others, SendOptions.SendReliable);
        return Apply(view, actor, version, position, rotation);
    }

    public static void OnEvent(EventData data)
    {
        if (!PhotonNetwork.InRoom || data.Code != DropEvent || !(data.CustomData is object[] values) || values.Length != 6
            || !(values[0] is int id) || !(values[1] is int actor) || !(values[2] is int version)
            || !(values[3] is Vector3 position) || !(values[4] is Quaternion rotation)) return;
        if (data.Sender != actor && data.Sender != PhotonNetwork.CurrentRoom.MasterClientId) return;
        PhotonView view = PhotonView.Find(id);
        // A reliable drop queued before standby must not release a later spawn reusing this ID.
        if (view == null || !Equals(values[5], view.SpawnToken)) return;
        if (data.Sender != actor && actor == PhotonNetwork.LocalPlayer.ActorNumber && view.IsMine)
        {
            // Report only a release that actually removed a confirmed local hand.
            if (view.TryGetComponent(out PhotonGrabbable item) && item.IsHeld && !item.PickupPending)
            {
                if (item.ApplyUnavailableDrop(actor, version, position, rotation)) item.ReportMasterDrop(data.Sender);
                return;
            }
            if (view.TryGetComponent(out BagObject bag) && bag.HasLocalHand)
            {
                if (bag.ApplyUnavailableDrop(actor, version, position, rotation)) bag.ReportMasterDrop(data.Sender);
                return;
            }
        }
        Apply(view, actor, version, position, rotation);
    }

    static bool Apply(PhotonView view, int actor, int version, Vector3 position, Quaternion rotation)
    {
        if (view.TryGetComponent(out PhotonGrabbable item)) return item.ApplyUnavailableDrop(actor, version, position, rotation);
        return view.TryGetComponent(out BagObject bag) && bag.ApplyUnavailableDrop(actor, version, position, rotation);
    }

    // Recovery is a reliable, authorized application event. Bags use Request ownership,
    // so an asleep owner cannot be asked to grant it. Room ownership follows master changes.
    public static void ReturnToRoom(PhotonView view)
    {
        if (!PhotonNetwork.InRoom) return;
        view.MarkLiveOwnership();
        view.OwnerActorNr = 0;
        view.ControllerActorNr = PhotonNetwork.CurrentRoom.MasterClientId;
    }
}
