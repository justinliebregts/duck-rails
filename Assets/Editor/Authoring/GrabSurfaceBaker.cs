using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Independent of store/catalog membership: item/weapon folders and root networked items.
// Only editor/import/build events scan assets. Players load the serialized shared references.
[InitializeOnLoad]
public sealed class GrabSurfaceBaker : AssetPostprocessor, IPreprocessBuildWithReport
{
    public const string OutputFolder = "Assets/Generated/GrabSurfaces";
    public const int MaxTriangles = 4096;
    static bool queued, baking;
    public int callbackOrder => -100;

    static GrabSurfaceBaker()
    {
        Queue();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { BakeAll(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
        };
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (baking) return;
        foreach (string path in imported) if (AffectsSurface(path)) { Queue(); return; }
        foreach (string path in deleted) if (AffectsSurface(path)) { Queue(); return; }
        foreach (string path in moved) if (AffectsSurface(path)) { Queue(); return; }
    }

    static bool AffectsSurface(string path)
    {
        if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
        // Generated asset writes do not trigger another bake. Deletion is repaired at play/build.
        if (path.StartsWith(OutputFolder + "/", StringComparison.Ordinal)) return false;
        return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            AssetImporter.GetAtPath(path) is ModelImporter ||
            path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
    }

    static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += BakeQueued;
    }

    static void BakeQueued()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
        try { BakeAll(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public void OnPreprocessBuild(BuildReport report) => BakeAll();

    [MenuItem("Tools/DuckZ/Maintenance/Validate Grab Surface Bakes")]
    public static void BakeAll()
    {
        if (baking) return;
        baking = true;
        try
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(OutputFolder);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<Grabbable>() == null) continue;
                if (prefab.GetComponent<PhotonGrabbable>() == null &&
                    !path.StartsWith("Assets/Prefabs/Items/", StringComparison.Ordinal) &&
                    !path.StartsWith("Assets/Prefabs/Weapons/", StringComparison.Ordinal)) continue;
                Bake(prefab, guid);
            }
        }
        finally { baking = false; }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static bool ActiveUnder(Transform part, Transform root)
    {
        // A disabled prefab root still needs a bake for when it is spawned/enabled.
        for (Transform t = part; t != root; t = t.parent)
            if (!t.gameObject.activeSelf) return false;
        return true;
    }

    public static void Bake(GameObject prefab, string guid)
    {
        var points = new List<Vector3>();
        Transform root = prefab.transform;
        Mesh reference = null;
        Matrix4x4 referencePose = Matrix4x4.identity;
        foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.enabled && ActiveUnder(skinned.transform, root))
                throw new BuildFailedException(prefab.name + ": animated/skinned grab surfaces require a static grip mesh.");
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !renderer.enabled || !ActiveUnder(filter.transform, root)) continue;
            if (filter.GetComponentInParent<Grabbable>(true) != prefab.GetComponent<Grabbable>()) continue;
            Matrix4x4 model = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            if (reference == null) { reference = mesh; referencePose = model; }
            // The editor API also permits non-readable imports during the Play Mode
            // transition, when ordinary mesh.vertices/GetTriangles reads can fail.
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
            var geometry = snapshot[0];
            using var vertices = new NativeArray<Vector3>(geometry.vertexCount, Allocator.Temp);
            geometry.GetVertices(vertices);
            for (int s = 0; s < geometry.subMeshCount; s++)
            {
                var submesh = geometry.GetSubMesh(s);
                if (submesh.topology != MeshTopology.Triangles) continue;
                using var indices = new NativeArray<int>(submesh.indexCount, Allocator.Temp);
                geometry.GetIndices(indices, s, applyBaseVertex: true);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = model.MultiplyPoint3x4(vertices[indices[i]]);
                    Vector3 b = model.MultiplyPoint3x4(vertices[indices[i + 1]]);
                    Vector3 c = model.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-20f) continue;
                    points.Add(a); points.Add(b); points.Add(c);
                }
            }
        }
        if (points.Count == 0 || points.Count / 3 > MaxTriangles)
            throw new BuildFailedException($"{prefab.name}: grab surface must have 1–{MaxTriangles} static triangles; found {points.Count / 3}.");

        string assetPath = OutputFolder + "/" + guid + ".asset";
        GrabSurfaceData data = AssetDatabase.LoadAssetAtPath<GrabSurfaceData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GrabSurfaceData>();
            data.name = prefab.name + " Grab Surface";
            data.SetBakedGeometry(points.ToArray(), reference, referencePose);
            AssetDatabase.CreateAsset(data, assetPath);
        }
        else if (data.SetBakedGeometry(points.ToArray(), reference, referencePose))
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }
        var serialized = new SerializedObject(prefab.GetComponent<Grabbable>());
        SerializedProperty field = serialized.FindProperty("grabSurface");
        if (field.objectReferenceValue == data) return;
        field.objectReferenceValue = data;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
    }
}
