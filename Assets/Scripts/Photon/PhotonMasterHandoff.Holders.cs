using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Ported from Duckz 0f9a8a96: before reclaiming a held item after heartbeat silence,
// the master asks the holder directly whether it is available. Duckz uses 110/111;
// Rails already uses those for drive grips and pickup reports.
public sealed partial class PhotonMasterHandoff
{
    const byte HolderQuery = 116, HolderReply = 117;
    const double HolderReplySeconds = 3;
    readonly Dictionary<int, Holder> holders = new(PhotonSession.MaxPlayers);
    readonly RaiseEventOptions holderTarget = new() { TargetActors = new int[1] };
    int nextHolderQuery;

    sealed class Holder
    {
        public int? Activity, ExplicitAt;
        public bool Standby, Checking;
        public int Query, Attempts, StartedAt;
        public double NextCheck;
        // Only the hands/claims observed when checking began can be released on silence.
        public readonly HashSet<(PhotonView view, string spawn, int actor, int version, int generation)> Holds = new();
    }

    Holder GetHolder(int actor)
    {
        if (!holders.TryGetValue(actor, out var holder)) holders.Add(actor, holder = new Holder());
        return holder;
    }

    void ObserveHolderState(int actor, int stamp, bool standby)
    {
        var holder = GetHolder(actor);
        if (holder.ExplicitAt.HasValue && unchecked(stamp - holder.ExplicitAt.Value) < 0) return;
        if (standby && holder.Activity.HasValue && unchecked(stamp - holder.Activity.Value) < 0) return;
        holder.ExplicitAt = stamp;
        holder.Standby = standby;
        holder.Checking = false;
        if (standby)
        {
            holder.Activity = null;
            PhotonHeldItems.CaptureHolds(actor, holder.Holds);
        }
        else
        {
            holder.Holds.Clear();
            if (!holder.Activity.HasValue || unchecked(stamp - holder.Activity.Value) > 0)
                holder.Activity = stamp;
        }
    }

    // Called only for validated human poses or held-item snapshots from their actual owner.
    internal static void HolderActivity(int actor, int stamp)
    {
        var host = instance;
        if (host == null || !PhotonNetwork.IsMasterClient || host.room == null ||
            host.room != PhotonNetwork.CurrentRoom || !Fresh(stamp, PhotonNetwork.ServerTimestamp) ||
            unchecked(stamp - host.observationStartedAt) < 0) return;
        var holder = host.GetHolder(actor);
        if (holder.Checking && unchecked(stamp - holder.StartedAt) < 0) return;
        if (holder.Standby && holder.ExplicitAt.HasValue &&
            (Fresh(holder.ExplicitAt.Value, PhotonNetwork.ServerTimestamp) || unchecked(stamp - holder.ExplicitAt.Value) <= 0)) return;
        if (holder.Activity.HasValue && unchecked(stamp - holder.Activity.Value) <= 0) return;
        holder.Activity = stamp;
        if (!holder.Checking) return;
        holder.Checking = false;
        holder.Holds.Clear();
    }

    internal bool CanRelease(int actor, PhotonView view, int heldActor, int version)
    {
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber) return !LocalAvailable;
        var player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (player == null || player.IsInactive) return true;
        var holder = GetHolder(actor);
        int now = PhotonNetwork.ServerTimestamp;
        var hold = (view, view.SpawnToken, heldActor, version, view.OwnershipGeneration);
        if (holder.Standby && !holder.Checking && holder.ExplicitAt.HasValue && Fresh(holder.ExplicitAt.Value, now) && holder.Holds.Contains(hold))
            return true;
        if (holder.Activity.HasValue && Fresh(holder.Activity.Value, now)) return false;
        // Rails: UnavailableReason treats a clock-skewed peer with live heartbeat cadence
        // as available (PhotonMasterHandoff.Clock), so it never reaches a holder check.
        if (!IsActorUnavailable(actor)) return false;

        double elapsed = Time.realtimeSinceStartupAsDouble;
        if (!holder.Checking || !holder.Holds.Contains(hold))
        {
            PhotonHeldItems.CaptureHolds(actor, holder.Holds);
            holder.Checking = true;
            holder.Query = unchecked(++nextHolderQuery);
            holder.StartedAt = now;
            holder.Attempts = 0;
            holder.NextCheck = elapsed;
        }
        if (elapsed < holder.NextCheck) return false;
        if (holder.Attempts == 2) return true;
        holder.NextCheck = elapsed + HolderReplySeconds;
        if (SendHolderEvent(HolderQuery, actor, new object[] { holder.Query, now }))
            holder.Attempts++;
        return false;
    }

    bool SendHolderEvent(byte code, int actor, object[] data)
    {
        holderTarget.TargetActors[0] = actor;
        return PhotonNetwork.RaiseEvent(code, data, holderTarget, SendOptions.SendReliable);
    }

    void ReceiveHolderCheck(EventData data)
    {
        if (room == null || room != PhotonNetwork.CurrentRoom || PhotonNetwork.OfflineMode ||
            !(data.CustomData is object[] fields) || fields.Length < 2 || !(fields[0] is int query) ||
            !(fields[1] is int stamp) || !Fresh(stamp, PhotonNetwork.ServerTimestamp)) return;
        if (data.Code == HolderQuery)
        {
            if (fields.Length == 2 && data.Sender == room.MasterClientId && !PhotonNetwork.IsMasterClient)
                SendHolderEvent(HolderReply, data.Sender, new object[] { query, PhotonNetwork.ServerTimestamp, LocalAvailable });
            return;
        }
        if (!PhotonNetwork.IsMasterClient || fields.Length != 3 || !(fields[2] is bool available) ||
            !holders.TryGetValue(data.Sender, out var holder) || !holder.Checking || holder.Query != query) return;
        ObserveHolderState(data.Sender, stamp, !available);
        if (!available && Ready) PhotonHeldItems.RecoverUnavailable(this);
    }
}
