using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class TrackedHandInputChecks
{
    // These fixtures temporarily replace PUN's local identity. Never run in the working editor.
    public static void RunHeldObjects()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch only");
        checks = 0;
        using var identity = new HoldIdentity();
        var local = new HoldActor(2, true);
        var peer = new HoldActor(3, false);
        var room = new Room("tracked-hold-fixture", new RoomOptions());
        room.AddPlayer(local); room.AddPlayer(peer);
        room.masterClientId = 2;
        room.LoadBalancingClient = PhotonNetwork.NetworkingClient;
        SelectHoldPeer(room, local);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (bool left in new[] { true, false })
        foreach (string kind in HoldKinds)
        {
            using var owner = new HoldFixture(kind, left, true);
            using var remote = new HoldFixture(kind, left, false);
            owner.Grab();
            int version = owner.Version;
            object[] initial = owner.Capture();
            remote.Receive(initial, local);
            Check(remote.Holding, kind + ": initial hold replicates");
            owner.Tick(default, false, .1f);
            owner.Tick(default, false, 1000f);
            Check(owner.Holding && owner.Version == version, kind + ": long loss preserves attachment/hold version");
            Check(!owner.RawGrip.down && !owner.input.WasHeldGripReleased(left), kind + ": UI grip resets independently");
            owner.CheckAllocations();
            object[] retained = owner.Capture();
            Check(retained.Length == initial.Length, kind + ": held packet layout unchanged");
            remote.Receive(retained, local);
            Check(remote.Holding && remote.Version == version, kind + ": remote retains same hold");
            owner.Tick(Fist, true);
            var partial = Fist; partial.middle = .5f;
            owner.Tick(partial, true);
            Check(owner.Holding && !owner.hand.WasUsePressed, kind + ": closed/partial recovery preserves existing threshold");
            owner.Tick(default, false, 1000f);
            owner.Tick(default, true, .02f);
            Check(!owner.Holding, kind + ": confidently open recovery releases");
            remote.Receive(owner.Capture(), local);
            Check(!remote.Holding, kind + ": intentional release replicates");
            owner.CheckContents(); remote.CheckContents();

            owner.Grab(); owner.Tick(default, false, 1000f);
            owner.SwitchController(false);
            Check(!owner.Holding && !owner.RawGrip.wasReleased, kind + ": controller-up switch releases retained hold without changing shared edges");
            owner.Grab(); owner.Tick(default, false, 1000f);
            owner.SwitchController(true);
            Check(owner.Holding, kind + ": controller-down switch preserves hold");
            owner.ControllerRelease();
            Check(!owner.Holding, kind + ": normal controller release unchanged");

            owner.Grab(); owner.Tick(default, false, 1000f);
            Apply(owner.input, left, true, default, false, .02f, false, owner.RawGrip, default);
            owner.UpdateHand();
            Check(!owner.Holding, kind + ": focus loss overrides retained hold");

            owner.Grab(); owner.Tick(default, false, 1000f);
            owner.input.mouseGrip = true;
            Call(owner.input, "UpdateDesktopInput"); owner.UpdateHand();
            Check(!owner.Holding, kind + ": desktop switch releases retained hold");
            owner.input.mouseGrip = false;

            owner.Grab(); owner.Tick(default, false, 1000f);
            owner.view.ViewID = 700001;
            if (owner.bag != null) owner.inventory.DropHandsForUnavailablePlayer();
            else owner.item.DropForUnavailablePlayer();
            Check(!owner.Holding, kind + ": availability cleanup overrides retained grip");
            owner.Tick(Fist, true);
            Check(!owner.Holding, kind + ": returning tracking never regrabs a recovered object");
            owner.CheckContents();
        }
        SharedHandEdges();
        Debug.Log("TrackedHandInputChecks HELD PASS " + checks + ": both hands, real item/five weapon prefabs, loaded bags, shared-input isolation, tracking flicker, release boundary, network streams, controller/focus/desktop switches and availability cleanup.");
    }

    static void SharedHandEdges()
    {
        using var left = new HoldFixture("WeaponNailBat", true, true);
        using var right = new HoldFixture("Bag", false, true, left.input, left.inventory);
        left.hand.transform.position = Vector3.left;
        right.hand.transform.position = Vector3.right;
        left.Grab(); right.Grab();
        left.Tick(default, false, 1000f);
        right.Tick(Fist, true);
        Check(left.Holding && right.Holding, "one lost hand does not release either held object");
        right.Tick(default, true);
        Check(left.Holding && !right.Holding, "tracked hand releases normally while the other remains untracked");
        right.Grab();
        right.Tick(default, false, 1000f);
        var boundary = Fist; boundary.middle = TrackedHandInput.ReleaseThreshold;
        left.Tick(boundary, true);
        Check(left.Holding && right.Holding, "exact release threshold retains both existing holds");
        boundary.middle -= .001f;
        left.Tick(boundary, true);
        Check(!left.Holding && right.Holding, "below-threshold valid release affects only that hand");
        left.Grab();
        for (int i = 0; i < 60; i++)
        {
            left.Tick(default, false, i % 2 == 0 ? .01f : .36f);
            right.Tick(default, false, .02f);
            left.Tick(Fist, true, .01f);
            right.Tick(Fist, true, .01f);
        }
        Check(left.Holding && right.Holding && !left.hand.WasUsePressed && !right.hand.WasUsePressed,
            "repeated short/long tracking flicker keeps both objects without use events");
        left.SwitchController(false);
        Check(!left.Holding && right.Holding, "switching one hand to released controller leaves other hold intact");
        var controllerGrip = new ButtonStatus { down = true, wasPressed = true };
        left.input.leftGripButton = controllerGrip;
        Apply(left.input, true, false, default, false, .02f, true, default, default);
        Check(left.hand.TryGrab(left.item), "controller can grab again after hand-tracking switch");
        right.Tick(default, false, 1000f);
        Check(left.Holding && left.input.leftGripButton.down && left.input.leftGripButton.wasPressed,
            "other hand's tracking loss preserves controller-held object and original button edges");
        left.ControllerRelease();
        Check(!left.Holding && right.Holding, "controller release remains independent of retained bag");
        right.Tick(default, true);
        Check(!right.Holding, "other hand's final intentional release still works");
        right.CheckContents();
    }

    static readonly string[] HoldKinds = { "ItemFlashlight", "WeaponNailBat", "WeaponFryPan", "WeaponGorilla", "WeaponNinjaKnife", "WeaponRambroKnife", "Bag" };
    sealed class HoldActor : Player { public HoldActor(int id, bool local) : base("tracked hold", id, local) { } }

    sealed class HoldIdentity : IDisposable
    {
        readonly LoadBalancingClient client = PhotonNetwork.NetworkingClient;
        readonly Room room;
        readonly Player player;
        readonly ClientState state;
        readonly ExitGames.Client.Photon.DebugLevel debug;
        readonly IHandGripHandler handler = PlayerHand.GripHandler;
        public HoldIdentity()
        {
            room = client.CurrentRoom; player = client.LocalPlayer; state = client.State;
            debug = client.LoadBalancingPeer.DebugOut;
            client.LoadBalancingPeer.DebugOut = ExitGames.Client.Photon.DebugLevel.OFF;
        }
        public void Dispose()
        {
            client.CurrentRoom = room; client.State = state;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, player);
            client.LoadBalancingPeer.DebugOut = debug;
            PlayerHand.GripHandler = handler;
        }
    }

    static void SelectHoldPeer(Room room, Player player)
    {
        var client = PhotonNetwork.NetworkingClient;
        client.CurrentRoom = room; client.State = ClientState.Joined;
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, player);
    }

    sealed class HoldFixture : IDisposable
    {
        public readonly ControllerInput input;
        public readonly PlayerHand hand;
        public readonly BagInventory inventory;
        public readonly PhotonGrabbable item;
        public readonly BagObject bag;
        public readonly PhotonView view;
        readonly GameObject root;
        readonly bool left;
        public HoldFixture(string kind, bool left, bool owner, ControllerInput sharedInput = null, BagInventory sharedInventory = null)
        {
            this.left = left;
            root = new GameObject("Tracked hold fixture"); root.SetActive(false);
            input = sharedInput != null ? sharedInput : root.AddComponent<ControllerInput>();
            hand = root.AddComponent<PlayerHand>();
            hand.controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            Set(hand, "input", input); Call(hand, "Awake"); Set(hand, "holdPointOnSkin", true);
            inventory = sharedInventory != null ? sharedInventory : root.AddComponent<BagInventory>();
            if (kind == "Bag")
            {
                var go = new GameObject("Loaded held bag"); go.SetActive(false);
                bag = go.AddComponent<BagObject>();
                Set(bag, "rb", go.GetComponent<Rigidbody>());
                Set(bag, "box", go.AddComponent<BoxCollider>());
                Set(bag, "cargo", new HelicopterCargo(bag, go.GetComponent<Rigidbody>()));
                Set(bag, "grabLeft", go.transform); Set(bag, "grabRight", go.transform);
                var slots = (List<BagSlot>)Get(bag, "slots");
                foreach (string key in new[] { "ItemGoldBar", "WeaponNailBat", "", "ItemFlashlight" })
                {
                    var slotRoot = new GameObject("slot"); slotRoot.SetActive(false); slotRoot.transform.SetParent(go.transform);
                    var slot = slotRoot.AddComponent<BagSlot>(); Set(slot, "itemKey", key); slots.Add(slot);
                }
                view = bag.photonView;
            }
            else
            {
                string folder = kind.StartsWith("Weapon", StringComparison.Ordinal) ? "Weapons" : "Items";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + folder + "/" + kind + ".prefab");
                Check(prefab != null, "fixture prefab exists: " + kind);
                var go = Object.Instantiate(prefab); go.SetActive(false);
                item = go.GetComponent<PhotonGrabbable>(); Call(item, "Awake");
                view = go.GetComponent<PhotonView>();
                go.SetActive(true);
            }
            view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
            typeof(PhotonView).GetProperty("IsMine").SetValue(view, owner);
            root.SetActive(true);
        }
        public bool Holding => item != null ? item.IsHeldByAnyPlayer : bag.State == BagState.Held;
        public int Version => item != null ? item.HoldVersion : bag.HoldVersion;
        public ButtonStatus RawGrip => left ? input.leftGripButton : input.rightGripButton;
        public void Grab()
        {
            PlayerHand.GripHandler = inventory;
            Sample(input, left, default); Sample(input, left, Fist);
            if (bag != null) Call(inventory, "Hold", bag, hand);
            else Check(hand.TryGrab(item), "fixture grab succeeds");
            Check(Holding, "fixture is held");
        }
        public void Tick(TrackedHandInput.Curls pose, bool valid, float dt = .02f)
        { Sample(input, left, pose, valid, dt); UpdateHand(); }
        public void UpdateHand() { PlayerHand.GripHandler = inventory; Call(hand, "LateUpdate"); }
        public void SwitchController(bool down)
        {
            ButtonStatus old = RawGrip;
            if (left) input.leftGripButton = new ButtonStatus { down = down };
            else input.rightGripButton = new ButtonStatus { down = down };
            Apply(input, left, false, default, false, .02f, true, old, default);
            UpdateHand();
        }
        public void ControllerRelease()
        {
            if (left) input.leftGripButton = new ButtonStatus { wasReleased = true };
            else input.rightGripButton = new ButtonStatus { wasReleased = true };
            Apply(input, left, false, default, false, .02f, true, new ButtonStatus { down = true }, default);
            UpdateHand();
        }
        public object[] Capture()
        {
            var stream = Writing();
            if (item != null) item.OnPhotonSerializeView(stream, default); else bag.OnPhotonSerializeView(stream, default);
            return stream.ToArray();
        }
        public void Receive(object[] data, Player sender)
        {
            typeof(PhotonView).GetProperty("Controller").SetValue(view, sender);
            var info = new PhotonMessageInfo(sender, PhotonNetwork.ServerTimestamp, view);
            if (item != null) item.OnPhotonSerializeView(new PhotonStream(false, data), info);
            else bag.OnPhotonSerializeView(new PhotonStream(false, data), info);
        }
        public void CheckContents()
        {
            if (bag != null) Check(string.Join("|", bag.Keys()) == "ItemGoldBar|WeaponNailBat||ItemFlashlight", "loaded bag contents preserved");
        }
        public void CheckAllocations()
        {
            PlayerHand.GripHandler = inventory;
            var tracked = (TrackedHandInput)Get(input, left ? "trackedLeft" : "trackedRight");
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), hand, typeof(PlayerHand).GetMethod("LateUpdate", Private));
            for (int i = 0; i < 20; i++) { tracked.Tick(default, false, 1f); tick(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { tracked.Tick(default, false, 1f); tick(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && Holding, "existing hold path allocates zero bytes over 10,000 missing samples");
        }
        public void Dispose()
        {
            if (item != null) Object.DestroyImmediate(item.gameObject);
            if (bag != null) { BagObject.Active.Remove(bag); Object.DestroyImmediate(bag.gameObject); }
            Object.DestroyImmediate(root);
        }
    }
}
