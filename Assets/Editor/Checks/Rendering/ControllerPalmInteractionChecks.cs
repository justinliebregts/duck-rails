using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated physics/interaction checks using real player prefabs and PUN callbacks.
public static class ControllerPalmInteractionChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        checks = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = Spawn("Assets/Prefabs/Player/DuckPlayer.prefab");
        var ownerRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        var peerRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        var oldPlayer = GR.Player.instance; var oldMovement = NewPlayer.instance;
        var active = new List<PhotonPlayer>(PhotonPlayer.Active);
        try {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var player = root.GetComponent<GR.Player>(); GR.Player.instance = player;
            var movement = root.GetComponent<NewPlayer>(); NewPlayer.instance = movement;
            var driver = root.GetComponentInChildren<LocalBodyDriver>(true);
            var body = driver.Body; body.InitializeIKPreview();
            var owner = ownerRoot.GetComponent<PhotonPlayer>(); var peer = peerRoot.GetComponent<PhotonPlayer>();
            Mine(owner.photonView, true); Mine(peer.photonView, false);

            owner.photonView.OwnerActorNr = peer.photonView.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
            typeof(PhotonView).GetProperty("Owner").SetValue(owner.photonView, PhotonNetwork.LocalPlayer);
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.Add(owner);
            owner.BindSources(driver.HeadAnchor, driver.LeftPalmSource, driver.RightPalmSource, driver);
            var hands = new[] { player.leftPlayerHand, player.rightPlayerHand };
            foreach (var hand in hands) Call(hand, "Awake");
            driver.HeadAnchor.SetPositionAndRotation(new Vector3(0, 2.5f, 0), Quaternion.identity);
            Pose(driver, hands, 0);
            foreach (var hand in hands) Call(hand, "TryAttachHoldPointToSkin");
            movement.InitializeValues();
            var skins = DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Manual);
            var samples = new Vector3[Grabbable.MaxLocomotionSamples];
            foreach (byte skin in new byte[] { 0, 1, 2, 3 })
            foreach (float angle in new[] { 0f, 90f, -90f, 180f }) {
                skins.Apply(skin); Pose(driver, hands, angle);
                foreach (var hand in hands) {
                    bool right = hand.IsControllerRTouch;
                    var raw = right ? driver.RightHandAnchor : driver.LeftHandAnchor;
                    var source = right ? driver.RightPalmSource : driver.LeftPalmSource;
                    var item = Item(true); var replica = Item(false);
                    try {
                        item.transform.position = hand.HoldPoint.position + Vector3.right * (hand.GrabTolerance + .019f);
                        Physics.SyncTransforms();
                        Check(hand.FindGrabCandidate() == item, "inside grab radius selects calibrated hand's item");
                        item.transform.position = hand.HoldPoint.position + Vector3.right * (hand.GrabTolerance + .021f);
                        Physics.SyncTransforms();
                        Check(hand.FindGrabCandidate() != item, "outside grab radius rejected");
                        item.transform.SetPositionAndRotation(hand.HoldPoint.position, source.rotation * Quaternion.Euler(15, 25, 5));
                        Check(hand.TryGrab(item) && item.Holder == hand, "actual grab succeeds");
                        Check(item.transform.parent == hand.HoldPoint && item.GetComponent<Rigidbody>().isKinematic, "item attaches to visible hand");
                        Near(item.transform.lossyScale, Vector3.one, "normal held-item size");
                        Vector3 position = item.transform.position; Quaternion rotation = item.transform.rotation;
                        skins.Apply((byte)((skin + 1) % 4)); driver.PoseNow();
                        Near(item.transform.position, position, "skin swap while holding preserves grip");
                        Check(Quaternion.Angle(item.transform.rotation, rotation) < .05f, "skin swap preserves item rotation");
                        skins.Apply(skin); driver.PoseNow();
                        Check(hand.TryGetLocomotionShape(samples, out int count, out float radius) && count > 0 && radius > 0, "held item supplies climbing geometry");
                        Vector3 centre = Vector3.zero; for (int i = 0; i < count; i++) centre += samples[i]; centre /= count;
                        Near(Current(movement, right), centre, "climbing uses held-item geometry");
                        Near(right ? movement.lastRightHandPosition : movement.lastLeftHandPosition, centre, "grab seeds anchor without yank");
                        CheckNetwork(owner, peer, item, replica);
                        CheckStretchedHold(driver, hand, owner, peer, item, replica);

                        // Seed the item behind a wall, then move the real controller through it.
                        Vector3 tracked = raw.position;
                        raw.position -= Vector3.forward * .25f; driver.PoseNow(); movement.SnapHandLocomotionAnchor(right);
                        raw.position = tracked; driver.PoseNow();
                        WallContact(movement, right, Current(movement, right));

                        var other = right ? hands[0] : hands[1];
                        position = item.transform.position;
                        Check(other.TryGrab(item) && !hand.IsHolding && other.IsHolding, "hand transfer clears old holder");
                        Near(item.transform.position, position, "transfer preserves world grip");
                        CheckNetwork(owner, peer, item, replica);
                        other.Release();
                        Check(!item.IsHeld && !item.GetComponent<Rigidbody>().isKinematic, "release restores physics");
                        Near(item.GetComponent<Rigidbody>().linearVelocity, Vector3.zero, "stationary release adds no velocity");
                        Check(!hand.TryGetLocomotionShape(samples, out _, out _), "release stops held climbing");
                        item.transform.position = hand.HoldPoint.position;
                        Check(hand.TryGrab(item), "regrab for throw sampling");
                        Call(hand, "SamplePose"); raw.position += Vector3.right * .02f; driver.PoseNow(); Call(hand, "SamplePose");
                        var times = (float[])Get(hand, "timeSamples"); times[0] = Time.time - .02f; times[1] = Time.time;
                        hand.Release();
                        Near(item.GetComponent<Rigidbody>().linearVelocity, Vector3.right, "2 cm / 20 ms throw remains 1 m/s", .002f);
                        Near(item.GetComponent<Rigidbody>().angularVelocity, Vector3.zero, "translation throw has no false spin");
                        raw.position = tracked; driver.PoseNow();
                    }
                    finally { hand.Release(); foreach (var h in hands) if (h.Held == item) h.Release(); Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(replica.gameObject); }

                    Vector3 expected = raw.position + raw.rotation * (right ? movement.rightHandOffset : movement.leftHandOffset);
                    Vector3 head = movement.headCollider.transform.position;
                    expected = head + Vector3.ClampMagnitude(expected - head, movement.maxArmLength);
                    Near(Current(movement, right), expected, "bare-hand collision retains raw controller mapping");
                    Vector3 firstPull = Vector3.zero;
                    for (int mode = 0; mode < 2; mode++) {
                        driver.controllerInput.mouseGrip = mode == 1; driver.PoseNow();
                        Near(Current(movement, right), expected, "calibration does not move bare-hand collision anchor");
                        if (right) movement.lastRightHandPosition = expected - Vector3.forward * .25f;
                        else movement.lastLeftHandPosition = expected - Vector3.forward * .25f;
                        Vector3 pull = WallContact(movement, right, expected);
                        if (mode == 0) firstPull = pull; else Near(pull, firstPull, "bare climbing pull unchanged by calibration");
                    }
                    driver.controllerInput.mouseGrip = false; driver.PoseNow();
                    if (right) movement.lastRightHandPosition = expected - Vector3.right * (movement.unStickDistance + .2f);
                    else movement.lastLeftHandPosition = expected - Vector3.right * (movement.unStickDistance + .2f);
                    object[] release = { right, true }; Call(movement, "TryUnstickHand", release);
                    Check(!(bool)release[1], "hand un-sticks when pulled away");
                    movement.SnapHandLocomotionAnchor(right);
                }
            }
            Debug.Log("ControllerPalmInteractionChecks PASS " + checks + ": four skins, both hands, four rotations; grab boundaries, transfers, throws, actual held/bare wall sweeps, unstick, PUN held-item callbacks.");
            Directory.CreateDirectory(".utmp/ControllerCalibration");
            File.WriteAllText(".utmp/ControllerCalibration/interactions.txt", "PASS " + checks + "; isolated physics and PUN serialization callbacks, not live server delivery.\n");
        }
        finally {
            Object.DestroyImmediate(root); Object.DestroyImmediate(ownerRoot); Object.DestroyImmediate(peerRoot);
            GR.Player.instance = oldPlayer; NewPlayer.instance = oldMovement;
            PhotonPlayer.Active.Clear(); PhotonPlayer.Active.AddRange(active);
        }
    }

    static void Pose(LocalBodyDriver driver, PlayerHand[] hands, float angle)
    {
        driver.LeftHandAnchor.SetPositionAndRotation(new Vector3(-.22f, 2.23f, .3f), Quaternion.Euler(angle, -30, -20));
        driver.RightHandAnchor.SetPositionAndRotation(new Vector3(.22f, 2.23f, .3f), Quaternion.Euler(angle, 30, 20));
        foreach (var hand in hands) {
            var raw = hand.IsControllerLTouch ? driver.LeftHandAnchor : driver.RightHandAnchor;
            hand.transform.SetPositionAndRotation(raw.position, raw.rotation);
        }
        driver.PoseNow();
    }

    static Vector3 WallContact(NewPlayer movement, bool right, Vector3 centre)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = LayerMask.NameToLayer("Environment");
        wall.transform.position = centre + Vector3.forward * .01f; wall.transform.localScale = new Vector3(.8f, .8f, .02f);
        Physics.SyncTransforms();
        try {
            object[] args = { right, Vector3.zero, 1f / 60f, true, new Queue<Vector3>(), 0f, Vector3.zero, false };
            Check((bool)Call(movement, "TryHandLocomotionFirstPass", args) && (bool)args[7], "actual climbing sweep contacts wall");
            Check(((Vector3)args[6]).sqrMagnitude > .000001f, "wall contact produces pull correction");
            return (Vector3)args[6];
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
    }

    static void CheckNetwork(PhotonPlayer owner, PhotonPlayer peer, PhotonGrabbable item, PhotonGrabbable replica)
    {
        var poseStream = Writing(); owner.OnPhotonSerializeView(poseStream, default);
        var decoded = (PosePacket)PosePacket.Deserialize(PosePacket.Serialize((PosePacket)poseStream.ToArray()[0]));
        // Supply the decoded playback pose; transport/interpolation is covered separately.
        Set(peer, "_lastPose", decoded.ToBodyPose()); Set(peer, "_hasPose", true);
        var stream = Writing(); item.OnPhotonSerializeView(stream, default);
        Check(stream.ToArray().Length == 11, "Rails held-item schema includes recovery revision; train cargo serializes separately");
        replica.OnPhotonSerializeView(new PhotonStream(false, stream.ToArray()), default);
        Set(replica, "remoteOwner", peer); Call(replica, "UpdateItemPose");
        Near(replica.transform.position, item.transform.position, "remote held item matches calibrated owner", .003f);
        Check(Quaternion.Angle(replica.transform.rotation, item.transform.rotation) < .2f, "remote held rotation matches");
        Near(replica.transform.lossyScale, Vector3.one, "remote item size unchanged");
    }

    static void CheckStretchedHold(LocalBodyDriver driver, PlayerHand hand, PhotonPlayer owner, PhotonPlayer peer,
        PhotonGrabbable item, PhotonGrabbable replica)
    {
        var arm = hand.IsControllerLTouch ? driver.Body.LeftArm : driver.Body.RightArm;
        var raw = hand.IsControllerLTouch ? driver.LeftHandAnchor : driver.RightHandAnchor;
        Vector3 saved = raw.position, savedHand = hand.transform.position;
        Vector3 grip = item.transform.localPosition;
        var collider = item.GetComponent<BoxCollider>();
        Vector3 size = collider.size, centre = collider.center;
        try
        {
            foreach (float reach in new[] { .45f, .62f, .70f, .76f, .9f, .45f })
            {
                Vector3 target = (Vector3)arm.RootPos + (hand.IsControllerLTouch ? Vector3.left : Vector3.right) * reach;
                raw.position = target; hand.transform.position = target;
                driver.PoseNow();
                Near(raw.position, target, "visual solve leaves tracked/physics anchor untouched");
                Near(item.transform.localPosition, grip, "stretch preserves held grip offset");
                Near(item.transform.lossyScale, Vector3.one, "stretch never scales held items");
                Check(collider.size == size && collider.center == centre, "stretch does not edit item collider dimensions");
                Check(Vector3.Distance(arm.RootPos, arm.EndPos) <= arm.TotalReach * 1.4f + .0001f, "held reach capped at 1.4x");
                CheckNetwork(owner, peer, item, replica);
            }
        }
        finally { raw.position = saved; hand.transform.position = savedHand; driver.PoseNow(); }
    }

    static PhotonGrabbable Item(bool mine)
    {
        var go = new GameObject("Palm interaction item"); go.layer = 8;
        var view = go.AddComponent<PhotonView>(); go.AddComponent<PhotonTransformView>();
        var box = go.AddComponent<BoxCollider>(); box.size = Vector3.one * .04f;
        var item = go.AddComponent<PhotonGrabbable>(); Set(item, "locomotionCollider", box); Set(item, "locomotionWhileHeld", true);
        Call(item, "Awake"); Mine(view, mine); view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        if (!mine) { go.transform.position = Vector3.one * 100; box.enabled = false; }
        return item;
    }
    static PhotonStream Writing() { var stream = new PhotonStream(true, null); typeof(PhotonStream).GetMethod("SetWriteStream", Private).Invoke(stream, new object[] { new List<object>(), 0 }); return stream; }
    static Vector3 Current(NewPlayer movement, bool right) => (Vector3)Call(movement, right ? "CurrentRightHandPosition" : "CurrentLeftHandPosition");
    static GameObject Spawn(string path) { var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); return root; }
    static FieldInfo Field(Type type, string name) { while (type != null) { var field = type.GetField(name, Private); if (field != null) return field; type = type.BaseType; } throw new Exception(name); }
    static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Mine(PhotonView view, bool value) => typeof(PhotonView).GetProperty("IsMine").SetValue(view, value);
    static void Near(Vector3 a, Vector3 b, string label, float tolerance = .0002f) => Check(Vector3.Distance(a, b) < tolerance, label + ": " + Vector3.Distance(a, b));
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("ControllerPalmInteractionChecks: " + label); }
}
