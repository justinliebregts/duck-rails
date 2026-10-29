using System;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

public static class EquipmentPersistenceChecks
{
    [MenuItem("Tools/DuckZ/Validation/Items/Validate Equipment Persistence")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying && !PhotonNetwork.IsConnected,
            "Run equipment checks outside Play Mode and disconnected from Photon.");
        // Edit-mode checks do not run Photon's Play Mode initialization.
        var previousPool = PhotonNetwork.PrefabPool;
        if (previousPool == null) PhotonNetwork.PrefabPool = new DefaultPool();
        string previousUser = UserPlayerPrefs.CurrentUserId;
        // Isolate tests without firing account-change listeners or consuming legacy saves.
        var userField = typeof(UserPlayerPrefs).GetField("currentUserId", BindingFlags.Static | BindingFlags.NonPublic);
        string[] legacyKeys = { "equipment.head", "equipment.eyes" };
        var legacyValues = new string[legacyKeys.Length];
        for (int i = 0; i < legacyKeys.Length; i++)
        {
            legacyValues[i] = PlayerPrefs.HasKey(legacyKeys[i]) ? PlayerPrefs.GetString(legacyKeys[i]) : null;
            PlayerPrefs.DeleteKey(legacyKeys[i]);
        }
        userField.SetValue(null, "equipment-check-" + Guid.NewGuid().ToString("N"));
        var anchor = new GameObject("Head anchor");
        GameObject owner = null, remote = null;
        try
        {
            VerifySessionTeardown();
            VerifyStandbyAndIdle();
            foreach (string name in new[] { "ItemHelmet", "ItemNightVisionGoggles" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/" + name + ".prefab");
                Require(prefab != null, "Missing prefab: " + name);
                var view = prefab.GetComponent<PhotonView>();
                Require(view.ObservedComponents.Contains(prefab.GetComponent<HeadEquipment>()), "Head state is not observed");
                VerifyWornPhysics(prefab, anchor.transform);
                owner = UnityEngine.Object.Instantiate(prefab);
                remote = UnityEngine.Object.Instantiate(prefab);
                string saveKey = name == "ItemHelmet" ? "equipment.head" : "equipment.eyes";
                var worn = owner.GetComponent<HeadEquipment>();
                var copy = remote.GetComponent<HeadEquipment>();
                Invoke(worn, "Equip", anchor.transform);
                Require(UserPlayerPrefs.GetString(saveKey, "") == name, "Equip must save prefab key");
                typeof(HeadEquipment).GetField("equippedActorNumber", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(worn, 42);
                var output = new PhotonStream(true, null);
                typeof(PhotonStream).GetField("writeData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(output, new System.Collections.Generic.List<object>());
                worn.OnPhotonSerializeView(output, default);
                copy.OnPhotonSerializeView(new PhotonStream(false, output.ToArray()), default);
                Require(copy.IsEquipped && !copy.IsLocallyEquipped, "Remote equipped state lost");
                Require((int)typeof(HeadEquipment).GetField("equippedActorNumber", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(copy) == 42,
                    "Remote wearer lost");
                Invoke(worn, "HandleGrabbed", (object)null);
                Require(UserPlayerPrefs.GetString(saveKey, "") == "", "Removal must clear save");
                Invoke(worn, "Equip", anchor.transform);
                UnityEngine.Object.DestroyImmediate(owner);
                Require(UserPlayerPrefs.GetString(saveKey, "") == name, "Teardown must preserve save");
                EquipmentInventory.Clear();
                Require(UserPlayerPrefs.GetString(saveKey, "") == "", "Death must clear save");
                UnityEngine.Object.DestroyImmediate(remote);
            }
            VerifyDualSlots(anchor.transform);
            Debug.Log("EquipmentPersistenceChecks PASS: helmet/goggles prefab observers, save/removal/teardown/death, Photon stream wearer and equipped state.");
        }
        finally
        {
            EquipmentInventory.Clear();
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (remote != null) UnityEngine.Object.DestroyImmediate(remote);
            UnityEngine.Object.DestroyImmediate(anchor);
            PhotonNetwork.PrefabPool = previousPool;
            userField.SetValue(null, previousUser);
            for (int i = 0; i < legacyKeys.Length; i++)
                if (legacyValues[i] != null) PlayerPrefs.SetString(legacyKeys[i], legacyValues[i]);
            PlayerPrefs.Save();
        }
    }

    static void VerifySessionTeardown()
    {
        var client = PhotonNetwork.NetworkingClient;
        var previousRoom = client.CurrentRoom;
        var previousState = client.State;
        bool previousQuit = ConnectionHandler.AppQuits;
        var previousPlayer = GR.Player.instance;
        var rig = new GameObject("Equipment teardown rig");
        rig.SetActive(false);
        var anchor = new GameObject("Equipment teardown anchor").transform;
        anchor.SetParent(rig.transform);
        GameObject item = null;
        try
        {
            GR.Player.instance = rig.AddComponent<GR.Player>();
            GR.Player.instance.helmetAnchor = anchor;
            client.CurrentRoom = new Room("equipment-teardown-check", new RoomOptions());
            foreach (string name in new[] { "ItemHelmet", "ItemNightVisionGoggles" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/" + name + ".prefab");
                item = UnityEngine.Object.Instantiate(prefab);
                var gear = item.GetComponent<HeadEquipment>();
                Call(item.GetComponent<PhotonGrabbable>(), "Awake");
                Invoke(gear, "Awake");
                SetMine(item.GetComponent<PhotonView>(), true);
                string key = name == "ItemHelmet" ? "equipment.head" : "equipment.eyes";
                string otherKey = name == "ItemHelmet" ? "equipment.eyes" : "equipment.head";
                string otherName = name == "ItemHelmet" ? "ItemNightVisionGoggles" : "ItemHelmet";
                UserPlayerPrefs.SetString(otherKey, otherName);

                // Quit can disable gear before Photon disconnects, with the local rig still alive.
                // Room switching and connection loss also disable gear, but after leaving Joined.
                foreach (var state in new[] { ClientState.Joined, ClientState.Leaving,
                    ClientState.DisconnectingFromGameServer, ClientState.ConnectingToMasterServer,
                    ClientState.Disconnecting, ClientState.Disconnected })
                foreach (bool quitting in new[] { false, true })
                foreach (string cleanup in new[] { "OnDisable", "UnequipAsDropped" })
                {
                    client.State = state;
                    ConnectionHandler.AppQuits = quitting;
                    Invoke(gear, "Equip", anchor);
                    Require(UserPlayerPrefs.GetString(key, "") == name, name + " must save immediately on equip");
                    Invoke(gear, cleanup);
                    bool preserve = quitting || state != ClientState.Joined;
                    Require(UserPlayerPrefs.GetString(key, "") == (preserve ? name : ""),
                        name + " " + state + " quitting=" + quitting + " " + cleanup + " persistence");
                    Require(!gear.IsEquipped && (cleanup == "OnDisable" || item.transform.parent != anchor),
                        "Preserving a save must still clean up the local worn object");
                    Require(UserPlayerPrefs.GetString(otherKey, "") == otherName,
                        "Equipment cleanup must preserve the other slot");
                }

                client.State = ClientState.Joined;
                ConnectionHandler.AppQuits = false;
                Invoke(gear, "Equip", anchor);
                Invoke(gear, "HandleGrabbed", (object)null);
                Require(UserPlayerPrefs.GetString(key, "") == "", "Intentional removal must clear " + name);
                Invoke(gear, "Equip", anchor);
                gear.DropOnDeath();
                Require(UserPlayerPrefs.GetString(key, "") == "", "Death drop must clear " + name);
                UnityEngine.Object.DestroyImmediate(item);
                item = null;
            }
            Debug.Log("Equipment teardown PASS: both slots, quit before/after disconnect, room leave, connection loss, local cleanup, other-slot preservation, removal and death.");
        }
        finally
        {
            // Cleanup must not touch the user's saves or the caller's Photon/rig state.
            ConnectionHandler.AppQuits = true;
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(rig);
            EquipmentInventory.Clear();
            GR.Player.instance = previousPlayer;
            client.CurrentRoom = previousRoom;
            client.State = previousState;
            ConnectionHandler.AppQuits = previousQuit;
        }
    }

    sealed class StandbyActor : Player
    {
        public StandbyActor() : base("Equipment standby check", 42, false) { }
    }

    static void VerifyStandbyAndIdle()
    {
        var client = PhotonNetwork.NetworkingClient;
        var previousRoom = client.CurrentRoom;
        var previousState = client.State;
        var previousDebug = client.LoadBalancingPeer.DebugOut;
        var previousPlayer = GR.Player.instance;
        var coordinatorField = typeof(PhotonMasterHandoff).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousCoordinator = coordinatorField.GetValue(null);
        var rig = new GameObject("Equipment standby checks");
        rig.SetActive(false);
        var items = new HeadEquipment[2];
        PhotonPlayer human = null;
        try
        {
            GR.Player.instance = rig.AddComponent<GR.Player>();
            GR.Player.instance.helmetAnchor = rig.transform;
            var inventory = rig.AddComponent<EquipmentInventory>();
            Call(inventory, "Awake");
            rig.AddComponent<PhotonView>();
            human = rig.AddComponent<PhotonPlayer>();
            SetMine(human.photonView, true);
            PhotonPlayer.Active.Add(human);
            var handoff = rig.AddComponent<PhotonMasterHandoff>();
            Call(handoff, "Awake");
            Set(handoff, "focused", true);
            client.CurrentRoom = new Room("equipment-standby-check", new RoomOptions());
            client.State = ClientState.Joined;
            // No connection is opened; availability announcements cannot reach a server.
            client.LoadBalancingPeer.DebugOut = ExitGames.Client.Photon.DebugLevel.OFF;
            string[] names = { "ItemHelmet", "ItemNightVisionGoggles" };
            for (int i = 0; i < names.Length; i++)
            {
                items[i] = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Items/" + names[i] + ".prefab")).GetComponent<HeadEquipment>();
                Call(items[i].GetComponent<PhotonGrabbable>(), "Awake");
                Invoke(items[i], "Awake");
                SetMine(items[i].GetComponent<PhotonView>(), true);
                Invoke(items[i], "Equip", rig.transform);
            }
            foreach (string callback in new[] { "OnApplicationPause", "OnApplicationFocus", "OnDisplayFocus" })
            foreach (bool value in new[] { true, false, true, false })
            {
                typeof(PhotonMasterHandoff).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(handoff, new object[] { value });
                foreach (var item in items)
                {
                    var grab = item.GetComponent<PhotonGrabbable>();
                    Require(item.IsLocallyEquipped && item.transform.parent == rig.transform,
                        callback + " must preserve worn " + item.name);
                    Require(!PhotonGrabbable.HeldItems.Contains(grab), "Worn gear must not enter held recovery sweep");
                    Require(!grab.ApplyUnavailableDrop(42, grab.HoldVersion, Vector3.zero, Quaternion.identity),
                        "Stale standby drop must not remove worn gear");
                }
                Require(UserPlayerPrefs.GetString("equipment.head", "") == names[0] &&
                    UserPlayerPrefs.GetString("equipment.eyes", "") == names[1], "Standby must preserve both saves");
            }

            Require(PhotonPlayer.Local == human, "Idle test must have a ready local network player");
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), inventory,
                typeof(EquipmentInventory).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic));
            for (int i = 0; i < 100; i++) tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) tick();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(allocated == 0, "Restored equipment inventory allocated " + allocated + " bytes while idle");

            var actor = new StandbyActor();
            typeof(Player).GetProperty("IsInactive").SetValue(actor, true);
            foreach (var item in items)
            {
                Invoke(item, "ApplyRemoteEquippedState", true, actor.ActorNumber);
                item.OnPlayerLeftRoom(actor);
                Require(item.IsEquipped && !(bool)Get(item, "destroyAfterOwnershipFallback"),
                    "Inactive wearer must keep remote equipment");
            }
            Debug.Log("Equipment standby PASS: pause/resume, app/XR focus, both worn slots/saves, stale drop rejection, inactive remote wearer; inventory idle = "
                + allocated + " bytes / 10,000 updates.");
        }
        finally
        {
            client.State = ClientState.Disconnected;
            foreach (var item in items) if (item != null) UnityEngine.Object.DestroyImmediate(item.gameObject);
            if (human != null) PhotonPlayer.Active.Remove(human);
            UnityEngine.Object.DestroyImmediate(rig);
            coordinatorField.SetValue(null, previousCoordinator);
            GR.Player.instance = previousPlayer;
            client.CurrentRoom = previousRoom;
            client.State = previousState;
            client.LoadBalancingPeer.DebugOut = previousDebug;
        }
    }
    static void Invoke(HeadEquipment target, string method, params object[] args) =>
        typeof(HeadEquipment).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    static void VerifyWornPhysics(GameObject prefab, Transform anchor)
    {
        GameObject item = UnityEngine.Object.Instantiate(prefab);
        var handObject = new GameObject("Equipment check hand");
        int velocityWarnings = 0;
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (message.IndexOf("kinematic", StringComparison.OrdinalIgnoreCase) >= 0 &&
                message.IndexOf("velocity", StringComparison.OrdinalIgnoreCase) >= 0)
                velocityWarnings++;
        };
        Application.logMessageReceived += capture;
        try
        {
            var gear = item.GetComponent<HeadEquipment>();
            var grab = item.GetComponent<PhotonGrabbable>();
            var body = item.GetComponent<Rigidbody>();
            var view = item.GetComponent<PhotonView>();
            var sync = item.GetComponent<PhotonTransformView>();
            var hand = handObject.AddComponent<PlayerHand>();
            // Edit-mode prefab instantiation does not run the gameplay lifecycle.
            Call(grab, "Awake");
            Invoke(gear, "Awake");
            Invoke(gear, "OnEnable");
            SetMine(view, true);
            Invoke(gear, "Equip", anchor);

            // Check both LateUpdate orders and both network controllers. Neither may detach
            // the item or turn physics back on, even for offline KeepLocalPhysics copies.
            foreach (bool mine in new[] { true, false })
            {
                SetMine(view, mine);
                for (int frame = 0; frame < 4; frame++)
                {
                    if ((frame & 1) == 0) Invoke(gear, "LateUpdate");
                    Call(grab, "UpdateItemPose");
                    Require(body.isKinematic && !body.useGravity && !body.detectCollisions,
                        "Network pose changed worn physics");
                    Require(item.transform.parent == anchor, "Network pose detached worn gear");
                    Require(sync == null || !sync.enabled, "Worn transform interpolation still enabled");
                    if ((frame & 1) != 0) Invoke(gear, "LateUpdate");
                }
            }
            grab.KeepLocalPhysics();
            Call(grab, "UpdateItemPose");
            Require(body.isKinematic && item.transform.parent == anchor, "Offline worn state overridden");
            Set(grab, "keepLocalPhysics", false);

            // A held packet can arrive before the matching unequip packet.
            Set(grab, "remoteHeld", true);
            item.transform.SetParent(hand.transform, true);
            Invoke(gear, "LateUpdate");
            Require(item.transform.parent == hand.transform, "Head pose overrode remote hand transition");
            Set(grab, "remoteHeld", false);

            SetMine(view, true);
            grab.Grab(hand);
            Require(grab.IsHeld && !gear.IsEquipped, "Grab did not clear equipped state");
            grab.Release(Vector3.right, Vector3.zero);
            Require(!grab.IsHeld && !gear.IsEquipped && body.detectCollisions,
                "Release did not restore dropped state");
            Call(grab, "UpdateItemPose");
            Require(!body.isKinematic && body.useGravity && (sync == null || sync.enabled),
                "Owned drop did not resume simulation and synchronization");

            Invoke(gear, "Equip", anchor);
            gear.DropOnDeath();
            Require(!gear.IsEquipped && !body.isKinematic && body.detectCollisions,
                "Death did not drop worn gear");
            Require((double)Get(grab, "dropDeadline") > 0d, "Death drop lost cleanup deadline");

            // Remote equip/unequip, followed by ownership transfer of the dropped item.
            SetMine(view, false);
            Invoke(gear, "ApplyRemoteEquippedState", true, 42);
            Set(gear, "equippedAnchor", anchor);
            Invoke(gear, "LateUpdate");
            Call(grab, "UpdateItemPose");
            Require(body.isKinematic && item.transform.parent == anchor, "Remote equip failed");
            Invoke(gear, "ApplyRemoteEquippedState", false, 0);
            Call(grab, "UpdateItemPose");
            Require(!gear.IsEquipped && body.isKinematic && body.detectCollisions &&
                item.transform.parent != anchor && (sync == null || sync.enabled), "Remote drop failed");
            SetMine(view, true);
            Call(grab, "UpdateItemPose");
            Require(!body.isKinematic && body.useGravity, "New controller did not simulate dropped gear");
            Require(velocityWarnings == 0, "Equipment transitions emitted kinematic velocity warnings");
        }
        finally
        {
            Application.logMessageReceived -= capture;
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(handObject);
        }
        Debug.Log("Equipment physics PASS: " + prefab.name + " update orders, owner/remote/offline wear, grab/release, death, remote drop and controller transfer.");
    }

    static void VerifyDualSlots(Transform anchor)
    {
        var helmetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/ItemHelmet.prefab");
        var gogglesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/ItemNightVisionGoggles.prefab");
        PhotonPreparePool.Register(helmetPrefab);
        PhotonPreparePool.Register(gogglesPrefab);
        var helmet = UnityEngine.Object.Instantiate(helmetPrefab).GetComponent<HeadEquipment>();
        var goggles = UnityEngine.Object.Instantiate(gogglesPrefab).GetComponent<HeadEquipment>();
        var duplicate = UnityEngine.Object.Instantiate(helmetPrefab).GetComponent<HeadEquipment>();
        var handObject = new GameObject("Slot removal hand");
        var inventoryObject = new GameObject("Slot restore inventory");
        var inventory = inventoryObject.AddComponent<EquipmentInventory>();
        var previousPlayer = GR.Player.instance;
        var playerObject = new GameObject("Restore player");
        playerObject.SetActive(false);
        try
        {
            Call(inventory, "Awake");
            GR.Player.instance = playerObject.AddComponent<GR.Player>();
            Require(helmet.Slot == HeadEquipment.EquipmentSlot.Head && goggles.Slot == HeadEquipment.EquipmentSlot.Eyes,
                "Prefab slot assignments");
            foreach (var item in new[] { helmet, goggles, duplicate })
            {
                Call(item.GetComponent<PhotonGrabbable>(), "Awake");
                Invoke(item, "Awake");
                Invoke(item, "OnEnable");
                SetMine(item.GetComponent<PhotonView>(), true);
            }
            var occupied = typeof(HeadEquipment).GetMethod("HasEquippedItem", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (bool helmetFirst in new[] { true, false })
            {
                var first = helmetFirst ? helmet : goggles;
                var second = helmetFirst ? goggles : helmet;
                Invoke(first, "Equip", anchor);
                Require(!(bool)occupied.Invoke(second, new object[] { anchor }), "Different slot must remain available");
                Invoke(second, "Equip", anchor);
                Require((bool)occupied.Invoke(duplicate, new object[] { anchor }), "Duplicate helmet must be blocked");
                Require(UserPlayerPrefs.GetString("equipment.head", "") == "ItemHelmet" &&
                    UserPlayerPrefs.GetString("equipment.eyes", "") == "ItemNightVisionGoggles", "Both saves must coexist");
                Invoke(first, "HandleGrabbed", (object)null);
                Require(second.IsLocallyEquipped, "Removing one must preserve the other");
                string remainingKey = helmetFirst ? "equipment.eyes" : "equipment.head";
                Require(!string.IsNullOrEmpty(UserPlayerPrefs.GetString(remainingKey, "")), "Removal erased the other slot save");
                Invoke(second, "HandleGrabbed", (object)null);
            }

            var hand = handObject.AddComponent<PlayerHand>();
            var grabNearest = typeof(HeadEquipment).GetMethod("TryGrabNearest", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var target in new[] { goggles, helmet })
            {
                Invoke(helmet, "Equip", anchor);
                Invoke(goggles, "Equip", anchor);
                hand.transform.position = ((Transform)Get(target, "grabPoint")).position;
                grabNearest.Invoke(null, new object[] { hand, new[] { helmet, goggles } });
                Require(hand.Held == target.GetComponent<Grabbable>(), "Removal must select the nearest grab point");
                var other = target == helmet ? goggles : helmet;
                Require(other.IsLocallyEquipped, "One grip removed both items");
                hand.Release();
            }

            EquipmentInventory.Clear();
            UserPlayerPrefs.SetString("equipment.head", "ItemNightVisionGoggles");
            typeof(EquipmentInventory).GetMethod("MigrateLegacySave", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Require(UserPlayerPrefs.GetString("equipment.head", "") == "" &&
                UserPlayerPrefs.GetString("equipment.eyes", "") == "ItemNightVisionGoggles", "Legacy goggles migration");

            // Independent in-flight requests must survive clearing the other slot.
            var pending = (string[])Get(inventory, "pendingKeys");
            pending[0] = "ItemHelmet";
            pending[1] = "ItemNightVisionGoggles";
            EquipmentInventory.Clear(HeadEquipment.EquipmentSlot.Head);
            Require(pending[0] == null && pending[1] == "ItemNightVisionGoggles", "Clear cancelled the other restore");
            inventory.AcceptRestore(goggles);
            Require(pending[1] == null, "Matching restore must consume its request");
            pending[0] = "ItemHelmet";
            pending[1] = "ItemNightVisionGoggles";
            EquipmentInventory.Clear();
            Require(pending[0] == null && pending[1] == null &&
                UserPlayerPrefs.GetString("equipment.head", "") == "" && UserPlayerPrefs.GetString("equipment.eyes", "") == "",
                "Death must clear both saves and pending restores");
            inventory.OnLeftRoom();
            var restored = (bool[])Get(inventory, "restored");
            Require(!restored[0] && !restored[1], "Rejoin must reset both restore slots");
            Invoke(helmet, "HandleGrabbed", (object)null);
            Invoke(goggles, "HandleGrabbed", (object)null);
            UserPlayerPrefs.SetString("equipment.head", "ItemHelmet");
            UserPlayerPrefs.SetString("equipment.eyes", "ItemNightVisionGoggles");
            var restore = typeof(EquipmentInventory).GetMethod("RestoreSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (bool eyesFirst in new[] { true, false })
            {
                inventory.OnLeftRoom();
                restore.Invoke(inventory, new object[] { HeadEquipment.EquipmentSlot.Head, anchor });
                restore.Invoke(inventory, new object[] { HeadEquipment.EquipmentSlot.Eyes, anchor });
                Require(pending[0] == "ItemHelmet" && pending[1] == "ItemNightVisionGoggles", "Both saves must queue a restore");
                var first = eyesFirst ? goggles : helmet;
                var second = eyesFirst ? helmet : goggles;
                Require(inventory.AcceptRestore(first), "First restore rejected");
                Invoke(first, "Equip", anchor);
                Require(inventory.AcceptRestore(second), "Second restore rejected");
                Require(!(bool)occupied.Invoke(second, new object[] { anchor }), "Restored item blocked the other slot");
                Invoke(second, "Equip", anchor);
                Require(helmet.IsLocallyEquipped && goggles.IsLocallyEquipped, "Both items must restore together");
                Invoke(first, "HandleGrabbed", (object)null);
                Invoke(second, "HandleGrabbed", (object)null);
                UserPlayerPrefs.SetString("equipment.head", "ItemHelmet");
                UserPlayerPrefs.SetString("equipment.eyes", "ItemNightVisionGoggles");
            }
            Debug.Log("Dual equipment slots PASS: both equip orders, duplicate rejection, nearest removal, independent saves/requests, legacy migration and death reset.");
        }
        finally
        {
            GR.Player.instance = previousPlayer;
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(helmet.gameObject);
            UnityEngine.Object.DestroyImmediate(goggles.gameObject);
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(inventoryObject);
        }
    }

    static void SetMine(PhotonView view, bool value) =>
        typeof(PhotonView).GetProperty("IsMine").GetSetMethod(true).Invoke(view, new object[] { value });
    static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static object Get(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
