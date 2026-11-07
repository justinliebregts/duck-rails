#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
public static class RailMetroChecks
{
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Capture review")]
    public static void Capture()
    {
        var go=new GameObject("Metro review camera");var cam=go.AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=200;cam.fieldOfView=78;
        Directory.CreateDirectory("output/underground-station-v2");
        try
        {
            Shot("main-platform",new(-19,-1.5f,62),new(-19,-1.5f,98));
            Shot("lower-wreck",new(4,-6.5f,65),new(9.5f,-6.5f,97));
            Shot("wreck-interior",new(9.5f,-6.5f,66),new(9.5f,-6.5f,95));
            Shot("stairs-down",new(-.5f,-1.5f,63),new(-.5f,-6.5f,43));
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
        void Shot(string name,Vector3 eye,Vector3 target)
        {
            cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            var rt=new RenderTexture(1440,900,24);var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes("output/underground-station-v2/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
        }
    }
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Validate geometry")]
    public static void Run()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong project/mode");
        var b=UnityEngine.Object.FindFirstObjectByType<RailBlock>();Physics.SyncTransforms();var lines=new List<string>();
        void Check(bool ok,string label)=>lines.Add((ok?"PASS ":"FAIL ")+label);
        void Walk(Vector3 p,string label)
        {
            Check(Physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var h,.25f,1<<6)&&Mathf.Abs(h.point.y-p.y)<.05f,"floor "+label);
            var hits=Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.75f,.3f,1<<6);
            Check(hits.Length==0,"body "+label+" "+string.Join(",",hits.Select(c=>c.name).Distinct()));
        }
        Check(b.id==RailAbandonedStation.Id,"block identity");
        for(float d=0;d<b.TrackLength;d+=.5f)
        {
            b.Sample(d,out var p,out var r);
            var hits=Physics.OverlapBox(p+Vector3.up+r*new Vector3(0,2.25f,2),new Vector3(2.7f,2.05f,7.1f),r,1<<6);
            Check(hits.Length==0,"train sweep "+d+" "+string.Join(",",hits.Select(c=>c.name).Distinct()));
        }
        foreach(float x in new[]{-7f,7f})for(float z=54;z<=106;z+=1)Walk(new(x-12,-3,z),"main "+x+","+z);
        for(float x=-24;x<=4;x+=.5f)Walk(new(x,-8,40),"cross passage "+x);
        for(float z=43;z<104;z++)Walk(new(4,-8,z),"lower "+z);
        for(float z=100;z<128;z++)Walk(new(4,-8,z),"departure service "+z);
        for(float z=29;z<41;z++)Walk(new(-6,-8,z),"pump corridor "+z);
        for(float z=64.5f;z<98;z+=.5f)Walk(new(9.5f,-8,z),"wreck "+z);
        foreach(float z in new[]{69.5f,81f,92.5f})for(float x=16;x<=23;x+=.5f)Walk(new(x-12,-8,z),"wreck door "+x+","+z);
        foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tread ")&&t.name!="Tread edge"))
        {var c=t.GetComponent<BoxCollider>();Walk(new(c.bounds.center.x,c.bounds.max.y,c.bounds.center.z),"stair "+t.position);}
        foreach(var v in b.variations)foreach(var t in v.coal.Concat(v.gold))Walk(t.position-Vector3.up*.5f,"loot "+t.name);
        Check(Physics.Linecast(new(0,1.5f,7),new(0,1.5f,153),1<<6),"Entrance-exit sightline blocked by solid geometry");
        var graph=CrawlGraph.Load(b.graph);var points=new[]{new Vector3(-7,-2.8f,70),new(7,-2.8f,70),new(16,-7.8f,70),new(21.5f,-7.8f,81)};
        var nodes=points.Select(q=>graph.FindNearestGroundNode(q+Vector3.left*12,2)).ToArray();Check(nodes.All(n=>n>=0),"Graph serves both levels and wreck");
        if(nodes.All(n=>n>=0))
        {
            var seen=new HashSet<int>{nodes[0]};var todo=new Queue<int>();todo.Enqueue(nodes[0]);
            while(todo.Count>0){int n=todo.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int k=graph.EdgeTarget[e];if(graph.GetNormal(k).y>.5f&&seen.Add(k))todo.Enqueue(k);}}
            Check(nodes.All(seen.Contains),"Ground graph connects platforms stairs and wreck");
        }
        Directory.CreateDirectory("docs/validation/underground-station");File.WriteAllLines("docs/validation/underground-station/metro-v2-checks.txt",lines);
        Debug.Log("METRO: "+lines.Count+" checks, "+lines.Count(s=>s.StartsWith("FAIL"))+" failures");
        if(lines.Any(s=>s.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(s=>s.StartsWith("FAIL")).Take(15)));
    }
}
#endif
