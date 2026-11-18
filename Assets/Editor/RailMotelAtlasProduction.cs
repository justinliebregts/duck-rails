#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Approved atlas integration; original source objects remain recoverable in place.</summary>
public static class RailMotelAtlasProduction
{
    public const string Prefab = "Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
    public const string Visual = "Optimized Motel atlas";
    const string Review = "Assets/DuckRails/MotelLowPolyAtlasReview/Motel-LowPoly-Atlas-Review.prefab";
    const string ReviewVisual = "Blender low-poly Motel - atlas review";
    const string AssetsFolder = "Assets/DuckRails/MotelLowPolyAtlas";
    const string Art = "ArtSource/MotelAtlasLowPoly/";
    const string StatePath = Art + "production-state.json";
    [Serializable] public class State { public string guid, fingerprint; }

    static Transform Resolve(Transform root, string key)
    {
        foreach (var i in key.Split('/')) root = root.GetChild(int.Parse(i));
        return root;
    }
    static string AssetKey(Object obj) => obj ? GlobalObjectId.GetGlobalObjectIdSlow(obj) + ":" +
        AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(obj)) : "null";

    public static string Fingerprint(GameObject root)
    {
        var text = new StringBuilder();
        // Includes originals and final chunks so material, geometry or lighting edits cannot
        // silently ship stale derived meshes. Uses persistent asset identities, not instance IDs.
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            text.Append(AnimationUtility.CalculateTransformPath(r.transform, root.transform));
            text.Append((root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix).ToString("R"));
            for (var t = r.transform; t != root.transform; t = t.parent) text.Append(t.gameObject.activeSelf);
            text.Append(r.enabled).Append(r.forceRenderingOff).Append(r.gameObject.layer)
                .Append(r.shadowCastingMode).Append(r.receiveShadows).Append(r.lightProbeUsage)
                .Append(r.reflectionProbeUsage).Append(r.renderingLayerMask).Append(r.sortingLayerID)
                .Append(r.sortingOrder).Append(r.motionVectorGenerationMode);
            var filter = r.GetComponent<MeshFilter>();
            text.Append(AssetKey(filter ? filter.sharedMesh : null));
            foreach (var mat in r.sharedMaterials) text.Append(AssetKey(mat));
        }
        var lighting = root.GetComponent<RailBakedBlockLighting>();
        text.Append(lighting.lightingScene);
        text.Append(lighting.supplementalLightingScene);
        foreach (var b in lighting.bindings)
            text.Append(b.renderer ? AnimationUtility.CalculateTransformPath(b.renderer.transform, root.transform) : "missing")
                .Append(AssetKey(b.color)).Append(b.scaleOffset.ToString("R"));
        return Hash128.Compute(text.ToString()).ToString();
    }

    public static void Integrate()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Use idle Duck Rails Edit Mode.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Prefab Mode before integrating saved assets.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Preserve pending scene edits before integrating saved assets.");
        var data = JsonUtility.FromJson<RailMotelLowPolyAtlasReview.Payload>(File.ReadAllText(Art + "unity-meshes.json"));
        if (File.Exists(StatePath) || AssetDatabase.GetAssetDependencyHash(Prefab).ToString() != data.sourceHash)
            throw new InvalidOperationException("Already integrated or source changed since review. Inspect before continuing.");
        File.Copy(Prefab, Art + "pre-integration.prefab", false);
        File.Copy(Prefab + ".meta", Art + "pre-integration.prefab.meta.txt", false);
        string guid = AssetDatabase.AssetPathToGUID(Prefab);
        if (!AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.CreateFolder("Assets/DuckRails", "MotelLowPolyAtlas");
        foreach (string name in new[] { "Motel_BaseColor.png", "Motel_MetallicSmoothness.png", "MotelAtlas.mat", "MotelAtlasMeshes.asset" })
        {
            string error = AssetDatabase.MoveAsset("Assets/DuckRails/MotelLowPolyAtlasReview/" + name, AssetsFolder + "/" + name);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        var reviewed = AssetDatabase.LoadAssetAtPath<GameObject>(Review);
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            if (root.transform.Find(Visual)) throw new InvalidOperationException("Unexpected render override.");
            // Resolve child-position source keys before removing the last root child.
            var sources = data.sourceKeys.Select(k => Resolve(root.transform, k).GetComponent<MeshRenderer>()).ToArray();
            if (sources.Any(r => !r || !r.enabled)) throw new InvalidOperationException("Source renderer state changed.");
            // The atlas supersedes the generated render chunks (RailMotelRenderChunks).
            // Remove the runtime switch and its inactive root so they cannot double-render.
            var opt = root.GetComponent<RailSceneryChunks>();
            if (opt)
            {
                if (opt.chunkRoot && opt.chunkRoot.transform.GetSiblingIndex() != root.transform.childCount - 1)
                    throw new InvalidOperationException("Generated render chunks are not the last root child; removing them would shift source keys.");
                if (opt.chunkRoot) Object.DestroyImmediate(opt.chunkRoot);
                Object.DestroyImmediate(opt);
            }
            var template = reviewed.transform.Find(ReviewVisual);
            var clone = Object.Instantiate(template.gameObject, root.transform, false);
            clone.name = Visual;
            var owner = root.GetComponent<RailBakedBlockLighting>();
            var bindings = owner.bindings.ToList();
            foreach (var b in reviewed.GetComponent<RailBakedBlockLighting>().bindings.Where(b => b.renderer && b.renderer.transform.IsChildOf(template)))
            {
                string relative = AnimationUtility.CalculateTransformPath(b.renderer.transform, template);
                bindings.Add(new RailBakedBlockLighting.Binding { renderer = clone.transform.Find(relative).GetComponent<Renderer>(), color = b.color, scaleOffset = b.scaleOffset });
            }
            foreach (var r in sources) r.enabled = false;
            owner.bindings = bindings.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (AssetDatabase.AssetPathToGUID(Prefab) != guid) throw new InvalidOperationException("Production GUID changed.");
        File.WriteAllText(StatePath, JsonUtility.ToJson(new State { guid = guid, fingerprint = Fingerprint(saved) }, true));
        ValidateSaved();
    }

    public static void RequireOriginalLightingWorkflow(string path)
    {
        if (path != Prefab) return;
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root && root.transform.Find(Visual))
            throw new InvalidOperationException("The Motel uses approved derived atlas UVs. Restore original rendering/bindings and regenerate the atlas for a new bake; see ArtSource/MotelAtlasLowPoly/README.md (lights-only changes: docs/atlas-lights-only-rebake.md). Existing lighting must not be re-exported over the integrated prefab.");
    }

    [MenuItem("Duck Rails/Authoring/Motel/Validate production atlas")]
    public static void ValidateSaved()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var visual = root ? root.transform.Find(Visual) : null;
        if (!visual && !File.Exists(StatePath)) return; // Not yet integrated.
        if (!visual || !visual.gameObject.activeSelf || root.GetComponent<RailSceneryChunks>()) throw new BuildFailedException("Missing/overridden Motel production atlas.");
        var state = JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
        if (state.guid != AssetDatabase.AssetPathToGUID(Prefab) || state.fingerprint != Fingerprint(root))
            throw new BuildFailedException("Motel atlas/source/lighting changed since approved integration. Reconcile and regenerate derived visuals before building.");
        var data = JsonUtility.FromJson<RailMotelLowPolyAtlasReview.Payload>(File.ReadAllText(Art + "unity-meshes.json"));
        if (data.sourceKeys.Any(k => Resolve(root.transform, k).GetComponent<MeshRenderer>().enabled)) throw new BuildFailedException("Duplicate source rendering.");
        var chunks = visual.GetComponentsInChildren<MeshRenderer>(true);
        var lighting = root.GetComponent<RailBakedBlockLighting>();
        // 1,964 original bindings + 298 atlas/repeat-exception chunks; one atlas + 13 repeat materials.
        const int expectedBindings = 2262;
        if (chunks.Length != 298 || chunks.Any(r => !r.enabled || !r.sharedMaterial || !r.GetComponent<MeshFilter>().sharedMesh) ||
            chunks.Select(r => r.sharedMaterial).Distinct().Count() != 14 || lighting.bindings.Length != expectedBindings ||
            chunks.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3) != 79948 ||
            lighting.bindings.Any(b => !b.renderer || !b.color) || lighting.bindings.Select(b => b.renderer).Distinct().Count() != expectedBindings)
            throw new BuildFailedException("Invalid Motel atlas chunks or lighting bindings.");
        Debug.Log($"Motel production atlas authoring guard passed: 298 chunks, {data.sourceKeys.Length} disabled originals, {expectedBindings} valid bindings, 79948 atlas triangles.");
    }
}

public sealed class RailMotelAtlasBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => RailMotelAtlasProduction.ValidateSaved();
}
#endif
