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
//
// A timeout does not cover spawns the stalled host already issued but could not send.
// Photon keeps at most 50 unacknowledged reliable commands in flight, so a large seed waits
// in the stalled host's queue and goes out when it resumes (November 21 stalled-host
// faults 1-2). After a timeout the successor therefore allocates past a gap, and removes
// any later-arriving room object whose run marker duplicates one it spawned itself.
public sealed partial class PhotonMasterHandoff
{
    const byte SettleQueryCode = 118, SettleReplyCode = 119;
    // Larger than any one room's run objects (~200), well inside the 999 room view IDs.
    const int TimeoutViewIdGap = 300;
    readonly RaiseEventOptions settleTarget = new RaiseEventOptions { TargetActors = new int[1] };
    readonly System.Collections.Generic.HashSet<string> settledMarkers = new System.Collections.Generic.HashSet<string>();
    readonly System.Collections.Generic.List<PhotonView> lateDuplicates = new System.Collections.Generic.List<PhotonView>();
    int knownMaster, settlingFrom, settleNonce, duplicateFloor;
    double settleUntil, nextDuplicateCheck;

    /// <summary>False while a newly confirmed local master waits for the previous master's in-flight spawns.</summary>
    public static bool RoomSpawnsSettled => instance == null || instance.settlingFrom == 0 || !PhotonNetwork.IsMasterClient;

    /// <summary>The local client may create room-owned network objects now. Never a PC spectator.</summary>
    public static bool CanSpawnRoomObjects => PhotonNetwork.IsMasterClient && !SpectatorMode.Active &&
        PhotonNetwork.IsMessageQueueRunning && RoomSpawnsSettled;

    void ResetSettle()
    {
        settlingFrom = 0; duplicateFloor = 0;
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
        TickLateDuplicates(now);
        if (settlingFrom == 0) return;
        if (!PhotonNetwork.IsMasterClient) { settlingFrom = 0; return; }
        Player old = PhotonNetwork.CurrentRoom.GetPlayer(settlingFrom);
        if (old == null || old.IsInactive) EndSettle("previous master left");
        else if (now >= settleUntil)
        {
            // The previous master is still connected and may still send spawns it issued
            // before stalling, with IDs from its own cursor. Allocate beyond them.
            int floor = PhotonNetwork.SkipRoomViewIds(TimeoutViewIdGap);
            if (floor > TimeoutViewIdGap) duplicateFloor = floor; // Not wrapped: IDs from here on are this master's.
            EndSettle("previous master silent; timed out; room view IDs from " + floor);
        }
    }

    static string RunMarker(PhotonView view) =>
        view.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 3] as string == "rails" &&
        d[d.Length - 2] is string run && d[d.Length - 1] is string marker ? run + "/" + marker : null;

    // Keeps this master's copy of each run marker and destroys a copy below the floor that
    // arrived later from the previous master's queue. PhotonNetwork.Destroy also clears its cache entry.
    void TickLateDuplicates(double now)
    {
        if (duplicateFloor == 0 || now < nextDuplicateCheck || !PhotonNetwork.IsMasterClient || !PhotonNetwork.IsMessageQueueRunning) return;
        nextDuplicateCheck = now + .25;
        settledMarkers.Clear();
        foreach (PhotonView view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID >= duplicateFloor && view.ViewID < PhotonNetwork.MAX_VIEW_IDS && RunMarker(view) is string key)
                settledMarkers.Add(key);
        if (settledMarkers.Count == 0) return;
        lateDuplicates.Clear();
        foreach (PhotonView view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID > 0 && view.ViewID < duplicateFloor && RunMarker(view) is string key && settledMarkers.Contains(key))
                lateDuplicates.Add(view);
        foreach (PhotonView view in lateDuplicates)
        {
            NoteSettle("removed late duplicate " + RunMarker(view) + " view " + view.ViewID);
            PhotonNetwork.Destroy(view);
        }
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
