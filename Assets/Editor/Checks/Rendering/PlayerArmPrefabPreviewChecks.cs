using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerArmPrefabPreviewChecks
{
    const string Output = ".utmp/PrefabArmPreview";
    static readonly string[] Paths = {
        "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab", "Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab"
    };
    static readonly string[] Skins = { "Default", "Rambro", "Ninja", "Gorilla" };
    static IEnumerator routine;
    static string folder;
    static int checks;

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Arm Prefab Preview")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying && PrefabStageUtility.GetCurrentPrefabStage() == null,
            "Run from the main stage outside Play mode");
        if (routine != null) throw new InvalidOperationException("Preview checks already running");
        Directory.CreateDirectory(Output);
        checks = 0;
        folder = "Assets/__ArmPreviewChecks_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        routine = CheckStages();
        EditorApplication.update += Advance;
    }

    static void Advance()
    {
        try
        {
            if (routine.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        EditorApplication.update -= Advance;
        routine = null;
        PlayerArmPrefabPreview.Clear();
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) StageUtility.GoToMainStage();
        if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        string report = error == null ? "PlayerArmPrefabPreviewChecks PASS: " + checks
            + " checks; four player routes/four skins, original mesh references and bone poses preserved, cached runtime-equivalent geometry, Undo, save/reopen and NPC exclusions."
            : "PlayerArmPrefabPreviewChecks FAILED: " + error;
        File.WriteAllText(Output + "/result.txt", report);
        if (error == null) Debug.Log(report); else Debug.LogException(error);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
    }

    static IEnumerator CheckStages()
    {
        for (int route = 0; route < Paths.Length; route++)
        {
            string originalText = File.ReadAllText(Paths[route]);
            string copy = folder + "/Player" + route + ".prefab";
            Require(AssetDatabase.CopyAsset(Paths[route], copy), "copy disposable prefab");
            var stage = PrefabStageUtility.OpenPrefab(copy);
            yield return null;
            var fit = stage.prefabContentsRoot.GetComponentsInChildren<PlayerArmGeometry>(true).First(g => g.enabled);
            var source = (SkinnedMeshRenderer)new SerializedObject(fit).FindProperty("body").objectReferenceValue;
            var transforms = source.bones.Distinct().ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            Require(!stage.scene.isDirty, "opening preview does not dirty prefab");
            CheckDisplay(fit, source);
            Render(stage, source, route + "-Default-authored-preview");
            PlayerArmPrefabPreview.Clear();
            Render(stage, source, route + "-Default-before");
            PlayerArmPrefabPreview.Refresh();
            var stableDisplay = Display(source);
            for (int i = 0; i < 3; i++) PlayerArmPrefabPreview.Refresh();
            Require(Display(source) == stableDisplay, "refresh reuses renderer");

            foreach (string skin in Skins)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Skins/DuckSkin_" + skin + "/DuckSkin_" + skin + ".fbx");
                var modelRenderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).First();
                Mesh before = source.sharedMesh;
                var map = source.bones.ToDictionary(t => t.name);
                Undo.RecordObject(source, "Preview skin check");
                source.sharedMesh = modelRenderer.sharedMesh;
                source.bones = modelRenderer.bones.Select(t => map[t.name]).ToArray();
                source.sharedMaterials = modelRenderer.sharedMaterials;
                Undo.FlushUndoRecordObjects();
                double deadline = EditorApplication.timeSinceStartup + 2;
                do { yield return null; } while (!DisplayMatches(source) && EditorApplication.timeSinceStartup < deadline);
                CheckDisplay(fit, source); // Real change event, not an explicit Refresh call.
                Render(stage, source, route + "-" + skin + "-preview");
                Undo.PerformUndo();
                deadline = EditorApplication.timeSinceStartup + 2;
                do { yield return null; } while (!DisplayMatches(source) && EditorApplication.timeSinceStartup < deadline);
                Require(source.sharedMesh == before, "Undo preserves original mesh reference");
                CheckDisplay(fit, source);
            }
            for (int i = 0; i < transforms.Length; i++)
                Require(transforms[i].localRotation == rotations[i] && transforms[i].localPosition == positions[i]
                    && transforms[i].localScale == scales[i], "bone pose unchanged: " + transforms[i].name);

            // Exercise the Prefab Mode save lifecycle on a disposable copy only.
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            typeof(PrefabStage).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(m => m.Name == "SavePrefab" && m.GetParameters().Length == 0).Invoke(stage, null);
            double saveDeadline = EditorApplication.timeSinceStartup + 2;
            do { yield return null; } while (!DisplayMatches(source) && EditorApplication.timeSinceStartup < saveDeadline);
            CheckDisplay(fit, source);
            Require(!File.ReadAllText(copy).Contains("Player arm mesh preview"), "preview objects never saved");
            var closedScene = stage.scene;
            StageUtility.GoToMainStage();
            yield return null;
            Require(!Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>().Any(r => IsDisplay(r) && r.gameObject.scene == closedScene),
                "closing stage destroys its previews");
            stage = PrefabStageUtility.OpenPrefab(copy);
            yield return null;
            fit = stage.prefabContentsRoot.GetComponentsInChildren<PlayerArmGeometry>(true).First(g => g.enabled);
            source = (SkinnedMeshRenderer)new SerializedObject(fit).FindProperty("body").objectReferenceValue;
            Require(AssetDatabase.Contains(source.sharedMesh), "reopened prefab retains persistent original mesh");
            CheckDisplay(fit, source);
            StageUtility.GoToMainStage();
            yield return null;
            Require(File.ReadAllText(Paths[route]) == originalText, "source prefab file unchanged");
        }
        foreach (string path in new[] { "Assets/Resources/DuckCompanion.prefab", "Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab" })
        {
            PrefabStageUtility.OpenPrefab(path);
            yield return null;
            Require(!Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>().Any(IsDisplay), "NPC/original corpse excluded");
            StageUtility.GoToMainStage();
        }
    }

    static bool IsDisplay(SkinnedMeshRenderer renderer) => renderer.name == "Player arm mesh preview"
        && (renderer.hideFlags & HideFlags.DontSaveInEditor) != 0;
    static SkinnedMeshRenderer Display(SkinnedMeshRenderer source) => source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(IsDisplay);

    static bool DisplayMatches(SkinnedMeshRenderer source)
    {
        var display = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(IsDisplay);
        return display != null && display.sharedMaterials.SequenceEqual(source.sharedMaterials)
            && display.bones.SequenceEqual(source.bones) && display.sharedMesh != source.sharedMesh
            && display.sharedMesh.name == source.sharedMesh.name + " (Player arms +10%)";
    }

    static void CheckDisplay(PlayerArmGeometry fit, SkinnedMeshRenderer source)
    {
        var display = Display(source);
        Require(AssetDatabase.Contains(source.sharedMesh), "saved renderer still uses source asset");
        Require(source.forceRenderingOff && !display.forceRenderingOff, "only corrected geometry visible");
        Require(display.bones.SequenceEqual(source.bones) && display.rootBone == source.rootBone, "same live bones");
        Require(display.sharedMesh == fit.Fit(source.sharedMesh, source.bones), "same cached runtime mesh");
        Require(display.sharedMaterials.SequenceEqual(source.sharedMaterials), "materials match");
        var original = source.sharedMesh.bindposes;
        var fitted = display.sharedMesh.bindposes;
        for (int i = 0; i < source.bones.Length; i++)
        {
            string name = source.bones[i].name;
            bool arm = name.StartsWith("upper_arm.") || name.StartsWith("forearm.");
            Require(fitted[i] == (arm ? Matrix4x4.Scale(new Vector3(1, 1.1f, 1)) * original[i] : original[i]),
                "arm-only fit; normal hands: " + name);
        }
    }

    static void Render(PrefabStage stage, SkinnedMeshRenderer source, string filename)
        => Render(stage.scene, stage.prefabContentsRoot, source, filename);

    internal static void Render(UnityEngine.SceneManagement.Scene scene, GameObject root, SkinnedMeshRenderer source, string filename)
    {
        // Temporary diagnostic materials allow the same check in a minimal built-in-renderer project.
        bool wasActive = root.activeSelf;
        root.SetActive(true); // The standalone corpse is authored inactive.
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var materials = renderers.Select(r => r.sharedMaterials).ToArray();
        var replacements = new System.Collections.Generic.List<Material>();
        var cameraObject = new GameObject("Preview check camera") { hideFlags = HideFlags.HideAndDontSave };
        var lightObject = new GameObject("Preview check light") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(1200, 700, 24);
        var image = new Texture2D(1200, 700, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original => {
                    var material = new Material(Shader.Find("Standard")); replacements.Add(material);
                    material.color = original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
                    if (original != null && original.HasProperty("_BaseMap")) material.mainTexture = original.GetTexture("_BaseMap");
                    return material;
                }).ToArray();
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            var bounds = source.bounds;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(.5f, bounds.extents.x * .7f);
            camera.transform.position = bounds.center + new Vector3(0, .05f, 3); camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f,.2f,.24f); camera.targetTexture = rt;
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = camera.transform.rotation;
            camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1200,700),0,0); image.Apply();
            var pixels = image.GetPixels32(); var background = pixels[0];
            Require(pixels.Count(p => p.r != background.r || p.g != background.g || p.b != background.b) > 1000,
                "capture contains visible geometry: " + filename);
            File.WriteAllBytes(Output + "/" + filename + ".png", image.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = materials[i];
            foreach (var material in replacements) Object.DestroyImmediate(material);
            RenderTexture.active = previous;
            Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
            root.SetActive(wasActive);
        }
    }

    static void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException("PlayerArmPrefabPreviewChecks: " + message); }
}
