using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using InstantiateParameters = Photon.Pun.InstantiateParameters;

// Deterministic transport fixture: exercises real checkpoints, consumers, CAS conflicts and packet layouts.
// Quest suspend timing and server delivery still require the two-device play test.
public static class RoomObjectRecoveryChecks
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static bool spawnedFixture;
    sealed class Actor : Player { public Actor(int id, bool local = false) : base("recovery test", id, local) { } }
    sealed class CleanupPool : IPunPrefabPool
    {
        public int Destroyed;
        public GameObject Instantiate(string id, Vector3 position, Quaternion rotation) => throw new NotSupportedException();
        public void Destroy(GameObject obj) { Destroyed++; Object.DestroyImmediate(obj); }
    }
    sealed class TestRoom : Room
    {
        public int Writes;
        public bool QueueWrites;
        readonly List<(Hashtable values, Hashtable expected)> queued = new();
        public TestRoom() : base("recovery test", new RoomOptions { DeleteNullProperties = true }) { }
        public override bool SetCustomProperties(Hashtable values, Hashtable expected = null, WebFlags flags = null)
        {
            Writes++;
            if (QueueWrites) { queued.Add((values, expected)); return true; }
            return Commit(values, expected);
        }
        public bool Deliver(int index) => Commit(queued[index].values, queued[index].expected);
        public void ClearQueued() => queued.Clear();
        public bool Commit(Hashtable values, Hashtable expected = null)
        {
            if (expected != null)
                foreach (var pair in expected)
                    if (!Equals(CustomProperties[pair.Key], pair.Value)) return false;
            foreach (var pair in values)
                if (pair.Value == null) CustomProperties.Remove(pair.Key); else CustomProperties[pair.Key] = pair.Value;
            RoomObjectRecovery.PropertiesChanged(values);
            return true;
        }
    }
    static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, Flags); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    static void Check(bool value, string text) { checks++; if (!value) throw new Exception(text); Debug.Log("PASS recovery: " + text); }
    static void Register(PhotonView view, int id, int owner, int controller)
    {
        view.ViewID = id; view.OwnerActorNr = owner; view.ControllerActorNr = controller;
        if (spawnedFixture) Token(view, "fixture-spawn-" + id);
    }
    static PhotonGrabbable Item(int id, int owner, int controller)
    {
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        Register(item.GetComponent<PhotonView>(), id, owner, controller);
        return item;
    }
    static BagObject Bag(int id, int owner, int controller, params string[] keys)
    {
        var go = new GameObject("recovery bag"); go.SetActive(false);
        var bag = go.AddComponent<BagObject>(); Set(bag, "rb", go.GetComponent<Rigidbody>());
        Register(bag.photonView, id, owner, controller);
        foreach (string key in keys)
        {
            var child = new GameObject("slot"); child.SetActive(false); child.transform.SetParent(go.transform);
            var slot = child.AddComponent<BagSlot>(); Set(slot, "itemKey", key);
            Get<List<BagSlot>>(bag, "slots").Add(slot);
        }
        return bag;
    }
    static void Switch(TestRoom room, Player player)
    {
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(PhotonNetwork.NetworkingClient, player);
        Set(room, "masterClientId", player.ActorNumber);
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        var client = PhotonNetwork.NetworkingClient;
        var savedRoom = client.CurrentRoom; var savedLocal = client.LocalPlayer;
        var savedState = client.State; var savedDebug = client.LoadBalancingPeer.DebugOut;
        var a = new Actor(1, true); var b = new Actor(2, true);
        var room = new TestRoom { LoadBalancingClient = client };
        room.AddPlayer(a); room.AddPlayer(b); client.CurrentRoom = room;
        client.State = ClientState.Joined; client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
        checks = 0;
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = PhotonSession.PlayScene;
            Switch(room, a);
            ItemCheckpoint(room, a, b);
            SceneBeforeJoin(room, b);
            BagCheckpoint(room, a, b);
            ChangedContents(room, a, b);
            EquipmentCheckpoint(room, a, b);
            AllSleeping(room, a, b);
            DetachedHands(room, a, b);
            DepartedWearer(room, a, b);
            PendingContentsOnStandby(room, a, b);
            RemoteHeldFloorCollision(room, a, b);
            DepartedEquipmentCleanup(room, a, b);
            UnfocusedBackpackRestore(room, a, b);
            CheckpointConflicts(room, a, b);
            QueuedConflicts(room, a, b);
            SpawnTokenTransport(room, a);
            ReusedSpawnIDs(room, a, b);
            BagContentsGeneration(room, a, b);
            SceneIDReservation();
            DelayedLifecycleMessages(room, a);
            // Repeat the previously passing behaviors with real spawned-generation headers as well.
            spawnedFixture = true;
            try
            {
                ItemCheckpoint(room, a, b);
                BagCheckpoint(room, a, b);
                ChangedContents(room, a, b);
                EquipmentCheckpoint(room, a, b);
                AllSleeping(room, a, b);
                DetachedHands(room, a, b);
                PendingContentsOnStandby(room, a, b);
                RemoteHeldFloorCollision(room, a, b);
                DepartedEquipmentCleanup(room, a, b);
                UnfocusedBackpackRestore(room, a, b);
            }
            finally { spawnedFixture = false; }
            IdleFlush(room);
            Debug.Log("RoomObjectRecoveryChecks ALL PASSED (" + checks + " assertions)");
        }
        finally
        {
            client.CurrentRoom = savedRoom;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, savedLocal);
            client.State = savedState; client.LoadBalancingPeer.DebugOut = savedDebug;
        }
        HeldItemRecoveryChecks.Run();
        EquipmentPersistenceChecks.Run();
        PhotonGrabbableChecks.Run();
        Debug.Log("RoomObjectRecoveryChecks REGRESSIONS PASSED");
    }
    static long Stamp(int revision, int writer) => ((long)revision << 32) | (uint)writer;

    static void Token(PhotonView view, string token)
    {
        typeof(PhotonView).GetProperty("SpawnToken").SetValue(view, token);
        view.isRuntimeInstantiated = true;
    }

    sealed class SpawnPool : IPunPrefabPool
    {
        public GameObject Instantiate(string id, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(id); go.SetActive(false); go.AddComponent<PhotonView>(); return go;
        }
        public void Destroy(GameObject go) => Object.DestroyImmediate(go);
    }

    static void SpawnTokenTransport(TestRoom room, Player a)
    {
        Switch(room, a);
        var previousPool = PhotonNetwork.PrefabPool;
        PhotonNetwork.PrefabPool = new SpawnPool();
        var network = typeof(PhotonNetwork);
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        var spawn = network.GetMethod("NetworkInstantiate", statics, null,
            new[] { typeof(InstantiateParameters), typeof(bool), typeof(bool), typeof(bool) }, null);
        var receive = network.GetMethod("NetworkInstantiate", statics, null, new[] { typeof(Hashtable), typeof(Player) }, null);
        try
        {
            var args = new InstantiateParameters("token test", Vector3.zero, Quaternion.identity, 0,
                new object[] { "equipment-restore-v1", 1 }, 0, null, a, 1234);
            var original = (GameObject)spawn.Invoke(null, new object[] { args, true, false, false });
            var view = original.GetComponent<PhotonView>();
            string token = view.SpawnToken; int id = view.ViewID;
            var sent = (Hashtable)network.GetField("SendInstantiateEvHashtable", statics).GetValue(null);
            var cached = new Hashtable(); foreach (var pair in sent) cached[pair.Key] = pair.Value;
            Check(!string.IsNullOrEmpty(token) && Equals(cached[(byte)10], token), "shared spawn generates and transmits one token");
            Object.DestroyImmediate(original);
            var lateJoin = (GameObject)receive.Invoke(null, new object[] { cached, a });
            Check(lateJoin.GetComponent<PhotonView>().SpawnToken == token && lateJoin.GetComponent<PhotonView>().ViewID == id,
                "cached spawn delivers same token to late joiner");
            Check(lateJoin.GetComponent<PhotonView>().InstantiationData.Length == 2,
                "spawn token leaves equipment/bag instantiation payload unchanged");
            Object.DestroyImmediate(lateJoin);
            network.GetField("lastUsedViewSubIdStatic", statics).SetValue(null, id - 1);
            var replacement = (GameObject)spawn.Invoke(null, new object[] { args, true, false, false });
            Check(replacement.GetComponent<PhotonView>().ViewID == id && replacement.GetComponent<PhotonView>().SpawnToken != token,
                "same prefab and reused ViewID receive a fresh spawn token");
            Object.DestroyImmediate(replacement);
        }
        finally { PhotonNetwork.PrefabPool = previousPool; }
    }

    static void ReusedSpawnIDs(TestRoom room, Player a, Player b)
    {
        foreach (string kind in new[] { "bag", "helmet", "goggles", "flashlight", "weapon", "gold" })
        {
            Switch(room, a); room.ClearQueued();
            const int id = 350;
            string oldToken = Guid.NewGuid().ToString("N"), newToken = Guid.NewGuid().ToString("N");
            Component old;
            if (kind == "bag")
            {
                var oldBag = Bag(id, 1, 1, "ItemHelmet", "ItemGoldBar", "", "WeaponNailBat");
                old = oldBag; Set(oldBag, "state", BagState.Stowed); Set(oldBag, "holderActor", 1);
            }
            else
            {
                var oldItem = Item(id, 1, 1); old = oldItem;
                if (kind == "helmet" || kind == "goggles")
                {
                    var oldHead = oldItem.gameObject.AddComponent<HeadEquipment>(); Call(oldHead, "Awake"); Set(oldItem, "headEquipment", oldHead);
                    Set(oldHead, "slot", kind == "helmet" ? HeadEquipment.EquipmentSlot.Head : HeadEquipment.EquipmentSlot.Eyes);
                    Set(oldHead, "equipped", true); Set(oldHead, "equippedActorNumber", 1);
                }
                else { Set(oldItem, "remoteHeld", true); Set(oldItem, "remoteHolderActorNumber", 1); }
            }
            Token(old.GetComponent<PhotonView>(), oldToken);
            Call(old, "RecoveryChanged"); RoomObjectRecovery.Flush();
            var oldRecord = Get<RoomObjectRecovery>(old, "recovery");
            object oldHeader = oldRecord.WireRevision;
            room.QueueWrites = true;
            Call(old, "RecoveryChanged"); RoomObjectRecovery.Flush(); // delayed previous lifetime write
            oldRecord.OnPreNetDestroy(old.GetComponent<PhotonView>()); // delayed previous lifetime deletion
            room.QueueWrites = false; Object.DestroyImmediate(old.gameObject);

            // The old wearer has left; new saved restore belongs to the joining player.
            Switch(room, b); room.Players.Remove(a.ActorNumber);
            GameObject go; PhotonView view; RoomObjectRecovery record;
            BagObject bag = null; PhotonGrabbable item = null; HeadEquipment head = null;
            if (kind == "bag")
            {
                bag = Bag(id, 2, 2, "ItemGoldBar", "", "WeaponRambroKnife", "ItemFlashlight");
                go = bag.gameObject; view = bag.photonView; Token(view, newToken);
                Set(bag, "state", BagState.Stowed); Set(bag, "holderActor", 2);
                Call(bag, "EnsureRecovery"); record = Get<RoomObjectRecovery>(bag, "recovery");
            }
            else
            {
                item = Item(id, 2, 2); go = item.gameObject; view = go.GetComponent<PhotonView>(); Token(view, newToken);
                if (kind == "helmet" || kind == "goggles")
                {
                    head = go.AddComponent<HeadEquipment>(); Call(head, "Awake"); Set(item, "headEquipment", head);
                    Set(head, "slot", kind == "helmet" ? HeadEquipment.EquipmentSlot.Head : HeadEquipment.EquipmentSlot.Eyes);
                    Call(head, "ApplyRecoveryWearer", 2);
                }
                Call(item, "EnsureRecovery"); record = Get<RoomObjectRecovery>(item, "recovery");
            }
            Check(view.OwnerActorNr == 2 && record.Revision == 0, kind + " ignores retained record from reused ID");
            record.Changed(); RoomObjectRecovery.Flush();
            string key = Get<string>(record, "key"); object current = room.CustomProperties[key];
            Check(!record.Accept(oldHeader) && record.Accept(record.WireRevision), kind + " rejects previous spawn snapshot at equal revision");
            room.Deliver(0); room.Deliver(1);
            Check(ReferenceEquals(room.CustomProperties[key], current) && view.OwnerActorNr == 2,
                kind + " survives delayed old checkpoint and deletion");
            // Receive from the valid current controller, so authority checks cannot mask a missing token guard.
            Switch(room, a);
            if (bag != null)
            {
                var oldSnapshot = new PhotonStream(false, new object[] { oldHeader, (byte)BagState.Dropped, 0, 99,
                    Vector3.one * 90, Quaternion.identity, 0d, false });
                bag.OnPhotonSerializeView(oldSnapshot, new PhotonMessageInfo(b, 0, view));
                Check(bag.State == BagState.Stowed && bag.HolderActor == 2 && string.Join("|", bag.Keys()) == "ItemGoldBar||WeaponRambroKnife|ItemFlashlight",
                    "replacement bag preserves exact contents, empty slot and wearer");
            }
            else
            {
                var pose = item.GetComponent<PhotonTransformView>(); pose.enabled = true;
                var stream = new PhotonStream(false, new object[] { oldHeader, false, 0, false, 99, false, 0d, false, false,
                    Vector3.one * 90, Vector3.zero, Quaternion.identity });
                Vector3 position = go.transform.position;
                item.OnPhotonSerializeView(stream, new PhotonMessageInfo(b, 0, view));
                pose.OnPhotonSerializeView(stream, new PhotonMessageInfo(b, 0, view));
                Check(go.transform.position == position && item.HoldVersion == 0, kind + " ignores old spawn state and transform packet");
                if (head != null)
                {
                    head.OnPhotonSerializeView(new PhotonStream(false, new object[] { true, 1 }), new PhotonMessageInfo(b, 0, view));
                    Check(head.IsEquipped && !Get<bool>(head, "destroyAfterOwnershipFallback") && Get<int>(head, "equippedActorNumber") == 2,
                        kind + " rejects stale wearer and is not queued for departed wearer cleanup");
                }
            }
            Switch(room, b);
            object header = record.WireRevision;
            for (int i = 0; i < 100; i++) record.Accept(header);
            long bytes = GC.GetAllocatedBytesForCurrentThread(); int writes = room.Writes;
            for (int i = 0; i < 10000; i++) { record.Accept(record.WireRevision); RoomObjectRecovery.Flush(); }
            Check(bytes == GC.GetAllocatedBytesForCurrentThread() && writes == room.Writes,
                kind + " 10,000 generation checks/idle flushes allocate zero and send no writes");
            record.OnPreNetDestroy(view); Object.DestroyImmediate(go); room.AddPlayer(a);
        }
        room.ClearQueued();
        // Scene pickup and spawned gear with the same numeric ID must never share checkpoint state.
        Switch(room, a);
        var scene = Item(351, 1, 1); scene.transform.position = Vector3.one * 3;
        Call(scene, "RecoveryChanged"); RoomObjectRecovery.Flush(); Call(scene, "OnDisable"); Object.DestroyImmediate(scene.gameObject);
        var runtime = Item(351, 2, 2); Token(runtime.GetComponent<PhotonView>(), Guid.NewGuid().ToString("N"));
        Call(runtime, "EnsureRecovery");
        Check(runtime.transform.position != Vector3.one * 3 && runtime.GetComponent<PhotonView>().OwnerActorNr == 2,
            "scene and runtime checkpoint namespaces cannot overwrite each other");
        Call(runtime, "OnDisable"); Object.DestroyImmediate(runtime.gameObject);
    }

    static void SceneIDReservation()
    {
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        var network = typeof(PhotonNetwork);
        var cursor = network.GetField("lastUsedViewSubIdStatic", statics); object previous = cursor.GetValue(null);
        var reserved = (HashSet<int>)network.GetField("reservedSceneViewIds", statics).GetValue(null);
        bool existed = reserved.Contains(360);
        var go = new GameObject("scene pickup"); go.SetActive(false); var view = go.AddComponent<PhotonView>();
        Set(view, "sceneViewId", 360); PhotonNetwork.RegisterPhotonView(view); Object.DestroyImmediate(go);
        cursor.SetValue(null, 359);
        Check(PhotonNetwork.AllocateViewID(0) != 360, "destroyed scene pickup ID remains reserved for joining clients");
        cursor.SetValue(null, previous); if (!existed) reserved.Remove(360);
    }

    static void BagContentsGeneration(TestRoom room, Player a, Player b)
    {
        Switch(room, b);
        var bag = Bag(352, 1, 1, "ItemGoldBar"); Token(bag.photonView, "new-bag-token");
        Call(bag, "RpcContents", new[] { "ItemFlashlight" }, false, "old-bag-token", new PhotonMessageInfo(a, 0, bag.photonView));
        Check(bag.Keys()[0] == "ItemGoldBar", "old spawn contents RPC cannot overwrite new bag before Start");
        Call(bag, "RpcContents", new[] { "" }, false, "new-bag-token", new PhotonMessageInfo(a, 0, bag.photonView));
        Check(bag.Keys()[0] == "", "matching initial contents still arrive before Start/ownership delivery");
        Object.DestroyImmediate(bag.gameObject);
    }

    static void DelayedLifecycleMessages(TestRoom room, Player a)
    {
        Switch(room, a);
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        var network = typeof(PhotonNetwork);
        var receive = network.GetMethod("OnEvent", statics);
        var cleanup = network.GetMethod("ServerCleanInstantiateAndDestroy", statics);
        var previousPool = PhotonNetwork.PrefabPool;
        PhotonNetwork.PrefabPool = new CleanupPool();
        try
        {
            foreach (string kind in new[] { "item", "loaded bag", "scene item" })
            {
                BagObject bag = null; PhotonGrabbable item = null; PhotonView view; GameObject go;
                if (kind == "loaded bag")
                {
                    bag = Bag(370, 1, 1, "ItemGoldBar", "WeaponRambroKnife"); go = bag.gameObject; view = bag.photonView;
                    Set(bag, "state", BagState.Held); Set(bag, "holderActor", 1); Set(bag, "holdVersion", 1);
                }
                else
                {
                    item = Item(370, 1, 1); go = item.gameObject; view = go.GetComponent<PhotonView>();
                    Set(item, "remoteHeld", true); Set(item, "remoteHolderActorNumber", 1); Set(item, "holdVersion", 1);
                }
                if (kind != "scene item") { Token(view, Guid.NewGuid().ToString("N")); view.InstantiationId = view.ViewID; }
                // Register outside Play Mode for the real event handlers' PhotonView.Find lookup.
                object views = network.GetField("photonViewList", statics).GetValue(null);
                views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(views, new object[] { view.ViewID, view });
                string token = view.SpawnToken;
                var drop = new EventData { Code = PhotonHeldItems.DropEvent };
                drop.Parameters[ParameterCode.ActorNr] = a.ActorNumber;
                drop.Parameters[ParameterCode.Data] = new object[] { view.ViewID, 1, 99, Vector3.one * 90, Quaternion.identity, "older-spawn" };
                PhotonHeldItems.OnEvent(drop);
                Check((bag != null ? bag.State == BagState.Held && bag.HoldVersion == 1 : item.IsHeldByAnyPlayer && item.HoldVersion == 1),
                    kind + " rejects delayed reliable drop from previous spawn");
                drop = new EventData { Code = PhotonHeldItems.DropEvent };
                drop.Parameters[ParameterCode.ActorNr] = a.ActorNumber;
                drop.Parameters[ParameterCode.Data] = new object[] { view.ViewID, 1, 1, Vector3.up * 4, Quaternion.identity, token };
                PhotonHeldItems.OnEvent(drop);
                Check((bag != null ? bag.State == BagState.Dropped : !item.IsHeldByAnyPlayer) && go.transform.position == Vector3.up * 4,
                    kind + " accepts matching reliable standby drop");
                if (bag != null) Check(string.Join("|", bag.Keys()) == "ItemGoldBar|WeaponRambroKnife", "token-protected drop preserves loaded bag slots");

                cleanup.Invoke(null, new object[] { view });
                var sent = (Hashtable)network.GetField("ServerCleanDestroyEvent", statics).GetValue(null);
                Check(Equals(sent[(byte)10], token), kind + " destroy message carries current spawn identity");
                if (token != null)
                {
                    var filter = (Hashtable)network.GetField("removeFilter", statics).GetValue(null);
                    Check(Equals(filter[(byte)10], token) && Equals(filter[(byte)7], view.ViewID),
                        "cached-spawn cleanup matches token and ID, preserving replacement cache");
                }
                var destroy = new EventData { Code = 204 }; // PUN Destroy
                destroy.Parameters[ParameterCode.ActorNr] = a.ActorNumber;
                destroy.Parameters[ParameterCode.Data] = new Hashtable { [(byte)0] = view.ViewID, [(byte)10] = "older-spawn" };
                receive.Invoke(null, new object[] { destroy });
                Check(go != null, kind + " survives delayed network destruction for older spawn");
                destroy = new EventData { Code = 204 };
                destroy.Parameters[ParameterCode.ActorNr] = a.ActorNumber;
                destroy.Parameters[ParameterCode.Data] = new Hashtable { [(byte)0] = view.ViewID, [(byte)10] = token };
                receive.Invoke(null, new object[] { destroy });
                Check(go == null, kind + " still destroys on matching network cleanup");
            }
        }
        finally { PhotonNetwork.PrefabPool = previousPool; }
    }

    static void RemoteHeldFloorCollision(TestRoom room, Player a, Player b)
    {
        var previousSimulation = Physics.simulationMode;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(20, 1, 20);
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            foreach (int id in new[] { 300, 301, 302 }) // shared flashlight/weapon/gold path
            {
                Switch(room, a);
                var source = Item(id, 1, 1);
                Set(source, "remoteHeld", true); Set(source, "remoteHolderActorNumber", 1); Set(source, "holdVersion", 1);
                Call(source, "RecoveryChanged"); RoomObjectRecovery.Flush();
                Call(source, "OnDisable"); Object.DestroyImmediate(source.gameObject);
                Switch(room, b);
                var copy = Item(id, 1, 1); Call(copy, "EnsureRecovery");
                var body = copy.GetComponent<Rigidbody>();
                Check(copy.IsHeldByAnyPlayer && !body.detectCollisions && copy.Holder == null,
                    "remote retained hold starts without collisions or a local hand");
                Check(copy.ApplyUnavailableDrop(1, 1, Vector3.up * 4, Quaternion.identity), "other master accepts client-held release");
                RoomObjectRecovery.Flush(); // includes the optimistic writer's own acknowledgement
                Check(body.detectCollisions && !body.isKinematic && body.useGravity, "authoritative release restores collision before gravity");
                Physics.SyncTransforms();
                for (int step = 0; step < 150; step++) Physics.Simulate(0.02f);
                Check(body.position.y > 0 && body.position.y < 1.2f, "released item lands on floor instead of falling through");
                var hand = new GameObject("pickup after recovery").AddComponent<PlayerHand>();
                Check(hand.ForceGrab(copy), "released item remains grabbable by the awake master");
                Call(copy, "OnDisable"); Object.DestroyImmediate(copy.gameObject); Object.DestroyImmediate(hand.gameObject);
            }
        }
        finally { Physics.simulationMode = previousSimulation; Object.DestroyImmediate(floor); }
    }

    static void DepartedEquipmentCleanup(TestRoom room, Player a, Player b)
    {
        var savedPool = PhotonNetwork.PrefabPool;
        var pool = new CleanupPool(); PhotonNetwork.PrefabPool = pool;
        Switch(room, b);
        try
        {
            foreach (var slot in new[] { HeadEquipment.EquipmentSlot.Head, HeadEquipment.EquipmentSlot.Eyes })
            {
                var item = Item(310 + (int)slot, 1, 1);
                var head = item.gameObject.AddComponent<HeadEquipment>(); Call(head, "Awake");
                Set(head, "slot", slot); Set(item, "headEquipment", head);
                Call(head, "ApplyRecoveryWearer", a.ActorNumber);
                var body = item.GetComponent<Rigidbody>();
                Check(body.isKinematic && !body.useGravity && !body.detectCollisions,
                    "worn " + slot + " stays frozen before its anchor arrives");
                var anchor = new GameObject("departed head anchor"); Set(head, "equippedAnchor", anchor.transform);
                Object.DestroyImmediate(anchor);
                room.Players.Remove(a.ActorNumber);
                head.OnPlayerLeftRoom(a);
                Call(head, "LateUpdate");
                Check(item != null && Get<bool>(head, "destroyAfterOwnershipFallback"), "cleanup waits for authority without consuming its request");
                var waitForAuthority = (Action)Delegate.CreateDelegate(typeof(Action), head, typeof(HeadEquipment).GetMethod("LateUpdate", Flags));
                for (int i = 0; i < 100; i++) waitForAuthority();
                int writes = room.Writes;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) waitForAuthority();
                Check(GC.GetAllocatedBytesForCurrentThread() == before && room.Writes == writes,
                    "waiting " + slot + " cleanup allocates zero bytes and sends no checkpoints");
                var hand = new GameObject("stale gear pickup").AddComponent<PlayerHand>();
                Check(!hand.ForceGrab(item) && !item.PickupPending, "stale " + slot + " cannot be grabbed while cleanup waits");
                item.GetComponent<PhotonView>().ControllerActorNr = b.ActorNumber;
                Call(head, "LateUpdate");
                Check(item == null && pool.Destroyed == 1 + (int)slot, "departed " + slot + " is destroyed after authority arrives without diagnostic exception");
                Object.DestroyImmediate(hand.gameObject);

                // Fresh late-joining copy has no destroyed Transform reference, but the wearer is absent.
                item = Item(312 + (int)slot, 1, 1);
                head = item.gameObject.AddComponent<HeadEquipment>(); Call(head, "Awake");
                Set(item, "headEquipment", head); Set(head, "slot", slot);
                Call(head, "ApplyRecoveryWearer", a.ActorNumber);
                Check(Get<bool>(head, "destroyAfterOwnershipFallback") && item.GetComponent<Rigidbody>().isKinematic,
                    "late join identifies and freezes departed " + slot);
                // A newer valid held checkpoint must still supersede obsolete worn cleanup.
                Call(head, "CancelHeldRestore");
                Check(!Get<bool>(head, "destroyAfterOwnershipFallback") && !head.IsEquipped, "new held state cancels obsolete equipment cleanup");
                Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);
                room.AddPlayer(a);
            }
        }
        finally { PhotonNetwork.PrefabPool = savedPool; if (room.GetPlayer(a.ActorNumber) == null) room.AddPlayer(a); }
    }

    static void UnfocusedBackpackRestore(TestRoom room, Player a, Player b)
    {
        Switch(room, a); Set(room, "masterClientId", b.ActorNumber);
        var host = new GameObject("unfocused restore"); host.SetActive(false);
        var handoff = host.AddComponent<PhotonMasterHandoff>(); Call(handoff, "Awake"); Set(handoff, "focused", false);
        var inventoryObject = new GameObject("restore inventory"); inventoryObject.SetActive(false);
        var inventory = inventoryObject.AddComponent<BagInventory>(); Set(inventory, "stowAnchor", inventoryObject.transform);
        var localInventory = typeof(BagInventory).GetProperty("Local"); var savedInventory = localInventory.GetValue(null);
        try
        {
            foreach (int scenario in new[] { 0, 1, 2, 3, 4 })
            {
                Set(handoff, "focused", false); Set(inventory, "backBag", null);
                localInventory.SetValue(null, scenario == 1 ? null : inventory);
                var bag = Bag(320 + scenario, 1, 1, "ItemGoldBar", "ItemFlashlight", "WeaponNailBat");
                Set(bag, "awaitingClaim", true); Set(bag, "requester", 1);
                Set(bag, "requestLife", BagInventory.Life + (scenario == 3 ? 1 : 0));
                Set(bag, "requestHandEpoch", BagInventory.HandClaimEpoch);
                Set(bag, "requestedTarget", scenario == 2 ? BagObject.Target.LeftHand : BagObject.Target.Back);
                Set(bag, "claimCancelled", scenario == 4);
                Set(bag, "spawnTime", Time.unscaledTime); Call(bag, "ApplyPhysics");
                Call(bag, "UpdateOwned");
                if (scenario == 1)
                {
                    Check(bag.AwaitingClaim && bag.GetComponent<Rigidbody>().isKinematic, "unfocused back restore waits safely for inventory");
                    localInventory.SetValue(null, inventory); Call(bag, "UpdateOwned");
                }
                bool restored = scenario < 2;
                Check(!bag.AwaitingClaim && (restored ? bag.State == BagState.Stowed && inventory.BackBag == bag : bag.State == BagState.Dropped),
                    "unfocused restore preserves Back, cancels hand/stale-life/explicitly cancelled claims: " + scenario);
                Check(string.Join("|", bag.Keys()) == "ItemGoldBar|ItemFlashlight|WeaponNailBat", "restore or cancellation preserves every bag slot");
                Set(handoff, "focused", true); Call(bag, "UpdateOwned");
                Check((bag.State == BagState.Stowed) == restored, "focus return does not resurrect cancelled delivery or drop restored backpack");
                Get<RoomObjectRecovery>(bag, "recovery")?.Dispose(); Object.DestroyImmediate(bag.gameObject);
            }
        }
        finally
        {
            localInventory.SetValue(null, savedInventory);
            Object.DestroyImmediate(inventoryObject); Object.DestroyImmediate(host);
        }
    }

    static void IdleFlush(TestRoom room)
    {
        RoomObjectRecovery.Flush();
        int writes = room.Writes;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) RoomObjectRecovery.Flush();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0 && room.Writes == writes, "10,000 idle checkpoint flushes allocate zero bytes and send no updates");
    }
    static void ItemCheckpoint(TestRoom room, Player a, Player b)
    {
        // Both flashlight and weapons share this path; no prefab-name allowlist.
        foreach (int id in new[] { 5, 6, 7 })
        {
            Switch(room, a);
            var source = Item(id, 1, 1);
            Set(source, "holdVersion", 1); Set(source, "remoteHeld", true); Set(source, "remoteHolderActorNumber", 1);
            Vector3 drop = new Vector3(33, 4, id);
            Check(source.ApplyUnavailableDrop(1, 1, drop, Quaternion.identity), "standby releases item " + id);
            RoomObjectRecovery.Flush();
            int writes = room.Writes; RoomObjectRecovery.Flush();
            Check(room.Writes == writes, "idle flush emits no network writes");
            string identity = Get<RoomObjectRecovery>(source, "recovery").Identity;
            Call(source, "OnDisable"); Object.DestroyImmediate(source.gameObject);
            Switch(room, b);
            var copy = Item(id, 0, 2); copy.transform.position = Vector3.zero;
            Call(copy, "EnsureRecovery");
            Check(copy.transform.position == drop && !copy.IsHeldByAnyPlayer && Get<bool>(copy, "hasBeenPickedUp"), "late join restores dropped pose instead of bus pose " + id);
            Check(Get<RoomObjectRecovery>(copy, "recovery").Identity == identity, "identity survives joining controller");
            var view = copy.GetComponent<PhotonView>(); view.OwnerActorNr = 1; view.ControllerActorNr = 1;
            var pose = copy.GetComponent<PhotonTransformView>(); pose.enabled = true;
            var stream = new PhotonStream(false, new object[] { 0, false, 0, false, 0, false, 0d, false, false,
                Vector3.one * 90, Vector3.zero, Quaternion.identity });
            copy.OnPhotonSerializeView(stream, new PhotonMessageInfo(a, 0, view));
            pose.OnPhotonSerializeView(stream, new PhotonMessageInfo(a, 0, view));
            Check(copy.HoldVersion == 1 && copy.transform.position == drop, "old initial packet cannot reset state OR transform " + id);
            Call(copy, "OnDisable"); Object.DestroyImmediate(copy.gameObject);
        }
    }
    static void BagCheckpoint(TestRoom room, Player a, Player b)
    {
        foreach (bool worn in new[] { false, true })
        {
            Switch(room, a);
            int id = worn ? 92 : 91;
            var bag = Bag(id, 1, 1, "ItemHelmet", "ItemGoldBar", "WeaponRambroKnife");
            Set(bag, "state", worn ? BagState.Stowed : BagState.Held); Set(bag, "holderActor", 1); Set(bag, "holdVersion", 7);
            if (worn) { bag.photonView.OwnerActorNr = 0; bag.photonView.ControllerActorNr = 1; }
            bag.transform.position = Vector3.one * 4;
            Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
            var record = Get<RoomObjectRecovery>(bag, "recovery"); string identity = record.Identity;
            Check(RoomObjectRecovery.Exists(room.Name, identity), "same-room restore detects existing logical bag");
            Check(!RoomObjectRecovery.Exists("other-room", identity), "new-room persistence remains allowed");
            record.Dispose(); Object.DestroyImmediate(bag.gameObject);
            Switch(room, b);
            var copy = Bag(id, 0, 2, "ItemHelmet", "ItemGoldBar", "WeaponRambroKnife");
            Set(copy, "awaitingClaim", true); Set(copy, "requester", 1);
            Call(copy, "EnsureRecovery");
            Check(copy.State == (worn ? BagState.Stowed : BagState.Held) && copy.HolderActor == 1 && !copy.AwaitingClaim,
                "late join replaces cached pending bag claim with latest disposition");
            if (worn) Check(copy.photonView.OwnerActorNr == 1, "worn checkpoint retains wearer even before transfer acknowledgement");
            Check(string.Join("|", copy.Keys()) == "ItemHelmet|ItemGoldBar|WeaponRambroKnife", "contents retained with disposition");
            copy.photonView.ControllerActorNr = 2;
            bool dropped = copy.ApplyUnavailableDrop(1, 7, Vector3.one * 8, Quaternion.identity);
            Check(dropped != worn, "recovery drops hands and preserves worn bags");
            if (worn)
            {
                Switch(room, a); copy.photonView.ControllerActorNr = 1;
                var inventoryGo = new GameObject("inventory"); inventoryGo.SetActive(false);
                var inventory = inventoryGo.AddComponent<BagInventory>();
                var anchor = new GameObject("back anchor"); Set(inventory, "stowAnchor", anchor.transform);
                Call(inventory, "ReattachRecoveredBack", copy);
                Check(inventory.BackBag == copy && Get<Transform>(copy, "localBackAnchor") == anchor.transform,
                    "visual worn recovery restores inventory grab reference and anchor");
                copy.photonView.ControllerActorNr = 2; Call(inventory, "ForgetLostBags");
                Check(inventory.BackBag == copy, "temporary controller loss does not forget valid local backpack");
                Object.DestroyImmediate(inventoryGo); Object.DestroyImmediate(anchor);
            }
            Get<RoomObjectRecovery>(copy, "recovery").Dispose(); Object.DestroyImmediate(copy.gameObject);
        }
    }

    static void SceneBeforeJoin(TestRoom room, Player b)
    {
        Switch(room, b);
        PhotonNetwork.NetworkingClient.CurrentRoom = null;
        var item = Item(5, 0, 2); Call(item, "EnsureRecovery");
        PhotonNetwork.NetworkingClient.CurrentRoom = room;
        RoomObjectRecovery.JoinedRoom();
        Check(item.transform.position == new Vector3(33, 4, 5) && item.HoldVersion == 1,
            "desktop scene loaded before joining receives retained room state");
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);
    }
    static void EquipmentCheckpoint(TestRoom room, Player a, Player b)
    {
        foreach (var slot in new[] { HeadEquipment.EquipmentSlot.Head, HeadEquipment.EquipmentSlot.Eyes })
        {
            Switch(room, a);
            int id = 100 + (int)slot;
            var item = Item(id, 1, 1); var head = item.gameObject.AddComponent<HeadEquipment>(); Call(head, "Awake");
            Set(head, "slot", slot); Set(item, "headEquipment", head);
            Set(head, "equipped", true); Set(head, "equippedActorNumber", 1);
            Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush();
            Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);
            Switch(room, b);
            var copy = Item(id, 0, 2); var copyHead = copy.gameObject.AddComponent<HeadEquipment>(); Call(copyHead, "Awake");
            Set(copyHead, "slot", slot); Set(copy, "headEquipment", copyHead);
            Call(copy, "EnsureRecovery");
            Check(copyHead.IsEquipped && !copyHead.IsLocallyEquipped, "late join retains worn " + slot);
            Check(!copy.ApplyUnavailableDrop(1, 0, Vector3.one * 80, Quaternion.identity), "standby cannot drop worn " + slot);
            Call(copy, "OnDisable"); Object.DestroyImmediate(copy.gameObject);
        }
    }

    static void ChangedContents(TestRoom room, Player a, Player b)
    {
        Switch(room, a);
        var bag = Bag(95, 1, 1, "ItemGoldBar", "ItemHelmet");
        Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
        Check(bag.Take(bag.Slots[0]) == "ItemGoldBar", "normal take removes gold from bag");
        RoomObjectRecovery.Flush();
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
        Switch(room, b);
        var copy = Bag(95, 0, 2, "ItemGoldBar", "ItemHelmet");
        Call(copy, "EnsureRecovery");
        Check(string.Join("|", copy.Keys()) == "|ItemHelmet", "late join replaces spawn contents with latest slot contents");
        Call(copy, "RpcContents", new[] { "ItemGoldBar", "ItemHelmet" }, false, copy.photonView.SpawnToken,
            new PhotonMessageInfo(a, 0, copy.photonView));
        Check(string.Join("|", copy.Keys()) == "|ItemHelmet", "delayed contents-only RPC cannot duplicate removed item");
        Get<RoomObjectRecovery>(copy, "recovery").Dispose(); Object.DestroyImmediate(copy.gameObject);
    }
    static void CheckpointConflicts(TestRoom room, Player a, Player b)
    {
        Switch(room, a);
        var obj = Item(200, 1, 1); Call(obj, "RecoveryChanged"); RoomObjectRecovery.Flush();
        var record = Get<RoomObjectRecovery>(obj, "recovery");
        object[] previous = (object[])room.CustomProperties["recovery:s:200"];
        string identity = record.Identity;
        var winner = new object[] { identity, 2, 2, 2, previous[4], 0 };
        room.SetCustomProperties(new Hashtable { ["recovery:s:200"] = winner, ["recovery:s:200:v"] = Stamp(2, 2) }, new Hashtable { ["recovery:s:200:v"] = Stamp(1, 1) });
        Check(obj.GetComponent<PhotonView>().OwnerActorNr == 2 && record.Revision == 2, "new owner checkpoint reconciles ownership");
        Check(!room.SetCustomProperties(new Hashtable { ["recovery:s:200"] = previous, ["recovery:s:200:v"] = Stamp(2, 1) }, new Hashtable { ["recovery:s:200:v"] = Stamp(1, 1) }),
            "server compare-and-swap rejects delayed previous-controller overwrite");
        Check(ReferenceEquals(room.CustomProperties["recovery:s:200"], winner), "CAS conflict preserves winner state");
        obj.transform.position = Vector3.one * 12;
        RoomObjectRecovery.OperationResponse(new OperationResponse { OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation });
        Check(obj.transform.position == Vector3.one * 12, "unrelated property failure does not replay acknowledged checkpoints");
        typeof(RoomObjectRecovery).GetProperty("Revision").SetValue(record, 3);
        RoomObjectRecovery.OperationResponse(new OperationResponse { OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation });
        Check(record.Revision == 2, "failed optimistic write adopts retained winner revision");
        Switch(room, b); obj.GetComponent<PhotonView>().ControllerActorNr = 2;
        record.OnPreNetDestroy(obj.GetComponent<PhotonView>());
        Check(!RoomObjectRecovery.Exists(room.Name, identity), "permanent destruction removes retained record");
        Object.DestroyImmediate(obj.gameObject);
    }

    static void QueuedConflicts(TestRoom room, Player a, Player b)
    {
        Switch(room, a);
        var item = Item(205, 1, 1); Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush();
        var initial = (object[])room.CustomProperties["recovery:s:205"];
        room.QueueWrites = true;
        Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush(); // A queues 2, without acknowledgement.
        Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush(); // A queues 3, based on its own 2.
        room.QueueWrites = false;
        var winner = new object[] { initial[0], 2, 2, 2, initial[4], 0 };
        Check(room.Commit(new Hashtable { ["recovery:s:205"] = winner, ["recovery:s:205:v"] = Stamp(2, 2) },
            new Hashtable { ["recovery:s:205:v"] = Stamp(1, 1) }), "competing owner wins while previous writer has queued updates");
        Check(!room.Deliver(0), "previous writer's first queued update loses conflict");
        Check(!room.Deliver(1), "previous writer's second queued update cannot overwrite equal revision from another writer");
        Check(ReferenceEquals(room.CustomProperties["recovery:s:205"], winner), "queued conflict preserves winning owner's checkpoint");
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);
    }

    static void DetachedHands(TestRoom room, Player a, Player b)
    {
        // The original master wakes first, with retained holds but no surviving local hand attachment.
        Switch(room, a);
        var item = Item(210, 1, 1);
        Set(item, "remoteHeld", true); Set(item, "remoteHolderActorNumber", 1); Set(item, "holdVersion", 4);
        Call(item, "RecoveryChanged"); RoomObjectRecovery.Flush();
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);
        item = Item(210, 1, 1); Call(item, "EnsureRecovery");
        Check(item.IsHeldByAnyPlayer && !item.IsHeld, "wake reconstructs a retained hold without a real local hand");
        Call(item, "RecoverDetachedHand"); RoomObjectRecovery.Flush();
        Check(!item.IsHeldByAnyPlayer && item.GetComponent<PhotonView>().OwnerActorNr == 0,
            "first waking original master releases a detached local item hold");
        Check(!(bool)((object[])((object[])room.CustomProperties[Get<string>(Get<RoomObjectRecovery>(item, "recovery"), "key")])[4])[0], "corrected item release is retained for later wakers");
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);

        item = Item(212, 1, 1);
        Set(item, "remoteHeld", true); Set(item, "remoteHolderActorNumber", 1); Set(item, "holdVersion", 4);
        Call(item, "TrackHeld");
        PhotonHeldItems.DropLocalHands(); RoomObjectRecovery.Flush();
        Check(!item.IsHeldByAnyPlayer, "pause callback immediately releases retained local hold without waiting for LateUpdate");
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);

        item = Item(213, 2, 2);
        Set(item, "remoteHeld", true); Set(item, "remoteHolderActorNumber", 1); Set(item, "holdVersion", 4);
        Set(item, "recoverDetachedHand", true); Call(item, "TrackHeld");
        PhotonHeldItems.DropLocalHands(); Call(item, "RecoverDetachedHand");
        Check(item.GetComponent<PhotonView>().OwnerActorNr == 2 && item.IsHeldByAnyPlayer,
            "wake cleanup does not revoke a newer player's grant while their pose is in flight");
        Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject);

        var bag = Bag(211, 1, 1, "ItemGoldBar", "WeaponRambroKnife");
        Set(bag, "state", BagState.Held); Set(bag, "holderActor", 1); Set(bag, "holdVersion", 4);
        Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
        bag = Bag(211, 1, 1, "ItemGoldBar", "WeaponRambroKnife"); Call(bag, "EnsureRecovery");
        Call(bag, "RecoverDetachedHand"); RoomObjectRecovery.Flush();
        Check(bag.State == BagState.Dropped && string.Join("|", bag.Keys()) == "ItemGoldBar|WeaponRambroKnife",
            "first waking original master releases detached bag without losing slots");
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
        Switch(room, b);
        bag = Bag(211, 0, 2, "", ""); Call(bag, "EnsureRecovery");
        Check(bag.State == BagState.Dropped && string.Join("|", bag.Keys()) == "ItemGoldBar|WeaponRambroKnife",
            "later waker receives corrected bag release and preserved contents");
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
    }

    static void DepartedWearer(TestRoom room, Player a, Player b)
    {
        Switch(room, b);
        string bagIdentity = (string)((object[])room.CustomProperties["recovery:s:92"])[0];
        string helmetIdentity = (string)((object[])room.CustomProperties["recovery:s:100"])[0];
        string gogglesIdentity = (string)((object[])room.CustomProperties["recovery:s:101"])[0];
        string droppedIdentity = (string)((object[])room.CustomProperties["recovery:s:211"])[0];
        room.Players.Remove(a.ActorNumber);
        try
        {
            Check(!RoomObjectRecovery.Exists(room.Name, bagIdentity), "departed wearer's pending bag cleanup cannot block saved backpack restore");
            Check(!RoomObjectRecovery.Exists(room.Name, helmetIdentity), "departed wearer's pending helmet cleanup cannot block saved helmet restore");
            Check(!RoomObjectRecovery.Exists(room.Name, gogglesIdentity), "departed wearer's pending goggles cleanup cannot block saved goggles restore");
            Check(RoomObjectRecovery.Exists(room.Name, droppedIdentity), "departed holder's surviving dropped bag still prevents a duplicate restore");
        }
        finally { room.AddPlayer(a); }
        Check(RoomObjectRecovery.Exists(room.Name, bagIdentity), "sleeping connected wearer's bag still prevents a duplicate restore");
    }

    static void PendingContentsOnStandby(TestRoom room, Player a, Player b)
    {
        Switch(room, a);
        Set(room, "masterClientId", b.ActorNumber); // A is a client; its drop hands control to B.
        var bag = Bag(215, 1, 1, "ItemGoldBar", "ItemHelmet");
        Set(bag, "state", BagState.Held); Set(bag, "holderActor", 1); Set(bag, "holdVersion", 5);
        Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
        bag.Take(bag.Slots[0]); // Dirty contents, before the regular end-of-frame checkpoint.
        BagObject.Active.Add(bag);
        var host = new GameObject("standby ordering"); host.SetActive(false);
        var handoff = host.AddComponent<PhotonMasterHandoff>();
        Call(handoff, "AvailabilityChanged", false);
        Check(bag.State == BagState.Dropped && !bag.CanControl, "client standby hands dropped bag control to the other master");
        var state = (object[])((object[])room.CustomProperties[Get<string>(Get<RoomObjectRecovery>(bag, "recovery"), "key")])[4];
        Check(string.Join("|", (string[])state[10]) == "|ItemHelmet",
            "pending contents are retained before standby releases the client's ownership");
        BagObject.Active.Remove(bag);
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject); Object.DestroyImmediate(host);
        Switch(room, b);
        bag = Bag(215, 0, 2, "ItemGoldBar", "ItemHelmet"); Call(bag, "EnsureRecovery");
        Check(bag.ApplyUnavailableDrop(1, 5, Vector3.one, Quaternion.identity), "master can recover the retained client hold after standby");
        RoomObjectRecovery.Flush();
        Check(string.Join("|", bag.Keys()) == "|ItemHelmet", "standby recovery does not resurrect the just-removed gold bar");
        Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
    }

    static void AllSleeping(TestRoom room, Player a, Player b)
    {
        foreach (var first in new[] { a, b })
        {
            int id = first.ActorNumber * 1000;
            foreach (var holder in new[] { a, b })
            {
                Switch(room, holder);
                var bag = Bag(id + holder.ActorNumber, holder.ActorNumber, holder.ActorNumber, "ItemGoldBar", "ItemHelmet");
                Set(bag, "state", BagState.Held); Set(bag, "holderActor", holder.ActorNumber); Set(bag, "holdVersion", 8);
                Call(bag, "RecoveryChanged"); RoomObjectRecovery.Flush();
                Check(bag.ApplyUnavailableDrop(holder.ActorNumber, 8, Vector3.one * holder.ActorNumber, Quaternion.identity),
                    "all asleep: each holder releases its loaded bag");
                RoomObjectRecovery.Flush();
                Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
            }
            Switch(room, first);
            foreach (var holder in new[] { a, b })
            {
                var bag = Bag(id + holder.ActorNumber, 0, first.ActorNumber, "ItemGoldBar", "ItemHelmet");
                Call(bag, "EnsureRecovery");
                Check(bag.CanInteract && bag.State == BagState.Dropped && bag.transform.position == Vector3.one * holder.ActorNumber,
                    "first waker " + first.ActorNumber + " restores released bags under current master");
                Check(string.Join("|", bag.Keys()) == "ItemGoldBar|ItemHelmet", "all-asleep contents survive either wake order");
                Check(!bag.ApplyUnavailableDrop(holder.ActorNumber, 8, Vector3.one * 99, Quaternion.identity),
                    "later wake cannot replay the old drop");
                Get<RoomObjectRecovery>(bag, "recovery").Dispose(); Object.DestroyImmediate(bag.gameObject);
            }
        }
    }
}
