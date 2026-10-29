using Photon.Pun;

public sealed partial class HeadEquipment
{
    internal int RecoveryWearer => equipped ? equippedActorNumber : restoreActor;

    internal void ApplyRecoveryWearer(int actor)
    {
        restoreActor = 0;
        destroyAfterOwnershipFallback = false;
        bool local = actor != 0 && actor == PhotonNetwork.LocalPlayer.ActorNumber && photonView.IsMine;
        if (local)
        {
            var anchor = GR.Player.instance != null ? GR.Player.instance.helmetAnchor : null;
            if (anchor != null)
            {
                bool notify = !IsLocallyEquipped;
                equipped = true; remoteEquipped = false; equippedActorNumber = actor; equippedAnchor = anchor;
                ApplyEquippedState();
                EquipmentInventory.Save(this);
                if (notify) LocallyEquipped?.Invoke();
                return;
            }
        }
        if (IsLocallyEquipped && actor != equippedActorNumber) CancelLocalEquipment();
        ApplyRemoteEquippedState(actor != 0, actor);
        destroyAfterOwnershipFallback = actor != 0 && PhotonNetwork.CurrentRoom.GetPlayer(actor) == null;
    }
}
