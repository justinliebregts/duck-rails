using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// A master change does not reach the previous master instantly. Until it dispatches the
// switch it still believes it is master and may send InstantiateRoomObject spawns (for
// example the whole cargo seed). Those reach the server after the switch and allocate room
// view IDs that the successor, which has not received them yet, can allocate again for the
// same markers. November 20 room 26164 cached two spawns for coal views 151-159 this way.
//
// The successor therefore asks the previous master to acknowledge the switch before it
// creates room objects. The server orders the switch before that query in the previous
// master's stream, so its reliable reply on the instantiation channel follows every spawn
// it sent while still believing it was master. The handshake ends early if that peer
// leaves or goes inactive, and after a timeout: a host stalled that long dispatches the
// already-delivered switch before its next Update can spawn.
public sealed partial class PhotonMasterHandoff
{
    const byte SettleQueryCode = 118, SettleReplyCode = 119;
    readonly RaiseEventOptions settleTarget = new RaiseEventOptions { TargetActors = new int[1] };
    int knownMaster, settlingFrom, settleNonce;
    double settleUntil;

    /// <summary>False while a newly confirmed local master waits for the previous master's in-flight spawns.</summary>
    public static bool RoomSpawnsSettled => instance == null || instance.settlingFrom == 0 || !PhotonNetwork.IsMasterClient;

    /// <summary>The local client may create room-owned network objects now.</summary>
    public static bool CanSpawnRoomObjects => PhotonNetwork.IsMasterClient && PhotonNetwork.IsMessageQueueRunning && RoomSpawnsSettled;

    void ResetSettle()
    {
        settlingFrom = 0;
        knownMaster = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.MasterClientId : 0;
    }

    void MasterSwitched(Player newMaster)
    {
        int previous = knownMaster;
        knownMaster = newMaster != null ? newMaster.ActorNumber : 0;
        settlingFrom = 0;
        if (newMaster == null || !newMaster.IsLocal || !PhotonNetwork.InRoom || previous <= 0 || previous == newMaster.ActorNumber) return;
        Player old = PhotonNetwork.CurrentRoom.GetPlayer(previous);
        if (old == null || old.IsInactive) return;
        settlingFrom = previous;
        settleNonce++;
        settleUntil = Time.realtimeSinceStartupAsDouble + TimeoutMilliseconds / 1000d;
        settleTarget.TargetActors[0] = previous;
        PhotonNetwork.RaiseEvent(SettleQueryCode, settleNonce, settleTarget, SendOptions.SendReliable);
        PhotonNetwork.SendAllOutgoingCommands();
        NoteSettle("waiting for previous master " + previous);
    }

    void ReceiveSettle(EventData data)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode || !(data.CustomData is int nonce)) return;
        if (data.Code == SettleQueryCode)
        {
            // Reached only after this client dispatched the switch that preceded the query.
            settleTarget.TargetActors[0] = data.Sender;
            PhotonNetwork.RaiseEvent(SettleReplyCode, nonce, settleTarget, SendOptions.SendReliable);
            PhotonNetwork.SendAllOutgoingCommands();
            return;
        }
        if (settlingFrom != 0 && data.Sender == settlingFrom && nonce == settleNonce) EndSettle("previous master acknowledged");
    }

    void TickSettle(double now)
    {
        if (settlingFrom == 0) return;
        if (!PhotonNetwork.IsMasterClient) { settlingFrom = 0; return; }
        Player old = PhotonNetwork.CurrentRoom.GetPlayer(settlingFrom);
        if (old == null || old.IsInactive) EndSettle("previous master left");
        else if (now >= settleUntil) EndSettle("previous master silent; timed out");
    }

    void EndSettle(string reason)
    {
        int previous = settlingFrom;
        settlingFrom = 0;
        NoteSettle(reason + " (" + previous + ")");
    }

    static void NoteSettle(string message)
    {
        DuckzLog.Log("Master handoff settle: " + message);
        if (RailDiagnostics.Instance != null) RailDiagnostics.Instance.Event("master settle: " + message);
    }
}
