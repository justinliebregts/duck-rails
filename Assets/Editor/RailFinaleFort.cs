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

public static class RailFinaleFort
{
 public const string Id="bridge-finale-v1", RootName="Containment fort and Happy Land", Folder="Assets/DuckRails/FinaleFort";
 public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", LightingFolder="Assets/DuckRails/Lighting/Blocks/"+Id, BakeScene=LightingFolder+"/"+Id+"-bake.unity";
 public const int LightCount=24;
 static Material concrete,steel,wood,yellow,gravel,grass,rock,cream,water,leaf,pink;
 static Mesh cube;
 static Material railOchre,railSteel,rubber,glass,olive;
 static Material[] carPaint;
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
 {Beam(p,"Edge handrail",a+Vector3.up*1.1f,b+Vector3.up*1.1f,.1f,railOchre);Beam(p,"Edge midrail",a+Vector3.up*.55f,b+Vector3.up*.55f,.08f,railSteel);int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3));for(int i=0;i<=n;i++)Beam(p,"Guard post",Vector3.Lerp(a,b,(float)i/n),Vector3.Lerp(a,b,(float)i/n)+Vector3.up*1.1f,.09f,railSteel);}

 static void Steps(Transform p,Vector3 a,Vector3 b,float width)
 {
  int count=Mathf.RoundToInt((b.y-a.y)/.2f);var dir=new Vector3(b.x-a.x,0,b.z-a.z);float len=dir.magnitude;dir.Normalize();
  for(int i=0;i<count;i++){float y=a.y+(i+1)*.2f;var pos=a+dir*((i+.5f)*len/count);pos.y=y/2;var g=Box(p,"Concrete stair tread",pos,new(width,y,len/count+.01f),concrete);g.transform.localRotation=Quaternion.LookRotation(dir);}
 }
 static void Platform(Transform p,string name,float x,float z,float w,float d,float y)
 {
  Slab(p,name,x-w/2,z-d/2,w,d,y,concrete);
  if(name.Contains("South bastion")||name.Contains("Side patrol")||name.Contains("Tower observation"))
   Box(p,"Solid concrete bastion base",new(x,(y-.5f)/2,z),new(w,y-.5f,d),concrete);
  else if(name.Contains("North bastion")){
   Box(p,"Bunker rear wall",new(x,(y-.5f)/2,z+d/2-1),new(w,y-.5f,2),concrete);
   foreach(float xx in new[]{x-w/2+1,x+w/2-1})Box(p,"Bunker side wall",new(xx,(y-.5f)/2,z),new(2,y-.5f,d),concrete);
   foreach(float xx in new[]{x-w/2+3,x+w/2-3})Box(p,"Bunker entrance pier",new(xx,(y-.5f)/2,z-d/2+1),new(6,y-.5f,2),concrete);
  }
  else if(y>2.5f&&!name.Contains("button balcony"))foreach(float xx in new[]{x-w/2+1,x+w/2-1})foreach(float zz in new[]{z-d/2+.65f,z+d/2-.65f})Box(p,"Support pillar",new(xx,(y-.5f)/2,zz),new(2,y-.5f,1.3f),concrete);
 }
 static Material Pixel(string name,Color a,Color b)
 {
  string tp=Folder+"/"+name+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
  if(!tex){tex=new Texture2D(32,32,TextureFormat.RGB24,true);tex.name=name;tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Repeat;var rng=new System.Random(73);for(int y=0;y<32;y++)for(int x=0;x<32;x++)tex.SetPixel(x,y,Color.Lerp(a,b,(float)rng.NextDouble()));tex.Apply();AssetDatabase.CreateAsset(tex,tp);}
  for(int y=0;y<32;y++)for(int x=0;x<32;x++){float t=.35f+((x/4*17+y/4*31)%11)*.03f;tex.SetPixel(x,y,Color.Lerp(a,b,t));}tex.Apply();EditorUtility.SetDirty(tex);
  string mp=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,mp);}m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",0);return m;
 }
 static float Hill(float x,float z)
 {
  float side=Mathf.SmoothStep(0,1,Mathf.InverseLerp(27,53,Mathf.Abs(x)))*(18+5*Mathf.Sin(z*.065f)+3*Mathf.Cos(z*.14f));
  float end=Mathf.SmoothStep(0,1,Mathf.InverseLerp(320,360,z))*(27+4*Mathf.Sin(x*.13f));return Mathf.Max(side,end);
 }
 static void Terrain(Transform p)
 {
  var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
  for(int z=160;z<380;z+=5)for(int x=-65;x<65;x+=5){int n=verts.Count;foreach(var q in new[]{new Vector3(x,Hill(x,z),z),new(x,Hill(x,z+5),z+5),new(x+5,Hill(x+5,z+5),z+5),new(x+5,Hill(x+5,z),z)}){verts.Add(q);uv.Add(new(q.x/3,q.z/3));}tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
  var m=new Mesh();m.SetVertices(verts);m.SetUVs(0,uv);m.SetTriangles(tris,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);MeshGo(p,"Continuous valley hills and far ridge",Save("ValleyTerrain",m),grass);
 }
 static void Tree(Transform p,float x,float z,bool blossom=false)
 {float y=z>=160?Hill(x,z):0;Box(p,"Tree trunk",new(x,y+1.5f,z),new(.55f,3,.55f),wood);
  string path=Folder+"/Meshes/Faceted crown.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(!mesh){var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;float lo=ring==0?.15f:1,hi=ring==2?.1f:1;var points=new[]{new Vector3(Mathf.Cos(a)*lo,ring-1.5f,Mathf.Sin(a)*lo),new(Mathf.Cos(b)*lo,ring-1.5f,Mathf.Sin(b)*lo),new(Mathf.Cos(b)*hi,ring-.5f,Mathf.Sin(b)*hi),new(Mathf.Cos(a)*hi,ring-.5f,Mathf.Sin(a)*hi)};int n=v.Count;v.AddRange(points);uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});tri.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);mesh=Save("Faceted crown",mesh);}
  var g=MeshGo(p,"Faceted crown",mesh,blossom?pink:leaf);g.transform.localPosition=new(x,y+4,z);g.transform.localScale=new(2.5f,1.4f,2.5f);
 }
 static void House(Transform p,float x,float z)
 {Box(p,"Village cottage",new(x,2,z),new(8,4,7),cream);foreach(float dx in new[]{-2f,2f})Box(p,"Blue window",new(x+dx,2.3f,z-3.53f),new(1.3f,1.4f,.08f),steel,false);Box(p,"Cottage door",new(x,.95f,z-3.55f),new(1.2f,1.9f,.12f),wood);foreach(float dx in new[]{-2.2f,2.2f}){var roof=Box(p,"Pitched cottage roof",new(x+dx,4.7f,z),new(4.8f,.3f,8),steel);roof.transform.localRotation=Quaternion.Euler(0,0,dx<0?22:-22);}}
 static void LookoutTower(Transform p,float sign)
 {
  float x=sign*34;var t=Group(p,sign<0?"Northwest lookout tower":"Northeast lookout tower");
  Platform(t,"Tower observation deck +8",x,74,8,8,8);
  // Four continuous corner piers establish the tower silhouette; all stay outside the railway.
  foreach(float dx in new[]{-3.65f,3.65f})foreach(float z in new[]{70.35f,77.65f})Box(t,"Tower concrete pier",new(x+dx,5.75f,z),new(.65f,11.5f,.65f),concrete);
  Box(t,"Concrete lookout roof",new(x,11.65f,74),new(8.8f,.7f,8.8f),concrete);
  foreach(float z in new[]{70f,78f}){Rail(t,new(x-4,8,z),new(x+4,8,z));}
  float outside=x+sign*4,inside=x-sign*4;
  Rail(t,new(outside,8,70),new(outside,8,78));
  // Leave the central 3 m opening toward the connecting walkway unobstructed.
  Rail(t,new(inside,8,70),new(inside,8,72.5f));Rail(t,new(inside,8,75.5f),new(inside,8,78));
  Slab(p,"Tower approach deck",Mathf.Min(sign*28,sign*30),72.5f,2,3,8,concrete);
  Rail(p,new(sign*28,8,72.5f),new(sign*30,8,72.5f));Rail(p,new(sign*28,8,75.5f),new(sign*30,8,75.5f));
 }
 static void Vehicle(Transform root,Vector3 pos,float yaw,bool truck,bool overturned,int color)
 {
  var p=Group(root,(truck?"Abandoned truck":"Abandoned car")+(overturned?" overturned":" jammed"));var paint=carPaint[color%carPaint.Length];float length=truck?6.6f:4.6f;
  Box(p,"Vehicle chassis",new(0,.7f,0),new(2.3f,.45f,length),rubber);
  Box(p,"Painted body",new(0,1,0),new(2.4f,.6f,length),paint);
  Box(p,"Cabin",new(0,1.65f,truck?1.9f:-.2f),new(2.1f,.9f,truck?2:2.4f),paint);
  Box(p,"Windshield",new(0,1.7f,truck?2.92f:1.03f),new(1.85f,.65f,.05f),glass,false);
  foreach(float x in new[]{-1.07f,1.07f})Box(p,"Side glazing",new(x,1.7f,truck?1.9f:-.2f),new(.05f,.65f,truck?1.5f:1.9f),glass,false);
  if(truck){Box(p,"Cargo bed",new(0,1.42f,-1.2f),new(2.35f,.18f,3.9f),railSteel);foreach(float x in new[]{-1.15f,1.15f})Box(p,"Cargo bed side",new(x,1.75f,-1.2f),new(.15f,.65f,3.9f),paint);}
  foreach(float x in new[]{-1.2f,1.2f})foreach(float z in new[]{-length*.32f,length*.32f}){var w=Box(p,"Tyre",new(x,.5f,z),new(.35f,.8f,.8f),rubber);w.transform.localRotation=Quaternion.Euler(45,0,0);}
  foreach(float z in new[]{-length/2,length/2}){Box(p,"Bumper",new(0,.75f,z),new(2.5f,.24f,.2f),railSteel);foreach(float x in new[]{-.8f,.8f})Box(p,"Lamp",new(x,1.1f,z+.07f),new(.4f,.23f,.1f),z>0?cream:carPaint[0],false);}
  p.localPosition=pos;p.localRotation=Quaternion.Euler(0,yaw,overturned?180:0);
  float min=p.GetComponentsInChildren<Renderer>().Min(v=>v.bounds.min.y);p.position-=Vector3.up*min;
 }
 static void Traffic(Transform p)
 {
  var root=Group(p,"Abandoned approach traffic jam");int index=0;
  foreach(float sign in new[]{-1f,1f})for(int row=0;row<4;row++)for(int lane=0;lane<5;lane++){
   if(row==3&&lane==1)continue;
   if(sign<0&&row==3&&lane==4)continue;
   float x=sign*(10+lane*6)+(row%2==0?1:-1),z=6+row*6.3f+(lane%2)*.7f;
   bool truck=(row+lane)%4==0,flip=!truck&&(row+lane)%5==2;
   Vehicle(root,new(x,0,z),sign*(row%2==0?12:-19)+(lane==1?28:0),truck,flip,index++);
  }
  var t=Group(root,"Abandoned containment tank");t.localPosition=new(-35,0,24);t.localRotation=Quaternion.Euler(0,24,0);
  foreach(float x in new[]{-1.8f,1.8f}){Box(t,"Tank track",new(x,.65f,0),new(.85f,1.3f,6.4f),rubber);for(int i=0;i<5;i++)Box(t,"Track road wheel",new(x+(x<0?-.44f:.44f),.65f,-2.4f+i*1.2f),new(.12f,.7f,.7f),railSteel);}
  Box(t,"Armored hull",new(0,1.3f,0),new(3.5f,1.15f,5.7f),olive);Box(t,"Tank turret",new(0,2.2f,-.3f),new(2.6f,1.1f,2.7f),olive);Box(t,"Hatch",new(0,2.83f,-.4f),new(1,.16f,1),railSteel);
  Beam(t,"Static tank barrel",new(0,2.4f,.8f),new(0,2.4f,5.7f),.27f,olive);Box(t,"Muzzle",new(0,2.4f,5.7f),new(.45f,.42f,.55f),rubber);
 }
 static void RecoveryFlight(Transform p,Vector3 a,Vector3 b)
 {
  // Solid steps start at the river bed, including when their world elevation is negative.
  const float bed=-12f,width=3f;int count=Mathf.CeilToInt((b.y-a.y)/.2f);
  var dir=new Vector3(b.x-a.x,0,b.z-a.z).normalized;float run=Vector3.Distance(new(a.x,0,a.z),new(b.x,0,b.z))/count;
  var right=Vector3.Cross(Vector3.up,dir);
  for(int i=0;i<count;i++){
   float top=Mathf.Lerp(a.y,b.y,(i+1f)/count);var pos=a+dir*((i+.5f)*run);pos.y=(top+bed)/2;
   var step=Box(p,"River recovery tread",pos,new(width,top-bed,run+.01f),concrete);step.transform.localRotation=Quaternion.LookRotation(dir);
  }
  // Posts terminate on actual treads, not an imaginary slope between the steps.
  foreach(float side in new[]{-1f,1f}){
   Vector3 last=Vector3.zero;for(int i=0;i<count;i+=4){var foot=a+dir*((i+.5f)*run)+right*(side*1.42f);foot.y=Mathf.Lerp(a.y,b.y,(i+1f)/count);
    Beam(p,"Recovery guard post",foot,foot+Vector3.up*1.1f,.09f,railSteel);
    if(i>0){Beam(p,"Recovery handrail",last+Vector3.up*1.1f,foot+Vector3.up*1.1f,.1f,railOchre);Beam(p,"Recovery midrail",last+Vector3.up*.55f,foot+Vector3.up*.55f,.08f,railSteel);}last=foot;
   }
  }
 }
 static void RiverAndBridge(Transform root,Transform art)
 {
  var bank=Group(art,"River retaining walls and fortress recovery");
  // Both bank faces meet the existing floor at -11.5 m and terrain at zero.
  Box(bank,"Fortress river retaining wall",new(0,-6.015f,79),new(100,11.97f,2),concrete);
  Box(bank,"Tunnel bank rock foundation",new(0,-6.015f,115),new(100,11.97f,10),rock);
  for(int x=-48;x<=48;x+=8){
   if(Mathf.Abs(x)<6)continue; // Leave the moving span's entire hinge/landing pocket clear.
   Box(bank,"Fortress bank buttress",new(x,-6,80.35f),new(1.3f,12,.7f),concrete);
   Box(bank,"Far bank stone buttress",new(x,-6,109.6f),new(2.2f,12,.8f),rock);
  }
  foreach(float sign in new[]{-1f,1f}){
   // High end walls close the ground-plane edges, with no low ledge across the water.
   Box(bank,"River end rock wall",new(sign*50,-3,95),new(4,18,30),rock);
   var stairs=Group(bank,sign<0?"West fortress recovery stairs":"East fortress recovery stairs");
   RecoveryFlight(stairs,new(sign*16,-11.5f,88.5f),new(sign*35.5f,-5.75f,88.5f));
   RecoveryFlight(stairs,new(sign*35.5f,-5.75f,83),new(sign*16,0,83));
   Box(stairs,"Recovery turn landing",new(sign*37.25f,-8.875f,85.75f),new(3.5f,6.25f,8.5f),concrete);
   Box(stairs,"Recovery upper landing",new(sign*14.25f,-6,81.5f),new(3.5f,12,6),concrete);
   Rail(stairs,new(sign*39,-5.75f,81.5f),new(sign*39,-5.75f,90));
   Rail(stairs,new(sign*35.5f,-5.75f,90),new(sign*39,-5.75f,90));
   Rail(stairs,new(sign*35.5f,-5.75f,81.5f),new(sign*39,-5.75f,81.5f));
   Rail(stairs,new(sign*12.5f,0,78.5f),new(sign*12.5f,0,84.5f));
   Rail(stairs,new(sign*12.5f,0,84.5f),new(sign*16,0,84.5f));
   // Stationary abutments/bearings remain outside the full moving train envelope.
   Box(bank,"Bridge concrete abutment",new(sign*4.7f,-2,79),new(1.6f,4,2),concrete);
   Box(bank,"Bridge hinge bearing",new(sign*4.3f,.15f,80),new(.9f,.9f,1.3f),railSteel);
   Box(bank,"Bridge far landing bearing",new(sign*4.3f,-.45f,110.2f),new(.9f,.9f,1),railSteel);
  }
  var river=root.Find("Ravine");river.GetComponent<Renderer>().sharedMaterial=Pixel("River deep blue",new(.10f,.24f,.28f),new(.18f,.38f,.41f));
  // The shallow visible surface retains the existing solid river collision/recovery behavior.
  for(int i=0;i<28;i++){float x=-43+(i*13%86),z=92+(i*7%15);Box(bank,"Pixel water ripple",new(x,-11.485f,z),new(2.4f+(i%3),.015f,.12f),water,false);}
  var hinge=root.Find("Bridge hinge");
  foreach(Transform t in hinge.Cast<Transform>().ToArray())if(t.name=="Bridge deck"||t.name=="Bridge safety rail"||t.name=="Authored steel drawbridge")Object.DestroyImmediate(t.gameObject);
  var span=Group(hinge,"Authored steel drawbridge");
  Box(span,"Bridge deck",new(0,-.18f,15),new(7.4f,.36f,30),railSteel);
  // Panels, fasteners and sleepers are visual-only; the continuous deck collider is smooth.
  for(int z=1;z<30;z+=2){
   Box(span,"Bridge steel sleeper",new(0,.025f,z),new(3.9f,.05f,.3f),rubber,false);
   foreach(float side in new[]{-1f,1f})Box(span,"Service walkway panel",new(side*2.65f,.012f,z),new(1.6f,.024f,1.9f),gravel,false);
  }
  foreach(float x in new[]{-1.5f,1.5f})Box(span,"Moving railway rail",new(x,.06f,15),new(.15f,.12f,30),railSteel,false);
  foreach(float side in new[]{-1f,1f}){
   float x=side*3.5f;
   Box(span,"Upper girder flange",new(x,-.32f,15),new(.32f,.25f,30),steel);
   Box(span,"Lower girder flange",new(x,-1.6f,15),new(.36f,.25f,30),steel);
   for(int z=0;z<30;z+=3){Beam(span,"Warren girder diagonal",new(x,z%6==0?-.4f:-1.5f,z),new(x,z%6==0?-1.5f:-.4f,z+3),.2f,steel);Box(span,"Girder connection plate",new(x,-.95f,z+.1f),new(.38f,1.4f,.25f),steel);}
   Rail(span,new(side*3.6f,0,.2f),new(side*3.6f,0,29.8f));
   for(int z=1;z<30;z+=4)Box(span,"Ochre deck edge marker",new(side*3.32f,.015f,z),new(.22f,.03f,1),railOchre,false);
  }
  for(int z=0;z<=30;z+=3)Box(span,"Underslung cross beam",new(0,-.6f,Mathf.Clamp(z,.2f,29.8f)),new(7.2f,.5f,.25f),steel);
  foreach(var t in span.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
  foreach(var renderer in span.GetComponentsInChildren<MeshRenderer>())renderer.receiveGI=ReceiveGI.LightProbes;
 }
 static void Art(Transform r)
 {
  var old=r.Find(RootName);if(old)Object.DestroyImmediate(old.gameObject);var p=Group(r,RootName);
  var island=r.Find("Bridge control island");if(island)Object.DestroyImmediate(island.gameObject);
  r.Find("Lower bridge button").localPosition=new(0,9.05f,71.1f);r.Find("LOWER BRIDGE").localPosition=new(0,10.5f,71.8f);
  r.Find("Far bank").GetComponent<Renderer>().sharedMaterial=grass;
  r.Find("Finale approach").GetComponent<Renderer>().sharedMaterial=gravel;
  foreach(Transform t in r){if(t.name=="Tunnel roof"){t.localPosition=new(0,7.5f,140);t.GetComponent<Renderer>().sharedMaterial=rock;}if(t.name=="Tunnel wall"){t.localPosition=new(t.localPosition.x,3.5f,140);t.localScale=new(1,7,40);t.GetComponent<Renderer>().sharedMaterial=rock;}}
  foreach(float sign in new[]{-1f,1f}){
   float x=sign*22;Platform(p,"South bastion +2",x,42,20,12,2);Platform(p,"North bastion +4",x,68,20,16,4);
   Platform(p,"Side patrol deck +4",sign*33,54,4,42,4);
   Steps(p,new(sign*17,0,28),new(sign*17,2,36),3);Steps(p,new(sign*27,2,48),new(sign*27,4,56),3);Platform(p,"Stair landing +4",sign*27,58,4,4,4);
   // Lookout/catwalk level has an independent stair from the +4 roof.
   Steps(p,new(sign*26,4,60),new(sign*26,8,72),3);Platform(p,"Lookout deck +8",sign*24,74,8,4,8);
   Platform(p,"Catwalk access +8",sign*17,68,6,4,8);Platform(p,"Lookout connection",sign*20,71,4,10,8);
   Box(p,"Containment wall",new(sign*39,3,54),new(1,6,48),concrete);
   foreach(float xx in new[]{sign*10,sign*32})Box(p,"Breached entry wall",new(xx,2,31),new(12,4,1),concrete);
   Rail(p,new(sign*20,8,76),new(sign*28,8,76));
   LookoutTower(p,sign);
   Rail(p,new(sign*35,4,34),new(sign*35,4,75));
   for(int i=0;i<5;i++){float h=.5f+(i%3)*.6f;Box(p,"Optional jump plinth",new(sign*(10+(i%2)*5),h/2,44+i*3),new(2.5f,h,2.5f),concrete);}
   foreach(float z in new[]{36f,72f}){Box(p,"Security lamp post",new(sign*35,7,z),new(.2f,6,.2f),steel);Box(p,"Floodlight",new(sign*35,10,z),new(1.8f,.7f,.3f),yellow,false);}
   Rail(p,new(sign*18,8,70),new(sign*18,8,76));Rail(p,new(sign*18,8,76),new(sign*20,8,76));
  }
  Platform(p,"Cross-track catwalk +8",0,68,34,3,8);
  Traffic(p);
  RiverAndBridge(r,p);
  Platform(p,"Off-path button balcony",0,70.75f,4,2.5f,8);
  Rail(p,new(-17,8,66.5f),new(17,8,66.5f));Rail(p,new(-17,8,69.5f),new(-2,8,69.5f));Rail(p,new(2,8,69.5f),new(17,8,69.5f));Rail(p,new(-2,8,69.5f),new(-2,8,72));Rail(p,new(2,8,69.5f),new(2,8,72));Rail(p,new(-2,8,72),new(2,8,72));
  // Remove automatic centre supports from the crossing and balcony: beams span from side abutments.
  foreach(Transform t in p.Cast<Transform>().ToArray())if(t.name=="Support pillar"&&Mathf.Abs(t.localPosition.x)<7)Object.DestroyImmediate(t.gameObject);
  for(int z=120;z<160;z+=10)foreach(float x in new[]{-27f,27f})Box(p,"Tunnel ridge shoulder",new(x,10,z+5),new(40,20,10),rock);
  Box(p,"Tunnel ridge crown",new(0,12,140),new(14,8,40),rock);
  var happy=Group(p,"Happy Land");Terrain(happy);
  for(int z=166;z<304;z+=12)foreach(float sign in new[]{-1f,1f})Tree(happy,sign*(18+(z%4)*3),z,z%3==1);
  for(int z=204;z<250;z+=12)foreach(float x in new[]{-12f,-23f})Tree(happy,x,z);
  foreach(float x in new[]{-18f,18f})foreach(float z in new[]{265f,289f})House(happy,x,z);
  for(int z=197;z<260;z+=4){float x=17+4*Mathf.Sin(z*.09f);Box(happy,"Stream ribbon",new(x,.035f,z),new(4,.05f,5),water,false);}
  for(int z=170;z<300;z+=7)foreach(float x in new[]{-9f,9f})for(int i=0;i<3;i++)Box(happy,"Flower patch",new(x+i*.5f,.16f,z+i*.4f),new(.3f,.3f,.3f),i==0?yellow:pink,false);
  for(int z=112;z<310;z+=2)Box(p,"Victory track sleeper",new(0,.015f,z),new(3.8f,.03f,.35f),wood,false);
 }
 [MenuItem("Duck Rails/Authoring/Finale/Install approved fortress and valley")]
 public static void Install()
 {
  RailCanyon.Guard();Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();boxes.Clear();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
  concrete=Pixel("Pale fortress concrete",new(.44f,.45f,.43f),new(.64f,.64f,.59f));steel=Pixel("Fort teal steel",new(.18f,.34f,.33f),new(.32f,.47f,.44f));wood=Mat("Timber","timber",new(.7f,.55f,.35f));yellow=Mat("Safety ochre","weathered paint",new(1,.75f,.2f));rock=Mat("Ridge rock","concrete",new(.55f,.57f,.52f));
  railOchre=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/SkyscraperWorks/Crane ochre.mat");railSteel=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/SkyscraperWorks/Scaffold steel.mat");
  if(!railOchre||!railSteel)throw new Exception("Missing approved Skyscraper Works railing materials");
  rubber=Pixel("Vehicle rubber",new(.06f,.07f,.07f),new(.13f,.14f,.14f));glass=Pixel("Vehicle glass",new(.10f,.19f,.23f),new(.22f,.32f,.36f));olive=Pixel("Tank olive",new(.22f,.27f,.16f),new(.37f,.40f,.25f));
  carPaint=new[]{Pixel("Car faded red",new(.35f,.15f,.12f),new(.58f,.28f,.22f)),Pixel("Car blue",new(.15f,.24f,.32f),new(.3f,.43f,.51f)),cream=Pixel("Car ivory",new(.55f,.53f,.43f),new(.78f,.75f,.63f)),Pixel("Car grey",new(.27f,.29f,.29f),new(.46f,.48f,.47f))};
  gravel=Pixel("Fort yard concrete",new(.25f,.27f,.26f),new(.4f,.42f,.39f));
  grass=Pixel("Dawn grass",new(.35f,.55f,.16f),new(.65f,.76f,.3f));leaf=Pixel("Leaf green",new(.3f,.5f,.1f),new(.57f,.68f,.2f));pink=Pixel("Blossom pink",new(.86f,.45f,.65f),new(1,.8f,.87f));cream=Pixel("Cottage cream",new(.78f,.73f,.56f),new(.97f,.91f,.73f));water=Pixel("Stream blue",new(.14f,.48f,.64f),new(.38f,.73f,.86f));
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));Art(go.transform);
  var b=go.GetComponent<RailBlock>();b.enemyBounds=new Bounds(new(0,5,56),new(78,14,46));var seeds=b.variations[0].enemies;for(int i=0;i<seeds.Length;i++)seeds[i].localPosition=new(i%2==0?-22:22,.05f,32+(i/2)*9);
  var baker=go.GetComponentInChildren<CrawlGraphBaker>();baker.transform.localPosition=new(0,6,56);baker.size=new(80,16,80);baker.seeds=seeds;baker.surfaceMask=1<<6;baker.minIslandNodes=10;Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Finale graph bake failed");b.graph=baker.graphAsset;
  PrefabUtility.ApplyPrefabInstance(go,InteractionMode.AutomatedAction);
  var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var pts=new List<Vector3>();for(int z=4;z<310;z+=8)foreach(float x in new[]{-30f,-15f,0f,15f,30f})foreach(float y in new[]{.7f,3f,8.7f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))pts.Add(new(x,y,z));
  foreach(float z in new[]{84f,95f,106f})foreach(float x in new[]{-42f,-24f,-10f,0f,10f,24f,42f})foreach(float y in new[]{-10f,-5f,-1f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))pts.Add(new(x,y,z));probes.probePositions=pts.ToArray();
  Lightmapping.lightingSettings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingFolder+"/Lighting.lighting");Lightmapping.lightingSettings.lightmapResolution=2;EditorUtility.SetDirty(Lightmapping.lightingSettings);
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new(.18f,.2f,.23f);RenderSettings.skybox=null;
  EditorSceneManager.SaveScene(scene,BakeScene);AssetDatabase.SaveAssets();Debug.Log("Fortress and valley installed; offline graph baked.");
 }
 public static void SyncScene()
 {
  RailCanyon.Guard();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();var old=route.finaleRoot;var pos=old.position;var parent=old.parent;var e=route.expedition;var previous=e.bridgeLabel;
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));go.transform.SetParent(parent,false);go.transform.position=pos;route.finaleRoot=go.transform;route.finale=go.GetComponent<RailBlock>();e.bridgeButton=go.transform.Find("Lower bridge button");e.bridgeDeck=go.transform.Find("Bridge hinge");e.bridgeLabel=go.transform.Find("LOWER BRIDGE").GetComponent<TMPro.TMP_Text>();
  if(e.runningLabels!=null)e.runningLabels=e.runningLabels.Select(t=>t==previous?e.bridgeLabel:t!=null&&t.transform.IsChildOf(old)?go.GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(q=>q.name==t.name):t).ToArray();Object.DestroyImmediate(old.gameObject);EditorUtility.SetDirty(route);EditorUtility.SetDirty(e);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
}
#endif
