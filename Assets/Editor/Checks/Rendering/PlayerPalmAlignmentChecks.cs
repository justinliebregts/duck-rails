using System;
using System.Linq;
using System.Reflection;
using Game.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerPalmAlignmentChecks
{
    const float Reach = .6067576f;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Players = {
        "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab"
    };
    static readonly string[] Skins = { "Default", "Rambro", "Ninja", "Gorilla" };
    static int checks;

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Palm Alignment")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run outside Play mode");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string path in Players) CheckPlayer(scene, path);
            foreach (string path in new[] {
                "Assets/Resources/DuckCompanion.prefab", "Assets/Prefabs/Enemies/ZombieBomber.prefab",
                "Assets/Prefabs/Enemies/ZombieMinion.prefab", "Assets/Prefabs/Enemies/ZombieSmasher.prefab",
                "Assets/Prefabs/Enemies/ZombieSpider.prefab"
            }) CheckNPC(scene, path);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("PlayerPalmAlignmentChecks PASS: " + checks + " checks; all skins on VR/desktop/Photon; rotated palms, bounded reach, unchanged hand geometry, corpse poses, held-item anchors, zero pose allocations, NPC exclusions.");
    }

    static void CheckPlayer(Scene scene, string path)
    {
        var root = Spawn(path, scene);
        var corpseRoot = Spawn("Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab", scene);
        try
        {
            var body = root.GetComponentInChildren<RemotePlayerBody>(true);
            Require(body.TracksPalms, path + " palm opt-in");
            body.InitializeIKPreview();
            var skin = DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Manual);
            var corpse = corpseRoot.GetComponent<Ragdoll>();
            corpse.sourceHip = body.Chest; corpse.leftHandSource = body.LeftHand; corpse.rightHandSource = body.RightHand;
            corpse.Park();
            var hands = root.GetComponentsInChildren<PlayerHand>(true);
            var offsets = new[] { body.LeftHand.localPosition, body.RightHand.localPosition,
                body.LeftHand.parent.localPosition, body.RightHand.parent.localPosition };
            foreach (byte id in new byte[] { 0, 1, 2, 3, 0, 3 })
            {
                skin.Apply(id);
                var renderer = Main(body.gameObject);
                var source = Main(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Skins/DuckSkin_" + Skins[id] + "/DuckSkin_" + Skins[id] + ".fbx"));
                var originalPoses = source.sharedMesh.bindposes; var fittedPoses = renderer.sharedMesh.bindposes;
                for (int i = 0; i < renderer.bones.Length; i++)
                    if (!renderer.bones[i].name.StartsWith("upper_arm.") && !renderer.bones[i].name.StartsWith("forearm."))
                        Require(originalPoses[i] == fittedPoses[i], "hand/non-arm geometry remains unchanged: " + renderer.bones[i].name);

                for (int p = 0; p < 5; p++)
                {
                    var pose = Pose(p);
                    body.ApplyIKPreview(pose);
                    var targets = new[] { (Vector3)pose.lHandPos, (Vector3)pose.rHandPos };
                    var rotations = new[] { (Quaternion)pose.lHandRot, (Quaternion)pose.rHandRot };
                    int side = 0;
                    foreach (var arm in new[] { body.LeftArm, body.RightArm })
                    {
                        var h = arm.EndBone;
                        Near(Palm(h), targets[side], "palm meets rotated tracking target");
                        Require(Mathf.Abs(Vector3.Distance(h.position, targets[side]) - .03731f) < .00002f, "wrist is behind palm by measured offset");
                        var endOffset = new SerializedObject(arm).FindProperty("endRotOffset").quaternionValue;
                        Require(Quaternion.Angle(h.rotation, rotations[side] * endOffset) < .05f, "authored hand rotation preserved");
                        Near(h.lossyScale, Vector3.one * .7f, "normal hand scale");
                        Require(Mathf.Abs(Vector3.Distance(h.position, h.parent.position) + Vector3.Distance(h.parent.position, h.parent.parent.position) - Reach) < .00002f, "arm length unchanged");
                        side++;
                    }
                    Require(body.LeftHand.localPosition == offsets[0] && body.RightHand.localPosition == offsets[1]
                        && body.LeftHand.parent.localPosition == offsets[2] && body.RightHand.parent.localPosition == offsets[3], "bone offsets unchanged");
                    CheckHoldPoints(hands, body, pose);

                    corpse.ApplySkin(id); corpse.PoseToPlayer(root.transform);
                    Near(Palm(corpse.leftHandTarget), Palm(body.LeftHand), "left corpse palm copies living pose once");
                    Near(Palm(corpse.rightHandTarget), Palm(body.RightHand), "right corpse palm copies living pose once");
                    Require(Main(corpseRoot).sharedMesh == renderer.sharedMesh, "corpse and living skin share corrected mesh");
                    corpse.Park();
                }
            }

            var far = Pose(0); far.lHandPos = new Vector3(-10, 2, 10); far.rHandPos = new Vector3(10, 2, 10);
            body.ApplyIKPreview(far);
            Require(Vector3.Distance(body.LeftArm.RootPos, body.LeftHand.position) <= Reach + .00002f
                && Vector3.Distance(body.RightArm.RootPos, body.RightHand.position) <= Reach + .00002f, "far palm targets do not stretch arms");
            var normal = Pose(0);
            for (int i = 0; i < 16; i++) body.ApplyIKPreview(normal);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) body.ApplyIKPreview(normal);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(bytes == 0, path + " pose hot path managed allocation: " + bytes);
        }
        finally { Object.DestroyImmediate(corpseRoot); Object.DestroyImmediate(root); }
    }

    static void CheckHoldPoints(PlayerHand[] hands, RemotePlayerBody body, BodyPose pose)
    {
        foreach (var hand in hands)
        {
            bool left = hand.IsControllerLTouch;
            hand.transform.SetPositionAndRotation(left ? (Vector3)pose.lHandPos : (Vector3)pose.rHandPos,
                left ? (Quaternion)pose.lHandRot : (Quaternion)pose.rHandRot);
            if (hand.HoldPoint == hand.transform)
            {
                typeof(PlayerHand).GetMethod("EnsureHoldPoint", Private).Invoke(hand, null);
                typeof(PlayerHand).GetMethod("TryAttachHoldPointToSkin", Private).Invoke(hand, null);
            }
            var h = left ? body.LeftHand : body.RightHand;
            var settings = new SerializedObject(hand);
            Vector3 offset = settings.FindProperty("holdLocalPosition").vector3Value;
            Quaternion rotation = hand.transform.rotation * Quaternion.Euler(settings.FindProperty("holdLocalEuler").vector3Value);
            Near(hand.HoldPoint.position, h.position + hand.transform.rotation * offset, "held point stays attached to the corrected hand");
            Require(Quaternion.Angle(hand.HoldPoint.rotation, rotation) < .05f, "held item orientation preserved");
            Near(hand.HoldPoint.lossyScale, Vector3.one, "held items keep normal scale");
        }
    }

    static void CheckNPC(Scene scene, string path)
    {
        var root = Spawn(path, scene);
        try
        {
            foreach (var body in root.GetComponentsInChildren<RemotePlayerBody>(true))
            {
                Require(!body.TracksPalms, path + " stays on wrist targets");
                body.InitializeIKPreview(); var pose = Pose(0); body.ApplyIKPreview(pose);
                Near(body.LeftHand.position, pose.lHandPos, path + " original left wrist target");
                Near(body.RightHand.position, pose.rHandPos, path + " original right wrist target");
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    static BodyPose Pose(int i) => new BodyPose {
        headPos = new Vector3(0, 2.5f, 0), headRot = Quaternion.identity,
        lHandPos = new Vector3(-.27f, 2.1f, .32f), rHandPos = new Vector3(.27f, 2.1f, .32f),
        lHandRot = Quaternion.Euler(i * 35, i * -40, i * 55), rHandRot = Quaternion.Euler(i * -35, i * 40, i * -55)
    };
    static Vector3 Palm(Transform hand) => hand.TransformPoint(RemotePlayerBody.PlayerPalmPoint);
    static SkinnedMeshRenderer Main(GameObject root) => root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).First();
    static GameObject Spawn(string path, Scene scene)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return root;
    }
    static void Near(Vector3 a, Vector3 b, string label) => Require(Vector3.Distance(a, b) < .0002f, label + ": " + Vector3.Distance(a, b));
    static void Require(bool value, string label) { checks++; if (!value) throw new Exception("PlayerPalmAlignmentChecks: " + label); }
}
