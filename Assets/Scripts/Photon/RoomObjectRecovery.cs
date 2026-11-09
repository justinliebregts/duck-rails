using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>One replaceable, server-retained checkpoint per interacted object. Movement stays in PUN's stream.</summary>
public sealed class RoomObjectRecovery : IOnPhotonViewPreNetDestroy
{
    const string Prefix = "recovery:";
    static readonly Dictionary<string, RoomObjectRecovery> objects = new();
    static readonly HashSet<RoomObjectRecovery> dirty = new();
    static readonly List<RoomObjectRecovery> work = new();
    static readonly HashSet<RoomObjectRecovery> pending = new();
    static readonly List<RoomObjectRecovery> pendingWork = new();
    static readonly HashSet<string> failedObjects = new();
    const double AcknowledgementTimeout = 3;
    const int MaxRetries = 3;
    const int RetryBudgetPerTick = 4;
    static int watchdogCursor;
    static double nextWatchdog, failureWindow;
    readonly PhotonView view;
    readonly Func<object[]> capture;
    readonly Action<object[]> restore;
    readonly Func<int> wearer;
    readonly Func<bool> localInteraction;
    readonly string key, revisionKey;
    readonly string spawnToken;
    readonly int viewId;
    Room room;
    object refreshProof;
    int refreshGeneration;
    bool applying, disposed, initialized, deferredRestore, awaitingCheckpoint, needsPublish;
    int revision, revisionWriter, issuedRevision, issuedWriter, lastIssued, firstQueuedRevision, retries;
    double pendingSince;
    public static readonly object ZeroRevision = 0;
    public object WireRevision { get; private set; } = ZeroRevision;
    // Only room-property acknowledgements advance this value. Queued writes
    // have a separate revision chain and cannot gate otherwise valid snapshots.
    public int Revision
    {
        get => revision;
        private set
        {
            if (revision == value) return;
            revision = value;
            // Cache the generation header; never allocate it on a serialization tick.
            WireRevision = spawnToken == null ? (object)value : new object[] { spawnToken, value };
        }
    }
    public string Identity { get; private set; }
    public bool Applying => applying;
    internal string Key => key;
    internal PhotonView View => view;

    internal static bool TryFind(string name, out RoomObjectRecovery record)
    {
        return objects.TryGetValue(name, out record) && record.room == PhotonNetwork.CurrentRoom &&
            !record.disposed && record.view != null && record.view.ViewID == record.viewId && record.view.SpawnToken == record.spawnToken;
    }

    internal void UnexpectedLoss(string reason, int sender, int holdVersion)
    {
        PhotonMasterHandoff.ReportPickupLoss(this, reason, sender, holdVersion, Revision);
    }

    // Only the corroborated controller may publish, and only its current live contents.
    bool CanCapturePickup() => view.TryGetComponent(out BagObject bag) ? bag.CanRefreshPickup :
        view.TryGetComponent(out PhotonGrabbable item) && item.CanRefreshPickup;

    internal bool RefreshFromController(int expectedOwner, int expectedController, int request)
    {
        if (!view.IsMine || view.OwnerActorNr != expectedOwner || view.ControllerActorNr != expectedController ||
            applying || disposed || room != PhotonNetwork.CurrentRoom ||
            !PhotonMasterHandoff.IsCurrentController(room, expectedOwner, expectedController) || !CanCapturePickup()) return false;
        // A capture following several coalesced requests satisfies all earlier requests in this epoch.
        // Bind the proof to this ownership interval and retain it across existing checkpoint CAS retries.
        if (!(refreshProof is long prior) || refreshGeneration != view.OwnershipGeneration ||
            (int)(prior >> 32) != room.MasterClientId || !PhotonMasterHandoff.Fresh((int)prior, PhotonNetwork.ServerTimestamp) ||
            unchecked(request - (int)prior) >= 0)
            refreshProof = Stamp(room.MasterClientId, request);
        refreshGeneration = view.OwnershipGeneration;
        Changed();
        return true;
    }

