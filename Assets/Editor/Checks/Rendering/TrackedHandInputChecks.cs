using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TrackedHandInputChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    public static int LastCheckCount => checks;
    sealed class Actor : Photon.Realtime.Player { public Actor() : base("tracked-hand regression", 91, false) { } }
    static TrackedHandInput.Curls Fist => new TrackedHandInput.Curls { thumb = 1, index = 1, middle = 1, ring = 1, pinky = 1 };

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Tracked Hand Input")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || PhotonNetwork.InRoom) throw new Exception("Run outside Play mode and a Photon room.");
        checks = 0;
        Gestures(); GestureEdges(); Geometry(); Integration(); Network();
        Debug.Log("TrackedHandInputChecks PASS " + checks + ": joint curl, gesture edges, post-grab trigger guard, tracking loss, controller isolation, item use and network curls.");
    }

    static void GestureEdges()
    {
        var hand = new TrackedHandInput(); hand.Enter(false); hand.Tick(default, true, .02f);
        var indexOnly = new TrackedHandInput.Curls { index = 1 };
        hand.Tick(indexOnly, true, .02f);
        Check(hand.Trigger.wasPressed && !hand.Grip.down, "index alone never grabs");
        hand.Tick(default, true, .02f);
        foreach (int missing in new[] { 0, 1, 2 })
        {
            var pose = Fist;
            if (missing == 0) pose.middle = 0;
            if (missing == 1) pose.ring = 0;
            if (missing == 2) pose.pinky = 0;
            hand.Tick(pose, true, .02f);
            Check(!hand.Grip.down, "all three grip fingers required: " + missing);
        }
        hand.Tick(default, true, .02f);
        hand.Tick(Fist, false, .05f);
        Check(!hand.Grip.down && !hand.Trigger.down, "invalid closed pose cannot begin actions");
        hand.Tick(Fist, true, .02f); hand.AfterGrab();
        hand.Tick(default, false, .1f);
        hand.Tick(Fist, true, .02f);
        Check(hand.Grip.down && !hand.Trigger.down && !hand.Grip.wasPressed, "brief loss preserves the post-grab guard");
        hand.Tick(default, false, .4f); hand.Tick(default, false, .4f);
        Check(!hand.Grip.wasReleased && !hand.Trigger.wasReleased, "prolonged loss emits release only once");
    }

    static void Gestures()
    {
        var h = new TrackedHandInput(); h.Enter(false);
        h.Tick(Fist, true, .02f);
        Check(!h.Grip.down && !h.Trigger.down, "entering with closed hand does not grab or fire");
        h.Tick(default, true, .02f);
        var pose = Fist; pose.thumb = pose.index = 0;
        h.Tick(pose, true, .02f);
        Check(h.Grip.wasPressed && !h.Trigger.down, "three grip fingers work with free thumb/index");
        h.Tick(Fist, true, .02f);
        Check(h.Grip.down && !h.Grip.wasPressed && h.Trigger.wasPressed, "index pulls trigger independently");
        h.AfterGrab();
        Check(!h.Trigger.down && h.TriggerValue == 0 && h.Visual.index > 0, "grab disarms use without resetting visual finger");
        for (int i = 0; i < 5; i++) h.Tick(Fist, true, .02f);
        Check(!h.Trigger.down && h.Grip.down, "closed fist cannot activate freshly grabbed item");
        h.Tick(pose, true, .02f); h.Tick(Fist, true, .02f);
        Check(h.Trigger.wasPressed && h.Trigger.down, "straighten then curl rearms trigger");
        var partial = Fist; partial.index = partial.middle = partial.ring = partial.pinky = .6f;
        h.Tick(partial, true, .02f);
        Check(h.Trigger.down && h.Grip.down && !h.Trigger.wasPressed, "threshold hysteresis prevents chatter");
        h.Tick(default, false, .1f);
        Check(h.Trigger.down && h.Grip.down && !h.Trigger.wasPressed && h.SuppressThrow, "brief loss holds state without edges");
        h.Tick(default, false, .3f);
        Check(h.Trigger.wasReleased && h.Grip.wasReleased && h.SuppressThrow, "long loss releases without throwing");
        h.Tick(Fist, true, .02f);
        Check(!h.Trigger.down && !h.Grip.down, "reacquired fist cannot reactivate");
        h.Tick(default, true, .1f); h.Tick(Fist, true, .1f);
        Check(h.Grip.wasPressed && h.Trigger.wasPressed && !h.SuppressThrow, "opening rearms after recovery");
        h.Tick(Fist, true, .02f, false);
        Check(h.Grip.wasReleased && h.Trigger.wasReleased && h.SuppressThrow, "focus loss releases immediately");
        h.Exit(); h.Enter(true); h.Tick(Fist, true, .02f);
        Check(h.Grip.down && !h.Grip.wasPressed && !h.Trigger.down, "controller hold can transition without a new grab");
        h.Tick(default, true, .02f);
        Check(h.Grip.wasReleased, "opening transitioned hand releases hold");
        for (int i = 0; i < 20; i++) h.Tick(default, true, .02f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) h.Tick(default, true, .02f);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "steady gesture sampling allocates no managed memory");
    }

    static void Geometry()
    {
        var expected = new TrackedHandInput.Curls { thumb = .3f, index = .8f, middle = .2f, ring = .9f, pinky = .5f };
        foreach (bool xr in new[] { false, true })
        foreach (float mirror in new[] { -1f, 1f })
        {
            var p = new Vector3[xr ? 26 : 24];
            Chain(p, xr ? 2 : 3, xr ? 5 : 19, expected.thumb * 60f);
            Chain(p, xr ? 7 : 6, xr ? 10 : 20, expected.index * 80f);
            Chain(p, xr ? 12 : 9, xr ? 15 : 21, expected.middle * 80f);
            Chain(p, xr ? 17 : 12, xr ? 20 : 22, expected.ring * 80f);
            Chain(p, xr ? 22 : 16, xr ? 25 : 23, expected.pinky * 80f);
            for (int i = 0; i < p.Length; i++) p[i] = Quaternion.Euler(35, 80, -110)
                * Vector3.Scale(p[i], new Vector3(mirror, 1, 1)) * .7f + new Vector3(2, 3, 4);
            Check(OvrHandCurlSource.TryMeasure(p, xr, out var got), "both SDK layouts accept valid joints");
            Near(got.thumb, expected.thumb, "independent thumb"); Near(got.index, expected.index, "independent index");
            Near(got.middle, expected.middle, "independent middle"); Near(got.ring, expected.ring, "independent ring"); Near(got.pinky, expected.pinky, "independent pinky");
            p[xr ? 8 : 7] = p[xr ? 7 : 6];
            Check(!OvrHandCurlSource.TryMeasure(p, xr, out _), "degenerate joints cannot produce input");
        }
        var bind = new OVRPlugin.Skeleton2 { NumBones = 24, Bones = new OVRPlugin.Bone[24] };
        var rotations = new OVRPlugin.Quatf[24];
        for (int i = 0; i < 24; i++)
        {
            bind.Bones[i].ParentBoneIndex = -1;
            bind.Bones[i].Pose.Orientation.w = rotations[i].w = 1;
        }
        foreach (int start in new[] { 3, 6, 9, 12, 16 })
        {
            int tip = start == 3 ? 19 : start == 6 ? 20 : start == 9 ? 21 : start == 12 ? 22 : 23;
            for (int k = 1; k < 4; k++)
            {
                int i = k == 3 ? tip : start + k;
                bind.Bones[i].ParentBoneIndex = (short)(start + k - 1);
                bind.Bones[i].Pose.Position.z = .03f;
                if (k < 3)
                {
                    var q = Quaternion.AngleAxis(start == 3 ? 60 : 80, Vector3.right);
                    rotations[i] = new OVRPlugin.Quatf { x = q.x, y = q.y, z = q.z, w = q.w };
                }
            }
        }
        var points = new Vector3[26]; var world = new Quaternion[26];
        Check(OvrHandCurlSource.BuildLegacyPositions(bind, rotations, points, world), "legacy hierarchy reconstructed");
        Check(OvrHandCurlSource.TryMeasure(points, false, out var fist), "legacy rotations yield measurable fist");
        Near(fist.thumb, 1, "legacy thumb curl"); Near(fist.index, 1, "legacy index curl"); Near(fist.Grip, 1, "legacy grip curl");
    }

    static void Chain(Vector3[] points, int start, int tip, float bend)
    {
        points[start] = new Vector3(start * .02f, 0, 0);
        points[start + 1] = points[start] + Vector3.forward * .03f;
        points[start + 2] = points[start + 1] + Quaternion.AngleAxis(bend, Vector3.right) * Vector3.forward * .025f;
        points[tip] = points[start + 2] + Quaternion.AngleAxis(bend * 2, Vector3.right) * Vector3.forward * .02f;
    }

    static void Integration()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var previousGripHandler = PlayerHand.GripHandler;
        GameObject root = null, itemRoot = null, replicaRoot = null;
        try
        {
            PlayerHand.GripHandler = null;
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"), scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var input = root.GetComponentInChildren<ControllerInput>(true);
            var driver = root.GetComponentInChildren<LocalBodyDriver>(true); driver.Body.InitializeIKPreview();
            itemRoot = new GameObject("Tracked hand input test item");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(itemRoot, scene);
            var item = itemRoot.AddComponent<PhotonGrabbable>(); Call(item, "Awake");
            var view = itemRoot.GetComponent<PhotonView>();
            typeof(PhotonView).GetProperty("IsMine").SetValue(view, true);
            if (PhotonNetwork.LocalPlayer != null) view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
            var flashlight = itemRoot.AddComponent<HandFlashlight>(); Call(flashlight, "Awake");
            var walkie = itemRoot.AddComponent<WalkieTalkie>(); Call(walkie, "Awake");
            replicaRoot = new GameObject("Remote tracked hand item");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replicaRoot, scene);
            var replica = replicaRoot.AddComponent<PhotonGrabbable>(); Call(replica, "Awake");
            typeof(PhotonView).GetProperty("IsMine").SetValue(replicaRoot.GetComponent<PhotonView>(), false);
            var remoteFlashlight = replicaRoot.AddComponent<HandFlashlight>(); Call(remoteFlashlight, "Awake");
            var remoteWalkie = replicaRoot.AddComponent<WalkieTalkie>(); Call(remoteWalkie, "Awake");
            foreach (var hand in root.GetComponentsInChildren<PlayerHand>(true))
            {
                Call(hand, "Awake");
                bool left = hand.IsControllerLTouch;
                Sample(input, left, default); Sample(input, left, Fist);
                Check(hand.TryGrab(item), "existing TryGrab succeeds from tracked grip");
                Sync(item, replica);
                Check(replica.IsHeldByAnyPlayer, "tracked grab reaches remote item through Photon callbacks");
                Check(!hand.WasUsePressed && !hand.IsUseHeld && !hand.IsTriggerHeld, "successful grab immediately suppresses trigger use");
                Sample(input, left, Fist); Call(flashlight, "Update"); Call(walkie, "Update");
                Check(!(bool)Get(flashlight, "isOn") && !(bool)Get(walkie, "transmitting"), "closed-fist pickup does not activate items");
                var openIndex = Fist; openIndex.index = 0;
                Sample(input, left, openIndex); Sample(input, left, Fist);
                Check(hand.WasUsePressed && hand.IsUseHeld && hand.IsTriggerHeld, "tracked index reaches every existing use API");
                Call(flashlight, "Update"); Call(walkie, "Update");
                Check((bool)Get(flashlight, "isOn"), "index press toggles flashlight");
                Check((bool)Get(walkie, "transmitting") == !PhotonVoiceChat.IsLocallyMuted, "index hold drives walkie respecting existing mute state");
                Sync(flashlight, remoteFlashlight); Sync(walkie, remoteWalkie);
                Check((bool)Get(remoteFlashlight, "isOn"), "trigger toggled light reaches remote peer");
                Check((bool)Get(remoteWalkie, "transmitting") == (bool)Get(walkie, "transmitting"), "walkie transmit state reaches remote peer");
                Sample(input, left, Fist); Call(flashlight, "Update");
                Check((bool)Get(flashlight, "isOn") && !hand.WasUsePressed, "holding index does not repeatedly toggle light");
                Sample(input, left, openIndex); Call(walkie, "Update");
                Check(!(bool)Get(walkie, "transmitting") && hand.IsHolding, "straight index stops walkie while fist retains item");
                Sync(walkie, remoteWalkie);
                Check(!(bool)Get(remoteWalkie, "transmitting"), "remote walkie stops after index release");
                Sample(input, left, Fist); Call(flashlight, "Update");
                Check(!(bool)Get(flashlight, "isOn"), "second curl toggles flashlight off");
                Sync(flashlight, remoteFlashlight);
                Check(!(bool)Get(remoteFlashlight, "isOn"), "second trigger toggle reaches remote light");
                var independent = new TrackedHandInput.Curls { thumb = .2f, index = .9f, middle = .4f, ring = 1, pinky = 1 };
                Sample(input, left, independent); driver.PoseNow();
                Check(input.TryGetTrackedFingerCurls(left, out var curls), "tracked finger pose exposed to body");
                var f = driver.Curl;
                Near(left ? f.leftThumb : f.rightThumb, curls.x, "body uses real thumb curl");
                Near(left ? f.leftIndex : f.rightIndex, curls.y, "body uses real index curl");
                Near(left ? f.leftPinky : f.rightPinky, curls.z, "middle animation independent of grip fingers");
                var packet = PosePacket.Capture(driver.HeadAnchor, driver.LeftPalmSource, driver.RightPalmSource, f);
                var decoded = (PosePacket)PosePacket.Deserialize(PosePacket.Serialize(packet));
                Check(Mathf.Abs((left ? decoded.Fingers.leftPinky : decoded.Fingers.rightPinky) - curls.z) <= 1f / 7f,
                    "existing network packet carries tracked middle curl");
                Sample(input, left, Fist); // Preserve held object until a loss-driven release.
                Set(hand, "posSamples", new[] { Vector3.zero, Vector3.one * 10, Vector3.zero, Vector3.zero, Vector3.zero });
                Set(hand, "rotSamples", new[] { Quaternion.identity, Quaternion.Euler(60, 0, 0), Quaternion.identity, Quaternion.identity, Quaternion.identity });
                Set(hand, "timeSamples", new[] { 0f, .01f, 0f, 0f, 0f });
                Set(hand, "sampleFilled", 2); Set(hand, "sampleWrite", 2);
                Sample(input, left, default, false, .4f);
                Call(hand, "LateUpdate");
                Check(!hand.IsHolding && item.Holder == null, "tracking loss releases through existing grip handler");
                Sync(item, replica);
                Check(!replica.IsHeldByAnyPlayer, "tracking-loss drop reaches remote item");
                Check(item.GetComponent<Rigidbody>().linearVelocity == Vector3.zero && item.GetComponent<Rigidbody>().angularVelocity == Vector3.zero,
                    "loss-driven release discards stale throw velocity");
                Sample(input, left, default); Sample(input, left, Fist);
                Check(hand.ForceGrab(item) && !hand.IsTriggerHeld, "ForceGrab also applies trigger guard");
                hand.Release();
                // Simulate the already-polled controller inputs and make sure hand mode clears cleanly.
                if (left) { input.leftGripButton = default; input.leftTriggerButton = default; input.leftTriggerValue = .23f; }
                else { input.rightGripButton = default; input.rightTriggerButton = default; input.rightTriggerValue = .23f; }
                Apply(input, left, false, default, false, .02f, true, new ButtonStatus { down = true }, default);
                Check(!input.TryGetTrackedFingerCurls(left, out _), "controller switch restores controller animation");
                Near(left ? input.leftTriggerValue : input.rightTriggerValue, .23f, "controller analog value preserved");
                var controllerButton = new ButtonStatus { down = true, wasPressed = true };
                if (left) input.leftTriggerButton = controllerButton; else input.rightTriggerButton = controllerButton;
                Apply(input, left, false, default, false, .02f, true, default, default);
                Check(hand.WasUsePressed && hand.IsTriggerHeld, "controller-only trigger edges remain untouched");
            }
            input.leftThumbstickValue = new Vector2(.3f, -.8f); input.rightThumbstickValue = new Vector2(-.4f, .6f);
            input.rightGripValue = .31f; input.rightTriggerValue = .62f;
            input.rightGripButton = new ButtonStatus { down = true, wasPressed = true };
            input.rightTriggerButton = new ButtonStatus { down = true, wasPressed = true };
            Sample(input, true, default); Sample(input, true, Fist);
            Check(input.rightGripButton.wasPressed && input.rightTriggerButton.wasPressed, "left hand tracking preserves right controller edges");
            Near(input.rightGripValue, .31f, "mixed-input controller grip analog");
            Near(input.rightTriggerValue, .62f, "mixed-input controller trigger analog");
            Check(input.leftThumbstickValue == new Vector2(.3f, -.8f) && input.rightThumbstickValue == new Vector2(-.4f, .6f),
                "hand gesture override never writes joystick movement values");
        }
        finally
        {
            PlayerHand.GripHandler = previousGripHandler;
            Object.DestroyImmediate(replicaRoot); Object.DestroyImmediate(itemRoot); Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Network()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null, senderRoot = null, peerRoot = null;
        try
        {
            GameObject Spawn(string path)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                return go;
            }
            root = Spawn("Assets/Prefabs/Player/DuckPlayer.prefab");
            senderRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab");
            peerRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab");
            var driver = root.GetComponentInChildren<LocalBodyDriver>(true);
            var input = driver.controllerInput;
            driver.Body.InitializeIKPreview();
            var sender = senderRoot.GetComponent<PhotonPlayer>();
            var peer = peerRoot.GetComponent<PhotonPlayer>();
            peerRoot.GetComponentInChildren<RemotePlayerBody>(true).InitializeIKPreview();
            Call(peerRoot.GetComponentInChildren<FingerRig>(true), "Awake");
            var actor = new Actor();
            sender.photonView.OwnerActorNr = peer.photonView.OwnerActorNr = actor.ActorNumber;
            typeof(PhotonView).GetProperty("IsMine").SetValue(sender.photonView, true);
            typeof(PhotonView).GetProperty("IsMine").SetValue(peer.photonView, false);
            sender.BindSources(driver.HeadAnchor, driver.LeftPalmSource, driver.RightPalmSource, driver);
            var channels = new[] { "leftThumb", "leftIndex", "leftPinky", "rightThumb", "rightIndex", "rightPinky" };
            var localSettings = new SerializedObject(driver);
            var remoteSettings = new SerializedObject(peerRoot.GetComponentInChildren<FingerRig>(true));
            foreach (byte skin in new byte[] { 0, 1, 2, 3 })
            {
                DuckSkinVisuals.Attach(driver.Body.gameObject, DuckSkinSource.Manual).Apply(skin);
                DuckSkinVisuals.Attach(peerRoot.GetComponentInChildren<RemotePlayerBody>(true).gameObject, DuckSkinSource.Manual).Apply(skin);
                for (int pose = 0; pose < 3; pose++)
                {
                    Sample(input, true, new TrackedHandInput.Curls { thumb = pose * .4f, index = .9f - pose * .3f, middle = .2f + pose * .3f });
                    Sample(input, false, new TrackedHandInput.Curls { thumb = .9f - pose * .3f, index = pose * .4f, middle = .8f - pose * .2f });
                    driver.PoseNow();
                    var writer = Writing(); sender.OnPhotonSerializeView(writer, default);
                    var payload = writer.ToArray();
                    byte[] bytes = PosePacket.Serialize(payload[0]);
                    Check(bytes.Length == 39, "tracked curls preserve network packet size");
                    payload[0] = PosePacket.Deserialize(bytes);
                    var sent = ((PosePacket)payload[0]).Fingers;
                    // A fresh receiver models late joining: its first valid packet must pose all fingers immediately.
                    Call(peer, "ResetPoseBuffer", false); Set(peer, "_hasPose", false);
                    var info = new PhotonMessageInfo(actor, PhotonNetwork.ServerTimestamp, peer.photonView);
                    peer.OnPhotonSerializeView(new PhotonStream(false, payload), info);
                    Check((bool)Get(peer, "_hasPose"), "actual Photon receive callback accepts tracked pose");
                    var received = ((BodyPose)Get(peer, "_lastPose")).fingers;
                    Near(received.leftThumb, sent.leftThumb, "remote left thumb wire value");
                    Near(received.leftIndex, sent.leftIndex, "remote left index wire value");
                    Near(received.leftPinky, sent.leftPinky, "remote left middle wire value");
                    Near(received.rightThumb, sent.rightThumb, "remote right thumb wire value");
                    Near(received.rightIndex, sent.rightIndex, "remote right index wire value");
                    Near(received.rightPinky, sent.rightPinky, "remote right middle wire value");
                    foreach (string channel in channels)
                    {
                        var localJoints = localSettings.FindProperty(channel).FindPropertyRelative("joints");
                        var remoteJoints = remoteSettings.FindProperty(channel).FindPropertyRelative("joints");
                        for (int j = 0; j < localJoints.arraySize; j++)
                        {
                            var localBone = (Transform)localJoints.GetArrayElementAtIndex(j).FindPropertyRelative("bone").objectReferenceValue;
                            var remoteBone = (Transform)remoteJoints.GetArrayElementAtIndex(j).FindPropertyRelative("bone").objectReferenceValue;
                            Check(Quaternion.Angle(localBone.localRotation, remoteBone.localRotation) < 7f,
                                "remote finger bones agree within existing 3-bit curl quantization: " + channel);
                        }
                    }
                    var changed = (PosePacket)payload[0]; changed.Fingers = default; payload[0] = changed;
                    peer.OnPhotonSerializeView(new PhotonStream(false, payload), info);
                    Check(((System.Collections.IList)Get(peer, "_buf")).Count == 1, "duplicate timestamp does not replay stale finger state");
                    Near(((BodyPose)Get(peer, "_lastPose")).fingers.leftIndex, sent.leftIndex, "duplicate pose does not reset remote curl");
                    Call(peer, "LateUpdate");
                    Near(((BodyPose)Get(peer, "_lastPose")).fingers.rightThumb, sent.rightThumb, "missing next snapshot preserves remote fingers");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(peerRoot); Object.DestroyImmediate(senderRoot); Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static PhotonStream Writing()
    {
        var stream = new PhotonStream(true, null);
        typeof(PhotonStream).GetMethod("SetWriteStream", Private).Invoke(stream, new object[] { new List<object>(), 0 });
        return stream;
    }
    static void Sync(IPunObservable source, IPunObservable receiver)
    {
        var stream = Writing(); source.OnPhotonSerializeView(stream, default);
        receiver.OnPhotonSerializeView(new PhotonStream(false, stream.ToArray()), default);
    }

    static void Sample(ControllerInput input, bool left, TrackedHandInput.Curls curls, bool valid = true, float dt = .1f)
        => Apply(input, left, true, curls, valid, dt, true, left ? input.leftGripButton : input.rightGripButton,
            left ? input.leftTriggerButton : input.rightTriggerButton);
    static void Apply(ControllerInput input, bool left, bool bare, TrackedHandInput.Curls curls, bool valid, float dt,
        bool focused, ButtonStatus grip, ButtonStatus trigger) => Call(input, "ApplyTrackedHand", left, bare, curls, valid, dt, focused, grip, trigger);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Near(float a, float b, string label) => Check(Mathf.Abs(a - b) < .001f, label + ": " + a + " vs " + b);
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("TrackedHandInputChecks: " + label); }
}
