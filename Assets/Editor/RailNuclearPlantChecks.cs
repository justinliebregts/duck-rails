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
 public static void Run()
 {
  RailNuclearPlant.Guard();EditorSceneManager.OpenScene(RailNuclearPlant.BakeScene);RunOpen();
 }
 public static void RunOpen()
 {
  if(UnityEngine.SceneManagement.SceneManager.sceneCount!=1 || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RailNuclearPlant.BakeScene)throw new InvalidOperationException("Open the nuclear bake scene alone for physics checks.");
  Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Output);
  try{
   var b=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RailNuclearPlant.RootName);Check(b.id==RailNuclearPlant.Id&&b.length==160&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160),"Identity and flat track anchors");
   for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Rail clearance "+z);
   foreach(float z in new[]{2f,158f})for(int x=-47;x<=47;x+=2)Walk(new(x,0,z),"Flat seam");
   foreach(float cz in new[]{67f,94f}){
    for(float x=9;x<=39;x+=.5f)Walk(new(x,.025f,cz),"Tower east west access");
    for(float z=cz-9;z<=cz+9;z+=.5f)Walk(new(26,.025f,z),"Tower centre crosswalk");
    for(int a=0;a<360;a+=10){float r=a*Mathf.Deg2Rad;Walk(new(26+9*Mathf.Cos(r),.015f,cz+9*Mathf.Sin(r)),"Tower ring");}
    Check(!Physics.Raycast(new(26,.2f,cz),Vector3.up,45,1<<6,QueryTriggerInteraction.Ignore),"Open view up hollow tower "+cz);
    Check(Physics.Raycast(new(26,12,cz),Vector3.right,20,1<<6,QueryTriggerInteraction.Ignore),"Inner shell collision "+cz);
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
   var all=root.GetComponentsInChildren<Transform>();Check(all.Count(t=>t.name.StartsWith("Waste drum tier "))==124&&all.Count(t=>t.name=="Spilled waste drum")==6,"130 total waste drums, six tipped");
   Check(all.Count(t=>t.name=="Waste drum tier 2")==27,"27 third-tier barrels");
   Check(all.Count(t=>t.name=="Radiation marking")==130,"Every drum has radiation marking");
   foreach(var t in all.Where(t=>t.name.StartsWith("Waste drum tier "))){var ray=t.position+Vector3.up*.03f;Check(Physics.Raycast(ray,Vector3.down,out var hit,.15f,1<<6,QueryTriggerInteraction.Ignore),"Barrel supported "+t.position);}
   Check(root.GetComponentsInChildren<Light>().Length==RailNuclearPlant.LightCount,"Expected baked light count");Check(root.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Environment fixtures bake-only");
   Check(root.GetComponentsInChildren<Rigidbody>().Length==0,"Scenery does not add loose networked physics");
   var graph=CrawlGraph.Load(b.graph);Check(graph!=null&&graph.NodeCount>100,"Prebaked graph present");
   var markers=b.GetComponentsInChildren<RailSpawnMarker>(true);
   Check(markers.Count(m=>m.kind==RailSpawnKind.Coal)==18 && markers.Count(m=>m.kind==RailSpawnKind.Gold)==6 && markers.Count(m=>m.kind==RailSpawnKind.Enemy)==18,"Retained authored candidate counts");
   foreach(var marker in markers){
    var t=marker.transform;
    Check(Physics.Raycast(t.position,Vector3.down,1.1f,1<<6,QueryTriggerInteraction.Ignore),"Spawn support "+t.name);
    Check(!Physics.CheckSphere(t.position+Vector3.up*.4f,.35f,1<<6,QueryTriggerInteraction.Ignore),"Spawn clearance "+t.name);
    Check(graph.FindNearestGroundNode(t.position,2)>=0,"Spawn graph "+t.name);
   }
   var site=root.Find(RailNuclearFullBlock.RootName);Check(site,"Finished site present");
   foreach(string zone in new[]{"Shift bus turnaround","Pipe delivery racks","Cooling tower manifolds","Supply trunk and collars","Reed settling pond","Stepped discharge flume","Transformer fins"})
    Check(site.GetComponentsInChildren<MeshCollider>().Any(c=>c.name.StartsWith(zone+" ")),zone+" has physical support");
   foreach(var t in site.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font.name=="CutePixel SDF","Site font "+t.text);
   Walk(new(-23,.12f,15),"Shift arrival");Walk(new(23,.04f,9),"Between delivery racks");Walk(new(-24,.21f,131),"Settling pond floor");Walk(new(23,.42f,146),"First flume step");
   Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
   Connected(graph,new[]{new Vector3(26,.2f,67),new(26,.2f,94),new(36,.2f,80)},"Both cooling towers connected");
   Connected(graph,new[]{new Vector3(-16,.2f,73),new(-30,.2f,86),new(-37,3.2f,73)},"Ground and upper gallery connected");
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>().Where(m=>!m.GetComponent<TMPro.TMP_Text>()))Check(mf.sharedMesh.uv.Length==mf.sharedMesh.vertexCount&&mf.sharedMesh.uv2.Length==mf.sharedMesh.vertexCount,"UV channels "+mf.name);
   var failed=lines.Where(l=>l.StartsWith("FAIL")).ToArray();if(failed.Length>0)throw new Exception(failed.Length+" failures:\n"+string.Join("\n",failed.Take(40)));Debug.Log("NUCLEAR PLANT CHECKS: "+lines.Count+" passed");
  }finally{File.WriteAllLines(Output+"/geometry-checks.txt",lines);}
 }
 static void Connected(CrawlGraph g,Vector3[] p,string name)
 {var nodes=p.Select(q=>g.FindNearestGroundNode(q,2)).ToArray();Check(nodes.All(n=>n>=0),name+" nodes");if(nodes.Any(n=>n<0))return;var seen=new HashSet<int>{nodes[0]};var todo=new Queue<int>();todo.Enqueue(nodes[0]);while(todo.Count>0){int n=todo.Dequeue();for(int e=g.EdgeStart[n];e<g.EdgeStart[n+1];e++)if(seen.Add(g.EdgeTarget[e]))todo.Enqueue(g.EdgeTarget[e]);}Check(nodes.All(seen.Contains),name+" path");}
 public static void Capture()
 {
  RailNuclearPlant.Guard();EditorSceneManager.OpenScene(RailNuclearPlant.BakeScene);Directory.CreateDirectory("output/nuclear-plant");var go=new GameObject("Review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.12f,.18f,.22f);cam.farClipPlane=400;
  var sun=new GameObject("Review fill light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(48,30,0);
  try{foreach(var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))t.ForceMeshUpdate();RenderSettings.fog=false;RenderSettings.ambientLight=new(.6f,.6f,.6f);foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))r.lightmapIndex=-1;
   Shot("overview",new(-64,54,4),new(0,9,79));Shot("barrel-yard",new(8,8,12),new(27,1.5f,25));Shot("tower-interior",new(34,1.4f,67),new(22,6,67));Shot("tower-sky",new(26,1,94),new(22,22,94));Shot("turbine-hall",new(-13.5f,1.3f,73),new(-30,3.3f,76));Shot("control-gallery",new(-37,4.2f,67),new(-32,3.6f,76));
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
   Shot("whole-block", new Vector3(0,3,80), new Vector3(-137,155,-174), 72, 2800, 2144);
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

