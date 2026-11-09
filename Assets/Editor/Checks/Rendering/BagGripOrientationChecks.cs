using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Real player/bag prefabs and PUN callbacks, with input modes injected at the calibration boundary.
// The synthetic room never connects to Photon. Run only in a disposable batch editor.
public static class BagGripOrientationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    public static int LastCheckCount => checks;
    static readonly Quaternion[] Turns = { Quaternion.identity, Quaternion.Euler(25, 80, -40), Quaternion.Euler(180, -60, 30) };

    // Rails working-editor coverage without replacing PUN identity or opening a gameplay scene.
    public static void RunAttachmentChecks()
    {
        if (Application.isPlaying || PhotonNetwork.InRoom)
            throw new InvalidOperationException("Run outside Play mode and a room.");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (int startup in new[] { 3, 0, 1, 2 })
            foreach (Quaternion turn in Turns)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"), scene);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                try
                {
                    var driver = root.GetComponentInChildren<LocalBodyDriver>(true);
                    driver.Body.InitializeIKPreview();
                    driver.HeadAnchor.SetPositionAndRotation(new Vector3(0, 2.5f, 0), Quaternion.identity);
                    driver.LeftHandAnchor.SetPositionAndRotation(new Vector3(-.22f, 2.23f, .3f), turn);
                    driver.RightHandAnchor.SetPositionAndRotation(new Vector3(.22f, 2.23f, .3f), turn);
                    var hands = root.GetComponentsInChildren<PlayerHand>(true);
                    Check(hands.Length == 2, "both real player hands found");
                    foreach (var hand in hands) Call(hand, "Awake");
                    Mode(driver, startup);
                    foreach (var hand in hands) Call(hand, "TryAttachHoldPointToSkin");
                    CheckHands(driver, hands, "startup " + startup);
                    foreach (int mode in new[] { 0, 3, 1, 2, 3, 0 })
                    {
                        Mode(driver, mode);
                        CheckHands(driver, hands, "switch " + mode);
                        Quaternion left = hands[0].HoldPoint.rotation, right = hands[1].HoldPoint.rotation;
                        Sample(driver, true, OVRInput.Controller.None, false);
                        Sample(driver, false, OVRInput.Controller.RTouch, false);
                        driver.PoseNow();
                        Rotation(hands[0].HoldPoint.rotation, left, "left loss retention");
                        Rotation(hands[1].HoldPoint.rotation, right, "right loss retention");
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
            DeferredAttachment(scene);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("BagGripOrientationChecks attachment PASS " + checks + " assertions (no synthetic room or live transport).");
    }

    public static void Run()
    {
        if (!Application.isBatchMode || Application.isPlaying || PhotonNetwork.InRoom)
            throw new InvalidOperationException("Run in a disposable batch editor outside Play mode and a room.");
        checks = 0;
        using var network = new NetworkScope();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (int startup in new[] { 3, 0, 1, 2 })
            foreach (Quaternion turn in Turns)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"), scene);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                try
                {
                    var driver = root.GetComponentInChildren<LocalBodyDriver>(true);
                    driver.Body.InitializeIKPreview();
                    driver.HeadAnchor.SetPositionAndRotation(new Vector3(0, 2.5f, 0), Quaternion.identity);
                    driver.LeftHandAnchor.SetPositionAndRotation(new Vector3(-.22f, 2.23f, .3f), turn);
                    driver.RightHandAnchor.SetPositionAndRotation(new Vector3(.22f, 2.23f, .3f), turn);
                    var hands = root.GetComponentsInChildren<PlayerHand>(true);
                    Check(hands.Length == 2, "both real player hands found");
                    foreach (var hand in hands) Call(hand, "Awake");
                    Mode(driver, startup);
                    // Initialization can also occur while the previous tracked pose is frozen.
                    if (startup == 3)
                    {
                        Sample(driver, true, OVRInput.Controller.None, false);
                        Sample(driver, false, OVRInput.Controller.RTouch, false);
                        driver.PoseNow();
                    }
                    foreach (var hand in hands) Call(hand, "TryAttachHoldPointToSkin");
                    CheckHands(driver, hands, "startup " + startup);
                    network.Owner.BindSources(driver.HeadAnchor, driver.LeftPalmSource, driver.RightPalmSource, driver);
                    foreach (byte bagId in new byte[] { 1, 2, 3, 4 })
                    foreach (var hand in hands)
                        CheckBag(driver, hands, hand, bagId, network);
                }
                finally { Object.DestroyImmediate(root); }
            }
            DeferredAttachment(scene);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("BagGripOrientationChecks PASS " + checks + ": four startup modes, three rotations, both hands, four bags, mode/loss transitions, transfer, drop/regrab, stow, delayed/duplicate packets and recovery pose.");
    }

    static void CheckBag(LocalBodyDriver driver, PlayerHand[] hands, PlayerHand hand, byte id, NetworkScope network)
    {
        var bag = CreateBag(id, true, network);
        var remote = CreateBag(id, false, network);
        try
        {
            string keys = string.Join("|", bag.Keys());
            bag.HoldLocal(hand);
            int version = bag.HoldVersion;
            object[] first = null;
            Vector3 localPosition = bag.transform.localPosition;
            Quaternion localRotation = bag.transform.localRotation;
            foreach (int mode in new[] { 0, 3, 1, 2, 3, 0 })
            {
                Mode(driver, mode);
                CheckHands(driver, hands, "switch " + mode);
                Check(bag.State == BagState.Held && bag.HoldVersion == version, "input switch retains hold identity");
                Near(bag.transform.localPosition, localPosition, "input switch retains grip position");
                Rotation(bag.transform.localRotation, localRotation, "input switch retains grip rotation");
                CheckGrab(bag, hand);
                object[] snapshot = network.Replicate(bag, remote);
                if (first == null) first = snapshot;
                else
                {
                    // Bag and avatar streams can arrive at different rates. A prior held offset
                    // must still reconstruct correctly against the newest decoded avatar pose.
                    network.Receive(remote, first);
                    network.CheckPose(bag, remote, "delayed held offset");
                    network.Receive(remote, snapshot);
                    network.Receive(remote, snapshot);
                    network.CheckPose(bag, remote, "duplicate held offset");
                }
                Quaternion saved = hand.HoldPoint.rotation;
                Sample(driver, true, OVRInput.Controller.None, false);
                Sample(driver, false, OVRInput.Controller.RTouch, false);
                driver.PoseNow();
                Rotation(hand.HoldPoint.rotation, saved, "tracking loss keeps last valid orientation");
                network.Replicate(bag, remote);
            }

            foreach (Quaternion turn in Turns)
            {
                driver.LeftHandAnchor.rotation = driver.RightHandAnchor.rotation = turn;
                driver.PoseNow();
                CheckHands(driver, hands, "moving controllers");
                CheckGrab(bag, hand);
                network.Replicate(bag, remote);
            }
            driver.controllerInput.mouseGrip = true;
            driver.PoseNow();
            CheckHands(driver, hands, "desktop mode");
            network.Replicate(bag, remote);
            driver.controllerInput.mouseGrip = false;
            Mode(driver, 0);
            network.Replicate(bag, remote);

            // A late join/recovery uses the separately serialized held offsets too.
            object[] recovery = (object[])Call(bag, "CaptureRecovery");
            Call(remote, "ApplyRecovery", (object)recovery);
            network.Render(remote);
            network.CheckPose(bag, remote, "recovery snapshot");

            PlayerHand other = hands[0] == hand ? hands[1] : hands[0];
            bag.HoldLocal(other);
            CheckGrab(bag, other);
            network.Replicate(bag, remote);
            network.Receive(remote, first);
            Check(remote.HeldInLeftHand == other.IsControllerLTouch, "older hold cannot undo hand transfer");
            network.CheckPose(bag, remote, "stale pre-transfer snapshot");

            bag.DropLocal(Vector3.zero);
            Check(bag.State == BagState.Dropped && bag.transform.parent == null && !bag.GetComponent<Rigidbody>().isKinematic,
                "drop detaches and restores physics");
            network.Replicate(bag, remote);
            Check(remote.State == BagState.Dropped, "drop replicates");
            bag.HoldLocal(hand);
            CheckGrab(bag, hand);
            network.Replicate(bag, remote);
            bag.StowLocal(driver.Body.Chest, true);
            Check(bag.State == BagState.Stowed && bag.transform.parent == null, "stow detaches from hand");
            bag.HoldLocal(hand);
            CheckGrab(bag, hand);
            network.Replicate(bag, remote);
            Check(string.Join("|", bag.Keys()) == keys && string.Join("|", remote.Keys()) == keys, "bag contents survive transitions");
        }
        finally { Object.DestroyImmediate(bag.gameObject); Object.DestroyImmediate(remote.gameObject); }
    }

    static BagObject CreateBag(byte id, bool mine, NetworkScope network)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bags/Bag.prefab"));
        var bag = root.GetComponent<BagObject>();
        Call(bag, "Awake");
        bag.photonView.OwnerActorNr = network.Sender.ActorNumber;
        Mine(bag.photonView, mine);
        typeof(PhotonView).GetProperty("Controller").SetValue(bag.photonView, network.Sender);
        bag.Init(BagObject.SpawnData(id, null, 0, BagObject.Target.Dropped, 0));
        Check(bag.GrabFor(true) != null && bag.GrabFor(false) != null, "real catalog bag markers present");
        // Keep a loaded slot without creating a miniature or modifying catalog assets.
        Check(bag.Slots.Count > 0, "real catalog slots present");
        Set(bag.Slots[0], "itemKey", "ItemGoldBar");
        return bag;
    }

    static void CheckHands(LocalBodyDriver driver, PlayerHand[] hands, string phase)
    {
        foreach (var hand in hands)
        {
            var source = hand.IsControllerLTouch ? driver.LeftPalmSource : driver.RightPalmSource;
            var bone = hand.IsControllerLTouch ? driver.Body.LeftHand : driver.Body.RightHand;
            var serialized = new SerializedObject(hand);
            Rotation(hand.HoldPoint.rotation, source.rotation * Quaternion.Euler(serialized.FindProperty("holdLocalEuler").vector3Value), phase + " hold orientation");
            Near(hand.HoldPoint.position, bone.position + source.rotation * serialized.FindProperty("holdLocalPosition").vector3Value, phase + " hold position");
            Near(hand.HoldPoint.lossyScale, Vector3.one, phase + " unit hold scale");
            Check(hand.HoldPoint.parent == bone, phase + " attached to visible hand");
        }
    }

    static void CheckGrab(BagObject bag, PlayerHand hand)
    {
        Check(bag.State == BagState.Held && bag.transform.parent == hand.HoldPoint, "bag attached to intended hand");
        Near(bag.GrabFor(hand.IsControllerLTouch).position, hand.HoldPoint.position, "bag marker contacts hand");
        Rotation(bag.GrabFor(hand.IsControllerLTouch).rotation, hand.HoldPoint.rotation, "bag marker matches orientation");
        Near(bag.transform.lossyScale, Vector3.one, "bag scale unchanged");
    }

    static void DeferredAttachment(UnityEngine.SceneManagement.Scene scene)
    {
        var root = new GameObject("No body yet");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var hand = root.AddComponent<PlayerHand>(); Call(hand, "Awake");
            Call(hand, "TryAttachHoldPointToSkin");
            Check(!(bool)Get(hand, "holdPointOnSkin") && hand.HoldPoint.parent == root.transform, "missing body safely defers attachment");
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), hand, typeof(PlayerHand).GetMethod("LateUpdate", Private));
            Set(hand, "holdPointOnSkin", true);
            for (int i = 0; i < 32; i++) tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) tick();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, "initialized hand update allocates zero bytes across 10000 ticks");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Mode(LocalBodyDriver driver, int mode)
    {
        Sample(driver, true, (mode & 1) != 0 ? OVRInput.Controller.LHand : OVRInput.Controller.LTouch, true);
        Sample(driver, false, (mode & 2) != 0 ? OVRInput.Controller.RHand : OVRInput.Controller.RTouch, true);
        driver.PoseNow();
    }
    static void Sample(LocalBodyDriver driver, bool left, OVRInput.Controller active, bool tracked)
    {
        _ = driver.LeftPalmSource; _ = driver.RightPalmSource;
        var calibration = Get(driver, "_palmCalibration");
        Call(calibration, "Source", left, active, tracked);
    }

    sealed class Actor : Player { public Actor(int id, bool local) : base("bag orientation fixture", id, local) { } }
    sealed class NetworkScope : IDisposable
    {
        readonly LoadBalancingClient client = PhotonNetwork.NetworkingClient;
        readonly Room savedRoom;
        readonly Player savedPlayer;
        readonly ClientState savedState;
        readonly ExitGames.Client.Photon.DebugLevel savedDebug;
        readonly List<PhotonPlayer> savedAvatars = new List<PhotonPlayer>(PhotonPlayer.Active);
        readonly NewPlayer savedMovement = NewPlayer.instance;
        readonly GR.Player savedHuman = GR.Player.instance;
        public readonly Player Sender = new Actor(2, true);
        public readonly PhotonPlayer Owner, Remote;

        public NetworkScope()
        {
            savedRoom = client.CurrentRoom; savedPlayer = client.LocalPlayer; savedState = client.State;
            savedDebug = client.LoadBalancingPeer.DebugOut;
            client.LoadBalancingPeer.DebugOut = ExitGames.Client.Photon.DebugLevel.OFF;
            var room = new Room("bag-orientation-fixture", new RoomOptions()) { LoadBalancingClient = client };
            room.AddPlayer(Sender); room.AddPlayer(new Actor(3, false)); room.masterClientId = 2;
            client.CurrentRoom = room; client.State = ClientState.Joined;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, Sender);
            NewPlayer.instance = null; GR.Player.instance = null;
            Owner = Avatar(true); Remote = Avatar(false);
        }
        PhotonPlayer Avatar(bool mine)
        {
            var root = new GameObject(mine ? "Sending avatar" : "Receiving avatar"); root.SetActive(false);
            var view = root.AddComponent<PhotonView>(); view.OwnerActorNr = Sender.ActorNumber; Mine(view, mine);
            return root.AddComponent<PhotonPlayer>();
        }
        public object[] Replicate(BagObject bag, BagObject remote)
        {
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.Add(Owner);
            var pose = Writing(); Owner.OnPhotonSerializeView(pose, default);
            var decoded = (PosePacket)PosePacket.Deserialize(PosePacket.Serialize((PosePacket)pose.ToArray()[0]));
            // Supply the decoded playback pose, as existing interaction tests do. Transport
            // and playback timing are outside this deterministic callback test.
            Set(Remote, "_lastPose", decoded.ToBodyPose()); Set(Remote, "_hasPose", true);
            var stream = Writing(); bag.OnPhotonSerializeView(stream, default);
            object[] data = stream.ToArray();
            if (bag.State == BagState.Held) Check(data.Length == 7, "held bag wire layout unchanged");
            Receive(remote, data);
            CheckPose(bag, remote, "PUN snapshot");
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.Add(Owner);
            return data;
        }
        public void Receive(BagObject remote, object[] data)
        {
            remote.OnPhotonSerializeView(new PhotonStream(false, data), new PhotonMessageInfo(Sender, PhotonNetwork.ServerTimestamp, remote.photonView));
            Render(remote);
        }
        public void Render(BagObject remote)
        {
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.Add(Remote);
            Call(remote, "UpdateRemote");
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.Add(Owner);
        }
        public void CheckPose(BagObject bag, BagObject remote, string label)
        {
            Check(bag.State == remote.State, label + " state");
            Near(bag.transform.position, remote.transform.position, label + " position", .003f);
            Rotation(bag.transform.rotation, remote.transform.rotation, label + " rotation", .2f);
        }
        public void Dispose()
        {
            Object.DestroyImmediate(Owner.gameObject); Object.DestroyImmediate(Remote.gameObject);
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.AddRange(savedAvatars);
            NewPlayer.instance = savedMovement; GR.Player.instance = savedHuman;
            client.CurrentRoom = savedRoom; client.State = savedState;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, savedPlayer);
            client.LoadBalancingPeer.DebugOut = savedDebug;
        }
    }

    public static void RunBatch()
    {
        try
        {
            Run();
            Directory.CreateDirectory(".utmp/BagOrientation");
            File.WriteAllText(".utmp/BagOrientation/orientation.txt", "PASS " + checks + " assertions; deterministic Unity/PUN callback tests, not a live server/headset test.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void RunRegressions()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch only");
        int failed = 0;
        var report = new List<string>();
        foreach (Action suite in new Action[] { Run, ControllerPalmCalibrationChecks.Run,
            ControllerPalmInteractionChecks.Run, TrackedHandInputChecks.Run, TrackedHandInputChecks.RunHeldObjects,
            PlayerPalmAlignmentChecks.Run, PickupPoseChecks.Run, HeldItemRecoveryChecks.Run })
        {
            string name = suite.Method.DeclaringType.Name + "." + suite.Method.Name;
            try { suite(); report.Add("PASS " + name); }
            catch (Exception error) { failed++; report.Add("FAIL " + name + ": " + error); Debug.LogException(error); }
        }
        Directory.CreateDirectory(".utmp/BagOrientation");
        File.WriteAllLines(".utmp/BagOrientation/regressions.txt", report);
        Debug.Log("Bag grip regressions: " + (report.Count - failed) + " passed, " + failed + " failed.");
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }

    static PhotonStream Writing() { var stream = new PhotonStream(true, null); typeof(PhotonStream).GetMethod("SetWriteStream", Private).Invoke(stream, new object[] { new List<object>(), 0 }); return stream; }
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Mine(PhotonView view, bool value) => typeof(PhotonView).GetProperty("IsMine").SetValue(view, value);
    static void Near(Vector3 a, Vector3 b, string label, float tolerance = .0002f) => Check(Vector3.Distance(a, b) < tolerance, label + ": " + Vector3.Distance(a, b));
    static void Rotation(Quaternion a, Quaternion b, string label, float tolerance = .05f) => Check(Quaternion.Angle(a, b) < tolerance, label + ": " + Quaternion.Angle(a, b));
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("BagGripOrientationChecks: " + label); }
}
