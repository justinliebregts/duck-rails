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
    readonly PhotonView view;
    readonly Func<object[]> capture;
    readonly Action<object[]> restore;
    readonly Func<int> wearer;
    readonly string key, revisionKey;
    readonly string spawnToken;
    Room room;
    bool applying, disposed;
    int confirmed, confirmedWriter, revision, revisionWriter;
    public static readonly object ZeroRevision = 0;
    public object WireRevision { get; private set; } = ZeroRevision;
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

    public RoomObjectRecovery(PhotonView view, Func<object[]> capture, Action<object[]> restore, Func<int> wearer = null)
    {
        this.view = view; this.capture = capture; this.restore = restore;
        this.wearer = wearer;
        spawnToken = view.SpawnToken;
        // Separate scene objects and every spawned lifetime, including objects sharing a prefab/ID.
        key = Prefix + (spawnToken == null ? "s:" : "r:" + spawnToken + ":") + view.ViewID;
        revisionKey = key + ":v";
        if (spawnToken != null) WireRevision = new object[] { spawnToken, 0 };
        room = PhotonNetwork.CurrentRoom;
        objects[key] = this;
        view.AddCallbackTarget(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { objects.Clear(); dirty.Clear(); work.Clear(); }

    public void Changed()
    {
        if (applying || disposed || !PhotonNetwork.InRoom || room != PhotonNetwork.CurrentRoom || !view.IsMine) return;
        Identity ??= Guid.NewGuid().ToString("N");
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
        int previous = Revision;
        int next = previous + 1;
        int writer = PhotonNetwork.LocalPlayer.ActorNumber;
        int wornBy = wearer != null ? wearer() : 0;
        var data = new object[] { Identity, next, writer,
            wornBy != 0 ? wornBy : view.OwnerActorNr, capture(), wornBy };
        var values = new Hashtable { [key] = data, [revisionKey] = Stamp(next, writer) };
        // CAS prevents a delayed previous controller from replacing a newer owner's checkpoint.
        // Initialization is written only by the first controlling client; subsequent changes compare revisions.
        // Include the writer: after A's revision 2 loses to B's revision 2, A's queued
        // revision 3 must not succeed just because the numeric revision is now 2.
        var expected = previous == 0 ? null : new Hashtable { [revisionKey] = Stamp(previous, revisionWriter) };
        if (room.SetCustomProperties(values, expected)) { Revision = next; revisionWriter = writer; }
    }

    static long Stamp(int version, int writer) => ((long)version << 32) | (uint)writer;

    public void Reconcile()
    {
        if (disposed || view == null || room == null || room != PhotonNetwork.CurrentRoom ||
            !(room.CustomProperties[key] is object[] data) || data.Length != 6 ||
            !(data[0] is string identity) || !(data[1] is int revision) ||
            !(data[2] is int writer) || !(data[3] is int owner) || !(data[4] is object[] state)) return;
        if (revision < confirmed || (revision == confirmed && writer == confirmedWriter && identity == Identity)) return;
        confirmed = revision;
        confirmedWriter = writer;
        Identity = identity;
        // Our optimistic state can be ahead of an earlier acknowledgement from the same reliable queue.
        if (writer == PhotonNetwork.LocalPlayer?.ActorNumber && Revision >= revision) return;
        Revision = revision;
        revisionWriter = writer;
        dirty.Remove(this);
        applying = true;
        try
        {
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
        if (response.OperationCode != OperationCode.SetProperties || response.ReturnCode != ErrorCode.InvalidOperation) return;
        foreach (var item in objects.Values)
        {
            if (item.Revision <= item.confirmed || item.room != PhotonNetwork.CurrentRoom || item.room == null ||
                !(item.room.CustomProperties[item.key] is object[] data) || data.Length != 6 ||
                (int)data[2] == PhotonNetwork.LocalPlayer.ActorNumber) continue;
            // A CAS loser adopts the winner, including an equal revision from a competing writer.
            item.confirmed = 0;
            item.Reconcile();
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
        foreach (var item in objects.Values)
        {
            if (item.room != PhotonNetwork.CurrentRoom)
            {
                item.room = PhotonNetwork.CurrentRoom;
                item.confirmed = item.confirmedWriter = item.revisionWriter = item.Revision = 0;
                item.Identity = null;
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
            if (value is object[] data && data.Length == 6 && data[0] is string token && token == identity)
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
        disposed = true; dirty.Remove(this);
        if (objects.TryGetValue(key, out var current) && ReferenceEquals(current, this)) objects.Remove(key);
        view.RemoveCallbackTarget(this);
    }
}
