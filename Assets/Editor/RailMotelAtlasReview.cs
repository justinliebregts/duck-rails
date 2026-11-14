#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Isolated Blender wing review. Never edits the production Motel or catalogue.</summary>
public static class RailMotelAtlasReview
{
    const string Source = "Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
    const string Folder = "Assets/DuckRails/MotelWingReview";
    const string Prefab = Folder + "/Motel-Atlas-Review.prefab";
    const string ScenePath = Folder + "/Motel-Atlas-Review.unity";
    const string Bake = "Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/roadside-motel-v1-bake.unity";
    [Serializable] public class Payload { public Part[] parts; }
    [Serializable] public class Part
    {
        public string name, lightmap;
        public Vector3[] vertices, normals;
        public Vector2[] uv, uv2;
        public int[] triangles;
    }
    internal struct Vertex
    {
        public Vector3 p, n;
        public Vector2 uv, uv2;
        public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
            p=Vector3.LerpUnclamped(a.p,b.p,t), n=Vector3.LerpUnclamped(a.n,b.n,t),
            uv=Vector2.LerpUnclamped(a.uv,b.uv,t), uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,t) };
    }
    static float Distance(Vertex v, int plane) => plane==0 ? v.p.x-22 : plane==1 ? v.p.z-54 : 94-v.p.z;
    internal static List<Vertex> Clip(List<Vertex> input, int plane, bool inside)
    {
        var output=new List<Vertex>();
        // A face exactly on the cut belongs to the wing, not both render versions.
        if(!inside && input.All(v=>Mathf.Abs(Distance(v,plane))<.000001f))return output;
        for(int i=0;i<input.Count;i++) {
            var a=input[i];var b=input[(i+1)%input.Count];
            float da=Distance(a,plane)*(inside?1:-1), db=Distance(b,plane)*(inside?1:-1);
            if(da>=0)output.Add(a);
            if((da>0&&db<0)||(da<0&&db>0))output.Add(Vertex.Lerp(a,b,da/(da-db)));
        }
        return output;
    }
    internal static bool HasArea(List<Vertex> p)
    {
        for(int i=1;i+1<p.Count;i++)if(Vector3.Cross(p[i].p-p[0].p,p[i+1].p-p[0].p).sqrMagnitude>1e-12f)return true;
        return false;
    }
    internal static Mesh MakeMesh(string name, List<Vertex> vertices, List<int>[] submeshes)
    {
        var mesh=new Mesh { name=name, indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
        mesh.SetVertices(vertices.Select(v=>v.p).ToList());mesh.SetNormals(vertices.Select(v=>v.n.normalized).ToList());
        mesh.SetUVs(0,vertices.Select(v=>v.uv).ToList());mesh.SetUVs(1,vertices.Select(v=>v.uv2).ToList());
        mesh.subMeshCount=submeshes.Length;
        for(int i=0;i<submeshes.Length;i++)mesh.SetTriangles(submeshes[i],i);
        mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    internal static void Append(List<Vertex> poly,List<Vertex> vertices,List<int> indices)
    {
        for(int j=1;j+1<poly.Count;j++) {
            if(Vector3.Cross(poly[j].p-poly[0].p,poly[j+1].p-poly[0].p).sqrMagnitude<=1e-12f)continue;
            foreach(var v in new[]{poly[0],poly[j],poly[j+1]}){indices.Add(vertices.Count);vertices.Add(v);}
        }
    }
    static Texture2D Texture(string name,bool srgb)
    {
        string path=Folder+"/"+name+".png";
        File.Copy("ArtSource/MotelWingPilot/"+name+".png",path,false);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture=srgb;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false;importer.filterMode=FilterMode.Point;
        importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=1024;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [MenuItem("Duck Rails/Authoring/Motel/Create isolated Blender wing review")]
    public static void Create()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)
            throw new InvalidOperationException("Use idle Duck Rails Edit Mode.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new InvalidOperationException("Review already exists; use Open review. Do not overwrite review edits.");
        var payload=JsonUtility.FromJson<Payload>(File.ReadAllText("ArtSource/MotelWingPilot/unity-meshes.json"));
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Motel wing single atlas"};
        mat.SetTexture("_BaseMap",Texture("MotelWing_BaseColor",true));mat.SetColor("_BaseColor",Color.white);
        mat.SetTexture("_EmissionMap",Texture("MotelWing_Emission",false));mat.SetColor("_EmissionColor",Color.white*8);mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_MetallicGlossMap",Texture("MotelWing_MetallicSmoothness",false));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);
        mat.SetFloat("_SmoothnessTextureChannel",0);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        AssetDatabase.CreateAsset(mat,Folder+"/MotelWing.mat");
        AssetDatabase.CopyAsset(Source,Prefab);
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try {
            var opt=root.GetComponent<RailSceneryChunks>();
            if(opt){if(opt.chunkRoot)UnityEngine.Object.DestroyImmediate(opt.chunkRoot);UnityEngine.Object.DestroyImmediate(opt);}
            var owner=root.GetComponent<RailBakedBlockLighting>();var bindings=owner.bindings.ToList();
            var originalBindings=bindings.Where(b=>b.renderer).ToDictionary(b=>b.renderer);
            var visual=new GameObject("Blender east wing - review");visual.transform.SetParent(root.transform,false);
            var remainderRoot=new GameObject("Original render portions outside wing");remainderRoot.transform.SetParent(root.transform,false);
            int replaced=0,remainders=0;
            var sources=root.transform.Find("Env/Motel greybox v1/Motel").GetComponentsInChildren<MeshRenderer>(true);
            foreach(var r in sources) {
                var filter=r.GetComponent<MeshFilter>();if(!r.enabled||!filter||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
                var mesh=filter.sharedMesh;var materials=r.sharedMaterials;
                if(Enumerable.Range(0,mesh.subMeshCount).Any(i=>mesh.GetIndexCount(i)>0&&(i>=materials.Length||!materials[i]||materials[i].shader.name!="Universal Render Pipeline/Lit")))continue;
                var pos=mesh.vertices;var norm=mesh.normals;var uv=mesh.uv;var uv2=mesh.uv2;
                var matrix=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                var input=pos.Select((p,i)=>new Vertex{p=matrix.MultiplyPoint3x4(p),n=normalMatrix.MultiplyVector(norm[i]).normalized,
                    uv=uv.Length==pos.Length?uv[i]:Vector2.zero,uv2=uv2.Length==pos.Length?uv2[i]:Vector2.zero}).ToArray();
                var remaining=new List<Vertex>();var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new List<int>()).ToArray();bool touched=false;
                for(int s=0;s<mesh.subMeshCount;s++) {
                    var indices=mesh.GetTriangles(s);
                    for(int i=0;i<indices.Length;i+=3) {
                        var original=new List<Vertex>{input[indices[i]],input[indices[i+1]],input[indices[i+2]]};
                        var inside=original;
                        for(int plane=0;plane<3;plane++)inside=Clip(inside,plane,true);
                        if(!HasArea(inside)){Append(original,remaining,submeshes[s]);continue;}
                        touched=true;inside=original;
                        for(int plane=0;plane<3;plane++){Append(Clip(inside,plane,false),remaining,submeshes[s]);inside=Clip(inside,plane,true);}
                    }
                }
                if(!touched)continue;
                r.enabled=false;replaced++;
                // Roundoff at the cut must not leave coplanar faces under the atlas wing.
                for(int s=0;s<submeshes.Length;s++) {
                    var clean=new List<int>();var indices=submeshes[s];
                    for(int i=0;i<indices.Count;i+=3) {
                        var tri=new[]{indices[i],indices[i+1],indices[i+2]};
                        if(tri.All(j=>remaining[j].p.x>=22-.000001f && remaining[j].p.z>=54-.000001f && remaining[j].p.z<=94+.000001f))continue;
                        clean.AddRange(tri);
                    }
                    submeshes[s]=clean;
                }
                if(remaining.Count==0)continue;
                var rem=MakeMesh("Retained outside wing "+remainders,remaining,submeshes);
                AssetDatabase.CreateAsset(rem,Folder+"/Remainder-"+remainders.ToString("D3")+".asset");remainders++;
                var go=new GameObject(r.name+" - retained outside wing");go.transform.SetParent(remainderRoot.transform,false);go.layer=r.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh=rem;
                var renderer=go.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(r,renderer);renderer.enabled=true;
                if(originalBindings.TryGetValue(r,out var b)){b.renderer=renderer;bindings.Add(b);}
            }
            int count=0;
            foreach(var part in payload.parts) {
                var mesh=new Mesh{name=part.name,indexFormat=part.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,
                    vertices=part.vertices,normals=part.normals,uv=part.uv,uv2=part.uv2,triangles=part.triangles};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh,Folder+"/Wing-"+count++.ToString("D2")+".asset");
                var go=new GameObject(part.name);go.layer=6;go.transform.SetParent(visual.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
                renderer.receiveGI=ReceiveGI.Lightmaps;renderer.lightProbeUsage=LightProbeUsage.Off;
                var lightmap=AssetDatabase.LoadAssetAtPath<Texture2D>(part.lightmap);
                if(!lightmap)throw new InvalidOperationException("Missing source lightmap: "+part.lightmap);
                bindings.Add(new RailBakedBlockLighting.Binding{renderer=renderer,color=lightmap,scaleOffset=new Vector4(1,1,0,0)});
            }
            owner.bindings=bindings.ToArray();PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            Debug.Log($"Motel review authored: {replaced} original renderers disabled, {remainders} outside-wing remnants, {count} Blender chunks. Collision and gameplay objects retained.");
        } finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        if(!AssetDatabase.CopyAsset(Bake,ScenePath))throw new IOException("Could not copy review lighting scene.");
        Open();
    }
    [MenuItem("Duck Rails/Authoring/Motel/Open Blender wing review")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&stage.scene.isDirty)throw new InvalidOperationException("Save your current prefab edits first.");
        StageUtility.GoToMainStage();
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var review=scene.GetRootGameObjects().FirstOrDefault(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==Prefab);
        if(!review) {
            foreach(var g in scene.GetRootGameObjects().Where(g=>g.GetComponent<RailBlock>()!=null).ToArray())UnityEngine.Object.DestroyImmediate(g);
            review=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
        }
        var maps=LightmapSettings.lightmaps.ToList();
        foreach(var b in review.GetComponent<RailBakedBlockLighting>().bindings) {
            if(!b.renderer||!b.color)continue;
            int i=maps.FindIndex(m=>m.lightmapColor==b.color);
            if(i<0){i=maps.Count;maps.Add(new LightmapData{lightmapColor=b.color});}
            b.renderer.lightmapIndex=i;b.renderer.lightmapScaleOffset=b.scaleOffset;
        }
        LightmapSettings.lightmaps=maps.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=review.transform.Find("Blender east wing - review").gameObject;
        var view=SceneView.lastActiveSceneView;
        if(view){view.sceneLighting=true;view.sceneViewState.showFog=true;view.LookAt(new Vector3(31,3.5f,74),Quaternion.LookRotation(new Vector3(1,-.4f,1)),35);view.Repaint();}
        Debug.Log("Review scene open with original baked textures; no Play Mode or tests run. Use Scene lighting toggle for neutral inspection.");
    }
}
#endif
