using Photon.Pun;
using UnityEngine;

public partial class PhotonGrabbable
{
    RoomObjectRecovery recovery;
    bool checkpointWhenSettled, recoveryStarted, recoverDetachedHand;
    internal bool AcceptRecoveryPacket { get; private set; } = true;
    internal string RecoveryIdentity => recovery?.Identity;

    void Start() { recoveryStarted = true; EnsureRecovery(); }

    void EnsureRecovery()
    {
        if (recovery != null || photonView == null || photonView.ViewID == 0) return;
        recovery = new RoomObjectRecovery(photonView, CaptureRecovery, ApplyRecovery,
            () => headEquipment != null ? headEquipment.RecoveryWearer : 0);
        recovery.Reconcile();
    }

    internal void RecoveryChanged()
    {
        EnsureRecovery();
        recovery?.Changed();
        checkpointWhenSettled = !IsHeldByAnyPlayer && (headEquipment == null || !headEquipment.IsEquipped) && !IsStashed;
    }

    object[] CaptureRecovery()
    {
        bool left = IsHeld ? Holder != null && Holder.IsControllerLTouch : remoteLeftHand;
        Vector3 grip = remoteGripPosition; Quaternion rotation = remoteGripRotation;
        if (IsHeld && Holder != null)
        {
            Transform hand = PhotonPlayer.Local != null ? PhotonPlayer.Local.HandSource(left) : null;
            if (hand == null) hand = Holder.transform;
            Quaternion inverse = Quaternion.Inverse(hand.rotation);
            grip = inverse * (transform.position - PickupOffset - hand.position);
            rotation = inverse * transform.rotation;
        }
        return new object[] { IsHeldByAnyPlayer, HeldActor, left, holdVersion, hasBeenPickedUp, dropDeadline,
            IsStashed, grip, rotation, transform.position, transform.rotation, releasedActor, releasedVersion,
            headEquipment != null ? headEquipment.RecoveryWearer : 0 };
    }

    void ApplyRecovery(object[] data)
    {
        if (data.Length != 14) return;
        bool held = (bool)data[0]; int actor = (int)data[1];
        bool preserveHand = IsHeld && held && actor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!preserveHand) CancelLocalHold();
        pickupPending = false;
        remoteHeld = held && !preserveHand;
        recoverDetachedHand = remoteHeld && actor == PhotonNetwork.LocalPlayer.ActorNumber;
        remoteHolderActorNumber = remoteHeld ? actor : 0;
        remoteLeftHand = (bool)data[2]; holdVersion = (int)data[3];
        hasBeenPickedUp = (bool)data[4]; dropDeadline = (double)data[5];
        ApplyRemoteStash((bool)data[6]);
        remoteGripPosition = (Vector3)data[7]; remoteGripRotation = (Quaternion)data[8];
        releasedActor = (int)data[11]; releasedVersion = (int)data[12];
        FinishPickupGrowth(); remoteOwner = null; _hasSamplePrev = false;
        int wearer = (int)data[13];
        if (headEquipment != null) headEquipment.ApplyRecoveryWearer(wearer);
        if (!preserveHand && wearer == 0)
        {
            ParentToItemContainer();
            transform.SetPositionAndRotation((Vector3)data[9], (Quaternion)data[10]);
            rb.detectCollisions = !held && !IsStashed;
            SetBodyState(held || IsStashed || !photonView.IsMine, !held && !IsStashed && photonView.IsMine);
        }
        TrackHeld();
        if (transformView != null) transformView.ResetNetworkState();
        RefreshRemoteTransformSync();
    }

    void RecoverDetachedHand()
    {
        if (!recoverDetachedHand) return;
        recoverDetachedHand = false;
        // Run after checkpoint application so the corrective drop can itself be retained.
        if (!IsHeld && !pickupPending && remoteHeld && remoteHolderActorNumber == PhotonNetwork.LocalPlayer.ActorNumber
            && (photonView.OwnerActorNr == 0 || photonView.OwnerActorNr == remoteHolderActorNumber))
            PhotonHeldItems.Drop(this);
    }
}
