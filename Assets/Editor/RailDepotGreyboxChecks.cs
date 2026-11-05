#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using Game.AI.Editor;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RailDepotGreyboxChecks
{
    const string Evidence="docs/validation/depot-greybox/v3";
    static readonly List<string> lines=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);lines.Add("PASS "+message);}
    [MenuItem("Duck Rails/Authoring/Depot/Check two-floor greybox")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish Play/bake first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene first.");
        EditorSceneManager.OpenScene(RailDepotGreybox.BakeScene);
        var block=Object.FindFirstObjectByType<RailBlock>();var root=block.transform.Find(RailDepotGreybox.RootName);
        lines.Clear();Physics.SyncTransforms();
        Check(block.track.Length==81&&Vector3.Distance(block.track[40],new Vector3(-4,0,80))<.001f,"Track samples and curve retained");
        var names=new[]{"Parts store","Workshop","Repair shed"};
        var sizes=new[]{new Vector3(26.4f,7.2f,21.6f),new Vector3(19.2f,6.8f,21.6f),new Vector3(24,8.4f,36)};
        for(int k=0;k<3;k++)
        {
            var b=root.Find(names[k]);var roof=b.Find("Simple roof");var s=sizes[k];
            Check(Mathf.Abs(roof.localScale.x-s.x)<.001f&&Mathf.Abs(roof.localScale.z-s.z)<.001f&&Mathf.Abs(roof.localPosition.y-s.y-.1f)<.001f,names[k]+" blueprint dimensions");
            foreach(string stairName in new[]{"Stair A","Stair B"})
            {
                var t=b.Find(stairName);var steps=t.Cast<Transform>().Where(c=>c.name.StartsWith("Tread ")).ToArray();
                var points=steps.Select(c=>c.position+Vector3.up*c.localScale.y/2).ToList();
                points.Insert(0,new Vector3(points[0].x,0,points[0].z-.4f));
                points.Add(points.Last()+Vector3.forward*.5f);
                for(int i=0;i<points.Count-1;i++)Check(CrawlGraphBake.StairSegment(points[i],points[i+1],1<<6),names[k]+" "+stairName+" supported capsule step "+i);
                foreach(var p in points)Check(!Physics.Raycast(p+Vector3.up*.02f,Vector3.up,1.95f,1<<6),names[k]+" "+stairName+" 1.95 m headroom");
            }
        }
        // Actual baked floor links must connect the outside encounter to both mezzanine landings.
        var graph=CrawlGraph.Load(block.graph);int start=graph.FindNearestGroundNode(new Vector3(12,.2f,80),2);
        Check(start>=0,"Outside navigation seed");var seen=new HashSet<int>{start};var q=new Queue<int>();q.Enqueue(start);
        while(q.Count>0){int n=q.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetSurface(next)==CrawlSurface.Floor&&seen.Add(next))q.Enqueue(next);}}
        foreach(var p in new[]{new Vector3(36.2f,4.1f,70),new Vector3(36.2f,4.1f,94),new Vector3(17.5f,4.1f,77),new Vector3(28,4.1f,69)})
            Check(seen.Contains(graph.FindNearestGroundNode(p,1.5f)),"Ground-only graph reaches shed mezzanine "+p);
        // Clear physical doorways and short outdoor connector, using a duck-sized sweep.
        foreach(var pair in new[]{new[]{new Vector3(-18,0,101),new Vector3(-14,0,101)},new[]{new Vector3(-18,0,65),new Vector3(-14,0,65)},new[]{new Vector3(14,0,80),new Vector3(18,0,80)},new[]{new Vector3(26,0,56),new Vector3(26,0,61)},new[]{new Vector3(28,0,93),new Vector3(28,0,102)}})
        {var d=pair[1]-pair[0];Check(!Physics.SphereCast(pair[0]+Vector3.up*.65f,.4f,d.normalized,out _,d.magnitude,1<<6),"Clear entrance/connector "+pair[0]+" to "+pair[1]);}
        RailWeek3Checks.Run();RailCatalogLightingChecks.Validate();
        Directory.CreateDirectory(Evidence);File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);
        File.Copy("Temp/rails-week3-checks.txt",Evidence+"/week3-checks.txt",true);
        File.Copy("output/block-lighting/export-checks.txt",Evidence+"/lighting-checks.txt",true);
        Debug.Log("Depot V3: "+lines.Count+" geometry checks passed; Week 3 and lighting checks passed.");
    }
    [MenuItem("Duck Rails/Authoring/Depot/Capture review cutaways")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish Play/bake first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene first.");
        EditorSceneManager.OpenScene(RailDepotGreybox.BakeScene);
        var block=Object.FindFirstObjectByType<RailBlock>();var root=block.transform.Find(RailDepotGreybox.RootName);
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.11f,.13f,.16f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.85f;fill.transform.rotation=Quaternion.Euler(50,25,0);
        Directory.CreateDirectory("output/depot-greybox/v3");
        try
        {
            foreach(var b in new[]{"Parts store","Workshop","Repair shed"})root.Find(b+"/Simple roof").gameObject.SetActive(false);
            Shot("overview-cutaway",new Vector3(-66,75,12),new Vector3(0,0,84),60,70);
            foreach(var name in new[]{"Parts store","Workshop","Repair shed"})
            {
                var b=root.Find(name);var roof=b.Find("Simple roof");var focus=b.TransformPoint(new Vector3(roof.localPosition.x,0,roof.localPosition.z));
                float size=name=="Repair shed"?22:17;
                Shot(name.Replace(' ','-').ToLowerInvariant()+"-floor-2",focus+new Vector3(0,60,0),focus,size,90);
                var hidden=new[]{b.Find("Floor 2 deck"),b.Find("Floor 2 rooms and props")};foreach(var t in hidden)t.gameObject.SetActive(false);
                Shot(name.Replace(' ','-').ToLowerInvariant()+"-floor-1",focus+new Vector3(0,60,0),focus,size,90);
                foreach(var t in hidden)t.gameObject.SetActive(true);
            }
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailDepotGreybox.BakeScene);}
        void Shot(string file,Vector3 eye,Vector3 focus,float size,float angle)
        {
            cam.transform.position=eye;cam.transform.rotation=angle==90?Quaternion.Euler(90,0,0):Quaternion.LookRotation(focus-eye);
            cam.orthographic=true;cam.orthographicSize=size;
            var rt=new RenderTexture(1400,1100,24);var old=RenderTexture.active;cam.targetTexture=rt;
            var tex=new Texture2D(1400,1100,TextureFormat.RGB24,false);
            try{cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1100),0,0);tex.Apply();File.WriteAllBytes("output/depot-greybox/v3/"+file+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif
