#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Converts the saved catalogue to prefab markers and one pick-lockable Env hierarchy.</summary>
public static class RailBlockAuthoringCleanup
{
    static readonly RailSpawnKind[] Kinds = { RailSpawnKind.Coal, RailSpawnKind.Gold, RailSpawnKind.Enemy };
    const string Evidence = "docs/validation/spawn-pools/catalogue-cleanup.txt";
    public static string[] Paths => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails" })
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RailBlock>() != null).OrderBy(p => p).ToArray();

    [MenuItem("Duck Rails/Authoring/Clean Up All Block Spawners and Environment")]
    public static void Run() => RunBatch(0, Paths.Length);
    public static void RunBatch(int start, int count)
    {
        if (Application.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Use Edit Mode outside a bake.");
        RailSpawnerPrefabs.CreateAssets();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Evidence));
        if (start == 0) File.WriteAllText(Evidence, "");
        foreach (var path in Paths.Skip(start).Take(count))
        {
            string line = Convert(path);
            string prefix = line.Split(':')[0] + ":";
            if (!File.ReadAllLines(Evidence).Any(existing => existing.StartsWith(prefix, StringComparison.Ordinal)))
                File.AppendAllText(Evidence, line + "\n");
            Debug.Log(line);
        }
        SceneView.RepaintAll(); EditorApplication.RepaintHierarchyWindow();
    }
    static Transform Group(Transform root, string name)
    {
        var t = root.Find(name);
        if (t != null) return t;
        var go = new GameObject(name); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, root.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name); Undo.SetTransformParent(go.transform, root, "Parent " + name);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        return go.transform;
    }
    static Vector3[][] Selection(RailBlock b) => Kinds.SelectMany(k => Enumerable.Range(0, 100)
        .Select(seed => RailSpawnPool.Select(b, k, seed).Select(p => p.position).ToArray())).ToArray();

    public static string Convert(string path)
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool open = stage != null && stage.assetPath == path;
        var root = open ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            var b = root.GetComponent<RailBlock>();
            var existing = Kinds.SelectMany(k => RailSpawnPool.Candidates(b, k)).Distinct().ToArray();
            var variations = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Spawn variation", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (b.spawnCandidatesMigrated && b.variations.Length == 0 && variations.Length == 0 && root.transform.Find("Env") != null)
            {
                Validate(root);
                return $"PASS {b.name}: already clean; {existing.Length} prefab markers verified without rewriting the asset.";
            }
            foreach (var t in variations)
                if (t.GetComponents<Component>().Length != 1 || t.Cast<Transform>().Any(child => !existing.Contains(child)))
                    throw new Exception(path + ": unexpected contents in " + t.name);
            foreach (var t in existing)
                if (t.childCount != 0 || t.GetComponents<Component>().Any(c => !(c is Transform) && !(c is CoalSpawner) && !(c is RailGoldSpawner) && !(c is RailEnemySpawner) && !(c is RailSpawnMarker)))
                    throw new Exception(path + ": unexpected marker contents in " + t.name);
            var selections = Selection(b);
            var poses = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var colliders = root.GetComponentsInChildren<Collider>(true);
            var lights = root.GetComponentsInChildren<Light>(true);
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            var bindings = lighting != null ? lighting.bindings.ToArray() : Array.Empty<RailBakedBlockLighting.Binding>();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Clean up " + b.name + " authoring");
            Undo.RecordObject(b, "Consolidate spawns"); RailSpawnPool.Migrate(b); b.variations = Array.Empty<RailSpawnSet>(); Undo.FlushUndoRecordObjects();
            int replacements = 0;
            foreach (var kind in Kinds)
            {
                var parent = Group(root.transform, kind + " Spawns");
                var oldPoints = RailSpawnPool.Candidates(b, kind);
                var points = new List<Transform>();
                for (int i = 0; i < oldPoints.Length; i++)
                {
                    var old = oldPoints[i];
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject) == RailSpawnerPrefabs.Path(kind))
                    {
                        if (old.parent != parent) Undo.SetTransformParent(old, parent, "Group spawner");
                        points.Add(old); continue;
                    }
                    // Prefab contents loaded beside another Prefab Stage may not be active in the editor.
                    bool active = true;
                    for (var t = old; t != root.transform; t = t.parent) active &= t.gameObject.activeSelf;
                    var marker = RailSpawnerPrefabs.Instantiate(kind, root.scene);
                    Undo.RegisterCreatedObjectUndo(marker, "Create spawner prefab");
                    // First use the original parent to preserve scale, then reparent in world space.
                    Undo.SetTransformParent(marker.transform, old.parent, "Match original marker parent");
                    Undo.RecordObject(marker.transform, "Preserve marker pose");
                    marker.transform.localPosition = old.localPosition; marker.transform.localRotation = old.localRotation; marker.transform.localScale = old.localScale;
                    Undo.SetTransformParent(marker.transform, parent, "Group spawner prefab");
                    marker.name = kind + " Spawner " + (i + 1).ToString("00"); marker.SetActive(active);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(marker); PrefabUtility.RecordPrefabInstancePropertyModifications(marker.transform);
                    for (int v = 0; v < 16; v++) if (Mathf.Abs(marker.transform.localToWorldMatrix[v] - poses[old][v]) > .0001f) throw new Exception("Marker pose changed: " + old.name);
                    points.Add(marker.transform); replacements++;
                }
                Undo.RecordObject(b, "Replace spawn references"); RailSpawnPool.SetPoints(b.spawnCandidates, kind, points.ToArray()); Undo.FlushUndoRecordObjects();
                foreach (var old in oldPoints) if (!points.Contains(old)) Undo.DestroyObjectImmediate(old.gameObject);
            }
            foreach (var t in variations)
            {
                if (t.childCount != 0) throw new Exception("Legacy container is not empty: " + t.name);
                Undo.DestroyObjectImmediate(t.gameObject);
            }
            var env = Group(root.transform, "Env");
            if (env.localPosition != Vector3.zero || env.localRotation != Quaternion.identity || env.localScale != Vector3.one) throw new Exception(path + ": Env is not identity");
            var geometry = root.transform.Cast<Transform>().Where(t => t != env &&
                (t.GetComponentInChildren<Renderer>(true) != null || t.GetComponentInChildren<Collider>(true) != null || t.GetComponentInChildren<Light>(true) != null)).ToArray();
            foreach (var t in geometry) Undo.SetTransformParent(t, env, "Group environment");
            foreach (var pose in poses)
                if (pose.Key != null)
                    for (int i = 0; i < 16; i++) if (Mathf.Abs(pose.Key.localToWorldMatrix[i] - pose.Value[i]) > .0001f) throw new Exception("Geometry or anchor moved: " + pose.Key.name);
            var after = Selection(b);
            for (int i = 0; i < selections.Length; i++)
                if (selections[i].Length != after[i].Length || selections[i].Where((p, j) => Vector3.Distance(p, after[i][j]) > .0001f).Any())
                    throw new Exception("Selection changed: " + path + " case=" + i + " before=" + string.Join(";", selections[i]) + " after=" + string.Join(";", after[i]));
            if (renderers.Any(r => r == null || !r.transform.IsChildOf(env)) || colliders.Any(c => c == null || !c.transform.IsChildOf(env)) || lights.Any(l => l == null || !l.transform.IsChildOf(env))) throw new Exception("Missing Env member: " + path);
            if (lighting != null && !bindings.SequenceEqual(lighting.bindings)) throw new Exception("Lighting binding changed: " + path);
            Validate(root);
            EditorUtility.SetDirty(b); Undo.CollapseUndoOperations(undo);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); if (!saved) throw new Exception("Save failed: " + path);
            return $"PASS {b.name}: replaced={replacements}, markers={existing.Length}, removed variation groups={variations.Length}, Env branches added={geometry.Length}, preserved renderers={renderers.Length}, colliders={colliders.Length}, lights={lights.Length}, lightmap bindings={bindings.Length}; all poses and 300 seeded selections unchanged.";
        }
        finally { if (!open) PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Validate(GameObject root)
    {
        var b = root.GetComponent<RailBlock>(); var env = root.transform.Find("Env");
        if (!b.spawnCandidatesMigrated || b.variations.Length != 0 || root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Spawn variation", StringComparison.OrdinalIgnoreCase))) throw new Exception("Remaining variations: " + root.name);
        if (env == null || env.localPosition != Vector3.zero || env.localRotation != Quaternion.identity || env.localScale != Vector3.one) throw new Exception("Missing identity Env: " + root.name);
        foreach (var c in root.GetComponentsInChildren<Component>(true))
            if ((c is Renderer || c is Collider || c is Light) && !c.transform.IsChildOf(env)) throw new Exception("Ungrouped environment: " + c.name);
        foreach (var kind in Kinds)
        foreach (var p in RailSpawnPool.Candidates(b, kind))
            if (p.parent != root.transform.Find(kind + " Spawns") || p.GetComponent<RailSpawnMarker>()?.kind != kind ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject) != RailSpawnerPrefabs.Path(kind) || EditorGUIUtility.GetIconForObject(p.gameObject) != RailSpawnerPrefabs.Icon(kind))
                throw new Exception("Invalid prefab marker: " + p.name);
        if (root.GetComponentsInChildren<CoalSpawner>(true).Any(c => c.GetComponent<RailSpawnMarker>() == null) ||
            root.GetComponentsInChildren<RailGoldSpawner>(true).Any(c => c.GetComponent<RailSpawnMarker>() == null) ||
            root.GetComponentsInChildren<RailEnemySpawner>(true).Any(c => c.GetComponent<RailSpawnMarker>() == null)) throw new Exception("Legacy marker labels remain: " + root.name);
    }
    [MenuItem("Duck Rails/Tests/Validate Clean Block Authoring")]
    public static void ValidateSaved()
    {
        foreach (var path in Paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Validate(root); } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("BLOCK AUTHORING: all " + Paths.Length + " saved prefabs passed hierarchy, prefab link, icon and legacy-label checks.");
    }
}
#endif
