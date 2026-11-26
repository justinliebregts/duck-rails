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
 static void Contact(Vector3 from, string expected, string location)
 {
  bool hit=Physics.Raycast(from,Vector3.down,out var surface,2,1<<6,QueryTriggerInteraction.Ignore);
  var type=hit?Game.Audio.AudioSurface.TypeOf(surface.collider):null;
  Check(type && type.name==expected,location+" contact "+(type?type.name:"unassigned")+" (expected "+expected+")");
 }
 static void Walk(Vector3 p,string n){Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var hit,.4f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-p.y)<.08f,"Support "+n+" "+p);Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Body clearance "+n+" "+p);}
 public static void Run()
 {
  RailHospital.Guard();EditorSceneManager.OpenScene(RailHospital.BakeScene);RunOpen();
 }
 public static void RunOpen()
 {
  if(EditorSceneManager.GetActiveScene().path!=RailHospital.BakeScene || UnityEngine.SceneManagement.SceneManager.sceneCount!=1 || EditorApplication.isPlaying)throw new InvalidOperationException("Open only the Hospital bake scene in Edit Mode.");
  Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory("docs/validation/hospital/finished-campus");
  try{
   var b=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RailHospital.RootName);Check(b.id==RailHospital.Id&&b.length==160&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160),"Block identity and seams");
   for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Train clearance z="+z);
   foreach(float x in new[]{-47f,47f})foreach(float z in new[]{2f,158f})Walk(new(x,0,z),"Flat seams");
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
   for(float x=-13;x>=-26;x-=.5f){Walk(new(x,0,92.5f),"Clinic entry");Walk(new(x,0,66.5f),"Pharmacy entry");}
   for(float x=-12;x>=-26;x-=.5f)Walk(new(x,.17f*Mathf.InverseLerp(-16,-12,x),135.2f),"Graded laundry entry");
   foreach(float z in new[]{85.4f,92.5f,99.6f})for(float x=-29.6f;x>=-32.4f;x-=.4f)Walk(new(x,0,z),"Clinic exam doorway");
   for(float x=-27.6f;x>=-33.6f;x-=.5f)Walk(new(x,0,73.5f),"Dispensary pass-through");
   for(float z=95.5f;z<=104f;z+=.5f)Walk(new(25.5f,0,z),"North supplies exit");
   foreach(var q in new[]{new Vector3(18,4,73.5f),new(19.5f,4,66),new(29.5f,4,66),new(34,0,69.5f),new(26,0,70)})Walk(q,"Department door");
   Check(Physics.Raycast(new(26,20,80),Vector3.down,out var courtyard,25,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(courtyard.point.y)<.08f,"Courtyard open to sky");
   var interiors=root.Find(RailHospitalInteriors.RootName);Check(interiors&&interiors.Find("Rooms")&&interiors.Find("Rooms").childCount==13,"Thirteen furnished interior areas");Check(interiors&&interiors.GetComponentsInChildren<BoxCollider>().Length>=200,"Interior furniture has physical colliders");Check(interiors&&interiors.Find("Rooms/H1_Surgery")&&interiors.Find("Rooms/H0_ER")&&interiors.Find("Rooms/Pharmacy")&&interiors.Find("Rooms/Laundry")&&interiors.Find("Rooms/Clinic"),"Surgery, ER and street building interiors present");if(interiors)foreach(var label in interiors.GetComponentsInChildren<TMPro.TMP_Text>())Check(label.font.name=="CutePixel SDF","CutePixel "+label.text);foreach(var name in RailHospitalInteriors.StreetBuildings)Check(!root.Find(name).GetComponentsInChildren<Transform>().Any(t=>t.name=="Shelves"||t.name=="Counter"),"Blockout props retired in "+name);var campus=root.Find(RailHospitalCampus.RootName);
   Check(campus,"Finished campus installed");
   foreach(string group in new[]{"Staff garden","Garden pergola","Ambulance on side","Ambulance inverted","Ambulance at barrier","Emergency canopy","Roof plant","Laundry yard","Visitor shelter","Visitor shuttle"})
    Check(campus.GetComponentsInChildren<MeshCollider>().Any(c=>c.name.StartsWith(group+"_")),group+" has physical geometry");
   foreach(var label in campus.GetComponentsInChildren<TMPro.TMP_Text>())Check(label.font.name=="CutePixel SDF","CutePixel "+label.text);
   Check(campus.Find("HOSPITAL")&&campus.Find("CENTRAL LAUNDRY")&&campus.Find("VISITOR TRANSIT"),"Built campus signs");
   Check(campus.Cast<Transform>().Count(t=>t.name=="MEDICAL CENTER")==1 && campus.Find("MEDICAL CENTER").localPosition.x<0,"Only garden medical center sign remains");
   foreach(float z in new[]{57f,98f})foreach(float y in new[]{4f,8f})
    Contact(new Vector3(35.88f,y+1.3f,z+3),"Metal","Stairwell edge "+z+" level "+y);
   Contact(new(-44,1,28),"Grass","District verge");Contact(new(3,1,83),"Dirt","Rail bed");
   Contact(new(-13,1,90),"Stone","Clinic sidewalk");Contact(new(25,1,32),"Stone","Parking asphalt");
   Contact(new(-29,1,23),"Stone","Garden path");Contact(new(20,1,80),"Stone","Hospital floor");
   Contact(new(-14,1,135.2f),"Stone","Laundry ramp");Contact(new(23.8f,1,137.6f),"Stone","Shelter platform");
   Contact(new(38.5f,2.5f,61.5f),"Stone","Stair landing");Contact(new(12,1,112),"Dirt","Island soil");
   Contact(new(14.28f,1,112),"Stone","Island curb");Contact(new(-27,6.5f,132),"Metal","Laundry sheet roof");
   Check(b.GetComponentsInChildren<Light>().Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)==RailHospital.LightCount,"Campus baked fixture count");
   foreach(var span in new[]{new Vector2(-29,24.5f),new Vector2(26,45),new Vector2(-20,122),new Vector2(24,138.2f)})
    Check(!Physics.CheckCapsule(new(span.x,.98f,span.y),new(span.x,1.85f,span.y),.30f,1<<6,QueryTriggerInteraction.Ignore),"Campus pedestrian clearance "+span);
   var g=CrawlGraph.Load(b.graph);Check(g!=null&&g.NodeCount>100,"Prebaked graph");
   Check(b.spawnCandidatesMigrated,"Hospital uses the registered candidate pools");
   foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
   {
    var points=RailSpawnPool.Available(b,kind);
    int maximum=kind==RailSpawnKind.Enemy?4*RailEnemySpawnBalance.PerQuadrant(8):
        kind==RailSpawnKind.Coal?RailResourceBalance.CoalCount(8):RailResourceBalance.GoldCount(8);
    Check(points.Length==RailSpawnPool.Points(b.spawnCandidates,kind).Length && points.Length>=maximum,"Distinct active "+kind+" candidates cover eight players");
    var shape=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,Vector3.one*.8f):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
    foreach(var point in points)
    {
     Vector3 center=point.position+shape.center;
     bool supported=true;
     foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
     {
      var foot=center+Vector3.Scale(shape.extents,new Vector3(x,-1,z));
      supported &= Physics.Raycast(foot+Vector3.up*.035f,Vector3.down,out var hit,.26f,1<<6,QueryTriggerInteraction.Ignore) && hit.normal.y>=.8f;
     }
     Check(supported,"Spawn footprint support "+point.name);
     Check(!Physics.CheckBox(center,shape.extents,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Spawn envelope clearance "+point.name);
     if(kind==RailSpawnKind.Enemy)Check(RailLootPlacementWindow.EnemyLocation(b,point.position,g),"Enemy bounds and baked ground "+point.name);
    }
   }
   Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
   var nodes=new[]{new Vector3(20,.2f,80),new(20,4.2f,80),new(20,8.2f,80),new(25,.2f,62),new(19.5f,4.2f,64),new(29.5f,4.2f,64)}.Select(q=>g.FindNearestGroundNode(q,2)).ToArray();Check(nodes.All(n=>n>=0),"All levels and rooms navigable");
   if(nodes.All(n=>n>=0)){var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++){int next=g.EdgeTarget[e];if(seen.Add(next))queue.Enqueue(next);}}Check(nodes.All(seen.Contains),"Ground upper and roof routes connected");}
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>().Where(m=>!m.GetComponent<TMPro.TMP_Text>()))Check(mf.sharedMesh.uv.Length==mf.sharedMesh.vertexCount&&mf.sharedMesh.uv2.Length==mf.sharedMesh.vertexCount,"UV channels "+mf.name);
   var failures=lines.Where(l=>l.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(failures.Length+" failures:\n"+string.Join("\n",failures.Take(35)));Debug.Log("HOSPITAL CHECKS: "+lines.Count+" passed");
  }finally{File.WriteAllLines("docs/validation/hospital/finished-campus/geometry-checks.txt",lines);}
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
