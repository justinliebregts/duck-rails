using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Operates on the existing patches: no field regeneration or transform changes.
public static class RailFarmCornPolish
{
    public static Vector2[] FaceUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var u = (b - a).normalized;
        var normal = Vector3.Cross(b - a, d - a).normalized;
        var v = Vector3.Cross(normal, u).normalized;
        return new[] { a, b, c, d }.Select(p => new Vector2(
            Vector3.Dot(p - a, u), Vector3.Dot(p - a, v)) / .75f).ToArray();
    }

    public static string Apply()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.assetPath != RailFarmSilos.PrefabPath)
            throw new InvalidOperationException("Open the Farm prefab first.");
        var filters = stage.prefabContentsRoot.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.name == "Dense corn patch").ToArray();
        var meshes = filters.Select(f => f.sharedMesh).Distinct().ToArray();
        // Validate every source before touching any asset.
        foreach (var mesh in meshes)
        {
            if (mesh.uv.Length > 1 && mesh.uv[1].x > 2f) continue;
            if (mesh.vertexCount % 56 != 0 || mesh.triangles.Length != mesh.vertexCount / 56 * 84)
                throw new InvalidOperationException("Unexpected corn topology: " + mesh.name);
        }
        int plants = 0, before = 0, after = 0;
        foreach (var mesh in meshes)
        {
            if (mesh.uv.Length > 1 && mesh.uv[1].x > 2f) continue;
            Undo.RecordObject(mesh, "Polish corn UVs and remove one leaf");
            var source = mesh.vertices;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            before += mesh.triangles.Length / 3;
            for (int p = 0; p < source.Length / 56; p++)
            {
                // Vary which leaf is removed, preserving a natural silhouette.
                int removed = 2 + (plants++ % 5);
                for (int face = 0; face < 7; face++)
                {
                    if (face == removed) continue;
                    int start = p * 56 + face * 8;
                    var uv = FaceUV(source[start], source[start + 1], source[start + 2], source[start + 3]);
                    for (int side = 0; side < 2; side++)
                    {
                        int k = verts.Count;
                        for (int j = 0; j < 4; j++)
                        {
                            verts.Add(source[start + side * 4 + j]);
                            uvs.Add(uv[side == 0 ? j : 3 - j]);
                        }
                        tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                    }
                }
            }
            mesh.Clear(); mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetUVs(1, uvs);
            mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            after += tris.Count / 3;
        }
        AssetDatabase.SaveAssets();
        var report = $"Corn: {filters.Length} patches, {plants} stalks retained. Leaves {plants * 5} -> {plants * 4} (20% fewer). Triangles {before} -> {after}. UVs use .75 metre repeats. Existing patch transforms, materials, lighting flags and colliders preserved.";
        System.IO.File.WriteAllText("docs/validation/farm-polish/corn-surface-fix.txt", report);
        return report;
    }
}

