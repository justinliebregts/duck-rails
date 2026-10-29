using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerArmScenePreviewChecks
{
    static readonly string[] Players = {
        "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab", "Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab"
    };
    static Scene scene;
    static string folder, phase;
    static IEnumerator routine;
    static Func<bool> waiting;
    static double deadline;
    static int checks;

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Arm Scene Preview")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying && PrefabStageUtility.GetCurrentPrefabStage() == null, "Run from main stage outside Play mode");
        Require(Application.isBatchMode || !string.IsNullOrEmpty(SceneManager.GetActiveScene().path), "Save the untitled scene before running validation");
        if (routine != null) throw new InvalidOperationException("Checks already running");
        checks = 0;
        Directory.CreateDirectory(".utmp/SceneArmPreview");
        Directory.CreateDirectory(".utmp/PrefabArmPreview");
        folder = "Assets/__ArmSceneChecks_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), folder + "/Harness.unity");
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        routine = CheckScene();
        EditorApplication.update += Advance;
    }

    static void Advance()
    {
        try
        {
            if (waiting != null)
            {
                if (!waiting())
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out: " + phase);
                    return;
                }
                waiting = null;
            }
            if (routine.MoveNext()) { waiting = routine.Current as Func<bool>; deadline = EditorApplication.timeSinceStartup + 5; return; }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        EditorApplication.update -= Advance; routine = null; waiting = null;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) StageUtility.GoToMainStage();
        if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.DeleteAsset(folder);
        string report = error == null ? "PlayerArmScenePreviewChecks PASS: " + checks
            + " checks; scene preview across VR/desktop/Photon/corpse, duplicate/delete/Undo, skin changes, disable/reenable, save/reopen, prefab/main-stage transitions, unchanged bones/source meshes, NPC exclusions."
            : "PlayerArmScenePreviewChecks FAILED at " + phase + ": " + error;
        File.WriteAllText(".utmp/SceneArmPreview/result.txt", report);
        if (error == null) Debug.Log(report); else Debug.LogException(error);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
    }

    static IEnumerator CheckScene()
    {
        var roots = Players.Select(p => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p), scene)).ToArray();
        for (int i = 0; i < roots.Length; i++) { roots[i].name = "PreviewRoute" + i; roots[i].transform.position = new Vector3(i * 10, 0, 0); }
        phase = "initial scene previews";
        yield return (Func<bool>)(() => roots.All(Ready));
        foreach (var root in roots) Check(root);
        for (int i = 0; i < roots.Length; i++)
            PlayerArmPrefabPreviewChecks.Render(scene, roots[i], Source(roots[i].GetComponentsInChildren<PlayerArmGeometry>(true).First(g => g.enabled)), "scene-" + i);

        phase = "duplicate player";
        Undo.IncrementCurrentGroup();
        var duplicate = Object.Instantiate(roots[0]);
        SceneManager.MoveGameObjectToScene(duplicate, scene);
        duplicate.transform.position += Vector3.right * 40;
        Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate preview player");
        yield return (Func<bool>)(() => Ready(duplicate));
        Check(duplicate);
        phase = "delete/undo player";
        string name = duplicate.name;
        Undo.IncrementCurrentGroup();
        Undo.DestroyObjectImmediate(duplicate);
        yield return (Func<bool>)(() => !scene.GetRootGameObjects().Any(g => g.name == name));
        Undo.PerformUndo();
        phase = "restore deleted player";
        yield return (Func<bool>)(() => (duplicate = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name)) != null && Ready(duplicate));
        Check(duplicate);
        Object.DestroyImmediate(duplicate);

        var fit = roots[0].GetComponentsInChildren<PlayerArmGeometry>(true).First(g => g.enabled);
        var source = Source(fit);
        var poses = source.bones.Select(t => Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale)).ToArray();
        phase = "disable preview opt-in";
        Undo.RecordObject(fit, "Disable preview fit"); fit.enabled = false; Undo.FlushUndoRecordObjects();
        yield return (Func<bool>)(() => !Displays(source).Any());
        Require(!source.forceRenderingOff, "disabled opt-in restores source visibility");
        Undo.PerformUndo();
        phase = "reenable preview opt-in";
        yield return (Func<bool>)(() => Ready(roots[0]));

        phase = "skin change";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Skins/DuckSkin_Ninja/DuckSkin_Ninja.fbx")
            .GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).First();
        Mesh original = source.sharedMesh;
        var map = source.bones.ToDictionary(t => t.name);
        Undo.RecordObject(source, "Scene skin preview");
        source.sharedMesh = model.sharedMesh; source.bones = model.bones.Select(t => map[t.name]).ToArray(); source.sharedMaterials = model.sharedMaterials;
        Undo.FlushUndoRecordObjects();
        yield return (Func<bool>)(() => Ready(roots[0]));
        Check(roots[0]);
        Undo.PerformUndo();
        phase = "undo skin change";
        yield return (Func<bool>)(() => source.sharedMesh == original && Ready(roots[0]));
        for (int i = 0; i < source.bones.Length; i++)
            Require(Matrix4x4.TRS(source.bones[i].localPosition, source.bones[i].localRotation, source.bones[i].localScale) == poses[i], "bone pose unchanged");

        foreach (string path in new[] { "Assets/Resources/DuckCompanion.prefab", "Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab",
            "Assets/Prefabs/Enemies/ZombieBomber.prefab", "Assets/Prefabs/Enemies/ZombieMinion.prefab",
            "Assets/Prefabs/Enemies/ZombieSmasher.prefab", "Assets/Prefabs/Enemies/ZombieSpider.prefab" })
        {
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            PlayerArmPrefabPreview.Refresh();
            Require(!npc.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(IsDisplay), "NPC excluded: " + path);
            Object.DestroyImmediate(npc);
        }
        phase = "apply prefab overrides";
        string applyPath = folder + "/ApplyTarget.prefab";
        Require(AssetDatabase.CopyAsset(Players[0], applyPath), "copy prefab for Apply test");
        var applyRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(applyPath), scene);
        applyRoot.transform.position = Vector3.right * 40;
        PrefabUtility.RecordPrefabInstancePropertyModifications(applyRoot.transform);
        yield return (Func<bool>)(() => Ready(applyRoot));
        PrefabUtility.ApplyPrefabInstance(applyRoot, InteractionMode.AutomatedAction);
        yield return (Func<bool>)(() => Ready(applyRoot));
        Require(!File.ReadAllText(applyPath).Contains("Player arm mesh preview"), "Apply overrides never saves preview objects");
        Check(applyRoot);
        Object.DestroyImmediate(applyRoot);
        phase = "save scene";
        string scenePath = folder + "/Preview.unity";
        Require(EditorSceneManager.SaveScene(scene, scenePath), "scene saved");
        yield return (Func<bool>)(() => roots.All(Ready));
        Require(!scene.isDirty, "preview does not dirty saved scene");
        Require(!File.ReadAllText(scenePath).Contains("Player arm mesh preview"), "preview objects never serialized");
        var closed = scene;
        EditorSceneManager.CloseScene(scene, true);
        Require(!Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>().Any(r => IsDisplay(r) && r.gameObject.scene == closed), "closed scene previews destroyed");
        phase = "reopen scene";
        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        roots = scene.GetRootGameObjects();
        yield return (Func<bool>)(() => roots.All(Ready));
        foreach (var root in roots) Check(root);
        Require(!scene.isDirty, "reopening does not dirty scene");

        phase = "prefab stage";
        PrefabStageUtility.OpenPrefab(Players[0]);
        yield return null;
        Require(!roots.SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Any(IsDisplay), "main scene previews parked in Prefab Mode");
        StageUtility.GoToMainStage();
        phase = "return to main stage";
        yield return (Func<bool>)(() => roots.All(Ready));
        foreach (var root in roots) Check(root);
    }

    static SkinnedMeshRenderer Source(PlayerArmGeometry fit) => (SkinnedMeshRenderer)new SerializedObject(fit).FindProperty("body").objectReferenceValue;
    static bool IsDisplay(SkinnedMeshRenderer r) => r.name == "Player arm mesh preview" && (r.hideFlags & HideFlags.DontSaveInEditor) != 0;
    static SkinnedMeshRenderer[] Displays(SkinnedMeshRenderer source) => source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => IsDisplay(r) && r.transform.parent == source.transform).ToArray();
    static bool Ready(GameObject root) => root != null && root.GetComponentsInChildren<PlayerArmGeometry>(true).Where(f => f.enabled).All(f => {
        var s = Source(f); var display = Displays(s);
        return display.Length == 1 && s.forceRenderingOff && !display[0].forceRenderingOff
            && display[0].sharedMesh == f.Fit(s.sharedMesh, s.bones) && display[0].sharedMaterials.SequenceEqual(s.sharedMaterials);
    });
    static void Check(GameObject root)
    {
        Require(Ready(root), "one matching visible preview per source");
        foreach (var fit in root.GetComponentsInChildren<PlayerArmGeometry>(true).Where(f => f.enabled))
        {
            var source = Source(fit); var display = Displays(source).Single();
            Require(AssetDatabase.Contains(source.sharedMesh), "source mesh still an asset");
            Require(display.bones.SequenceEqual(source.bones), "preview follows original bones");
            var original = source.sharedMesh.bindposes; var fitted = display.sharedMesh.bindposes;
            for (int i = 0; i < source.bones.Length; i++)
            {
                string n = source.bones[i].name;
                bool arm = n.StartsWith("upper_arm.") || n.StartsWith("forearm.");
                Require(fitted[i] == (arm ? Matrix4x4.Scale(new Vector3(1, 1.1f, 1)) * original[i] : original[i]), "only arm geometry fitted");
            }
        }
    }
    static void Require(bool ok, string message) { checks++; if (!ok) throw new Exception("PlayerArmScenePreviewChecks: " + message); }
}
