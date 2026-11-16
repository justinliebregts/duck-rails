using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Offline slicing only: original prefab, collision and baked lighting remain untouched.</summary>
public static class RailBridgeCollapseAuthoring
{
    const string Path = "Assets/Resources/Rails/BridgeCollapse.prefab";
    const string MeshPath = "Assets/Resources/Rails/BridgeCollapseMeshes.asset";
    const string FX = "Assets/Plugins/PolygonParticleFX/Prefabs/";
    struct V
    {
        public Vector3 p, n;
        public Vector2 uv;
        public static V Lerp(V a, V b, float t) => new V { p = Vector3.Lerp(a.p,b.p,t), n = Vector3.Lerp(a.n,b.n,t).normalized, uv = Vector2.Lerp(a.uv,b.uv,t) };
    }
    sealed class Geometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> indices = new List<int>();
        public void Triangle(V a, V b, V c, Vector3 pivot)
        {
            foreach (var v in new[] { a,b,c }) { indices.Add(vertices.Count); vertices.Add(v.p-pivot); normals.Add(v.n); uv.Add(v.uv); }
        }
    }
    static List<V> Clip(List<V> input, float z, bool above)
    {
        var output = new List<V>();
        if (input.Count == 0) return output;
        V a = input[input.Count-1];
        foreach (var b in input)
        {
            bool ai = above ? a.p.z >= z : a.p.z <= z, bi = above ? b.p.z >= z : b.p.z <= z;
            if (ai != bi) output.Add(V.Lerp(a,b,(z-a.p.z)/(b.p.z-a.p.z)));
            if (bi) output.Add(b);
            a=b;
        }
        return output;
    }
    static void Cap(Geometry g, List<Vector3> points, float z, bool upper, Vector3 pivot)
    {
        var unique = new List<Vector3>();
        foreach (var p in points) if (!unique.Any(q => (q-p).sqrMagnitude < .000001f)) unique.Add(p);
        if (unique.Count < 3) return;
        var center = unique.Aggregate(Vector3.zero,(a,b)=>a+b)/unique.Count;
        unique = unique.OrderBy(p=>Mathf.Atan2(p.y-center.y,p.x-center.x)).ToList();
        Vector3 normal = upper ? Vector3.forward : Vector3.back;
        V Make(Vector3 p) => new V { p=p, n=normal, uv=new Vector2(p.x,p.y) };
        for(int i=0;i<unique.Count;i++)
        {
            var a=Make(unique[i]); var b=Make(unique[(i+1)%unique.Count]);
            if(upper)g.Triangle(Make(center),a,b,pivot); else g.Triangle(Make(center),b,a,pivot);
        }
    }
    [MenuItem("Duck Rails/Authoring/Finale/Build crash sections")]
    public static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab");
        var hinge = source.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Bridge hinge");
        var root = new GameObject("Bridge collapse");
        try
        {
            var effect=root.AddComponent<RailBridgeCollapse>();
            effect.sections=new Transform[5]; effect.restHeights=new float[5];
            effect.explosionPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(FX+"FX_Explosion_Large_Dark_01.prefab");
            var fires=new List<GameObject>();
            var existing=AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToDictionary(m=>m.name);
            for(int section=0;section<5;section++)
            {
                var pivot=new Vector3(0,0,section*6+3);
                var groups=new Dictionary<Material,Geometry>();
                foreach(var mf in hinge.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!mf.gameObject.activeSelf)continue;
                    var renderer=mf.GetComponent<MeshRenderer>();
                    if(renderer==null || !renderer.enabled)continue;
                    var mesh=mf.sharedMesh; var vertices=mesh.vertices; var normals=mesh.normals; var uvs=mesh.uv;
                    var matrix=hinge.worldToLocalMatrix*mf.transform.localToWorldMatrix;
                    var normalMatrix=matrix.inverse.transpose;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var material=renderer.sharedMaterials[sub];
                        if(!groups.TryGetValue(material,out var g))groups[material]=g=new Geometry();
                        var tris=mesh.GetTriangles(sub);
                        var low=new List<Vector3>(); var high=new List<Vector3>();
                        V Vertex(int i)=>new V {p=matrix.MultiplyPoint3x4(vertices[i]),n=normalMatrix.MultiplyVector(normals[i]).normalized,uv=uvs.Length==vertices.Length?uvs[i]:Vector2.zero};
                        for(int t=0;t<tris.Length;t+=3)
                        {
                            var polygon=new List<V>{Vertex(tris[t]),Vertex(tris[t+1]),Vertex(tris[t+2])};
                            // Collect section intersections for closed fracture faces on each source part.
                            foreach(float plane in new[]{section*6f,(section+1)*6f})
                                for(int e=0;e<3;e++)
                                {
                                    var a=polygon[e]; var b=polygon[(e+1)%3];
                                    if((a.p.z<plane && b.p.z>plane)||(a.p.z>plane && b.p.z<plane))
                                        (plane==section*6f?low:high).Add(V.Lerp(a,b,(plane-a.p.z)/(b.p.z-a.p.z)).p);
                                }
                            polygon=Clip(Clip(polygon,section*6,true),(section+1)*6,false);
                            for(int v=1;v+1<polygon.Count;v++)g.Triangle(polygon[0],polygon[v],polygon[v+1],pivot);
                        }
                        Cap(g,low,section*6,false,pivot); Cap(g,high,(section+1)*6,true,pivot);
                    }
                }
                var piece=new GameObject("Fractured span "+(section+1)); piece.transform.SetParent(root.transform,false); piece.transform.localPosition=pivot;
                effect.sections[section]=piece.transform;
                float bottom=float.PositiveInfinity;
                RailBridgeCollapse.Pose(section,Quaternion.identity,10,out _,out var restRotation);
                int group=0;
                foreach(var pair in groups.Where(p=>p.Value.vertices.Count>0))
                {
                    string name="Span "+section+" material "+group++;
                    if(!existing.TryGetValue(name,out var mesh))
                    {
                        mesh=new Mesh {name=name};
                        if(AssetDatabase.LoadMainAssetAtPath(MeshPath)==null)AssetDatabase.CreateAsset(mesh,MeshPath);
                        else AssetDatabase.AddObjectToAsset(mesh,MeshPath);
                    }
                    mesh.Clear(); mesh.indexFormat=IndexFormat.UInt32;
                    var g=pair.Value;
                    mesh.SetVertices(g.vertices);mesh.SetNormals(g.normals);mesh.SetUVs(0,g.uv);mesh.SetTriangles(g.indices,0);mesh.RecalculateBounds();
                    EditorUtility.SetDirty(mesh);
                    AssetDatabase.SaveAssetIfDirty(mesh);
                    foreach(var v in g.vertices)bottom=Mathf.Min(bottom,(restRotation*v).y);
                    var go=new GameObject(pair.Key.name);go.transform.SetParent(piece.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=pair.Key;mr.lightProbeUsage=LightProbeUsage.BlendProbes;
                }
                if(!float.IsFinite(bottom))throw new Exception("No geometry generated for section "+section);
                effect.restHeights[section]=-11.48f-bottom;
                if(section%2==0)
                {
                    var fire=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FX+(section==2?"FX_Fire_Big_03":"FX_Fire_Big_02")+".prefab"));
                    fire.transform.SetParent(piece.transform,false);fire.transform.localPosition=new Vector3(section==2?1:-1,.2f,0);
                    foreach(var light in fire.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(light);
                    fire.SetActive(false);fires.Add(fire);
                }
            }
            effect.fires=fires.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Path);
            Debug.Log("Built five closed bridge sections and three fire pockets: "+Path);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
