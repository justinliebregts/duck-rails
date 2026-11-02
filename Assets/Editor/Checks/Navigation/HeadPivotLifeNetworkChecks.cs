using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Offline integration: actual respawn, ragdoll, pose writer/codec and remote receiver.
// Does not connect to Photon or invoke the complete timed death/effects coroutine.
public static class HeadPivotLifeNetworkChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    static int checks;
    sealed class Actor : Photon.Realtime.Player { public Actor() : base("movement regression", 2, false) { } }
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static Action Bind(string n) => (Action)Delegate.CreateDelegate(typeof(Action), typeof(NewPlayer).GetMethod(n, P));
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < .001f, message + ": " + a + " vs " + b);
    static PhotonPlayer Avatar(Actor actor, bool local)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab"));
        go.SetActive(false); var p = go.GetComponent<PhotonPlayer>();
        p.photonView.OwnerActorNr = actor.ActorNumber; p.photonView.ControllerActorNr = actor.ActorNumber;
        typeof(PhotonView).GetProperty("IsMine").SetValue(p.photonView, local);
        Set(p, "deathRagdollPrefab", null);
        foreach (var limb in go.GetComponentsInChildren<TwoBoneLimb>(true)) limb.CaptureRest();
        return p;
    }
    static object[] Capture(PhotonPlayer p, NewPlayer rig)
    {
        var stream = new PhotonStream(true, null); Set(stream, "writeData", new List<object>(5));
        p.OnPhotonSerializeView(stream, default); var data = stream.ToArray();
        var original = (PosePacket)data[0];
        Near(original.HeadPos, rig.headCollider.transform.position, "Writer used root instead of tracked head");
        var wire = new ExitGames.Client.Photon.StreamBuffer(128);
        short length = PosePacket.SerializeStream(wire, data[0]); wire.Position = 0;
        data[0] = PosePacket.DeserializeStream(wire, length);
        var decoded = (PosePacket)data[0];
        Near(decoded.HeadPos, original.HeadPos, "Head codec changed pose");
        Near(decoded.LHandPos, rig.leftHandTransform.position, "Left hand codec changed pose");
        Near(decoded.RHandPos, rig.rightHandTransform.position, "Right hand codec changed pose");
        Check(Quaternion.Angle(decoded.HeadRot, rig.headCollider.transform.rotation) < .05f, "Turn heading lost in codec");
        Check(length == 39 && data.Length == (GR.Player.instance.IsDead ? 5 : 3), "Life-state wire layout changed");
        return data;
    }
    static void Deliver(PhotonPlayer receiver, Actor actor, object[] data, int timestamp, int now)
    {
        typeof(PhotonNetwork).GetField("frame", P).SetValue(null, Time.frameCount);
        typeof(PhotonNetwork).GetField("frametime", P).SetValue(null, now / 1000d);
        receiver.OnPhotonSerializeView(new PhotonStream(false, data), new PhotonMessageInfo(actor, timestamp, receiver.photonView));
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        var oldPlayer = GR.Player.instance; var oldVR = NewPlayer.instance; var oldManager = GameManager.instance;
        var oldHeli = HelicopterManager.instance; var oldRoom = PhotonNetwork.NetworkingClient.CurrentRoom;
        var oldAnalytics = DuckzAnalytics.Instance; var oldSimulation = Physics.simulationMode;
        var services = new GameObject("Life/network test services"); services.SetActive(false);
        var analytics = services.AddComponent<DuckzAnalytics>(); typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, analytics);
        var manager = services.AddComponent<GameManager>(); GameManager.instance = manager; manager.usingDesktopPlayer = false;
        manager.respawnPoint = new GameObject("Spawn").transform; manager.respawnPoint.SetParent(services.transform);
        manager.respawnPoint.SetPositionAndRotation(new Vector3(20, 2, -10), Quaternion.Euler(0, 110, 0));
        var actor = new Actor(); var room = new Room("offline movement regression", new RoomOptions()); room.AddPlayer(actor);
        PhotonNetwork.NetworkingClient.CurrentRoom = room; HelicopterManager.instance = null;
        Physics.simulationMode = SimulationMode.Script;
        Action publish = Bind("RestoreMovementPoses"), render = Bind("RestoreZeroStepRenderedPoses"); checks = 0;
        try
        {
            foreach (var offset in new[] { Vector3.zero, new Vector3(2, 0, -1), new Vector3(-3, 0, 2) })
            foreach (bool betweenStages in new[] { false, true })
            using (var rig = new MovementTestRig())
            {
                var p = rig.p; NewPlayer.instance = p; p.rb.interpolation = RigidbodyInterpolation.Interpolate;
                p.headCollider.transform.localPosition = offset + Vector3.up * 1.6f;
                p.leftHandTransform.localPosition = offset + new Vector3(-.3f, 1, 0);
                p.rightHandTransform.localPosition = offset + new Vector3(.3f, 1, 0);
                var player = rig.root.AddComponent<GR.Player>(); GR.Player.instance = player;
                player.playerHUD = rig.root.AddComponent<PlayerHUD>();
                var image = new GameObject("Death overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                image.transform.SetParent(rig.root.transform); player.playerHUD.deathEffect = image.GetComponent<Image>();
                player.ragdoll = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab"));
                player.ragdoll.Park(); player.ragdoll.sourceHip = p.bodyMidpoint;
                player.ragdoll.leftHandSource = p.leftHandTransform; player.ragdoll.rightHandSource = p.rightHandTransform;
                player.ragdoll.Prepare(rig.root.transform);
                var sender = Avatar(actor, true); var receiver = Avatar(actor, false);
                sender.BindSources(p.headCollider.transform, p.leftHandTransform, p.rightHandTransform);
                try
                {
                    p.Warp(Vector3.zero); p.rb.linearVelocity = Vector3.right * 4;
                    p.transform.position -= Vector3.right * .03f;
                    p.Turn(45); p.ApplyPassengerDisplacement(Vector3.forward * .1f);
                    // Seed real B contact-render history before dying. A respawn
                    // must clear it as well as the queued turn/carry pose, or the
                    // next rendered movement can revisit the death location.
                    p.leftHandColliding = true;
                    var contactRender = (Func<Vector3, Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3, Vector3>),
                        p, typeof(NewPlayer).GetMethod("ContactRenderPosition", P));
                    contactRender(p.transform.position);
                    p.leftHandColliding = false;
                    Check(Get<int>(p, "movementRenderCount") > 0, "Death fixture lacks contact-render history");
                    var living = Capture(sender, p); Deliver(receiver, actor, living, 100000, 100050);
                    Near(Get<BodyPose>(receiver, "_lastPose").headPos, p.headCollider.transform.position, "First remote pose misplaced");
                    Vector3 display = p.transform.position, physical = p.rb.position;
                    player.IsDead = true; player.playerHUD.EnableDeathEffect();
                    player.ragdoll.PoseToPlayer(rig.root.transform);
                    Near(p.transform.position, display, "Ragdoll activation moved displayed player");
                    Near(p.rb.position, physical, "Ragdoll sync discarded queued physical travel");
                    var dead = Capture(sender, p); Deliver(receiver, actor, dead, 100100, 100150);
                    Check(receiver.IsDead, "Remote death state missing");
                    Vector3 corpse = player.ragdoll.transform.position;
                    if (betweenStages) publish();
                    manager.RespawnPlayer();
                    Check(Get<int>(p, "movementRenderCount") == 0, "Respawn retained pre-death contact-render history");
                    Vector3 head = p.headCollider.transform.position;
                    Near(new Vector3(head.x, 2, head.z), manager.respawnPoint.position, "Actual respawn destination incorrect");
                    Check(Mathf.Abs(Mathf.DeltaAngle(p.headCollider.transform.eulerAngles.y, 110)) < .01f, "Respawn heading incorrect");
                    Check(!player.IsDead && !player.playerHUD.deathEffect.enabled && !player.ragdoll.gameObject.activeSelf, "Respawn lifecycle did not reset");
                    Near(p.rb.linearVelocity, Vector3.zero, "Respawn retained momentum");
                    if (!betweenStages) publish(); render();
                    Near(p.headCollider.transform.position, head, "Pending B pose update undid respawn");
                    Physics.Simulate(1f / 72); rig.move(1f / 72);
                    Near(p.headCollider.transform.position, head, "Post-respawn movement snapped back");
                    Near(player.ragdoll.transform.position, corpse, "Respawn dragged detached corpse");
                    var alive = Capture(sender, p); Deliver(receiver, actor, alive, 100200, 100250);
                    Check(!receiver.IsDead, "Remote respawn state missing");
                    Near(Get<BodyPose>(receiver, "_lastPose").headPos, head, "Remote respawn interpolated from old death location");
                    Deliver(receiver, actor, dead, 100100, 100300);
                    Check(!receiver.IsDead, "Delayed death packet reversed respawn");
                    Near(Get<BodyPose>(receiver, "_lastPose").headPos, head, "Stale death packet moved remote avatar");
                }
                finally { Object.DestroyImmediate(sender.gameObject); Object.DestroyImmediate(receiver.gameObject); Object.DestroyImmediate(player.ragdoll.gameObject); publish(); render(); }
            }
            Debug.Log("HeadPivotLifeNetworkChecks PASS: " + checks + " assertions; six B respawn/pose combinations, ragdoll sync, compact pose codec, remote death/respawn and stale death rejection.");
        }
        finally
        {
            publish(); render(); Object.DestroyImmediate(services);
            GR.Player.instance = oldPlayer; NewPlayer.instance = oldVR; GameManager.instance = oldManager;
            HelicopterManager.instance = oldHeli; PhotonNetwork.NetworkingClient.CurrentRoom = oldRoom;
            typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, oldAnalytics); Physics.simulationMode = oldSimulation;
        }
    }
}
