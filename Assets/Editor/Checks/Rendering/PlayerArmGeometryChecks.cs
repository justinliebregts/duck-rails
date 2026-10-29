using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerArmGeometryChecks
{
    static readonly string[] Players = {
        "Assets/Prefabs/Player/DuckPlayer.prefab",
        "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab"
    };
    const string CorpsePath = "Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab";
    const string Output = ".utmp/player-arm-geometry";
    static readonly string[] Skins = { "Default", "Rambro", "Ninja", "Gorilla" };

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate and Render Player Arm Geometry")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run outside Play mode");
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.NewPreviewScene();
        var shared = new Dictionary<byte, Mesh>();
        try
        {
            for (int route = 0; route < 4; route++)
            for (byte id = 0; id < Skins.Length; id++)
                CheckPlayer(scene, route, id, shared);
            CheckExclusions(scene);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("PlayerArmGeometryChecks PASS: all 4 skins on VR, desktop, Photon and player ragdoll; shared/idempotent meshes; unchanged hand vertices, source meshes and transforms; NPC exclusions. Saved 48 posed renders to " + Output);
    }

    static void CheckPlayer(Scene scene, int route, byte id, Dictionary<byte, Mesh> shared)
    {
        var root = Spawn(Players[Math.Min(route, 2)], scene);
        GameObject corpseRoot = null;
        try
        {
            var body = root.GetComponentInChildren<RemotePlayerBody>(true);
            body.InitializeIKPreview();
            var visuals = DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Manual);
            visuals.Apply(id);
            GameObject rig = body.gameObject;
            Ragdoll corpse = null;
            if (route == 3)
            {
                corpseRoot = Spawn(CorpsePath, scene);
                corpse = corpseRoot.GetComponent<Ragdoll>();
                corpse.Park(); // Exercise dressing before activation/Awake.
                corpse.sourceHip = body.Chest;
                corpse.leftHandSource = body.LeftHand;
                corpse.rightHandSource = body.RightHand;
                corpse.ApplySkin(id);
                rig = corpseRoot;
            }
            var renderer = Main(rig);
            // Multiple snapshots run in one editor tick; refresh skinning for every render.
            renderer.updateWhenOffscreen = true;
            renderer.forceMatrixRecalculationPerRender = true;
            var fit = rig.GetComponent<PlayerArmGeometry>();
            Require(fit != null && fit.enabled, "human opt-in");
            Require(new SerializedObject(fit).FindProperty("body").objectReferenceValue == renderer, "Awake renderer reference");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Skins/DuckSkin_" + Skins[id] + "/DuckSkin_" + Skins[id] + ".fbx");
            var source = Main(asset).sharedMesh;
            Require(renderer.sharedMesh != source, "production Apply must fit " + Skins[id]);
            CheckMesh(source, renderer, fit);
            if (shared.TryGetValue(id, out Mesh previous)) Require(previous == renderer.sharedMesh, "same cached mesh across routes");
            else shared.Add(id, renderer.sharedMesh);

            // Hide only unrelated player equipment/UI, keeping skin face/accessories visible.
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(body.transform) || r.GetComponent<TMPro.TMP_Text>() != null) r.enabled = false;
            for (int pose = 0; pose < 3; pose++)
            {
                body.ApplyIKPreview(Pose(pose));
                if (corpse != null)
                {
                    corpse.PoseToPlayer(root.transform);
                    foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                    foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                Render(scene, pose, route + "-" + Skins[id] + "-" + pose);
            }
        }
        finally
        {
            if (corpseRoot != null) Object.DestroyImmediate(corpseRoot);
            Object.DestroyImmediate(root);
        }
    }

    static void CheckMesh(Mesh source, SkinnedMeshRenderer renderer, PlayerArmGeometry fit)
    {
        Mesh corrected = renderer.sharedMesh;
        var bones = renderer.bones;
        var originalPoses = source.bindposes;
        var poses = corrected.bindposes;
        Require(corrected.vertexCount == source.vertexCount && corrected.subMeshCount == source.subMeshCount, "topology unchanged");
        int armCount = 0;
        for (int i = 0; i < poses.Length; i++)
        {
            bool arm = bones[i] != null && IsArm(bones[i].name);
            Matrix4x4 expected = arm ? Matrix4x4.Scale(new Vector3(1, 1.1f, 1)) * originalPoses[i] : originalPoses[i];
            Require(poses[i] == expected, "only arm bind poses change: " + bones[i].name);
            if (arm) armCount++;
        }
        Require(armCount == 4, "four arm segments");
        var positions = bones.Select(b => b.localPosition).ToArray();
        var rotations = bones.Select(b => b.localRotation).ToArray();
        var scales = bones.Select(b => b.localScale).ToArray();
        Bounds bounds = renderer.localBounds;
        fit.Prepare(renderer);
        fit.Prepare(renderer);
        Require(renderer.sharedMesh == corrected && fit.Fit(source, bones) == corrected, "no double stretching or per-instance copies");
        Require(renderer.localBounds == bounds, "bounds do not compound");
        for (int i = 0; i < bones.Length; i++)
            Require(bones[i].localPosition == positions[i] && bones[i].localRotation == rotations[i] && bones[i].localScale == scales[i], "transforms untouched");

        var originalBake = new Mesh();
        var correctedBake = new Mesh();
        try
        {
            renderer.sharedMesh = source;
            renderer.BakeMesh(originalBake);
            renderer.sharedMesh = corrected;
            renderer.BakeMesh(correctedBake);
            var a = originalBake.vertices;
            var b = correctedBake.vertices;
            var weights = source.boneWeights;
            int handVertices = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                if ((w.weight0 == 0 || !IsArm(bones[w.boneIndex0].name)) &&
                    (w.weight1 == 0 || !IsArm(bones[w.boneIndex1].name)) &&
                    (w.weight2 == 0 || !IsArm(bones[w.boneIndex2].name)) &&
                    (w.weight3 == 0 || !IsArm(bones[w.boneIndex3].name)))
                {
                    Require(Vector3.Distance(a[i], b[i]) < .000001f, "non-arm vertex unchanged");
                    if (bones[w.boneIndex0].name.StartsWith("hand.")) handVertices++;
                }
            }
            Require(handVertices > 0, "hand vertices verified");
            Require(source.bindposes.SequenceEqual(originalPoses), "source asset never mutated");
        }
        finally { renderer.sharedMesh = corrected; Object.DestroyImmediate(originalBake); Object.DestroyImmediate(correctedBake); }
    }

    static void CheckExclusions(Scene scene)
    {
        foreach (string path in new[] {
            "Assets/Resources/DuckCompanion.prefab", "Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab",
            "Assets/Prefabs/Enemies/ZombieBomber.prefab", "Assets/Prefabs/Enemies/ZombieMinion.prefab",
            "Assets/Prefabs/Enemies/ZombieSmasher.prefab", "Assets/Prefabs/Enemies/ZombieSpider.prefab"
        })
        {
            var root = Spawn(path, scene);
            try
            {
                foreach (var fit in root.GetComponentsInChildren<PlayerArmGeometry>(true))
                {
                    Require(!fit.enabled, path + " must not opt in");
                    var r = Main(fit.gameObject);
                    Mesh before = r.sharedMesh;
                    fit.Prepare(r);
                    Require(r.sharedMesh == before && fit.Fit(before, r.bones) == before, "disabled companion no-op");
                }
                Require(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r.sharedMesh == null || AssetDatabase.Contains(r.sharedMesh)), "NPC meshes remain assets");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    static BodyPose Pose(int i) => new BodyPose {
        headPos = new Vector3(0, 1.6f, 0), headRot = Quaternion.identity,
        lHandPos = i == 0 ? new Vector3(-.3f, 1.2f, .48f) : i == 1 ? new Vector3(-.65f, 1.25f, .3f) : new Vector3(-.24f, 1.5f, .23f),
        rHandPos = i == 0 ? new Vector3(.3f, 1.2f, .48f) : i == 1 ? new Vector3(.65f, 1.25f, .3f) : new Vector3(.24f, 1.5f, .23f),
        lHandRot = i == 2 ? Quaternion.Euler(25, -20, -30) : Quaternion.identity,
        rHandRot = i == 2 ? Quaternion.Euler(25, 20, 30) : Quaternion.identity
    };

    static void Render(Scene scene, int pose, string filename)
    {
        var cameraGO = new GameObject("Arm validation camera");
        var lightGO = new GameObject("Arm validation light");
        SceneManager.MoveGameObjectToScene(cameraGO, scene);
        SceneManager.MoveGameObjectToScene(lightGO, scene);
        var target = new RenderTexture(1000, 800, 24);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            var camera = cameraGO.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic = true;
            camera.orthographicSize = .55f;
            camera.transform.position = new Vector3(pose == 2 ? 1.8f : -1.8f, 1.55f, 1.4f);
            camera.transform.LookAt(new Vector3(0, 1.3f, .20f));
            camera.nearClipPlane = .01f; camera.farClipPlane = 20;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .20f, .24f);
            camera.targetTexture = target;
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.transform.rotation = camera.transform.rotation;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0);
            image.Apply();
            File.WriteAllBytes(Output + "/" + filename + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            Object.DestroyImmediate(target); Object.DestroyImmediate(cameraGO); Object.DestroyImmediate(lightGO);
        }
    }

    static GameObject Spawn(string path, Scene scene)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return root;
    }
    static SkinnedMeshRenderer Main(GameObject root) => root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).First();
    static bool IsArm(string name) => name == "upper_arm.L" || name == "upper_arm.R" || name == "forearm.L" || name == "forearm.R";
    static void Require(bool condition, string message) { if (!condition) throw new Exception("PlayerArmGeometryChecks: " + message); }
}
