#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Split the reviewed wheel mesh offline; keep suspension and collision stationary.</summary>
public static class RailTrainWheelArt
{
    public static void Apply(GameObject root)
    {
        var source = root.transform.Find("Blender train shell/Wheels");
        var mesh = source.GetComponent<MeshFilter>().sharedMesh;
        var renderer = source.GetComponent<MeshRenderer>();
        var vertices = mesh.vertices;
        var uv = mesh.uv;
        var normals = mesh.normals;
        var triangles = mesh.triangles;
        if (mesh.subMeshCount != 1) throw new InvalidOperationException("Expected reviewed single-material wheels");
        var train = root.GetComponent<RailTrain>();
        var pivots = new List<Transform>();
        var suspension = new List<int>();
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
        {
            var selected = new List<int>();
            var center = new Vector3(sx * 1.52f, -.68f, sz * 3.3f);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                if (Mathf.Sign(a.x) != sx || Mathf.Sign(a.z) != sz) continue;
                bool mount = true;
                for (int j = 0; j < 3; j++)
                {
                    var v = vertices[triangles[i+j]];
                    if (Mathf.Sign(v.x) != sx || Mathf.Sign(v.z) != sz)
                        throw new InvalidOperationException("Triangle crosses wheel quadrants");
                    mount &= new Vector2(v.y - center.y, v.z - center.z).magnitude > .431f;
                }
                for (int j = 0; j < 3; j++) (mount ? suspension : selected).Add(triangles[i+j]);
            }
            pivots.Add(SavePart(source, "Rolling wheel " + sx + " " + sz, center, selected, vertices, uv, normals, renderer));
        }
        SavePart(source, "Stationary wheel mounts", Vector3.zero, suspension, vertices, uv, normals, renderer);
        renderer.enabled = false;
        train.wheels = pivots.ToArray();
        train.wheelRadius = .43f;
    }

    static Transform SavePart(Transform parent, string name, Vector3 center, List<int> indices,
        Vector3[] vertices, Vector2[] uv, Vector3[] normals, MeshRenderer source)
    {
        if (indices.Count == 0) throw new InvalidOperationException("Empty wheel part " + name);
        var used = indices.Distinct().ToArray();
        var remap = used.Select((id, slot) => new { id, slot }).ToDictionary(x => x.id, x => x.slot);
        string path = RailBlenderTrainImport.Folder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.vertices = used.Select(i => vertices[i] - center).ToArray();
        mesh.uv = used.Select(i => uv[i]).ToArray();
        mesh.normals = used.Select(i => normals[i]).ToArray();
        mesh.triangles = indices.Select(i => remap[i]).ToArray();
        mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.localPosition = center; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
        child.gameObject.layer = parent.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        var filter = child.GetComponent<MeshFilter>(); if (!filter) filter = child.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = child.GetComponent<MeshRenderer>(); if (!renderer) renderer = child.gameObject.AddComponent<MeshRenderer>();
        EditorUtility.CopySerialized(source, renderer);
        renderer.enabled = true;
        return child;
    }

    [MenuItem("Duck Rails/Train/Install Wheel Rotation")]
    public static void Install()
    {
        if (Application.dataPath != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Duck Rails Edit Mode required");
        var root = PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, RailYardMuleArt.Train); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
#endif
