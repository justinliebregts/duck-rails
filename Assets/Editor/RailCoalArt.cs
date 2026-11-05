#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Offline authoring only: the runtime pickup loads a normal saved mesh/material.
public static class RailCoalArt
{
    const string Folder = "Assets/DuckRails/Coal";
    const string Prefab = "Assets/Resources/Rails/RailCoal.prefab";

    [MenuItem("Duck Rails/Art/Build Approved Coal")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Folder);
        var mesh = MakeMesh();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Coal.asset");
        if (existing == null) AssetDatabase.CreateAsset(mesh, Folder + "/Coal.asset");
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }

        var texture = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var colors = new[] { new Color32(27,29,35,255), new Color32(34,37,45,255), new Color32(43,47,57,255), new Color32(54,60,73,255), new Color32(66,73,88,255) };
        var pixels = new Color32[1024];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = colors[1];
        var random = new System.Random(1105);
        // Broad stepped patches rather than independent pixel noise.
        for (int patch = 0; patch < 65; patch++)
        {
            int x = random.Next(32), y = random.Next(32), w = random.Next(2,7), h = random.Next(2,5);
            var color = colors[random.Next(colors.Length)];
            for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
                pixels[((y + row) % 32) * 32 + (x + col + row / 2) % 32] = color;
        }
        texture.SetPixels32(pixels); texture.Apply();
        File.WriteAllBytes(Folder + "/CoalPixels.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(Folder + "/CoalPixels.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/CoalPixels.png");
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = true; importer.maxTextureSize = 32; importer.anisoLevel = 0;
        importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Coal.mat");
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Coal.mat"));
            material.name = "Coal Pixel Charcoal"; AssetDatabase.CreateAsset(material, Folder + "/Coal.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/CoalPixels.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_MainTex", material.GetTexture("_BaseMap")); material.SetColor("_Color", Color.white);
        material.SetFloat("_Smoothness", 0); material.SetFloat("_Metallic", 0);
        material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(material); EditorUtility.SetDirty(mesh);
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            // Preserve the collider and its serialized grab reference, physics, scale and Photon components.
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("COAL ART: saved " + mesh.triangles.Length / 3 + " triangles, 32x32 point texture; pickup components preserved.");
    }

    static Mesh MakeMesh()
    {
        // Three deliberately uneven octagonal rings, including a chipped shoulder and sloping crown.
        var bottom = new[] { new Vector3(-.32f,-.5f,-.35f), new Vector3(.28f,-.5f,-.38f), new Vector3(.45f,-.5f,-.18f), new Vector3(.42f,-.5f,.27f), new Vector3(.22f,-.5f,.41f), new Vector3(-.3f,-.5f,.38f), new Vector3(-.46f,-.5f,.2f), new Vector3(-.5f,-.5f,-.18f) };
        var middle = new[] { new Vector3(-.36f,-.03f,-.5f), new Vector3(.32f,-.08f,-.46f), new Vector3(.5f,.06f,-.2f), new Vector3(.46f,.12f,.28f), new Vector3(.22f,.05f,.5f), new Vector3(-.34f,-.02f,.43f), new Vector3(-.5f,-.15f,.19f), new Vector3(-.47f,-.2f,-.2f) };
        var top = new[] { new Vector3(-.22f,0,-.29f), new Vector3(.2f,0,-.28f), new Vector3(.33f,0,-.12f), new Vector3(.28f,0,.2f), new Vector3(.1f,0,.32f), new Vector3(-.25f,0,.29f), new Vector3(-.35f,0,.12f), new Vector3(-.32f,0,-.16f) };
        for (int i = 0; i < top.Length; i++) top[i].y = .44f - top[i].x * .17f;
        top[2].y = .22f; top[3].y = .28f; // Broken lower shoulder beside the broad crown.
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        Action<Vector3,Vector3,Vector3> face = (a,b,c) =>
        {
            var normal = Vector3.Cross(b-a,c-a);
            if (Vector3.Dot(normal,(a+b+c)/3) < 0) { var swap=b; b=c; c=swap; normal=-normal; }
            int start=vertices.Count;
            foreach (var p in new[] {a,b,c})
            {
                vertices.Add(new Vector3(p.x, p.y * .85f - .075f, p.z));
                // Object-space planar projection keeps equal, large texels on each broad face.
                var n = new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                uv.Add(n.y >= n.x && n.y >= n.z ? new Vector2(p.x,p.z) : n.x > n.z ? new Vector2(p.z,p.y) : new Vector2(p.x,p.y));
            }
            triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
        };
        foreach (var rings in new[] { new[] {bottom,middle}, new[] {middle,top} })
        for (int i=0;i<8;i++) { int j=(i+1)%8; face(rings[0][i],rings[0][j],rings[1][j]); face(rings[0][i],rings[1][j],rings[1][i]); }
        for (int i=1;i<7;i++) { face(bottom[0],bottom[i],bottom[i+1]); face(top[0],top[i],top[i+1]); }
        var mesh = new Mesh { name = "Coal" };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public static void Preview()
    {
        var preview = new PreviewRenderUtility();
        try
        {
            preview.camera.backgroundColor = new Color(.33f,.31f,.30f); preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.transform.position = new Vector3(1.35f,.85f,-1.65f);
            preview.camera.transform.LookAt(Vector3.zero);
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 10; preview.camera.fieldOfView = 36;
            preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(35,-35,0);
            preview.lights[1].intensity = .7f; preview.lights[1].transform.rotation = Quaternion.Euler(20,140,0);
            preview.ambientColor = new Color(.35f,.35f,.4f);
            preview.BeginStaticPreview(new Rect(0,0,1000,850));
            var model = new GameObject("Coal preview");
            model.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Coal.asset");
            model.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Coal.mat");
            preview.AddSingleGO(model);
            preview.Render(true);
            var image = preview.EndStaticPreview();
            Directory.CreateDirectory("docs/validation/coal-art");
            File.WriteAllBytes("docs/validation/coal-art/unity-preview.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); }
    }
}
#endif
