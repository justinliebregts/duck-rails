#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Reuse the depot atlas treatment on motel boxes; preserve custom chain-link meshes.</summary>
public static class RailMotelMaterials
{
    public const string Folder="Assets/DuckRails/MotelMaterials";
    const string Evidence="docs/validation/motel-materials";
    [MenuItem("Duck Rails/Authoring/Motel/Apply Quackville rough materials")]
    public static void Apply()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)
            throw new InvalidOperationException("Use Duck Rails Edit Mode with no bake running");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene first");
        RailDepotMaterials.BeginSharedPass(Folder+"/Meshes");
        var scene=EditorSceneManager.OpenScene(RailMotelGreybox.BakeScene);
        var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailMotelGreybox.RootName);
        if(!root)throw new InvalidOperationException("Motel greybox required");
        RailMotelEdgeBanks.Build(root);
        string[] Shape()=>b.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix.ToString("F6")).ToArray();
        var shapes=Shape();var track=b.track.ToArray();
        var lights=b.GetComponentsInChildren<Light>().Select(l=>EditorJsonUtility.ToJson(l)).ToArray();int count=0;
        foreach(var r in b.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.GetComponent<TMPro.TMP_Text>()||r.name=="Fixture")continue;
            if(!r.transform.IsChildOf(root)&&r.name!="Block ground")continue;
            var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;
            if(r.transform.IsChildOf(root.Find("Low edge banks")))continue;
            if(r.name=="Chain-link mesh"){MapFence(f,r);count++;continue;}
            string n=r.name.ToLowerInvariant();
            string kind=n=="wall"?"wall":n.StartsWith("tread ")?"tread":
                n.Contains("roof")?"roof":n=="block ground"||n.Contains("courtyard")||n.Contains("apron")||n.Contains("lane")||n.Contains("approach")?"ground":
                n.Contains("stripe")?"yellow":n.Contains("crate")||n.Contains("desk")||n.Contains("table")?"timber":
                n.Contains("slab")||n.Contains("walk")||n.Contains("landing")||n.Contains("pad")||n.Contains("base")?"concrete":
                n.Contains("bed")||n.Contains("sofa")||n.Contains("counter")?"prop":"steel";
            float floor=r.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="Floor 2")?3.6f:0;
            var oldBounds=f.sharedMesh.bounds;
            RailDepotMaterials.Map(f,r,kind,floor,Folder+"/Meshes");
            if((f.sharedMesh.bounds.size-oldBounds.size).sqrMagnitude>.000001f)throw new InvalidOperationException("Shape changed "+r.name);
            count++;
        }
        if(!shapes.SequenceEqual(Shape())||!track.SequenceEqual(b.track)||!lights.SequenceEqual(b.GetComponentsInChildren<Light>().Select(l=>EditorJsonUtility.ToJson(l))))
            throw new InvalidOperationException("Collision, track or lighting settings changed");
        Physics.SyncTransforms();
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();
        if(!CrawlGraphBake.Bake(baker,false))throw new InvalidOperationException("Motel edge terrain graph bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/apply.txt",count+" surfaces mapped. Collision, track and light settings unchanged. Shared Quackville atlases/materials reused unchanged.\n");
        Validate();RailCatalogLightingAuthoring.BakeOpen();
        Debug.Log("Motel Quackville pass applied: "+count+" surfaces; lighting bake queued");
    }
    static void MapFence(MeshFilter f,MeshRenderer r)
    {
        string sourcePath="Assets/DuckRails/MotelGreybox/Fence-"+f.transform.parent.name.Replace(' ','-')+".asset";
        var source=AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);if(!source)throw new InvalidOperationException(sourcePath);
        string path=Folder+"/Meshes/"+Path.GetFileName(sourcePath);
        var mesh=Object.Instantiate(source);mesh.name=Path.GetFileNameWithoutExtension(path);
        // A small steel tile region on the rectangular Industrial atlas; topology/UV2 remain intact.
        mesh.uv=source.uv.Select(v=>new Vector2((32.5f+v.x*63)/256f,1-(64.5f+v.y*31)/128f)).ToArray();
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved){saved.uv=mesh.uv;EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);mesh=saved;}
        else AssetDatabase.CreateAsset(mesh,path);
        f.sharedMesh=mesh;r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(RailDepotMaterials.Folder+"/Oxidized steel.mat");
        PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
    }
    [MenuItem("Duck Rails/Authoring/Motel/Validate rough materials")]
    public static void Validate()
    {
        var b=AssetDatabase.LoadAssetAtPath<GameObject>(RailMotelGreybox.PrefabPath);int count=0,vertices=0;
        foreach(var f in b.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=f.sharedMesh;if(!mesh||!AssetDatabase.GetAssetPath(mesh).StartsWith(Folder+"/"))continue;
            if(mesh.uv.Length!=mesh.vertexCount||mesh.uv2.Length!=mesh.vertexCount||mesh.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||v.x<0||v.x>1||v.y<0||v.y>1))
                throw new InvalidOperationException("Invalid atlas/lightmap UVs "+f.name);
            var r=f.GetComponent<MeshRenderer>();if(r.sharedMaterials.Any(m=>!m||!m.mainTexture))throw new InvalidOperationException("Missing atlas "+f.name);
            count++;vertices+=mesh.vertexCount;
        }
        var root=b.transform.Find(RailMotelGreybox.RootName);
        foreach(var r in b.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.GetComponent<TMPro.TMP_Text>()||r.name=="Fixture"||(!r.transform.IsChildOf(root)&&r.name!="Block ground"))continue;
            var f=r.GetComponent<MeshFilter>();
            if(!f||!f.sharedMesh||!AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Folder+"/"))
                throw new InvalidOperationException("Unmapped motel surface: "+r.name);
        }
        var banks=b.transform.Find(RailMotelGreybox.RootName+"/Low edge banks");
        if(!banks||banks.childCount!=6)throw new InvalidOperationException("Missing edge banks");
        foreach(var f in banks.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=f.sharedMesh;var bounds=mesh.bounds;
            foreach(var v in mesh.vertices)
            {
                if(Mathf.Abs(v.x)<18||v.x< -48||v.x>48||v.z<0||v.z>160||v.y>.913f)throw new InvalidOperationException("Edge bank leaves approved envelope");
                if(Mathf.Abs(v.x-bounds.min.x)<.001f||Mathf.Abs(v.x-bounds.max.x)<.001f||Mathf.Abs(v.z-bounds.min.z)<.001f||Mathf.Abs(v.z-bounds.max.z)<.001f)
                    if(Mathf.Abs(v.y-.012f)>.001f)throw new InvalidOperationException("Edge bank perimeter not flush");
            }
        }
        Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/checks.txt","PASS "+count+" mapped meshes have complete UV/lightmap channels, in-range atlas coordinates and material textures.\n"+vertices+" vertices across instances; Quest profiling remains pending.\n");
    }
}
#endif
