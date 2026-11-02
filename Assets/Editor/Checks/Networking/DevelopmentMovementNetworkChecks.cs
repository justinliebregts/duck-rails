using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Uses the actual motor, pose writer, wire codec, receiver and body playback with synthetic delivery.
public static partial class DevelopmentMovementNetworkChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    sealed class Actor : Photon.Realtime.Player { public Actor(int id) : base("development movement " + id, id, false) { } }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static readonly FieldInfo NetworkFrame = typeof(PhotonNetwork).GetField("frame", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo NetworkTime = typeof(PhotonNetwork).GetField("frametime", BindingFlags.Static | BindingFlags.NonPublic);
    static void Clock(double time) { NetworkFrame.SetValue(null, Time.frameCount); NetworkTime.SetValue(null, time); }
    static PhotonPlayer Avatar(Actor actor, bool local)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab"));
        go.SetActive(false);
        var avatar = go.GetComponent<PhotonPlayer>();
        avatar.photonView.OwnerActorNr = avatar.photonView.ControllerActorNr = actor.ActorNumber;
        typeof(PhotonView).GetProperty("IsMine").SetValue(avatar.photonView, local);
        Set(avatar, "deathRagdollPrefab", null);
        foreach (var limb in go.GetComponentsInChildren<TwoBoneLimb>(true)) limb.CaptureRest();
        return avatar;
    }
    struct Delivery { public double arrive, sent; public object[] payload; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        checks = 0;
        var oldVR = NewPlayer.instance; var oldPlayer = GR.Player.instance; var oldHeli = HelicopterManager.instance;
        var oldRoom = PhotonNetwork.NetworkingClient.CurrentRoom; var oldMode = Physics.simulationMode;
        var room = new Room("offline development movement", new RoomOptions()); var actor = new Actor(2); room.AddPlayer(actor);
        PhotonNetwork.NetworkingClient.CurrentRoom = room; HelicopterManager.instance = null; GR.Player.instance = null;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            foreach (bool adverse in new[] { false, true }) RunScenario(actor, adverse);
            foreach (CollisionCase scenario in Enum.GetValues(typeof(CollisionCase)))
                foreach (bool adverse in new[] { false, true }) RunCollisionScenario(actor, scenario, adverse);
            Debug.Log("DevelopmentMovementNetworkChecks PASS: " + checks + " assertions; actual motor/collisions/toggles, unchanged 39-byte pose codec, late join, 80-250ms delivery/jitter/loss, stale packets and remote body playback.");
        }
        finally
        {
            Physics.simulationMode = oldMode; NewPlayer.instance = oldVR; GR.Player.instance = oldPlayer;
            HelicopterManager.instance = oldHeli; PhotonNetwork.NetworkingClient.CurrentRoom = oldRoom;
        }
    }
    static void RunScenario(Actor actor, bool adverse)
    {
        using (var rig = new MovementTestRig())
        {
            var p = rig.p; p.rb.interpolation = RigidbodyInterpolation.None; p.locomotionEnabledLayers = 0;
            p.stickMove = true; p.rb.useGravity = true;
            p.controllerInput.leftThumbstickValue = Vector2.up; p.controllerInput.rightThumbstickValue = Vector2.up;
            var move = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), p, typeof(NewPlayer).GetMethod("DevelopmentMovement", Private));
            var sender = Avatar(actor, true); var receiver = Avatar(actor, false); var late = Avatar(actor, false);
            sender.BindSources(p.headCollider.transform, p.leftHandTransform, p.rightHandTransform);
            var playback = (Action)Delegate.CreateDelegate(typeof(Action), receiver, typeof(PhotonPlayer).GetMethod("LateUpdate", Private));
            var latePlayback = (Action)Delegate.CreateDelegate(typeof(Action), late, typeof(PhotonPlayer).GetMethod("LateUpdate", Private));
            var deliveries = new List<Delivery>(); float previousZ = float.NegativeInfinity;
            try
            {
                const float dt = 1f / 72;
                for (int frame = 0; frame < 720; frame++)
                {
                    double now = 100 + frame * (double)dt;
                    Physics.SyncTransforms(); Physics.Simulate(dt); move(dt);
                    if (frame % 4 == 0 && (!adverse || frame % 28 != 0))
                    {
                        var stream = new PhotonStream(true, null); Set(stream, "writeData", new List<object>(5));
                        sender.OnPhotonSerializeView(stream, default); object[] payload = stream.ToArray();
                        var packet = (PosePacket)payload[0];
                        Check(Vector3.Distance(packet.HeadPos, p.headCollider.transform.position) < .0001f, "Writer lost joystick head pose");
                        var wire = new ExitGames.Client.Photon.StreamBuffer(128);
                        short length = PosePacket.SerializeStream(wire, packet); wire.Position = 0;
                        payload[0] = PosePacket.DeserializeStream(wire, length);
                        Check(length == 39 && payload.Length == 3, "Development movement changed wire format");
                        var decoded = (PosePacket)payload[0];
                        Check(Vector3.Distance(decoded.LHandPos, p.leftHandTransform.position) < .001f
                            && Vector3.Distance(decoded.RHandPos, p.rightHandTransform.position) < .001f, "Hands detached in codec");
                        double delay = adverse ? .08 + (frame % 16) / 12d * .17 : .08;
                        deliveries.Add(new Delivery { sent = now, arrive = now + delay, payload = payload });
                    }
                    Clock(now);
                    for (int i = deliveries.Count - 1; i >= 0; i--)
                    {
                        var item = deliveries[i]; if (item.arrive > now) continue;
                        receiver.OnPhotonSerializeView(new PhotonStream(false, item.payload), new PhotonMessageInfo(actor, (int)(item.sent * 1000), receiver.photonView));
                        if (frame >= 360) late.OnPhotonSerializeView(new PhotonStream(false, item.payload), new PhotonMessageInfo(actor, (int)(item.sent * 1000), late.photonView));
                        deliveries.RemoveAt(i);
                    }
                    playback(); if (frame >= 360) latePlayback();
                    if (frame > 100)
                    {
                        var remote = Get<BodyPose>(receiver, "_lastPose");
                        Check(float.IsFinite(remote.headPos.y) && float.IsFinite(remote.headPos.z), "Non-finite remote pose");
                        Check(remote.headPos.z >= previousZ - .002f, "Stale delivery rewound forward motion");
                        Check(remote.headPos.z <= p.headCollider.transform.position.z + .001f, "Remote extrapolated through accepted position");
                        Check(p.headCollider.transform.position.z - remote.headPos.z < 4, "Remote fell too far behind joystick");
                        previousZ = remote.headPos.z;
                        var body = Get<RemotePlayerBody>(receiver, "body");
                        // BodyPose is an eye pose; the avatar places its bone using its authored world-space calibration.
                        Vector3 expectedHead = (Vector3)remote.headPos - (Quaternion)remote.headRot * (Vector3)Get<Unity.Mathematics.float3>(body, "eyeOffset");
                        Check(Vector3.Distance(body.Head.position, expectedHead) < .001f, "Remote body did not apply calibrated joystick pose");
                    }
                }
                Check(Get<BodyPose>(receiver, "_lastPose").headPos.y > 20, "Remote lift did not ascend");
                Check(Vector3.Distance(Get<BodyPose>(late, "_lastPose").headPos, Get<BodyPose>(receiver, "_lastPose").headPos) < 2f, "Late join failed to converge");
            }
            finally { Object.DestroyImmediate(sender.gameObject); Object.DestroyImmediate(receiver.gameObject); Object.DestroyImmediate(late.gameObject); }
        }
    }
}
