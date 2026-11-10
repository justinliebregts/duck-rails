using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run three disposable batch editors with -executeMethod PickupJoinNetworkChecks.Run
// -pickupRun <same fresh GUID> -pickupRole <1|2|3>, without -quit. Each runs a full PUN loop.
// Add -pickupRejoin on all three to exercise a master departure during a bag request.
// Rails: this inherited synthetic-identity harness still needs authenticated account wiring
// before use against the configured production Photon app; it is not Rails live acceptance.
[InitializeOnLoad]
public static partial class PickupJoinNetworkChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Running = "DuckzPickupJoinNetworkCheck";
    static int role, repairMessages;
    static string run, markers, waiting;
    static IEnumerator steps;
    static Func<bool> condition;
    static double deadline;
    static PhotonGrabbable bat, light, gold, secondBat;
    static BagObject bag;
    static PlayerHand left, right;
    static PhotonMasterHandoff handoff;

    static PickupJoinNetworkChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                steps = Scenario();
                EditorApplication.update += Tick;
            }
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch editors only");
        var fixtureFolder = "Assets/PickupCheck-" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(fixtureFolder));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, fixtureFolder + "/" + PhotonSession.RequiredScene + ".unity")) throw new IOException("Saving fixture scene");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        try
        {
            MonitorHolderTrial();
            if (condition != null && !condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(waiting + " / " + PhotonNetwork.NetworkClientState);
                return;
            }
            if (steps.MoveNext()) { condition = (Func<bool>)steps.Current; return; }
            Finish(0);
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        PhotonNetwork.Disconnect();
        EditorApplication.Exit(code);
    }

    static Func<bool> Until(string description, Func<bool> test)
    {
        waiting = description;
        Debug.Log("PICKUP STAGE " + role + " " + description);
        deadline = EditorApplication.timeSinceStartup + 100;
        return test;
    }

    static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        return args[Array.IndexOf(args, name) + 1];
    }

    // Files only coordinate observation barriers. Ownership, poses, inventory and recovery all use Photon.
    static void Mark(string name) => File.WriteAllText(Path.Combine(markers, role + "-" + name), "");
    static bool Marked(int peer, string name) => File.Exists(Path.Combine(markers, peer + "-" + name));
    static Func<bool> Barrier(string name)
    {
        Mark(name);
        return Until(name, () => Marked(1,name) && Marked(2,name) && Marked(3,name));
    }

    static PhotonGrabbable Item(string folder, string name, int id)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + folder + "/" + name + ".prefab");
        var go = Object.Instantiate(prefab, new Vector3(id - 930, 2, 0), Quaternion.identity);
        var view = go.GetComponent<PhotonView>();
        view.ViewID = id;
        return go.GetComponent<PhotonGrabbable>();
    }

    static void Setup()
    {
        Physics.gravity = Vector3.zero;
        var items = new GameObject("fixture item manager").AddComponent<PhotonItemManager>();
        items.itemParent = items.transform;
        items.enabled = false;
        left = new GameObject("left test hand").AddComponent<PlayerHand>(); left.enabled = false;
        right = new GameObject("right test hand").AddComponent<PlayerHand>(); right.enabled = false;
        left.controller = OVRInput.Controller.LTouch; right.controller = OVRInput.Controller.RTouch;
        bat = Item("Weapons", "WeaponNailBat", 930);
        light = Item("Items", "ItemFlashlight", 931);
        gold = Item("Items", "ItemGoldBar", 932);
        secondBat = Item("Weapons", "WeaponNailBat", 933);
        bag = new GameObject("loaded bag").AddComponent<BagObject>();
        bag.photonView.ViewID = 934;
        bag.photonView.OwnershipTransfer = OwnershipOption.Request;
        bag.photonView.ObservedComponents = new List<Component> { bag };
        var slots = (List<BagSlot>)typeof(BagObject).GetField("slots", Fields).GetValue(bag);
        foreach (string key in new[] { "ItemGoldBar", "WeaponNailBat" })
        {
            var child = new GameObject("slot"); child.transform.SetParent(bag.transform); child.SetActive(false);
            var slot = child.AddComponent<BagSlot>();
            typeof(BagSlot).GetField("itemKey", Fields).SetValue(slot, key);
            slots.Add(slot);
        }
        typeof(BagObject).GetField("grabLeft", Fields).SetValue(bag, bag.transform);
        typeof(BagObject).GetField("grabRight", Fields).SetValue(bag, bag.transform);
        // Supply readiness and hand input in a headless editor. PUN drives its own callbacks and streams.
        var avatar = new GameObject("local availability fixture"); avatar.SetActive(false);
        avatar.AddComponent<PhotonView>();
        var human = avatar.AddComponent<PhotonPlayer>();
        typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, true);
        PhotonPlayer.Active.Add(human);
        handoff = new GameObject("pickup recovery").AddComponent<PhotonMasterHandoff>();
        typeof(PhotonMasterHandoff).GetField("focused", Fields).SetValue(handoff, true);
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static bool Loaded() => string.Join("|", bag.Keys()) == "ItemGoldBar|WeaponNailBat";
    static void Grab(PlayerHand hand, PhotonGrabbable item) => Check(hand.ForceGrab(item), "Failed local grab " + item.name);
    static bool EveryoneHolding() => bat.HeldActor == 1 && light.HeldActor == 2 && gold.HeldActor == 2 &&
        secondBat.HeldActor == 3 && bag.HolderActor == 3 && bag.State == BagState.Held && Loaded();
    static bool Loose() => !light.IsHeldByAnyPlayer && !gold.IsHeldByAnyPlayer && !secondBat.IsHeldByAnyPlayer &&
        bag.State == BagState.Dropped && bag.HolderActor == 0;
    static bool Refreshed(int viewId)
    {
        var checkpoint = PhotonNetwork.CurrentRoom.CustomProperties["recovery:s:" + viewId] as object[];
        return checkpoint != null && checkpoint.Length == 7 && checkpoint[6] is long stamp && (int)(stamp >> 32) == 1;
    }

    static IEnumerator Scenario()
    {
        role = int.Parse(Argument("-pickupRole"));
        run = Guid.Parse(Argument("-pickupRun")).ToString("N");
        markers = Path.Combine(Path.GetTempPath(), "DuckzPickupTest-" + run);
        Directory.CreateDirectory(markers);
        Setup();
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = "pickup-check-" + run; settings.FixedRegion = PhotonSession.Region;
        PhotonNetwork.AuthValues = new AuthenticationValues(run + "-" + role);
        Check(PhotonNetwork.ConnectUsingSettings(settings), "connect private test client");
        yield return Until("connect", () => PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer);
        if (role > 1) yield return Until("join permission", () => Marked(1, "join" + role));
        if (role == 1)
            Check(PhotonNetwork.CreateRoom(run, new RoomOptions { MaxPlayers = 3, IsVisible = false, DeleteNullProperties = true }), "create");
        else Check(PhotonNetwork.JoinRoom(run), "join");
        yield return Until("joined", () => PhotonNetwork.InRoom && PhotonNetwork.NetworkClientState == ClientState.Joined);
        Check(PhotonNetwork.LocalPlayer.ActorNumber == role, "join order");
        PhotonNetwork.NetworkingClient.EventReceived += e => { if (e.Code >= 111 && e.Code <= 113) repairMessages++; };
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.NetworkSimulationSettings.IncomingLag = peer.NetworkSimulationSettings.OutgoingLag = 75;
        peer.NetworkSimulationSettings.IncomingJitter = peer.NetworkSimulationSettings.OutgoingJitter = 20;
        peer.NetworkSimulationSettings.IncomingLossPercentage = peer.NetworkSimulationSettings.OutgoingLossPercentage = 3;
        peer.IsSimulationEnabled = true;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-holderCheck") >= 0)
        {
            foreach (var step in HolderTrial()) yield return step;
            yield break;
        }
        if (role == 1)
        {
            Mark("join2");
            yield return Until("second actor", () => PhotonNetwork.CurrentRoom.PlayerCount == 2);
            Grab(right, bat); bag.RequestControl(); bag.HoldLocal(left);
            yield return Until("hold before third join", () => bat.IsHeld && !bat.PickupPending && Marked(2, "saw-hold"));
            Mark("join3");
        }
        if (role == 2)
        {
            yield return Until("prejoin observation", () => bat.HeldActor == 1 && bag.HolderActor == 1);
            Mark("saw-hold");
        }
        yield return Until("late join state", () => PhotonNetwork.CurrentRoom.PlayerCount == 3 &&
            bat.HeldActor == 1 && bag.HolderActor == 1 && Loaded());
        yield return Barrier("joined-hold");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pickupRejoin") >= 0)
        {
            foreach (var step in MasterLeavesAndReturns()) yield return step;
            yield break;
        }
        if (role == 1) bag.DropLocal(Vector3.zero);
        yield return Until("released bag", () => bag.State == BagState.Dropped);
        for (int wave = 0; wave <= 2; wave++)
        {
            if (role == 2) { Grab(left, light); Grab(right, gold); }
            if (role == 3)
            {
                bag.RequestControl();
                yield return Until("bag grant " + wave, () => bag.photonView.IsMine);
                bag.HoldLocal(left); Grab(right, secondBat);
            }
            yield return Until("agree on all holders " + wave, EveryoneHolding);
            yield return Barrier("holders-" + wave);
            if (wave == 0)
            {
                Check(repairMessages == 0, "healthy pickups sent recovery traffic");
                yield return Barrier("healthy-traffic");
            }
            if (wave == 2) break;
            if (role == 1)
            {
                if (wave == 1)
                {
                    double after = EditorApplication.timeSinceStartup + 5;
                    yield return Until("repair grace", () => EditorApplication.timeSinceStartup >= after);
                }
                // Fault injection: the healthy master incorrectly releases confirmed holds.
                // All receivers, diagnostics, repair requests and the subsequent handoff are production code.
                Check(PhotonHeldItems.Drop(light) && PhotonHeldItems.Drop(gold) &&
                    PhotonHeldItems.Drop(bag) && PhotonHeldItems.Drop(secondBat), "inject four unexpected releases");
            }
            yield return Until("forced releases " + wave, Loose);
            yield return Barrier("loose-" + wave);
            if (wave == 0)
            {
                if (role == 1) yield return Until("first targeted repairs committed", () => repairMessages >= 4 &&
                    Refreshed(931) && Refreshed(932) && Refreshed(933) && Refreshed(934));
                Check(PhotonNetwork.MasterClient.ActorNumber == 1, "first repair escalated");
                yield return Barrier("repairs-issued");
            }
            else yield return Until("handoff with original master connected", () => PhotonNetwork.MasterClient.ActorNumber != 1 &&
                PhotonNetwork.CurrentRoom.PlayerCount == 3);
        }
        Check(bat.HeldActor == 1 && Loaded() && PhotonNetwork.CurrentRoom.PlayerCount == 3, "final hold, membership or inventory");
        Debug.Log("PICKUP LIVE PASS actor=" + role + " successor=" + PhotonNetwork.MasterClient.ActorNumber +
            " healthy recovery events=0, total received=" + repairMessages);
        yield return Barrier("passed");
    }

    static IEnumerable<Func<bool>> MasterLeavesAndReturns()
    {
        double until = EditorApplication.timeSinceStartup + 5;
        yield return Until("hold through the reported snapback interval", () =>
        {
            Check(bat.HeldActor == 1 && bag.HolderActor == 1 && Loaded(), "hold or contents lost after late join");
            return EditorApplication.timeSinceStartup >= until;
        });
        yield return Barrier("stable-late-join");
        if (role == 1) bag.DropLocal(Vector3.zero);
        yield return Until("master dropped the bag", () => bag.State == BagState.Dropped);
        if (role == 2)
        {
            bag.RequestControl();
            yield return Until("non-master granted the loaded bag", () => bag.photonView.IsMine);
            bag.HoldLocal(left);
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (role == 3) Grab(right, secondBat);
            yield return Until("confirmed same-bat pickup " + attempt, () => secondBat.HeldActor == 3 &&
                secondBat.GetComponent<PhotonView>().OwnerActorNr == 3 && bag.HolderActor == 2 && Loaded());
            yield return Barrier("held-same-bat-" + attempt);
            if (attempt == 2) break;
            if (role == 1) Check(PhotonHeldItems.Drop(secondBat), "inject single-object failure");
            yield return Until("one bat released", () => !secondBat.IsHeldByAnyPlayer);
            yield return Barrier("released-same-bat-" + attempt);
        }
        Check(repairMessages == 0 && PhotonNetwork.MasterClient.ActorNumber == 1, "isolated losses triggered recovery");
        yield return Barrier("isolated-loss-check");
        if (role == 2)
        {
            bag.DropLocal(Vector3.zero);
            // Delay delivery to the owner while the requester and the departing master keep normal timing.
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingLag = 900;
        }
        yield return Until("loaded bag awaiting a new holder", () => bag.State == BagState.Dropped && bag.photonView.OwnerActorNr == 2);
        yield return Barrier("bag-ready");
        if (role == 3)
        {
            Check(!bag.RequestControl(), "bag should require a remote grant");
            PhotonNetwork.SendAllOutgoingCommands();
            Mark("requested-bag");
        }
        if (role == 1)
        {
            yield return Until("request sent", () => Marked(3, "requested-bag"));
            Check(bat.IsHeld && bag.photonView.OwnerActorNr == 2, "master should leave with bat held and bag grant outstanding");
            Check(PhotonNetwork.LeaveRoom(false), "leave room");
            yield return Until("left game server", () => PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer);
            yield return Until("others picked up while master was out", () => Marked(2, "playing") && Marked(3, "playing"));
            Check(PhotonNetwork.JoinRoom(run), "join the same room again");
            yield return Until("joined as a new actor", () => PhotonNetwork.NetworkClientState == ClientState.Joined &&
                PhotonNetwork.LocalPlayer.ActorNumber == 4);
        }
        else
        {
            yield return Until("master left with two players remaining", () => PhotonNetwork.CurrentRoom.PlayerCount == 2 &&
                PhotonNetwork.MasterClient.ActorNumber == 2 && !PhotonNetwork.CurrentRoom.Players.ContainsKey(1));
            if (role == 2)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingLag = 75;
                yield return Until("departed master's bat available", () => !bat.IsHeldByAnyPlayer);
                Grab(right, bat);
            }
            if (role == 3)
            {
                yield return Until("bag grant survived master departure", () => bag.photonView.IsMine);
                bag.HoldLocal(left);
            }
            yield return Until("pickups work before rejoin", () => bat.HeldActor == 2 && bag.HolderActor == 3 && Loaded());
            Mark("playing");
        }
        yield return Until("everyone sees the current holders", () => PhotonNetwork.CurrentRoom.PlayerCount == 3 &&
            PhotonNetwork.MasterClient.ActorNumber == 2 && bat.HeldActor == 2 && bag.HolderActor == 3 && Loaded());
        if (role == 1) Grab(right, light);
        yield return Until("returning player can pick up again", () => light.HeldActor == 4 && bat.HeldActor == 2 &&
            secondBat.HeldActor == 3 && bag.HolderActor == 3 && bag.photonView.OwnerActorNr == 3 && Loaded());
        Check(repairMessages == 0 && PhotonNetwork.MasterClient.ActorNumber == 2, "unnecessary pickup recovery on rejoin");
        Debug.Log("PICKUP REJOIN PASS role=" + role + " master=2, bat=2, loaded bag=3, returning flashlight holder=4, recovery traffic=0");
        yield return Barrier("passed-rejoin");
    }
}
