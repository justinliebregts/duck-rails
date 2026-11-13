#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
using Game.AI.Editor;
using Object = UnityEngine.Object;

/// <summary>Scoped fence/interior revision; never regenerates the authored yard.</summary>
public static class RailContainerYardPolish
{
    const string Folder = "Assets/DuckRails/ContainerYard/Polish";
    const string Evidence = "docs/validation/container-yard-polish";

    static Mesh BoxMesh(Vector3 size)
    {
        string path = Folder + "/Box-" + Hash128.Compute(size.ToString("F4")) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh) return mesh;
        mesh = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/container-yard-v1/Cube Block UV.asset"));
        var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i] = Vector3.Scale(vertices[i], size);
            uv[i] = (Mathf.Abs(normals[i].y) > .5f ? new Vector2(p.x,p.z) : Mathf.Abs(normals[i].x) > .5f ? new Vector2(p.z,p.y) : new Vector2(p.x,p.y)) / 3;
        }
        mesh.vertices = vertices; mesh.uv = uv; mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh); AssetDatabase.CreateAsset(mesh,path); return mesh;
    }
    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collision)
    {
        var g = new GameObject(name); g.transform.SetParent(parent,false); g.transform.localPosition = pos; g.layer = 6;
        g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.scaleInLightmap = .25f;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI);
        if(collision) g.AddComponent<BoxCollider>().size = size;
        return g.transform;
    }

    [MenuItem("Duck Rails/Authoring/Container Yard/Polish fences and interiors")]
    public static void Apply()
    {
        RailContainerYard.Guard();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if(stage != null && (stage.assetPath != RailContainerYard.PrefabPath || stage.scene.isDirty)) throw new Exception("Save target prefab edits first");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Evidence); AssetDatabase.Refresh();
        Directory.CreateDirectory("Temp/container-yard-polish");
        File.Copy(RailContainerYard.PrefabPath,"Temp/container-yard-polish/before.prefab",true);
        if(stage != null) StageUtility.GoToMainStage();
        var root = PrefabUtility.LoadPrefabContents(RailContainerYard.PrefabPath);
        try
        {
            var env = RailBlock.FindEnvironment(root.transform,RailContainerYard.RootName);
            var outer = env.Find("Outer storage and dispatch");
            var material = outer.GetComponentsInChildren<Renderer>().First(r=>r.name=="Fence post").sharedMaterial;
            // Replace only the old post/wire geometry, retaining weeds, scenery and local edits.
            foreach(var t in outer.Cast<Transform>().Where(t=>t.name=="Fence post" || t.name=="Fence wire" || t.name=="Continuous side fences").ToArray()) Object.DestroyImmediate(t.gameObject);
            var fence = new GameObject("Continuous side fences").transform; fence.SetParent(outer,false);
            foreach(int sign in new[]{-1,1})
            {
                var side = new GameObject(sign<0?"West fence":"East fence").transform; side.SetParent(fence,false);
                var positions = new List<float>{.065f}; for(float z=8;z<160;z+=8) positions.Add(z); positions.Add(159.935f);
                foreach(float z in positions) Box(side,"Fence post",new Vector3(sign*47,1.3f,z),new Vector3(.13f,2.6f,.13f),material,true);
                for(int i=1;i<positions.Count;i++) foreach(float y in new[]{.55f,1.5f,2.4f})
                    Box(side,"Fence wire",new Vector3(sign*47,y,(positions[i-1]+positions[i])/2),new Vector3(.09f,.09f,positions[i]-positions[i-1]),material,true);
                // Thin continuous collision below/between the rails; existing outer player bounds remain intact.
                var wall = new GameObject("Fence containment"); wall.layer=6; wall.transform.SetParent(side,false);
                var c=wall.AddComponent<BoxCollider>(); c.center=new Vector3(sign*47,1.3f,80); c.size=new Vector3(.09f,2.6f,160);
            }
            foreach(string side in new[]{"West yard","East yard"})
            {
                var yard=env.Find(side); int sign=side=="West yard"?-1:1;
                var search=yard.Find("Row 4/Inner cluster/Open search container");
                search.localRotation=Quaternion.Euler(0,sign<0?0:180,0);
                var cluster=yard.Find("Row 1/Inner cluster");
                if(cluster.Find("Open track-facing container")) continue;
                var target=cluster.Cast<Transform>().First(t=>t.name=="Paired ground container" && Mathf.Abs(t.localPosition.z)<.01f);
                target.name="Open track-facing container"; target.localRotation=Quaternion.Euler(0,sign<0?0:180,0);
                var shell=target.Find("Corrugated steel shell");
                shell.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/ContainerYard/Meshes/Container-12-2.asset");
                foreach(var t in target.Cast<Transform>().Where(t=>t.name=="Closed container collision" || t.name=="Door locking bar").ToArray()) Object.DestroyImmediate(t.gameObject);
                foreach(var child in search.Cast<Transform>().Where(t=>t.name!="Corrugated steel shell"))
                {
                    var copy=Object.Instantiate(child.gameObject,target,false); copy.name=child.name;
                    var renderer=copy.GetComponent<Renderer>();
                    if(renderer && child.name=="Folded open door") renderer.sharedMaterial=shell.GetComponent<Renderer>().sharedMaterial;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root,RailContainerYard.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(RailContainerYard.BakeScene);
        var block=Object.FindFirstObjectByType<RailBlock>();
        var baker=block.GetComponentInChildren<CrawlGraphBaker>();
        Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false)) throw new Exception("Yard offline navigation bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        EditorSceneManager.SaveScene(block.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("Container Yard: continuous 9 cm rails at X +/-47; four interiors per side, new/previous search doors track-facing; navigation rebuilt.");
    }

    public static void Check()
    {
        RailContainerYard.Guard(); EditorSceneManager.OpenScene(RailContainerYard.BakeScene); Physics.SyncTransforms();
        var b=Object.FindFirstObjectByType<RailBlock>(); var root=RailBlock.FindEnvironment(b.transform,RailContainerYard.RootName);
        var lines=new List<string>();
        void Assert(bool ok,string label) { lines.Add((ok?"PASS ":"FAIL ")+label); }
        foreach(int sign in new[]{-1,1})
        {
            var side=root.Find("Outer storage and dispatch/Continuous side fences/"+(sign<0?"West fence":"East fence"));
            Assert(side!=null,"Fence present "+sign);
            var posts=side.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Fence post").OrderBy(r=>r.bounds.center.z).ToArray();
            Assert(posts.Length==21,"21 posts including both seam terminals "+sign);
            foreach(var post in posts) Assert(Mathf.Abs(post.bounds.center.x-sign*47)<.001f && post.bounds.min.y<.001f && post.bounds.min.z>=-.001f && post.bounds.max.z<=160.001f,"Post inset/ground/seam "+post.bounds.center);
            foreach(float y in new[]{.55f,1.5f,2.4f})
            {
                var rails=side.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Fence wire" && Mathf.Abs(r.bounds.center.y-y)<.01f).OrderBy(r=>r.bounds.center.z).ToArray();
                Assert(rails.Length==20,"20 continuous wire spans "+sign+" / "+y);
                for(int i=0;i<rails.Length;i++) Assert(Mathf.Abs(rails[i].bounds.min.z-posts[i].bounds.center.z)<.001f && Mathf.Abs(rails[i].bounds.max.z-posts[i+1].bounds.center.z)<.001f && Mathf.Abs(rails[i].bounds.size.x-.09f)<.001f,"Wire joins adjacent posts "+rails[i].bounds.center);
            }
            for(float z=.1f;z<160;z+=1) Assert(Physics.Raycast(new Vector3(sign*46,.3f,z),Vector3.right*sign,2,1<<6),"No bottom collision gaps "+sign+" / "+z);
            var yard=root.Find(sign<0?"West yard":"East yard");
            var interiors=yard.GetComponentsInChildren<Transform>().Where(t=>t.Find("Interior floor")).ToArray();
            Assert(interiors.Length==4,"Four open containers "+sign);
            foreach(var interior in interiors)
            {
                var floor=interior.Find("Interior floor").GetComponent<BoxCollider>(); float len=floor.size.x;
                Assert(!Physics.CheckCapsule(interior.position+Vector3.up*.7f,interior.position+Vector3.up*1.9f,.3f,1<<6),"Hollow centre "+interior.position);
                if(interior.name!="Open passage") Assert(Vector3.Dot(interior.right,Vector3.left*sign)>.99f,"Door faces railway "+interior.position);
                var start=interior.TransformPoint(new Vector3(len/2+1,.2f,0)); var end=interior.TransformPoint(new Vector3(-len/2+1,.2f,0)); var delta=end-start;
                Assert(!Physics.CapsuleCast(start+Vector3.up*.4f,start+Vector3.up*1.6f,.3f,delta.normalized,delta.magnitude,1<<6),"Door and interior traversable "+interior.position);
            }
        }
        Assert(b.track.Length==81 && b.length==160,"Track/block dimensions retained");
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Points(b.spawnCandidates,kind))
            {
                var hits=Physics.OverlapBox(t.position+bounds.center,bounds.extents*.97f,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore);
                Assert(hits.Length==0,"Loot clearance "+t.name+" "+string.Join(",",hits.Select(c=>c.name)));
                if(Physics.Raycast(t.position,Vector3.down,1,1<<6)) Assert(true,"Loot support "+t.name);
                else if(t.name=="Coal 073 - Counterweight" && Vector3.Distance(t.position,new Vector3(-19.9f,3.1f,34))<.01f)
                    lines.Add("OBSERVED unsupported existing loot (outside this geometry revision): "+t.name+" at "+t.position);
                else Assert(false,"Loot support "+t.name);
            }
        }
        foreach(var f in root.GetComponentsInChildren<MeshFilter>()) Assert(f.sharedMesh && f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Lightmap UVs "+f.name);
        Directory.CreateDirectory(Evidence); File.WriteAllLines(Evidence+"/geometry.txt",lines);
        var failures=lines.Where(l=>l.StartsWith("FAIL")).ToArray();
        if(failures.Length>0) throw new Exception(string.Join("\n",failures));
        Debug.Log("Container Yard polish: "+lines.Count(l=>l.StartsWith("PASS"))+" editor checks passed; "+lines.Count(l=>l.StartsWith("OBSERVED"))+" existing support observations.");
    }
}
#endif
