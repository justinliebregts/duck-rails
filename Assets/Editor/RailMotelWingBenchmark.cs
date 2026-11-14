#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using V = RailMotelAtlasReview.Vertex;

public static class RailMotelWingBenchmark
{
    public const string Folder="Assets/DuckRails/MotelWingBenchmark";
    public const string Prefab=Folder+"/Motel-Wing-Comparison.prefab";
    public static bool Building;
    sealed class Part { public MeshRenderer r; public Mesh mesh; public int sub; public Material material; public RailBakedBlockLighting.Binding light; public Matrix4x4 matrix; public Vector3 center; }
    static string Key(Part p) => $"{Mathf.FloorToInt(p.center.x/12)}_{Mathf.FloorToInt(p.center.y/6)}_{Mathf.FloorToInt(p.center.z/12)}_{p.material.GetInstanceID()}_{p.light.color.GetInstanceID()}_{p.r.shadowCastingMode}_{p.r.receiveShadows}_{p.r.lightProbeUsage}_{p.r.reflectionProbeUsage}_{p.r.renderingLayerMask}_{p.r.sortingLayerID}_{p.r.sortingOrder}_{p.r.motionVectorGenerationMode}";
    static int Combine(List<Part> parts, Transform parent, List<RailBakedBlockLighting.Binding> bindings, string prefix)
    {
        int count=0;
        foreach(var group in parts.GroupBy(Key)) {
            var first=group.First();var combines=group.Select(p=>new CombineInstance{mesh=p.mesh,subMeshIndex=p.sub,transform=p.matrix,lightmapScaleOffset=p.light.scaleOffset}).ToArray();
            var mesh=new Mesh{name=prefix+count,indexFormat=combines.Sum(c=>(long)c.mesh.vertexCount)>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.CombineMeshes(combines,true,true,true);
            AssetDatabase.CreateAsset(mesh,Folder+"/"+prefix+count++.ToString("D3")+".asset");
            var go=new GameObject(mesh.name);go.layer=first.r.gameObject.layer;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(first.r,r);r.enabled=true;r.sharedMaterials=new[]{first.material};
            bindings.Add(new RailBakedBlockLighting.Binding{renderer=r,color=first.light.color,scaleOffset=new Vector4(1,1,0,0)});
        }
        return count;
    }
    [MenuItem("Duck Rails/Tests/Quest Performance/Prepare Motel Wing Comparison")]
    public static void Prepare()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new Exception("Requires Duck Rails Edit Mode");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new Exception("Benchmark already prepared; refusing overwrite");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        AssetDatabase.CopyAsset("Assets/DuckRails/MotelWingReview/Motel-Atlas-Review.prefab",Prefab);
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try {
            var owner=root.GetComponent<RailBakedBlockLighting>();var bindings=owner.bindings.ToList();
            var originals=bindings.Where(b=>b.renderer).ToDictionary(b=>b.renderer);
            var atlas=root.transform.Find("Blender east wing - review").gameObject;
            var common=new GameObject("Common outside-wing consolidation");common.transform.SetParent(root.transform,false);
            var control=new GameObject("Control wing - original material chunks");control.transform.SetParent(root.transform,false);
            var eligible=typeof(RailMotelRenderChunks).GetMethod("Eligible",BindingFlags.NonPublic|BindingFlags.Static);
            var outside=new List<Part>();var inside=new List<Part>();var temp=new List<Mesh>();
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)) {
                if(r.transform.IsChildOf(atlas.transform))continue;
                var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh||!originals.TryGetValue(r,out var light)||!light.color)continue;
                var mesh=f.sharedMesh;var matrix=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                if(r.enabled) {
                    if(!(bool)eligible.Invoke(null,new object[]{r,root.transform}))continue;
                    for(int s=0;s<mesh.subMeshCount;s++)if(mesh.GetIndexCount(s)>0)outside.Add(new Part{r=r,mesh=mesh,sub=s,material=r.sharedMaterials[s],light=light,matrix=matrix,center=root.transform.InverseTransformPoint(r.bounds.center)});
                    continue;
                }
                // Review-disabled Motel renderers are the unmodified source of the selected wing.
                if(!r.transform.IsChildOf(root.transform.Find("Env/Motel greybox v1/Motel")))continue;
                var pos=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var uv2=mesh.uv2;
                var normalMatrix=matrix.inverse.transpose;
                var vertices=pos.Select((p,i)=>new V{p=matrix.MultiplyPoint3x4(p),n=normalMatrix.MultiplyVector(normals[i]).normalized,uv=uv[i],uv2=uv2[i]}).ToArray();
                var kept=new List<V>();var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new List<int>()).ToArray();
                for(int s=0;s<mesh.subMeshCount;s++) {
                    var indices=mesh.GetTriangles(s);
                    for(int i=0;i<indices.Length;i+=3) {
                        var poly=new List<V>{vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]};
                        for(int plane=0;plane<3;plane++)poly=RailMotelAtlasReview.Clip(poly,plane,true);
                        RailMotelAtlasReview.Append(poly,kept,submeshes[s]);
                    }
                }
                if(kept.Count==0)continue;
                var clipped=RailMotelAtlasReview.MakeMesh("Temporary source wing",kept,submeshes);temp.Add(clipped);
                for(int s=0;s<clipped.subMeshCount;s++)if(clipped.GetIndexCount(s)>0)inside.Add(new Part{r=r,mesh=clipped,sub=s,material=r.sharedMaterials[s],light=light,matrix=Matrix4x4.identity,center=root.transform.InverseTransformPoint(r.bounds.center)});
            }
            int exterior=Combine(outside,common.transform,bindings,"Common-");
            int interior=Combine(inside,control.transform,bindings,"Control-");
            foreach(var r in outside.Select(p=>p.r).Distinct())r.enabled=false;
            foreach(var m in temp)UnityEngine.Object.DestroyImmediate(m);
            atlas.SetActive(false);control.SetActive(true);
            var selector=root.AddComponent<RailMotelWingComparison>();selector.control=control;selector.atlas=atlas;
            owner.bindings=bindings.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            var report=$"Same-build wing comparison: {exterior} common outside chunks; {interior} original-material wing chunks vs {atlas.GetComponentsInChildren<MeshRenderer>(true).Length} atlas wing chunks. Both alternatives resident. Production prefab and review unchanged.\n";
            Directory.CreateDirectory("docs/validation/motel-wing-comparison");File.WriteAllText("docs/validation/motel-wing-comparison/preparation.txt",report);Debug.Log(report);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Duck Rails/Tests/Quest Performance/Build Motel Wing Comparison")]
    public static void Build()
    {
        Building=true;
        try {RailQuestPerformanceBuild.Build();}
        finally {Building=false;}
    }
}

// Replace the catalogue reference in the serialized build scene only, never a saved scene.
public sealed class RailMotelWingBuildScene : IProcessSceneWithReport
{
    public int callbackOrder=>100;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!RailMotelWingBenchmark.Building)return;
        if(report==null||(report.summary.options&BuildOptions.Development)==0)throw new BuildFailedException("Motel comparison requires explicit development build");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailMotelWingBenchmark.Prefab).GetComponent<RailBlock>();
        foreach(var route in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailRoute>(true)))
            for(int i=0;i<route.catalog.Length;i++)if(route.catalog[i]&&route.catalog[i].id==prefab.id)route.catalog[i]=prefab;
    }
}
#endif
