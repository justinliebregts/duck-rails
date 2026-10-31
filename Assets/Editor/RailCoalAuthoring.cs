#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RailCoalAuthoring
{
    public const string PrefabPath = "Assets/DuckRails/Authoring/CoalSpawner.prefab";

    public static GameObject EnsurePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        var root = new GameObject("CoalSpawner");
        try
        {
            root.AddComponent<CoalSpawner>();
            EditorGUIUtility.SetIconForObject(root, EditorGUIUtility.IconContent("sv_label_3").image as Texture2D);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static Transform Create(Transform parent, string name, Vector3 position)
    {
        var marker = (GameObject)PrefabUtility.InstantiatePrefab(EnsurePrefab(), parent);
        marker.name = name;
        marker.transform.localPosition = position;
        return marker.transform;
    }

    public static Vector3[] Spread(IEnumerable<Vector3> candidates, int variation)
    {
        var remaining = candidates.OrderBy(p => unchecked(((int)p.x * 73856093 ^ (int)p.z * 19349663 ^ (variation + 1) * 83492791) & int.MaxValue)).ToList();
        var selected = new List<Vector3>();
        while (selected.Count < 6 && remaining.Count > 0)
        {
            var next = selected.Count == 0 ? remaining[0] : remaining.OrderByDescending(p => selected.Min(s => (p - s).sqrMagnitude)).First();
            selected.Add(next);
            remaining.RemoveAll(p => Vector3.Distance(p, next) < 8f);
        }
        if (selected.Count != 6) throw new InvalidOperationException("Cannot place six coal markers at least 8 metres apart.");
        return selected.ToArray();
    }

    [MenuItem("Duck Rails/Authoring/Spread Block Coal Markers")]
    public static void UpdateBlocks()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        EnsurePrefab();
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { RailWeek3Builder.Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var block = root.GetComponent<RailBlock>();
                if (block == null || block.variations.All(v => v.coal.Length == 0)) continue;
                var colliders = root.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.gameObject.layer == 6).ToArray();
                var candidates = new List<Vector3>();
                foreach (float z in new[] { 56f, 68f, 80f, 92f, 104f })
                foreach (float x in new[] { 12f, 16f, 24f, 32f })
                {
                    var ray = new Ray(new Vector3(x, 1.8f, z), Vector3.down);
                    var hits = colliders.Select(c => c.Raycast(ray, out var hit, 4) ? (RaycastHit?)hit : null).Where(h => h.HasValue).Select(h => h.Value).OrderBy(h => h.distance).ToArray();
                    if (hits.Length == 0 || hits[0].point.y > 2) continue;
                    Vector3 p = hits[0].point + Vector3.up * .45f;
                    if (colliders.Any(c => Vector3.Distance(c.ClosestPoint(p + Vector3.up * .2f), p + Vector3.up * .2f) < .45f)) continue;
                    candidates.Add(p);
                }
                // Compute every variation before changing this asset.
                var positions = Enumerable.Range(0, block.variations.Length).Select(v => Spread(candidates, v)).ToArray();
                for (int v = 0; v < block.variations.Length; v++)
                {
                    var set = block.variations[v];
                    var parent = set.coal[0].parent;
                    var previous = set.coal;
                    set.coal = positions[v].Select((p, i) => Create(parent, "CoalSpawner " + (i + 1), parent.InverseTransformPoint(p))).ToArray();
                    foreach (var old in previous) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Updated coal markers in " + count + " blocks; six per variation, minimum separation 8m.");
    }
}
#endif
