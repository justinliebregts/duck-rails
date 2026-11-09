using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed partial class PhotonMasterHandoff
{
    const byte PickupReportCode = 111, PickupRefreshCode = 112, PickupAuthorityCode = 113;
    const double PickupWindow = 20, RepairGrace = 4, PickupHandoffCooldown = 60;
    [Tooltip("Allow a healthy master to hand off after two players report recurring failures on different repaired pickups.")]
    [SerializeField] bool allowPickupRecoveryHandoff = true;

    struct Loss
    {
        public string Key;
        public double First, Sent;
        public int Attempts;
        public int Request, Generation, Revision, Owner, Controller;
        public bool Pending;
    }

    struct Repair
    {
        public string Key;
        public int Reporter, Request;
        public double First;
        public bool Recurred;
    }

    // Fixed windows are touched only on a loss, a repair message or a room/focus change.
    readonly Loss[] localLosses = new Loss[4];
    readonly Repair[] repairs = new Repair[16];
    readonly RaiseEventOptions pickupTarget = new RaiseEventOptions { TargetActors = new int[1] };
    int nextLoss, nextRepair, pickupReportsSince, batchCount;
    double budgetRenews, nextLocalBatch, pickupHandoffAfter;

    void ClearPickupReports()
    {
        Array.Clear(localLosses, 0, localLosses.Length);
        Array.Clear(repairs, 0, repairs.Length);
        nextLoss = nextRepair = batchCount = 0;
        budgetRenews = nextLocalBatch = 0;
        pickupReportsSince = PhotonNetwork.ServerTimestamp;
    }

    internal static void ReportPickupLoss(RoomObjectRecovery record, string reason, int sender, int holdVersion, int revision)
    {
        var host = instance;
        if (host == null || !PhotonNetwork.InRoom || !host.Ready || host.room != PhotonNetwork.CurrentRoom ||
            (GR.Player.instance != null && GR.Player.instance.IsDead)) return;
        var view = record.View;
        if (DuckzLog.AllowWarning(out var notice, reason))
            notice.Write($"Pickup revoked ({reason}): view={view.ViewID}, actor={PhotonNetwork.LocalPlayer.ActorNumber}, sender={sender}, master={host.room.MasterClientId}, owner={view.OwnerActorNr}, controller={view.ControllerActorNr}, hold={holdVersion}, checkpoint={revision}.", view);
        host.ObservePickupLoss(record.Key, Time.realtimeSinceStartupAsDouble);
    }

    void ObservePickupLoss(string key, double now)
    {
        int index = -1;
        bool another = false;
        for (int i = 0; i < localLosses.Length; i++)
        {
            ref var loss = ref localLosses[i];
            if (loss.Key == null || now - loss.First >= PickupWindow) continue;
            if (loss.Key == key) index = i;
            else another = true;
        }
        if (index < 0)
        {
            index = nextLoss;
            nextLoss = (nextLoss + 1) % localLosses.Length;
            localLosses[index] = new Loss { Key = key, First = now };
        }
        localLosses[index].Pending = true;
        if (!another || now < nextLocalBatch) return;
        int sent = 0;
        for (int i = 0; i < localLosses.Length && sent < 2; i++)
        {
            ref var loss = ref localLosses[i];
            if (!loss.Pending || now - loss.First >= PickupWindow || loss.Attempts >= 2 ||
                (loss.Attempts > 0 && now - loss.Sent < RepairGrace)) continue;
            if (!RoomObjectRecovery.TryFind(loss.Key, out var record)) continue;
            loss.Pending = false;
            loss.Sent = now;
            loss.Attempts++;
            loss.Request = PhotonNetwork.ServerTimestamp;
            loss.Generation = record.View.OwnershipGeneration;
            loss.Revision = record.Revision;
            loss.Controller = 0;
            sent++;
            if (PhotonNetwork.IsMasterClient)
                HandlePickupReport(PhotonNetwork.LocalPlayer.ActorNumber, loss.Key, loss.Request, now);
            else
                SendPickupEvent(PickupReportCode, room.MasterClientId, loss.Key, 0, 0, loss.Request);
        }
        if (sent > 0) nextLocalBatch = now + 1;
    }

    bool SendPickupEvent(byte code, int recipient, string key, int owner, int controller, int request)
    {
        pickupTarget.TargetActors[0] = recipient;
        return PhotonNetwork.RaiseEvent(code, new object[] { room.MasterClientId, request, key, owner, controller },
            pickupTarget, SendOptions.SendReliable);
    }

    void ReceivePickupEvent(EventData data)
    {
        if (room == null || room != PhotonNetwork.CurrentRoom || !Ready ||
            !(data.CustomData is object[] fields) || fields.Length != 5 ||
            !(fields[0] is int master) || master != room.MasterClientId || !(fields[1] is int sentAt) ||
            !Fresh(sentAt, PhotonNetwork.ServerTimestamp) || unchecked(sentAt - pickupReportsSince) < 0 ||
            !(fields[2] is string key) || !(fields[3] is int owner) || !(fields[4] is int controller)) return;
        Player sender = room.GetPlayer(data.Sender);
        if (sender == null || sender.IsInactive) return;
        if (data.Code == PickupReportCode)
        {
            if (PhotonNetwork.IsMasterClient) HandlePickupReport(sender.ActorNumber, key, sentAt, Time.realtimeSinceStartupAsDouble);
        }
        else if (sender.ActorNumber == master && RoomObjectRecovery.TryFind(key, out var record))
        {
            if (data.Code == PickupRefreshCode) record.RefreshFromController(owner, controller, sentAt);
            else ConfirmPickupAuthority(record, sentAt, owner, controller);
        }
    }

    void ConfirmPickupAuthority(RoomObjectRecovery record, int request, int owner, int controller)
    {
        for (int i = 0; i < localLosses.Length; i++)
        {
            ref var loss = ref localLosses[i];
            if (loss.Key != record.Key || loss.Attempts == 0 || loss.Request != request ||
                loss.Generation != record.View.OwnershipGeneration) continue;
            loss.Owner = owner;
            loss.Controller = controller;
            record.Reconcile(); // The new controller's checkpoint can arrive before this message.
            return;
        }
    }

    // A divergent observer needs both the master's answer and a newer checkpoint from
    // that controller. An intervening transfer, grab, room change or expiry cancels repair.
    internal static bool CanRepairObserver(RoomObjectRecovery record, int owner, int writer, int revision, object proof)
    {
        var host = instance;
        if (!(proof is long stamp) || host == null || host.room == null || host.room != PhotonNetwork.CurrentRoom ||
            (int)(stamp >> 32) != host.room.MasterClientId || record.View.IsMine ||
            !IsCurrentController(host.room, owner, writer)) return false;
        foreach (var loss in host.localLosses)
            if (loss.Key == record.Key && loss.Controller == writer && loss.Owner == owner &&
                loss.Generation == record.View.OwnershipGeneration && revision > loss.Revision &&
                unchecked((int)stamp - loss.Request) >= 0 && Fresh(loss.Request, PhotonNetwork.ServerTimestamp))
                return true;
        return false;
    }

    internal static bool IsCurrentController(Room room, int owner, int controller)
    {
        if (room == null || !(room.GetPlayer(controller) is Player peer) || peer.IsInactive) return false;
        Player holder = room.GetPlayer(owner);
        if (owner != 0 && holder == null) return false;
        return controller == (holder != null && !holder.IsInactive ? owner : room.MasterClientId);
    }

    void HandlePickupReport(int reporter, string key, int request, double now)
    {
        if (!RoomObjectRecovery.TryFind(key, out var record)) return;
        int previous = -1;
        bool corroborated = false;
        for (int i = 0; i < repairs.Length; i++)
        {
            ref var attempt = ref repairs[i];
            if (attempt.Key == null || now - attempt.First >= PickupWindow) continue;
            if (attempt.Reporter == reporter && attempt.Key == key) previous = i;
            else if (attempt.Reporter != reporter && attempt.Key != key && attempt.Recurred &&
                room.GetPlayer(attempt.Reporter) is Player other && !other.IsInactive) corroborated = true;
        }
        if (previous >= 0 && (repairs[previous].Recurred || unchecked(request - repairs[previous].Request) <= 0 ||
            now - repairs[previous].First < RepairGrace)) return;
        if (now >= budgetRenews) { budgetRenews = now + 1; batchCount = 0; }
        if (batchCount >= 4) return;
        var view = record.View;
        int owner = view.OwnerActorNr, controller = view.ControllerActorNr;
        batchCount++;
        // The master identifies the controller. That peer rechecks both IDs before capturing live state.
        // The reporter supplies only a spawn-specific key; it cannot assign an owner, pose or inventory.
        if (IsCurrentController(room, owner, controller))
        {
            bool issued = view.IsMine ? record.RefreshFromController(owner, controller, request) :
                SendPickupEvent(PickupRefreshCode, controller, key, owner, controller, request);
            if (issued && reporter != room.MasterClientId && reporter != controller)
                SendPickupEvent(PickupAuthorityCode, reporter, key, owner, controller, request);
        }
        if (previous < 0)
        {
            repairs[nextRepair] = new Repair { Key = key, Reporter = reporter, Request = request, First = now };
            nextRepair = (nextRepair + 1) % repairs.Length;
            return;
        }
        repairs[previous].Recurred = true;
        if (!corroborated || now < pickupHandoffAfter) return;
        if (DuckzLog.AllowWarning(out var warning, "RecurringPickupFailures"))
            warning.Write($"Pickup failures recurred on different objects for two players after targeted repair; master={room.MasterClientId}, handoff enabled={allowPickupRecoveryHandoff}.");
        if (!allowPickupRecoveryHandoff || now < retryAt) return;
        Player candidate = SelectCandidate();
        if (candidate == null) return;
        pickupHandoffAfter = now + PickupHandoffCooldown;
        RequestMaster(candidate);
    }
}
