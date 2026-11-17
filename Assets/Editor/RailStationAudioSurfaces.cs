#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Collision-only partitions of the existing Blender meshes. Renderers and lightmaps stay intact.
public static class RailStationAudioSurfaces
{
    const string Folder = "Assets/DuckRails/StationAudioCollision";
    const string Compound = "Assets/DuckRails/Compound/SubwayTerminal/SubwayTerminal.prefab";
    const string City = "Assets/DuckRails/StartingStationV3/StartingStationV3.prefab";
    const string Station = "Assets/DuckRails/StartingStation.prefab";
    static SurfaceType Surface(string name) => AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/" + name + ".asset") ?? throw new Exception(name);
    static void Tag(GameObject go, string type)
    {
        var tag = go.GetComponent<AudioSurface>() ?? go.AddComponent<AudioSurface>();
        tag.type = Surface(type);
        if (PrefabUtility.IsPartOfPrefabInstance(tag)) PrefabUtility.RecordPrefabInstancePropertyModifications(tag);
    }

    // Tile coordinates from ArtSource/SubwayTerminal/build_terminal.py (also used by the station).
    static string Classify(string material, Vector2 uv, string mesh, Vector3 a, Vector3 b, Vector3 c)
    {
        int x = Mathf.FloorToInt(uv.x * 256), y = Mathf.FloorToInt((1 - uv.y) * (material == "Subway Town atlas" ? 256 : 128));
        if (material == "Subway Town atlas")
        {
            int col = x / 64, row = y / 64;
            if (row == 1 && (col == 1 || col == 3) || row == 3 && col == 2) return "Wood";
            if (row == 2 && col == 0) return "Metal";
            if (row == 0 && col == 1) return "Dirt";
            if (row == 3 && col == 1) return "Water";
            if (row == 3 && col == 0)
            {
                // Olive is painted masonry on station walls, but crate/signboard paint elsewhere.
                if (mesh == "Station_Shell" || mesh == "Station_StairsBridge" || mesh == "Tunnel") return "Stone";
                if (mesh == "Station_Rooms")
                {
                    // Supplies crates are west of the original platform edge; the olive wall band is on room boundaries.
                    var p = (a + b + c) / 3;
                    if (p.x > -14.9f && p.x < -13.5f && p.y < 2.4f && p.z > 80.3f && p.z < 84.1f) return "Wood";
                    if (p.x >= 15 && p.x <= 17.4f && p.z >= 87.23f && p.z <= 87.35f) return "Wood"; // office route board
                    return "Stone";
                }
                return "Wood";
            }
            return "Stone"; // concrete, plaster, painted wall bands, asphalt and dark masonry
        }
        if (material == "Subway Industrial atlas")
        {
            int col = x / 32, row = y / 32;
            if (mesh == "Station_Rooms" && row == 0 && col == 4) return "Stone"; // washroom ceramic fixtures/wall tiles
            if (row == 0 && (col == 1 || col == 2) || row == 1 && col == 4 || row == 3 && col == 5) return "Stone";
            if (row == 2 && col == 0) return "Wood";
            return "Metal";
        }
        if (material == "Subway tunnel void") return "Stone";
        if (material == "Subway lamp lens" || material == "Subway warm bulb" || material == "Subway night glow") return "Stone";
        if (material == "Subway mesh fence") return "Metal";
        throw new Exception("Unmapped material: " + material);
    }

    static void Partition(MeshCollider source, string prefix, List<string> report)
    {
        var mesh = source.sharedMesh;
        var renderer = source.GetComponent<MeshRenderer>();
        if (!mesh || !renderer || source.convex) throw new Exception("Unexpected collider " + source.name);
        var vertices = mesh.vertices;
        var uv = mesh.uv;
        var groups = new Dictionary<string, List<int>>();
        int total = 0;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            string mat = renderer.sharedMaterials[sub].name;
            var indices = mesh.GetTriangles(sub);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                string type = Classify(mat, (uv[a] + uv[b] + uv[c]) / 3, source.name, vertices[a], vertices[b], vertices[c]);
                if (!groups.TryGetValue(type, out var list)) groups.Add(type, list = new List<int>());
                list.Add(a); list.Add(b); list.Add(c); total++;
            }
        }
        foreach (var group in groups)
        {
            string path = Folder + "/" + prefix + "-" + source.name + "-" + group.Key + ".asset";
            var split = new Mesh { name = prefix + " " + source.name + " " + group.Key, indexFormat = mesh.indexFormat };
            // Keep exact original coordinates, winding and triangle multiplicity.
            split.vertices = vertices; split.SetTriangles(group.Value, 0); split.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(split, existing); Object.DestroyImmediate(split); split = existing; }
            else AssetDatabase.CreateAsset(split, path);
            var go = new GameObject("Audio collision - " + group.Key); go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            var collider = go.AddComponent<MeshCollider>();
            EditorUtility.CopySerialized(source, collider);
            collider.sharedMesh = split;
            Tag(go, group.Key);
        }
        source.enabled = false;
        if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        report.Add(prefix + "/" + source.name + ": " + total + " triangles; " + string.Join(", ", groups.Select(g => g.Key + "=" + g.Value.Count / 3)));
    }

    [MenuItem("Duck Rails/Authoring/Audio/Tag station and compound surfaces")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode only");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var report = new List<string>();
        foreach (string path in new[] { Compound, City, Station })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == 6).ToArray())
                {
                    if (AudioSurface.TypeOf(collider)) continue;
                    // Merchandise interaction volumes are not environment collision.
                    var renderer = collider.GetComponent<Renderer>();
                    if (collider is MeshCollider mc && renderer && renderer.sharedMaterials.Any(m => m && m.name.StartsWith("Subway ")))
                        Partition(mc, path == Compound ? "Compound" : "Station", report);
                    else if (collider.name == "Station ground") Tag(collider.gameObject, "Stone");
                    else if (collider.transform.parent && collider.transform.parent.name == "Invisible guards" || collider.name.Contains("bracket") || collider.name.Contains("Security")) Tag(collider.gameObject, "Metal");
                    else throw new Exception("Unclassified environment collider: " + collider.name + " in " + path);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs/validation/station-audio-surfaces");
        File.WriteAllLines("docs/validation/station-audio-surfaces/partitions.txt", report);
        Debug.Log("Station surface partitions saved: " + report.Count);
    }
}
#endif
