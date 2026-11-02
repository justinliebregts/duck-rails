using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DevelopmentMovementNetworkChecks
{
    enum CollisionCase { WallSlide, InsideCorner, OutsideCorner, CeilingLift, ModeToggle, WallAndCeiling }

    static BoxCollider Obstacle(List<GameObject> objects, Vector3 position, Vector3 size)
    {
        var go = new GameObject("Network collision fixture"); objects.Add(go);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>(); box.size = size;
        return box;
    }

    static object[] CaptureCollisionPose(PhotonPlayer sender, NewPlayer p)
    {
        var stream = new PhotonStream(true, null); Set(stream, "writeData", new List<object>(5));
        sender.OnPhotonSerializeView(stream, default);
        object[] payload = stream.ToArray();
        var packet = (PosePacket)payload[0];
        Check(Vector3.Distance(packet.HeadPos, p.headCollider.transform.position) < .0001f, "Collision writer used an uncorrected head pose");
        var wire = new ExitGames.Client.Photon.StreamBuffer(128);
        short length = PosePacket.SerializeStream(wire, packet); wire.Position = 0;
        payload[0] = PosePacket.DeserializeStream(wire, length);
        var decoded = (PosePacket)payload[0];
        Check(length == 39 && payload.Length == 3, "Collision/toggle changed the wire format");
        Check(Vector3.Distance(decoded.HeadPos, packet.HeadPos) < .0001f, "Head wire round-trip error");
        Check(Vector3.Distance(decoded.LHandPos, p.leftHandTransform.position) < .001f
            && Vector3.Distance(decoded.RHandPos, p.rightHandTransform.position) < .001f, "Collision/toggle detached transmitted hands");
        return payload;
    }

    static float Penetration(Collider shape, Vector3 position, Quaternion rotation, BoxCollider obstacle)
        => Physics.ComputePenetration(shape, position, rotation, obstacle, obstacle.transform.position,
            obstacle.transform.rotation, out _, out float depth) ? depth : 0f;

    static Vector3 CheckCollisionReceiver(PhotonPlayer receiver, NewPlayer p, List<BoxCollider> obstacles,
        Vector3 previous, bool measureStep, ref float maxStep, ref float maxHeadEdgeOverlap)
    {
        var pose = Get<BodyPose>(receiver, "_lastPose");
        Vector3 head = pose.headPos;
        Check(float.IsFinite(head.x) && float.IsFinite(head.y) && float.IsFinite(head.z), "Non-finite collision playback");
        Check(Vector3.Distance(head, p.headCollider.transform.position) < 4, "Collision playback fell excessively behind");
        if (measureStep)
        {
            maxStep = Mathf.Max(maxStep, Vector3.Distance(head, previous));
            Check(Vector3.Distance(head, previous) < .35f, "Collision/toggle caused a remote teleport");
        }
        foreach (var obstacle in obstacles)
        {
            // Remote avatars interpolate samples, not colliders. Check eye traversal separately
            // from the visual head sphere's edge skimming an outside corner between samples.
            Check(!obstacle.bounds.Contains(head), "Remote eye crossed solid geometry");
            float edgeOverlap = Penetration(p.headCollider, head, pose.headRot, obstacle);
            maxHeadEdgeOverlap = Mathf.Max(maxHeadEdgeOverlap, edgeOverlap);
            Check(edgeOverlap < .0031f, "Remote head sphere clipped collision geometry between snapshots");
        }
        var body = Get<RemotePlayerBody>(receiver, "body");
        Vector3 expected = head - (Quaternion)pose.headRot * (Vector3)Get<Unity.Mathematics.float3>(body, "eyeOffset");
        Check(Vector3.Distance(body.Head.position, expected) < .001f, "Remote collision pose not applied to avatar");
        Check(receiver.TryGetHandPose(true, out Vector3 left, out _) && Vector3.Distance(left, pose.lHandPos) < .0001f,
            "Remote left hand disagrees with accepted pose");
        Check(receiver.TryGetHandPose(false, out Vector3 right, out _) && Vector3.Distance(right, pose.rHandPos) < .0001f,
            "Remote right hand disagrees with accepted pose");
        return head;
    }

    static void RunCollisionScenario(Actor actor, CollisionCase scenario, bool adverse)
    {
        var objects = new List<GameObject>(); var obstacles = new List<BoxCollider>();
        bool lifting = scenario == CollisionCase.CeilingLift || scenario == CollisionCase.WallAndCeiling;
        if (scenario == CollisionCase.WallSlide || scenario == CollisionCase.InsideCorner
            || scenario == CollisionCase.ModeToggle || scenario == CollisionCase.WallAndCeiling)
            obstacles.Add(Obstacle(objects, new Vector3(0, 2, 2), new Vector3(100, 20, .02f)));
        if (scenario == CollisionCase.InsideCorner || scenario == CollisionCase.ModeToggle)
            obstacles.Add(Obstacle(objects, new Vector3(2, 2, 0), new Vector3(.02f, 20, 100)));
        if (scenario == CollisionCase.OutsideCorner)
            obstacles.Add(Obstacle(objects, new Vector3(0, 1, 2), new Vector3(2, 4, 2)));
        if (lifting)
        {
            obstacles.Add(Obstacle(objects, new Vector3(0, 3, 0), new Vector3(100, .02f, 100)));
            obstacles.Add(Obstacle(objects, new Vector3(0, 0, 0), new Vector3(100, .1f, 100)));
        }
        try
        {
            using (var rig = new MovementTestRig())
            {
                var p = rig.p; p.rb.interpolation = RigidbodyInterpolation.None;
                p.headCollider.enabled = p.bodyCollider.enabled = true;
                p.headCollider.gameObject.layer = p.bodyCollider.gameObject.layer = 2;
                p.rb.useGravity = lifting; p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                p.locomotionEnabledLayers = 1; p.stickMove = true;
                var motor = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), p, typeof(NewPlayer).GetMethod("DevelopmentMovement", Private));
                var rotate = (Action)Delegate.CreateDelegate(typeof(Action), p, typeof(NewPlayer).GetMethod("Rotate", Private));
                var sender = Avatar(actor, true); var receiver = Avatar(actor, false); var late = Avatar(actor, false);
                foreach (var avatar in new[] { sender, receiver, late })
                    foreach (var child in avatar.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
                sender.BindSources(p.headCollider.transform, p.leftHandTransform, p.rightHandTransform);
                var playback = (Action)Delegate.CreateDelegate(typeof(Action), receiver, typeof(PhotonPlayer).GetMethod("LateUpdate", Private));
                var latePlayback = (Action)Delegate.CreateDelegate(typeof(Action), late, typeof(PhotonPlayer).GetMethod("LateUpdate", Private));
                var deliveries = new List<Delivery>();
                Vector3 previous = default, previousLate = default;
                float maxStep = 0, maxLocalOverlap = 0, maxRemoteEdge = 0, maxX = 0, maxY = 0;
                int toggles = 0, normalFrames = 0, sentPackets = 0, dropped = 0, reordered = 0, blockingFrames = 0;
                double newestDelivered = 0;
                try
                {
                    const float dt = 1f / 72;
                    Physics.SyncTransforms(); motor(dt);
                    for (int frame = 0; frame < 720; frame++)
                    {
                        double now = 1000 + (int)scenario * 20 + frame * (double)dt;
                        Vector2 input = frame < 360 ? Vector2.one : Vector2.zero;
                        if (scenario == CollisionCase.InsideCorner && frame >= 240 && frame < 360) input = -Vector2.one;
                        if (scenario == CollisionCase.OutsideCorner)
                            input = frame < 120 ? Vector2.one : frame < 240 ? Vector2.left : frame < 360 ? Vector2.down : Vector2.zero;
                        if (scenario == CollisionCase.CeilingLift) input = frame < 360 ? Vector2.up : Vector2.zero;
                        if (scenario == CollisionCase.ModeToggle)
                        {
                            if (frame == 120 || frame == 180 || frame == 360 || frame == 420)
                            {
                                bool was = p.stickMove; p.controllerInput.rightThumbstickButton.wasPressed = true;
                                rotate(); p.controllerInput.rightThumbstickButton.wasPressed = false;
                                Check(p.stickMove != was, "Right-stick click failed to toggle during collision"); toggles++;
                            }
                            input = frame < 180 ? Vector2.one : frame < 270 ? -Vector2.one : frame < 480 ? Vector2.one : Vector2.zero;
                        }
                        p.controllerInput.leftThumbstickValue = input;
                        p.controllerInput.rightThumbstickValue = lifting && (frame < 240 || (frame >= 300 && frame < 360)) ? Vector2.up : Vector2.zero;
                        Vector3 before = p.rb.position;
                        Physics.SyncTransforms(); Physics.Simulate(dt);
                        if (!motor(dt)) { rig.move(dt); normalFrames++; }
                        if (p.stickMove && p.wasHeadTouching) blockingFrames++;
                        Check(Vector3.Distance(before, p.rb.position) < .2f, "Local collision/toggle caused a jump");
                        maxX = Mathf.Max(maxX, p.rb.position.x); maxY = Mathf.Max(maxY, p.headCollider.transform.position.y);
                        foreach (var obstacle in obstacles)
                        {
                            float headDepth = Penetration(p.headCollider, p.headCollider.transform.position, p.headCollider.transform.rotation, obstacle);
                            float bodyDepth = Penetration(p.bodyCollider, p.bodyCollider.transform.position, p.bodyCollider.transform.rotation, obstacle);
                            maxLocalOverlap = Mathf.Max(maxLocalOverlap, headDepth, bodyDepth);
                            Check(headDepth < .0031f && bodyDepth < .0031f, "Authoritative head/body crossed collision geometry in " + scenario);
                        }
                        if (frame % 4 == 0)
                        {
                            if (adverse && frame % 28 == 0) dropped++;
                            else
                            {
                                double delay = adverse ? .08 + (frame % 16) / 12d * .17 : .08;
                                deliveries.Add(new Delivery { sent = now, arrive = now + delay, payload = CaptureCollisionPose(sender, p) }); sentPackets++;
                            }
                        }
                        Clock(now);
                        for (int i = deliveries.Count - 1; i >= 0; i--)
                        {
                            var item = deliveries[i]; if (item.arrive > now) continue;
                            if (item.sent < newestDelivered) reordered++;
                            newestDelivered = Math.Max(newestDelivered, item.sent);
                            receiver.OnPhotonSerializeView(new PhotonStream(false, item.payload), new PhotonMessageInfo(actor, (int)(item.sent * 1000), receiver.photonView));
                            if (frame >= 300) late.OnPhotonSerializeView(new PhotonStream(false, item.payload), new PhotonMessageInfo(actor, (int)(item.sent * 1000), late.photonView));
                            deliveries.RemoveAt(i);
                        }
                        playback(); if (frame >= 300) latePlayback();
                        if (frame >= 80) previous = CheckCollisionReceiver(receiver, p, obstacles, previous, frame > 80, ref maxStep, ref maxRemoteEdge);
                        if (frame >= 380) previousLate = CheckCollisionReceiver(late, p, obstacles, previousLate, frame > 380, ref maxStep, ref maxRemoteEdge);
                    }
                    if (scenario == CollisionCase.WallSlide || scenario == CollisionCase.WallAndCeiling) Check(maxX > 8, "Wall fixture failed to exercise sliding");
                    if (scenario == CollisionCase.InsideCorner || scenario == CollisionCase.ModeToggle) Check(maxX > 1.7f && maxX < 1.85f, "Corner fixture did not reach the corner");
                    if (scenario == CollisionCase.OutsideCorner) Check(maxX > 4, "Outside corner fixture did not round obstacle");
                    if (scenario != CollisionCase.CeilingLift) Check(blockingFrames > 0, "Fixture never exercised a blocking wall/corner");
                    if (lifting) Check(maxY > 2.7f && maxY < 2.85f, "Ceiling fixture did not reach ceiling");
                    if (scenario == CollisionCase.ModeToggle) Check(toggles == 4 && normalFrames == 120, "Both locomotion modes were not exercised");
                    if (adverse) Check(dropped > 10 && reordered > 10, "Loss/reordering not exercised");
                    Check(Vector3.Distance(Get<BodyPose>(receiver, "_lastPose").headPos, p.headCollider.transform.position) < .01f, "Remote did not settle at corrected final pose");
                    Check(Vector3.Distance(Get<BodyPose>(late, "_lastPose").headPos, p.headCollider.transform.position) < .01f, "Late join did not settle at corrected final pose");
                    Debug.Log($"NETWORK COLLISION {scenario} adverse={adverse}: packets={sentPackets}, dropped={dropped}, reordered={reordered}, toggles={toggles}, blockingFrames={blockingFrames}, localPenetration={maxLocalOverlap:F6}m, remoteHeadEdgeOverlap={maxRemoteEdge:F6}m, maxRemoteStep={maxStep:F6}m");
                }
                finally { Object.DestroyImmediate(sender.gameObject); Object.DestroyImmediate(receiver.gameObject); Object.DestroyImmediate(late.gameObject); }
            }
        }
        finally { foreach (var go in objects) Object.DestroyImmediate(go); }
    }
}
