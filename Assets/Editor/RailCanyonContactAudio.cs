using System;
using System.Collections.Generic;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class RailCanyonContactAudio
{
    const string Folder = RailCanyon.Folder + "/Contact meshes";
    const string WaterGroup = "Creek and mine water";
    static SurfaceType Surface(string name) =>
        AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/" + name + ".asset") ?? throw new InvalidOperationException(name);

    [MenuItem("Duck Rails/Authoring/Canyon/Install contact audio and mine puddles")]
    public static void Install()
    {
        RailCanyon.Guard();
        var root = PrefabUtility.LoadPrefabContents(RailCanyon.PrefabPath);
        try
        {
            Apply(RailBlock.FindEnvironment(root.transform, RailCanyon.RootName));
            PrefabUtility.SaveAsPrefabAsset(root, RailCanyon.PrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("Canyon prefab save failed");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    public static void Apply(Transform shell)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(RailCanyon.Folder, "Contact meshes");
        foreach (var c in shell.GetComponentsInChildren<Collider>(true))
        {
            if (!c.enabled || c.isTrigger || c.gameObject.layer != 6 || AudioSurface.TypeOf(c)) continue;
            string mat = c.GetComponent<Renderer>().sharedMaterial.name;
            if (c.name.StartsWith("Survey tents_")) Tag(c.gameObject, "Dirt");
            else if (mat == "Canyon Survey Atlas" || mat == "Lantern glass and iron") TagSurvey((MeshCollider)c);
            else Tag(c.gameObject, mat switch {
                "Canyon sand" or "Ochre rim" => "Sand",
                "Bedded sandstone" or "Layered sandstone" => "Stone",
                "Weathered bridge timber" => "Wood",
                "Track ballast" => "Dirt",
                "Bridge iron" => "Metal",
                "Cactus" => "Foliage",
                _ => throw new InvalidOperationException("Unknown canyon material " + mat)
            });
        }
        if (shell.Find(WaterGroup)) return;
        var group = Child(shell, WaterGroup);
        var banks = Enumerable.Range(-40,81).Select(x => {
            float z = RailCanyon.Creek(x) - 80;
            return new Vector2(z - 1.8f, z + 1.8f);
        }).ToArray();
        Water(group, "Shallow creek", new Vector3(0,-12.06f,80), 80, .55f, banks);
        foreach (var (z, width, length, offset) in new[]{(52,1.65f,3.5f,-.15f),(63,2.2f,4.2f,.12f),
            (77,2.35f,5.4f,-.18f),(89,1.8f,3.7f,.2f),(99,2.15f,4.4f,-.1f)})
        {
            var edges = new[]{new Vector2(-.08f,.03f),new(-.36f,.32f),new(-.48f,.46f),
                new(-.44f,.5f),new(-.32f,.38f),new(-.06f,.1f)};
            for (int i=0;i<edges.Length;i++) edges[i] *= length;
            var surface = new Vector3(RailCanyon.RiverTunnelX(z)+offset,-17.96f,z);
            var volume = Water(group, "Mine puddle " + z, surface, width,.16f,edges);
            var top = Child(volume.transform,"Water surface");
            top.localPosition = Vector3.up * volume.size.y * .5f;
            var verts = new List<Vector3>();
            var indices = new List<int>();
            for (int i=0;i<edges.Length;i++)
            {
                float x = width * ((float)i/(edges.Length-1)-.5f);
                verts.Add(new Vector3(x,0,edges[i].x));
                verts.Add(new Vector3(x,0,edges[i].y));
                int a = i*2;
                if (i+1<edges.Length) indices.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices,0);
            mesh.SetUVs(0,verts.Select(v=>new Vector2(v.x,v.z)*.35f).ToList());
            mesh.RecalculateNormals();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            top.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh,volume.name);
            top.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shell.Find("Shallow creek surface").GetComponent<MeshRenderer>().sharedMaterial;
        }
    }

    static Transform Child(Transform parent,string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent,false);
        return t;
    }

    static WaterVolume Water(Transform group,string name,Vector3 surface,float width,float depth,Vector2[] edges)
    {
        var t = Child(group,name);
        t.localPosition = surface - Vector3.up * depth * .5f;
        var w = t.gameObject.AddComponent<WaterVolume>();
        w.size = new Vector3(width,depth,2 * edges.Max(e=>Mathf.Max(Mathf.Abs(e.x),Mathf.Abs(e.y))));
        w.shoreEdges = edges;
        w.waterSurface = Surface("Water");
        return w;
    }

    static Mesh SaveMesh(Mesh mesh,string name)
    {
        mesh.name = name;
        string path = Folder + "/" + name + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!saved) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
        EditorUtility.CopySerialized(mesh,saved);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(saved);
        return saved;
    }

    static void Tag(GameObject go,string type) => go.AddComponent<AudioSurface>().type = Surface(type);

    // UV tiles from the 8-column Canyon Survey atlas. Canvas has a soft dirt contact,
    // cable on spools uses metal, and the canvas guy lines use the same soft contact.
    static string AtlasSurface(int tile,string name) => tile switch {
        0 or 2 or 15 => "Dirt",
        1 when name.StartsWith("Survey tents") => "Dirt",
        3 or 5 or 8 => "Wood",
        1 or 10 or 12 or 13 or 18 => "Stone",
        16 or 19 or 20 => "Foliage",
        4 or 6 or 7 or 9 or 11 or 14 or 17 or 21 => "Metal",
        _ => throw new InvalidOperationException("Unknown survey atlas tile " + tile)
    };

    static void TagSurvey(MeshCollider source)
    {
        var mesh = source.sharedMesh;
        var uv = mesh.uv;
        int[] indices = mesh.triangles;
        var groups = new Dictionary<string,List<int>>();
        for (int i=0;i<indices.Length;i+=3)
        {
            var p = (uv[indices[i]]+uv[indices[i+1]]+uv[indices[i+2]])/3;
            string type = AtlasSurface(Mathf.FloorToInt(p.x*8)+8*Mathf.FloorToInt(p.y*8),source.name);
            if (!groups.TryGetValue(type,out var list)) groups.Add(type,list=new List<int>());
            list.Add(indices[i]); list.Add(indices[i+1]); list.Add(indices[i+2]);
        }
        if (groups.Count == 1) { Tag(source.gameObject,groups.Keys.Single()); return; }
        foreach (var pair in groups)
        {
            var child = Child(source.transform,"Contact " + pair.Key);
            child.gameObject.layer = source.gameObject.layer;
            var part = new Mesh { indexFormat=mesh.indexFormat, vertices=mesh.vertices };
            part.SetTriangles(pair.Value,0);
            var collider = child.gameObject.AddComponent<MeshCollider>();
            EditorUtility.CopySerialized(source,collider);
            collider.sharedMesh = SaveMesh(part,source.name+" "+pair.Key);
            Tag(child.gameObject,pair.Key);
        }
        source.enabled = false;
    }
}
