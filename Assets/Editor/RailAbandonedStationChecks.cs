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
public static class RailAbandonedStationChecks
{
    const string Evidence="docs/validation/underground-station";
    static readonly List<string> lines=new();
    static void Check(bool value,string label){lines.Add((value?"PASS ":"FAIL ")+label);}
    static void Walk(Vector3 p,string label)
    {
        Check(Physics.Raycast(p+Vector3.up*.25f,Vector3.down,out var hit,.6f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-p.y)<.12f,"Floor "+label);
        var hits=Physics.OverlapCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore);
        Check(hits.Length==0,"Body "+label+(hits.Length>0?" blocked by "+string.Join(",",hits.Select(c=>c.name)):""));
    }
    static void Path(Vector3 a,Vector3 b,string name)
    {
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)*2);
        for(int i=0;i<=count;i++)Walk(Vector3.Lerp(a,b,i/(float)count),name+" "+i);
        Check(!Physics.SphereCast(a+Vector3.up*.8f,.3f,(b-a).normalized,out var hit,Vector3.Distance(a,b),1<<6,QueryTriggerInteraction.Ignore),"Continuous route "+name+(hit.collider?" blocked by "+hit.collider.name:""));
    }
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Validate geometry")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish play/bake first");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RailAbandonedStation.BakeScene)EditorSceneManager.OpenScene(RailAbandonedStation.BakeScene);
        Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);
        var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailAbandonedStation.RootName);
        Check(b.id==RailAbandonedStation.Id&&b.length==160&&root,"Original block identity and footprint retained");
        Check(b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160)&&b.stop.localPosition==new Vector3(0,-4,80),"Flat seams and lowered stop");
        b.Sample(0,out var ep,out var er);b.Sample(b.TrackLength,out var xp,out var xr);
        Check(ep==Vector3.zero&&Vector3.Distance(xp,new(0,0,160))<.001f&&Quaternion.Angle(er,Quaternion.identity)<.01f&&Quaternion.Angle(xr,Quaternion.identity)<.01f,"Exact entry/exit forward tangents");
        Check(b.TrackLength>160&&b.TrackLength<162,"Arc distance accounts for descent and ascent: "+b.TrackLength);
        float maxPitch=0;Quaternion previous=er;
        for(float d=0;d<=b.TrackLength;d+=.5f)
        {
            b.Sample(d,out var pos,out var rot);maxPitch=Mathf.Max(maxPitch,Vector3.Angle(rot*Vector3.forward,Vector3.forward));
            Check(Quaternion.Angle(previous,rot)<1,"Smooth track tangent d="+d);previous=rot;
            // Mesh envelope from the existing train: X +/-2.58, Y up to 4, Z -5..9.
            // Add margin to width/top, exclude wheel/rail contact at the bottom.
            Vector3 center=pos+Vector3.up+rot*new Vector3(0,2.25f,2);
            var hit=Physics.OverlapBox(center,new Vector3(2.7f,2.05f,7.1f),rot,1<<6,QueryTriggerInteraction.Ignore);
            Check(hit.Length==0,"Train upper/body envelope d="+d+(hit.Length>0?" hits "+string.Join(",",hit.Select(c=>c.name).Distinct()):""));
        }
        Check(maxPitch<9,"Maximum train pitch under nine degrees: "+maxPitch);
        foreach(float z in new[]{1f,5f,155f,159f})foreach(float x in new[]{-43f,-20f,0f,20f,43f})Walk(new(x,0,z),"Seam "+x+","+z);
        foreach(float x in new[]{-46f,46f})foreach(float z in new[]{12f,40f,80f,120f,150f})Walk(new(x,0,z),"Side seam "+x+","+z);
        foreach(float x in new[]{-24f,-16f,-8f})foreach(float z in new[]{143f,148f,152f})Walk(new(x,0,z),"Existing midpoint shop reserve "+x+","+z);
        Path(new(-7,-3,48),new(-7,-3,112),"West platform");Path(new(7,-3,48),new(7,-3,112),"Island platform");
        Path(new(-40,-3,53),new(-40,-3,110),"West staff corridor");Path(new(41.5f,-3,53),new(41.5f,-3,110),"East staff corridor");
        for(int i=0;i<4;i++)
        {
            float z=67+8*i;Path(new(-39,-3,z),new(-21,-3,z),"West room "+i);Path(new(28,-3,z),new(41,-3,z),"East room "+i);
        }
        Path(new(-32,-7,46.5f),new(32,-7,46.5f),"Staff underpass");
        foreach(var stair in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Public stairs"||t.name=="Stopped escalator"||t.name.EndsWith("staff stair")))
        {
            var treads=stair.Cast<Transform>().Where(t=>t.name.StartsWith("Tread ")&&t.name!="Tread edge").ToArray();
            foreach(var t in treads){var c=t.GetComponent<BoxCollider>();Walk(new(c.bounds.center.x,c.bounds.max.y,c.bounds.center.z),stair.name+" "+t.name);}
            Check(treads.Length>=24,"Full stair flight "+stair.name);
        }
        Path(new(-38,3.2f,101.5f),new(39,3.2f,101.5f),"Public cross concourse");
        Path(new(21.5f,-3,64.2f),new(21.5f,-3,98.2f),"Wreck end-to-end");
        foreach(float z in new[]{69.5f,81f,92.5f})Path(new(18,-3,z),new(26,-3,z),"Wreck side doors "+z);
        foreach(float z in new[]{28f,132f})foreach(float side in new[]{-1f,1f})
            Path(new(side*5,RailAbandonedStation.RailHeight(z)+1,z),new(side*14,RailAbandonedStation.RailHeight(z)+1,z),"Outer service alcove "+side+","+z);
        Check(root.GetComponentsInChildren<Light>().Length==RailAbandonedStation.LightCount&&root.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Expected baked-only fixtures");
        foreach(var v in b.variations)
        {
            Check(v.coal.Length==6&&v.gold.Length==2&&v.enemies.Length==6,"Retained spawn counts");
            foreach(var t in v.coal.Concat(v.gold))Walk(t.position-Vector3.up*.5f,"Loot "+t.name+" "+t.position);
        }
        foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Visual rail")||t.name=="Visual sleeper"||t.name.StartsWith("Disused")))Check(!t.GetComponent<Collider>(),"Visual-only railway "+t.name);
        var graph=CrawlGraph.Load(b.graph);Check(graph!=null&&graph.NodeCount>100,"Offline graph exists");
        var waypoints=new[]{new Vector3(-40,-2.8f,76),new(-7,-2.8f,70),new(7,-2.8f,70),new(41,-2.8f,76),new(0,-6.8f,46),new(0,3.4f,102),new(21.5f,-2.8f,81)};
        var nodes=new List<int>();foreach(var q in waypoints){int n=graph.FindNearestGroundNode(q,2);Check(n>=0,"Graph waypoint "+q);if(n>=0)nodes.Add(n);}
        if(nodes.Count>0)
        {
            var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);
            while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetNormal(next).y>.5f&&seen.Add(next))queue.Enqueue(next);}}
            Check(nodes.All(seen.Contains),"Ground-only paths connect halls, wreck, upper and lower crossings");
        }
        Check(Enumerable.Range(0,graph.NodeCount).All(n=>!b.enemySurfaceRailExclusion.Contains(graph.GetPosition(n))),"Navigation excludes operating railway");
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMPro.TextMeshPro>()==null))Check(f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Texture/lightmap UV "+f.name);
        File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);int failures=lines.Count(l=>l.StartsWith("FAIL"));Debug.Log("STATION CHECKS "+(lines.Count-failures)+" passed, "+failures+" failed. "+Evidence);
        if(failures>0)throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL")).Take(25)));
    }
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Capture review")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish first");
        EditorSceneManager.OpenScene(RailAbandonedStation.BakeScene);
        var go=new GameObject("Temporary station review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.08f,.1f,.12f);cam.farClipPlane=350;cam.nearClipPlane=.05f;
        bool fog=RenderSettings.fog;var hidden=new List<Renderer>();Directory.CreateDirectory("output/underground-station");
        try
        {
            RenderSettings.fog=false;
            Shot("arrival",new(0,0,43),new(0,0,85),85);
            Shot("west-platform",new(-6,-1.8f,58),new(-14,-.6f,96),85);
            Shot("concourse",new(-7,4.4f,102),new(25,4,105),85);
            Shot("wreck",new(27,-1.6f,61),new(21,-1.5f,86),80);
            Shot("wreck-interior",new(21.5f,-1.8f,66),new(21.5f,-1.8f,95),82);
            Shot("underpass",new(-30,-5.8f,46.5f),new(25,-5.8f,46.5f),75);
            Shot("mini-mart",new(-25,-1.7f,75),new(-34,-1.7f,76),85);
            Shot("descent",new(0,2,10),new(0,-2,43),75);
            var root=Object.FindFirstObjectByType<RailBlock>().transform.Find(RailAbandonedStation.RootName);
            foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.name.Contains("roof")||r.name.Contains("ceiling")||r.name=="Cross concourse deck"||r.name.Contains("Concourse")||r.transform.IsChildOf(root.Find("Upper shopping concourse"))){if(r.enabled){r.enabled=false;hidden.Add(r);}}
            Shot("cutaway",new(-38,71,36),new(0,-3,80),66);
        }
        finally{foreach(var r in hidden)r.enabled=true;RenderSettings.fog=fog;Object.DestroyImmediate(go);}
        void Shot(string name,Vector3 eye,Vector3 target,float fov)
        {
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;
            var rt=new RenderTexture(1600,1000,24);var prior=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/underground-station/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=prior;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif
