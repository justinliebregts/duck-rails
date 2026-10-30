#if UNITY_EDITOR
using System;
using System.Linq;
using Photon.Pun;

/// <summary>Checks the actual PUN registry, including inactive objects, at settled multiplayer checkpoints.</summary>
public static class RailAvatarChecks
{
    public static string AssertCurrentRoom()
    {
        if (!PhotonNetwork.InRoom) throw new Exception("Avatar check requires a live room");
        var avatars = new System.Collections.Generic.List<PhotonPlayer>();
        foreach (var view in PhotonNetwork.PhotonViewCollection)
        {
            var player = view != null ? view.GetComponent<PhotonPlayer>() : null;
            if (player != null && !player.IsCompanion) avatars.Add(player);
        }
        foreach (var actor in PhotonNetwork.PlayerList)
        {
            var copies = avatars.Where(p => p.photonView.OwnerActorNr == actor.ActorNumber).ToArray();
            if (copies.Length != 1)
                throw new Exception("Actor " + actor.ActorNumber + " has " + copies.Length + " avatars: "
                    + string.Join(",", copies.Select(p => p.photonView.ViewID)));
            if (!actor.IsInactive && (!copies[0].isActiveAndEnabled || !PhotonPlayer.Active.Contains(copies[0])))
                throw new Exception("Connected actor has an inactive/unregistered avatar");
        }
        if (avatars.Any(p => PhotonNetwork.CurrentRoom.GetPlayer(p.photonView.OwnerActorNr) == null))
            throw new Exception("Avatar for an actor outside the current room");
        var local = PhotonPlayer.Local;
        var driver = UnityEngine.Object.FindFirstObjectByType<Game.Animation.LocalBodyDriver>();
        if (local == null || driver == null || local.HandSource(true) != driver.LeftHandAnchor
            || local.HandSource(false) != driver.RightHandAnchor)
            throw new Exception("Local avatar is not bound to the current rig");
        return string.Join(",", avatars.OrderBy(p => p.photonView.OwnerActorNr)
            .Select(p => p.photonView.OwnerActorNr + ":" + p.photonView.ViewID));
    }
}
#endif
