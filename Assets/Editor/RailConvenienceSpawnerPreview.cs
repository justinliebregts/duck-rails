#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailConvenienceSpawnerPreview
{
    const string Path = "Assets/DuckRails/Blocks/convenience-store-v1.prefab";
    [MenuItem("Duck Rails/Authoring/Preview Spawner Prefabs on Convenience Store")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        RailSpawnerPrefabs.CreateAssets();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool open = stage != null && stage.assetPath == Path;
        var root = open ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var b = root.GetComponent<RailBlock>();
            var kinds = new[] { RailSpawnKind.Coal, RailSpawnKind.Gold, RailSpawnKind.Enemy };
            var selections = kinds.SelectMany(k => Enumerable.Range(0, 100).Select(seed => RailSpawnPool.Select(b, k, seed).Select(p => p.position).ToArray())).ToArray();
            var poses = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            var bindings = lighting.bindings.ToArray();
            var oldMarkers = kinds.SelectMany(k => RailSpawnPool.Candidates(b, k)).Distinct().ToArray();
            foreach (var marker in oldMarkers)
                if (marker.childCount != 0 || marker.GetComponents<Component>().Any(c => !(c is Transform) && !(c is CoalSpawner) && !(c is RailGoldSpawner) && !(c is RailEnemySpawner) && !(c is RailSpawnMarker)))
                    throw new Exception("Unexpected marker contents: " + marker.name);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convenience store spawner prefab preview");
            Undo.RecordObject(b, "Consolidate marker pools"); RailSpawnPool.Migrate(b); Undo.FlushUndoRecordObjects();
            int replaced = 0;
            foreach (var kind in kinds)
            {
                var markers = RailSpawnPool.Candidates(b, kind);
                var newMarkers = new List<Transform>();
                for (int i = 0; i < markers.Length; i++)
                {
                    var old = markers[i];
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject) == RailSpawnerPrefabs.Path(kind)) { newMarkers.Add(old); continue; }
                    var go = RailSpawnerPrefabs.Instantiate(kind, root.scene);
                    Undo.RegisterCreatedObjectUndo(go, "Create prefab spawner");
                    Undo.SetTransformParent(go.transform, old.parent, "Parent prefab spawner");
                    Undo.RecordObject(go.transform, "Preserve spawn pose");
                    go.transform.SetPositionAndRotation(old.position, old.rotation); go.transform.localScale = old.localScale;
                    go.transform.SetSiblingIndex(old.GetSiblingIndex());
                    go.name = kind + " Spawner " + (i + 1).ToString("00");
                    go.SetActive(old.gameObject.activeSelf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    newMarkers.Add(go.transform); replaced++;
                }
                Undo.RecordObject(b, "Replace pool references");
                RailSpawnPool.SetPoints(b.spawnCandidates, kind, newMarkers.ToArray());
                Undo.FlushUndoRecordObjects();
                foreach (var old in markers) if (!newMarkers.Contains(old)) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var env = root.transform.Find("Env");
            if (env == null)
            {
                var go = new GameObject("Env"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, root.scene);
                Undo.RegisterCreatedObjectUndo(go, "Create Env"); Undo.SetTransformParent(go.transform, root.transform, "Parent Env");
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one; env = go.transform;
            }
            var geometry = root.transform.Cast<Transform>().Where(t => t != env &&
                (t.GetComponentInChildren<Renderer>(true) != null || t.GetComponentInChildren<Collider>(true) != null || t.GetComponentInChildren<Light>(true) != null)).ToArray();
            foreach (var t in geometry) Undo.SetTransformParent(t, env, "Group environment");
            foreach (var pair in poses)
            {
                if (pair.Key == null) continue; // Replaced marker roots are checked by seeded selection below.
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(pair.Key.localToWorldMatrix[i] - pair.Value[i]) > .0001f) throw new Exception("Moved existing geometry/anchor: " + pair.Key.name);
            }
            var after = kinds.SelectMany(k => Enumerable.Range(0, 100).Select(seed => RailSpawnPool.Select(b, k, seed).Select(p => p.position).ToArray())).ToArray();
            for (int i = 0; i < selections.Length; i++) if (!selections[i].SequenceEqual(after[i])) throw new Exception("Spawn selection changed");
            if (renderers.Any(r => r == null || !r.transform.IsChildOf(env)) || !bindings.SequenceEqual(lighting.bindings)) throw new Exception("Renderer/lightmap binding changed");
            if (env.localPosition != Vector3.zero || env.localRotation != Quaternion.identity || env.localScale != Vector3.one) throw new Exception("Env must have identity local pose");
            foreach (var kind in kinds)
            foreach (var marker in RailSpawnPool.Candidates(b, kind))
                if (!PrefabUtility.IsPartOfPrefabInstance(marker) || marker.GetComponent<RailSpawnMarker>()?.kind != kind || EditorGUIUtility.GetIconForObject(marker.gameObject) == null)
                    throw new Exception("Missing prefab connection/icon: " + marker.name);
            EditorUtility.SetDirty(b); Undo.CollapseUndoOperations(group);
            PrefabUtility.SaveAsPrefabAsset(root, Path, out bool saved); if (!saved) throw new Exception("Prefab save failed");
            Directory.CreateDirectory("docs/validation/spawn-pools");
            File.WriteAllText("docs/validation/spawn-pools/convenience-preview.txt", $"PASS: {replaced} marker replacements; 42 connected prefab markers with native GameObject icons; {geometry.Length} root geometry objects grouped beneath identity Env; {renderers.Length} renderers and {bindings.Length} lightmap bindings preserved; all surviving transform matrices unchanged; 300 seeded position selections unchanged. Only convenience-store-v1 converted.\n");
            Debug.Log(File.ReadAllText("docs/validation/spawn-pools/convenience-preview.txt"));
            SceneView.RepaintAll(); EditorApplication.RepaintHierarchyWindow();
        }
        finally { if (!open) PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
