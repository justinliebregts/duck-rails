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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RailSkyscraper
{
 public const string Id="skyscraper-works-v1", RootName="Skyscraper Works authored v2", Folder="Assets/DuckRails/SkyscraperWorks";
 public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", LightingFolder="Assets/DuckRails/Lighting/Blocks/"+Id, BakeScene=LightingFolder+"/Bake.unity";
 public const int LightCount=24;
 static Material concrete,steel,wood,yellow,gravel;
 static Mesh cube;
 static readonly Dictionary<string,Mesh> boxes=new();
 public static readonly Vector3[] Towers={new(-38,0,54),new(12,0,54),new(-38,0,86),new(12,0,86)};
 public static readonly Rect[] Holes={new(2,2,5,8),new(19,14,5,8),new(8,16,8,6)};
 static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static Material Mat(string name,string source,Color tint)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){var src=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville "+source+".mat");if(!src)throw new Exception("Missing Quackville "+source);m=new Material(src);AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",.03f);EditorUtility.SetDirty(m);return m;
    }
    static Mesh Save(string name,Mesh m)
    {
        m.name=name;string path=Folder+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){old.Clear();old.indexFormat=m.indexFormat;old.vertices=m.vertices;old.normals=m.normals;old.uv=m.uv;old.uv2=m.uv2;old.subMeshCount=m.subMeshCount;for(int i=0;i<m.subMeshCount;i++)old.SetTriangles(m.GetTriangles(i),i);old.bounds=m.bounds;old.UploadMeshData(false);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject MeshGo(Transform p,string n,Mesh mesh,Material mat,bool collision=true)
    {var g=Group(p,n).gameObject;g.layer=6;g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.3f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;}
    static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat,bool collision=true)
    {
        string key=Hash128.Compute(size.ToString("F4")).ToString();if(!boxes.TryGetValue(key,out var m)){m=Object.Instantiate(cube);var v=m.vertices;var ns=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=(Mathf.Abs(ns[i].y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(ns[i].x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}m.uv=uv;m=Save("Box-"+key,m);boxes[key]=m;}
        var g=MeshGo(p,name,m,mat,false);g.transform.localPosition=pos;g.transform.localScale=size;if(collision)g.AddComponent<BoxCollider>();return g;
    }
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float w,Material mat,bool collision=true)
    {var g=Box(p,n,(a+b)/2,new Vector3(w,(b-a).magnitude,w),mat,collision);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}

 static void Slab(Transform p,string n,float x,float z,float w,float d,float y,Material m)=>Box(p,n,new(x+w/2,y-.25f,z+d/2),new(w,.5f,d),m);
 static void Rail(Transform p,Vector3 a,Vector3 b)
 {Beam(p,"Edge handrail",a+Vector3.up*1.1f,b+Vector3.up*1.1f,.1f,yellow);Beam(p,"Edge midrail",a+Vector3.up*.55f,b+Vector3.up*.55f,.08f,steel);int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3));for(int i=0;i<=n;i++)Beam(p,"Guard post",Vector3.Lerp(a,b,(float)i/n),Vector3.Lerp(a,b,(float)i/n)+Vector3.up*1.1f,.09f,steel);}
 static void Stairs(Transform p,float x,float z,float y)
 {
  for(int i=0;i<10;i++){Box(p,"Stair up flight",new(x+1.2f,y+(i+1)*.2f-.1f,z+.18f+i*.36f),new(2.4f,.2f,.36f),concrete);Box(p,"Stair return flight",new(x+3.8f,y+2+(i+1)*.2f-.1f,z+3.42f-i*.36f),new(2.4f,.2f,.36f),concrete);}
  Slab(p,"Stair turn landing",x,z+3.6f,5,2,y+2,concrete);Slab(p,"Stair top landing",x,z-1,5,1,y+4,concrete);
  Beam(p,"Stair centre handrail",new(x+2.5f,y+1.1f,z),new(x+2.5f,y+3.1f,z+3.6f),.1f,yellow);
 }
 static void Floor(Transform p,int level,int tower)
 {
  float y=level*4;
  if(level==0)Slab(p,"Ground slab",0,0,26,24,0,concrete);
  else{float[] xs={0,2,7,8,16,19,24,26},zs={0,2,10,14,16,22,24};for(int i=1;i<xs.Length;i++)for(int j=1;j<zs.Length;j++){var c=new Vector2((xs[i-1]+xs[i])/2,(zs[j-1]+zs[j])/2);if(!Holes.Any(h=>h.Contains(c)))Slab(p,"Open floor slab",xs[i-1],zs[j-1],xs[i]-xs[i-1],zs[j]-zs[j-1],y,concrete);}foreach(var h in Holes.Take(2))Slab(p,"Rear stairwell landing",h.x,h.y+6.6f,h.width,1.4f,y,concrete);}
  if(level<4)foreach(var h in Holes.Take(2))Stairs(p,h.x,h.y+1,y);
  Box(p,"Shaft back wall",new(12,y+2,22),new(8,4,.22f),concrete);
  foreach(float x in new[]{8f,12f,16f})Box(p,"Shaft side wall",new(x,y+2,19),new(.22f,4,6),concrete);
  foreach(float x in new[]{8.5f,11.5f,12.5f,15.5f})Box(p,"Shaft front jamb",new(x,y+2,16),new(.8f,4,.22f),concrete);
  foreach(float x in new[]{10f,14f}){Box(p,"Shaft opening lintel",new(x,y+3.5f,16),new(2.2f,1,.22f),concrete);if(level>0)Rail(p,new(x-1.1f,y,15.9f),new(x+1.1f,y,15.9f));}
  foreach(float x in new[]{1f,7.5f,18f,25f})foreach(float z in new[]{1f,11.5f,23f})if(level<4)Box(p,"Concrete pillar",new(x,y+1.75f,(x==1||x==25)&&z==11.5f?9:z),new(.65f,3.5f,.65f),concrete);
  foreach(var q in new[]{new Vector3(3,y,12),new Vector3(21,y,4),new Vector3(21,y,8)}){if(tower==0&&level==4&&q.z==8)continue;var c=q+new Vector3(((tower+level)%3)*.2f,0,0);Box(p,"Supply pallet",c+Vector3.up*.12f,new(2,.24f,1.2f),wood);for(int i=0;i<3;i++)Box(p,level%2==0?"Cement bags":"Stacked timber",c+new Vector3(0,.35f+i*.18f,0),new(1.7f,.16f,.85f),level%2==0?concrete:wood);}
  for(int i=0;i<4;i++)Beam(p,"Loose pipe bundle",new(21+i*.18f,y+.12f,11),new(21+i*.18f,y+.12f,13),.12f,steel);
  if(level>0){foreach(float x in new[]{0f,26f}){Rail(p,new(x,y,0),new(x,y,8));if(!(level==4&&tower==0&&x==26))Rail(p,new(x,y,16),new(x,y,24));}foreach(float z in new[]{0f,24f}){if(!(level==4&&tower==3&&z==0))Rail(p,new(0,y,z),new(9,y,z));Rail(p,new(17,y,z),new(26,y,z));}}
  if(level<4)Lamp(p,"Tower worklight",new(17,y+3.3f,12));
 }
 static void Tower(Transform root,int i)
 {
  var t=Group(root,"Tower "+(char)('A'+i));t.localPosition=Towers[i];for(int f=0;f<5;f++)Floor(Group(t,"Level "+f+" at "+f*4+"m"),f,i);
  float h=new[]{24f,28f,32f,24f}[i];foreach(float x in new[]{1f,25f})foreach(float z in new[]{1f,23f})Box(t,"Unfinished skyline pillar",new(x,(16+h)/2,z),new(.65f,h-16,.65f),concrete);
  for(float y=20;y<=h;y+=4){Beam(t,"Exposed skyline beam",new(1,y,1),new(25,y,1),.4f,concrete);Beam(t,"Exposed skyline beam",new(1,y,23),new(25,y,23),.4f,concrete);foreach(float x in new[]{1f,25f})Beam(t,"Exposed skyline beam",new(x,y,1),new(x,y,23),.4f,concrete);}
 }
 static void Deck(Transform root,string name,Vector3 a,Vector3 b,float width,Material mat)
 {
  var t=Group(root,name);t.localPosition=a;t.localRotation=Quaternion.LookRotation(b-a);float len=Vector3.Distance(a,b);Slab(t,"Continuous walking deck",-width/2,0,width,len,0,mat);foreach(float x in new[]{-width/2,width/2})Rail(t,new(x,0,0),new(x,0,len));
  if(name.Contains("crane")){foreach(float x in new[]{-width/2,width/2}){Beam(t,"Crane lattice chord",new(x,.1f,0),new(x,.1f,len),.18f,yellow);Beam(t,"Crane lattice chord",new(x,2.6f,0),new(x,2.6f,len),.18f,yellow);for(float z=0;z<len;z+=3)Beam(t,"Crane lattice diagonal",new(x,.1f,z),new(x,2.6f,Mathf.Min(z+3,len)),.12f,yellow);}Box(t,"Fallen crane machinery housing",new(0,.7f,-3),new(2,1.4f,2),yellow);}
 }
 static void Lamp(Transform root,string name,Vector3 p)
 {Box(root,name+" fixture",p,new(.4f,.15f,.4f),yellow,false);var l=Group(root,name).gameObject.AddComponent<Light>();l.transform.localPosition=p;l.transform.localRotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=65;l.range=15;l.spotAngle=130;l.innerSpotAngle=90;l.color=new(1,.83f,.62f);}
 static void Site(Transform root)
 {
  for(int x=-48;x<48;x+=16)for(int z=0;z<160;z+=16)Box(root,"Flat construction site",new(x+8,-.25f,z+8),new(16,.5f,16),gravel);
  for(int z=1;z<160;z+=2)Box(root,"Sleeper",new(0,.015f,z),new(3.8f,.03f,.35f),wood,false);foreach(float x in new[]{-1.5f,1.5f})Box(root,"Rail",new(x,.06f,80),new(.12f,.12f,160),steel,false);
  for(int i=0;i<4;i++)Tower(root,i);var bridges=Group(root,"Open air skybridges");
  Deck(bridges,"South skybridge +8m",new(-12,8,66),new(12,8,66),4,steel);Deck(bridges,"North skybridge +12m",new(-12,12,98),new(12,12,98),4,steel);Deck(bridges,"West side bridge +8m",new(-25,8,78),new(-25,8,86),4,steel);Deck(bridges,"East side bridge +8m",new(25,8,78),new(25,8,86),4,steel);Deck(root,"Fallen crane crossing +16m",new(-20,16,62),new(25,16,90),3,yellow);
  var outer=Group(root,"Approach and departure yards");foreach(float x in new[]{-26f,26f})foreach(float z in new[]{19f,40f,116f,137f}){var t=Group(outer,"Work yard "+x+" "+z);t.localPosition=new(x,0,z);if(z==19||z==116){Box(t,"Shelter roof",new(0,3.5f,0),new(14,.25f,8),steel);Box(t,"Shelter rear wall",new(0,1.7f,3.8f),new(14,3.4f,.25f),concrete);foreach(float xx in new[]{-6.8f,6.8f})Box(t,"Shelter post",new(xx,1.7f,-3.8f),new(.3f,3.4f,.3f),steel);}for(int j=0;j<3;j++)Box(t,"Building supplies",new(-4+j*4,.6f,1),new(2.5f,1.2f,2.2f),wood);Lamp(t,"Yard worklight",new(0,3.2f,0));}
 }
 public static void Guard()=>RailCanyon.Guard();
 [MenuItem("Duck Rails/Authoring/Skyscraper/Install approved district")]
 public static void Install()
 {
  Guard();Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(LightingFolder);AssetDatabase.Refresh();boxes.Clear();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
  concrete=Mat("Raw concrete","concrete",new(.77f,.76f,.7f));wood=Mat("Construction timber","timber",new(.65f,.5f,.3f));steel=Mat("Scaffold steel","weathered paint",new(.42f,.49f,.49f));yellow=Mat("Crane ochre","weathered paint",new(.93f,.58f,.15f));gravel=Mat("Site gravel","gravel",new(.5f,.48f,.41f));
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=new GameObject("Skyscraper Works");var b=go.AddComponent<RailBlock>();b.id=Id;b.displayName="Skyscraper Works";b.tags=new[]{"construction","vertical","skybridges","straight"};b.length=160;b.entry=Group(go.transform,"Track Entry");b.exit=Group(go.transform,"Track Exit");b.exit.localPosition=new(0,0,160);b.stop=Group(go.transform,"Suggested Stop");b.stop.localPosition=new(0,0,80);b.track=Enumerable.Range(0,81).Select(i=>new Vector3(0,0,i*2)).ToArray();
  b.enemyBounds=new Bounds(new(0,9,80),new(84,22,70));b.enemySurfaceRailExclusion=new Bounds(new(0,2.5f,80),new(14,10,160));var root=Group(go.transform,RootName);Site(root);
  var blocker=Group(root,"Railway navigation exclusion").gameObject.AddComponent<CrawlGraphBlocker>();var bx=blocker.GetComponent<BoxCollider>();bx.isTrigger=true;bx.center=b.enemySurfaceRailExclusion.center;bx.size=b.enemySurfaceRailExclusion.size;
  b.variations=new RailSpawnSet[3];var coal=new[]{new Vector3(-24,.5f,66),new(-24,8.5f,98),new(-24,16.5f,63),new(26,.5f,66),new(26,8.5f,98),new(26,16.5f,63)};var enemies=new[]{new Vector3(-24,.2f,66),new(26,.2f,66),new(-24,8.2f,98),new(26,8.2f,98),new(-24,16.2f,63),new(26,12.2f,98)};
  for(int v=0;v<3;v++){var parent=Group(go.transform,"Spawn variation "+v);float dx=(v-1)*.5f;b.variations[v]=new RailSpawnSet{coal=coal.Select((p,i)=>RailCoalAuthoring.Create(parent,"CoalSpawner "+i,p+Vector3.right*dx)).ToArray(),enemies=enemies.Select((p,i)=>{var t=Group(parent,"Enemy "+i);t.localPosition=p+Vector3.right*dx;return t;}).ToArray()};}
  RailEconomyBuilder.AddGold(b);foreach(var v in b.variations){v.gold[0].localPosition=new(-24,12.5f,66);v.gold[1].localPosition=new(26,16.5f,98);}
  var baker=Group(go.transform,"Authored crawlgraph settings").gameObject.AddComponent<CrawlGraphBaker>();baker.transform.localPosition=new(0,9,80);baker.size=new(80,22,96);baker.cellSize=1;baker.agentRadius=.25f;baker.allowCeilings=false;baker.minIslandNodes=10;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();baker.drawNodes=false;
  string graph="Assets/DuckRails/Blocks/"+Id+".bytes";if(!File.Exists(graph))File.WriteAllBytes(graph,new byte[8]);AssetDatabase.ImportAsset(graph);baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(graph);b.graph=baker.graphAsset;Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Skyscraper graph bake failed");
  var lighting=go.AddComponent<RailBakedBlockLighting>();lighting.lightingScene=LightingFolder+"/"+Id+"-Lighting.unity";PrefabUtility.SaveAsPrefabAssetAndConnect(go,PrefabPath,InteractionMode.AutomatedAction);
  var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();for(int z=4;z<160;z+=8)foreach(float x in new[]{-40f,-26f,-12f,0f,12f,26f,40f})foreach(float y in new[]{.7f,2.5f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));foreach(var t in Towers)for(float y=4.7f;y<20;y+=4)foreach(float x in new[]{5f,14f,22f})foreach(float z in new[]{5f,12f,23f})if(!Physics.CheckSphere(t+new Vector3(x,y,z),.2f,1<<6))points.Add(t+new Vector3(x,y,z));foreach(float y in new[]{8.7f,12.7f,16.7f})foreach(float x in new[]{-9f,0f,9f})foreach(float z in new[]{66f,80f,98f})points.Add(new(x,y,z));probes.probePositions=points.ToArray();
  RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new(.045f,.055f,.075f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new(.018f,.024f,.033f);
  var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingFolder+"/Lighting.lighting");if(!settings){settings=new LightingSettings{name="Skyscraper Lighting",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=4,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=32,indirectSampleCount=128,environmentSampleCount=64,maxBounces=4,indirectScale=1.5f};AssetDatabase.CreateAsset(settings,LightingFolder+"/Lighting.lighting");}Lightmapping.lightingSettings=settings;EditorSceneManager.SaveScene(scene,BakeScene);AssetDatabase.SaveAssets();Debug.Log("Skyscraper installed; graph baked. Check before registration and lighting.");
 }
 public static void Register(){Guard();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();var b=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<RailBlock>();if(!route.catalog.Any(x=>x.id==Id))route.catalog=route.catalog.Concat(new[]{b}).ToArray();EditorUtility.SetDirty(route);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(BakeScene);}
}
#endif