    public RoomObjectRecovery(PhotonView view, Func<object[]> capture, Action<object[]> restore, Func<int> wearer = null,
        Func<bool> localInteraction = null)
    {
        this.view = view; this.capture = capture; this.restore = restore;
        this.wearer = wearer;
        this.localInteraction = localInteraction;
        spawnToken = view.SpawnToken;
        viewId = view.ViewID;
        // Separate scene objects and every spawned lifetime, including objects sharing a prefab/ID.
        key = Prefix + (spawnToken == null ? "s:" : "r:" + spawnToken + ":") + view.ViewID;
        revisionKey = key + ":v";
        if (spawnToken != null) WireRevision = new object[] { spawnToken, 0 };
        room = PhotonNetwork.CurrentRoom;
        objects[key] = this;
        view.AddCallbackTarget(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        objects.Clear(); dirty.Clear(); work.Clear(); pending.Clear(); pendingWork.Clear(); failedObjects.Clear();
        nextWatchdog = failureWindow = 0;
        watchdogCursor = 0;
    }

    public void Changed()
    {
        if (applying || disposed || !PhotonNetwork.InRoom || room != PhotonNetwork.CurrentRoom || !view.IsMine) return;
        Identity ??= Guid.NewGuid().ToString("N");
        needsPublish = true;
        dirty.Add(this);
    }

    // Called by the existing handoff component, after item LateUpdates, and before its standby flush.
    public static void Flush()
    {
        if (dirty.Count == 0) return;
        work.Clear(); work.AddRange(dirty); dirty.Clear();
        foreach (var item in work) item.Publish();
        work.Clear();
    }

    void Publish()
    {
        if (disposed || applying || view == null || !view.IsMine || !PhotonNetwork.InRoom || room != PhotonNetwork.CurrentRoom) return;
        // A standby flush may queue several transitions before an acknowledgement.
        // Keep that chain separate from the confirmed revision used by snapshots.
        int previous = awaitingCheckpoint ? issuedRevision : Revision;
        int previousWriter = awaitingCheckpoint ? issuedWriter : revisionWriter;
        if (previous == 0 && firstQueuedRevision != 0)
        {
            // Initial properties cannot use CAS on a nonexistent key. Once an
            // initialization has been queued, never repeat it unconditionally:
            // it may have succeeded while its acknowledgement is delayed.
            previous = firstQueuedRevision;
            previousWriter = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        int next = Math.Max(previous, lastIssued) + 1;
        int writer = PhotonNetwork.LocalPlayer.ActorNumber;
        int wornBy = wearer != null ? wearer() : 0;
        if (refreshProof is long proof && (refreshGeneration != view.OwnershipGeneration ||
            (int)(proof >> 32) != room.MasterClientId || !PhotonMasterHandoff.Fresh((int)proof, PhotonNetwork.ServerTimestamp) ||
            !CanCapturePickup())) refreshProof = null;
        var data = new object[refreshProof == null ? 6 : 7];
        data[0] = Identity; data[1] = next; data[2] = writer;
        data[3] = wornBy != 0 ? wornBy : view.OwnerActorNr; data[4] = capture(); data[5] = wornBy;
        if (refreshProof != null) data[6] = refreshProof;
        var values = new Hashtable { [key] = data, [revisionKey] = Stamp(next, writer) };
        // CAS prevents a delayed previous controller from replacing a newer owner's checkpoint.
        // Initialization is written only by the first controlling client; subsequent changes compare revisions.
        // Include the writer: after A's revision 2 loses to B's revision 2, A's queued
        // revision 3 must not succeed just because the numeric revision is now 2.
        var expected = previous == 0 ? null : new Hashtable { [revisionKey] = Stamp(previous, previousWriter) };
        if (!awaitingCheckpoint) { pendingSince = Time.realtimeSinceStartupAsDouble; retries = 0; }
        // Set these before sending: offline rooms and deterministic fixtures acknowledge synchronously.
        awaitingCheckpoint = true;
        issuedRevision = lastIssued = next; issuedWriter = writer;
        needsPublish = false;
        pending.Add(this);
        bool initializing = previous == 0;
        if (initializing) firstQueuedRevision = next;
        if (!room.SetCustomProperties(values, expected))
        {
            // A failed enqueue did not create a new server revision. Retain the
            // previous chain and retry the current state through the same watchdog.
            issuedRevision = previous; issuedWriter = previousWriter;
            needsPublish = true;
            if (initializing) firstQueuedRevision = 0;
        }
    }

    static long Stamp(int version, int writer) => ((long)version << 32) | (uint)writer;

    public void Reconcile()
    {
        if (disposed || view == null || room == null || room != PhotonNetwork.CurrentRoom ||
            !(room.CustomProperties[key] is object[] data) || (data.Length != 6 && data.Length != 7) ||
            !(data[0] is string identity) || !(data[1] is int revision) ||
            !(data[2] is int writer) || !(data[3] is int owner) || !(data[4] is object[] state)) return;
        if (revision < Revision || (revision == Revision && writer == revisionWriter && identity == Identity && !deferredRestore)) return;
        bool ownAcknowledgement = writer == PhotonNetwork.LocalPlayer?.ActorNumber && lastIssued >= revision;
        bool newer = revision != Revision || writer != revisionWriter;
        bool ownerConflict = (initialized || view.HasLiveOwnership) && owner != view.OwnerActorNr;
        bool preserveLiveState = (ownerConflict && (data.Length == 6 || !PhotonMasterHandoff.CanRepairObserver(this, owner, writer, revision, data[6])))
            || (localInteraction != null && localInteraction())
            || (view.IsMine && (awaitingCheckpoint || needsPublish));
        Identity = identity;
        Revision = revision;
        revisionWriter = writer;
        initialized = true;
        if (ownAcknowledgement)
        {
            // An acknowledgement is not a command to replay an older pose, hold,
            // or bag inventory. More local changes may already have happened.
            if (revision >= issuedRevision && !needsPublish) ClearPending();
            else if (newer) pendingSince = Time.realtimeSinceStartupAsDouble;
            return;
        }
        if (preserveLiveState)
        {
            // Ownership events and current local interactions take precedence over
            // a delayed previous controller's checkpoint. Revisit after a transfer.
            deferredRestore = true;
            if (view.IsMine && newer)
            {
                issuedRevision = Revision; issuedWriter = revisionWriter;
                needsPublish = true;
                dirty.Add(this);
            }
            return;
        }
        deferredRestore = false;
        ClearPending();
        needsPublish = false;
        dirty.Remove(this);
        applying = true;
        try
        {
            if (ownerConflict) view.MarkLiveOwnership();
            view.OwnerActorNr = owner;
            var player = room.GetPlayer(owner);
            view.ControllerActorNr = owner > 0 && player != null && !player.IsInactive ? owner : room.MasterClientId;
            restore(state);
        }
        finally { applying = false; }
    }

    public bool Accept(object header) => spawnToken == null
        ? header is int revision && revision == Revision
        : header is object[] fields && fields.Length == 2 && fields[0] is string token && token == spawnToken
            && fields[1] is int version && version == Revision;

    public static void OperationResponse(OperationResponse response)
    {
        // Photon supplies no item key/request identity in these errors. A skin,
        // master-election, or other object's failure says nothing about our saves.
        if (response.OperationCode != OperationCode.SetProperties || response.ReturnCode == 0 || pending.Count == 0) return;
        if (DuckzLog.AllowWarning(out var notice, "CheckpointOperation"))
            notice.Write($"Item checkpoint operation error {response.ReturnCode}: {response.DebugMessage}; pending={pending.Count}. Awaiting per-item acknowledgement; no pickup rollback.");
    }

    void ClearPending()
    {
        awaitingCheckpoint = false;
        issuedRevision = Revision; issuedWriter = revisionWriter;
        retries = 0;
        pending.Remove(this);
    }

    // Only outstanding saves are visited, once per second. No idle object scan,
    // ownership claims, inventory restoration, or respawning is performed here.
    public static void Tick()
    {
        if (pending.Count == 0) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMessageQueueRunning || now < nextWatchdog) return;
        nextWatchdog = now + 1;
        pendingWork.Clear(); pendingWork.AddRange(pending);
        int visited = 0, attempts = 0;
        // Bound allocations/traffic during a room-wide stall. Rotate the start
        // so early entries cannot consume every retry slot on subsequent ticks.
        while (visited < pendingWork.Count && attempts < RetryBudgetPerTick)
        {
            watchdogCursor %= pendingWork.Count;
            var item = pendingWork[watchdogCursor++];
            visited++;
            if (item.CheckPending(now)) attempts++;
        }
        pendingWork.Clear();
    }

    bool CheckPending(double now)
    {
        if (disposed || view == null || room != PhotonNetwork.CurrentRoom || !view.IsMine)
        { ClearPending(); return false; }
        if (!awaitingCheckpoint || now - pendingSince < AcknowledgementTimeout) return false;
        Reconcile();
        if (!awaitingCheckpoint || !view.IsMine) return false;
        RecordFailure(now);
        if (retries >= MaxRetries)
        {
            // Stop automatic traffic. A subsequent explicit interaction can start
            // a new save; a late acknowledgement is still accepted normally.
            ClearPending(); needsPublish = false; dirty.Remove(this);
            return false;
        }
        retries++;
        pendingSince = now;
        // Rebase on the confirmed cache. CAS still protects against a newer
        // server value we have not received. Never reuse an issued revision:
        // a late successful retry must not acknowledge a different payload.
        if (Revision != 0) { issuedRevision = Revision; issuedWriter = revisionWriter; }
        needsPublish = true;
        dirty.Remove(this);
        Publish();
        return true;
    }

    void RecordFailure(double now)
    {
        if (DuckzLog.AllowWarning(out var notice, "CheckpointTimeout"))
            notice.Write($"Item checkpoint stalled: view={view.ViewID}, owner={view.OwnerActorNr}, controller={view.ControllerActorNr}, confirmed={Revision}/{revisionWriter}, issued={issuedRevision}/{issuedWriter}, retry={retries}/{MaxRetries}.", view);
        if (now >= failureWindow) { failedObjects.Clear(); failureWindow = now + 30; }
        if (failedObjects.Count < 3 && failedObjects.Add(key) && failedObjects.Count == 3)
        {
            if (DuckzLog.AllowWarning(out var roomNotice, "CheckpointRoomFailures"))
                roomNotice.Write("Item checkpoint failures affected at least three objects within 30 seconds. Local recovery is active; room state was not reset.");
        }
    }

    public static void PropertiesChanged(Hashtable changes)
    {
        foreach (object k in changes.Keys)
        {
            if (k is string name && objects.TryGetValue(name, out var item))
                item.Reconcile();
        }
    }

    public static void ReconcileAll()
    {
        foreach (var item in objects.Values) item.Reconcile();
    }

    public static void JoinedRoom()
    {
        // Editor can enter Play in Main before joining. Its scene objects have already run Start.
        dirty.Clear();
        failedObjects.Clear(); failureWindow = nextWatchdog = 0;
        watchdogCursor = 0;
        foreach (var item in objects.Values)
        {
            if (item.room != PhotonNetwork.CurrentRoom)
            {
                item.room = PhotonNetwork.CurrentRoom;
                item.ClearPending();
                item.revisionWriter = item.Revision = item.issuedRevision = item.issuedWriter = item.lastIssued = item.firstQueuedRevision = 0;
                item.initialized = item.deferredRestore = item.needsPublish = false;
                item.Identity = null;
                item.refreshProof = null;
            }
            item.Reconcile();
        }
    }

    // Check only once per saved-loadout restore. A live room copy takes precedence over a disk copy.
    public static bool Exists(string roomName, string identity)
    {
        var current = PhotonNetwork.CurrentRoom;
        if (current == null || current.Name != roomName || string.IsNullOrEmpty(identity)) return false;
        foreach (object value in current.CustomProperties.Values)
            if (value is object[] data && (data.Length == 6 || data.Length == 7) && data[0] is string token && token == identity)
            {
                // A departed wearer's old copy is awaiting destruction, not a live
                // world pickup. It must not suppress their saved loadout on rejoin.
                int wornBy = (int)data[5];
                return wornBy == 0 || current.GetPlayer(wornBy) != null;
            }
        return false;
    }

    public void OnPreNetDestroy(PhotonView rootView)
    {
        if (view != null && view.IsMine && PhotonNetwork.InRoom && room == PhotonNetwork.CurrentRoom)
            room.SetCustomProperties(new Hashtable { [key] = null, [revisionKey] = null });
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; dirty.Remove(this); pending.Remove(this);
        if (objects.TryGetValue(key, out var current) && ReferenceEquals(current, this)) objects.Remove(key);
        view.RemoveCallbackTarget(this);
    }
}
