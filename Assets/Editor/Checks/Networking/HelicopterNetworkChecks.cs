using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Synthetic peers exercise production capture, wire codec, receive and render paths.
// No Photon service connection is made. Run in a batch editor with no unsaved scene.
public static class HelicopterNetworkChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo NetworkFrame = typeof(PhotonNetwork).GetField("frame", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo NetworkTime = typeof(PhotonNetwork).GetField("frametime", BindingFlags.Static | BindingFlags.NonPublic);
    static int checks;
    static int cloudSequence;
    sealed class Actor : Photon.Realtime.Player { public Actor(int id) : base("Cabin test " + id, id, false) { } }
    static void Set(object o, string n, object value) => o.GetType().GetField(n, P).SetValue(o, value);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static Action Bind(object o, string n) => (Action)Delegate.CreateDelegate(typeof(Action), o, o.GetType().GetMethod(n, P));
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < 0.006f, message + ": " + a + " vs " + b);
    static void Clock(double now) { NetworkFrame.SetValue(null, Time.frameCount); NetworkTime.SetValue(null, now); }

    sealed class Peer : IDisposable
    {
        public readonly HelicopterManager manager;
        public readonly Helicopter cabin;
        public readonly BoxCollider roof;
        public readonly PhotonPlayer avatar;
        public readonly Action render;
        public Peer(Actor owner, bool local)
        {
            var cabinGo = new GameObject("Network cabin");
            cabin = cabinGo.AddComponent<Helicopter>();
            var box = cabinGo.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = Vector3.up; box.size = new Vector3(3f, 2f, 4f);
            Set(cabin, "passengerCabin", box);
            var roofGo = new GameObject("Network roof"); roofGo.layer = 6; roofGo.transform.SetParent(cabinGo.transform, false);
            roof = roofGo.AddComponent<BoxCollider>(); roof.center = Vector3.up * 2.9f; roof.size = new Vector3(4, .2f, 6);
            manager = new GameObject("Network cabin manager").AddComponent<HelicopterManager>();
            manager.stayForever = false;
            Set(manager, "helicopter", cabin);
            Set(manager, "hasCabinPose", true);
            Set(manager, "cabinArrivalSerial", 1);
            Set(manager, "cabinLandingIndex", 0);
            Set(manager, "<ArrivalSerial>k__BackingField", 1);
            var phase = typeof(HelicopterManager).GetField("phase", P);
            phase.SetValue(manager, Enum.ToObject(phase.FieldType, 3));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
            var go = Object.Instantiate(prefab); go.SetActive(false);
            avatar = go.GetComponent<PhotonPlayer>();
            avatar.photonView.OwnerActorNr = owner.ActorNumber;
            avatar.photonView.ControllerActorNr = owner.ActorNumber;
            typeof(PhotonView).GetProperty("IsMine").SetValue(avatar.photonView, local);
            Set(avatar, "deathRagdollPrefab", null);
            foreach (var limb in go.GetComponentsInChildren<TwoBoneLimb>(true)) limb.CaptureRest();
            render = Bind(avatar, "LateUpdate");
        }
        public void Dispose()
        {
            Object.DestroyImmediate(avatar.gameObject);
            Object.DestroyImmediate(cabin.gameObject);
            Object.DestroyImmediate(manager.gameObject);
        }
    }

    sealed class Rig : IDisposable
    {
        public readonly GR.Player player;
        public readonly Transform head, left, right;
        public readonly NewPlayer vr;
        public readonly DesktopPlayer desktop;
        public readonly float headHeight;
        public Rig(bool useDesktop)
        {
            var go = new GameObject(useDesktop ? "Local desktop" : "Local VR");
            player = go.AddComponent<GR.Player>();
            GR.Player.instance = player;
            head = Anchor(go.transform, "Head", Vector3.up * (useDesktop ? 1.55f : 0.4f));
            left = Anchor(go.transform, "Left hand", head.localPosition + new Vector3(-0.3f, -0.2f, 0.2f));
            right = Anchor(go.transform, "Right hand", head.localPosition + new Vector3(0.3f, -0.2f, 0.2f));
            if (useDesktop)
            {
                desktop = go.AddComponent<DesktopPlayer>();
                var cc = go.AddComponent<CharacterController>(); cc.height = 1.7f; cc.center = Vector3.up * 0.85f;
                Set(desktop, "_controller", cc);
                go.transform.position = Vector3.up * 0.05f;
                headHeight = 1.6f;
            }
            else
            {
                vr = go.AddComponent<NewPlayer>();
                vr.rb = go.AddComponent<Rigidbody>(); vr.rb.useGravity = false;
                vr.bodyMidpoint = go.transform;
                vr.bodyCollider = go.AddComponent<CapsuleCollider>(); vr.bodyCollider.height = 1; vr.bodyCollider.radius = .25f;
                vr.headCollider = head.gameObject.AddComponent<SphereCollider>();
                vr.leftHandTransform = left; vr.rightHandTransform = right;
                vr.velocityHistorySize = 8;
                go.transform.position = Vector3.up * 0.6f;
                vr.InitializeValues();
                headHeight = 1f;
            }
        }
        static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        public void Warp(Vector3 position)
        {
            if (desktop != null) desktop.Teleport(position, Quaternion.identity);
            else vr.Warp(position);
        }
        public void Move(Vector3 delta)
        {
            if (desktop != null) desktop.ApplyPassengerDisplacement(delta);
            else vr.ApplyPassengerDisplacement(delta);
        }
        public void Dispose() => Object.DestroyImmediate(player.gameObject);
    }

    sealed class Delivery { public double sent, at; public object[] values; }

    static object[] Capture(PhotonPlayer player, bool roundTrip = true)
    {
        var stream = new PhotonStream(true, null);
        Set(stream, "writeData", new List<object>(5));
        player.OnPhotonSerializeView(stream, default);
        object[] values = stream.ToArray();
        if (!roundTrip) return values;
        // Run the actual compact wire codec, not just an in-memory pose copy.
        var wire = new StreamBuffer(128);
        short length = PosePacket.SerializeStream(wire, values[0]);
        wire.Position = 0;
        values[0] = PosePacket.DeserializeStream(wire, length);
        return values;
    }

    static void Deliver(Peer receiver, Photon.Realtime.Player sender, object[] values, double sent, double now)
    {
        Clock(now);
        receiver.avatar.OnPhotonSerializeView(new PhotonStream(false, values),
            new PhotonMessageInfo(sender, unchecked((int)(uint)(long)Math.Round(sent * 1000)), receiver.avatar.photonView));
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        GR.Player oldPlayer = GR.Player.instance;
        HelicopterManager oldManager = HelicopterManager.instance;
        var oldRoom = PhotonNetwork.NetworkingClient.CurrentRoom;
        try
        {
            var owner = new Actor(2);
            var room = new Room("Cabin regression", new RoomOptions()); room.AddPlayer(owner);
            PhotonNetwork.NetworkingClient.CurrentRoom = room;
            foreach (bool desktop in new[] { false, true })
                foreach (double latency in new[] { 0.08, 0.25, 0.45 })
                {
                    Playback(owner, desktop, latency);
                    Playback(owner, desktop, latency, 1);
                    if (!desktop) Playback(owner, false, latency, 2);
                }
            Codec();
            Debug.Log("HelicopterNetworkChecks: ALL PASSED (" + checks + " assertions; simulated peers, actual Photon stream and codec)");
        }
        finally
        {
            GR.Player.instance = oldPlayer;
            HelicopterManager.instance = oldManager;
            PhotonNetwork.NetworkingClient.CurrentRoom = oldRoom;
        }
    }

    // support: 0 = cabin volume, 1 = body on roof, 2 = one VR hand with body outside roof.
    static void Playback(Actor owner, bool desktop, double latency, int support = 0)
    {
        using var rig = new Rig(desktop);
        using var sender = new Peer(owner, true);
        using var receiver = new Peer(owner, false);
        float minimumHead = rig.headHeight;
        if (support != 0)
        {
            rig.Move(new Vector3(support == 2 ? 2.2f : 0, desktop ? 3.01f : 3.51f, 0) - rig.player.transform.position);
            minimumHead = rig.head.position.y;
        }
        receiver.cabin.transform.position = Vector3.right * 100;
        Physics.SyncTransforms();
        Set(sender.avatar, "headSource", rig.head); Set(sender.avatar, "lHandSource", rig.left); Set(sender.avatar, "rHandSource", rig.right);
        // Replicate real manager identity/timers before receiving player snapshots.
        var state = new PhotonStream(true, null); Set(state, "writeData", new List<object>(6));
        sender.manager.OnPhotonSerializeView(state, default);
        receiver.manager.OnPhotonSerializeView(new PhotonStream(false, state.ToArray()), default);
        var queue = new List<Delivery>();
        int packet = 0;
        object[] last = null;
        double lastSent = 0;
        float worstDeficit = 0f;
        float previousJump = 0f;
        for (int step = 0; step <= 1200; step++)
        {
            double t = step / 120d, now = 100d + t;
            GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
            if (support == 2)
            {
                rig.vr.wasLeftHandTouching = step % 120 < 60;
                rig.vr.wasRightHandTouching = !rig.vr.wasLeftHandTouching;
                sender.cabin.ReportHandSupport(rig.player.transform,
                    rig.vr.wasLeftHandTouching ? sender.roof : null, rig.vr.wasRightHandTouching ? sender.roof : null);
            }
            sender.cabin.MoveCabin(new Vector3(0f, (float)t * 3f, 0f), Quaternion.Euler(2f * Mathf.Sin((float)t), 12f, 2f), true);
            float hopDuration = support == 1 ? .85f : .6f;
            float hopHeight = support == 1 ? .8f : .35f;
            float jump = support != 2 && t >= 2 && t <= 2 + hopDuration ? hopHeight * Mathf.Sin((float)(t - 2) / hopDuration * Mathf.PI) : 0f;
            rig.Move(Vector3.up * (jump - previousJump)); previousJump = jump;
            sender.cabin.RefreshPassenger();
            if (step % 6 == 0)
            {
                last = Capture(sender.avatar); lastSent = now;
                Check(((PosePacket)last[0]).CabinArrival == 1, "Owner captures passenger frame for " + (desktop ? "desktop" : "VR"));
                // Packet loss and an outage longer than the pose buffer reset
                // threshold, plus jitter that reorders packets.
                if (latency < 0.2 || (packet % 7 != 0 && !(t > 4 && t < 5.3)))
                    queue.Add(new Delivery { sent = now, at = now + latency + (latency < 0.2 ? 0 : 0.08 * Math.Sin(packet * 2.3)), values = last });
                packet++;
            }
            Vector3 ownerBeforePlayback = rig.player.transform.position;
            GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
            // Deliberately different helicopter animation timing/sway on this peer.
            receiver.cabin.MoveCabin(new Vector3(100f, (float)(t + 0.12) * 3f, 0f), Quaternion.Euler(-1f, 12f, -2f), true);
            if (latency > 0.4 && t > 6 && t < 6.8) continue; // Receiver stalls; queued packets drain on resume.
            for (int i = 0; i < queue.Count;)
            {
                if (queue[i].at > now) { i++; continue; }
                Deliver(receiver, owner, queue[i].values, queue[i].sent, now);
                queue.RemoveAt(i);
            }
            Clock(now); receiver.render();
            if (Get<bool>(receiver.avatar, "_hasPose"))
            {
                var pose = Get<BodyPose>(receiver.avatar, "_lastPose");
                Vector3 local = receiver.cabin.transform.InverseTransformPoint(pose.headPos);
                worstDeficit = Mathf.Max(worstDeficit, minimumHead - local.y);
                Check(local.y >= minimumHead - 0.02f, "Remote passenger sank at support " + support + ", latency " + latency + ", step " + step + ": " + local.y);
                var body = Get<RemotePlayerBody>(receiver.avatar, "body");
                Vector3 eyeOffset = Get<Unity.Mathematics.float3>(body, "eyeOffset");
                Near(body.Head.position + body.Head.rotation * eyeOffset, pose.headPos, "Resolved pose reaches rendered eyes");
            }
            Near(rig.player.transform.position, ownerBeforePlayback, "Remote playback never moves local rig");
        }

        // The old controller must not tag a replacement/missing local rig before
        // the next manager update has had a chance to refresh its cached sources.
        HelicopterManager.instance = sender.manager; GR.Player.instance = null;
        Check(((PosePacket)Capture(sender.avatar)[0]).CabinArrival == 0, "Stale local rig cannot supply passenger metadata");
        HelicopterManager.instance = receiver.manager;

        // A late joiner with just one packet must follow the cabin between packets.
        Bind(receiver.avatar, "OnDisable")();
        Deliver(receiver, owner, last, lastSent, 110.1);
        receiver.cabin.MoveCabin(Vector3.up * 33f, Quaternion.identity, true);
        Clock(110.2); receiver.render();
        Check(receiver.cabin.transform.InverseTransformPoint(Get<BodyPose>(receiver.avatar, "_lastPose").headPos).y > minimumHead - 0.02f,
            "Single late-join snapshot follows moving cabin");

        // A manager packet arriving later than its passenger must recover without
        // another avatar packet. Wrong arrivals/pads must never attach to a new visit.
        Set(receiver.manager, "hasCabinPose", false);
        receiver.render();
        Near(Get<BodyPose>(receiver.avatar, "_lastPose").headPos, ((PosePacket)last[0]).HeadPos, "Missing cabin uses world fallback");
        Set(receiver.manager, "hasCabinPose", true);
        receiver.render();
        Check(Get<int>(receiver.avatar, "_renderCabinArrival") == 1, "Delayed manager state resolves existing passenger sample");
        for (int i = 0; i < 50; i++) receiver.render();
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) receiver.render();
        Check(GC.GetAllocatedBytesForCurrentThread() == allocationBefore, "Passenger avatar playback allocates zero managed bytes");
        Set(receiver.manager, "<ArrivalSerial>k__BackingField", 2);
        Set(receiver.manager, "cabinArrivalSerial", 2);
        receiver.cabin.MoveCabin(new Vector3(100f, 0f, 0f), Quaternion.identity, false);
        receiver.render();
        Near(Get<BodyPose>(receiver.avatar, "_lastPose").headPos, ((PosePacket)last[0]).HeadPos, "Old passenger never attaches to new arrival");
        Set(receiver.manager, "<ArrivalSerial>k__BackingField", 1); Set(receiver.manager, "cabinArrivalSerial", 1);
        receiver.cabin.MoveCabin(Vector3.up * 30f, Quaternion.identity, false);
        receiver.manager.OnMasterClientSwitched(owner);
        receiver.render();
        Check(Get<int>(receiver.avatar, "_renderCabinArrival") == 1, "Host switch retains visit identity and carry");

        // Ghost snapshots retain cabin metadata; respawn strips it immediately.
        GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
        Set(rig.player, "isDead", true);
        object[] dead = Capture(sender.avatar);
        Check((bool)dead[1] && ((PosePacket)dead[0]).CabinArrival == 1, "Ghost keeps cabin in wire snapshot");
        GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
        Deliver(receiver, owner, dead, 110.3, 110.4);
        receiver.cabin.MoveCabin(Vector3.up * 31f, Quaternion.identity, true);
        Clock(110.5); receiver.render();
        Check(Get<int>(receiver.avatar, "_renderCabinArrival") == 1, "Remote hidden ghost follows cabin");
        GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
        rig.Warp(new Vector3(40f, 2f, 0f)); Set(rig.player, "isDead", false);
        object[] respawn = Capture(sender.avatar);
        Check(((PosePacket)respawn[0]).CabinArrival == 0 && PosePacket.Serialize(respawn[0]).Length == 39, "Respawn restores base world packet");
        GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
        Deliver(receiver, owner, respawn, 110.6, 110.7);
        Near(Get<BodyPose>(receiver.avatar, "_lastPose").headPos, ((PosePacket)respawn[0]).HeadPos, "Remote respawn applies immediately in world space");
        Deliver(receiver, owner, dead, 110.3, 110.8);
        Check(!receiver.avatar.IsDead && Get<int>(receiver.avatar, "_renderCabinArrival") == 0, "Reordered old death cannot reattach or resurrect ghost");

        // Reboard after respawn, then walk through the door while alive. This
        // exercises the interpolation boundary without a death-state buffer reset.
        GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
        Set(sender.cabin, "releasedFrame", -1);
        if (rig.vr != null) { rig.vr.wasLeftHandTouching = rig.vr.wasRightHandTouching = false; sender.cabin.ReportHandSupport(rig.player.transform, null, null); }
        rig.Move(sender.cabin.transform.position + Vector3.up * (desktop ? 0.05f : 0.6f) - rig.player.transform.position);
        sender.cabin.MoveCabin(Vector3.up * 30f, Quaternion.identity, true);
        object[] boarded = Capture(sender.avatar);
        Check(((PosePacket)boarded[0]).CabinArrival == 1, "Reboarding restores passenger packet");
        GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
        for (int i = 0; i < 10; i++) { Deliver(receiver, owner, boarded, 111 + i * 0.05, 111.08 + i * 0.05); receiver.render(); }
        Check(Get<int>(receiver.avatar, "_renderCabinArrival") == 1, "Remote interpolation reboards");
        GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
        rig.Move(Vector3.right * 3f);
        object[] exited = Capture(sender.avatar);
        Check(((PosePacket)exited[0]).CabinArrival == 0, "Door exit clears wire metadata immediately after movement");
        GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
        for (int i = 0; i < 10; i++) { Deliver(receiver, owner, exited, 112 + i * 0.05, 112.08 + i * 0.05); receiver.render(); }
        Check(Get<int>(receiver.avatar, "_renderCabinArrival") == 0, "Remote interpolation leaves cabin without ghost/death reset");
        receiver.cabin.MoveCabin(Vector3.up * 35f, Quaternion.identity, true);
        Clock(112.6); receiver.render();
        Near(Get<BodyPose>(receiver.avatar, "_lastPose").headPos, ((PosePacket)exited[0]).HeadPos, "Remote exited player is not dragged by next cabin movement");
        Debug.Log($"Carry network PASS: {(desktop ? "desktop" : "VR")}, support {support}, {latency * 1000:F0} ms latency, max head deficit {worstDeficit:F4} m; jitter/loss, late join, manager delay, host switch, ghost/respawn");
    }

    static void Codec()
    {
        var packet = new PosePacket { HeadPos = Vector3.up, HeadRot = Quaternion.identity, LHandRot = Quaternion.identity, RHandRot = Quaternion.identity,
            CabinArrival = 1234567, CabinLanding = 42, CabinPosition = new Vector3(30, 70, 10), CabinRotation = Quaternion.Euler(2, 45, 3) };
        object boxed = packet;
        var wire = new StreamBuffer(128);
        Check(PosePacket.Serialize(boxed).Length == 63, "Only passengers add 24 bytes");
        short length = PosePacket.SerializeStream(wire, boxed); wire.Position = 0;
        var decoded = (PosePacket)PosePacket.DeserializeStream(wire, length);
        Check(decoded.CabinArrival == packet.CabinArrival && decoded.CabinLanding == 42, "Visit identity survives binary codec");
        Near(decoded.CabinPosition, packet.CabinPosition, "Cabin position codec");
        Check(Quaternion.Angle(decoded.CabinRotation, packet.CabinRotation) < 0.2f, "Cabin rotation codec");
        packet.CabinArrival = 0; object walking = packet;
        wire.Position = 0; length = PosePacket.SerializeStream(wire, walking); wire.Position = 0;
        Check(length == 39 && ((PosePacket)PosePacket.DeserializeStream(wire, length)).CabinArrival == 0, "Short packet never inherits stale shared-buffer cabin metadata");
        for (int i = 0; i < 50; i++) { wire.Position = 0; PosePacket.SerializeStream(wire, boxed); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { wire.Position = 0; PosePacket.SerializeStream(wire, boxed); }
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "Passenger stream encoding has no new allocation after existing packet boxing");
    }

    // Opt-in real network test, isolated from game rooms by a unique app version
    // and a hidden two-client room. Uses two Realtime clients in one batch editor.
    public static void RunCloud()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        var a = new LoadBalancingClient(ConnectionProtocol.Udp);
        var b = new LoadBalancingClient(ConnectionProtocol.Udp);
        GR.Player oldPlayer = GR.Player.instance;
        HelicopterManager oldManager = HelicopterManager.instance;
        var oldRoom = PhotonNetwork.NetworkingClient.CurrentRoom;
        try
        {
            typeof(PhotonCustomTypes).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string run = Guid.NewGuid().ToString("N");
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "cabin-validation-" + run;
            settings.FixedRegion = PhotonSession.Region;
            a.AuthValues = new AuthenticationValues("cabin-a-" + run);
            b.AuthValues = new AuthenticationValues("cabin-b-" + run);
            Check(a.ConnectUsingSettings(settings) && b.ConnectUsingSettings(settings), "Start isolated Photon connections");
            PumpUntil(a, b, () => a.State == ClientState.ConnectedToMasterServer && b.State == ClientState.ConnectedToMasterServer, "connect to Photon master");
            string room = "cabin-" + run;
            Check(a.OpCreateRoom(new EnterRoomParams { RoomName = room, RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = 2, PlayerTtl = 0, EmptyRoomTtl = 0 } }), "Create private test room");
            PumpUntil(a, b, () => a.InRoom, "create test room");
            Check(b.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Join private test room");
            PumpUntil(a, b, () => a.InRoom && b.InRoom && b.CurrentRoom.PlayerCount == 2, "join test peers");
            CloudPlayback(a, b, false);
            CloudPlayback(a, b, false, 2);

            // Real server master change, followed by rejoining with a new actor ID.
            a.Disconnect();
            PumpUntil(a, b, () => a.State == ClientState.Disconnected && b.CurrentRoom.MasterClientId == b.LocalPlayer.ActorNumber, "transfer room master");
            Check(a.ConnectUsingSettings(settings), "Reconnect test sender");
            PumpUntil(a, b, () => a.State == ClientState.ConnectedToMasterServer, "reconnect sender");
            Check(a.OpJoinRoom(new EnterRoomParams { RoomName = room }), "Rejoin after master switch");
            PumpUntil(a, b, () => a.InRoom && b.CurrentRoom.PlayerCount == 2, "restore two peers");
            CloudPlayback(a, b, true);
            CloudPlayback(a, b, true, 1);
            Debug.Log("HelicopterNetworkChecks CLOUD: ALL PASSED (two live UDP clients, VR then desktop, real server host transfer/rejoin; production capture/codec/receive/render)");
        }
        finally
        {
            a.Disconnect(); b.Disconnect();
            for (int i = 0; i < 100 && (a.IsConnected || b.IsConnected); i++) { a.Service(); b.Service(); System.Threading.Thread.Sleep(10); }
            GR.Player.instance = oldPlayer;
            HelicopterManager.instance = oldManager;
            PhotonNetwork.NetworkingClient.CurrentRoom = oldRoom;
        }
    }

    static void PumpUntil(LoadBalancingClient a, LoadBalancingClient b, Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            a.Service(); b.Service();
            if (timer.Elapsed.TotalSeconds > 30)
                throw new Exception("Photon timeout: " + stage + "; " + a.State + "/" + a.DisconnectedCause + ", " + b.State + "/" + b.DisconnectedCause);
            System.Threading.Thread.Sleep(5);
        }
        Debug.Log("Cloud stage PASS: " + stage);
    }

    static void CloudPlayback(LoadBalancingClient a, LoadBalancingClient b, bool desktop, int support = 0)
    {
        int sequence = ++cloudSequence;
        using var rig = new Rig(desktop);
        var owner = new Actor(a.LocalPlayer.ActorNumber);
        using var sender = new Peer(owner, true);
        using var receiver = new Peer(owner, false);
        float minimumHead = rig.headHeight;
        if (support != 0)
        {
            rig.Move(new Vector3(support == 2 ? 2.2f : 0, desktop ? 3.01f : 3.51f, 0) - rig.player.transform.position);
            minimumHead = rig.head.position.y;
        }
        receiver.cabin.transform.position = Vector3.right * 100;
        Physics.SyncTransforms();
        PhotonNetwork.NetworkingClient.CurrentRoom = b.CurrentRoom;
        Set(sender.avatar, "headSource", rig.head); Set(sender.avatar, "lHandSource", rig.left); Set(sender.avatar, "rHandSource", rig.right);
        int received = 0, rendered = 0;
        float worst = 0f;
        Action<EventData> onEvent = e =>
        {
            if (e.Code != 77 || e.Sender != a.LocalPlayer.ActorNumber) return;
            var envelope = (object[])e.CustomData;
            // Unreliable packets from the preceding fixture can still be in flight.
            if (envelope.Length != 3 || (int)envelope[2] != sequence) return;
            var values = (object[])envelope[1];
            Check(values[0] is PosePacket && ((PosePacket)values[0]).CabinArrival == 1, "Photon Cloud decodes extended passenger packet");
            GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
            // Preserve the timestamp's uint wrap just as PUN's message header does.
            double now = unchecked((uint)b.LoadBalancingPeer.ServerTimeInMilliSeconds) / 1000d;
            double sent = unchecked((uint)(int)envelope[0]) / 1000d;
            Deliver(receiver, b.CurrentRoom.GetPlayer(e.Sender), values, sent, now);
            received++;
        };
        b.EventReceived += onEvent;
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double nextSend = 0;
            float previousJump = 0;
            while (timer.Elapsed.TotalSeconds < 7)
            {
                float t = (float)timer.Elapsed.TotalSeconds;
                GR.Player.instance = rig.player; HelicopterManager.instance = sender.manager;
                if (support == 2)
                {
                    rig.vr.wasLeftHandTouching = true;
                    sender.cabin.ReportHandSupport(rig.player.transform, sender.roof, null);
                }
                sender.cabin.MoveCabin(Vector3.up * (t * 3f), Quaternion.Euler(1f, 12f, 2f), true);
                float jump = support == 1 && t >= 2 && t <= 2.85f ? .8f * Mathf.Sin((t - 2) / .85f * Mathf.PI) : 0;
                rig.Move(Vector3.up * (jump - previousJump)); previousJump = jump;
                sender.cabin.RefreshPassenger();
                if (t >= nextSend)
                {
                    var values = Capture(sender.avatar, false);
                    Check(a.OpRaiseEvent(77, new object[] { a.LoadBalancingPeer.ServerTimeInMilliSeconds, values, sequence },
                        new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable), "Send passenger event");
                    nextSend = t + 0.05;
                }
                GR.Player.instance = null; HelicopterManager.instance = receiver.manager;
                receiver.cabin.MoveCabin(new Vector3(100, t * 3f + .4f, 0), Quaternion.Euler(-1f, 12f, -2f), true);
                a.Service(); b.Service();
                Clock(unchecked((uint)b.LoadBalancingPeer.ServerTimeInMilliSeconds) / 1000d);
                receiver.render();
                if (Get<bool>(receiver.avatar, "_hasPose"))
                {
                    float relativeHeight = receiver.cabin.transform.InverseTransformPoint(Get<BodyPose>(receiver.avatar, "_lastPose").headPos).y;
                    worst = Mathf.Max(worst, minimumHead - relativeHeight);
                    Check(relativeHeight > minimumHead - 0.02f, "Live remote passenger stays above supporting surface: " + relativeHeight + " vs " + minimumHead + ", support " + support);
                    rendered++;
                }
                System.Threading.Thread.Sleep(10);
            }
            Check(received > 40 && rendered > 100, "Enough real network packets/render samples for " + (desktop ? "desktop" : "VR"));
            Debug.Log($"Cloud carry PASS: {(desktop ? "desktop" : "VR")}, support {support}, {received} real packets, {rendered} render samples, max height deficit {worst:F4} m");
        }
        finally { b.EventReceived -= onEvent; }
    }
}
