using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ControllerPalmCalibrationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Controller Palm Calibration")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run outside Play mode");
        checks = 0;
        Require(typeof(LocalBodyDriver).Assembly.GetType("HeadsetHandDiagnostics") == null, "temporary diagnostics removed from runtime");
        var scene = EditorSceneManager.NewPreviewScene();
        try {
            var root = Spawn("Assets/Prefabs/Player/DuckPlayer.prefab", scene);
            var remoteRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab", scene);
            var ownerRoot = Spawn("Assets/Prefabs/Photon/PhotonPlayer.prefab", scene);
            var corpseRoot = Spawn("Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab", scene);
            var remoteCorpseRoot = Spawn("Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab", scene);
            try {
                var driver = root.GetComponentInChildren<LocalBodyDriver>(true);
                var body = driver.Body;
                var remote = remoteRoot.GetComponentInChildren<RemotePlayerBody>(true);
                body.InitializeIKPreview(); remote.InitializeIKPreview();
                Vector3 offset = new Vector3(0f, -.028f, 0f);
                Require(ControllerPalmCalibration.ControllerLocalOffset == offset, "permanent 2.8 cm Y calibration");
                var left = driver.LeftPalmSource; var right = driver.RightPalmSource;
                var owner = ownerRoot.GetComponent<PhotonPlayer>();
                owner.BindSources(driver.HeadAnchor, left, right, driver);
                typeof(PhotonView).GetProperty("IsMine").SetValue(owner.photonView, true);
                var skin = DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Manual);
                var remoteSkin = DuckSkinVisuals.Attach(remote.gameObject, DuckSkinSource.Manual);
                var corpse = corpseRoot.GetComponent<Ragdoll>(); var remoteCorpse = remoteCorpseRoot.GetComponent<Ragdoll>();
                BindCorpse(corpse, body); BindCorpse(remoteCorpse, remote);
                var hands = root.GetComponentsInChildren<PlayerHand>(true);
                bool holdPointsReady = false;
                Vector3 rawLeft = new Vector3(-.22f, 2.23f, .3f), rawRight = new Vector3(.22f, 2.23f, .3f);
                driver.HeadAnchor.SetPositionAndRotation(new Vector3(0, 2.5f, 0), Quaternion.identity);
                Vector3 leftBone = body.LeftHand.localPosition, rightBone = body.RightHand.localPosition;
                foreach (byte id in new byte[] { 0, 1, 2, 3 }) {
                    skin.Apply(id); remoteSkin.Apply(id); corpse.ApplySkin(id); remoteCorpse.ApplySkin(id);
                    foreach (float spacing in new[] { .04f, .22f, .32f })
                    foreach (float angle in new[] { 0f, 45f, 90f, -90f, 180f }) {
                        rawLeft.x = -spacing; rawRight.x = spacing;
                        driver.LeftHandAnchor.SetPositionAndRotation(rawLeft, Quaternion.Euler(angle, -30, -20));
                        driver.RightHandAnchor.SetPositionAndRotation(rawRight, Quaternion.Euler(angle, 30, 20));
                        foreach (var hand in hands) {
                            Transform raw = hand.IsControllerLTouch ? driver.LeftHandAnchor : driver.RightHandAnchor;
                            hand.transform.SetPositionAndRotation(raw.position, raw.rotation);
                        }
                        driver.PoseNow();
                        // Match production: first hand attachment is after the first solved body pose.
                        if (!holdPointsReady) {
                            foreach (var hand in hands) {
                                typeof(PlayerHand).GetMethod("EnsureHoldPoint", Private).Invoke(hand, null);
                                typeof(PlayerHand).GetMethod("TryAttachHoldPointToSkin", Private).Invoke(hand, null);
                            }
                            holdPointsReady = true;
                        }
                        Near(driver.LeftHandAnchor.position, rawLeft, "left raw locomotion anchor unchanged");
                        Near(driver.RightHandAnchor.position, rawRight, "right raw locomotion anchor unchanged");
                        Near(left.position, rawLeft + driver.LeftHandAnchor.rotation * new Vector3(-offset.x, offset.y, offset.z), "left offset in controller frame");
                        Near(right.position, rawRight + driver.RightHandAnchor.rotation * offset, "right offset in controller frame");
                        Require(left == owner.HandSource(true) && right == owner.HandSource(false), "network source identity stable across poses and skins");
                        Require(Quaternion.Angle(left.rotation, driver.LeftHandAnchor.rotation) < .01f, "source rotation unchanged");

                        var stream = new PhotonStream(true, null);
                        // PUN initializes its reusable write list separately from the constructor.
                        var written = new List<object>();
                        typeof(PhotonStream).GetMethod("SetWriteStream", Private).Invoke(stream, new object[] { written, 0 });
                        owner.OnPhotonSerializeView(stream, default);
                        var packet = (PosePacket)written[0];
                        byte[] bytes = (byte[])PosePacket.Serialize(packet);
                        Require(bytes.Length == 39, "existing pose packet size retained");
                        var decoded = (PosePacket)PosePacket.Deserialize(bytes);
                        Near(decoded.LHandPos, left.position, "Photon sends calibrated left once", .002f);
                        Near(decoded.RHandPos, right.position, "Photon sends calibrated right once", .002f);
                        Require(owner.TryGetHandPose(true, out Vector3 sentLeft, out _) && Vector3.Distance(sentLeft, left.position) < .0001f, "owner interaction pose matches stream");
                        remote.ApplyIKPreview(decoded.ToBodyPose());
                        CheckArm(body.LeftArm, left, body.LeftHand.localPosition == leftBone);
                        CheckArm(body.RightArm, right, body.RightHand.localPosition == rightBone);
                        Near(Palm(remote.LeftHand), Palm(body.LeftHand), "remote left palm agrees", .003f);
                        Near(Palm(remote.RightHand), Palm(body.RightHand), "remote right palm agrees", .003f);
                        foreach (var hand in hands) {
                            bool isLeft = hand.IsControllerLTouch;
                            Transform source = owner.HandSource(isLeft);
                            Transform bone = isLeft ? body.LeftHand : body.RightHand;
                            Vector3 holdOffset = new SerializedObject(hand).FindProperty("holdLocalPosition").vector3Value;
                            Near(hand.HoldPoint.position, bone.position + hand.transform.rotation * holdOffset, "held-item point remains attached to calibrated hand");
                            Vector3 grip = Quaternion.Inverse(source.rotation) * (hand.HoldPoint.position - source.position);
                            Vector3 peerGrip = (Vector3)(isLeft ? decoded.LHandPos : decoded.RHandPos)
                                + (Quaternion)(isLeft ? decoded.LHandRot : decoded.RHandRot) * grip;
                            Near(peerGrip, hand.HoldPoint.position, "remote held-item reconstruction matches", .003f);
                        }
                        corpse.PoseToPlayer(root.transform); remoteCorpse.PoseToPlayer(remoteRoot.transform);
                        Near(Palm(corpse.leftHandTarget), Palm(body.LeftHand), "local ragdoll inherits calibration");
                        Near(Palm(remoteCorpse.leftHandTarget), Palm(corpse.leftHandTarget), "Photon ragdoll agrees", .003f);
                        Near(Palm(remoteCorpse.rightHandTarget), Palm(corpse.rightHandTarget), "Photon right ragdoll agrees", .003f);
                        corpse.Park(); remoteCorpse.Park();
                    }
                }
                CheckHandTrackingRotation(driver, remote, left, right);
                driver.LeftHandAnchor.position = driver.HeadAnchor.position + Vector3.left * 3f;
                driver.PoseNow();
                Require(Vector3.Distance(body.LeftArm.RootPos, body.LeftHand.position) <= body.LeftArm.TotalReach + .0001f,
                    "calibration cannot stretch arm past existing reach");
                Near(driver.LeftPalmSource.position, driver.LeftHandAnchor.position + driver.LeftHandAnchor.rotation * offset, "far controller source keeps fixed calibration");

                driver.controllerInput.mouseGrip = true;
                Require(driver.LeftPalmSource == left && driver.RightPalmSource == right, "mode switch retains bound network transforms");
                Near(left.position, driver.LeftHandAnchor.position, "desktop bypasses calibration after mode change");
                Near(right.position, driver.RightHandAnchor.position, "desktop right bypasses calibration");
                driver.controllerInput.mouseGrip = false;
                Near(driver.LeftPalmSource.position, driver.LeftHandAnchor.position + driver.LeftHandAnchor.rotation * offset, "return to VR restores fixed calibration");
                Vector3 originalScale = driver.LeftHandAnchor.localScale;
                driver.LeftHandAnchor.localScale = originalScale * 2f;
                Near(driver.LeftPalmSource.position, driver.LeftHandAnchor.position + driver.LeftHandAnchor.rotation * offset, "controller scale does not double physical offset");
                driver.LeftHandAnchor.localScale = originalScale;

                var originalLeft = driver.LeftHandAnchor;
                var replacement = new GameObject("Replacement tracked anchor");
                SceneManager.MoveGameObjectToScene(replacement, scene);
                try {
                    replacement.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(20, 30, 40));
                    driver.BindAnchors(driver.HeadAnchor, replacement.transform, driver.RightHandAnchor);
                    Require(driver.LeftPalmSource == left && owner.HandSource(true) == left, "anchor rebind retains network source identity");
                    Near(left.position, replacement.transform.position + replacement.transform.rotation * offset, "rebound source has fixed offset");
                    driver.BindAnchors(driver.HeadAnchor, originalLeft, driver.RightHandAnchor);
                }
                finally { Object.DestroyImmediate(replacement); }
                for (int i = 0; i < 20; i++) { _ = driver.LeftPalmSource; _ = driver.RightPalmSource; }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) { _ = driver.LeftPalmSource; _ = driver.RightPalmSource; }
                Require(GC.GetAllocatedBytesForCurrentThread() == before, "source sampling is allocation-free");
                Require(left.GetComponents<Component>().Length == 1 && right.GetComponents<Component>().Length == 1, "sources are transforms only, no physics/network components");

                var npc = Spawn("Assets/Resources/DuckCompanion.prefab", scene);
                try {
                    var npcDriver = npc.AddComponent<LocalBodyDriver>();
                    npcDriver.controllerInput = driver.controllerInput;
                    npcDriver.BindAnchors(driver.HeadAnchor, driver.LeftHandAnchor, driver.RightHandAnchor, npc.GetComponentInChildren<RemotePlayerBody>(true));
                    Require(npcDriver.LeftPalmSource == driver.LeftHandAnchor && npcDriver.RightPalmSource == driver.RightHandAnchor, "companion excluded even if given a driver");
                }
                finally { Object.DestroyImmediate(npc); }

                var desktop = Spawn("Assets/Prefabs/Player/DesktopPlayer.prefab", scene);
                CursorLockMode cursorLock = Cursor.lockState;
                bool cursorVisible = Cursor.visible;
                try {
                    // Desktop creates its controller anchors and driver in Awake, not in the prefab.
                    typeof(DesktopPlayer).GetMethod("Awake", Private).Invoke(desktop.GetComponent<DesktopPlayer>(), null);
                    var desktopDriver = desktop.GetComponentInChildren<LocalBodyDriver>(true);
                    Require(desktopDriver.LeftPalmSource == desktopDriver.LeftHandAnchor && desktopDriver.RightPalmSource == desktopDriver.RightHandAnchor,
                        "desktop prefab retains original mapping without proxy transforms");
                }
                finally { Object.DestroyImmediate(desktop); Cursor.lockState = cursorLock; Cursor.visible = cursorVisible; }
                Object.DestroyImmediate(root);
                Require(left == null && right == null, "player destruction cleans up cached sources");
            }
            finally { Object.DestroyImmediate(corpseRoot); Object.DestroyImmediate(remoteCorpseRoot); Object.DestroyImmediate(ownerRoot); Object.DestroyImmediate(remoteRoot); Object.DestroyImmediate(root); }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Directory.CreateDirectory(".utmp/ControllerCalibration");
        File.WriteAllText(".utmp/ControllerCalibration/result.txt", "PASS " + checks + " checks: permanent -2.8 cm Y, four skins, rotated/close/far targets, raw tracking unchanged, Photon serialization and held-item frames, ragdolls, desktop/NPC exclusions, reach/scale/rebinding/cleanup, zero allocation.\n");
        Debug.Log("ControllerPalmCalibrationChecks PASS " + checks + ": includes bare-hand orientation, tracking loss, input switching and Photon rotation parity");
    }

    static void CheckHandTrackingRotation(LocalBodyDriver driver, RemotePlayerBody remote, Transform left, Transform right)
    {
        var calibration = (ControllerPalmCalibration)typeof(LocalBodyDriver).GetField("_palmCalibration", Private).GetValue(driver);
        var sample = typeof(ControllerPalmCalibration).GetMethod("Source", Private);
        Transform Sample(bool isLeft, OVRInput.Controller active, bool tracked = true)
            => (Transform)sample.Invoke(calibration, new object[] { isLeft, active, tracked });
        var body = driver.Body;
        foreach (var worldTurn in new[] { Quaternion.identity, Quaternion.Euler(25, 80, -40), Quaternion.Euler(180, -60, 30) })
        {
            driver.LeftHandAnchor.SetPositionAndRotation(new Vector3(-.22f, 2.23f, .3f), worldTurn);
            driver.RightHandAnchor.SetPositionAndRotation(new Vector3(.22f, 2.23f, .3f), worldTurn);
            Require(Sample(true, OVRInput.Controller.LHand) == left && Sample(false, OVRInput.Controller.RHand) == right,
                "hand tracking retains bound source transforms");
            driver.PoseNow();
            // In the authored rig, -X/+X are the left/right palm normals, Y points toward the fingers,
            // and Z points toward the thumb. Neutral tracked hands previously had inward palms/upward thumbs.
            Near(body.LeftHand.rotation * Vector3.left, worldTurn * Vector3.down, "left tracked palm faces down");
            Near(body.RightHand.rotation * Vector3.right, worldTurn * Vector3.down, "right tracked palm faces down");
            Near(body.LeftHand.rotation * Vector3.forward, worldTurn * Vector3.right, "left tracked thumb points inward");
            Near(body.RightHand.rotation * Vector3.forward, worldTurn * Vector3.left, "right tracked thumb points inward");
            foreach (var hand in new[] { body.LeftHand, body.RightHand })
                Near(hand.rotation * Vector3.up, worldTurn * Vector3.forward, "roll keeps finger direction");
            Near(left.position, driver.LeftHandAnchor.position + worldTurn * ControllerPalmCalibration.ControllerLocalOffset, "orientation fix leaves left position mapping unchanged");
            Near(right.position, driver.RightHandAnchor.position + worldTurn * ControllerPalmCalibration.ControllerLocalOffset, "orientation fix leaves right position mapping unchanged");
            Require(Quaternion.Angle(driver.LeftHandAnchor.rotation, worldTurn) < .01f
                && Quaternion.Angle(driver.RightHandAnchor.rotation, worldTurn) < .01f, "raw tracking rotations unchanged");
            var packet = PosePacket.Capture(driver.HeadAnchor, left, right, default);
            var decoded = (PosePacket)PosePacket.Deserialize((byte[])PosePacket.Serialize(packet));
            remote.ApplyIKPreview(decoded.ToBodyPose());
            Require(Quaternion.Angle(remote.LeftHand.rotation, body.LeftHand.rotation) < .05f
                && Quaternion.Angle(remote.RightHand.rotation, body.RightHand.rotation) < .05f, "Photon applies hand correction exactly once");
            Quaternion heldLeft = left.rotation, heldRight = right.rotation;
            Sample(true, OVRInput.Controller.None, false);
            Sample(false, OVRInput.Controller.RTouch, false);
            Require(Quaternion.Angle(left.rotation, heldLeft) < .01f && Quaternion.Angle(right.rotation, heldRight) < .01f,
                "tracking loss retains rotation until raw anchor updates");
            Sample(true, OVRInput.Controller.LTouch);
            Require(Quaternion.Angle(left.rotation, worldTurn) < .01f && Quaternion.Angle(right.rotation, heldRight) < .01f,
                "mixed input changes only the controller hand");
            Sample(false, OVRInput.Controller.RTouch);
            driver.PoseNow();
            Require(Quaternion.Angle(left.rotation, worldTurn) < .01f && Quaternion.Angle(right.rotation, worldTurn) < .01f,
                "returning to controllers removes both corrections");
            CheckArm(body.LeftArm, left, true); CheckArm(body.RightArm, right, true);
        }
        Sample(true, OVRInput.Controller.LHand); Sample(false, OVRInput.Controller.RHand);
        driver.controllerInput.mouseGrip = true;
        Require(driver.LeftPalmSource == left && driver.RightPalmSource == right, "desktop transition retains source identity");
        Require(Quaternion.Angle(left.localRotation, Quaternion.identity) < .01f
            && Quaternion.Angle(right.localRotation, Quaternion.identity) < .01f, "desktop clears hand-tracking correction");
        driver.controllerInput.mouseGrip = false;
        Sample(true, OVRInput.Controller.LTouch); Sample(false, OVRInput.Controller.RTouch);
    }

    static void BindCorpse(Ragdoll corpse, RemotePlayerBody body) { corpse.sourceHip = body.Chest; corpse.leftHandSource = body.LeftHand; corpse.rightHandSource = body.RightHand; corpse.Park(); }
    static void CheckArm(TwoBoneLimb arm, Transform target, bool sameOffset)
    {
        Require(sameOffset, "authored hand bone offset retained");
        Near(Palm(arm.EndBone), target.position, "local palm reaches calibrated source");
        Require(Mathf.Abs(arm.TotalReach - .6067576f) < .00003f, "arm length retained");
        Require(Mathf.Abs(Vector3.Distance(arm.EndBone.position, target.position) - .03731f) < .00003f, "palm-to-wrist correction retained");
        Near(arm.EndBone.lossyScale, Vector3.one * .7f, "hand size retained");
        Quaternion endOffset = new SerializedObject(arm).FindProperty("endRotOffset").quaternionValue;
        Require(Quaternion.Angle(arm.EndBone.rotation, target.rotation * endOffset) < .05f, "IK hand rotation offset retained");
    }
    static Vector3 Palm(Transform hand) => hand.TransformPoint(RemotePlayerBody.PlayerPalmPoint);
    static GameObject Spawn(string path, Scene scene) { var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene); PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); return root; }
    static void Near(Vector3 a, Vector3 b, string label, float tolerance = .0002f) => Require(Vector3.Distance(a, b) < tolerance, label + " " + Vector3.Distance(a, b));
    static void Require(bool value, string label) { checks++; if (!value) throw new Exception("ControllerPalmCalibrationChecks: " + label); }
}
