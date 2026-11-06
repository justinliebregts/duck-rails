#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailMotelGreyboxChecks
{
    const string Evidence="docs/validation/motel-greybox";
    static readonly List<string> lines=new List<string>();
    static void Check(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);}
    static void Clear(Vector3 a,Vector3 b,string description)
    {
        var d=b-a;
        Check(!Physics.CapsuleCast(a+Vector3.up*.5f,a+Vector3.up*1.5f,.4f,d.normalized,out _,d.magnitude,1<<6,QueryTriggerInteraction.Ignore),description);
    }
    [MenuItem("Duck Rails/Authoring/Motel/Check greybox")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish Play/bake first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene first.");
        EditorSceneManager.OpenScene(RailMotelGreybox.BakeScene);
        var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailMotelGreybox.RootName);var motel=root.Find("Motel");
        lines.Clear();Directory.CreateDirectory(Evidence);Physics.SyncTransforms();
        try
        {
            Check(b.id=="roadside-motel-v1"&&b.length==160&&b.track.Length==81&&b.track.All(p=>p.x==0),"Original straight 160 m track and block identity");
            Check(b.stop.localPosition==new Vector3(0,0,80),"Stop at Z80");
            Check(root.Find("Transformer yard").Cast<Transform>().Count(t=>t.Find("Transformer body")!=null)==6,"Six transformers in three rows");
            Check(motel.Find("Floor 1").Cast<Transform>().Count(t=>t.name.StartsWith("Bed "))==5&&motel.Find("Floor 2").Cast<Transform>().Count(t=>t.name.StartsWith("Bed "))==8,"Five downstairs and eight upstairs guest rooms");
            Check(motel.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Ajar door "))==6,"Six static ajar doors");
            foreach(string name in new[]{"A","B"})
            {
                var stair=motel.Find("Stair "+name);var steps=stair.Cast<Transform>().Where(t=>t.name.StartsWith("Tread ")).ToArray();
                Check(steps.Length==20,"Stair "+name+" twenty risers");
                var points=steps.Select(t=>t.position+Vector3.up*t.localScale.y/2).ToList();
                points.Insert(0,new Vector3(points[0].x,0,points[0].z-.4f));points.Add(points.Last()+Vector3.forward*.5f);
                for(int i=0;i<points.Count-1;i++)Check(CrawlGraphBake.StairSegment(points[i],points[i+1],1<<6),"Stair "+name+" supported capsule step "+i);
                foreach(var p in points)Check(!Physics.Raycast(p+Vector3.up*.02f,Vector3.up,1.95f,1<<6),"Stair "+name+" headroom at "+p);
            }
            Clear(new Vector3(23.8f,3.6f,61.6f),new Vector3(26.5f,3.6f,61.6f),"Stair A landing joins balcony");
            Clear(new Vector3(11.2f,3.6f,92.8f),new Vector3(11.2f,3.6f,95.5f),"Stair B landing joins balcony");
            Clear(new Vector3(26.5f,3.6f,55),new Vector3(26.5f,3.6f,95.5f),"East balcony clear end to end");
            Clear(new Vector3(11.2f,3.6f,95.5f),new Vector3(38.5f,3.6f,95.5f),"North balcony clear end to end");
            for(int floor=0;floor<2;floor++)
            {
                float y=floor*3.6f;
                for(int i=0;i<5;i++)Clear(new Vector3(26.5f,y,56.8f+i*8),new Vector3(30.5f,y,56.8f+i*8),"Guest "+((floor+1)*100+i+1)+" doorway clear beside door leaf");
                for(int i=0;i<3;i++)Clear(new Vector3(12.2f+i*10,y,95.5f),new Vector3(12.2f+i*10,y,99),"North room "+i+" floor "+floor+" doorway clear");
            }
            Clear(new Vector3(-44,0,59),new Vector3(-39,0,59),"West broken chain-link gap passable");
            Clear(new Vector3(-12,0,57),new Vector3(-6,0,57),"East broken chain-link gap passable");
            Clear(new Vector3(-27,0,68),new Vector3(-27,0,77),"Transformer north gate and garage apron clear");
            foreach(float x in new[]{-31f,-21f})Clear(new Vector3(x,0,49),new Vector3(x,0,69),"Transformer aisle "+x+" clear");
            Clear(new Vector3(-40,0,59),new Vector3(-12,0,59),"Transformer cross aisle clear");
            Clear(new Vector3(-30,0,76),new Vector3(-30,0,81),"Garage bay clear");
            Clear(new Vector3(-18,0,83.2f),new Vector3(-14,0,83.2f),"Office door clear");
            Clear(new Vector3(44,0,54),new Vector3(44,0,111),"Rear service lane clear past dumpsters");
            var graph=CrawlGraph.Load(b.graph);int start=graph.FindNearestGroundNode(new Vector3(18,.2f,80),2);
            Check(start>=0,"Courtyard ground graph node");var seen=new HashSet<int>{start};var q=new Queue<int>();q.Enqueue(start);
            while(q.Count>0){int n=q.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetSurface(next)==CrawlSurface.Floor&&seen.Add(next))q.Enqueue(next);}}
            foreach(var p in new[]{new Vector3(23.8f,3.7f,61.6f),new Vector3(11.2f,3.7f,92.8f),new Vector3(26.5f,3.7f,80),new Vector3(35,3.7f,103)})
                Check(seen.Contains(graph.FindNearestGroundNode(p,1.5f)),"Ground-only baked navigation reaches "+p);
            foreach(var marker in b.variations.SelectMany(v=>v.coal.Concat(v.gold)))
            {
                Check(Physics.Raycast(marker.position,Vector3.down,1,1<<6),"Resource supported "+marker.position);
                Check(!Physics.CheckSphere(marker.position+Vector3.up*.2f,.44f,1<<6),"Resource clear "+marker.position);
            }
            var method=typeof(RailWeek3Builder).GetMethod("BuildBlock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var preserved=(RailBlock)method.Invoke(null,new object[]{2});
            Check(AssetDatabase.GetAssetPath(preserved)==RailMotelGreybox.PrefabPath&&preserved.transform.Find(RailMotelGreybox.RootName),"Catalogue builder preserves authored motel");
            if(lines.Any(line=>line.StartsWith("FAIL")))throw new Exception(string.Join("; ",lines.Where(line=>line.StartsWith("FAIL"))));
            RailWeek3Checks.Run();RailCatalogLightingChecks.Validate();
            File.Copy("Temp/rails-week3-checks.txt",Evidence+"/week3-checks.txt",true);
            File.Copy("output/block-lighting/export-checks.txt",Evidence+"/lighting-checks.txt",true);
            Debug.Log("Motel: "+lines.Count+" physical/layout checks plus Week 3 and lighting checks passed.");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    [MenuItem("Duck Rails/Authoring/Motel/Capture greybox")]
    public static void Capture()
    {
        CaptureTo("output/motel-greybox");
    }
    public static void CaptureTo(string folder)
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish Play/bake first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene first.");
        EditorSceneManager.OpenScene(RailMotelGreybox.BakeScene);
        var root=Object.FindFirstObjectByType<RailBlock>().transform.Find(RailMotelGreybox.RootName);
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.14f,.17f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.9f;fill.transform.rotation=Quaternion.Euler(50,25,0);
        Directory.CreateDirectory(folder);
        try
        {
            RenderSettings.fog=false;
            Shot("whole-block",new Vector3(0,135,80),new Vector3(0,0,80),85,true);
            Shot("courtyard",new Vector3(-10,16,35),new Vector3(26,3,81),60,false);
            Shot("transformer-yard",new Vector3(-53,20,32),new Vector3(-25,1,66),50,false);
            root.Find("Motel/Roofs").gameObject.SetActive(false);root.Find("Office and garage/Roof").gameObject.SetActive(false);
            Shot("upper-cutaway",new Vector3(25,80,81),new Vector3(25,0,81),32,true);
            root.Find("Motel/Floor 2").gameObject.SetActive(false);root.Find("Motel/East upper slab").gameObject.SetActive(false);root.Find("Motel/North upper slab").gameObject.SetActive(false);
            Shot("ground-cutaway",new Vector3(25,80,81),new Vector3(25,0,81),32,true);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailMotelGreybox.BakeScene);}
        void Shot(string file,Vector3 eye,Vector3 focus,float size,bool ortho)
        {
            cam.transform.position=eye;cam.transform.rotation=ortho?Quaternion.Euler(90,0,0):Quaternion.LookRotation(focus-eye);cam.orthographic=ortho;cam.orthographicSize=size;cam.fieldOfView=size;
            var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;cam.targetTexture=rt;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try{cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+file+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif


