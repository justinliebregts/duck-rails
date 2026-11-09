using System;
using System.Reflection;
using ExitGames.Client.Photon;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static partial class SpiderRaiseChecks
{
    static int delivery;
    static void SelectPeer(LoadBalancingClient peer)
    {
        PhotonNetwork.NetworkingClient.CurrentRoom = peer.CurrentRoom;
        PhotonNetwork.NetworkingClient.State = ClientState.Joined;
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(PhotonNetwork.NetworkingClient, peer.LocalPlayer);
    }

    static void Pump(LoadBalancingClient[] peers, Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            foreach (var peer in peers) peer.Service();
            if (timer.Elapsed.TotalSeconds > 30) throw new Exception("Spider network timeout: " + stage);
            System.Threading.Thread.Sleep(5);
        }
        Debug.Log("SPIDER NETWORK stage PASS: " + stage);
    }

    static EnemyPosePacket Deliver(LoadBalancingClient[] peers, LoadBalancingClient source, LoadBalancingClient receiver, EnemyPosePacket packet)
    {
        EnemyPosePacket result = null;
        int sequence = ++delivery;
        Action<EventData> receive = e =>
        {
            if (e.Code == 77 && e.Sender == source.LocalPlayer.ActorNumber
                && e.CustomData is object[] data && (int)data[0] == sequence)
                result = (EnemyPosePacket)data[1];
        };
        receiver.EventReceived += receive;
        try
        {
            for (int attempt = 0; attempt < 8 && result == null; attempt++)
            {
                Check(source.OpRaiseEvent(77, new object[] { sequence, packet },
                    new RaiseEventOptions { TargetActors = new[] { receiver.LocalPlayer.ActorNumber } }, SendOptions.SendUnreliable), "Send production pose");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (result == null && timer.Elapsed.TotalSeconds < 2)
                {
                    foreach (var peer in peers) peer.Service();
                    System.Threading.Thread.Sleep(5);
                }
            }
            Check(result != null, "Impaired UDP delivered production spider pose");
            return result;
        }
        finally { receiver.EventReceived -= receive; }
    }

    static void Impair(LoadBalancingClient peer)
    {
        var sim = peer.LoadBalancingPeer.NetworkSimulationSettings;
        sim.IncomingLag = sim.OutgoingLag = 150;
        sim.IncomingJitter = sim.OutgoingJitter = 40;
        sim.IncomingLossPercentage = sim.OutgoingLossPercentage = 5;
        peer.LoadBalancingPeer.IsSimulationEnabled = true;
    }

    static void Match(Rig owner, Rig observer, int hz)
    {
        observer.Settle(hz);
        Check(observer.gait.RaiseLevel == owner.gait.RaiseLevel, "Observers use owner stance");
        Check(Mathf.Abs(observer.gait.RaiseHeight - owner.gait.RaiseHeight) < .015f, "Observer lift converges at " + hz);
        Check(Quaternion.Angle(observer.head.localRotation, owner.head.localRotation) < .15f, "Close head angle converges at " + hz);
        Check(Vector3.Distance(observer.kill.transform.TransformPoint(observer.kill.center), owner.kill.transform.TransformPoint(owner.kill.center)) < .02f,
            "Head kill volume converges at " + hz);
    }

    public static void RunCloud()
    {
        Setup();
        var original = PhotonNetwork.NetworkingClient;
        var oldRoom = original.CurrentRoom; var oldLocal = original.LocalPlayer; var oldState = original.State;
        var a = new LoadBalancingClient(ConnectionProtocol.Udp);
        var b = new LoadBalancingClient(ConnectionProtocol.Udp);
        var c = new LoadBalancingClient(ConnectionProtocol.Udp);
        var peers = new[] { a, b, c };
        Rig owner = null, observer = null, late = null;
        try
        {
            typeof(PhotonCustomTypes).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string run = Guid.NewGuid().ToString("N");
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "spider-raise-validation-" + run; settings.FixedRegion = PhotonSession.Region;
            a.AuthValues = new AuthenticationValues("spider-a-" + run);
            b.AuthValues = new AuthenticationValues("spider-b-" + run);
            c.AuthValues = new AuthenticationValues("spider-c-" + run);
            Check(a.ConnectUsingSettings(settings) && b.ConnectUsingSettings(settings), "Connect test peers");
            Pump(peers, () => a.State == ClientState.ConnectedToMasterServer && b.State == ClientState.ConnectedToMasterServer, "connect");
            string room = "spider-raise-" + run;
            Check(a.OpCreateRoom(new EnterRoomParams { RoomName = room, RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = 3, PlayerTtl = 0, EmptyRoomTtl = 0 } }), "Create hidden test room");
            Pump(peers, () => a.InRoom, "create");
            Check(b.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Join observer");
            Pump(peers, () => b.InRoom && a.CurrentRoom.PlayerCount == 2, "observer joined");
            Impair(a); Impair(b);
            int controller = a.LocalPlayer.ActorNumber;
            Target(new Vector3(0, 1.5f, .8f));
            SelectPeer(a); owner = new Rig(); owner.view.ControllerActorNr = controller; owner.Settle();
            Check(owner.enemy.HasAuthority && owner.gait.RaiseLevel == 7, "Owner computes raised posture");
            EnemyPosePacket raised = owner.Capture();
            SelectPeer(b); observer = new Rig(); observer.view.ControllerActorNr = controller;
            Check(!observer.enemy.HasAuthority, "Observer must not simulate authority");
            observer.Receive(Deliver(peers, a, b, raised), b.CurrentRoom.GetPlayer(controller), 1000);
            // Deliberately contradictory local target: only the master's level/angles count.
            Target(new Vector3(0, .1f, 100));
            foreach (int hz in new[] { 30, 72, 90, 144 }) Match(owner, observer, hz);
            var stale = new EnemyPosePacket(); stale.Set(Vector3.zero, Quaternion.identity, 0, false, 0, 0);
            observer.Receive(Deliver(peers, a, b, stale), b.CurrentRoom.GetPlayer(controller), 999); Match(owner, observer, 90);
            observer.Receive((EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(stale)), b.LocalPlayer, 2000); Match(owner, observer, 90);
            observer.Receive((EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(raised)), b.CurrentRoom.GetPlayer(controller), 1000); Match(owner, observer, 90);

            Check(c.ConnectUsingSettings(settings), "Connect late observer");
            Pump(peers, () => c.State == ClientState.ConnectedToMasterServer, "late connect");
            Check(c.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Late join");
            Pump(peers, () => c.InRoom && c.CurrentRoom.PlayerCount == 3, "late join"); Impair(c);
            SelectPeer(c); late = new Rig(); late.view.ControllerActorNr = controller;
            late.Receive(Deliver(peers, a, c, raised), c.CurrentRoom.GetPlayer(controller), 1001); Match(owner, late, 90);

            Target(new Vector3(0, 1.5f, 2)); SelectPeer(a); owner.Settle(); EnemyPosePacket lowered = owner.Capture();
            Check(lowered.SpiderRaise == 0, "Owner publishes lowering");
            SelectPeer(b); observer.Receive(Deliver(peers, a, b, lowered), b.CurrentRoom.GetPlayer(controller), 2001); Match(owner, observer, 90);
            SelectPeer(c); late.Receive(Deliver(peers, a, c, lowered), c.CurrentRoom.GetPlayer(controller), 2001); Match(owner, late, 144);

            Target(new Vector3(0, 1.5f, .8f)); SelectPeer(a); owner.Settle(); raised = owner.Capture();
            SelectPeer(b); observer.Receive(Deliver(peers, a, b, raised), b.CurrentRoom.GetPlayer(controller), 3000); Match(owner, observer, 90);
            a.Disconnect();
            Pump(peers, () => a.State == ClientState.Disconnected && b.CurrentRoom.MasterClientId == b.LocalPlayer.ActorNumber, "real master migration");
            SelectPeer(b); observer.view.ControllerActorNr = b.LocalPlayer.ActorNumber; observer.sync.OnControllerChange(b.LocalPlayer, null);
            Check(observer.gait.RaiseHeight == 0 && observer.gait.RaiseLevel == 0, "Handoff discards old rise state");
            observer.enemy.ResetBrain(); observer.enemy.SetRemoteState(Enemy.State.Chase); Set(observer.enemy, "_target", 0); observer.Settle();
            Check(observer.enemy.HasAuthority && observer.gait.RaiseLevel == 7, "New owner recomputes posture");
            EnemyPosePacket migrated = observer.Capture();
            SelectPeer(c); late.view.ControllerActorNr = b.LocalPlayer.ActorNumber; late.sync.OnControllerChange(c.CurrentRoom.GetPlayer(b.LocalPlayer.ActorNumber), null);
            late.Receive(Deliver(peers, b, c, migrated), c.CurrentRoom.GetPlayer(b.LocalPlayer.ActorNumber), 4000); Match(observer, late, 90);
            late.Receive((EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(lowered)), a.LocalPlayer, 5000); Match(observer, late, 90);
            Debug.Log($"SpiderRaiseChecks CLOUD PASS: {checks} assertions; three live UDP clients; 150ms lag each direction, 40ms jitter, 5% configured loss; rise/lower, close head/kill alignment, 30/72/90/144Hz, late join, stale/duplicate/wrong-owner packets, real master migration; unchanged 22-byte pose.");
        }
        finally
        {
            owner?.Dispose(); observer?.Dispose(); late?.Dispose();
            foreach (var peer in peers) { peer.LoadBalancingPeer.IsSimulationEnabled = false; peer.Disconnect(); }
            for (int i = 0; i < 100; i++) { foreach (var peer in peers) peer.Service(); System.Threading.Thread.Sleep(5); }
            original.CurrentRoom = oldRoom; original.State = oldState;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(original, oldLocal);
        }
    }
}
