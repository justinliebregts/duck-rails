using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Disposable Unity batch project only. Two real UDP clients share a deterministic
// scene fixture; actual PUN RPC encoding/transport/dispatch and game receivers run.
// This is not a two-headset test of tracking, rendering or perceived latency.
[InitializeOnLoad]
public static class PlayerKnockbackChecks
{
    const string Key = "PlayerKnockbackChecks.Run";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<PhotonView> views = new List<PhotonView>();
    static readonly List<GameObject> objects = new List<GameObject>();
    static LoadBalancingClient a, b, original;
    static PhotonPlayer pa, pb;
    static MovementTestRig rig;
    static GR.Player human;
    static DesktopPlayer desktop;
    static int checks, received, sent, accepted;
    static Vector3 deliveredVelocity;
    static bool useDesktop;
    static Exception receiveError;

    static PlayerKnockbackChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Execute();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static FieldInfo Field(Type t, string name)
    {
        for (; t != null; t = t.BaseType) { var f = t.GetField(name, Private); if (f != null) return f; }
        throw new MissingFieldException(name);
    }
    static void Set(object o, string name, object value) => Field(o.GetType(), name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o.GetType(), name).GetValue(o);
    static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Near(Vector3 actual, Vector3 expected, string label) => Check(Vector3.Distance(actual, expected) < .002f, label + ": " + actual + " expected " + expected);
    static Vector3 Expected(Vector3 velocity)
    {
        float strength = Mathf.Clamp01((velocity.magnitude - 2f) / 7f);
        return strength <= 0 ? Vector3.zero : (velocity.normalized + Vector3.up * (.55f * strength)).normalized * (12f * strength);
    }
    static void Select(LoadBalancingClient peer)
    {
        PhotonNetwork.NetworkingClient = peer;
        foreach (var view in views)
        {
            typeof(PhotonView).GetProperty("Owner").SetValue(view, peer.CurrentRoom.GetPlayer(view.OwnerActorNr));
            typeof(PhotonView).GetProperty("Controller").SetValue(view, peer.CurrentRoom.GetPlayer(view.ControllerActorNr));
            typeof(PhotonView).GetProperty("IsMine").SetValue(view, view.ControllerActorNr == peer.LocalPlayer.ActorNumber);
        }
    }
    static void Pump(Func<bool> ready, string stage)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            a.Service(); b.Service();
            if (receiveError != null) throw new Exception("RPC dispatch failed", receiveError);
            if (timer.Elapsed.TotalSeconds > 35) throw new Exception("Timeout: " + stage + "; " + a.State + "/" + a.DisconnectedCause + ", " + b.State + "/" + b.DisconnectedCause);
            System.Threading.Thread.Sleep(5);
        }
    }
    static void OnEvent(LoadBalancingClient peer, EventData data)
    {
        if (data.Code != 200) return;
        try
        {
            Select(peer);
            var rpc = (Hashtable)data.CustomData;
            int target = (int)rpc[(byte)0];
            var victim = PhotonView.Find(target).GetComponent<PhotonPlayer>();
            float before = Get<float>(victim, "_nextItemHitTime");
            typeof(PhotonNetwork).GetMethod("ExecuteRpc", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { rpc, peer.CurrentRoom.GetPlayer(data.Sender) });
            // Blood is broadcast by the receiver as a separate two-argument RPC.
            // Dispatch it too, but count only three-argument ReceiveItemHit calls.
            if (!(rpc[(byte)4] is object[] args) || args.Length != 3) return;
            received++;
            if (Get<float>(victim, "_nextItemHitTime") > before) accepted++;
            deliveredVelocity = useDesktop ? Get<Vector3>(desktop, "_pushVelocity") : rig.p.rb.linearVelocity;
        }
        catch (Exception e) { receiveError = e; }
    }
    static PhotonPlayer Avatar(LoadBalancingClient peer)
    {
        var staging = new GameObject("Inactive avatar staging"); staging.SetActive(false); objects.Add(staging);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab"), staging.transform);
        var p = go.GetComponent<PhotonPlayer>();
        Check(p != null, "Authored player prefab resolves");
        var view = p.photonView;
        view.ViewID = peer.LocalPlayer.ActorNumber * PhotonNetwork.MAX_VIEW_IDS + 1;
        view.OwnerActorNr = peer.LocalPlayer.ActorNumber; view.ControllerActorNr = view.OwnerActorNr;
        views.Add(view); PhotonPlayer.Active.Add(p);
        Set(p, "_hasPose", true);
        // Prevent unrelated audio/visual services; retain authored hit volumes/settings.
        Set(p, "hitBloodPrefab", null);
        return p;
    }
    static PhotonGrabbable Weapon(string name, LoadBalancingClient owner)
    {
        Select(owner);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/" + name + ".prefab"));
        objects.Add(go);
        var weapon = go.GetComponent<PhotonGrabbable>();
        Check(weapon != null && weapon.PlayerLaunchSpeed == 12, name + " authored launch speed");
        var view = go.GetComponent<PhotonView>();
        view.ViewID = owner.LocalPlayer.ActorNumber * PhotonNetwork.MAX_VIEW_IDS + 100 + views.Count;
        view.OwnerActorNr = owner.LocalPlayer.ActorNumber; view.ControllerActorNr = view.OwnerActorNr; views.Add(view);
        // Audio/haptics aren't the focus of these deterministic physics assertions.
        Set(weapon, "itemSounds", null);
        Check(weapon.LocomotionSampleCount > 0, name + " has combat samples");
        return weapon;
    }
    static void Reset(PhotonPlayer victim)
    {
        Set(victim, "_nextItemHitTime", -1f); Set(victim, "_swingLockUntil", -1f);
        Set(victim, "_remoteDead", false); human.IsDead = false;
        rig.p.rb.linearVelocity = Vector3.zero; desktop.Launch(Vector3.zero);
        rig.p.StopAllCoroutines(); rig.p.disableMovement = false;
        Set(rig.p, "disableMovementRequestCount", 0);
        deliveredVelocity = Vector3.zero;
    }
    static void MathCases(PhotonPlayer victim)
    {
        foreach (float speed in new[] { 0f, 1.999f, 2f, 2.001f, 5.5f, 9f, 90f })
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, new Vector3(1, 1, 1).normalized })
        {
            bool launches = victim.TryLaunchVelocity(direction * speed, 12, out var velocity);
            Check(launches == (speed > 2), "Launch speed boundary " + speed);
            Near(velocity, Expected(direction * speed), "Launch scaling/direction " + speed);
            Check(velocity.magnitude <= 12.001f, "Launch remains capped");
        }
        Check(!victim.TryLaunchVelocity(Vector3.right * 9, 0, out _), "Zero configured push stays disabled");
    }
    static void ReceiverEdges(PhotonGrabbable weapon, PhotonPlayer attacker, PhotonPlayer victim, LoadBalancingClient sender, LoadBalancingClient receiver)
    {
        Select(receiver);
        var view = weapon.GetComponent<PhotonView>();
        var info = new PhotonMessageInfo(receiver.CurrentRoom.GetPlayer(sender.LocalPlayer.ActorNumber), 0, victim.photonView);
        Vector3 position = new Vector3(30, 1.6f, -20);
        // Unparented tracked source keeps the exact 6m boundary free of skeletal
        // scale roundoff (the rendered head can differ by a few micrometres).
        var head = new GameObject("Deterministic tracked head").transform; objects.Add(head.gameObject);
        head.position = position; Set(victim, "headSource", head);
        weapon.transform.position = position;
        void Reject(string label, int id, Vector3 velocity, PhotonMessageInfo message)
        {
            Reset(victim); victim.ReceiveItemHit(id, velocity, position, message);
            Check(Get<float>(victim, "_nextItemHitTime") == -1, label);
            Near(rig.p.rb.linearVelocity, Vector3.zero, label + " preserves velocity");
        }
        Reject("Unknown item rejected", 999999, Vector3.right * 9, info);
        Reject("Wrong sender rejected", view.ViewID, Vector3.right * 9, new PhotonMessageInfo(receiver.LocalPlayer, 0, victim.photonView));
        Reject("Slow impact rejected", view.ViewID, Vector3.right * 1.999f, info);
        Reject("NaN rejected", view.ViewID, new Vector3(float.NaN, 0, 0), info);
        Reject("Infinity rejected", view.ViewID, new Vector3(float.PositiveInfinity, 0, 0), info);
        weapon.transform.position = position + Vector3.right * 6.001f;
        Reject("Beyond six metre validation rejected", view.ViewID, Vector3.right * 9, info);
        weapon.transform.position = position + Vector3.right * 6f;
        Reset(victim); victim.ReceiveItemHit(view.ViewID, Vector3.right * 9, position, info);
        Near(rig.p.rb.linearVelocity, Expected(Vector3.right * 9), "Exact six metre limit and tracked-head/root offset accepted");
        weapon.transform.position = position;
        Vector3 first = rig.p.rb.linearVelocity;
        victim.ReceiveItemHit(view.ViewID, Vector3.left * 9, position, info);
        Near(rig.p.rb.linearVelocity, first, "Duplicate hit suppressed during cooldown");
        Set(victim, "_nextItemHitTime", Time.time - .001f);
        victim.ReceiveItemHit(view.ViewID, Vector3.left * 9, position, info);
        Near(rig.p.rb.linearVelocity, Expected(Vector3.left * 9), "Next hit accepted after cooldown");
        Reset(victim); human.IsDead = true;
        victim.ReceiveItemHit(view.ViewID, Vector3.right * 9, position, info);
        Near(rig.p.rb.linearVelocity, Vector3.zero, "Dead victim not launched");
        human.IsDead = false; Set(attacker, "_remoteDead", true);
        victim.ReceiveItemHit(view.ViewID, Vector3.right * 9, position, info);
        Near(rig.p.rb.linearVelocity, Vector3.zero, "Dead attacker rejected"); Set(attacker, "_remoteDead", false);
        Reset(victim);
        Check(!victim.AcceptItemHit(receiver.LocalPlayer.ActorNumber, Vector3.right * 9, position, 12), "Self hit rejected");
        Check(!victim.AcceptItemHit(sender.LocalPlayer.ActorNumber, Vector3.right * 9, new Vector3(0, float.NaN, 0), 12), "Invalid hit position rejected");
        Select(sender); Reset(victim);
        victim.ReceiveItemHit(view.ViewID, Vector3.right * 9, position, info);
        Near(rig.p.rb.linearVelocity, Vector3.zero, "Non-owner replica cannot apply launch");
        Debug.Log("KNOCKBACK receiver edges PASS " + weapon.name);
    }
    static void NetworkHit(PhotonGrabbable weapon, PhotonPlayer victim, LoadBalancingClient sender, LoadBalancingClient receiver, float speed, bool held)
    {
        Select(sender); Reset(victim);
        var body = Get<Game.Animation.RemotePlayerBody>(victim, "body");
        weapon.transform.position = new Vector3(30, 1, -20);
        var now = Get<Vector3[]>(weapon, "_sampleNow");
        var prev = Get<Vector3[]>(weapon, "_samplePrev");
        int count = weapon.CopySampleWorldPositions(now);
        Check(count > 0, "Production weapon sample copy");
        Vector3 velocity = Vector3.right * speed;
        const float dt = 1f / 60f;
        body.Head.position = now[count / 2]; Set(victim, "headSource", body.Head);
        for (int i = 0; i < count; i++) prev[i] = now[i] - velocity * dt;
        GameObject handObject = null;
        if (held) { handObject = new GameObject("Test hand"); handObject.SetActive(false); Set(weapon, "holder", handObject.AddComponent<PlayerHand>()); }
        else Set(weapon, "holder", null);
        int before = received, acceptedBefore = accepted;
        Call(weapon, "TickPlayerSwings", count, dt, weapon.LocomotionRadius, false);
        sent++;
        Pump(() => received > before, "player hit RPC");
        Check(accepted == acceptedBefore + 1, "Exactly one accepted RPC");
        Near(deliveredVelocity, Expected(velocity), "Live RPC applied expected " + (useDesktop ? "desktop" : "VR") + " velocity");
        Check(!human.IsDead, "Knockback does not kill victim");
        if (!useDesktop) Check(rig.p.disableMovement, "VR launch protects against immediate hand locomotion damping");
        Set(weapon, "holder", null); if (handObject != null) Object.DestroyImmediate(handObject);
        Debug.Log($"KNOCKBACK live PASS {weapon.name}, actor {sender.LocalPlayer.ActorNumber}->{receiver.LocalPlayer.ActorNumber}, {(useDesktop ? "desktop" : "VR")}, held={held}, swing={speed}, launch={deliveredVelocity.magnitude:F3}");
    }
    static void Execute()
    {
        original = PhotonNetwork.NetworkingClient;
        int result = 1;
        try
        {
            checks = received = sent = accepted = 0; views.Clear(); objects.Clear();
            a = new LoadBalancingClient(ConnectionProtocol.Udp); b = new LoadBalancingClient(ConnectionProtocol.Udp);
            typeof(PhotonCustomTypes).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string id = Guid.NewGuid().ToString("N");
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = "knockback-check-" + id; settings.FixedRegion = "us";
            a.AuthValues = new AuthenticationValues("knockback-a-" + id); b.AuthValues = new AuthenticationValues("knockback-b-" + id);
            Check(a.ConnectUsingSettings(settings) && b.ConnectUsingSettings(settings), "Start private UDP clients");
            Pump(() => a.State == ClientState.ConnectedToMasterServer && b.State == ClientState.ConnectedToMasterServer, "connect");
            Check(a.OpCreateRoom(new EnterRoomParams { RoomName = id, RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = 2, PlayerTtl = 0, EmptyRoomTtl = 0 } }), "Create private room");
            Pump(() => a.InRoom, "create"); Check(b.OpJoinRoom(new EnterRoomParams { RoomName = id }), "Join private room");
            Pump(() => b.InRoom && a.CurrentRoom.PlayerCount == 2 && b.CurrentRoom.PlayerCount == 2, "join");
            Select(a); pa = Avatar(a); pb = Avatar(b);
            rig = new MovementTestRig(); NewPlayer.instance = rig.p; rig.p.enabled = true;
            human = rig.root.AddComponent<GR.Player>(); GR.Player.instance = human;
            desktop = rig.root.AddComponent<DesktopPlayer>(); desktop.enabled = false;
            a.EventReceived += e => OnEvent(a, e); b.EventReceived += e => OnEvent(b, e);
            MathCases(pb);
            var fryPan = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponFryPan.prefab");
            Check(fryPan.GetComponent<PhotonGrabbable>().PlayerLaunchSpeed == 0, "Frying pan remains unchanged");
            foreach (string name in new[] { "WeaponNailBat", "WeaponGorilla" })
            {
                var weapon = Weapon(name, a);
                ReceiverEdges(weapon, pa, pb, a, b);
                foreach (bool impaired in new[] { false, true })
                {
                    foreach (var peer in new[] { a.LoadBalancingPeer, b.LoadBalancingPeer })
                    {
                        peer.NetworkSimulationSettings.IncomingLag = 150; peer.NetworkSimulationSettings.OutgoingLag = 150;
                        peer.NetworkSimulationSettings.IncomingJitter = 40; peer.NetworkSimulationSettings.OutgoingJitter = 40;
                        peer.NetworkSimulationSettings.IncomingLossPercentage = 5; peer.NetworkSimulationSettings.OutgoingLossPercentage = 5;
                        peer.IsSimulationEnabled = impaired;
                    }
                    Debug.Log("KNOCKBACK network impairment=" + impaired + " (150ms each direction, 40ms jitter, 5% loss per peer)");
                    foreach (bool reverse in new[] { false, true })
                    {
                        var sender = reverse ? b : a; var receiver = reverse ? a : b;
                        var victim = reverse ? pa : pb;
                        Select(sender); var view = weapon.GetComponent<PhotonView>();
                        view.OwnerActorNr = sender.LocalPlayer.ActorNumber; view.ControllerActorNr = sender.LocalPlayer.ActorNumber;
                        foreach (bool desktopMode in new[] { false, true })
                        {
                            useDesktop = desktopMode; desktop.enabled = desktopMode;
                            foreach (bool held in new[] { false, true })
                                NetworkHit(weapon, victim, sender, receiver, held ? 9 : 5.5f, held);
                        }
                    }
                }
                desktop.enabled = false; useDesktop = false;
            }
            Debug.Log($"PlayerKnockbackChecks PASS: {checks} assertions, {sent} live PUN hits, {accepted} accepted; both weapons, both master/client directions, desktop/VR, held/thrown, lag/jitter/loss, receiver guards and launch boundaries.");
            result = 0;
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            SessionState.SetBool(Key, false);
            foreach (var peer in new[] { a, b }) if (peer != null) { peer.LoadBalancingPeer.IsSimulationEnabled = false; peer.Disconnect(); }
            PhotonNetwork.NetworkingClient = original;
            if (pa != null) PhotonPlayer.Active.Remove(pa); if (pb != null) PhotonPlayer.Active.Remove(pb);
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            rig?.Dispose(); GR.Player.instance = null; NewPlayer.instance = null;
            EditorApplication.Exit(result);
        }
    }
}
