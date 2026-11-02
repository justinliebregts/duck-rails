using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Deterministic delayed transport; run only in a disposable Unity batch project.
public static class PickupRecoveryChecks
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    sealed class Actor : Player { public Actor(int id) : base("investigation", id, true) { } }
    sealed class QueuedRoom : Room
    {
        public readonly List<(Hashtable values, Hashtable expected)> Pending = new();
        public bool RejectEnqueue;
        public QueuedRoom() : base("isolated-pickup-investigation", new RoomOptions { DeleteNullProperties = true }) { }
        public override bool SetCustomProperties(Hashtable values, Hashtable expected = null, WebFlags flags = null)
        { if (RejectEnqueue) return false; Pending.Add((values, expected)); return true; }
        public bool Deliver(int index)
        {
            var op = Pending[index];
            if (op.expected != null)
                foreach (var pair in op.expected)
                    if (!Equals(CustomProperties[pair.Key], pair.Value)) return false;
            foreach (var pair in op.values) CustomProperties[pair.Key] = pair.Value;
            RoomObjectRecovery.PropertiesChanged(op.values);
            return true;
        }
    }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(n, Flags); if (f != null) return f; }
        throw new Exception(n);
    }
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
    static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); Debug.Log("PASS pickup recovery: " + label); }
    static long Stamp(int v, int w) => ((long)v << 32) | (uint)w;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        var client = PhotonNetwork.NetworkingClient;
        var oldRoom = client.CurrentRoom; var oldLocal = client.LocalPlayer;
        var oldState = client.State; var oldDebug = client.LoadBalancingPeer.DebugOut;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int local = 1; local <= 3; local++)
            {
                TestPickup(local, false);
                TestPickup(local, true);
            }
            TestOwnWriterRejection();
            TestEnqueueFailure();
            TestDelayedAcknowledgement();
            TestBoundedRetriesAndAuthorityLoss();
            TestOwnershipTimeout();
            TestPendingBagContents();
            TestPreviousOwnerCheckpoint();
            TestLateSaveAfterTransfer();
            TestUnacknowledgedInitialization();
            TestOwnerChangeWithoutControllerChange();
            TestPendingSweepAllocations();
            TestRoomRetryBudget();
            TestObserverSnapshotOrdering();
            TestRoomDiagnosticsAndIdle();
            checks += PickupRegrabChecks.RunCases();
            Debug.Log("PickupRecoveryChecks ALL PASSED (" + checks + " assertions)");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally
        {
            client.CurrentRoom = oldRoom;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, oldLocal);
            client.State = oldState; client.LoadBalancingPeer.DebugOut = oldDebug;
        }
    }
    static QueuedRoom Setup(int local)
    {
        var client = PhotonNetwork.NetworkingClient;
        var room = new QueuedRoom { LoadBalancingClient = client };
        for (int i = 1; i <= 3; i++) room.AddPlayer(new Actor(i));
        client.CurrentRoom = room;
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, room.GetPlayer(local));
        Set(room, "masterClientId", 1);
        client.State = ClientState.Joined; client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
        return room;
    }
    static PhotonGrabbable Seed(QueuedRoom room, int writer, int id)
    {
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>();
        view.ViewID = id; view.OwnershipTransfer = OwnershipOption.Takeover;
        view.OwnerActorNr = writer; view.ControllerActorNr = writer;
        string key = "recovery:s:" + id;
        var state = new object[] { false, 0, false, 1, true, 0d, false,
            Vector3.zero, Quaternion.identity, new Vector3(4, 1, 2), Quaternion.identity, 0, 0, 0, null };
        room.CustomProperties[key] = new object[] { "seed-" + id, 1, writer, writer, state, 0 };
        room.CustomProperties[key + ":v"] = Stamp(1, writer);
        Call(item, "EnsureRecovery");
        return item;
    }
    static void TestPickup(int local, bool unrelatedFailure)
    {
        var room = Setup(local);
        int writer = local == 1 ? 2 : 1;
        var item = Seed(room, writer, 700 + local);
        var view = item.GetComponent<PhotonView>();
        var origin = item.transform.position;
        var hand = new GameObject("investigation hand").AddComponent<PlayerHand>(); hand.enabled = false;
        try
        {
            item.Grab(hand);
            // Deliver the real ownership event's setters and callback, without contacting Photon.
            var previousOwner = view.Owner;
            view.OwnerActorNr = local; view.ControllerActorNr = local;
            item.OnOwnershipTransfered(view, previousOwner);
            item.transform.position = origin + Vector3.up * 2;
            RoomObjectRecovery.Flush();
            Check(item.IsHeld && Get<RoomObjectRecovery>(item, "recovery").Revision == 1,
                "actor " + local + " grabs immediately without advancing the confirmed revision");
            if (unrelatedFailure)
            {
                RoomObjectRecovery.OperationResponse(new OperationResponse {
                    OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation,
                    DebugMessage = "Simulated unrelated property CAS failure" });
                Check(item.IsHeld && item.transform.position != origin && view.OwnerActorNr == local,
                    "actor " + local + " unrelated error preserves hold, pose and ownership");
                Check(room.Deliver(0), "valid pickup still commits after an unrelated error");
                Check(item.IsHeld && Get<RoomObjectRecovery>(item, "recovery").Revision == 2,
                    "acknowledgement confirms the revision without detaching the hand");
            }
            else
            {
                Check(room.Deliver(0), "normal checkpoint CAS succeeds");
                Check(item.IsHeld && item.transform.position != origin,
                    "actor " + local + " control: acknowledgment preserves grab and hand position");
            }
        }
        finally { Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(hand.gameObject); }
    }
    static void TestOwnWriterRejection()
    {
        var room = Setup(1);
        var item = Seed(room, 1, 710);
        try
        {
            var record = Get<RoomObjectRecovery>(item, "recovery");
            Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush();
            // Model a rejected property write: no property event updates the server/local cache.
            RoomObjectRecovery.OperationResponse(new OperationResponse {
                OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation });
            Check(record.Revision == 1 && record.Accept(1), "rejected save does not poison the confirmed revision or stream");
            Timeout(record);
            Check(room.Deliver(1), "watchdog retries a rejected same-writer save against the confirmed stamp");
            Check(record.Revision == 3 && !Get<bool>(record, "awaitingCheckpoint"), "retry confirms a fresh revision and clears pending state");
        }
        finally { Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject); }
    }

    static void Timeout(RoomObjectRecovery record)
    { Set(record, "pendingSince", Time.realtimeSinceStartupAsDouble - 4); Call(record, "CheckPending", Time.realtimeSinceStartupAsDouble); }
    static void Destroy(PhotonGrabbable item)
    { Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject); }
    static void Save(PhotonGrabbable item) { Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush(); }

    static void TestEnqueueFailure()
    {
        var room = Setup(1); var item = Seed(room, 1, 711);
        try
        {
            room.RejectEnqueue = true; Save(item);
            var record = Get<RoomObjectRecovery>(item, "recovery");
            Check(room.Pending.Count == 0 && record.Revision == 1, "failed enqueue is not treated as a server revision");
            room.RejectEnqueue = false; Timeout(record);
            Check(room.Deliver(0) && record.Revision == 3, "failed enqueue is retried without requiring another interaction");
        }
        finally { Destroy(item); }
    }

    static void TestDelayedAcknowledgement()
    {
        var room = Setup(1); var item = Seed(room, 1, 712);
        try
        {
            var record = Get<RoomObjectRecovery>(item, "recovery");
            item.transform.position = Vector3.one; Save(item); // revision 2, delayed
            item.transform.position = Vector3.one * 9;
            Timeout(record); // revision 3, expects 1
            Check(room.Deliver(0), "original delayed save can succeed after a timeout");
            Check(item.transform.position == Vector3.one * 9 && Get<bool>(record, "awaitingCheckpoint"),
                "late original acknowledgement does not restore old pose or acknowledge a different retry payload");
            Check(!room.Deliver(1), "retry based on an old cache safely fails CAS after original success");
            Timeout(record);
            Check(room.Deliver(2) && record.Revision == 4, "next retry rebases on the newly acknowledged revision");
            var state = (object[])((object[])room.CustomProperties["recovery:s:712"])[4];
            Check((Vector3)state[9] == Vector3.one * 9, "recovery saves the latest pose instead of the failed payload");
        }
        finally { Destroy(item); }
    }

    static void TestBoundedRetriesAndAuthorityLoss()
    {
        var room = Setup(1); var item = Seed(room, 1, 713);
        try
        {
            var record = Get<RoomObjectRecovery>(item, "recovery"); Save(item);
            for (int i = 0; i < 10; i++) Timeout(record);
            Check(room.Pending.Count == 4 && !Get<bool>(record, "awaitingCheckpoint"), "automatic retries stop after three attempts");
            Save(item);
            Check(room.Pending.Count == 5, "new explicit interaction remains usable after retry exhaustion");
            var view = item.GetComponent<PhotonView>(); view.OwnerActorNr = 2; view.ControllerActorNr = 2;
            Timeout(record);
            Check(room.Pending.Count == 5 && view.OwnerActorNr == 2, "watchdog stops writing after ownership passes to another player");
            record.Dispose(); Timeout(record);
            Check(room.Pending.Count == 5, "disposed objects cannot retry or resurrect a checkpoint");
        }
        finally { Destroy(item); }
    }

    static void TestOwnershipTimeout()
    {
        var room = Setup(1); var item = Seed(room, 2, 714);
        var hand = new GameObject("timeout hand").AddComponent<PlayerHand>(); hand.enabled = false;
        try
        {
            item.Grab(hand);
            Check(item.IsHeld && item.PickupPending, "unconfirmed ownership still allows immediate predicted pickup");
            Set(item, "pickupStartedAt", Time.realtimeSinceStartupAsDouble - 4);
            Call(item, "CheckPendingPickup");
            var view = item.GetComponent<PhotonView>();
            Check(!item.IsHeld && !item.PickupPending && view.OwnerActorNr == 2, "ownership timeout clears only our predicted hold without stealing authority");
            var previous = view.Owner; view.OwnerActorNr = 1; view.ControllerActorNr = 1;
            item.OnOwnershipTransfered(view, previous);
            Check(!item.IsHeld, "late grant does not reattach a canceled hand");
            item.Grab(hand);
            Check(item.IsHeld, "fresh pickup is possible after a timed-out grant");
            Set(item, "pickupPending", true); Set(item, "pickupStartedAt", Time.realtimeSinceStartupAsDouble - 4);
            Call(item, "CheckPendingPickup");
            Check(item.IsHeld && !item.PickupPending, "watchdog preserves hold when ownership arrived without a completion callback");
        }
        finally { Destroy(item); Object.DestroyImmediate(hand.gameObject); }
    }

    static void TestPendingBagContents()
    {
        var room = Setup(1);
        var go = new GameObject("pending contents"); go.SetActive(false);
        var bag = go.AddComponent<BagObject>(); Set(bag, "rb", go.GetComponent<Rigidbody>());
        // Rails uses its separate RailCargo component, not Duckz HelicopterCargo.
        bag.photonView.ViewID = 715; bag.photonView.OwnerActorNr = 1; bag.photonView.ControllerActorNr = 1;
        var slotGo = new GameObject("slot"); slotGo.SetActive(false); slotGo.transform.SetParent(go.transform);
        var slot = slotGo.AddComponent<BagSlot>(); Set(slot, "itemKey", "ItemGoldBar");
        Get<List<BagSlot>>(bag, "slots").Add(slot);
        try
        {
            Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush(); Check(room.Deliver(0), "initial bag contents confirmed");
            Set(slot, "itemKey", "WeaponNailBat"); Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
            Set(slot, "itemKey", ""); Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
            var record = Get<RoomObjectRecovery>(bag, "recovery");
            RoomObjectRecovery.OperationResponse(new OperationResponse { OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation });
            Check(slot.ItemKey == "", "unrelated failure cannot restore removed bag contents");
            Check(room.Deliver(1) && slot.ItemKey == "", "earlier bag acknowledgement does not restore newer local contents");
            Check(room.Deliver(2) && record.Revision == 3, "chained standby/contents flush preserves the latest save");
            Check(((string[])((object[])((object[])room.CustomProperties["recovery:s:715"])[4])[10])[0] == "", "retained bag contents have no duplicated gold or weapon");
        }
        finally { Get<RoomObjectRecovery>(bag, "recovery")?.Dispose(); Object.DestroyImmediate(go); }
    }

    static void TestPreviousOwnerCheckpoint()
    {
        var room = Setup(1); var item = Seed(room, 2, 716);
        var hand = new GameObject("stale checkpoint hand").AddComponent<PlayerHand>(); hand.enabled = false;
        try
        {
            item.Grab(hand);
            var view = item.GetComponent<PhotonView>(); var previous = view.Owner;
            view.OwnerActorNr = 1; view.ControllerActorNr = 1; item.OnOwnershipTransfered(view, previous);
            var old = (object[])room.CustomProperties["recovery:s:716"];
            room.CustomProperties["recovery:s:716"] = new object[] { old[0], 2, 2, 2, old[4], 0 };
            room.CustomProperties["recovery:s:716:v"] = Stamp(2, 2);
            var record = Get<RoomObjectRecovery>(item, "recovery"); record.Reconcile();
            Check(item.IsHeld && view.OwnerActorNr == 1, "delayed previous-owner checkpoint cannot revoke current hold or ownership");
            RoomObjectRecovery.Flush(); Check(room.Deliver(0), "new owner publishes current hold against the retained revision");
            Check(item.IsHeld && record.Revision == 3, "current hold remains attached after reconciling the older controller's save");
        }
        finally { Destroy(item); Object.DestroyImmediate(hand.gameObject); }
    }

    static void TestRoomDiagnosticsAndIdle()
    {
        var room = Setup(1);
        var items = new List<PhotonGrabbable>();
        try
        {
            typeof(RoomObjectRecovery).GetField("failureWindow", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 0d);
            for (int i = 0; i < 3; i++)
            { var item = Seed(room, 1, 720 + i); items.Add(item); Save(item); Timeout(Get<RoomObjectRecovery>(item, "recovery")); }
            var failures = (HashSet<string>)typeof(RoomObjectRecovery).GetField("failedObjects", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(failures.Count == 3, "room diagnostic detects distinct stalled objects with bounded tracking");
            foreach (var item in items) Get<RoomObjectRecovery>(item, "recovery").Dispose();
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), typeof(RoomObjectRecovery).GetMethod("Tick", BindingFlags.Public | BindingFlags.Static));
            tick(); RoomObjectRecovery.Flush();
            int writes = room.Pending.Count; long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { tick(); RoomObjectRecovery.Flush(); }
            Check(GC.GetAllocatedBytesForCurrentThread() == before && room.Pending.Count == writes, "10,000 idle watchdog/flush calls allocate nothing and send no traffic");
        }
        finally { foreach (var item in items) Destroy(item); }
    }

    static void TestLateSaveAfterTransfer()
    {
        var room = Setup(1); var item = Seed(room, 1, 717);
        var hand = new GameObject("transfer hand").AddComponent<PlayerHand>(); hand.enabled = false;
        try
        {
            item.Grab(hand); Save(item);
            var view = item.GetComponent<PhotonView>(); var previous = view.Owner;
            view.OwnerActorNr = 2; view.ControllerActorNr = 2; item.OnOwnershipTransfered(view, previous);
            Check(!item.IsHeld, "real transfer clears the previous owner's local hold");
            Check(room.Deliver(0), "previous owner's save may still succeed after the transfer");
            Check(view.OwnerActorNr == 2 && !item.IsHeld, "late own acknowledgement cannot reclaim another player's item");
            var record = Get<RoomObjectRecovery>(item, "recovery"); Timeout(record);
            Check(room.Pending.Count == 1, "old owner's watchdog sends nothing after the late save");
        }
        finally { Destroy(item); Object.DestroyImmediate(hand.gameObject); }
    }

    static void TestUnacknowledgedInitialization()
    {
        var room = Setup(1);
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>(); view.ViewID = 718; view.OwnerActorNr = 1; view.ControllerActorNr = 1;
        try
        {
            Save(item); var record = Get<RoomObjectRecovery>(item, "recovery");
            Check(room.Pending[0].expected == null, "first checkpoint initializes a previously nonexistent key");
            Timeout(record);
            Check(room.Pending[1].expected != null, "unacknowledged initialization is never retried as an unconditional overwrite");
            for (int i = 0; i < 5; i++) Timeout(record);
            Save(item);
            Check(room.Pending[room.Pending.Count - 1].expected != null, "explicit interaction after exhaustion also cannot overwrite an unseen newer checkpoint");
            Check(room.Deliver(0), "original initialization can still be acknowledged late");
            Timeout(record);
            Check(room.Deliver(room.Pending.Count - 1) && record.Revision > 1, "latest state is recoverable once initialization is acknowledged");
        }
        finally { Destroy(item); }
    }

    static void TestOwnerChangeWithoutControllerChange()
    {
        var room = Setup(3); var item = Seed(room, 1, 730);
        try
        {
            var view = item.GetComponent<PhotonView>();
            var record = Get<RoomObjectRecovery>(item, "recovery");
            var initial = (object[])room.CustomProperties["recovery:s:730"];
            initial[3] = 0; view.OwnerActorNr = 0; // Room-owned, controlled by master actor 1.
            var state = (object[])((object[])initial[4]).Clone(); state[0] = true; state[1] = 1;
            room.CustomProperties["recovery:s:730"] = new object[] { initial[0], 2, 1, 1, state, 0 };
            room.CustomProperties["recovery:s:730:v"] = Stamp(2, 1);
            record.Reconcile();
            Check(!item.IsHeldByAnyPlayer && view.OwnerActorNr == 0, "checkpoint ahead of ownership waits for the ownership event");
            var previous = view.Owner; view.OwnerActorNr = 1; view.ControllerActorNr = 1;
            item.OnOwnershipTransfered(view, previous);
            Check(item.IsHeldByAnyPlayer && item.HeldActor == 1,
                "owner-only change applies deferred held checkpoint even when controller remains master");
        }
        finally { Destroy(item); }
    }

    static void TestPendingSweepAllocations()
    {
        var room = Setup(1); var items = new List<PhotonGrabbable>();
        var checks = new List<Func<double, bool>>();
        try
        {
            for (int i = 0; i < 40; i++)
            {
                var item = Seed(room, 1, 740 + i); items.Add(item); Save(item);
                var record = Get<RoomObjectRecovery>(item, "recovery");
                Set(record, "pendingSince", 1000d);
                checks.Add((Func<double, bool>)Delegate.CreateDelegate(typeof(Func<double, bool>), record,
                    typeof(RoomObjectRecovery).GetMethod("CheckPending", Flags)));
            }
            foreach (var check in checks) check(1001d);
            int writes = room.Pending.Count; long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) foreach (var check in checks) check(1001d);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && room.Pending.Count == writes,
                "400,000 actual pending-item checks allocate zero bytes and emit no premature retries");
        }
        finally { foreach (var item in items) Destroy(item); }
    }

    static void TestRoomRetryBudget()
    {
        var room = Setup(1); var items = new List<PhotonGrabbable>();
        var records = new List<RoomObjectRecovery>();
        var nextTick = typeof(RoomObjectRecovery).GetField("nextWatchdog", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            for (int i = 0; i < 40; i++)
            {
                var item = Seed(room, 1, 800 + i); items.Add(item); Save(item);
                records.Add(Get<RoomObjectRecovery>(item, "recovery"));
            }
            for (int tick = 0; tick < 10; tick++)
            {
                foreach (var record in records) Set(record, "pendingSince", Time.realtimeSinceStartupAsDouble - 4);
                int before = room.Pending.Count; nextTick.SetValue(null, 0d); RoomObjectRecovery.Tick();
                Check(room.Pending.Count - before == 4, "room-wide timeout sends at most four retries per watchdog tick");
            }
            foreach (var record in records)
                Check(Get<int>(record, "retries") == 1, "round-robin retry budget reaches every stalled object without starvation");

            // Force the real collection-copy/sweep path, not the timer fast exit.
            foreach (var record in records) Set(record, "pendingSince", Time.realtimeSinceStartupAsDouble + 1000);
            var tickMethod = typeof(RoomObjectRecovery).GetMethod("Tick", BindingFlags.Public | BindingFlags.Static);
            var tickAction = (Action)Delegate.CreateDelegate(typeof(Action), tickMethod);
            nextTick.SetValue(null, 0d); tickAction();
            long allocated = 0; int writes = room.Pending.Count;
            for (int i = 0; i < 1000; i++)
            {
                nextTick.SetValue(null, 0d); // Reflection setup is outside the measured interval.
                long before = GC.GetAllocatedBytesForCurrentThread(); tickAction();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Check(allocated == 0 && room.Pending.Count == writes, "1,000 real pending-list sweeps allocate nothing and send no healthy-room traffic");
        }
        finally { foreach (var item in items) Destroy(item); }
    }

    static object[] Packet(PhotonGrabbable item)
    {
        var stream = new PhotonStream(true, null); Set(stream, "writeData", new List<object>(20));
        item.OnPhotonSerializeView(stream, default); return stream.ToArray();
    }

    static void TestObserverSnapshotOrdering()
    {
        // Separate sender/observer caches; pass actual serialized item packets
        // and retained records across them, with deliberately delayed delivery.
        var senderRoom = Setup(1); var sender = Seed(senderRoom, 1, 731);
        var hand = new GameObject("packet hand").AddComponent<PlayerHand>(); hand.enabled = false;
        object[] oldLoose, pendingHeld, confirmedHeld, checkpoint;
        try
        {
            oldLoose = Packet(sender);
            sender.Grab(hand); Save(sender); pendingHeld = Packet(sender);
            Check(senderRoom.Deliver(0), "sender's held checkpoint commits");
            confirmedHeld = Packet(sender); checkpoint = (object[])senderRoom.CustomProperties["recovery:s:731"];
        }
        finally { Destroy(sender); Object.DestroyImmediate(hand.gameObject); }
        var observerRoom = Setup(3); var observer = Seed(observerRoom, 1, 731);
        try
        {
            var view = observer.GetComponent<PhotonView>();
            var owner = new PhotonMessageInfo(observerRoom.GetPlayer(1), 0, view);
            observer.OnPhotonSerializeView(new PhotonStream(false, pendingHeld), owner);
            Check(observer.IsHeldByAnyPlayer, "observer sees current owner pickup before checkpoint acknowledgement");
            observerRoom.CustomProperties["recovery:s:731"] = checkpoint;
            observerRoom.CustomProperties["recovery:s:731:v"] = Stamp(2, 1);
            Get<RoomObjectRecovery>(observer, "recovery").Reconcile();
            observer.OnPhotonSerializeView(new PhotonStream(false, oldLoose), owner);
            Check(observer.IsHeldByAnyPlayer, "old loose snapshot cannot undo a newer confirmed hold");
            observer.OnPhotonSerializeView(new PhotonStream(false, confirmedHeld), owner);
            Check(observer.HeldActor == 1, "matching confirmed snapshot continues updating the observer");
            var wrongOwner = new PhotonMessageInfo(observerRoom.GetPlayer(2), 0, view);
            var forgedLoose = (object[])oldLoose.Clone(); forgedLoose[0] = 2;
            observer.OnPhotonSerializeView(new PhotonStream(false, forgedLoose), wrongOwner);
            Check(observer.IsHeldByAnyPlayer, "matching revision from a stale controller still cannot release another player's hold");
        }
        finally { Destroy(observer); }
    }
}
