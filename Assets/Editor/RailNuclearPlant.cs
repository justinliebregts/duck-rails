#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using Game.AI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Approved V2 site and interiors, with tightly grouped stacked waste drums.
public static class RailNuclearPlant
{
 public const string Id="nuclear-power-plant-v1", RootName="Nuclear plant authored v2", Folder="Assets/DuckRails/NuclearPlant";
 public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", LightingFolder="Assets/DuckRails/Lighting/Blocks/"+Id, BakeScene=LightingFolder+"/Bake.unity";
 public const int LightCount=29;
 static Material concrete,steel,wood,yellow,gravel,paint,white,red,glass,grass,water,screen,waste;
 static Mesh cube;
 static readonly Dictionary<string,Mesh> boxes=new();
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

 static void Slab(Transform p,string n,float x,float z,float w,float d,float y,Material m)=>Box(p,n,new(x+w/2,y-.15f,z+d/2),new(w,.3f,d),m);
 static void Wall(Transform p,string n,bool az,float axis,float a,float b,float y,float h,Material m,params float[] doors)
 {
  float cursor=a;for(int i=0;i<=doors.Length;i+=2){float end=i<doors.Length?doors[i]:b;Seg(cursor,end,y,h);if(i<doors.Length){Seg(end,doors[i+1],y+2.7f,h-2.7f);cursor=doors[i+1];}}
  void Seg(float lo,float hi,float yy,float hh){if(hi<=lo||hh<=0)return;Box(p,n,az?new(axis,yy+hh/2,(lo+hi)/2):new((lo+hi)/2,yy+hh/2,axis),az?new(.25f,hh,hi-lo):new(hi-lo,hh,.25f),m);}
 }
 static void Lamp(Transform p,Vector3 pos,bool warm=false,float intensity=20)
 {var l=Group(p,"Baked plant light").gameObject.AddComponent<Light>();l.transform.localPosition=pos;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.range=13;l.intensity=intensity;l.color=warm?new(1,.72f,.38f):new(.69f,.85f,1);l.shadows=LightShadows.Soft;Box(p,"Light fixture",pos+Vector3.up*.12f,new(.55f,.15f,.4f),white,false);}
 static void Sign(Transform p,string text,Vector3 pos,float width,float yaw=0,bool glow=false)
 {
  var t=Group(p,"Sign "+text);t.localPosition=pos;t.localRotation=Quaternion.Euler(0,yaw,0);Box(t,"Backing",new(0,0,.08f),new(width,glow?1.9f:.65f,.2f),steel,false);
  var label=Group(t,"Lettering").gameObject.AddComponent<TextMeshPro>();label.transform.localPosition=new(0,0,-.06f);label.text=text;label.fontSize=glow?40:3;label.enableAutoSizing=true;label.fontSizeMin=1;label.fontSizeMax=glow?40:3;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new(width-.3f,glow?1.7f:.6f);label.color=glow?new(1,.78f,.38f):Color.white;
  if(glow){string path=Folder+"/Sign glow.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(label.fontSharedMaterial);mat.SetColor("_FaceColor",new Color(3,2.1f,.7f,1));AssetDatabase.CreateAsset(mat,path);}label.fontSharedMaterial=mat;Lamp(p,pos+new Vector3(0,-.6f,-1),true,35);}label.ForceMeshUpdate();
 }

 // Faceted surfaces have explicit inward/outward faces and their own lightmap UVs.
 sealed class Surface
 {
  readonly List<Vector3> v=new();readonly List<Vector2> uv=new();readonly List<int> tr=new();
  public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
  {int k=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add((Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3);tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
  public Mesh Build(string n){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);return Save(n,m);}
 }
 static Vector3 Ring(float a,float r,float y)=>new(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
 static Mesh Lathe(string n,float[] ys,float[] rs,bool hollow)
 {
  var s=new Surface();const int sides=32;
  for(int j=0;j<ys.Length-1;j++)for(int i=0;i<sides;i++){
   float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
   s.Quad(Ring(a,rs[j],ys[j]),Ring(a,rs[j+1],ys[j+1]),Ring(b,rs[j+1],ys[j+1]),Ring(b,rs[j],ys[j]));
   if(hollow){s.Quad(Ring(b,rs[j]-.3f,ys[j]),Ring(b,rs[j+1]-.3f,ys[j+1]),Ring(a,rs[j+1]-.3f,ys[j+1]),Ring(a,rs[j]-.3f,ys[j]));}
  }
  for(int i=0;i<sides;i++){
   float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int j=ys.Length-1;
   s.Quad(Ring(a,rs[j],ys[j]),Ring(a,hollow?rs[j]-.3f:0,ys[j]),Ring(b,hollow?rs[j]-.3f:0,ys[j]),Ring(b,rs[j],ys[j]));
   s.Quad(Ring(b,rs[0],ys[0]),Ring(b,hollow?rs[0]-.3f:0,ys[0]),Ring(a,hollow?rs[0]-.3f:0,ys[0]),Ring(a,rs[0],ys[0]));
  }
  return s.Build(n);
 }
 static Mesh drumMesh;
 static void Drum(Transform root,Vector3 pos,bool tipped=false,int tier=0)
 {
  var p=Group(root,tipped?"Spilled waste drum":"Waste drum tier "+tier);p.localPosition=pos;
  if(tipped)p.localRotation=Quaternion.Euler(0,(pos.x+pos.z)*19,90);
  MeshGo(p,"Ribbed barrel",drumMesh,yellow);
  // Radiation trefoil made from shared flat geometry, not a text-font dependency.
  var mark=Group(p,"Radiation marking");mark.localPosition=new(0,.52f,-.435f);
  Box(mark,"Warning label",Vector3.zero,new(.38f,.38f,.012f),yellow,false);
  for(int k=0;k<3;k++){float a=k*120*Mathf.Deg2Rad;var arm=Box(mark,"Trefoil lobe",new(Mathf.Sin(a)*.09f,Mathf.Cos(a)*.09f,-.012f),new(.10f,.15f,.01f),gravel,false);arm.transform.localRotation=Quaternion.Euler(0,0,-k*120);}
  Box(mark,"Trefoil centre",new(0,0,-.02f),new(.06f,.06f,.01f),gravel,false);
 }
 static void Pipe(Transform p,Vector3 a,Vector3 b,float width=.4f)=>Beam(p,"Utility pipe",a,b,width,steel);
 static void Tower(Transform root,float z,bool drained)
 {
  var p=Group(root,drained?"Tower B drained":"Tower A damp");p.localPosition=new(-26,0,z);
  MeshGo(p,"Hollow cooling shell",Lathe("Cooling shell",new[]{3.2f,8f,16f,24f,30f,35f},new[]{11f,9.8f,7.6f,6.8f,7.3f,8.5f},true),concrete);
  // Open undercroft; designated wide east and west entrances, no hidden cap collider.
  for(int i=0;i<16;i++){float a=i*Mathf.PI/8;if(Mathf.Abs(Mathf.Sin(a))<.2f)continue;Beam(p,"Shell support",Ring(a,10.4f,0),Ring(a+.05f,10.8f,3.2f),.45f,concrete);}
  foreach(float x in new[]{-4.3f,4.3f})foreach(float zz in new[]{-4.3f,4.3f})Box(p,"Interior support",new(x,2.35f,zz),new(.8f,4.7f,.8f),concrete);
  var floor=Lathe("Tower floor",new[]{-.16f,.035f},new[]{10.7f,10.7f},false);MeshGo(p,"Basin floor",floor,drained?gravel:concrete);
  // Flat decorative water does not trap players or loose items below an inaccessible mesh.
  if(!drained){var s=new Surface();for(int x=-7;x<7;x++)for(int zz=-7;zz<7;zz++){if(Mathf.Abs(x+.5f)<1.6f||Mathf.Abs(zz+.5f)<1.6f||new Vector2(x+.5f,zz+.5f).magnitude>6.8f)continue;s.Quad(new(x,.045f,zz),new(x,.045f,zz+1),new(x+1,.045f,zz+1),new(x+1,.045f,zz));}MeshGo(p,"Shallow basin water",s.Build("Basin water"),water,false);}
  Slab(p,"Crosswalk east west",-11,-1.5f,22,3,.06f,concrete);Slab(p,"Crosswalk north south",-1.5f,-10.5f,3,21,.055f,concrete);
  foreach(float zz in new[]{-5.5f,5.5f}){
   if(!drained||zz>0){Box(p,"Retained overhead fill",new(0,5,zz),new(10,.6f,2.4f),wood);for(int k=-4;k<=4;k++)Box(p,"Fill slats",new(k,4.62f,zz),new(.15f,.15f,2.4f),steel,false);Pipe(p,new(-5,5.6f,zz),new(5,5.6f,zz));}
  }
  Pipe(p,new(-6,4.6f,-6),new(-6,4.6f,6));Pipe(p,new(6,4.6f,-6),new(6,4.6f,6));
  if(drained)foreach(var q in new[]{new Vector3(-4,.22f,-5.7f),new(5,.22f,2.8f)}){var debris=Box(p,"Fallen fill panel",q,new(2.4f,.44f,1.4f),wood);debris.transform.localRotation=Quaternion.Euler(0,22,0);}
  foreach(float x in new[]{-9f,9f}){Lamp(p,new(x,2.7f,0),true,26);Sign(p,"EXIT",new(x,2.3f,1.8f),1.8f,0);}
  Sign(p,drained?"COOLING 02":"COOLING 01",new(11.2f,2.5f,0),4,90);
  foreach(float a in new[]{45f,225f}){var q=Ring(a*Mathf.Deg2Rad,8.4f,35.2f);Box(p,"Red warning beacon",q,new(.35f,.45f,.35f),red,false);}
 }
 public static readonly Rect[] StairHoles={new(33,57,3,9),new(33,80,3,9)};
 public static Vector3 StairPoint(int flight,int tread)=>new(34.5f,(tread+1)*.2f,flight==0?57+(tread+.5f)*.6f:89-(tread+.5f)*.6f);
 static void Hall(Transform root)
 {
  var p=Group(root,"Turbine hall");Slab(p,"Ground slab",12,55,28,36,.035f,concrete);
  Wall(p,"West entry wall",true,12,55,91,0,7.8f,paint,71,75);Wall(p,"East service wall",true,40,55,91,0,7.8f,paint,71,75);
  foreach(float z in new[]{55f,91f})Wall(p,"End wall",false,z,12,40,0,7.8f,paint);
  Slab(p,"Roof",11.8f,54.8f,28.4f,36.4f,8.1f,steel);
  // Gallery deck split exactly around both stair openings.
  float[] xs={32,33,36,40},zs={55,57,66,80,89,91};
  for(int i=1;i<xs.Length;i++)for(int j=1;j<zs.Length;j++){var c=new Vector2((xs[i-1]+xs[i])/2,(zs[j-1]+zs[j])/2);if(StairHoles.Any(h=>h.Contains(c)))continue;Slab(p,"Gallery deck",xs[i-1],zs[j-1],xs[i]-xs[i-1],zs[j]-zs[j-1],3,steel);}
  Beam(p,"Gallery edge rail",new(32,4.05f,55),new(32,4.05f,91),.12f,steel);for(int z=55;z<=91;z+=3)Box(p,"Gallery baluster",new(32,3.5f,z),new(.1f,1,.1f),steel);
  for(int f=0;f<2;f++){
   for(int i=0;i<15;i++)Box(p,"Stair tread",StairPoint(f,i)-new Vector3(0,.1f,0),new(3,.2f,.6f),concrete);
   var a=StairPoint(f,0);var b=StairPoint(f,14);foreach(float x in new[]{32.9f,36.1f})Beam(p,"Stair handrail",new(x,a.y+.85f,a.z),new(x,b.y+.85f,b.z),.09f,steel);
  }
  foreach(float z in new[]{63f,78f}){
   var t=Group(p,"Turbine unit");t.localPosition=new(23,0,z+4);Box(t,"Machine plinth",new(0,.25f,0),new(6,.5f,8),concrete);
   var body=MeshGo(t,"Turbine housing",Lathe("Turbine housing mesh",new[]{0f,1f,5.6f,6f},new[]{1.4f,1.8f,1.8f,1.3f},false),steel);body.transform.localPosition=new(0,2.1f,-3);body.transform.localRotation=Quaternion.Euler(90,0,0);
   foreach(float xx in new[]{-2.4f,2.4f})Pipe(t,new(xx,.65f,-3),new(xx,.65f,3),.28f);
   Box(t,"Generator casing",new(0,1.2f,2.8f),new(3,2,1.8f),paint);
  }
  for(int z=68;z<=77;z+=3){Box(p,"Control console",new(38.9f,3.65f,z),new(1.4f,1.3f,2.7f),steel);Box(p,"Green console display",new(38.15f,4.05f,z),new(.05f,.55f,2.4f),screen,false);}
  Box(p,"Maintenance workbench",new(20,.65f,56.4f),new(12,1.3f,1.3f),wood);
  foreach(float z in new[]{60f,73f,86f}){Lamp(p,new(16,6.5f,z),true,55);Lamp(p,new(37.5f,6.5f,z),false,40);}
  Sign(p,"TURBINE HALL",new(11.8f,5,73),12,-90);Sign(p,"CONTROL",new(31.8f,5.2f,73),8,-90);
  var reactor=Group(root,"Reactor containment exterior");reactor.localPosition=new(26,0,103);MeshGo(reactor,"Containment shell",Lathe("Reactor containment",new[]{0f,10f,13f,15f,16f},new[]{10f,10f,8.8f,5.5f,.4f},false),concrete);
  // Solid base prevents accidental access into an unplanned reactor interior.
  Box(reactor,"Sealed containment base",new(0,4,0),new(13,8,13),concrete);
  Box(reactor,"Roof warning beacon",new(0,16.5f,0),new(.45f,.6f,.45f),red,false);
 }
 static void SmallBuilding(Transform root,string name,float x,float z,float w,float d,bool eastFacing)
 {
  var p=Group(root,name);Slab(p,"Floor",x,z,w,d,.035f,concrete);
  Wall(p,"West wall",true,x,z,z+d,0,3.6f,paint,eastFacing?Array.Empty<float>():new[]{z+3,z+7});Wall(p,"East wall",true,x+w,z,z+d,0,3.6f,paint,eastFacing?new[]{z+3,z+7}:Array.Empty<float>());
  foreach(float zz in new[]{z,z+d})Wall(p,"End wall",false,zz,x,x+w,0,3.6f,paint);Slab(p,"Roof",x-.2f,z-.2f,w+.4f,d+.4f,3.9f,steel);
  Box(p,"Shelving",new(eastFacing?x+1:x+w-1,1.1f,z+d/2),new(1,2.2f,d-3),wood);Box(p,"Desk",new(x+w/2,.6f,z+d-1.5f),new(w-4,1.2f,1),steel);Lamp(p,new(x+w/2,3.3f,z+d/2),true,28);
  Sign(p,name.ToUpperInvariant(),new(eastFacing?x+w+.2f:x-.2f,2.9f,z+5),w-1,eastFacing?90:-90);
 }
 static void Truck(Transform root)
 {
  var p=Group(root,"Truck with spilled load");p.localPosition=new(17,0,29);p.localRotation=Quaternion.Euler(0,-8,0);
  Box(p,"Chassis",new(0,.7f,0),new(2.7f,.4f,7),steel);Box(p,"Flatbed",new(0,1.1f,-.8f),new(3,.25f,5.2f),wood);
  Box(p,"Cab",new(0,1.7f,2.4f),new(2.7f,2,2),paint);Box(p,"Windshield",new(0,2.2f,3.42f),new(2.3f,.75f,.03f),glass,false);
  foreach(float x in new[]{-1.5f,1.5f}){Box(p,"Cargo sideboard",new(x,1.5f,-.8f),new(.12f,.6f,5.2f),steel);foreach(float z in new[]{-2f,2.5f}){var wheel=Box(p,"Tyre",new(x,.55f,z),new(.35f,1.1f,1.1f),gravel);wheel.transform.localRotation=Quaternion.Euler(45,0,0);}}
  var gate=Box(p,"Dropped tailgate",new(0,.7f,-3.9f),new(3,.15f,1.4f),steel);gate.transform.localRotation=Quaternion.Euler(25,0,0);
  foreach(float x in new[]{-.55f,.55f})foreach(float z in new[]{-1f,.1f})Drum(p,new(x,1.23f,z));
 }
 static void WasteYard(Transform root)
 {
  var p=Group(root,"Dense waste barrel yard");Slab(p,"Loading apron",11,16,29,18,.01f,gravel);Truck(p);
  // Three dense pallet clusters, 1.02m centres for 0.84m bodies, separated by aisles.
  for(int cluster=0;cluster<3;cluster++)for(int row=0;row<3;row++)for(int col=0;col<5;col++){
   float x=27+col*1.02f,z=18+cluster*5+row*1.02f;int tiers=col<3?3:2;
   for(int t=0;t<tiers;t++)Drum(p,new(x,.02f+t*1.02f,z),false,t);
  }
  foreach(var q in new[]{new Vector3(16,.44f,22),new(18,.44f,20),new(20,.44f,23),new(21,.44f,18),new(23,.44f,26),new(24,.44f,22)})Drum(p,q,true);
  foreach(var q in new[]{new Vector3(15,.02f,18),new(22,.02f,29),new(24,.02f,19)})Drum(p,q);
  foreach(var q in new[]{new Vector3(19,.025f,21),new(23,.025f,24)}){var spill=Box(p,"Small waste spill",q,new(2.5f,.015f,1.6f),waste,false);spill.transform.localRotation=Quaternion.Euler(0,27,0);}
  Sign(p,"WASTE STORAGE",new(33,2.3f,33.5f),8,0);Lamp(p,new(25,5,25),true,45);
 }
 static void Tree(Transform p,float x,float z){Box(p,"Tree trunk",new(x,1.3f,z),new(.45f,2.6f,.45f),wood);var t=Box(p,"Tree crown",new(x,3.1f,z),new(2.8f,3,2.8f),grass,false);t.transform.localRotation=Quaternion.Euler(0,(x+z)*17,0);}
 static void Fence(Transform p,float x,float z,float length)
 {for(float zz=z;zz<=z+length;zz+=3)Box(p,"Fence post",new(x,1,zz),new(.12f,2,.12f),steel);foreach(float y in new[]{.5f,1.6f})Beam(p,"Fence rail",new(x,y,z),new(x,y,z+length),.08f,steel);}
 static void Site(Transform root)
 {
  for(int x=-48;x<48;x+=16)for(int z=0;z<160;z+=16)Slab(root,"Flat ground",x,z,16,16,0,grass);
  Slab(root,"Rail ballast",-4,0,8,160,.006f,gravel);for(int z=1;z<160;z+=2)Box(root,"Sleeper",new(0,.025f,z),new(3.8f,.03f,.35f),wood,false);foreach(float x in new[]{-1.5f,1.5f})Box(root,"Rail",new(x,.07f,80),new(.12f,.12f,160),steel,false);
  foreach(float x in new[]{-7f,4f,-41f,-12f,9f,42f})Slab(root,"Long walking path",x,6,3,148,.015f,concrete);
  foreach(float z in new[]{38f,67f,94f,124f}){Slab(root,"West access path",-41,z-1.5f,34,3,.02f,concrete);Slab(root,"East access path",7,z-1.5f,38,3,.02f,concrete);}
  // Hall doors align at Z73, not at the tower entrances.
  Slab(root,"Hall arrival path",7,71,5,4,.025f,concrete);Slab(root,"Hall rear exit path",40,71,5,4,.025f,concrete);
  Tower(root,67,false);Tower(root,94,true);Hall(root);
  SmallBuilding(root,"Gatehouse",-38,35,14,12,true);SmallBuilding(root,"Maintenance",27,37,13,12,false);SmallBuilding(root,"Pump hut",-37,140,10,7,true);
  WasteYard(root);
  Slab(root,"Old parking",-39,17,25,15,.01f,gravel);
  foreach(float x in new[]{-35f,-28f,-21f}){Box(root,"Abandoned car body",new(x,.65f,23),new(2.3f,1.3f,4.6f),steel);Box(root,"Abandoned car cabin",new(x,1.4f,23.3f),new(1.9f,1,2.4f),glass);}
  Slab(root,"Cooling basin apron",-39,120,25,19,.02f,concrete);Slab(root,"Cooling basin water",-37,122,21,15,.04f,water); // shallow, physically supported surface, not a fall trap
  var yard=Group(root,"Transformer yard");Slab(yard,"Transformer apron",15,121,25,20,.02f,gravel);
  foreach(float x in new[]{19f,29f})foreach(float z in new[]{125f,134f}){Box(yard,"Transformer",new(x+2.5f,1.1f,z+1.5f),new(5,2.2f,3),steel);foreach(float xx in new[]{x+1,x+2.5f,x+4})Box(yard,"Insulator",new(xx,2.8f,z+1.5f),new(.3f,1.2f,.3f),white);}
  foreach(float x in new[]{14f,41f})Fence(yard,x,121,20);
  foreach(float z in new[]{16f,40f,119f,145f})foreach(float x in new[]{-10f,10f}){Box(root,"Yard lamp post",new(x,2.5f,z),new(.16f,5,.16f),steel);Lamp(root,new(x,5,z),true,35);}
  foreach(var q in new[]{new Vector3(-26,3,130),new(27,4,130),new(-26,3,80),new(26,4,96),new(-20,3,28),new(20,3,42),new(-34,3,106)})Lamp(root,q,false,24);
  foreach(float z in new[]{10f,27f,49f,116f,146f,151f})foreach(float x in new[]{-44f,46f})Tree(root,x,z);
  foreach(float z in new[]{10f,151f})foreach(float x in new[]{-30f,-17f,16f,29f})Tree(root,x,z);
  foreach(float z in new[]{20f,42f,116f,142f})foreach(float x in new[]{-44f,46f})Fence(root,x,z,10);
 }
 public static void Guard()=>RailCanyon.Guard();
 [MenuItem("Duck Rails/Authoring/Nuclear Plant/Install approved block")]
 public static void Install()
 {
  Guard();Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(LightingFolder);AssetDatabase.Refresh();boxes.Clear();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
  concrete=Mat("Plant concrete","concrete",new(.74f,.75f,.69f));steel=Mat("Plant steel","weathered paint",new(.36f,.46f,.48f));wood=Mat("Weathered timber","timber",new(.55f,.43f,.27f));yellow=Mat("Waste drum ochre","weathered paint",new(.82f,.59f,.18f));gravel=Mat("Dark gravel","gravel",new(.33f,.37f,.35f));paint=Mat("Plant teal","weathered paint",new(.40f,.57f,.54f));white=Mat("Insulator ivory","concrete",new(.82f,.82f,.7f));red=Mat("Beacon red","weathered paint",new(.9f,.12f,.08f));glass=Mat("Dark glass","dark roofing",new(.13f,.23f,.27f));grass=Mat("Plant grass","grass",new(.43f,.52f,.34f));water=Mat("Basin water","lake water",new(.24f,.43f,.46f));screen=Mat("Green displays","weathered paint",new(.25f,.9f,.4f));waste=Mat("Small waste spill","lake water",new(.48f,.55f,.18f));
  foreach(var m in new[]{screen,red}){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",m.GetColor("_BaseColor")*2);EditorUtility.SetDirty(m);}
  drumMesh=Lathe("Shared ribbed drum",new[]{0f,.06f,.15f,.20f,.80f,.85f,.96f,1.02f},new[]{.38f,.42f,.42f,.40f,.40f,.42f,.42f,.38f},false);
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=new GameObject("Nuclear Power Plant");var b=go.AddComponent<RailBlock>();b.id=Id;b.displayName="Nuclear Power Plant";b.tags=new[]{"industrial","cooling-towers","interior","straight"};b.length=160;b.entry=Group(go.transform,"Track Entry");b.exit=Group(go.transform,"Track Exit");b.exit.localPosition=new(0,0,160);b.stop=Group(go.transform,"Suggested Stop");b.stop.localPosition=new(0,0,80);b.track=Enumerable.Range(0,81).Select(i=>new Vector3(0,0,i*2)).ToArray();
  b.enemyBounds=new Bounds(new(0,3,80),new(84,10,70));b.enemySurfaceRailExclusion = default;var root=Group(go.transform,RootName);Site(root);
  b.variations=new RailSpawnSet[3];var coal=new[]{new Vector3(-26,.5f,67),new(-26,.5f,94),new(-36,.5f,80),new(16,.5f,63),new(16,.5f,86),new(37,3.5f,73)};var enemies=new[]{new Vector3(-20,.2f,67),new(-26,.2f,94),new(-36,.2f,80),new(16,.2f,73),new(30,.2f,86),new(37,3.2f,73)};
  for(int v=0;v<3;v++){var parent=Group(go.transform,"Spawn variation "+v);float dx=(v-1)*.4f;b.variations[v]=new RailSpawnSet{coal=coal.Select((p,i)=>RailCoalAuthoring.Create(parent,"CoalSpawner "+i,p+Vector3.right*dx)).ToArray(),enemies=enemies.Select((p,i)=>{var t=Group(parent,"Enemy "+i);t.localPosition=p+Vector3.right*dx;return t;}).ToArray()};}
  RailEconomyBuilder.AddGold(b);foreach(var v in b.variations){v.gold[0].localPosition=new(-26,.5f,101);v.gold[1].localPosition=new(37,3.5f,77);}
  var baker=Group(go.transform,"Authored crawlgraph settings").gameObject.AddComponent<CrawlGraphBaker>();baker.transform.localPosition=new(0,6,80);baker.size=new(80,16,96);baker.cellSize=1;baker.agentRadius=.25f;baker.allowCeilings=false;baker.minIslandNodes=10;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();baker.drawNodes=false;
  string graph="Assets/DuckRails/Blocks/"+Id+".bytes";if(!File.Exists(graph))File.WriteAllBytes(graph,new byte[8]);AssetDatabase.ImportAsset(graph);baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(graph);b.graph=baker.graphAsset;Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Plant graph bake failed");
  var lighting=go.AddComponent<RailBakedBlockLighting>();lighting.lightingScene=LightingFolder+"/"+Id+"-Lighting.unity";PrefabUtility.SaveAsPrefabAssetAndConnect(go,PrefabPath,InteractionMode.AutomatedAction);
  var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();for(int z=4;z<160;z+=8)foreach(float x in new[]{-40f,-26f,-12f,0f,12f,26f,40f})foreach(float y in new[]{.7f,2.5f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));foreach(float z in new[]{60f,67f,73f,80f,87f})foreach(float x in new[]{16f,30f,37f})foreach(float y in new[]{.7f,3.7f,5f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));foreach(float z in new[]{67f,94f})foreach(float x in new[]{-34f,-26f,-18f})foreach(float y in new[]{.7f,2.5f})points.Add(new(x,y,z));probes.probePositions=points.ToArray();
  RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new(.045f,.055f,.075f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new(.018f,.024f,.033f);
  var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingFolder+"/Lighting.lighting");if(!settings){settings=new LightingSettings{name="Nuclear Plant Lighting",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=4,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=32,indirectSampleCount=128,environmentSampleCount=64,maxBounces=4,indirectScale=1.5f};AssetDatabase.CreateAsset(settings,LightingFolder+"/Lighting.lighting");}Lightmapping.lightingSettings=settings;EditorSceneManager.SaveScene(scene,BakeScene);AssetDatabase.SaveAssets();Debug.Log("Plant installed; graph baked. Check before registration and lighting.");
 }
 public static void Register(){Guard();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();var b=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<RailBlock>();if(!route.catalog.Any(x=>x.id==Id))route.catalog=route.catalog.Concat(new[]{b}).ToArray();EditorUtility.SetDirty(route);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(BakeScene);}
}
#endif

