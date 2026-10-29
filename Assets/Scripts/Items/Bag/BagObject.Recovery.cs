using Photon.Pun;
using UnityEngine;

public partial class BagObject
{
    RoomObjectRecovery recovery;
    bool checkpointWhenSettled, recoveryStarted, recoverDetachedHand;
    public string RecoveryIdentity => recovery?.Identity;

    void Start() { recoveryStarted = true; EnsureRecovery(); }

    void EnsureRecovery()
    {
        if (recovery != null || photonView.ViewID == 0) return;
        recovery = new RoomObjectRecovery(photonView, CaptureRecovery, ApplyRecovery,
            () => state == BagState.Stowed ? holderActor : 0);
        recovery.Reconcile();
    }

    void RecoveryChanged()
    {
        EnsureRecovery();
        recovery?.Changed();
        checkpointWhenSettled = state == BagState.Dropped;
    }

    object[] CaptureRecovery()
    {
        if (state == BagState.Held) MeasureHeldOffset();
        return new object[] { (byte)state, holderActor, holdVersion, heldLeft, heldOffsetPos, heldOffsetRot,
            transform.position, transform.rotation, droppedAt, awaitingClaim, Keys(), releasedActor, releasedVersion };
    }

    void ApplyRecovery(object[] data)
    {
        if (data.Length != 13) return;
        var next = (BagState)(byte)data[0]; int holder = (int)data[1];
        PlayerHand hand = state == BagState.Held && next == BagState.Held && holder == LocalActor ? holdingHand : null;
        holdVersion = (int)data[2]; heldOffsetPos = (Vector3)data[4]; heldOffsetRot = (Quaternion)data[5];
        droppedAt = (double)data[8]; awaitingClaim = (bool)data[9];
        releasedActor = (int)data[11]; releasedVersion = (int)data[12];
        contentsAuthorityKnown = true; stateReceived = true; orphaned = false; grantedTo = 0;
        ApplyKeys((string[])data[10]);
        SetState(next, holder, (bool)data[3]); holdingHand = hand;
        recoverDetachedHand = next == BagState.Held && holder == LocalActor && hand == null && !awaitingClaim;
        orphaned = next != BagState.Dropped && holder > 0 && PhotonNetwork.CurrentRoom.GetPlayer(holder) == null;
        if (hand == null)
        {
            transform.SetParent(null, true);
            netPos = (Vector3)data[6]; netRot = (Quaternion)data[7]; netPoseReceived = true;
            transform.SetPositionAndRotation(netPos, netRot);
        }
        ApplyPhysics();
        BagInventory.Local?.ReattachRecoveredBack(this);
    }

    void RecoverDetachedHand()
    {
        if (!recoverDetachedHand) return;
        recoverDetachedHand = false;
        if (state == BagState.Held && holderActor == LocalActor && holdingHand == null && !awaitingClaim
            && (photonView.OwnerActorNr == 0 || photonView.OwnerActorNr == holderActor))
            PhotonHeldItems.Drop(this);
    }

    internal void ReattachBack(Transform anchor, bool isChest)
    {
        localBackAnchor = anchor; localBackAnchorIsChest = isChest;
        if (anchor != null) PinToBack(anchor, isChest);
    }
}
