#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailConvenienceStoreChecks
{
    const string Evidence="docs/validation/convenience-store-v5";
    static readonly List<string> lines=new List<string>();
    static void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);}
    static void Clear(Vector3 a,Vector3 b,string label)
    {
        var d=b-a;Check(!Physics.CheckCapsule(a+Vector3.up*.55f,a+Vector3.up*1.55f,.4f,1<<6)&&
            !Physics.CapsuleCast(a+Vector3.up*.55f,a+Vector3.up*1.55f,.4f,d.normalized,out _,d.magnitude,1<<6),label);
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Validate installed block")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Finish Play/bake and save scene first.");
        EditorSceneManager.OpenScene(RailConvenienceStore.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);
        var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailConvenienceStore.RootName);
        try
        {
            Check(b.id=="convenience-store-v1"&&b.length==160&&b.track.Length==81&&b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(v=>v),"Original identity and straight 160 m track");
            Check(b.stop.localPosition==new Vector3(0,0,80),"Stop retained");
            Check(b.enemyBounds==new Bounds(new Vector3(26,4,85),new Vector3(34,12,80)),"Expanded northward east-only enemy bounds");
            Check(root.Find("Store").Cast<Transform>().Count(t=>t.name.StartsWith("Shelf run"))==3,"Three shelf runs");
            Check(root.Find("Arcade").Cast<Transform>().Count(t=>t.name.StartsWith("Arcade cabinet"))==8,"Eight arcade cabinets");
            Check(root.GetComponentsInChildren<Light>().Length==RailConvenienceStore.LightCount&&root.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Thirty baked-only fixtures");
            Check(root.GetComponentsInChildren<Light>().Count(l=>l.name=="Road streetlight")==8,"Eight road streetlights");
            Check(root.Find("Gas forecourt").Cast<Transform>().Count(t=>t.name=="Gas pump")==4,"Four gas pump bodies");
            Clear(new Vector3(7.5f,0,78.5f),new Vector3(25,0,78.5f),"Track-facing store approach");
            Clear(new Vector3(34,0,80.5f),new Vector3(43,0,80.5f),"Store stockroom and rear exit");
            foreach(float z in new[]{71f,75.5f,80f})Clear(new Vector3(25,0,z),new Vector3(35,0,z),"Shelf cross aisle "+z);
            Clear(new Vector3(24,0,64),new Vector3(24,0,80),"Store front aisle");
            Clear(new Vector3(41,0,60),new Vector3(41,0,88),"Dumpster bypass");
            Clear(new Vector3(-8,0,78.5f),new Vector3(-31,0,78.5f),"Arcade track-facing entry");
            Clear(new Vector3(-31,0,78.5f),new Vector3(-31,0,71.5f),"Arcade rear aisle");
            Clear(new Vector3(-31,0,71.5f),new Vector3(-39,0,71.5f),"Arcade rear escape");
            Clear(new Vector3(34,0,35),new Vector3(34,0,55),"Car wash walk-through");
            Clear(new Vector3(26.5f,0,35),new Vector3(26.5f,0,55),"Car wash exterior bypass");
            Clear(new Vector3(15,0,87),new Vector3(15,0,101),"Gas canopy central lane");
            for(int i=0;i<3;i++)Clear(new Vector3(14.5f+i*10,0,118.8f),new Vector3(14.5f+i*10,0,134),"Restaurant front/kitchen/rear route "+i);
            foreach(float x in new[]{-13.5f,25.5f})Clear(new Vector3(x,0,102),new Vector3(x,0,114),"Crosswalk "+x);
            Clear(new Vector3(-10,0,108),new Vector3(10,0,108),"Rail crossing clear");
            foreach(float x in new[]{-5.5f,5.5f})Clear(new Vector3(x,0,10),new Vector3(x,0,150),"Continuous trackside walk "+x);            foreach(var marker in b.variations.SelectMany(v=>v.coal.Concat(v.gold)))
            {Check(Physics.Raycast(marker.position,Vector3.down,1,1<<6),"Resource support "+marker.position);Check(!Physics.CheckSphere(marker.position+Vector3.up*.2f,.44f,1<<6),"Resource clearance "+marker.position);}
            foreach(var collider in root.GetComponentsInChildren<Collider>())
            {
                var q=collider.bounds;Check(q.min.x>=-48&&q.max.x<=48&&q.min.z>=6&&q.max.z<=154.1f,"Seam clearance "+collider.name);
                Check(q.max.x<=-4||q.min.x>=4,"Clear rail envelope "+collider.name);
            }
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                if(f.GetComponent<TMPro.TMP_Text>())continue;var mesh=f.sharedMesh;
                Check(mesh&&mesh.uv.Length==mesh.vertexCount&&mesh.uv2.Length==mesh.vertexCount,"UV0 and lightmap UVs "+f.name);
            }
            var graph=CrawlGraph.Load(b.graph);var start=graph.FindNearestGroundNode(new Vector3(24,.2f,80),2);var seen=new HashSet<int>();var queue=new Queue<int>();if(start>=0){seen.Add(start);queue.Enqueue(start);}
            while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetSurface(next)==CrawlSurface.Floor&&seen.Add(next))queue.Enqueue(next);}}
            foreach(var p in new[]{new Vector3(24,.2f,75),new Vector3(38,.2f,80),new Vector3(34,.2f,45),new Vector3(25,.2f,125),new Vector3(35,.2f,130)})Check(seen.Contains(graph.FindNearestGroundNode(p,2)),"Ground navigation connects "+p);
            var method=typeof(RailWeek3Builder).GetMethod("BuildBlock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var preserved=(RailBlock)method.Invoke(null,new object[]{1});Check(AssetDatabase.GetAssetPath(preserved)==RailConvenienceStore.PrefabPath&&preserved.transform.Find(RailConvenienceStore.RootName),"Catalogue rebuild preserves authored store");
            if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL"))));
            RailWeek3Checks.Run();RailCatalogLightingChecks.Validate();File.Copy("Temp/rails-week3-checks.txt",Evidence+"/week3-checks.txt",true);File.Copy("output/block-lighting/export-checks.txt",Evidence+"/lighting-checks.txt",true);
            Debug.Log("Store: "+lines.Count+" physical/layout/UV/navigation checks plus catalogue checks passed.");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Capture review views")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save/finish first");
        EditorSceneManager.OpenScene(RailConvenienceStore.BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.17f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.9f;fill.transform.rotation=Quaternion.Euler(50,25,0);
        Directory.CreateDirectory("output/convenience-store-v5");
        try
        {
            RenderSettings.fog=false;
            
            Shot("street",new Vector3(-3,24,45),new Vector3(15,2,90),65,false);
            Shot("store-front",new Vector3(9,3,71),new Vector3(25,2,77),70,false);
            Shot("store-interior",new Vector3(24,1.2f,79),new Vector3(31,1.2f,70),85,false);
            Shot("arcade",new Vector3(-17,1.2f,78.5f),new Vector3(-30,1.3f,78.5f),80,false);
            Shot("whole-block",new Vector3(0,95,80),new Vector3(0,0,80),83,true);
            b.transform.Find(RailConvenienceStore.RootName+"/Store/Roofs").gameObject.SetActive(false);
            Shot("store-cutaway",new Vector3(31,45,74),new Vector3(31,0,74),14,true);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailConvenienceStore.BakeScene);}
        void Shot(string name,Vector3 eye,Vector3 target,float size,bool ortho)
        {
            cam.transform.position=eye;cam.transform.rotation=ortho?Quaternion.Euler(90,0,0):Quaternion.LookRotation(target-eye);cam.orthographic=ortho;cam.orthographicSize=size;cam.fieldOfView=size;
            var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes("output/convenience-store-v5/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif



