#if UNITY_EDITOR
using System;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class RailFarmFoliage
{
    public static void Configure(GameObject patch, int verticesPerStalk = 48)
    {
        var surface = AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/Foliage.asset");
        if (!surface) throw new InvalidOperationException("Missing foliage surface");
        var mesh = patch.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        if (vertices.Length % verticesPerStalk != 0) throw new InvalidOperationException("Unexpected corn mesh topology");
        var volume = patch.GetComponent<FoliageVolume>() ?? patch.AddComponent<FoliageVolume>();
        volume.surface = surface;
        volume.stalks = new Vector4[vertices.Length / verticesPerStalk];
        for (int i = 0; i < volume.stalks.Length; i++)
        {
            // First double-sided quad is the vertical stem, before leaves.
            var stem = new Bounds(vertices[i * verticesPerStalk], Vector3.zero);
            for (int j = 1; j < 8; j++) stem.Encapsulate(vertices[i * verticesPerStalk + j]);
            volume.stalks[i] = new Vector4(stem.center.x, stem.min.y, stem.center.z, stem.size.y);
        }
        volume.bounds = mesh.bounds;
        var b = volume.bounds; b.Expand(new Vector3(0, 0.2f, 0)); volume.bounds = b;
    }

    public static void Install()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Duck Rails Edit Mode required");
        var root = PrefabUtility.LoadPrefabContents(RailFarmSilos.PrefabPath);
        try
        {
            var patches = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "Dense corn patch").ToArray();
            if (patches.Length != 52) throw new InvalidOperationException("Expected 52 corn patches");
            foreach (var patch in patches) Configure(patch.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, RailFarmSilos.PrefabPath);
            Debug.Log("Foliage coverage saved on 52 corn patches");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
