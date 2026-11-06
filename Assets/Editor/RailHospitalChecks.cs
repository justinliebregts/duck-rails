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
public static class RailHospitalChecks
{
 static readonly List<string> lines=new();
 static void Check(bool ok,string n)=>lines.Add((ok?"PASS ":"FAIL ")+n);
 static void Walk(Vector3 p,string n){Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var hit,.4f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-p.y)<.08f,"Support "+n+" "+p);Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Body clearance "+n+" "+p);}
 public static void Run()
 {
  RailHospital.Guard();EditorSceneManager.OpenScene(RailHospital.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory("docs/validation/hospital");
  try{
   var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailHospital.RootName);Check(b.id==RailHospital.Id&&b.length==160&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160),"Block identity and seams");
   for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Train clearance z="+z);
   foreach(float x in new[]{-47f,47f})foreach(float z in new[]{2f,40f,80f,120f,158f})Walk(new(x,0,z),"Flat seams");
   foreach(float y in new[]{0f,4f,8f})
   {
    foreach(float x in new[]{20f,32f})for(float z=68;z<=92;z+=1)Walk(new(x,y,z),"Loop side");
    foreach(float z in new[]{68f,92f})for(float x=20;x<=32;x+=1)Walk(new(x,y,z),"Loop end");
    foreach(float z in new[]{56f,97f})for(float x=32;x<=40;x+=.5f)Walk(new(x,y,z),"Stair approach");
   }
   foreach(float y in new[]{0f,4f})foreach(float z in new[]{57f,98f})
   {
    for(int i=0;i<10;i++){Walk(new(37.2f,y+(i+1)*.2f,z+.18f+i*.36f),"Ascending stair");Walk(new(39.8f,y+2+(i+1)*.2f,z+3.42f-i*.36f),"Return stair");}
    foreach(float x in new[]{37.2f,38.5f,39.8f})Walk(new(x,y+2,z+4.6f),"Stair landing");
   }
   for(float z=46;z<=68;z+=.5f)Walk(new(25,0,z),"ER arrival");
   for(float x=6;x<=20;x+=.5f)Walk(new(x,0,80),"Main reception return");
   foreach(var q in new[]{new Vector3(18,4,73.5f),new(19.5f,4,66),new(29.5f,4,66),new(34,0,69.5f),new(26,0,70)})Walk(q,"Department door");
   Check(Physics.Raycast(new(26,20,80),Vector3.down,out var courtyard,25,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(courtyard.point.y)<.08f,"Courtyard open to sky");
   var all=root.GetComponentsInChildren<Transform>();Check(all.Count(t=>t.name=="Operating table")==3,"Three surgery rooms furnished");Check(all.Count(t=>t.name=="Water tank")==2&&all.Count(t=>t.name=="HVAC housing")==3,"Roof water tanks and HVAC");Check(all.Count(t=>t.name.StartsWith("Ambulance "))==3,"Three ambulance wrecks");Check(all.Any(t=>t.name=="Sign HOSPITAL"),"Large hospital sign");
   var g=CrawlGraph.Load(b.graph);Check(g!=null&&g.NodeCount>100,"Prebaked graph");
   foreach(var v in b.variations){Check(v.coal.Length==6&&v.enemies.Length==6&&v.gold.Length==2,"Spawn counts");foreach(var p in v.coal.Concat(v.enemies).Concat(v.gold)){Check(Physics.Raycast(p.position,Vector3.down,1.1f,1<<6,QueryTriggerInteraction.Ignore),"Spawn support "+p.name);Check(!Physics.CheckSphere(p.position+Vector3.up*.4f,.35f,1<<6,QueryTriggerInteraction.Ignore),"Spawn clear "+p.name);if(p.position.x>0)Check(g.FindNearestGroundNode(p.position,2)>=0,"Spawn graph "+p.name);}}
   Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
   var nodes=new[]{new Vector3(20,.2f,80),new(20,4.2f,80),new(20,8.2f,80),new(25,.2f,62),new(19.5f,4.2f,64),new(29.5f,4.2f,64)}.Select(q=>g.FindNearestGroundNode(q,2)).ToArray();Check(nodes.All(n=>n>=0),"All levels and rooms navigable");
   if(nodes.All(n=>n>=0)){var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++){int next=g.EdgeTarget[e];if(seen.Add(next))queue.Enqueue(next);}}Check(nodes.All(seen.Contains),"Ground upper and roof routes connected");}
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>().Where(m=>!m.GetComponent<TMPro.TMP_Text>()))Check(mf.sharedMesh.uv.Length==mf.sharedMesh.vertexCount&&mf.sharedMesh.uv2.Length==mf.sharedMesh.vertexCount,"UV channels "+mf.name);
   var failures=lines.Where(l=>l.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(failures.Length+" failures:\n"+string.Join("\n",failures.Take(35)));Debug.Log("HOSPITAL CHECKS: "+lines.Count+" passed");
  }finally{File.WriteAllLines("docs/validation/hospital/geometry-checks.txt",lines);}
 }
 public static void Capture()
 {
  RailHospital.Guard();EditorSceneManager.OpenScene(RailHospital.BakeScene);Directory.CreateDirectory("output/hospital");var go=new GameObject("Review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.14f,.18f,.2f);cam.farClipPlane=400;
  var sun=new GameObject("Review light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(48,30,0);
  try{foreach(var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))t.ForceMeshUpdate();RenderSettings.fog=false;RenderSettings.ambientLight=new(.65f,.65f,.65f);foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.lightmapIndex=-1;
   Shot("overview",new(-55,65,5),new(7,1,80));Shot("south-er",new(4,4,24),new(25,3.8f,57));Shot("roof",new(1,29,48),new(26,8,82));Shot("surgery",new(22,5.2f,64),new(16,5,60));Shot("er-interior",new(25,1.2f,63),new(15,1,58));
  }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailHospital.BakeScene);}
  void Shot(string n,Vector3 eye,Vector3 target){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=65;var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/hospital/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
 }
}
#endif
