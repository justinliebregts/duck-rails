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

public static class RailFarmSilosChecks
{
    const string Evidence="docs/validation/farm-silos";
    static readonly List<string> lines=new();
    static void Check(bool ok,string n){lines.Add((ok?"PASS ":"FAIL ")+n);}
    static void ClearHouse(Vector3 a,Vector3 b,string n)=>Clear(RailFarmSilos.HousePoint(a),RailFarmSilos.HousePoint(b),n);
    static void Clear(Vector3 a,Vector3 b,string n)
    {var d=b-a;Check(!Physics.CheckCapsule(a+Vector3.up*.55f,a+Vector3.up*1.55f,.35f,1<<6)&&!Physics.CapsuleCast(a+Vector3.up*.55f,a+Vector3.up*1.55f,.35f,d.normalized,out _,d.magnitude,1<<6),n);}
    [MenuItem("Duck Rails/Authoring/Farm Silos/Validate farm geometry")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Finish bake/play and save first");
        EditorSceneManager.OpenScene(RailFarmSilos.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);
        var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailFarmSilos.RootName);var house=root.Find("Farmhouse");
        try
        {
            Check(b.id==RailFarmSilos.Id&&b.length==160&&b.track.Length==81,"Identity and length");
            Check(b.track.Select((p,i)=>Vector3.Distance(p,new Vector3(-4*Mathf.Pow(Mathf.Sin(Mathf.PI*i/80),2),0,i*2))<.001f).All(v=>v),"Original sampled rail curve");
            Check(b.stop.localPosition==new Vector3(-4,0,80),"Original stop");Check(b.enemyBounds==new Bounds(new Vector3(23,4,80),new Vector3(28,12,70)),"Enemy bounds unchanged");
            Check(house.Find("Basement")&&house.Find("Ground floor")&&house.Find("Upper floor")&&house.Find("Attic"),"Four accessible house levels authored");
            Check(root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Apple tree")==9,"Nine apple trees");
            Check(root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Apple")==81,"Visible apples on all trees");
            Check(root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Corn field "))==3,"Three extensive corn fields");
            Check(b.GetComponentsInChildren<Light>().Length==RailFarmSilos.LightCount&&b.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"18 baked-only lights");
            Check(house.GetComponentsInChildren<Transform>().Count(t=>t.name=="Stair flight")==4,"Three internal flights plus external cellar stair");
            ClearHouse(new Vector3(24,0,122),new Vector3(24,0,145),"Porch and continuous house hall");
            ClearHouse(new Vector3(24,3.2f,129),new Vector3(24,3.2f,145),"Upper floor hall");
            foreach(float y in new[]{0f,3.2f})foreach(float z in new[]{131.5f,140.5f}){ClearHouse(new Vector3(20.5f,y,z),new Vector3(25,y,z),"West room door "+y+" / "+z);ClearHouse(new Vector3(25,y,z),new Vector3(29,y,z),"East room door "+y+" / "+z);}
            ClearHouse(new Vector3(31,-3.2f,143),new Vector3(31,-3.2f,146.8f),"Rear cellar doorway and landing");
            ClearHouse(new Vector3(19,-3.2f,136.5f),new Vector3(27,-3.2f,136.5f),"Basement utility doorway");
            ClearHouse(new Vector3(23,6.4f,137.8f),new Vector3(31.5f,6.4f,137.8f),"Attic stair arrival and storage passage");
            foreach(var flight in house.GetComponentsInChildren<Transform>().Where(t=>t.name=="Stair flight"))foreach(Transform tread in flight)
            {
                var box=tread.GetComponent<BoxCollider>().bounds;var foot=new Vector3(box.center.x,box.max.y+.02f,box.center.z);
                Check(!Physics.CheckCapsule(foot+Vector3.up*.45f,foot+Vector3.up*1.7f,.28f,1<<6),"Stair full-height clearance "+flight.parent.name+" / "+tread.name);
                Check(Physics.Raycast(foot+Vector3.up*.1f,Vector3.down,out var hit,.3f,1<<6)&&Mathf.Abs(hit.point.y-box.max.y)<.025f,"Stair support "+flight.parent.name+" / "+tread.name);
            }
            foreach(float y in new[]{-3.2f,0f,3.2f,6.4f}){Check(Physics.Raycast(RailFarmSilos.HousePoint(new Vector3(24,y+.2f,137)),Vector3.down,out var hit,.5f,1<<6)&&Mathf.Abs(hit.point.y-y-1.5f)<.04f,"House floor support "+y);}
            // Headroom below the former solid ground slab proves excavation, not only a basement shell.
            ClearHouse(new Vector3(19,-3.2f,130),new Vector3(19,-3.2f,141),"Excavated basement west aisle");
            Clear(new Vector3(14,0,76),new Vector3(24,0,76),"Barn wide west entry");Clear(new Vector3(26,0,85),new Vector3(26,0,94),"Barn rear exit");
            Check(RailFarmSilos.CornAreas.Sum(r=>r.width*r.height)>4400,"Over 4400 square metres allocated to corn");
            foreach(var m in b.variations.SelectMany(v=>v.coal.Concat(v.gold)))
            {Check(Physics.Raycast(m.position,Vector3.down,1,1<<6),"Loot support "+m.position);Check(!Physics.CheckSphere(m.position+Vector3.up*.2f,.4f,1<<6),"Loot free space "+m.position);}
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("hay pyramid")))
            {
                var boxes=t.GetComponentsInChildren<BoxCollider>();Check(boxes.Length>3,"Climbable stepped hay "+t.name);
                var tops=boxes.Select(c=>c.bounds.max.y).Distinct().OrderBy(y=>y).ToArray();Check(tops.Zip(tops.Skip(1),(a,c)=>c-a).All(d=>d<.7f),"Hay rise below 0.7m "+t.name);
            }
            foreach(var c in root.GetComponentsInChildren<Collider>().Concat(b.transform.Find("Left-side buildings").GetComponentsInChildren<Collider>()))
            {
                if(c.transform.IsChildOf(root.Find("Ground with basement excavation")))continue;
                var q=c.bounds;Check(q.min.x>=-48&&q.max.x<=48&&q.min.z>=6&&q.max.z<=154,"Block seam clearance "+c.name);
                Check(q.max.x<=-10||q.min.x>=7,"Rail envelope clear "+c.name);
            }
            foreach(var f in root.GetComponentsInChildren<MeshFilter>()){var m=f.sharedMesh;Check(m&&m.uv.Length==m.vertexCount&&m.uv2.Length==m.vertexCount,"UVs "+f.name);}
            var graph=CrawlGraph.Load(b.graph);foreach(var v in b.variations)foreach(var e in v.enemies)Check(graph.FindNearestGroundNode(e.localPosition,2)>=0,"Enemy graph support "+e.localPosition);
            var method=typeof(RailWeek3Builder).GetMethod("BuildBlock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var kept=(RailBlock)method.Invoke(null,new object[]{6});Check(kept&&kept.transform.Find(RailFarmSilos.RootName),"Catalogue rebuild preserves farm");
            if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL"))));
            RailWeek3Checks.Run();RailCatalogLightingChecks.Validate();File.Copy("Temp/rails-week3-checks.txt",Evidence+"/week3-checks.txt",true);File.Copy("output/block-lighting/export-checks.txt",Evidence+"/lighting-checks.txt",true);
            Debug.Log("Farm "+lines.Count+" geometry checks passed, plus catalogue and lighting");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    [MenuItem("Duck Rails/Authoring/Farm Silos/Capture farm")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Finish/save first");
        EditorSceneManager.OpenScene(RailFarmSilos.BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();
        var go=new GameObject("Temporary farm review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.16f,.19f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.1f;fill.transform.rotation=Quaternion.Euler(50,25,0);Directory.CreateDirectory("output/farm-silos");
        try
        {
            RenderSettings.fog=false;
            Shot("top-down",new Vector3(0,130,80),new Vector3(0,0,80.01f),75);
            Shot("whole-farm",new Vector3(-60,80,5),new Vector3(0,0,85),65);
            Shot("farmhouse",new Vector3(0,10,111),new Vector3(-27,6,134),65);
            Shot("barn-and-silos",new Vector3(-6,11,49),new Vector3(25,4,83),65);
            Shot("apple-orchard",new Vector3(-10,5,116),new Vector3(-27,5,135),75);
            HouseShot("basement",new Vector3(19,-1.7f,130),new Vector3(21,-1.7f,142),85);
            HouseShot("ground-floor",new Vector3(24,1.5f,129),new Vector3(24,1.5f,144),85);
            HouseShot("upper-floor",new Vector3(24,4.7f,129),new Vector3(24,4.7f,144),85);
            HouseShot("attic",new Vector3(31,7.9f,138),new Vector3(18,7.9f,137),85);
            Shot("corn-lane",new Vector3(-21,1.2f,16),new Vector3(-18,1.2f,25),80);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailFarmSilos.BakeScene);}
        void HouseShot(string n,Vector3 eye,Vector3 target,float fov)=>Shot(n,RailFarmSilos.HousePoint(eye),RailFarmSilos.HousePoint(target),fov);
        void Shot(string n,Vector3 eye,Vector3 target,float fov)
        {cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes("output/farm-silos/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
    }
}
#endif

