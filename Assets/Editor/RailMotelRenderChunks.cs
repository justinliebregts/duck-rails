#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Derived render meshes; never rebuilds the authored Motel layout.</summary>
public static class RailMotelRenderChunks
{
    const string Prefab = "Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
    const string Folder = "Assets/DuckRails/MotelRenderChunks";
    const string MeshAsset = Folder + "/Meshes.asset";
    const string RootName = "Generated Motel render chunks";
    sealed class Part
    {
        public MeshRenderer renderer;
        public Mesh mesh;
        public int submesh;
        public Material material;
        public RailBakedBlockLighting.Binding binding;
    }
    static string AssetKey(Object value) => value ? AssetDatabase.GetAssetPath(value) + ":" +
        AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(value)) + ":" + value.name : "null";

    // Includes all original renderers so added/deleted/edited scenery invalidates the build too.
    public static string Fingerprint(GameObject root)
    {
        var text = new StringBuilder();
        var optimization = root.GetComponent<RailSceneryChunks>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (optimization && optimization.chunkRoot && r.transform.IsChildOf(optimization.chunkRoot.transform)) continue;
            text.Append(AnimationUtility.CalculateTransformPath(r.transform, root.transform));
            text.Append((root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix).ToString("R"));
            text.Append(r.enabled).Append(r.gameObject.activeInHierarchy).Append(r.gameObject.layer);
            for (var t = r.transform; t != root.transform; t = t.parent)
                foreach (var component in t.GetComponents<Component>()) text.Append(component ? component.GetType().FullName : "missing");
            text.Append(r.shadowCastingMode).Append(r.receiveShadows).Append(r.lightProbeUsage).Append(r.reflectionProbeUsage);
            text.Append(r.renderingLayerMask).Append(r.sortingLayerID).Append(r.sortingOrder);
            var f = r.GetComponent<MeshFilter>(); text.Append(AssetKey(f ? f.sharedMesh : null));
            foreach (var m in r.sharedMaterials) text.Append(AssetKey(m));
        }
        foreach (var b in root.GetComponent<RailBakedBlockLighting>().bindings)
            text.Append(b.renderer ? AnimationUtility.CalculateTransformPath(b.renderer.transform, root.transform) : "missing")
                .Append(AssetKey(b.color)).Append(b.scaleOffset.ToString("R"));
        return Hash128.Compute(text.ToString()).ToString();
    }

    static bool Eligible(MeshRenderer r, Transform root)
    {
        if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff || r.HasPropertyBlock() ||
            r.gameObject.layer != 6 || r.probeAnchor || r.lightProbeProxyVolumeOverride ||
            r.bounds.size.x > 20 || r.bounds.size.y > 12 || r.bounds.size.z > 20 ||
            r.transform.localToWorldMatrix.determinant <= 0) return false;
        for (var t = r.transform; t != root; t = t.parent)
            if (t.GetComponent<Rigidbody>() || t.GetComponent<Animator>() || t.GetComponent<Animation>() ||
                t.GetComponents<MonoBehaviour>().Any(c => !c || c.GetType().Name != "AudioSurface")) return false;
        var f = r.GetComponent<MeshFilter>();
        if (!f || !f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.uv2.Length != f.sharedMesh.vertexCount) return false;
        var mesh = f.sharedMesh;
        if (r.sharedMaterials.Length != mesh.subMeshCount) return false;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetIndexCount(i) > 0 && (mesh.GetTopology(i) != MeshTopology.Triangles ||
                !r.sharedMaterials[i] || r.sharedMaterials[i].renderQueue > 2500)) return false;
        return true;
    }

    [MenuItem("Duck Rails/Authoring/Motel/Regenerate optimized render chunks")]
    public static void Generate()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Use Duck Rails Edit Mode without a bake.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == Prefab) throw new InvalidOperationException("Close Motel Prefab Mode before generating from the saved prefab.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var opt = root.GetComponent<RailSceneryChunks>();
            if (opt && opt.chunkRoot) Object.DestroyImmediate(opt.chunkRoot);
            if (!opt) opt = root.AddComponent<RailSceneryChunks>();
            var protectedState = ProtectedState(root);
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            var parts = new List<Part>();
            foreach (var binding in lighting.bindings)
            {
                var r = binding.renderer as MeshRenderer;
                if (!r || !binding.color || !Eligible(r, root.transform)) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetIndexCount(i) > 0) parts.Add(new Part { renderer = r, mesh = mesh, submesh = i,
                        material = r.sharedMaterials[i], binding = binding });
            }
            string Key(Part p)
            {
                var r = p.renderer; var c = root.transform.InverseTransformPoint(r.bounds.center);
                return $"{Mathf.FloorToInt(c.x / 12)}_{Mathf.FloorToInt(c.y / 6)}_{Mathf.FloorToInt(c.z / 12)}_" +
                    $"{p.material.GetInstanceID()}_{p.binding.color.GetInstanceID()}_{r.shadowCastingMode}_{r.receiveShadows}_" +
                    $"{r.lightProbeUsage}_{r.reflectionProbeUsage}_{r.renderingLayerMask}_{r.sortingLayerID}_{r.sortingOrder}_{r.motionVectorGenerationMode}";
            }
            var groups = parts.GroupBy(Key).ToArray();
            var generated = new GameObject(RootName); generated.transform.SetParent(root.transform, false);
            // Kept inactive in authoring/bake scenes. The runtime switch enables it before lighting loads.
            generated.SetActive(false); opt.chunkRoot = generated;
            var container = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAsset);
            if (!container) AssetDatabase.CreateAsset(new Mesh { name = "Motel render mesh collection" }, MeshAsset);
            var oldMeshes = AssetDatabase.LoadAllAssetsAtPath(MeshAsset).OfType<Mesh>().ToDictionary(m => m.name);
            var bindings = new List<RailBakedBlockLighting.Binding>();
            int index = 0; long inputIndices = 0, outputIndices = 0; long vertices = 0;
            foreach (var group in groups)
            {
                var first = group.First(); var original = first.renderer;
                string name = "Motel chunk " + index++.ToString("D3");
                var combines = group.Select(p => new CombineInstance { mesh = p.mesh, subMeshIndex = p.submesh,
                    transform = root.transform.worldToLocalMatrix * p.renderer.transform.localToWorldMatrix,
                    lightmapScaleOffset = p.binding.scaleOffset }).ToArray();
                var mesh = new Mesh { name = name, indexFormat = combines.Sum(c => (long)c.mesh.vertexCount) > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.CombineMeshes(combines, true, true, true);
                VerifyGeometry(group.ToArray(), mesh, root.transform);
                // UV2 now holds atlas UVs; the lighting loader must bind with identity scale/offset.
                long expected = group.Sum(p => (long)p.mesh.GetIndexCount(p.submesh));
                if (mesh.GetIndexCount(0) != expected) throw new Exception("Triangle preservation failed");
                inputIndices += expected; outputIndices += mesh.GetIndexCount(0); vertices += mesh.vertexCount;
                if (oldMeshes.TryGetValue(name, out var existing))
                { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
                var go = new GameObject(name); go.layer = original.gameObject.layer; go.transform.SetParent(generated.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = first.material;
                r.shadowCastingMode = original.shadowCastingMode; r.receiveShadows = original.receiveShadows;
                r.lightProbeUsage = original.lightProbeUsage; r.reflectionProbeUsage = original.reflectionProbeUsage;
                r.renderingLayerMask = original.renderingLayerMask; r.sortingLayerID = original.sortingLayerID;
                r.sortingOrder = original.sortingOrder; r.motionVectorGenerationMode = original.motionVectorGenerationMode;
                bindings.Add(new RailBakedBlockLighting.Binding { renderer = r, color = first.binding.color, scaleOffset = new Vector4(1, 1, 0, 0) });
            }
            opt.sources = parts.Select(p => (Renderer)p.renderer).Distinct().ToArray();
            opt.sourceLighting = lighting.bindings.Where(b => opt.sources.Contains(b.renderer)).ToArray();
            opt.chunkLighting = bindings.ToArray();
            if (ProtectedState(root) != protectedState) throw new Exception("Collision/gameplay/lighting source changed");
            opt.sourceFingerprint = Fingerprint(root);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab); AssetDatabase.SaveAssets();
            string report = $"Motel render consolidation\nSources replaced: {opt.sources.Length}\nNonempty source submeshes: {parts.Count}\nGenerated single-material chunks: {groups.Length}\nIndices before/after: {inputIndices}/{outputIndices}\nGenerated vertices: {vertices}\nOriginal source bindings retained: {lighting.bindings.Length}\nSpatial cells: 12m X/Z, 6m Y; oversized surfaces retained separately.\nPASS original colliders, gameplay components, transforms and lighting bindings unchanged.\n";
            Directory.CreateDirectory("docs/validation/motel-render-chunks");
            File.WriteAllText("docs/validation/motel-render-chunks/generation.txt", report); Debug.Log(report);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ValidateSaved();
    }

    static string ProtectedState(GameObject root)
    {
        var generated = root.GetComponent<RailSceneryChunks>().chunkRoot;
        return string.Join("\n", root.GetComponentsInChildren<Component>(true)
            .Where(c => c && c != root.transform && !(c is RailSceneryChunks) && !(c is MeshFilter) && !(c is Renderer) &&
                (!generated || !c.transform.IsChildOf(generated.transform)))
            .Select(c => EditorJsonUtility.ToJson(c)));
    }

    static void VerifyGeometry(Part[] parts, Mesh mesh, Transform root)
    {
        var actual = mesh.vertices; var actualUv = mesh.uv2; var indices = mesh.GetIndices(0); int offset = 0;
        foreach (var p in parts)
        {
            var vertices = p.mesh.vertices; var uv = p.mesh.uv2;
            var matrix = root.worldToLocalMatrix * p.renderer.transform.localToWorldMatrix;
            var st = p.binding.scaleOffset;
            foreach (int i in p.mesh.GetIndices(p.submesh))
            {
                int j = indices[offset++]; var expected = matrix.MultiplyPoint3x4(vertices[i]);
                var expectedUv = new Vector2(uv[i].x * st.x + st.z, uv[i].y * st.y + st.w);
                if ((expected - actual[j]).sqrMagnitude > .000001f || (expectedUv - actualUv[j]).sqrMagnitude > .0000000001f)
                    throw new Exception("Combined geometry/atlas UV mismatch: " + p.renderer.name);
            }
        }
        if (offset != indices.Length) throw new Exception("Combined index count mismatch");
    }

    public static void ValidateSaved()
    {
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var opt = root.GetComponent<RailSceneryChunks>();
            if (!opt) return;
            if (opt.sourceFingerprint != Fingerprint(root)) throw new BuildFailedException("Motel scenery changed: regenerate optimized render chunks after saving geometry/lighting.");
            if (opt.chunkRoot.activeSelf) throw new Exception("Authoring must use originals");
            var lighting = root.GetComponent<RailBakedBlockLighting>(); int originalCount = lighting.bindings.Length;
            var state = ProtectedState(root);
            if (!opt.Activate() || opt.sources.Any(r => r.enabled) || !opt.chunkRoot.activeSelf || lighting.bindings.Length != originalCount + opt.chunkLighting.Length)
                throw new Exception("Render switch/binding append failed");
            if (!opt.Activate() || lighting.bindings.Length != originalCount + opt.chunkLighting.Length) throw new Exception("Activation duplicated bindings");
            opt.Restore();
            if (lighting.bindings.Length != originalCount || opt.sources.Any(r => !r.enabled) || opt.chunkRoot.activeSelf || state != ProtectedState(root))
                throw new Exception("Source restore failed");
            var source = opt.sources[0].transform; var position = source.localPosition;
            source.localPosition += Vector3.right;
            if (opt.sourceFingerprint == Fingerprint(root)) throw new Exception("Moved scenery did not invalidate fingerprint");
            source.localPosition = position;
            var first = lighting.bindings.First(b => b.renderer == opt.sourceLighting[0].renderer);
            var changed = lighting.bindings.ToArray(); int slot = Array.FindIndex(changed, b => b.renderer == first.renderer);
            changed[slot].scaleOffset += Vector4.one; lighting.bindings = changed;
            if (opt.Activate() || opt.chunkRoot.activeSelf || opt.sources.Any(r => !r.enabled)) throw new Exception("Stale UVs did not fall back to original scenery");
            Debug.Log("PASS Motel saved fingerprint, activation, idempotence, source restoration, stale-lightmap fallback. No Play Mode entered.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

public sealed class RailMotelChunksBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => RailMotelRenderChunks.ValidateSaved();
}
#endif
