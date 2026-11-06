#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using TMPro;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RailHospital
{
 public const string Id="hospital-district-v1", RootName="Hospital district authored v4", Folder="Assets/DuckRails/HospitalDistrict";
 public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", LightingFolder="Assets/DuckRails/Lighting/Blocks/"+Id, BakeScene=LightingFolder+"/Bake.unity";
 public const int LightCount=40;
 static Material concrete,steel,wood,yellow,gravel,paint,white,red,glass,grass;
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
 {var l=Group(p,"Baked hospital light").gameObject.AddComponent<Light>();l.transform.localPosition=pos;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.range=13;l.intensity=intensity;l.color=warm?new(1,.72f,.38f):new(.69f,.85f,1);l.shadows=LightShadows.Soft;Box(p,"Light fixture",pos+Vector3.up*.12f,new(.55f,.15f,.4f),white,false);}
 static void Sign(Transform p,string text,Vector3 pos,float width,float yaw=0,bool glow=false)
 {
  var t=Group(p,"Sign "+text);t.localPosition=pos;t.localRotation=Quaternion.Euler(0,yaw,0);Box(t,"Backing",new(0,0,.08f),new(width,glow?1.9f:.65f,.2f),steel,false);
  var label=Group(t,"Lettering").gameObject.AddComponent<TextMeshPro>();label.transform.localPosition=new(0,0,-.06f);label.text=text;label.fontSize=glow?40:3;label.enableAutoSizing=true;label.fontSizeMin=1;label.fontSizeMax=glow?40:3;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new(width-.3f,glow?1.7f:.6f);label.color=glow?new(1,.78f,.38f):Color.white;
  if(glow){string path=Folder+"/Sign glow.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(label.fontSharedMaterial);mat.SetColor("_FaceColor",new Color(3,2.1f,.7f,1));AssetDatabase.CreateAsset(mat,path);}label.fontSharedMaterial=mat;Lamp(p,pos+new Vector3(0,-.6f,-1),true,35);}label.ForceMeshUpdate();
 }
 static void Bed(Transform p,Vector3 q,bool surgery=false)
 {var t=Group(p,surgery?"Operating table":"Patient bed");t.localPosition=q;Box(t,"Base",new(0,.45f,0),new(.6f,.9f,1),steel);Box(t,"Mattress",new(0,.95f,0),new(1.25f,.2f,2.5f),surgery?paint:white);Box(t,"Pillow",new(0,1.1f,.8f),new(.9f,.15f,.5f),white);foreach(float x in new[]{-.7f,.7f})Box(t,"Bed rail",new(x,1.15f,0),new(.08f,.35f,2.4f),steel);if(surgery){Beam(t,"Surgical light arm",new(0,3.3f,0),new(.7f,2.7f,0),.12f,steel,false);Box(t,"Surgical lamp",new(.7f,2.65f,0),new(.8f,.2f,.8f),white,false);Box(t,"Instrument trolley",new(1.6f,.65f,0),new(.65f,1.3f,1),steel);}}
 public static readonly Rect[] StairHoles={new(36,57,5,6),new(36,98,5,6)};
 static void Floor(Transform p,float y)
 {
  float[] xs={10,22,30,36,41,42},zs={55,57,63,70,90,98,104,105};
  for(int i=1;i<xs.Length;i++)for(int j=1;j<zs.Length;j++){var c=new Vector2((xs[i-1]+xs[i])/2,(zs[j-1]+zs[j])/2);if(new Rect(22,70,8,20).Contains(c)||y>0&&StairHoles.Any(h=>h.Contains(c)))continue;Slab(p,"Floor slab "+y,xs[i-1],zs[j-1],xs[i]-xs[i-1],zs[j]-zs[j-1],y,concrete);}
 }
 static void Stairs(Transform p,float z,float y)
 {
  var t=Group(p,"Stair flight at "+z+" level "+y);
  for(int i=0;i<10;i++){Box(t,"Ascending tread",new(37.2f,y+(i+1)*.2f-.1f,z+.18f+i*.36f),new(2.4f,.2f,.36f),concrete);Box(t,"Returning tread",new(39.8f,y+2+(i+1)*.2f-.1f,z+3.42f-i*.36f),new(2.4f,.2f,.36f),concrete);}
  Slab(t,"Turn landing",36,z+3.6f,5,2.4f,y+2,concrete);
  Beam(t,"Stair rail",new(38.5f,y+1.1f,z),new(38.5f,y+3.1f,z+3.6f),.1f,steel);
 }
 static void WindowSide(Transform p,bool az,float axis,float a,float b,float y,params float[] doors)
 {
  // Segmented exterior walls give low-eye views through real open windows.
  for(float k=a;k<b;k+=4){float end=Mathf.Min(k+4,b);if(doors.Length>0&&k<doors[1]&&end>doors[0]){Wall(p,"Door surround",az,axis,k,end,y,3.7f,paint,Mathf.Max(k,doors[0]),Mathf.Min(end,doors[1]));continue;}
   Wall(p,"Low window sill",az,axis,k,end,y,.32f,paint);Wall(p,"Window lintel",az,axis,k,end,y+2.6f,1.1f,paint);Wall(p,"Window pier",az,axis,k,k+.3f,y+.32f,2.28f,paint);
  }
 }
 static void Hospital(Transform root)
 {
  var p=Group(root,"Hospital");
  for(int f=0;f<2;f++)
  {
   float y=f*4;var t=Group(p,f==0?"Ground floor":"Upper surgery floor");Floor(t,y);
   WindowSide(t,true,10,55,105,y,f==0?new[]{78f,82f}:Array.Empty<float>());WindowSide(t,true,42,55,105,y);
   WindowSide(t,false,55,10,42,y,f==0?new[]{23f,27f}:Array.Empty<float>());WindowSide(t,false,105,10,42,y,f==0?new[]{24f,27f}:Array.Empty<float>());
   foreach(float x in new[]{22f,30f})WindowSide(t,true,x,70,90,y);
   WindowSide(t,false,70,22,30,y,f==0?new[]{24f,28f}:Array.Empty<float>());WindowSide(t,false,90,22,30,y);
   // Corridor loop: west X18..22, east X30..34, south Z66..70, north Z90..94.
   Wall(t,"West room corridor wall",true,18,66,94,y,3.7f,paint,72,75,79,83,87,90);
   Wall(t,"East room corridor wall",true,34,64,96,y,3.7f,paint,68,71,78,81,88,91);
   Wall(t,"North wing corridor wall",false,94,10,36,y,3.7f,paint,19,22,29,32);
   Wall(t,"South wing corridor wall",false,66,10,34,y,3.7f,paint,f==0?new[]{18f,21f,23f,27f,28f,31f}:new[]{18f,21f,28f,31f});
   if(f==0)
   {
    Sign(t,"EMERGENCY",new(25,2.4f,65.8f),7);Sign(t,"RECEPTION",new(10.2f,2.4f,80),6,-90);
    foreach(float x in new[]{13f,17f,30f})Bed(t,new(x,0,59));
    foreach(float z in new[]{74f,84f,91f})Bed(t,new(38,0,z));
    Box(t,"Reception desk",new(13, .65f,84),new(4,1.3f,1.2f),wood);
    for(int i=0;i<5;i++)Box(t,"North supplies shelving",new(12+i*4,1.2f,102),new(2.5f,2.4f,1),steel);
    Box(t,"Imaging scanner base",new(38, .6f,68),new(2.5f,1.2f,3),white);Box(t,"Scanner arch",new(38,1.8f,69),new(3,2,1),white);Sign(t,"IMAGING",new(34.2f,2.4f,69),4,-90);
   }
   else
   {
    Wall(t,"Surgery west partition",false,79,10,18,y,3.7f,paint);Wall(t,"Surgery south partition",true,24,55,66,y,3.7f,paint);
    Bed(t,new(14,y,72),true);Bed(t,new(17,y,60),true);Bed(t,new(29,y,60),true);
    Sign(t,"SURGERY 1",new(18.2f,y+2.4f,73.5f),4,90);Sign(t,"SURGERY 2",new(19.5f,y+2.4f,66.2f),4,180);Sign(t,"SURGERY 3",new(29.5f,y+2.4f,66.2f),4,180);
    Box(t,"Nurses station desk",new(13,.65f+y,85),new(4,1.3f,1.2f),wood);
    foreach(float z in new[]{74f,84f,91f}){Bed(t,new(38,y,z));if(z<90)Wall(t,"Patient room divider",false,z+3,34,42,y,3.7f,paint);}
    foreach(float x in new[]{13f,17f,25f,30f})Bed(t,new(x,y,100));
   }
   foreach(float z in new[]{57f,98f})Stairs(t,z,y);
   foreach(float z in new[]{60f,80f,99f})foreach(float x in new[]{15f,32f})Lamp(t,new(x,y+3.25f,z));
   foreach(float z in new[]{57f,98f})Lamp(t,new(39,y+3.3f,z+2));
   foreach(float z in new[]{68f,92f})Lamp(t,new(25,y+3.2f,z));
  }
  var roof=Group(p,"Accessible roof");Floor(roof,8);
  foreach(float x in new[]{10f,42f})Wall(roof,"Outer parapet",true,x,55,105,8,1.1f,concrete);foreach(float z in new[]{55f,105f})Wall(roof,"Outer parapet",false,z,10,42,8,1.1f,concrete);
  foreach(float x in new[]{22f,30f})Wall(roof,"Courtyard guard",true,x,70,90,8,1.1f,concrete);foreach(float z in new[]{70f,90f})Wall(roof,"Courtyard guard",false,z,22,30,8,1.1f,concrete);
  foreach(float z in new[]{55f,96f}){Wall(roof,"Stair headhouse side",true,35.8f,z,z+8,8,3,paint,z+.2f,z+2);Wall(roof,"Stair headhouse side",true,41.2f,z,z+8,8,3,paint);foreach(float zz in new[]{z,z+8})Wall(roof,"Stair headhouse end",false,zz,35.8f,41.2f,8,3,paint);Slab(roof,"Stair headhouse roof",35.6f,z-.2f,5.8f,8.6f,11.2f,concrete);Lamp(roof,new(38.5f,10.7f,z+3));}
  foreach(float x in new[]{15f,20f})Cylinder(roof,"Water tank",new(x,10,99),new(3.5f,3.5f,3.5f),steel);
  foreach(float z in new[]{73f,81f,89f}){Box(roof,"HVAC housing",new(38,9,z),new(4,2,4),steel);Cylinder(roof,"HVAC fan",new(38,10.08f,z),new(2.5f,.1f,2.5f),gravel);for(int i=-2;i<=2;i++)Box(roof,"HVAC grille",new(38+i*.5f,10.18f,z),new(.06f,.06f,2.6f),white,false);}
  foreach(float z in new[]{74f,82f,88f}){Box(roof,"Vent duct",new(13,8.6f,z),new(1.5f,1.2f,3),steel);Box(roof,"Vent cap",new(13,9.3f,z),new(2,.25f,1.6f),white);}
  Box(roof,"South ventilation trunk",new(23,8.5f,58),new(14,1,1.5f),steel);foreach(float x in new[]{16f,30f})Cylinder(roof,"Vent stack",new(x,9.3f,60),new(.8f,2.6f,.8f),steel);
  foreach(var q in new[]{new Vector3(19,9,68),new(32,9,92)})Lamp(roof,q,true);
  Sign(p,"HOSPITAL",new(25,6.2f,54.7f),18,0,true);Sign(p,"EMERGENCY",new(25,2.8f,51),9);
  Slab(p,"ER canopy",20,50,11,5,3.3f,steel);foreach(float x in new[]{20.3f,30.7f})Box(p,"Canopy support",new(x,1.5f,50.3f),new(.3f,3,.3f),steel);Lamp(p,new(25,2.8f,52.5f),true);
  // Courtyard ground and planted corners remain open to the sky.
  Slab(p,"Courtyard paving",22,70,8,20,0,gravel);foreach(float z in new[]{73f,87f}){Box(p,"Courtyard planter",new(26,.3f,z),new(3,.6f,2),concrete);Tree(p,26,z, .6f);}
 }
 static void Cylinder(Transform p,string n,Vector3 pos,Vector3 size,Material mat)
 {
  var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new(size.x,size.y/2,size.z);g.layer=6;Object.DestroyImmediate(g.GetComponent<Collider>());g.AddComponent<BoxCollider>().size=new(1,2,1);
  var mesh=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);Unwrapping.GenerateSecondaryUVSet(mesh);g.GetComponent<MeshFilter>().sharedMesh=Save("Cylinder-"+Hash128.Compute(n+size),mesh);var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.25f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);
 }
 static void Tree(Transform p,float x,float z,float y=0){Box(p,"Tree trunk",new(x,y+1.3f,z),new(.5f,2.6f,.5f),wood);var t=Box(p,"Tree crown",new(x,y+3,z),new(2.8f,2.6f,2.8f),grass,false);t.transform.localRotation=Quaternion.Euler(0,(x+z)*11,0);}
 static void SmallBuilding(Transform root,string n,float x,float z,float w,float d)
 {
  var p=Group(root,n);Slab(p,"Floor",x,z,w,d,0,concrete);Wall(p,"Back",true,x,z,z+d,0,3.6f,paint);Wall(p,"Entry wall",true,x+w,z,z+d,0,3.6f,paint,z+d/2-2,z+d/2+2);foreach(float zz in new[]{z,z+d})WindowSide(p,false,zz,x,x+w,0);Slab(p,"Roof",x-.2f,z-.2f,w+.4f,d+.4f,3.9f,steel);Sign(p,n.ToUpperInvariant(),new(x+w+.2f,2.8f,z+d/2),8,90);
  Box(p,"Counter",new(x+4,.6f,z+d/2),new(1.3f,1.2f,6),wood);foreach(float zz in new[]{z+3,z+d-3}){Box(p,"Shelves",new(x+w/2,1,zz),new(w-4,2,1),steel);Lamp(p,new(x+w/2,3.2f,zz+1));}
 }
 static void Vehicle(Transform root,string n,Vector3 pos,float yaw,float roll,bool ambulance)
 {
  var p=Group(root,n);p.localPosition=pos;p.localRotation=Quaternion.Euler(0,yaw,roll);
  Box(p,"Chassis",new(0,.7f,0),new(2.7f,.45f,5.6f),steel);Box(p,"Body",new(0,1.5f,.7f),new(2.7f,1.7f,4),white);Box(p,"Cab",new(0,1.3f,-1.9f),new(2.5f,1.3f,1.2f),white);Box(p,"Windscreen",new(0,1.75f,-2.52f),new(2.1f,.65f,.06f),glass,false);
  foreach(float x in new[]{-1.38f,1.38f}){Box(p,"Emergency stripe",new(x,1.3f,.4f),new(.06f,.3f,4.9f),ambulance?red:paint,false);foreach(float z in new[]{-1.8f,1.8f}){var wheel=Box(p,"Tyre",new(x,.55f,z),new(.3f,1,1),gravel);wheel.transform.localRotation=Quaternion.Euler(45,0,0);}}
  if(ambulance){Box(p,"Light bar",new(0,2.48f,-1.6f),new(1.8f,.22f,.4f),red,false);foreach(float x in new[]{-1.4f,1.4f}){Box(p,"Medical cross horizontal",new(x,1.9f,.7f),new(.06f,.2f,.9f),red,false);Box(p,"Medical cross vertical",new(x,1.9f,.7f),new(.06f,.8f,.2f),red,false);}Sign(p,"AMBULANCE",new(0,1.8f,2.73f),2.3f,180);}
 }
 static void Site(Transform root)
 {
  for(int x=-48;x<48;x+=16)for(int z=0;z<160;z+=16)Slab(root,"Flat district ground",x,z,16,16,0,grass);
  Slab(root,"Rail ballast",-5,0,10,160,0,gravel);for(int z=1;z<160;z+=2)Box(root,"Sleeper",new(0,.015f,z),new(3.8f,.03f,.35f),wood,false);foreach(float x in new[]{-1.5f,1.5f})Box(root,"Rail",new(x,.06f,80),new(.12f,.12f,160),steel,false);
  foreach(float x in new[]{-43f,7f})Slab(root,"Sidewalk",x,6,36,148,.015f,concrete);
  foreach(float x in new[]{-44f,43f})Slab(root,"Service road",x,8,4,144,.025f,gravel);
  foreach(float z in new[]{45f,115f}){Slab(root,"Crossing pavement",-44,z,88,4,.02f,gravel);for(int x=-4;x<=4;x+=2)Box(root,"Crosswalk stripe",new(x,.04f,z+2),new(.6f,.02f,3.6f),white,false);}
  Hospital(root);SmallBuilding(root,"Clinic",-40,82,26,21);SmallBuilding(root,"Pharmacy",-40,57,26,19);SmallBuilding(root,"Laundry utilities",-40,126,24,20);
  foreach(float z in new[]{18f,28f,38f,126f,136f,146f})foreach(float x in new[]{14f,35f}){Box(root,"Parking stripe",new(x,.045f,z),new(5,.02f,.12f),white,false);}
  Vehicle(root,"Ambulance overturned on side",new(15,1.4f,36),12,90,true);Vehicle(root,"Ambulance upside down",new(33,2.5f,29),-20,180,true);Vehicle(root,"Ambulance crashed",new(38,0,42),-35,0,true);Box(root,"Crashed barrier",new(39, .5f,39),new(5,1,.4f),concrete);
  foreach(var q in new[]{new Vector3(15,0,130),new(35,0,140),new(-22,0,24)})Vehicle(root,"Parked vehicle",q,0,0,false);
  for(int z=12;z<153;z+=12)foreach(float x in new[]{-46f,46f})Tree(root,x,z);
  foreach(float z in new[]{16f,28f,40f}){Tree(root,-36,z);Box(root,"Park bench",new(-32,.5f,z),new(3,.4f,.8f),wood);}
  foreach(float z in new[]{20f,40f,120f,145f})foreach(float x in new[]{-10f,10f}){Box(root,"Streetlamp post",new(x,2.6f,z),new(.18f,5.2f,.18f),steel);Lamp(root,new(x,5.2f,z),true);}
  Box(root,"Utility generator",new(-23,1.1f,117),new(6,2.2f,3),steel);foreach(float x in new[]{-38f,-33f})Box(root,"Loading yard dumpster",new(x,.8f,119),new(3,1.6f,2),paint);
 }

 public static void Guard()=>RailCanyon.Guard();
 [MenuItem("Duck Rails/Authoring/Hospital/Install approved district")]
 public static void Install()
 {
  Guard();Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(LightingFolder);AssetDatabase.Refresh();boxes.Clear();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
  concrete=Mat("Raw concrete","concrete",new(.77f,.76f,.7f));wood=Mat("Construction timber","timber",new(.65f,.5f,.3f));steel=Mat("Scaffold steel","weathered paint",new(.42f,.49f,.49f));yellow=Mat("Crane ochre","weathered paint",new(.93f,.58f,.15f));gravel=Mat("Site gravel","gravel",new(.5f,.48f,.41f));
  paint=Mat("Hospital green","weathered paint",new(.55f,.68f,.60f));white=Mat("Medical ivory","concrete",new(.88f,.86f,.75f));red=Mat("Emergency red","weathered paint",new(.72f,.15f,.12f));glass=Mat("Dark glass","dark roofing",new(.13f,.24f,.28f));grass=Mat("District planting","grass",new(.48f,.57f,.37f));var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=new GameObject("Hospital District");var b=go.AddComponent<RailBlock>();b.id=Id;b.displayName="Hospital District";b.tags=new[]{"hospital","vertical","interior","straight"};b.length=160;b.entry=Group(go.transform,"Track Entry");b.exit=Group(go.transform,"Track Exit");b.exit.localPosition=new(0,0,160);b.stop=Group(go.transform,"Suggested Stop");b.stop.localPosition=new(0,0,80);b.track=Enumerable.Range(0,81).Select(i=>new Vector3(0,0,i*2)).ToArray();
  b.enemyBounds=new Bounds(new(0,5,80),new(84,14,70));b.enemySurfaceRailExclusion = default;var root=Group(go.transform,RootName);Site(root);
  b.variations=new RailSpawnSet[3];var coal=new[]{new Vector3(-20,.5f,65),new(-20,.5f,90),new(-33,.5f,75),new(20,.5f,80),new(32,4.5f,92),new(20,8.5f,68)};var enemies=new[]{new Vector3(20,.2f,68),new(32,.2f,84),new(20,4.2f,80),new(32,4.2f,92),new(20,8.2f,80),new(32,8.2f,68)};  for(int v=0;v<3;v++){var parent=Group(go.transform,"Spawn variation "+v);float dx=(v-1)*.5f;b.variations[v]=new RailSpawnSet{coal=coal.Select((p,i)=>RailCoalAuthoring.Create(parent,"CoalSpawner "+i,p+Vector3.right*dx)).ToArray(),enemies=enemies.Select((p,i)=>{var t=Group(parent,"Enemy "+i);t.localPosition=p+Vector3.right*dx;return t;}).ToArray()};}
  RailEconomyBuilder.AddGold(b);foreach(var v in b.variations){v.gold[0].localPosition=new(14,4.5f,76);v.gold[1].localPosition=new(25,8.5f,94);}
  var baker=Group(go.transform,"Authored crawlgraph settings").gameObject.AddComponent<CrawlGraphBaker>();baker.transform.localPosition=new(0,6,80);baker.size=new(80,16,96);baker.cellSize=1;baker.agentRadius=.25f;baker.allowCeilings=false;baker.minIslandNodes=10;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();baker.drawNodes=false;
  string graph="Assets/DuckRails/Blocks/"+Id+".bytes";if(!File.Exists(graph))File.WriteAllBytes(graph,new byte[8]);AssetDatabase.ImportAsset(graph);baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(graph);b.graph=baker.graphAsset;Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Hospital graph bake failed");
  var lighting=go.AddComponent<RailBakedBlockLighting>();lighting.lightingScene=LightingFolder+"/"+Id+"-Lighting.unity";PrefabUtility.SaveAsPrefabAssetAndConnect(go,PrefabPath,InteractionMode.AutomatedAction);
  var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();for(int z=4;z<160;z+=8)foreach(float x in new[]{-40f,-26f,-12f,0f,12f,26f,40f})foreach(float y in new[]{.7f,2.5f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));foreach(float y in new[]{4.7f,6.5f,8.7f,10.5f})foreach(float x in new[]{14f,20f,26f,32f,38f})for(float z=58;z<105;z+=6)if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));probes.probePositions=points.ToArray();
  RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new(.045f,.055f,.075f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new(.018f,.024f,.033f);
  var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingFolder+"/Lighting.lighting");if(!settings){settings=new LightingSettings{name="Hospital Lighting",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=4,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=32,indirectSampleCount=128,environmentSampleCount=64,maxBounces=4,indirectScale=1.5f};AssetDatabase.CreateAsset(settings,LightingFolder+"/Lighting.lighting");}Lightmapping.lightingSettings=settings;EditorSceneManager.SaveScene(scene,BakeScene);AssetDatabase.SaveAssets();Debug.Log("Hospital installed; graph baked. Check before registration and lighting.");
 }
 public static void Register(){Guard();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();var b=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<RailBlock>();if(!route.catalog.Any(x=>x.id==Id))route.catalog=route.catalog.Concat(new[]{b}).ToArray();EditorUtility.SetDirty(route);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(BakeScene);}
}
#endif
