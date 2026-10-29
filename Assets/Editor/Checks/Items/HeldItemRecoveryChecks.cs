using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Disposable batch fixture only. Actual server delivery/Quest callbacks require a play test.
public static class HeldItemRecoveryChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks, nextId = 800;
    sealed class Actor : Player { public Actor(int id, bool local = false) : base("held recovery", id, local) {} }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, P); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, P).Invoke(o, args);
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); Debug.Log("PASS: " + label); }
    static void Register(PhotonView view, int owner, int id = 0)
    {
        view.ViewID = id == 0 ? nextId++ : id;
        // RegisterPhotonView intentionally clears its registry outside Play Mode.
        object views = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(views, new object[] { view.ViewID, view });
        view.OwnerActorNr = owner;
        view.ControllerActorNr = owner;
    }
    static PhotonGrabbable Item(int owner)
    {
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        Register(item.GetComponent<PhotonView>(), owner);
        return item;
    }
    static void HeldPacket(PhotonGrabbable item, Player actor, int version)
    {
        var view = item.GetComponent<PhotonView>();
        item.OnPhotonSerializeView(new PhotonStream(false, new object[] {
            0, true, actor.ActorNumber, false, version, true, 0d, false, false, Vector3.zero, Quaternion.identity
        }), new PhotonMessageInfo(actor, PhotonNetwork.ServerTimestamp, view));
    }
    static BagObject Bag(int actor, BagState state, params string[] keys)
    {
        var go = new GameObject("bag recovery"); go.SetActive(false);
        var bag = go.AddComponent<BagObject>();
        Set(bag, "rb", go.GetComponent<Rigidbody>());
        Register(bag.photonView, actor);
        Set(bag, "state", state); Set(bag, "holderActor", actor); Set(bag, "holdVersion", 7);
        var slots = Get<List<BagSlot>>(bag, "slots");
        foreach (string key in keys)
        {
            var slotGo = new GameObject("slot"); slotGo.SetActive(false); slotGo.transform.SetParent(go.transform);
            var slot = slotGo.AddComponent<BagSlot>(); Set(slot, "itemKey", key); slots.Add(slot);
        }
        BagObject.Active.Add(bag);
        Call(bag, "ApplyPhysics");
        return bag;
    }
    static void Dispose(BagObject bag) { BagObject.Active.Remove(bag); Object.DestroyImmediate(bag.gameObject); }
    static void Beat(PhotonMasterHandoff h, Player actor, bool ready)
    {
        var e = new EventData { Code = 105 };
        e.Parameters[ParameterCode.ActorNr] = actor.ActorNumber;
        e.Parameters[ParameterCode.Data] = new object[] { PhotonNetwork.ServerTimestamp, ready };
        h.OnEvent(e);
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        checks = 0;
        var client = PhotonNetwork.NetworkingClient;
        var savedRoom = client.CurrentRoom; var savedLocal = client.LocalPlayer;
        var savedState = client.State; var savedDebug = client.LoadBalancingPeer.DebugOut;
        var local = new Actor(2, true); var remote = new Actor(3);
        var room = new Room("held recovery", new RoomOptions()); room.LoadBalancingClient = client;
        room.AddPlayer(local); room.AddPlayer(remote); Set(room, "masterClientId", 2);
        client.CurrentRoom = room;
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, local);
        client.State = ClientState.Joined; client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = PhotonSession.PlayScene;
            ItemDrops(local, remote);
            BagDrops(local, remote);
            Authority(room, local, remote);
            Heartbeat(room, local, remote);
            ReviewWakeEdges(room, local, remote);
            Lifetime(room, local, remote);
            Debug.Log("HeldItemRecoveryChecks: ALL PASSED (" + checks + " assertions)");
        }
        finally
        {
            client.CurrentRoom = savedRoom;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, savedLocal);
            client.State = savedState; client.LoadBalancingPeer.DebugOut = savedDebug;
        }
    }
    static void ItemDrops(Player local, Player remote)
    {
        var hand = new GameObject("hand").AddComponent<PlayerHand>();
        var item = Item(local.ActorNumber); var view = item.GetComponent<PhotonView>(); var rb = item.GetComponent<Rigidbody>();
        int releases = 0; item.OnReleased += _ => releases++;
        Check(hand.ForceGrab(item), "local pickup remains immediate");
        int version = item.HoldVersion;
        item.DropForUnavailablePlayer();
        Check(hand.Held == null && !item.IsHeldByAnyPlayer && !PhotonGrabbable.HeldItems.Contains(item), "forced drop clears both references and held registry");
        Check(releases == 0 && rb.detectCollisions && !rb.isKinematic && rb.useGravity && rb.linearVelocity == Vector3.zero,
            "forced drop restores collisions/gravity without throwing, equipping, or release events");
        Check(view.OwnerActorNr == 0 && view.IsMine && item.GetComponent<PhotonTransformView>().enabled,
            "dropped item belongs to room and has live master physics/synchronization");
        Check(!item.ApplyUnavailableDrop(local.ActorNumber, version, Vector3.one * 90, Quaternion.identity), "duplicate release is harmless");
        typeof(PhotonView).GetProperty("IsMine").SetValue(view, false);
        HeldPacket(item, local, version);
        Check(!item.IsHeldByAnyPlayer, "delayed held snapshot cannot resurrect a recovered hold");
        view.OwnerActorNr = remote.ActorNumber; view.ControllerActorNr = remote.ActorNumber;
        HeldPacket(item, remote, version + 1);
        Check(item.IsHeldByAnyPlayer && PhotonGrabbable.HeldItems.Contains(item), "new holder's new hold is accepted");
        Check(!item.ApplyUnavailableDrop(local.ActorNumber, version, Vector3.one, Quaternion.identity) && item.IsHeldByAnyPlayer,
            "old release cannot drop another player's new pickup");
        item.ApplyUnavailableDrop(remote.ActorNumber, version + 1, Vector3.zero, Quaternion.identity);
        Check(hand.ForceGrab(item), "recovered item is immediately grabbable");
        Set(item, "pickupPending", true);
        item.DropForUnavailablePlayer();
        view.OwnerActorNr = local.ActorNumber; view.ControllerActorNr = local.ActorNumber;
        item.OnOwnershipTransfered(view, remote);
        Check(view.OwnerActorNr == 0 && !item.PickupPending && !item.IsHeldByAnyPlayer, "delayed local ownership echo cannot revive cancelled pickup");
        Check(hand.ForceGrab(item), "fresh pickup still works after cancelled ownership echo");
        item.OnOwnershipTransfered(view, null);
        view.OwnerActorNr = remote.ActorNumber; view.ControllerActorNr = remote.ActorNumber;
        item.OnControllerChange(remote, local);
        Check(hand.Held == null && !item.IsHeld && releases == 0, "controller-only loss clears hand without gameplay release");
        HeldPacket(item, remote, 10);
        Call(item, "OnDisable"); Call(item, "OnEnable");
        Check(PhotonGrabbable.HeldItems.Contains(item), "re-enabled remote hold remains discoverable without another snapshot");
        Check(item.ApplyUnavailableDrop(remote.ActorNumber, 11, Vector3.zero, Quaternion.identity),
            "reliable item release may precede the newest held snapshot");
        var equipment = item.gameObject.AddComponent<HeadEquipment>(); Call(equipment, "Awake"); Set(item, "headEquipment", equipment);
        Set(equipment, "equipped", true); Set(equipment, "remoteEquipped", true); Set(equipment, "equippedActorNumber", remote.ActorNumber);
        view.OwnerActorNr = remote.ActorNumber; view.ControllerActorNr = remote.ActorNumber;
        Check(!item.ApplyUnavailableDrop(remote.ActorNumber, 11, Vector3.zero, Quaternion.identity) && equipment.IsEquipped,
            "old release leaves worn equipment alone");
        Check(item.ApplyUnavailableDrop(remote.ActorNumber, 12, Vector3.zero, Quaternion.identity) && !equipment.IsEquipped,
            "removing equipment and sleeping before a snapshot still drops it");
        Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(hand.gameObject);
    }
    static void BagDrops(Player local, Player remote)
    {
        var inventoryGo = new GameObject("inventory"); inventoryGo.SetActive(false);
        var inventory = inventoryGo.AddComponent<BagInventory>(); Call(inventory, "OnEnable");
        var loaded = Bag(local.ActorNumber, BagState.Held, "WeaponNailBat", "ItemGoldBar", "ItemFlashlight", null);
        var empty = Bag(local.ActorNumber, BagState.Held);
        var worn = Bag(local.ActorNumber, BagState.Stowed, "ItemGoldBar");
        Set(inventory, "leftBag", loaded); Set(inventory, "rightBag", empty); Set(inventory, "backBag", worn);
        int epoch = BagInventory.HandClaimEpoch;
        inventory.DropHandsForUnavailablePlayer();
        Check(loaded.State == BagState.Dropped && empty.State == BagState.Dropped, "both empty and loaded hand bags drop");
        Check(string.Join("|", loaded.Keys()) == "WeaponNailBat|ItemGoldBar|ItemFlashlight|" && empty.Keys().Length == 0,
            "drop preserves mixed weapons, gold, items, and empty slots exactly");
        Check(loaded.CanInteract && !loaded.GetComponent<Rigidbody>().isKinematic && loaded.GetComponent<Rigidbody>().detectCollisions,
            "dropped bag is pickable with master-controlled physics");
        Check(Get<BagObject>(inventory, "leftBag") == null && Get<BagObject>(inventory, "rightBag") == null && worn.State == BagState.Stowed,
            "hand cleanup leaves worn bag and its inventory unchanged");
        Check(BagInventory.HandClaimEpoch != epoch, "sleep invalidates delayed hand deliveries");
        Check(!worn.ApplyUnavailableDrop(local.ActorNumber, 7, Vector3.zero, Quaternion.identity), "old release leaves worn bag alone");
        Check(worn.ApplyUnavailableDrop(local.ActorNumber, 8, Vector3.zero, Quaternion.identity) && worn.State == BagState.Dropped && worn.Keys()[0] == "ItemGoldBar",
            "removing bag from back and sleeping before a snapshot drops it intact");
        Check(!loaded.ApplyUnavailableDrop(local.ActorNumber, 7, Vector3.one, Quaternion.identity), "duplicate bag drop cannot move or empty it");
        loaded.photonView.OwnerActorNr = remote.ActorNumber; loaded.photonView.ControllerActorNr = remote.ActorNumber;
        loaded.OnPhotonSerializeView(new PhotonStream(false, new object[] {
            0, (byte)BagState.Held, local.ActorNumber, 7, false, Vector3.zero, Quaternion.identity
        }), new PhotonMessageInfo(remote, PhotonNetwork.ServerTimestamp, loaded.photonView));
        Check(loaded.State == BagState.Dropped, "stale bag held snapshot cannot reattach recovered bag");
        Set(loaded, "state", BagState.Held); Set(loaded, "holderActor", remote.ActorNumber); Set(loaded, "holdVersion", 8);
        Check(!loaded.ApplyUnavailableDrop(local.ActorNumber, 7, Vector3.zero, Quaternion.identity), "old drop cannot steal bag from new holder");
        Call(loaded, "RpcContents", new[] { "changed" }, false, loaded.photonView.SpawnToken, new PhotonMessageInfo(local, PhotonNetwork.ServerTimestamp, loaded.photonView));
        Check(string.Join("|", loaded.Keys()) == "WeaponNailBat|ItemGoldBar|ItemFlashlight|", "former controller's delayed contents cannot overwrite recovered bag");
        Check(loaded.ApplyUnavailableDrop(remote.ActorNumber, 9, Vector3.zero, Quaternion.identity),
            "reliable bag release may precede the newest held snapshot");
        var pending = Get<IList>(inventory, "pending");
        var hand = new GameObject("pending hand").AddComponent<PlayerHand>();
        var kind = typeof(BagInventory).GetNestedType("PendingKind", BindingFlags.NonPublic);
        foreach (string name in new[] { "Hold", "Take", "Put" }) Call(inventory, "AddPending", Enum.Parse(kind, name), loaded, hand, null);
        inventory.DropHandsForUnavailablePlayer();
        Check(pending.Count == 0, "availability cleanup cancels hold/take/put requests together");
        var delivery = Bag(remote.ActorNumber, BagState.Dropped, "ItemGoldBar");
        Set(delivery, "awaitingClaim", true); Set(delivery, "requester", local.ActorNumber); Set(delivery, "holdVersion", 0);
        delivery.ApplyUnavailableDrop(local.ActorNumber, 0, Vector3.zero, Quaternion.identity);
        delivery.photonView.OwnerActorNr = local.ActorNumber; delivery.photonView.ControllerActorNr = local.ActorNumber;
        delivery.OnOwnershipTransfered(delivery.photonView, remote);
        Check(delivery.photonView.OwnerActorNr == 0 && delivery.State == BagState.Dropped && delivery.Keys()[0] == "ItemGoldBar",
            "late ownership grant of cancelled bag delivery returns it to room without losing contents");
        Dispose(delivery);
        var joiningBag = Bag(remote.ActorNumber, BagState.Dropped, "ItemGoldBar");
        Call(joiningBag, "RpcContents", new[] { "" }, false, joiningBag.photonView.SpawnToken, new PhotonMessageInfo(local, PhotonNetwork.ServerTimestamp, joiningBag.photonView));
        Check(joiningBag.Keys()[0] == "", "joining client accepts initial contents before cross-sender ownership update");
        Dispose(joiningBag);
        Call(inventory, "OnDisable");
        Dispose(loaded); Dispose(empty); Dispose(worn); Object.DestroyImmediate(hand.gameObject); Object.DestroyImmediate(inventoryGo);
    }
    static void Authority(Room room, Player local, Player remote)
    {
        var go = new GameObject("handler"); go.SetActive(false); var handler = go.AddComponent<PhotonHandler>();
        var item = Item(local.ActorNumber); var view = item.GetComponent<PhotonView>();
        Set(room, "masterClientId", remote.ActorNumber); handler.OnMasterClientSwitched(remote);
        Check(view.OwnerActorNr == local.ActorNumber && view.IsMine, "master migration preserves an awake client's claimed room item");
        view.OwnerActorNr = 0; view.ControllerActorNr = remote.ActorNumber;
        Set(room, "masterClientId", local.ActorNumber); handler.OnMasterClientSwitched(local);
        Check(view.Owner == null && view.OwnerActorNr == 0 && view.IsMine, "ownerless room item follows new master without assigning an owner");
        Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(go);
    }
    static void Heartbeat(Room room, Player local, Player remote)
    {
        var h = new GameObject("availability").AddComponent<PhotonMasterHandoff>();
        Set(h, "focused", true); Set(h, "room", room); Call(h, "ResetObservation");
        var avatar = new GameObject("local avatar").AddComponent<PhotonPlayer>();
        if (avatar.photonView == null) avatar.gameObject.AddComponent<PhotonView>();
        typeof(PhotonView).GetProperty("IsMine").SetValue(avatar.photonView, true); PhotonPlayer.Active.Add(avatar);
        Beat(h, remote, true);
        var item = Item(remote.ActorNumber); HeldPacket(item, remote, 1);
        var healthyBag = Bag(remote.ActorNumber, BagState.Held, "WeaponNailBat", "ItemGoldBar", "ItemFlashlight");
        PhotonHeldItems.RecoverUnavailable(h);
        Check(item.IsHeldByAnyPlayer, "healthy holder is never released by watchdog");
        var sweep = (Action<PhotonMasterHandoff, int>)Delegate.CreateDelegate(typeof(Action<PhotonMasterHandoff, int>), typeof(PhotonHeldItems).GetMethod("RecoverUnavailable"));
        for (int i = 0; i < 100; i++) sweep(h, 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) sweep(h, 0);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "healthy held-item and loaded-bag sweep allocates zero bytes over 10,000 checks");
        Dispose(healthyBag);
        var newOwner = new Actor(4); room.AddPlayer(newOwner); Beat(h, newOwner, true);
        var transferredBag = Bag(remote.ActorNumber, BagState.Held, "ItemGoldBar");
        transferredBag.photonView.OwnerActorNr = newOwner.ActorNumber; transferredBag.photonView.ControllerActorNr = newOwner.ActorNumber;
        item.GetComponent<PhotonView>().OwnerActorNr = newOwner.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = newOwner.ActorNumber;
        Beat(h, remote, false);
        Check(transferredBag.State == BagState.Held && transferredBag.photonView.OwnerActorNr == newOwner.ActorNumber
            && item.IsHeldByAnyPlayer && item.GetComponent<PhotonView>().OwnerActorNr == newOwner.ActorNumber,
            "ownership arriving before new held snapshot protects healthy new bag/item owner");
        Beat(h, newOwner, false);
        Check(transferredBag.State == BagState.Dropped && !item.IsHeldByAnyPlayer,
            "stale holder metadata still recovers if both old holder and new owner become unavailable");
        Dispose(transferredBag); room.Players.Remove(newOwner.ActorNumber);
        item.GetComponent<PhotonView>().OwnerActorNr = remote.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = remote.ActorNumber;
        HeldPacket(item, remote, 2); Beat(h, remote, true);
        var beats = Get<IDictionary>(h, "heartbeats"); object stale = beats[remote.ActorNumber];
        stale.GetType().GetField("SentAt").SetValue(stale, unchecked(PhotonNetwork.ServerTimestamp - 5100)); beats[remote.ActorNumber] = stale;
        Set(room, "masterClientId", remote.ActorNumber); PhotonHeldItems.RecoverUnavailable(h);
        Check(item.IsHeldByAnyPlayer, "non-master does not run remote recovery");
        Set(room, "masterClientId", local.ActorNumber); PhotonHeldItems.RecoverUnavailable(h);
        Check(!item.IsHeldByAnyPlayer, "five-second silence releases a client's held item");
        item.GetComponent<PhotonView>().OwnerActorNr = remote.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = remote.ActorNumber;
        HeldPacket(item, remote, 3); Beat(h, remote, false);
        Check(!item.IsHeldByAnyPlayer, "explicit unavailability releases immediately without timeout");
        Beat(h, remote, true); item.GetComponent<PhotonView>().OwnerActorNr = remote.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = remote.ActorNumber;
        HeldPacket(item, remote, 4); h.OnPlayerLeftRoom(remote);
        Check(!item.IsHeldByAnyPlayer, "confirmed departure releases immediately");
        h.OnPlayerEnteredRoom(remote);
        Check(!h.IsActorUnavailable(remote.ActorNumber), "newly joined player has time to send first heartbeat");
        item.GetComponent<PhotonView>().OwnerActorNr = remote.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = remote.ActorNumber;
        HeldPacket(item, remote, 5);
        var sleepingBag = Bag(remote.ActorNumber, BagState.Held, "ItemGoldBar", "WeaponNailBat");
        Call(h, "ResetObservation"); PhotonHeldItems.RecoverUnavailable(h);
        Check(item.IsHeldByAnyPlayer && sleepingBag.State == BagState.Held, "first waking master observes peers before recovering their holds");
        Set(h, "observeUntil", Time.realtimeSinceStartupAsDouble - 0.001);
        Set(h, "observationStartedAt", unchecked(PhotonNetwork.ServerTimestamp - 5100)); PhotonHeldItems.RecoverUnavailable(h);
        Check(!item.IsHeldByAnyPlayer && sleepingBag.State == BagState.Dropped && string.Join("|", sleepingBag.Keys()) == "ItemGoldBar|WeaponNailBat",
            "all-asleep first-waker recovers both weapon and loaded bag after observation grace");
        Dispose(sleepingBag);
        var hand = new GameObject("resuming hand").AddComponent<PlayerHand>();
        item.GetComponent<PhotonView>().OwnerActorNr = local.ActorNumber; item.GetComponent<PhotonView>().ControllerActorNr = local.ActorNumber;
        Check(hand.ForceGrab(item), "resume fixture starts with a local hold");
        Set(h, "paused", true); Call(h, "OnApplicationPause", false);
        Check(hand.Held == null && !item.IsHeldByAnyPlayer, "resume clears old hands even when the OS skipped the suspend callback");
        Object.DestroyImmediate(hand.gameObject);
        Object.DestroyImmediate(item.gameObject); PhotonPlayer.Active.Remove(avatar); Object.DestroyImmediate(avatar.gameObject); Object.DestroyImmediate(h.gameObject);
    }
    static void ReviewWakeEdges(Room room, Player local, Player remote)
    {
        var failures = new List<string>();
        void Case(Action action) { try { action(); } catch (Exception e) { failures.Add(e.Message); } }
        Case(() =>
        {
            var bag = Bag(remote.ActorNumber, BagState.Held, "WeaponNailBat", "ItemGoldBar", "ItemFlashlight", null);
            try
            {
                bag.ApplyUnavailableDrop(remote.ActorNumber, 7, Vector3.zero, Quaternion.identity);
                // A pre-suspend ownership grant arrives after recovery. The bag has
                // already fallen/moved, so repairing ownership must not replay its pose.
                bag.transform.position = Vector3.one * 3;
                double deadline = Get<double>(bag, "droppedAt");
                bag.photonView.OwnerActorNr = remote.ActorNumber; bag.photonView.ControllerActorNr = remote.ActorNumber;
                bag.OnOwnershipTransfered(bag.photonView, local);
                Check(bag.ApplyUnavailableDrop(remote.ActorNumber, 7, Vector3.one * 90, Quaternion.identity)
                    && bag.photonView.OwnerActorNr == 0 && bag.CanInteract,
                    "late grant after a completed drop cannot leave bag locked to sleeping owner");
                Check(bag.transform.position == Vector3.one * 3 && Get<double>(bag, "droppedAt") == deadline
                    && string.Join("|", bag.Keys()) == "WeaponNailBat|ItemGoldBar|ItemFlashlight|",
                    "late-grant repair preserves pose, lifetime, and every bag slot");
                Check(!bag.ApplyUnavailableDrop(remote.ActorNumber, 7, Vector3.zero, Quaternion.identity),
                    "already repaired duplicate does no further work");
                bag.photonView.ControllerActorNr = remote.ActorNumber;
                bag.OnPhotonSerializeView(new PhotonStream(false, new object[] {
                    0, (byte)BagState.Stowed, remote.ActorNumber, 6
                }), new PhotonMessageInfo(remote, PhotonNetwork.ServerTimestamp, bag.photonView));
                Check(bag.State == BagState.Dropped && bag.HoldVersion == 7,
                    "queued pre-drop worn snapshot cannot reattach or rewind recovered bag");
                Set(bag, "requester", remote.ActorNumber);
                bag.OnPhotonSerializeView(new PhotonStream(false, new object[] {
                    0, (byte)BagState.Dropped, 0, 7, Vector3.zero, Quaternion.identity, 0d, true
                }), new PhotonMessageInfo(remote, PhotonNetwork.ServerTimestamp, bag.photonView));
                Check(!bag.AwaitingClaim && bag.State == BagState.Dropped,
                    "queued pending-delivery snapshot cannot lock a recovered bag again");
                bag.photonView.OwnerActorNr = local.ActorNumber; bag.photonView.ControllerActorNr = local.ActorNumber;
                Set(bag, "state", BagState.Held); Set(bag, "holderActor", local.ActorNumber); Set(bag, "holdVersion", 8);
                Check(!bag.ApplyUnavailableDrop(remote.ActorNumber, 7, Vector3.zero, Quaternion.identity)
                    && bag.photonView.OwnerActorNr == local.ActorNumber && bag.State == BagState.Held,
                    "late old release cannot revoke a new holder's bag");
            }
            finally { Dispose(bag); }
        });

        string[][] orders = {
            new[] { "OnApplicationPause", "OnApplicationFocus", "OnDisplayFocus" },
            new[] { "OnApplicationPause", "OnDisplayFocus", "OnApplicationFocus" },
            new[] { "OnApplicationFocus", "OnApplicationPause", "OnDisplayFocus" },
            new[] { "OnApplicationFocus", "OnDisplayFocus", "OnApplicationPause" },
            new[] { "OnDisplayFocus", "OnApplicationPause", "OnApplicationFocus" },
            new[] { "OnDisplayFocus", "OnApplicationFocus", "OnApplicationPause" }
        };
        var instance = typeof(PhotonMasterHandoff).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var savedInstance = instance.GetValue(null);
        var avatar = new GameObject("wake review avatar").AddComponent<PhotonPlayer>();
        if (avatar.photonView == null) avatar.gameObject.AddComponent<PhotonView>();
        typeof(PhotonView).GetProperty("IsMine").SetValue(avatar.photonView, true); PhotonPlayer.Active.Add(avatar);
        var handlerGo = new GameObject("wake review handler"); handlerGo.SetActive(false);
        var handler = handlerGo.AddComponent<PhotonHandler>();
        try
        {
            foreach (bool completedHandoff in new[] { false, true })
            foreach (string[] order in orders)
            Case(() =>
            {
                Set(room, "masterClientId", local.ActorNumber);
                var h = new GameObject("wake review availability").AddComponent<PhotonMasterHandoff>();
                instance.SetValue(null, h); Set(h, "focused", true); Set(h, "room", room); Call(h, "ResetObservation");
                var mine = Bag(local.ActorNumber, BagState.Held, "ItemGoldBar", "WeaponNailBat");
                var theirs = Bag(remote.ActorNumber, BagState.Held, "ItemFlashlight", "ItemGoldBar", null);
                // RegisterPhotonView resets the registry in Edit Mode; restore both for the real handler.
                var registry = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                registry.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(registry, new object[] { mine.photonView.ViewID, mine.photonView });
                try
                {
                    Beat(h, remote, true);
                    foreach (string callback in order) Call(h, callback, callback == "OnApplicationPause");
                    Check(mine.State == BagState.Dropped && mine.photonView.OwnerActorNr == 0,
                        "all-sleep: original master releases its loaded bag");
                    if (completedHandoff)
                    {
                        Set(room, "masterClientId", remote.ActorNumber);
                        handler.OnMasterClientSwitched(remote); h.OnMasterClientSwitched(remote);
                    }
                    // The other headset stops without delivering its drop. Wake only the original.
                    foreach (string callback in order) Call(h, callback, callback != "OnApplicationPause");
                    PhotonHeldItems.RecoverUnavailable(h);
                    Check(theirs.State == BagState.Held, "first waker gives silent peers the observation grace");
                    Set(h, "observationStartedAt", unchecked(PhotonNetwork.ServerTimestamp - 5100));
                    Set(h, "observeUntil", Time.realtimeSinceStartupAsDouble - 0.001);
                    if (completedHandoff)
                    {
                        PhotonHeldItems.RecoverUnavailable(h);
                        Check(theirs.State == BagState.Held, "waking former master does not recover before confirmed authority");
                        Set(room, "masterClientId", local.ActorNumber);
                        handler.OnMasterClientSwitched(local); h.OnMasterClientSwitched(local);
                    }
                    PhotonHeldItems.RecoverUnavailable(h);
                    Check(mine.CanInteract && theirs.CanInteract && theirs.State == BagState.Dropped
                        && mine.photonView.OwnerActorNr == 0 && theirs.photonView.OwnerActorNr == 0,
                        "first waking original master recovers loaded bags without a second five-second wait (handoff=" + completedHandoff + ")");
                    Check(string.Join("|", mine.Keys()) == "ItemGoldBar|WeaponNailBat"
                        && string.Join("|", theirs.Keys()) == "ItemFlashlight|ItemGoldBar|",
                        "all-sleep recovery preserves both players' loaded bags exactly");
                    // Another player takes the bag before the previous holder wakes.
                    mine.photonView.OwnerActorNr = remote.ActorNumber; mine.photonView.ControllerActorNr = remote.ActorNumber;
                    Set(mine, "state", BagState.Held); Set(mine, "holderActor", remote.ActorNumber); Set(mine, "holdVersion", 8);
                    Beat(h, remote, true);
                    Check(!mine.ApplyUnavailableDrop(local.ActorNumber, 7, Vector3.one * 90, Quaternion.identity)
                        && mine.State == BagState.Held && mine.photonView.OwnerActorNr == remote.ActorNumber,
                        "late wake/drop cannot steal the recovered bag from its new holder");
                    theirs.photonView.OwnerActorNr = local.ActorNumber; theirs.photonView.ControllerActorNr = local.ActorNumber;
                    Set(theirs, "state", BagState.Held); Set(theirs, "holderActor", local.ActorNumber); Set(theirs, "holdVersion", 8);
                    Check(!theirs.ApplyUnavailableDrop(remote.ActorNumber, 7, Vector3.one * 90, Quaternion.identity)
                        && theirs.State == BagState.Held && theirs.photonView.IsMine,
                        "later waking client cannot reclaim the loaded bag already picked up by original master");
                }
                finally { Dispose(mine); Dispose(theirs); Object.DestroyImmediate(h.gameObject); }
            });
        }
        finally
        {
            instance.SetValue(null, savedInstance); Set(room, "masterClientId", local.ActorNumber);
            PhotonPlayer.Active.Remove(avatar); Object.DestroyImmediate(avatar.gameObject); Object.DestroyImmediate(handlerGo);
        }
        if (failures.Count != 0) throw new Exception("Wake review failures: " + string.Join("; ", failures));
    }
    sealed class Pool : IPunPrefabPool
    {
        public GameObject Instantiate(string id, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(id); go.SetActive(false); go.AddComponent<PhotonView>(); return go;
        }
        public void Destroy(GameObject go) => Object.DestroyImmediate(go);
    }
    static void Lifetime(Room room, Player local, Player remote)
    {
        var savedPool = PhotonNetwork.PrefabPool; PhotonNetwork.PrefabPool = new Pool();
        try
        {
            var go = PhotonNetwork.InstantiateWithRoomLifetime("persistent pickup", Vector3.zero, Quaternion.identity);
            var view = go.GetComponent<PhotonView>();
            Check(view.PreserveOnCreatorLeave && view.CreatorActorNr == local.ActorNumber, "persistent pickup preserves immediate local allocation");
            var options = (RaiseEventOptions)typeof(PhotonNetwork).GetField("SendInstantiateRaiseEventOptions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(options.CachingOption == EventCaching.AddToRoomCacheGlobal, "persistent pickup creation uses room-global cache");
            object views = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(views, new object[] { view.ViewID, view });
            view.OwnerActorNr = remote.ActorNumber; view.ControllerActorNr = remote.ActorNumber;
            PhotonNetwork.DestroyPlayerObjects(local.ActorNumber, true);
            Check(go != null && view.OwnerActorNr == remote.ActorNumber, "creator departure retains pickup now owned by another player");
            PhotonNetwork.DestroyPlayerObjects(remote.ActorNumber, true);
            Check(go != null && view.OwnerActorNr == 0 && view.IsMine, "departing owner leaves persistent pickup under master control");
            Object.DestroyImmediate(go);
            var data = new Hashtable { { (byte)0, "late pickup" }, { (byte)6, PhotonNetwork.ServerTimestamp }, { (byte)7, 5001 }, { (byte)9, true } };
            var receive = typeof(PhotonNetwork).GetMethod("NetworkInstantiate", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Hashtable), typeof(Player) }, null);
            var late = (GameObject)receive.Invoke(null, new object[] { data, null });
            var lateView = late.GetComponent<PhotonView>();
            Check(lateView.PreserveOnCreatorLeave && lateView.IsMine, "cached pickup instantiates safely after creator is gone");
            Object.DestroyImmediate(late);
            var ordinary = PhotonNetwork.Instantiate("ordinary actor object", Vector3.zero, Quaternion.identity);
            var ordinaryView = ordinary.GetComponent<PhotonView>();
            Check(!ordinaryView.PreserveOnCreatorLeave && options.CachingOption == EventCaching.AddToRoomCache,
                "ordinary avatar/object creation keeps actor lifetime and cache");
            views = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(views, new object[] { ordinaryView.ViewID, ordinaryView });
            PhotonNetwork.DestroyPlayerObjects(local.ActorNumber, true);
            Check(ordinary == null, "ordinary player objects still clean up on departure");
        }
        finally { PhotonNetwork.PrefabPool = savedPool; }
    }
}
