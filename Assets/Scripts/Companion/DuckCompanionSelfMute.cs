using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Duck refuses local muting everywhere; nearby requests trigger a room-wide unmute and reaction.</summary>
public sealed class DuckCompanionSelfMute : MonoBehaviourPun
{
    const double ReactionCooldown = 30;
    const double ReactionExpiry = 15;
    DuckCompanion duck;
    AudioSource voice;
    double nextReactionAt;
    double pendingReactionUntil = -1;
    double nextLocalRequest;
    readonly Dictionary<int, double> nextActorRequest = new Dictionary<int, double>();

    void Awake() => duck = GetComponent<DuckCompanion>();

    void LateUpdate()
    {
        // Enforce Duck's special mute rule on every listener, including remote
        // clients and offline play. Repair stale/direct source mute changes
        // without generating another room request or complaint.
        AudioSource source = Voice();
        if (source != null && source.mute) ApplyUnmute();
    }

    public void SetMuted(bool muted)
    {
        // Only Duck's voice is affected. Clear it locally immediately, even when
        // throttling a repeated click or rejecting a distant reaction request.
        ApplyUnmute();
        if (!muted || !PhotonNetwork.InRoom || PhotonNetwork.Time < nextLocalRequest) return;
        if (!WithinReactionRange(PhotonPlayer.Local)) return;
        nextLocalRequest = PhotonNetwork.Time + 1;
        photonView.RPC(nameof(RequestRoomUnmute), RpcTarget.MasterClient);
    }

    [PunRPC]
    void RequestRoomUnmute(PhotonMessageInfo info)
    {
        if (duck == null || !duck.HasAuthority || info.Sender == null) return;
        int actor = info.Sender.ActorNumber;
        if (nextActorRequest.TryGetValue(actor, out double allowed) && PhotonNetwork.Time < allowed) return;
        // Resolve the sender's avatar on the authority; never trust a client-supplied distance.
        if (!WithinReactionRange(PhotonPlayer.ForActor(actor))) return;
        nextActorRequest[actor] = PhotonNetwork.Time + 1;
        photonView.RPC(nameof(UnmuteRoom), RpcTarget.All);
        TryQueueReaction(PhotonNetwork.Time);
    }

    bool TryQueueReaction(double now)
    {
        // Only an accepted click starts this fixed, room-wide cooldown.
        // Expired pending work must not block a new click before Update runs.
        if (pendingReactionUntil >= 0 && now > pendingReactionUntil) pendingReactionUntil = -1;
        if (now < nextReactionAt || pendingReactionUntil >= 0) return false;
        pendingReactionUntil = now + ReactionExpiry;
        nextReactionAt = now + ReactionCooldown;
        return true;
    }

    void Update()
    {
        if (duck == null || !duck.HasAuthority) return;
        double now = PhotonNetwork.Time;
        // At most one pending remark. Never interrupt a sentence, attack or death;
        // discard an old complaint rather than accumulating a queue of grievances.
        if (pendingReactionUntil >= 0)
        {
            if (now > pendingReactionUntil) pendingReactionUntil = -1;
            else if (duck.Dialogue != null && duck.Dialogue.TryReactToUnmute())
            {
                pendingReactionUntil = -1;
            }
        }
    }

    [PunRPC]
    void UnmuteRoom(PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient) return;
        ApplyUnmute();
    }

    bool WithinReactionRange(PhotonPlayer player)
    {
        if (duck == null || duck.Head == null || player == null || player.IsCompanion
            || !player.TryGetHeadPosition(out Vector3 position)) return false;
        float range = DuckCompanionDialogue.ConversationRange;
        return (position - duck.Head.position).sqrMagnitude <= range * range;
    }

    void ApplyUnmute()
    {
        AudioSource source = Voice();
        if (source != null) source.mute = false;
        if (duck != null) duck.RaiseUnmutedItself();
    }

    // Carry deadlines and the shared speech cooldown across authority changes.
    readonly PhotonValueCache serializationValues = new PhotonValueCache(4);
    public void Serialize(PhotonStream stream)
    {
        serializationValues.Begin();
        if (stream.IsWriting)
        {
            serializationValues.Send(stream, nextReactionAt);
            serializationValues.Send(stream, pendingReactionUntil);
        }
        else
        {
            nextReactionAt = (double)stream.ReceiveNext();
            pendingReactionUntil = (double)stream.ReceiveNext();
        }
    }

    AudioSource Voice()
    {
        if (voice == null)
        {
            PhotonPlayer player = GetComponent<PhotonPlayer>();
            voice = player != null ? player.VoiceSource : null;
        }
        return voice;
    }
}
