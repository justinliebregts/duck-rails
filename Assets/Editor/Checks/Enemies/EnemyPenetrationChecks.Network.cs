using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EnemyPenetrationChecks
{
    static int deliverySequence;
    sealed class NetActor : IDisposable
    {
        public readonly Enemy enemy;
        public readonly EnemyNetSync sync;
        public readonly PhotonView view;
        public readonly GroundLocomotion ground;
        public readonly ClimbLocomotion climb;
        public NetActor(string type, int controller)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie" + type + ".prefab");
            var go = Object.Instantiate(prefab); go.SetActive(false);
            enemy = go.GetComponent<Enemy>(); sync = go.GetComponent<EnemyNetSync>(); view = go.GetComponent<PhotonView>();
            ground = go.GetComponent<GroundLocomotion>(); climb = go.GetComponent<ClimbLocomotion>();
            Call(enemy, "Awake"); Call(sync, "Awake");
            var jump = go.GetComponent<ZombieSmasherJump>(); if (jump != null) Call(jump, "Awake");
            view.OwnerActorNr = 0; view.ControllerActorNr = controller;
            enemy.Teleport(Vector3.down * .08f, Quaternion.identity);
        }
        public Collider Helper => ground != null ? Get<CapsuleCollider>(ground, "_queryCapsule") : Get<SphereCollider>(climb, "_querySphere");
        public bool Overlap() => ground != null ? ground.Overlaps(enemy.Root.position, Mask) : climb.Overlaps(enemy.Root.position, Mask);
        public void Repair(EnemyManager manager)
        {
            Check(enemy.HasAuthority, "Only the authority may run the recovery fixture");
            enemy.Teleport(Vector3.down * .08f, Quaternion.identity);
            Set(enemy, "_hadAuthority", true);
            Set(enemy, "_retreatActive", true); Set(enemy, "_retreatRemaining", 2f);
            Physics.SyncTransforms();
            enemy.TickAuthority(manager, 1f / 90, false);
            Check(!Overlap() && Mathf.Abs(enemy.Root.position.y) < .005f, "Authority must repair actual prefab overlap");
            Check(Helper != null && !Helper.enabled, "Authority recovery retires its helper");
        }
        public EnemyPosePacket Capture()
        {
            var stream = new PhotonStream(true, null);
            Set(stream, "writeData", new List<object>(1));
            sync.OnPhotonSerializeView(stream, default);
            return (EnemyPosePacket)stream.ToArray()[0];
        }
        public void Receive(EnemyPosePacket packet, Player sender, int timestamp)
            => sync.OnPhotonSerializeView(new PhotonStream(false, new object[] { packet }), new PhotonMessageInfo(sender, timestamp, view));
        public void Settle(Vector3 expected, int hz)
        {
            Check(!enemy.HasAuthority, "Receiver must stay non-authoritative");
            for (int i = 0; i < hz * 2; i++) enemy.ApplyRemote(1f / hz);
            Check(Vector3.Distance(enemy.Root.position, expected) < .001f, "Remote must converge to the corrected pose at " + hz + " Hz");
            var body = enemy.Root.GetComponent<Rigidbody>();
            Check(body == null || (body.isKinematic && Vector3.Distance(body.position, enemy.Root.position) < .001f), "Remote rigidbody and transform agree");
        }
        public void Dispose()
        {
            Call((object)ground ?? climb, "OnDestroy");
            Object.DestroyImmediate(enemy.gameObject);
        }
    }

    static EnemyPosePacket CopyPacket(EnemyPosePacket packet)
        => (EnemyPosePacket)EnemyPosePacket.Deserialize((byte[])EnemyPosePacket.Serialize(packet).Clone());

    // Actual UDP service traffic in a unique, hidden room/app version. Production
    // capture, custom packet codec, receive, interpolation and recovery all run.
    public static void RunCloud()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = PhotonNetwork.NetworkingClient;
        var oldRoom = root.CurrentRoom; var oldLocal = root.LocalPlayer; var oldState = root.State;
        var a = new LoadBalancingClient(ConnectionProtocol.Udp);
        var b = new LoadBalancingClient(ConnectionProtocol.Udp);
        var owners = new List<NetActor>(); var observers = new List<NetActor>();
        Mesh mesh = FloorMesh();
        var floor = new GameObject("Network recovery floor") { layer = 6 };
        floor.AddComponent<MeshCollider>().sharedMesh = mesh;
        var managerObject = new GameObject("Network recovery manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>(); Set(manager, "surfaceMask", (LayerMask)Mask);
        try
        {
            typeof(PhotonCustomTypes).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string run = Guid.NewGuid().ToString("N");
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "recovery-validation-" + run; settings.FixedRegion = PhotonSession.Region;
            a.AuthValues = new AuthenticationValues("recovery-a-" + run);
            b.AuthValues = new AuthenticationValues("recovery-b-" + run);
            Check(a.ConnectUsingSettings(settings) && b.ConnectUsingSettings(settings), "Start private recovery clients");
            PumpRecovery(a, b, () => a.State == ClientState.ConnectedToMasterServer && b.State == ClientState.ConnectedToMasterServer, "connect");
            string room = "recovery-" + run;
            Check(a.OpCreateRoom(new EnterRoomParams { RoomName = room, RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = 2, PlayerTtl = 0, EmptyRoomTtl = 0 } }), "Create hidden recovery room");
            PumpRecovery(a, b, () => a.InRoom, "create");
            Check(b.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Join recovery observer");
            PumpRecovery(a, b, () => b.InRoom && a.CurrentRoom.PlayerCount == 2, "join");
            foreach (var peer in new[] { a.LoadBalancingPeer, b.LoadBalancingPeer })
            {
                peer.NetworkSimulationSettings.IncomingLag = 150; peer.NetworkSimulationSettings.OutgoingLag = 150;
                peer.NetworkSimulationSettings.IncomingJitter = 40; peer.NetworkSimulationSettings.OutgoingJitter = 40;
                peer.NetworkSimulationSettings.IncomingLossPercentage = 5; peer.NetworkSimulationSettings.OutgoingLossPercentage = 5;
                peer.IsSimulationEnabled = true;
            }
            int originalController = a.LocalPlayer.ActorNumber;
            foreach (string type in Types)
            {
                SelectRecoveryPeer(a);
                var owner = new NetActor(type, originalController); owners.Add(owner);
                owner.Repair(manager);
                Vector3 expected = owner.enemy.Root.position;
                EnemyPosePacket packet = owner.Capture();
                Check(EnemyPosePacket.Serialize(packet).Length == EnemyPosePacket.PacketBytes, "Recovery must retain the existing 22-byte pose");
                SelectRecoveryPeer(b);
                var observer = new NetActor(type, originalController); observers.Add(observer);
                observer.enemy.ApplyRemote(.5f);
                Check(observer.Helper == null && observer.enemy.Root.position.y < -.07f, "An observer without a sample must not run local repair");
                EnemyPosePacket delivered = DeliverRecovery(a, b, packet, false);
                observer.Receive(delivered, b.CurrentRoom.GetPlayer(originalController), 1000);
                foreach (int hz in new[] { 30, 90, 144 })
                {
                    observer.enemy.SetRootPose(Vector3.down * .08f, Quaternion.identity);
                    observer.Settle(expected, hz);
                }
                Check(observer.Helper == null, "Network interpolation must not create a recovery collider");
                // Delayed and duplicated packets must not undo the newer correction.
                var stale = new EnemyPosePacket(); stale.Set(Vector3.down * .08f, Quaternion.identity, 0, false, 0, 0);
                observer.Receive(DeliverRecovery(a, b, stale, false), b.CurrentRoom.GetPlayer(originalController), 999);
                observer.Settle(expected, 90);
                observer.Receive(CopyPacket(packet), b.CurrentRoom.GetPlayer(originalController), 1000);
                observer.Settle(expected, 90);
                Debug.Log("RECOVERY CLOUD corrected/stale/duplicate PASS " + type);
            }
            // A real server master change: the observer must now repair and publish.
            a.Disconnect();
            PumpRecovery(a, b, () => a.State == ClientState.Disconnected && b.CurrentRoom.MasterClientId == b.LocalPlayer.ActorNumber, "master departure/election");
            SelectRecoveryPeer(b);
            foreach (var observer in observers)
            {
                Player previous = observer.view.Controller;
                observer.view.ControllerActorNr = b.LocalPlayer.ActorNumber;
                observer.sync.OnControllerChange(b.LocalPlayer, previous);
                Check(!Get<bool>(observer.enemy, "_hadAuthority") && !Get<bool>(observer.sync, "_hasSample"), "Handoff clears old authority and samples");
                observer.Repair(manager);
            }
            Check(a.ConnectUsingSettings(settings), "Reconnect old master as observer");
            PumpRecovery(a, b, () => a.State == ClientState.ConnectedToMasterServer, "reconnect");
            Check(a.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Rejoin as observer");
            PumpRecovery(a, b, () => a.InRoom && a.CurrentRoom.PlayerCount == 2, "rejoin");
            for (int i = 0; i < Types.Length; i++)
            {
                SelectRecoveryPeer(b); EnemyPosePacket packet = observers[i].Capture();
                Vector3 expected = observers[i].enemy.Root.position;
                EnemyPosePacket delivered = DeliverRecovery(b, a, packet, true);
                SelectRecoveryPeer(a);
                Player previous = owners[i].view.Controller;
                owners[i].view.ControllerActorNr = b.LocalPlayer.ActorNumber;
                owners[i].sync.OnControllerChange(a.CurrentRoom.GetPlayer(b.LocalPlayer.ActorNumber), previous);
                owners[i].enemy.SetRootPose(Vector3.down * .08f, Quaternion.identity);
                owners[i].Receive(delivered, a.CurrentRoom.GetPlayer(b.LocalPlayer.ActorNumber), 2000);
                owners[i].Settle(expected, 90);
                Check(!owners[i].Helper.enabled, "Former authority must leave its old helper disabled");
                var wrong = new EnemyPosePacket(); wrong.Set(Vector3.down, Quaternion.identity, 0, false, 0, 0);
                owners[i].Receive(wrong, a.LocalPlayer, 3000);
                owners[i].Settle(expected, 90);
            }
            Debug.Log("EnemyPenetrationChecks CLOUD: ALL PASSED (four production enemies; two live UDP peers; 150ms lag each direction, 40ms jitter, 5% configured loss; corrected poses, stale/duplicate/wrong-controller rejection, 30/90/144Hz interpolation, real master election and rejoin; unchanged 22-byte pose)");
        }
        finally
        {
            foreach (var actor in owners) actor.Dispose(); foreach (var actor in observers) actor.Dispose();
            a.LoadBalancingPeer.IsSimulationEnabled = false; b.LoadBalancingPeer.IsSimulationEnabled = false;
            a.Disconnect(); b.Disconnect();
            for (int i = 0; i < 100 && (a.IsConnected || b.IsConnected); i++) { a.Service(); b.Service(); System.Threading.Thread.Sleep(10); }
            root.CurrentRoom = oldRoom; root.State = oldState;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(root, oldLocal);
            Object.DestroyImmediate(managerObject); Object.DestroyImmediate(floor); Object.DestroyImmediate(mesh);
        }
    }

    static void SelectRecoveryPeer(LoadBalancingClient peer)
    {
        PhotonNetwork.NetworkingClient.CurrentRoom = peer.CurrentRoom;
        PhotonNetwork.NetworkingClient.State = ClientState.Joined;
        typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(PhotonNetwork.NetworkingClient, peer.LocalPlayer);
    }

    static EnemyPosePacket DeliverRecovery(LoadBalancingClient source, LoadBalancingClient receiver, EnemyPosePacket packet, bool reliable)
    {
        EnemyPosePacket delivered = null;
        int sequence = ++deliverySequence;
        Action<EventData> receive = e => {
            if (e.Code == 77 && e.Sender == source.LocalPlayer.ActorNumber
                && e.CustomData is object[] data && (int)data[0] == sequence) delivered = (EnemyPosePacket)data[1];
        };
        receiver.EventReceived += receive;
        try
        {
            // A dropped unreliable sample is followed by the next pose, as in PUN.
            for (int attempt = 0; attempt < 8 && delivered == null; attempt++)
            {
                Check(source.OpRaiseEvent(77, new object[] { sequence, packet }, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, new SendOptions { Reliability = reliable }), "Send production enemy packet");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (delivered == null && timer.Elapsed.TotalSeconds < 2) { source.Service(); receiver.Service(); System.Threading.Thread.Sleep(5); }
            }
            Check(delivered != null, "Deliver enemy pose through impaired UDP");
            return delivered;
        }
        finally { receiver.EventReceived -= receive; }
    }

    static void PumpRecovery(LoadBalancingClient a, LoadBalancingClient b, Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            a.Service(); b.Service();
            if (timer.Elapsed.TotalSeconds > 30) throw new Exception("Recovery cloud timeout: " + stage + "; " + a.State + "/" + a.DisconnectedCause + ", " + b.State + "/" + b.DisconnectedCause);
            System.Threading.Thread.Sleep(5);
        }
        Debug.Log("RECOVERY CLOUD stage PASS " + stage);
    }
}
