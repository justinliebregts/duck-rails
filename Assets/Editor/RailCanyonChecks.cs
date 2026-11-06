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

public static class RailCanyonChecks
{
    const string Evidence="docs/validation/canyon";
    static readonly List<string> lines=new();
    static void Check(bool pass,string message){lines.Add((pass?"PASS ":"FAIL ")+message);}
    static void Walk(Vector3 p,string label,bool clearance=true)
    {Check(Physics.Raycast(p+Vector3.up*.18f,Vector3.down,out var h,.5f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(h.point.y-p.y)<.12f,"Floor "+label);if(clearance)Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Clearance "+label);}
    [MenuItem("Duck Rails/Authoring/Canyon/Check physical layout and graph")]
    public static void Run()
    {
        RailCanyon.Guard();EditorSceneManager.OpenScene(RailCanyon.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);
        try
        {
            var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailCanyon.RootName);
            Check(b.id==RailCanyon.Id&&b.length==160&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160),"Canyon identity and seams");
            Check(b.track.Length==81&&b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(v=>v),"Straight level track");
            for(int z=0;z<160;z+=2)foreach(float x in new[]{-2f,0f,2f})Walk(new(x,0,z+.1f),"Rail deck "+x+"/"+z);
            foreach(float z in new[]{1f,5f,155f,159f})foreach(float x in new[]{-45f,-25f,0f,25f,45f})Walk(new(x,0,z),"Flat seam "+x+"/"+z);
            foreach(float x in new[]{-46f,46f})for(int z=1;z<160;z+=10)Walk(new(x,0,z),"Lateral boundary "+x+"/"+z);
            foreach(var route in new[]{RailCanyon.South,RailCanyon.North})
            for(int i=1;i<route.Length;i++)
            {
                var a=route[i-1];var d=route[i]-a;float run=new Vector2(d.x,d.z).magnitude;Check(Mathf.Abs(d.y)/run<=.12f,"Switchback grade "+a+" to "+route[i]);
                var side=Vector3.Cross(Vector3.up,d).normalized;
                for(int step=1;step<Mathf.CeilToInt(run);step+=2)foreach(float off in new[]{-1.5f,0f,1.5f}){var p=Vector3.Lerp(a,route[i],step/run)+side*off;Walk(p,"Switchback "+p);}
            }
            foreach(var p in RailCanyon.Landings)foreach(float dx in new[]{-2f,0f,2f})foreach(float dz in new[]{-1f,0f,1f})Walk(p+new Vector3(dx,0,dz),"Exploration plateau "+p+" sample "+dx+"/"+dz);
            for(int z=43;z<=117;z++)foreach(float x in new[]{23f,24f,25f})Walk(new(x,RailCanyon.FootbridgeY(z),z+.01f),"Suspension crossing "+x+"/"+z);
            for(int z=89;z<123;z++)Walk(new(-27,RailCanyon.CaveFloor(-27,z),z),"Shaft cave walk-out "+z);
            for(int z=85;z<109;z++)Walk(new(14,RailCanyon.CaveFloor(14,z),z),"East cave "+z);
            Check(Physics.Raycast(new Vector3(-27,1,119),Vector3.down,out var hit,22,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y+16)<.05f,"Shaft falls directly to cave floor without ground/ceiling obstruction");
            for(int z=90;z<120;z++){var a=new Vector3(-27,RailCanyon.CaveFloor(-27,z)+1,z);var d=new Vector3(-27,RailCanyon.CaveFloor(-27,z-1)+1,z-1)-a;Check(!Physics.SphereCast(a,.35f,d.normalized,out _,d.magnitude,1<<6,QueryTriggerInteraction.Ignore),"Cave continuous walk-out sweep "+z);}
            foreach(float x in new[]{-5.8f,5.8f})foreach(float z in new[]{73f,80f,90f,94f})Walk(new(x,-3,z),"Worker platform "+x+"/"+z);
            foreach(float x in new[]{-5.8f,5.8f})foreach(float z in new[]{76f,86f})Check(!Physics.Raycast(new Vector3(x,-2.8f,z),Vector3.down,.5f,1<<6,QueryTriggerInteraction.Ignore),"Broken worker gap "+x+"/"+z);
            Check(root.Find("Exploration landings").childCount==6,"Six authored exploration plateaus");
            foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name=="Track rail"||t.name=="Sleeper"))Check(!t.GetComponent<Collider>(),"Visual-only railway "+t.name);
            Check(b.GetComponentsInChildren<Light>().Length==RailCanyon.LightCount&&b.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Twelve baked fixtures");
            var g=CrawlGraph.Load(b.graph);Check(g!=null&&g.NodeCount>100,"Offline graph exists");
            foreach(var v in b.variations)
            {Check(v.coal.Length==6&&v.enemies.Length==6&&v.gold.Length==2,"Variation counts");foreach(var t in v.coal.Concat(v.enemies).Concat(v.gold)){Check(Physics.Raycast(t.position,Vector3.down,1.5f,1<<6,QueryTriggerInteraction.Ignore),"Spawn support "+t.name+" "+t.position);Check(!Physics.CheckSphere(t.position+Vector3.up*.5f,.35f,1<<6,QueryTriggerInteraction.Ignore),"Spawn clearance "+t.name+" "+t.position);}foreach(var t in v.enemies)Check(b.enemyBounds.Contains(t.position)&&g.FindNearestGroundNode(t.position,2)>=0,"Enemy landing navigable "+t.position);}
            var nodes=RailCanyon.Landings.Select(p=>g.FindNearestGroundNode(p+Vector3.up*.2f,2)).ToArray();
            Check(nodes.All(n=>n>=0),"Every plateau has a ground graph node");
            if(nodes.All(n=>n>=0))
            {var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++){int next=g.EdgeTarget[e];if(g.GetNormal(next).y>.5f&&seen.Add(next))queue.Enqueue(next);}}Check(nodes.All(seen.Contains),"Ground-only navigation connects all six plateaus via descents and canyon floor");}
            Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())Check(f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Texture/lightmap UVs "+f.name);
            var failures=lines.Where(s=>s.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(failures.Length+" Canyon failures:\n"+string.Join("\n",failures.Take(35)));
            Debug.Log("CANYON CHECKS: "+lines.Count+" passed");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    public static void Capture()
    {
        RailCanyon.Guard();EditorSceneManager.OpenScene(RailCanyon.BakeScene);Directory.CreateDirectory("output/canyon");
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.12f,.15f,.18f);cam.farClipPlane=500;
        var light=new GameObject("Temporary review sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(55,25,0);
        try
        {
            // Review-only illumination: never save these lightmap/ambient overrides to the bake scene.
            RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);RenderSettings.ambientIntensity=1.3f;
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))renderer.lightmapIndex=-1;
            Shot("overview",new(-65,85,5),new(0,-4,80),68);Shot("bridges",new(38,13,40),new(3,-4,81),75);Shot("plateaus",new(-39,0,80),new(15,-6,55),80);Shot("shaft",new(-27,10,115),new(-27,-14,119),75);
            var fill=go.AddComponent<Light>();fill.type=LightType.Point;fill.intensity=150;fill.range=35;fill.shadows=LightShadows.None;Shot("cave-exit",new(-27,-14.6f,116),new(-27,-10.6f,90),75);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(light.gameObject);EditorSceneManager.OpenScene(RailCanyon.BakeScene);}
        void Shot(string name,Vector3 eye,Vector3 target,float fov){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("output/canyon/"+name+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
    }
}
#endif
