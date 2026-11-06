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

public static class RailSkyscraperChecks
{
 static readonly List<string> lines=new();
 static void Check(bool ok,string name)=>lines.Add((ok?"PASS ":"FAIL ")+name);
 static void Walk(Vector3 p,string name)
 {Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var hit,.35f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-p.y)<.08f,"Support "+name);Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Body clearance "+name);}
 public static void Run()
 {
  RailSkyscraper.Guard();EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory("docs/validation/skyscraper");
  try{
   var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailSkyscraper.RootName);
   Check(b.id==RailSkyscraper.Id&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160)&&b.length==160,"Identity and flat seams");
   Check(b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(x=>x),"Straight track unchanged");
   for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Train envelope 5.8m wide / 6.3m high z="+z);
   foreach(float x in new[]{-46f,46f})foreach(float z in new[]{2f,40f,80f,120f,158f})Walk(new(x,0,z),"Flat side seam");
   foreach(var t in RailSkyscraper.Towers)
   {
    foreach(float x in new[]{10f,14f}){Check(Physics.Raycast(t+new Vector3(x,21,19),Vector3.down,out var h,22,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(h.point.y)<.05f,"Hollow shaft to ground "+t+" "+x);for(float z=14;z<=20;z+=.5f)Walk(t+new Vector3(x,0,z),"Shaft ground walk-out "+t+" "+x+"/"+z);}
    for(int f=0;f<5;f++)
    {
     float y=f*4;Walk(t+new Vector3(14,y,12),"Open floor centre "+t+"/"+f);Walk(t+new Vector3(17,y,23),"Behind shaft circulation "+t+"/"+f);
     if(f<4)foreach(var h in RailSkyscraper.Holes.Take(2))
     {
      for(int i=0;i<10;i++){Walk(t+new Vector3(h.x+1.2f,y+(i+1)*.2f,h.y+1.18f+i*.36f),"Ascending stair "+t+"/"+f+"/"+h+"/"+i);Walk(t+new Vector3(h.x+3.8f,y+2+(i+1)*.2f,h.y+4.42f-i*.36f),"Returning stair "+t+"/"+f+"/"+h+"/"+i);}
      foreach(float xx in new[]{1.2f,2.5f,3.8f})Walk(t+new Vector3(h.x+xx,y+2,h.y+5.6f),"Turn landing "+t+"/"+f);
      foreach(float xx in new[]{1.2f,2.5f,3.8f})Walk(t+new Vector3(h.x+xx,y+4,h.y+.5f),"Floor landing "+t+"/"+f);
     }
    }
   }
   foreach(var deck in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Continuous walking deck"))
   {
    var parent=deck.parent;float length=deck.localScale.z;float width=deck.localScale.x;
    for(float z=-.6f;z<=length+.6f;z+=.5f)foreach(float x in new[]{-width*.25f,0,width*.25f})Walk(parent.TransformPoint(new Vector3(x,0,z)),"Bridge/crane "+parent.name+" "+x+"/"+z);
   }
   var g=CrawlGraph.Load(b.graph);Check(g!=null&&g.NodeCount>100,"Prebaked graph");
   foreach(var v in b.variations){Check(v.coal.Length==6&&v.enemies.Length==6&&v.gold.Length==2,"Spawn counts");foreach(var p in v.coal.Concat(v.enemies).Concat(v.gold)){Check(Physics.Raycast(p.position,Vector3.down,1.1f,1<<6,QueryTriggerInteraction.Ignore),"Spawn supported "+p.name);Check(!Physics.CheckSphere(p.position+Vector3.up*.3f,.35f,1<<6,QueryTriggerInteraction.Ignore),"Spawn clear "+p.name);Check(g.FindNearestGroundNode(p.position,2)>=0,"Spawn navigation "+p.name);}}
   Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
   var nodes=RailSkyscraper.Towers.SelectMany(t=>Enumerable.Range(0,5).Select(f=>g.FindNearestGroundNode(t+new Vector3(14,f*4+.2f,12),2))).ToArray();Check(nodes.All(n=>n>=0),"All 20 levels navigable");
   if(nodes.All(n=>n>=0)){var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++){int next=g.EdgeTarget[e];if(seen.Add(next))queue.Enqueue(next);}}Check(nodes.All(seen.Contains),"All 20 levels connected in baked graph");}
   Check(root.GetComponentsInChildren<Light>().Length==RailSkyscraper.LightCount,"24 baked worklights");
   foreach(var m in root.GetComponentsInChildren<MeshFilter>())Check(m.sharedMesh.uv.Length==m.sharedMesh.vertexCount&&m.sharedMesh.uv2.Length==m.sharedMesh.vertexCount,"UV channels "+m.name);
   var failures=lines.Where(s=>s.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(failures.Length+" failures:\n"+string.Join("\n",failures.Take(35)));Debug.Log("SKYSCRAPER CHECKS: "+lines.Count+" passed");
  }finally{File.WriteAllLines("docs/validation/skyscraper/geometry-checks.txt",lines);}
 }
 public static void Capture()
 {
  RailSkyscraper.Guard();EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);Directory.CreateDirectory("output/skyscraper");var go=new GameObject("Review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.16f,.19f,.22f);cam.farClipPlane=500;var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.transform.rotation=Quaternion.Euler(48,30,0);
  try{RenderSettings.fog=false;RenderSettings.ambientLight=new(.65f,.65f,.65f);RenderSettings.ambientIntensity=1.3f;foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.lightmapIndex=-1;Shot("overview",new(-65,55,15),new(0,10,83));Shot("rail-corridor",new(0,2,42),new(0,13,95));Shot("open-floor",new(-34,9.3f,65),new(-15,9,70));Shot("crane",new(-23,20,61),new(19,16,87));}
  finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);}
  void Shot(string n,Vector3 eye,Vector3 target){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=65;var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/skyscraper/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
 }
}
#endif
