#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>Static, 2 cm raised outlines from CutePixel SDF's source font.</summary>
public static class RailStationMeshSigns
{
    const string Source = "ArtSource/StartingStation/Signs/";
    public const string Folder = "Assets/DuckRails/StartingStationV3/Signs";
    static readonly string[] Labels = { "CONCOURSE", "OFFICE", "SUPPLIES", "WASHROOM" };
    [Serializable] class MeshData { public float[] vertices; public int[] triangles; }

    public static void BuildAssets()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }
        foreach (var label in Labels)
        {
            var path = Folder + "/" + label + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) continue;
            var data = JsonUtility.FromJson<MeshData>(File.ReadAllText(Source + label + ".json"));
            if (data.vertices == null || data.vertices.Length % 3 != 0 || data.triangles == null)
                throw new InvalidOperationException("Invalid sign geometry: " + label);
            var mesh = new Mesh { name = "CutePixel raised " + label };
            mesh.vertices = Enumerable.Range(0, data.vertices.Length / 3)
                .Select(i => new Vector3(data.vertices[i * 3], data.vertices[i * 3 + 1], data.vertices[i * 3 + 2])).ToArray();
            mesh.triangles = data.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            AssetDatabase.CreateAsset(mesh, path);
        }
        if (!AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Lettering.mat"))
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CutePixel ivory raised lettering" };
            material.SetColor("_BaseColor", new Color(.91f, .87f, .75f));
            material.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(material, Folder + "/Lettering.mat");
        }
    }

    public static int Convert(GameObject root)
    {
        BuildAssets();
        var labels = root.GetComponentsInChildren<Transform>(true)
            .Where(t => t.transform.parent && t.transform.parent.name == "Sign labels").ToArray();
        // Validate the whole set before replacing any component.
        foreach (var t in labels)
            if (!Labels.Contains(t.name.Replace("Label ", ""))) throw new InvalidOperationException("Unknown station sign: " + t.name);
        foreach (var t in labels)
        {
            var go = t.gameObject;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + t.name.Replace("Label ", "") + ".asset");
            var text = go.GetComponent<TextMeshPro>();
            if (text) Object.DestroyImmediate(text);
            var filter = go.GetComponent<MeshFilter>();
            if (!filter) filter = go.AddComponent<MeshFilter>();
            // TMP's generated MeshFilter is DontSave; a static replacement must persist.
            filter.hideFlags = HideFlags.None;
            filter.sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            if (!renderer) renderer = go.AddComponent<MeshRenderer>();
            renderer.hideFlags = HideFlags.None;
            renderer.enabled = true;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Lettering.mat");
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = 1;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
        }
        return labels.Length;
    }
}
#endif
