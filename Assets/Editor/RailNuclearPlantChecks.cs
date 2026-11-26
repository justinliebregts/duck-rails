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
public static class RailNuclearPlantChecks
{
 public const string Output="docs/validation/nuclear-full-block";
 static readonly List<string> lines=new();
 static void Check(bool ok,string n)=>lines.Add((ok?"PASS ":"FAIL ")+n);
 static void Walk(Vector3 p,string n)
 {Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var h,.4f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(h.point.y-p.y)<.08f,"Support "+n+" "+p);Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Body clearance "+n+" "+p);}
 [Serializable] class RampManifest {public Vector3[] rampSamples,pipeJoints;}
 public static void Run()
 {
  RailNuclearPlant.Guard();EditorSceneManager.OpenScene(RailNuclearPlant.BakeScene);RunOpen();
 }
 [MenuItem("Duck Rails/Authoring/Nuclear Plant/Check open full site")]
 public static void RunOpen()
 {
  if(UnityEngine.SceneManagement.SceneManager.sceneCount!=1 || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RailNuclearPlant.BakeScene)throw new InvalidOperationException("Open the nuclear bake scene alone for physics checks.");
  Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Output);
  try{
   var b=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RailNuclearPlant.RootName);Check(b.id==RailNuclearPlant.Id&&b.length==160&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160),"Identity and flat track anchors");
   for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Rail clearance "+z);
   foreach(float z in new[]{2f,158f})for(int x=-46;x<=46;x+=2)Walk(new(x,0,z),"Flat seam");
   const float cz=103;
   foreach(float cx in new[]{-26f,26f}){
    for(float x=cx-15;x<=cx+15;x+=.5f)Walk(new(x,.025f,cz),"Tower east west access");
    for(float z=cz-9;z<=cz+9;z+=.5f)Walk(new(cx,.025f,z),"Tower centre crosswalk");

    Check(!Physics.Raycast(new(cx,.2f,cz),Vector3.up,45,1<<6,QueryTriggerInteraction.Ignore),"Open view up hollow tower "+cz);
    Check(Physics.Raycast(new(cx,12,cz),Vector3.right,20,1<<6,QueryTriggerInteraction.Ignore),"Inner shell collision "+cz);
   }
   for(float x=-45;x<=-7;x+=.5f)Walk(new(x,.025f,73),"Cross hall and exterior return");
   foreach(float x in new[]{-16f,-30f})for(float z=59;z<=88;z+=1)Walk(new(x,0,z),"Hall ground loop");
   foreach(float z in new[]{59f,88f})for(float x=-30;x<=-16;x+=1)Walk(new(x,0,z),"Hall loop end");
   for(float z=56;z<=90;z+=.5f)Walk(new(-37,3,z),"Gallery aisle");
   for(int f=0;f<2;f++)for(int t=0;t<15;t++)Walk(RailNuclearFullBlock.AcrossTrack(RailNuclearPlant.StairPoint(f,t)),"Stair "+f+" tread "+t);
   foreach(float z in new[]{56f,90f})for(float x=-37;x<=-30;x+=.5f)Walk(new(x,0,z),"Stair foot approach");
   foreach(float z in new[]{67f,79f})for(float x=-37;x<=-33.5f;x+=.5f)Walk(new(x,3,z),"Stair upper landing");
   for(float z=18;z<=33;z+=.5f)Walk(new(34,0,z),"Waste yard clear return lane");
   foreach(float z in new[]{21.5f,26.5f})for(float x=25;x<=34;x+=.5f)Walk(new(x,0,z),"Barrel cluster aisles");
   var all=root.GetComponentsInChildren<Transform>();Check(all.Count(t=>t.name.StartsWith("Waste drum tier "))==59&&all.Count(t=>t.name=="Spilled waste drum")==6,"65 total waste drums, six tipped");
   Check(all.Count(t=>t.name=="Waste drum tier 2")==3,"Three irregular third-tier stacks");
   var drums=all.Where(t=>t.name=="Ribbed barrel").Select(t=>t.GetComponent<MeshFilter>()).ToArray();
   Check(drums.Length==65 && drums.Select(f=>f.sharedMesh).Distinct().Count()==1 && drums[0].sharedMesh.triangles.Length/3==60,"65 shared 60-triangle drums");
   Check(drums.All(f=>f.GetComponent<MeshRenderer>().sharedMaterial.name=="Waste drum paint"),"Textured barrel markings");
   Check(all.Where(t=>t.name=="Flat ground").All(t=>new[]{"Concrete","Aggregate"}.Contains(t.GetComponent<MeshRenderer>().sharedMaterial.name)),"Concrete and aggregate ground");
   Check(all.Any(t=>t.name=="QUACKWATT" && t.position.z<10),"South-facing station sign");
   foreach(float z in new[]{48f,81f,95f})Check(Physics.Raycast(new Vector3(0,7,z),Vector3.up,4,1<<6),"Overhead crossing "+z);
   Check(all.Count(t=>t.name.StartsWith("Waste spill ") && t.GetComponent<MeshRenderer>() && t.GetComponent<MeshRenderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"))==4,"Emissive spill surface, raised edges and highlights");
   foreach(var t in all.Where(t=>t.name.StartsWith("Waste drum tier "))){var ray=t.position+Vector3.up*.03f;Check(Physics.Raycast(ray,Vector3.down,out var hit,.15f,1<<6,QueryTriggerInteraction.Ignore),"Barrel supported "+t.position);}
   Check(root.GetComponentsInChildren<Light>().Count(l=>l.name=="Shell uplight" && l.type==LightType.Spot)==12,"Six baked uplights around each shell");Check(root.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Environment fixtures bake-only");
   Check(root.GetComponentsInChildren<Rigidbody>().Length==0,"Scenery does not add loose networked physics");
   var graph=CrawlGraph.Load(b.graph);Check(graph!=null&&graph.NodeCount>100,"Prebaked graph present");
   var markers=b.GetComponentsInChildren<RailSpawnMarker>(true);
   CheckSpawnPools(b,graph,markers);
   foreach(var marker in markers){
    var t=marker.transform;
    Check(Physics.Raycast(t.position,Vector3.down,1.1f,1<<6,QueryTriggerInteraction.Ignore),"Spawn support "+t.name);
    Check(!Physics.CheckSphere(t.position+Vector3.up*.4f,.35f,1<<6,QueryTriggerInteraction.Ignore),"Spawn clearance "+t.name);
    Check(graph.FindNearestGroundNode(t.position,2)>=0,"Spawn graph "+t.name);
   }
   var site=root.Find(RailNuclearFullBlock.RootName);Check(site,"Finished site present");
   foreach(string zone in new[]{"Shift bus turnaround","Pipe delivery racks","Tower entrance worklights","Supply trunk and collars","Reed settling pond","Stepped discharge flume","Transformer fins"})
    Check(site.GetComponentsInChildren<MeshCollider>().Any(c=>c.name.StartsWith(zone+" ")),zone+" has physical support");
   foreach(var t in site.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font.name=="CutePixel SDF","Site font "+t.text);
   Walk(new(-23,.12f,15),"Shift arrival");Walk(new(23,.04f,9),"Between delivery racks");Walk(new(-24,1.33f,131),"Settling pond floor");Walk(new(23,.23f,142.5f),"First flume step");
   var manifest=JsonUtility.FromJson<RampManifest>(File.ReadAllText("ArtSource/NuclearFullBlock/site.json"));
   var samples=manifest.rampSamples;
   Check(manifest.pipeJoints.Length>=60,"Authored bend and branch fittings");
   foreach(var q in manifest.pipeJoints)Check(Physics.OverlapSphere(q,.64f,1<<6).Any(c=>c.GetComponent<MeshFilter>() && c.transform.IsChildOf(site)),"Installed pipe fitting "+q);
   Check(!all.Any(t=>t.name.StartsWith("Cooling tower manifolds")),"Floating tower manifolds removed");
   Check(!all.Where(t=>t.name.StartsWith("Supply trunk and collars")).Any(t=>t.GetComponent<MeshRenderer>().bounds.max.z>90.02f),"Supply run ends before tower access");
   Check(drums.Where(f=>f.transform.parent.name!="Spilled waste drum").Select(f=>Mathf.RoundToInt(f.transform.parent.localEulerAngles.y)).Distinct().Count()>40,"Independent barrel orientations");
   var tyres=all.Where(t=>t.name=="Tyre").ToArray();
   Check(tyres.Length==4 && tyres.All(t=>t.GetComponent<MeshCollider>() && t.GetComponent<MeshRenderer>().sharedMaterial.name=="Rubber"),"Four bevelled rubber tyres with matching collision");
   Check(root.GetComponentsInChildren<Light>().Count(l=>l.name=="Transformer floodlight" && l.type==LightType.Spot && l.transform.forward.y<0)==3,"Three aimed transformer floodlights");
   Check(!all.Any(t=>(t.name=="Baked plant light" || t.name=="Light fixture") && Mathf.Abs(t.position.x-27)<.01f && Mathf.Abs(t.position.z-130)<.01f),"Floating transformer fixture removed");
   var paving=site.Find("Continuous site paving").GetComponent<MeshFilter>().sharedMesh;
   var pv=paving.vertices;var puv=paving.uv2;
   Check(pv.Length>4 && pv.Select((v,i)=>Vector2.Distance(puv[i],new Vector2((v.x+48)/160,v.z/160))).Max()<.00001f,"Paving has continuous undistorted lightmap coordinates");
   var pavement=site.Find("Continuous site paving").GetComponent<MeshRenderer>();
   int ballastSlot=Array.FindIndex(pavement.sharedMaterials,m=>m.name=="Aggregate");
   var av=paving.vertices;var at=paving.GetTriangles(ballastSlot);
   Check(paving.subMeshCount==3 && pavement.sharedMaterials.Length==3,"Paving material draw ranges");
   for(int z=1;z<160;z+=3)Check(Enumerable.Range(0,at.Length/3).Any(i=>{
    var a=av[at[i*3]];var b=av[at[i*3+1]];var c=av[at[i*3+2]];
    return Mathf.Min(a.x,b.x,c.x)<=.1f && Mathf.Max(a.x,b.x,c.x)>=.1f && Mathf.Min(a.z,b.z,c.z)<=z && Mathf.Max(a.z,b.z,c.z)>=z;
   }),"Continuous gravel under track "+z);
   Check(all.Where(t=>t.name=="Long walking path" || t.name=="Flat ground").All(t=>!t.GetComponent<MeshRenderer>().enabled),"Original overlapping paving renderers disabled");
   foreach (float x in new[]{-7.55f,7.55f}) foreach(float z in new[]{9f,22f,34.4f,41.6f,53f,62.8f,71.2f,84f,98.8f,107.2f,119.8f,128.2f,140f,151f})
    Check(Physics.Raycast(new(x,1.7f,z),Vector3.down,out var post,1,1<<6) && post.collider.name.StartsWith("Striped verge bollards"),"Off-path striped bollard "+x+","+z);
   var hall=root.Find("Turbine hall");
   Check(!hall.Cast<Transform>().Any(t=>t.name=="Light fixture" || t.name=="Baked plant light" || t.name=="Sign CONTROL"),"Legacy floating gallery fixtures and sign removed");
   var floods=site.GetComponentsInChildren<Light>().Where(l=>l.name=="Hall wall flood").ToArray();
   Check(floods.Length==2,"Two working control-room perimeter floodlights");
   var fixtureMeshes=site.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Hall wall floodlights ")).ToArray();
   foreach(var (lens,count) in new[]{("Lamp lens",2),("Burnt lamp glass",8)}) {
    var mesh=fixtureMeshes.Single(f=>f.GetComponent<Renderer>().sharedMaterial.name==lens);
    var material=mesh.GetComponent<Renderer>().sharedMaterial;
    Check(mesh.sharedMesh.triangles.Length==count*36,"Hall floodlight lenses: "+count+" "+lens);
    if(lens=="Burnt lamp glass")
     Check(!material.IsKeywordEnabled("_EMISSION") && material.GetColor("_BaseColor").maxColorComponent<.10f,"Broken lenses are black and non-emissive");
   }
   foreach(var normal in new[]{Vector3.right,Vector3.left}) {
    var bank=floods.Where(l=>Vector3.Dot(l.transform.forward,normal)>.65f).ToArray();
    Check(bank.Length==1,"Working floodlight on wall "+normal);
    foreach(var l in bank) {
     var p=l.transform.position;
     // Check the wall behind the bracket above the fixture, clear of its own mesh.
     Check(l.type==LightType.Spot && l.transform.forward.y<-.5f &&
       Physics.Raycast(p+Vector3.up*.65f,-normal,out var mount,1.05f,1<<6) &&
       mount.collider.transform.IsChildOf(hall),"Mounted hall spotlight "+p);
    }
   }
   var facadeLights=site.GetComponentsInChildren<Light>().Where(l=>l.name=="Hall facade spotlight").ToArray();
   Check(facadeLights.Length==3 && facadeLights.All(l=>l.type==LightType.Spot && Mathf.Approximately(l.intensity,140) &&
      l.transform.forward.y<-.96f && l.transform.forward.x<-.1f),"Three dimmed downward facade spots");
   CheckFeedback(b,root,site,graph);
   var tripods=site.GetComponentsInChildren<Light>().Where(l=>l.name=="Worklight").ToArray();
   Check(tripods.Length==8,"Four existing work lights retained per tower");
   foreach(var l in tripods) {
    var foot=l.transform.position+new Vector3(0,-2.4f,.45f);
    var center=new Vector2(foot.x<0?-26:26,103);
    Check(Vector2.Distance(center,new Vector2(foot.x,foot.z))>12.7f &&
      Mathf.Abs(foot.z-103)>9 && !Physics.OverlapBox(foot+Vector3.up*1.29f,new Vector3(.82f,1.30f,.82f),Quaternion.identity,1<<6)
      .Any(c=>c.name.Contains("tower ramp")),"Entire tripod clears tower ramp and entrance "+foot);
    foreach(var offset in new[]{new Vector3(-.7f,0,-.4f),new Vector3(.65f,0,-.4f),new Vector3(0,0,.7f)})
     Check(Physics.RaycastAll(foot+offset+Vector3.up*.15f,Vector3.down,.25f,1<<6).Any(hit=>!hit.collider.name.StartsWith("Tower entrance worklights") && Mathf.Abs(hit.point.y)<.08f),"Tripod foot at paving "+(foot+offset));
   }
   var nameboard=site.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.text=="TURBINE HALL");
   Check(nameboard.transform.position.x>-12 && Vector3.Dot(-nameboard.transform.forward,Vector3.right)>.99f,"Nameboard faces train");
   Check(!all.Any(t=>t.name=="Stair handrail") && all.Any(t=>t.name=="Gallery edge rail"),"Both lower flights clear, gallery edge guarded");
   foreach(float z in new[]{74.4f,76f,80f,84.1f})
    Check(Physics.Raycast(new Vector3(-38.5f,8.5f,z),Vector3.up,out var roof,4,1<<6) && roof.collider.name.StartsWith("Roof access headhouse"),"Weather roof over stair "+z);
   for(float z=72.1f;z<=74.6f;z+=.3f)Walk(new(-38.5f,8.1f,z),"Headhouse exit");
   foreach(var f in site.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Compound chain-link panel"))
    Check(f.sharedMesh.vertexCount==4 && f.sharedMesh.triangles.Length==6 && f.GetComponent<MeshRenderer>().sharedMaterial.name=="Subway mesh fence","Compound textured quad "+f.transform.position);
   Check(site.GetComponentsInChildren<MeshFilter>().Count(f=>f.name=="Compound chain-link panel")==6,"Six chain-link spans");
   foreach(float x in new[]{12.52f,13.6f,14.8f})for(float z=131.5f;z<153.35f;z+=.75f)
    Walk(new(x,Mathf.Lerp(.04f,4.12f,(z-131)/22.35f),z),"East flume ramp");
   for(float x=12.65f;x<=38.1f;x+=.7f)foreach(float z in new[]{154.1f,155.5f,156.7f})Walk(new(x,4.12f,z),"Flume deck");
   for(float z=116.6f;z<130;z+=.8f)Walk(new(-10.65f,Mathf.Lerp(.04f,2.66f,(z-116)/14),z),"West pond ramp");
   foreach(float x in new[]{15.5f,38.5f})for(int i=0;i<4;i++)
    foreach(float offset in new[]{.02f,1.5f,3.08f})Check(Physics.Raycast(new(x,6,140.95f+i*3.1f+offset),Vector3.down,out var wall,6,1<<6) && Mathf.Abs(wall.point.y-(1.87f+i*.9f))<.03f,"Continuous flume sidewall");
   foreach(var t in all.Where(t=>t.name=="Yard lamp post"))Check(OffPath(t.position.x,t.position.z),"Yard light off path "+t.position);
   foreach(var l in site.GetComponentsInChildren<Light>().Where(l=>l.name=="Street pool")) {
    var foot=l.transform.position;foot.x+=Mathf.Sign(foot.x)*1.35f;
    Check(OffPath(foot.x,foot.z),"Streetlamp footing off path "+foot);
   }

   foreach(float x in new[]{-34f,-33f,-32f,-31f,-30f,-29f})
    Check(Physics.Raycast(new Vector3(x,2,139.55f),Vector3.forward,out var wallHit,.8f,1<<6) && wallHit.collider.name.StartsWith("Pump house frontage"),"Solid pump hut pond wall "+x);
   Check(site.GetComponentsInChildren<MeshFilter>().Any(f=>f.name.StartsWith("Pond pump skid ")),"Installed pump and motor");
   Check(!all.Any(t=>t.name=="Shallow basin water" || t.name=="Sign EXIT" || t.name.StartsWith("Sign COOLING ")),"Dry tower floors and removed suspended tower signs");
   var pondLights=site.GetComponentsInChildren<Light>().Where(l=>l.name=="Pond corner flood").ToArray();
   Check(pondLights.Length==4 && pondLights.All(l=>l.type==LightType.Spot && Vector3.Dot(l.transform.forward,(new Vector3(-26,2.33f,130)-l.transform.position).normalized)>.90f),"Four inward-facing pond floodlights");
   Check(!all.Any(t=>(t.name=="Light fixture" || t.name=="Baked plant light") && Mathf.Abs(t.position.x+26)<.01f && Mathf.Abs(t.position.z-130)<.01f),"Removed central pond light");
   Check(all.Count(t=>t.name=="Turbine unit")==4,"Four turbine units");
   foreach(float z in new[]{60f,67,73,82,88})Walk(new(-23,0,z),"Between turbine rows");
   foreach(var post in all.Where(t=>t.name=="Gallery baluster")) {
    var bnd=post.GetComponent<Renderer>().bounds;
    foreach(float x in new[]{bnd.min.x,bnd.max.x})foreach(float z in new[]{bnd.min.z,bnd.max.z})
     Check(Physics.Raycast(new Vector3(x,2.99f+.06f,z),Vector3.down,out var deck,.12f,1<<6) && deck.collider.name=="Gallery deck","Gallery post fully on deck "+x+","+z);
   }
   var truck=all.Single(t=>t.name=="Truck with spilled load");
   Check(Mathf.Abs(truck.Find("Flatbed").GetComponent<Renderer>().bounds.min.y-truck.Find("Chassis").GetComponent<Renderer>().bounds.max.y)<.001f,"Truck bed seated on chassis");
   Check(Mathf.Abs(all.Single(t=>t.name=="Roof warning beacon").GetComponent<Renderer>().bounds.min.y-16.1f)<.01f,"Beacon seated on dome cap");
   foreach(var side in new[]{"West","East"}) {
    int tris=site.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith(side+" tower ramp ")).Sum(f=>f.sharedMesh.triangles.Length/3);
    Check(tris>0 && tris<10000,side+" ramp, deck and rails "+tris+" triangles (previously 15428)");
   }
   var painted=site.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Shift bus turnaround Worn line"));
   var paintVertices=painted.sharedMesh.vertices.Select(v=>painted.transform.TransformPoint(v)).ToArray();
   Check(paintVertices.All(v=>v.x>=-37.5f && v.x<=-8.5f && v.z>=10.5f && v.z<=29.5f && v.y>=.119f && v.y<=.127f),"Southwest stripes on asphalt");
   var facade=site.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Hall facade and roof plant "));
   Check(!facade.Any(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).Any(v=>v.x < -40.6f && v.y>3 && v.y<3.7f && v.z>64f)),"Removed three unsupported west pipes");
   foreach (var f in site.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains("tower ramp Galvanized steel"))) {
    float cx=f.name.StartsWith("West")?-26f:26f;
    var ringHeights=new[]{3.2f,8f,16f,24f,30f,35f};
    var radii=new[]{11f,9.8f,7.6f,6.8f,7.3f,8.5f};
    foreach (int ring in Enumerable.Range(0,5)) {
     var vertices=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).Where(v=>v.y>=ringHeights[ring] && v.y<ringHeights[ring+1]).ToArray();
     Check(vertices.Length>0 && vertices.All(v=>{
      float r=Mathf.Lerp(radii[ring],radii[ring+1],Mathf.InverseLerp(ringHeights[ring],ringHeights[ring+1],v.y));
      return new Vector2(v.x-cx,v.z-103).magnitude<r*Mathf.Cos(Mathf.PI/32)-.03f;
     }),"Beam ends inside outer wall "+cx+" ring "+ring);
    }
   }
   Check(samples.Length==320,"Four turns sampled on each tower");
   foreach(var q in samples)Walk(q,"Tower ramp");
   foreach(float cx in new[]{-26f,26f}) {
    float sign=cx<0?1:-1;
    Walk(new(cx+sign*7.7f,35.5f,103+sign*1.5f),"Lookout");
    var last=samples[cx<0?159:319];Connected(graph,new[]{new Vector3(cx,.2f,103), last+Vector3.up*.2f},"Tower foot to lookout "+cx);
   }
   for(int k=0;k<30;k++)Walk(new(-38.5f,3+(k+1)*.17f,89.2f-(k+.5f)*.46f),"Roof stair");
   Walk(new(-38.5f,8.1f,74.5f),"Roof landing");
   Connected(graph,new[]{new Vector3(-37,3.2f,86),new(-38.5f,8.3f,74)},"Gallery to roof");
   for(float x=17.8f;x<=22.4f;x+=.4f)Walk(new(x,.02f,72),"Reactor doorway");
   foreach(float x in new[]{25.55f,34.4f})for(float z=67;z<=77;z+=.5f)Walk(new(x,1.164f,z),"Refueling pool loop");
   Walk(new(30,0,72),"Recoverable pool floor");Walk(new(37.10f,1.62f,75.8f),"Crane control platform");
   foreach(var w in new[]{new Vector3(-34.9f,2,34.5f),new(29.9f,2,36.5f),new(20.5f,2.45f,51.5f)})
    Check(!Physics.Raycast(w,Vector3.forward,1.1f,1<<6),"Open-air window "+w);
   foreach(float x in new[]{-47f,47f})foreach(float z in new[]{4f,30f,65f,90f,119f,150f})
    Check(Physics.Raycast(new(x-Mathf.Sign(x)*2,1.4f,z),Vector3.right*Mathf.Sign(x),2.6f,1<<6),"Boundary collision "+x+","+z);
   Check(!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Tree trunk"||t.name=="Tree crown"||t.name.StartsWith("Pine verge")||t.name.StartsWith("Perimeter planting")),"Trees removed");
   Check(site.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("02:10")) && site.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="SPILL KIT"),"Receiving and repair props");
   Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
   Connected(graph,new[]{new Vector3(-26,.2f,103),new(26,.2f,103),new(-36,.2f,100)},"Both cooling towers connected");
   Connected(graph,new[]{new Vector3(-16,.2f,73),new(-30,.2f,86),new(-37,3.2f,73)},"Ground and upper gallery connected");
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>().Where(m=>!m.GetComponent<TMPro.TMP_Text>()))Check(mf.sharedMesh.uv.Length==mf.sharedMesh.vertexCount&&mf.sharedMesh.uv2.Length==mf.sharedMesh.vertexCount,"UV channels "+mf.name);
   var failed=lines.Where(l=>l.StartsWith("FAIL")).ToArray();if(failed.Length>0)throw new Exception(failed.Length+" failures:\n"+string.Join("\n",failed.Take(40)));Debug.Log("NUCLEAR PLANT CHECKS: "+lines.Count+" passed");
  }finally{File.WriteAllLines(Output+"/geometry-checks.txt",lines);}
 }
 // November 26 feedback: water depth, walkway, dimmed buildings and the emptied east rack.
 static void CheckFeedback(RailBlock block,Transform root,Transform site,CrawlGraph graph)
 {
  var volumes=site.GetComponentsInChildren<Game.Audio.WaterVolume>();
  Check(volumes.Length==RailNuclearFullBlock.Waters.Length,"Six water volumes");
  foreach(var water in RailNuclearFullBlock.Waters) {
   var v=volumes.Single(q=>q.name=="Water volume "+water.name);
   Check(Mathf.Abs(v.SurfaceY-water.surface.y)<.001f && v.waterSurface && v.enterSound && v.exitSound && v.bubblesSound,"Water surface and sounds "+water.name);
   Check(v.Contains(water.surface+Vector3.down*.05f) && v.Contains(water.surface+Vector3.down*1f) && !v.Contains(water.surface+Vector3.up*.05f),"Submersion range "+water.name);
   var probe=water.surface+new Vector3(water.name=="Refueling pool"?1.6f:0,0,0);
   Check(Physics.Raycast(probe+Vector3.down*.01f,Vector3.down,out var bed,2,1<<6) && Mathf.Abs(water.surface.y-bed.point.y-1)<.03f,"One metre deep "+water.name);
  }
  Check(site.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial && r.sharedMaterial.name.EndsWith("water")).All(r=>!r.GetComponent<Collider>()),"Water surfaces have no collision");
  for(int k=1;k<=7;k++)Walk(new(-12.81f-.34f*(7-k)-.17f,1.33f+1.34f*k/7,134),"Pond exit tread "+k);
  for(int i=1;i<4;i++)for(int k=1;k<=4;k++)Walk(new(17,.23f+.9f*(i-1)+.18f*k,140.95f+i*3.1f+.085f-.35f*(4-k)-.175f),"Flume stair "+i+" tread "+k);
  for(int k=1;k<=6;k++)Walk(new(17,2.93f+(4.12f-2.93f)/7*k,153.35f-.30f*(6-k)-.15f),"Flume deck stair "+k);
  for(float t=0;t<=1.001f;t+=1f/72)Walk(Vector3.Lerp(new(-16.3f,35.49f,104.491f),new(16.3f,35.49f,101.509f),t),"Tower pipe walkway");
  Connected(graph,new[]{new Vector3(-18.3f,35.7f,104.5f),new(18.3f,35.7f,101.5f)},"Lookouts joined by walkway");
  foreach(var building in new[]{"Gatehouse","Maintenance"})
   Check(Mathf.Approximately(root.Find(building+"/Baked plant light").GetComponent<Light>().intensity,14),"Dimmed interior lamp "+building);
  var racks=site.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Pipe delivery racks Galvanized steel "));
  var xs=racks.sharedMesh.vertices.Select(v=>racks.transform.TransformPoint(v).x).ToArray();
  Check(xs.Max()<28 && xs.Min()>17,"East rack emptied, other stacks keep their width");
  foreach(var zone in new[]{"Annex heat exchangers","Annex control panel","Annex lockers","Annex parts shelves","Annex shift desk"})
   Check(site.GetComponentsInChildren<MeshCollider>().Any(c=>c.name.StartsWith(zone+" ")),zone+" installed");
  for(float z=52.6f;z<=57.4f;z+=.4f)Walk(new(27,.07f,z),"Annex doorway aisle");
 }
 static void CheckSpawnPools(RailBlock block,CrawlGraph graph,RailSpawnMarker[] markers)
 {
  var asset=AssetDatabase.LoadAssetAtPath<RailBlock>(RailNuclearPlant.PrefabPath);
  foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind))) {
   var points=RailSpawnPool.Available(block,kind);
   var saved=RailSpawnPool.Available(asset,kind);
   Check(points.Length==markers.Count(m=>m.kind==kind) && saved.Length==points.Length,"All saved and active "+kind+" markers registered");
   int minimum=kind==RailSpawnKind.Coal?Math.Max(93,RailResourceBalance.CoalCount(8)):kind==RailSpawnKind.Gold?Math.Max(52,RailResourceBalance.GoldCount(8)):24;
   Check(points.Length>=minimum,kind+" candidate capacity "+points.Length);
   var bounds=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,Vector3.one*.8f):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
   for(int i=0;i<points.Length;i++){
    var t=points[i];var p=t.position;var local=block.transform.InverseTransformPoint(p);
    Check(i<saved.Length && (asset.transform.InverseTransformPoint(saved[i].position)-local).sqrMagnitude<.000001f &&
       PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t)==RailSpawnerPrefabs.Path(kind),"Saved native marker "+t.name);
    bool supported=true;
    foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1}){
     var corner=p+new Vector3(bounds.center.x+sx*bounds.extents.x,bounds.min.y+.09f,bounds.center.z+sz*bounds.extents.z);
     supported &= Physics.Raycast(corner,Vector3.down,out var hit,.29f,1<<6,QueryTriggerInteraction.Ignore) && hit.normal.y>=.90f;
    }
    Check(supported && !Physics.CheckBox(p+bounds.center,bounds.extents*.99f,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Supported clear footprint "+t.name);
    if(kind==RailSpawnKind.Enemy)Check(RailLootPlacementWindow.EnemyLocation(block,p,graph),"Enemy within bounds and 0.8 m of baked ground "+t.name);
   }
  }
  for(int crew=1;crew<=8;crew++){
   int quota=RailEnemySpawnBalance.PerQuadrant(crew);
   for(int seed=0;seed<100;seed++){
    var state=new RailRouteState{resourceCrew=crew,enemySeed=seed};
    foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold}){
     var pick=RailRoute.SelectResources(asset,kind,state);
     int count=kind==RailSpawnKind.Coal?RailResourceBalance.CoalCount(crew):RailResourceBalance.GoldCount(crew);
     Check(pick.Length==count && pick.Distinct().Count()==count,kind+" selection crew "+crew+" seed "+seed);
    }
    var enemies=RailEnemySpawnBalance.Select(asset,seed,crew);
    bool spaced=enemies.All(a=>enemies.All(b=>a==b || Vector2.Distance(new Vector2(a.position.x,a.position.z),new Vector2(b.position.x,b.position.z))>=RailEnemySpawnBalance.MinimumSpacing));
    Check(enemies.Length==4*quota && enemies.Distinct().Count()==enemies.Length && spaced &&
       Enumerable.Range(0,4).All(q=>enemies.Count(t=>RailEnemySpawnBalance.Quadrant(asset,t)==q)==quota),"Enemy quadrants and spacing crew "+crew+" seed "+seed);
   }
  }
 }
 static bool OffPath(float x,float z)
 {
  float inset=Mathf.Abs(x);
  // Poles sit in the 2 m verge between both longitudinal walks. Leave room for the entire base.
  return inset>7.36f && inset<8.64f && new[]{38f,67f,103f,124f}.All(cross=>Mathf.Abs(z-cross)>1.9f);
 }
 static void Connected(CrawlGraph g,Vector3[] p,string name)
 {var nodes=p.Select(q=>g.FindNearestGroundNode(q,2)).ToArray();Check(nodes.All(n=>n>=0),name+" nodes");if(nodes.Any(n=>n<0))return;var seen=new HashSet<int>{nodes[0]};var todo=new Queue<int>();todo.Enqueue(nodes[0]);while(todo.Count>0){int n=todo.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++)if(seen.Add(g.EdgeTarget[e]))todo.Enqueue(g.EdgeTarget[e]);}Check(nodes.All(seen.Contains),name+" path");}
 public static void Capture()
 {
  RailNuclearPlant.Guard();EditorSceneManager.OpenScene(RailNuclearPlant.BakeScene);Directory.CreateDirectory("output/nuclear-plant");var go=new GameObject("Review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.12f,.18f,.22f);cam.farClipPlane=400;
  var sun=new GameObject("Review fill light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(48,30,0);
  try{foreach(var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))t.ForceMeshUpdate();RenderSettings.fog=false;RenderSettings.ambientLight=new(.6f,.6f,.6f);foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.lightmapIndex=-1;
   Shot("overview",new(-64,54,4),new(0,9,79));Shot("barrel-yard",new(8,8,12),new(27,1.5f,25));Shot("tower-interior",new(-34,1.4f,103),new(-22,6,103));Shot("tower-sky",new(26,1,103),new(22,22,103));Shot("turbine-hall",new(-13.5f,1.3f,73),new(-30,3.3f,76));Shot("control-gallery",new(-37,4.2f,67),new(-32,3.6f,76));
  }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailNuclearPlant.BakeScene);}
  void Shot(string name,Vector3 eye,Vector3 target){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=65;var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/nuclear-plant/"+name+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
 }
 [MenuItem("Duck Rails/Authoring/Nuclear Plant/Capture open full site")]
 public static void CaptureFullSite()
 {
  var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if (scene.path != RailNuclearPlant.BakeScene || UnityEngine.SceneManagement.SceneManager.sceneCount != 1 || EditorApplication.isPlaying)
   throw new InvalidOperationException("Open the nuclear bake scene by itself in Edit Mode.");
  var block = Object.FindFirstObjectByType<RailBlock>();
  var camera = new GameObject("Plant review camera").AddComponent<Camera>();
  var key = new GameObject("Temporary inspection sun").AddComponent<Light>();
  var oldPipeline = QualitySettings.renderPipeline;
  var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
  var oldDistance = pipeline.shadowDistance;
  var oldFog = RenderSettings.fog;
  var oldSun = RenderSettings.sun;
  var oldMode = RenderSettings.ambientMode;
  var oldAmbient = RenderSettings.ambientLight;
  try
  {
   QualitySettings.renderPipeline = pipeline;
   pipeline.shadowDistance = 400;
   RenderSettings.fog = false;
   RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
   RenderSettings.ambientLight = new Color(.41f,.45f,.52f);
   key.type = LightType.Directional;
   key.intensity = 1.55f;
   key.color = new Color(1,.95f,.89f);
   key.transform.rotation = Quaternion.Euler(36,102,0);
   key.shadows = LightShadows.Soft;
   key.shadowStrength = .82f;
   RenderSettings.sun = key;
   camera.orthographic = true;
   camera.clearFlags = CameraClearFlags.SolidColor;
   camera.backgroundColor = new Color(.13f,.17f,.21f);
   camera.farClipPlane = 520;
   foreach (var label in block.GetComponentsInChildren<TMPro.TMP_Text>()) label.ForceMeshUpdate();
   Directory.CreateDirectory("output/nuclear-full-block");
   Shot("whole-block",new Vector3(0,2,81),new Vector3(-15,164,-190),60,1800,1660);
   Shot("blueprint",new Vector3(0,0,80),new Vector3(0,230,-.05f),84,1100,1760);
   Shot("waste-yard",new Vector3(23,1,24),new Vector3(-8,9,-14),10,1350,900);
   Shot("entrance",new Vector3(-25,4,8),new Vector3(-2,4,-20),8,1400,800);
   Interior("tower-ramp",new Vector3(-27.8f,1.5f,101),new Vector3(-20,14.5f,108),81);
   Interior("refueling-hall",new Vector3(23.6f,4.15f,67.9f),new Vector3(30.8f,2.02f,73.75f),84);
   Interior("tower-descent",new Vector3(-21.6f,19.2f,104.2f),new Vector3(-27.6f,12.8f,103.5f),90);
   Interior("gatehouse",new Vector3(-25.1f,1.65f,39.9f),new Vector3(-32.3f,1.47f,44.8f),79);
   Interior("maintenance",new Vector3(28.2f,1.65f,41.25f),new Vector3(32.0f,1.41f,46.4f),83);
   Interior("hall-facade",new Vector3(3.5f,4.7f,64.5f),new Vector3(-12.0f,5.55f,71.8f),74);
   Interior("roof-headhouse",new Vector3(-30.5f,11.4f,67.6f),new Vector3(-38.4f,9.5f,77.1f),69);
   Interior("flume-ramp",new Vector3(6.7f,6.8f,130.4f),new Vector3(23,2.7f,148.5f),72);
   Interior("pipe-joints",new Vector3(-14,8.7f,43.9f),new Vector3(-19.8f,7.15f,50.5f),70);
   Interior("roof-stair",new Vector3(-36.8f,4.55f,89.88f),new Vector3(-38.6f,6.35f,80.6f),78);
   Interior("tower-lights",new Vector3(-5,2,63),new Vector3(0,16,103),82);
   Shot("south-arrival", new Vector3(0,2,26), new Vector3(-24,52,-75), 32, 1600, 1000);
   Shot("north-waterworks", new Vector3(0,2,134), new Vector3(-30,55,-74), 32, 1600, 1000);
  }
  finally
  {
   RenderSettings.sun = oldSun;
   RenderSettings.fog = oldFog;
   RenderSettings.ambientMode = oldMode;
   RenderSettings.ambientLight = oldAmbient;
   pipeline.shadowDistance = oldDistance;
   QualitySettings.renderPipeline = oldPipeline;
   Object.DestroyImmediate(key.gameObject);
   Object.DestroyImmediate(camera.gameObject);
  }

  void Interior(string name,Vector3 eye,Vector3 target,float fov)
  {
   camera.orthographic=false;camera.fieldOfView=fov;
   try { Shot(name,target,eye-target,1,1400,900); }
   finally {camera.orthographic=true;}
  }

  void Shot(string name, Vector3 target, Vector3 offset, float size, int width, int height)
  {
   camera.aspect = (float)width/height;
   camera.orthographicSize = size;
   camera.transform.position = target + offset;
   camera.transform.rotation = Quaternion.LookRotation(-offset);
   var previous = RenderTexture.active;
   var buffer = RenderTexture.GetTemporary(width,height,24);
   var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
   try
   {
    camera.targetTexture = buffer;
    camera.Render();
    camera.Render();
    RenderTexture.active = buffer;
    pixels.ReadPixels(new Rect(0,0,width,height),0,0);
    pixels.Apply();
    File.WriteAllBytes("output/nuclear-full-block/"+name+".png",pixels.EncodeToPNG());
   }
   finally
   {
    camera.targetTexture = null;
    RenderTexture.active = previous;
    RenderTexture.ReleaseTemporary(buffer);
    Object.DestroyImmediate(pixels);
   }
  }
 }
 public static void PrepareProof()
 {
  RailNuclearPlant.Guard();var sample=new Vector3(-26,2,61);
  var ids=new[]{RailNuclearPlant.Id,"electrical-substation-v1","lumber-yard-v1","tunnel-works-v1","scrapyard-v1"};
  var expected=new float[ids.Length];for(int i=0;i<ids.Length;i++){EditorSceneManager.OpenScene("Assets/DuckRails/Lighting/Blocks/"+ids[i]+"/"+ids[i]+"-bake.unity");LightProbes.GetInterpolatedProbe(sample,null,out var sh);expected[i]=sh[0,0];}
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var guard=new GameObject("Offline sign-in guard");guard.SetActive(false);guard.AddComponent<LoginFlow>();
  var proof=new GameObject("Nuclear plant route proof").AddComponent<RailCatalogLightingProof>();proof.catalog=ids.Select(id=>AssetDatabase.LoadAssetAtPath<GameObject>(RailWeek3Builder.Folder+"/"+id+".prefab").GetComponent<RailBlock>()).ToArray();proof.expectedRedL0=expected;proof.samplePosition=sample;proof.seeds=new[]{11,43};proof.outputDirectory=Output+"/runtime";proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath).GetComponent<RailBlock>();proof.finalePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailWeek3Builder.Folder+"/bridge-finale-v1.prefab").GetComponent<RailBlock>();
  EditorSceneManager.SaveScene(scene,RailNuclearPlant.LightingFolder+"/PlantProof.unity");
 }
}
#endif
