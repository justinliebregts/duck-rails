using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class TrackedHandInputChecks
{
    static int holdDeliveryId;
    // Opt-in real UDP transport; unique app version/hidden room cannot match game clients.
    public static void RunCloudHolds()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch only");
        checks = 0;
        using var identity = new HoldIdentity();
        var a = new LoadBalancingClient(ConnectionProtocol.Udp);
        var b = new LoadBalancingClient(ConnectionProtocol.Udp);
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            typeof(PhotonCustomTypes).GetMethod("Register", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            string run = Guid.NewGuid().ToString("N");
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "tracked-hold-check-" + run; settings.FixedRegion = PhotonSession.Region;
            a.AuthValues = new AuthenticationValues("hold-a-" + run); b.AuthValues = new AuthenticationValues("hold-b-" + run);
            Check(a.ConnectUsingSettings(settings) && b.ConnectUsingSettings(settings), "connect private hold peers");
            PumpHolds(a, b, () => a.State == ClientState.ConnectedToMasterServer && b.State == ClientState.ConnectedToMasterServer, "connect");
            string room = "tracked-hold-" + run;
            Check(a.OpCreateRoom(new EnterRoomParams { RoomName = room, RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = 2, PlayerTtl = 0, EmptyRoomTtl = 0 } }), "create private hold room");
            PumpHolds(a, b, () => a.InRoom, "create");
            Check(b.OpJoinRoom(new EnterRoomParams { RoomName = room }), "join private hold room");
            PumpHolds(a, b, () => a.InRoom && b.InRoom && a.CurrentRoom.PlayerCount == 2 && b.CurrentRoom.PlayerCount == 2, "join");
            foreach (var peer in new[] { a.LoadBalancingPeer, b.LoadBalancingPeer })
            {
                peer.NetworkSimulationSettings.IncomingLag = 100;
                peer.NetworkSimulationSettings.OutgoingLag = 100;
                peer.NetworkSimulationSettings.IncomingJitter = 25;
                peer.NetworkSimulationSettings.OutgoingJitter = 25;
                peer.NetworkSimulationSettings.IncomingLossPercentage = 5;
                peer.NetworkSimulationSettings.OutgoingLossPercentage = 5;
                peer.IsSimulationEnabled = true;
            }
            foreach (bool sourceIsMaster in new[] { true, false })
            foreach (bool left in new[] { true, false })
            foreach (string kind in new[] { "ItemFlashlight", "WeaponNailBat", "Bag" })
            {
                var sender = sourceIsMaster ? a : b;
                var receiver = sourceIsMaster ? b : a;
                SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
                using var owner = new HoldFixture(kind, left, true);
                SelectHoldPeer(receiver.CurrentRoom, receiver.LocalPlayer);
                using var remote = new HoldFixture(kind, left, false);
                SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
                owner.Grab();
                int version = owner.Version;
                DeliverHold(sender, receiver, owner, remote);
                Check(remote.Holding, "live initial " + kind + " hold");
                owner.Tick(default, false, 1000f);
                DeliverHold(sender, receiver, owner, remote, false);
                Check(owner.Holding && remote.Holding && remote.Version == version, "live " + kind + " retains both peers across long tracking loss");
                owner.Tick(Fist, true);
                Check(owner.Holding && !owner.hand.WasUsePressed, "live closed recovery keeps hold without use");
                owner.Tick(default, false, 1000f); owner.Tick(default, true, .02f);
                DeliverHold(sender, receiver, owner, remote, false);
                Check(!owner.Holding && !remote.Holding, "live intentional " + kind + " release reaches peer");
                owner.CheckContents(); remote.CheckContents();
            }

            DepartHeldPeer(b, a); // Ordinary client leaves while holding a weapon and a loaded bag.
            Check(b.ConnectUsingSettings(settings), "reconnect departed test peer");
            PumpHolds(a, b, () => b.State == ClientState.ConnectedToMasterServer, "reconnect");
            Check(b.OpJoinRoom(new EnterRoomParams { RoomName = room }), "rejoin private hold room");
            PumpHolds(a, b, () => b.InRoom && a.CurrentRoom.PlayerCount == 2 && b.CurrentRoom.PlayerCount == 2, "rejoin");
            DepartHeldPeer(a, b); // The same recovery must work after server-elected master handoff.
            Debug.Log("TrackedHandInputChecks CLOUD PASS " + checks + ": two live Photon UDP peers, both holder roles/hands, production item/weapon/bag packets over reliable and unreliable delivery, 100 ms lag + 25 ms jitter + 5% loss per direction, closed/open recovery, loaded-bag/weapon cleanup on client and master departure, stale snapshot rejection. Synthetic tracking samples; not a headset test.");
        }
        finally
        {
            a.LoadBalancingPeer.IsSimulationEnabled = false; b.LoadBalancingPeer.IsSimulationEnabled = false;
            a.Disconnect(); b.Disconnect();
            for (int i = 0; i < 100 && (a.IsConnected || b.IsConnected); i++) { a.Service(); b.Service(); System.Threading.Thread.Sleep(10); }
        }
    }

    static void DepartHeldPeer(LoadBalancingClient sender, LoadBalancingClient receiver)
    {
        SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
        using var weapon = new HoldFixture("WeaponNailBat", true, true);
        using var bag = new HoldFixture("Bag", false, true);
        SelectHoldPeer(receiver.CurrentRoom, receiver.LocalPlayer);
        using var remoteWeapon = new HoldFixture("WeaponNailBat", true, false);
        using var remoteBag = new HoldFixture("Bag", false, false);
        SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
        weapon.Grab(); bag.Grab();
        weapon.Tick(default, false, 1000f); bag.Tick(default, false, 1000f);
        DeliverHold(sender, receiver, weapon, remoteWeapon);
        DeliverHold(sender, receiver, bag, remoteBag);
        object[] staleWeapon = weapon.Capture(), staleBag = bag.Capture();
        int departedActor = sender.LocalPlayer.ActorNumber;
        Player departedPlayer = receiver.CurrentRoom.GetPlayer(departedActor);
        sender.Disconnect();
        PumpHolds(sender, receiver, () => sender.State == ClientState.Disconnected && receiver.CurrentRoom.PlayerCount == 1
            && receiver.CurrentRoom.MasterClientId == receiver.LocalPlayer.ActorNumber, "holder departure");
        SelectHoldPeer(receiver.CurrentRoom, receiver.LocalPlayer);
        // The fixture hosts two object sets in one process; recover only the survivor's replicas.
        PhotonGrabbable.HeldItems.Remove(weapon.item);
        BagObject.Active.Add(remoteBag.bag);
        remoteWeapon.view.ViewID = 700002; remoteBag.view.ViewID = 700003;
        remoteWeapon.view.OwnerActorNr = remoteBag.view.OwnerActorNr = departedActor;
        var coordinator = new GameObject("hold recovery coordinator").AddComponent<PhotonMasterHandoff>();
        try
        {
            PhotonHeldItems.RecoverUnavailable(coordinator, departedActor);
            Check(!remoteWeapon.Holding && remoteWeapon.view.OwnerActorNr == 0, "live departure recovers retained weapon under survivor authority");
            Check(!remoteBag.Holding && remoteBag.view.OwnerActorNr == 0, "live departure recovers retained loaded bag under survivor authority");
            remoteWeapon.Receive(staleWeapon, departedPlayer); remoteBag.Receive(staleBag, departedPlayer);
            Check(!remoteWeapon.Holding && !remoteBag.Holding, "late pre-departure held snapshots cannot resurrect recovered objects");
            remoteBag.CheckContents();
        }
        finally { UnityEngine.Object.DestroyImmediate(coordinator.gameObject); }
    }

    static void DeliverHold(LoadBalancingClient sender, LoadBalancingClient receiver, HoldFixture owner, HoldFixture remote, bool reliable = true)
    {
        SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
        int delivery = ++holdDeliveryId;
        object[] packet = owner.Capture(), delivered = null;
        var envelope = new object[] { delivery, packet }; // Test transport correlation only; production packet is untouched.
        Action<EventData> receive = e => {
            if (e.Code == 77 && e.Sender == sender.LocalPlayer.ActorNumber && e.CustomData is object[] data && (int)data[0] == delivery)
                delivered = (object[])data[1];
        };
        receiver.EventReceived += receive;
        try
        {
            var targets = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            var options = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
            Check(sender.OpRaiseEvent(77, envelope, targets, options), "send production hold stream");
            var resend = System.Diagnostics.Stopwatch.StartNew();
            PumpHolds(sender, receiver, () => {
                if (delivered != null) return true;
                // Repeated current snapshots model a stream when individual unreliable sends are lost.
                if (!reliable && resend.ElapsedMilliseconds >= 100) {
                    Check(sender.OpRaiseEvent(77, envelope, targets, options), "repeat current unreliable snapshot");
                    resend.Restart();
                }
                return false;
            }, "hold packet");
            SelectHoldPeer(receiver.CurrentRoom, receiver.LocalPlayer);
            remote.Receive(delivered, receiver.CurrentRoom.GetPlayer(sender.LocalPlayer.ActorNumber));
        }
        finally
        {
            receiver.EventReceived -= receive;
            SelectHoldPeer(sender.CurrentRoom, sender.LocalPlayer);
        }
    }

    static void PumpHolds(LoadBalancingClient a, LoadBalancingClient b, Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            a.Service(); b.Service();
            if (timer.Elapsed.TotalSeconds > 30) throw new Exception("Hold cloud timeout: " + stage + "; " + a.State + "/" + a.DisconnectedCause + ", " + b.State + "/" + b.DisconnectedCause);
            System.Threading.Thread.Sleep(5);
        }
    }
}
