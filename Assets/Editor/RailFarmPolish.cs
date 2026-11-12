#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Audio;
using Object = UnityEngine.Object;

/// <summary>One-time, scoped installation of the November 11 screenshot review. Never rebuilds the farm.</summary>
public static partial class RailFarmPolish
{
    public const string Folder = "Assets/DuckRails/FarmSilos/Polish";
    public const string Evidence = "docs/validation/farm-polish";
    static Mesh cube;
    static Material wood, white, red, steel, dark, hay, stone, grass, glow, cloth;
    static int meshId;
    static Transform group;
    static readonly Dictionary<Material,List<CombineInstance>> pieces = new();
    static Transform Group(Transform p,string n) { var g=new GameObject(n);g.layer=6;g.transform.SetParent(p,false);return g.transform; }
    static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/"+n+".mat");
    static void Init()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();meshId=0;
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/farm-silos-v1/Cube Block UV.asset");
        wood=Mat("Old timber");white=Mat("White farmhouse siding");red=Mat("Barn red timber");steel=Mat("Silo metal");dark=Mat("Dark shingles");hay=Mat("Golden hay");stone=Mat("Foundation stone");grass=Mat("Pasture");
        glow=NewMat("Warm lantern glass",steel,new Color(1,.68f,.28f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.48f,.13f)*2);glow.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        cloth=NewMat("Faded green upholstery",white,new Color(.33f,.42f,.25f));
    }
    static Material NewMat(string n,Material source,Color c)
    {var path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);EditorUtility.SetDirty(m);return m;}
    static Mesh SaveMesh(string n,Mesh m)
    {
        m.name=n;var path=Folder+"/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old)
        {
            // Explicit setters also refresh live GPU buffers after a mesh correction.
            old.Clear();old.indexFormat=m.indexFormat;old.vertices=m.vertices;old.normals=m.normals;old.uv=m.uv;old.uv2=m.uv2;old.triangles=m.triangles;old.bounds=m.bounds;old.UploadMeshData(false);
            Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;
        }
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static void UV(Mesh m)
    {var v=m.vertices;var norm=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=(Mathf.Abs(norm[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(norm[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y))/3;m.uv=uv;Unwrapping.GenerateSecondaryUVSet(m);}
    static void Start(Transform p,string n){group=Group(p,n);pieces.Clear();}
    static void Box(string n,Vector3 pos,Vector3 size,Material mat,bool collision=true,Quaternion? rot=null)
    {
        var q=rot??Quaternion.identity;if(!pieces.TryGetValue(mat,out var list)){list=new();pieces.Add(mat,list);}list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(pos,q,size)});
        if(collision){var g=Group(group,n);g.localPosition=pos;g.localRotation=q;g.gameObject.AddComponent<BoxCollider>().size=size;}
    }
    static void Beam(string n,Vector3 a,Vector3 b,float width,Material m,bool collision=true)=>Box(n,(a+b)/2,new Vector3(width,(b-a).magnitude,width),m,collision,Quaternion.FromToRotation(Vector3.up,b-a));
    static void Finish()
    {
        foreach(var kv in pieces){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(kv.Value.ToArray(),true,true);UV(m);MeshObject(group,group.name+" "+kv.Key.name,SaveMesh("Detail-"+(meshId++),m),kv.Key,false);}pieces.Clear();
    }
    static GameObject MeshObject(Transform p,string name,Mesh m,Material mat,bool collision)
    {
        var t=Group(p,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=m;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.35f;r.lightProbeUsage=LightProbeUsage.BlendProbes;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
        if(collision)t.gameObject.AddComponent<MeshCollider>().sharedMesh=m;return t.gameObject;
    }
    static void Delete(Transform t){if(t)Object.DestroyImmediate(t.gameObject);}
    public static void Install()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Save prefab changes first");
        if(stage!=null)StageUtility.GoToMainStage();
        Init();var root=PrefabUtility.LoadPrefabContents(RailFarmSilos.PrefabPath);
        try
        {
            var env=root.transform.Find("Env");if(env.Find("Farm polish November 11"))throw new Exception("Already installed; edit saved content instead of resetting it");
            var b=root.GetComponent<RailBlock>();var track=b.track.ToArray();var markerPositions=RailSpawnPool.Candidates(b,RailSpawnKind.Enemy).Select(t=>t.localPosition).ToArray();
            var farm=env.Find(RailFarmSilos.RootName);var house=farm.Find("Farmhouse");var added=Group(env,"Farm polish November 11");
            House(house);Sheds(env);Barn(farm.Find("Barn and silos"));Silo(farm.Find("Barn and silos"));Exterior(added);Loot(b,house);
            if(!track.SequenceEqual(b.track)||!markerPositions.SequenceEqual(RailSpawnPool.Candidates(b,RailSpawnKind.Enemy).Select(t=>t.localPosition)))throw new Exception("Track/enemies changed");
            EditorUtility.SetDirty(b);PrefabUtility.SaveAsPrefabAsset(root,RailFarmSilos.PrefabPath);AssetDatabase.SaveAssets();
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        PrefabStageUtility.OpenPrefab(RailFarmSilos.PrefabPath);
        Debug.Log("Farm screenshot polish installed; navigation and lighting need rebake.");
    }
    static void Resize(Transform t,Vector3 p,Vector3 s){t.localPosition=p;t.localScale=s;}
    static void House(Transform h)
    {
        // Wall tops from the floor below were exactly coplanar with the floor above.
        foreach(var level in new[]{"Basement","Ground floor","Upper floor"})
        {
            var f=h.Find(level);float ceiling=level=="Basement"?0:level=="Ground floor"?3.2f:6.4f;
            foreach(Transform t in f)
            {if(!t.GetComponent<BoxCollider>()||t.localScale.y<.4f||Mathf.Abs(t.localPosition.y+t.localScale.y/2-ceiling)>.01f)continue;var s=t.localScale;s.y-=.2f;var p=t.localPosition;p.y-=.1f;Resize(t,p,s);}
        }
        foreach(var name in new[]{"Ground floor","Upper floor","Attic"})
        {
            var f=h.Find(name);float y=name=="Ground floor"?0:name=="Upper floor"?3.2f:6.4f;
            Delete(f.Find("Floor east"));
            Resize(f.Find("Stair bottom landing"),new Vector3(31.75f,y-.1f,129),new Vector3(4.5f,.2f,2));
            Resize(f.Find("Stair top landing"),new Vector3(31.75f,y-.1f,141.6f),new Vector3(4.5f,.2f,8.8f));
            Delete(f.Find("Stair flight"));Start(f,"Broad nine-step stair");
            Steps(29.5f,130,4.39f,7.2f,y-3.2f,3.2f,9);Finish();
        }
        // Keep the exterior cellar access, but close the accidental ground-floor doorway above it.
        Start(h.Find("Ground floor"),"Rear wall closure");Box("Rear wall",new Vector3(30.75f,1.35f,146),new Vector3(3.5f,2.7f,.22f),white);Finish();
        var ext=h.Find("Exterior cellar stair");foreach(Transform t in ext.Cast<Transform>().ToArray())Delete(t);
        Start(ext,"Compact cellar access");
        Box("Exit landing",new Vector3(31,-3.3f,146.5f),new Vector3(3,.2f,1),stone);
        Steps(29.5f,147,3,6,-3.2f,3.2f,9);
        foreach(float x in new[]{29.39f,32.61f}){Box("Retaining wall",new Vector3(x,-1.6f,149.5f),new Vector3(.22f,3.2f,7),stone);Box("Cellar edge rail",new Vector3(x,.55f,149.5f),new Vector3(.12f,1.1f,7),wood);}
        Box("Closed excavation bottom",new Vector3(31,-3.45f,149.5f),new Vector3(3,.3f,7),stone);
        foreach(float x in new[]{28.75f,33.25f})Box("Filled excess excavation",new Vector3(x,-.12f,149.5f),new Vector3(1.5f,.24f,7),grass);
        Box("Cellar doorway jamb",new Vector3(29.25f,-1.875f,146),new Vector3(.5f,2.65f,.22f),stone);Finish();
        // Clip the decorative front gable to the main roof envelope, preserving its exterior silhouette.
        foreach(var t in h.Find("Front gable").Cast<Transform>().Where(t=>t.name=="Gable roof").ToArray())
        {
            var source=t.GetComponent<MeshFilter>().sharedMesh;var verts=source.vertices.Select(v=>t.localPosition+t.localRotation*Vector3.Scale(v,t.localScale)).ToArray();var tris=source.triangles;var output=new List<Vector3>();
            float Plane(Vector3 p)=>p.y-(6.7f+(p.z-128)*4.3f/9+.035f);
            for(int i=0;i<tris.Length;i+=3)
            {var polygon=new List<Vector3>{verts[tris[i]],verts[tris[i+1]],verts[tris[i+2]]};var clipped=new List<Vector3>();for(int j=0;j<polygon.Count;j++){var a=polygon[j];var b=polygon[(j+1)%polygon.Count];float da=Plane(a),db=Plane(b);if(da>=0)clipped.Add(a);if((da>=0)!=(db>=0))clipped.Add(Vector3.Lerp(a,b,da/(da-db)));}for(int j=1;j+1<clipped.Count;j++)output.AddRange(new[]{clipped[0],clipped[j],clipped[j+1]});}
            var mesh=new Mesh();mesh.SetVertices(output);mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateNormals();UV(mesh);mesh=SaveMesh("Trimmed-front-gable-"+t.localPosition.x,mesh);
            t.GetComponent<MeshFilter>().sharedMesh=mesh;t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;Object.DestroyImmediate(t.GetComponent<BoxCollider>());t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        FurnishHouse(h);
        Start(h,"House entrance lanterns");Lamp(new Vector3(24,2.4f,127.5f),false);Lamp(new Vector3(31,-.65f,146.65f),false);Finish();
    }
    static void Steps(float x,float z,float width,float run,float bottom,float rise,int count)
    {
        for(int i=0;i<count;i++){float top=bottom+rise*(i+1)/count;Box("Broad tread "+i,new Vector3(x+width/2,top-rise/count/2,z+run*(i+.5f)/count),new Vector3(width,rise/count,run/count+.006f),wood);}
    }
    static void Table(Vector3 p,float w,float d,float height=1)
    {Box("Tabletop",p+Vector3.up*(height-.09f),new Vector3(w,.18f,d),wood);foreach(float x in new[]{-w/2+.14f,w/2-.14f})foreach(float z in new[]{-d/2+.14f,d/2-.14f})Box("Table leg",p+new Vector3(x,(height-.18f)/2,z),new Vector3(.17f,height-.18f,.17f),wood);}
    static void Chair(Vector3 p,float angle)
    {var q=Quaternion.Euler(0,angle,0);void B(string n,Vector3 o,Vector3 s)=>Box(n,p+q*o,s,wood,true,q);B("Chair seat",new Vector3(0,.48f,0),new Vector3(.64f,.14f,.64f));B("Chair back",new Vector3(0,.9f,.26f),new Vector3(.64f,.75f,.12f));foreach(float x in new[]{-.24f,.24f})foreach(float z in new[]{-.24f,.24f})B("Chair leg",new Vector3(x,.22f,z),new Vector3(.1f,.44f,.1f));}
    static void Shelf(Vector3 p,float w=3)
    {foreach(float y in new[]{.25f,1.05f,1.85f})Box("Shelf",p+new Vector3(0,y,0),new Vector3(w,.12f,.75f),wood);foreach(float x in new[]{-w/2+.08f,w/2-.08f})Box("Shelf upright",p+new Vector3(x,1,0),new Vector3(.14f,2,.75f),steel);}
    static void FurnishHouse(Transform h)
    {
        var f=h.Find("Ground floor");Delete(f.Find("Dining table"));Delete(f.Find("Living bench"));
        Start(f,"Kitchen and dining furniture");Table(new Vector3(18,0,132),3,1.6f,1.05f);foreach(float x in new[]{17.2f,18.8f}){Chair(new Vector3(x,0,130.7f),180);Chair(new Vector3(x,0,133.3f),0);}
        Box("Enamel stove",new Vector3(16,.52f,139.5f),new Vector3(1.4f,1.04f,1.1f),white);Box("Oven window",new Vector3(16,.49f,138.93f),new Vector3(.9f,.5f,.04f),dark,false);
        foreach(float x in new[]{15.65f,16.35f})foreach(float z in new[]{139.2f,139.8f})Box("Burner",new Vector3(x,1.06f,z),new Vector3(.35f,.035f,.35f),dark,false);
        Shelf(new Vector3(19.5f,0,145.3f),2.6f);Box("Kitchen cupboard",new Vector3(14.7f,.9f,141.5f),new Vector3(.9f,1.8f,1.6f),white);Finish();
        Start(f,"Living room furniture");
        Box("Sofa base",new Vector3(30.5f,.3f,143),new Vector3(2.8f,.6f,1.1f),wood);Box("Sofa back",new Vector3(30.5f,.85f,143.48f),new Vector3(2.8f,1.1f,.25f),cloth);
        foreach(float x in new[]{29.6f,30.5f,31.4f})Box("Seat cushion",new Vector3(x,.69f,142.95f),new Vector3(.85f,.22f,.8f),cloth);
        foreach(float x in new[]{29.05f,31.95f})Box("Sofa arm",new Vector3(x,.65f,143),new Vector3(.22f,.85f,1.1f),cloth);
        Table(new Vector3(30.5f,0,140.7f),1.7f,.85f,.52f);Finish();
        Start(h.Find("Upper floor"),"Bedroom furniture");foreach(float z in new[]{132f,142f}){Table(new Vector3(19,3.2f,z),.8f,.75f,.65f);Box("Pillow",new Vector3(17,4.06f,z+1),new Vector3(1.7f,.15f,.6f),cloth);}Box("Wardrobe",new Vector3(20.5f,4.2f,145.1f),new Vector3(1.8f,2,.9f),wood);Finish();
    }
    static void Sheds(Transform env)
    {
        foreach(string name in new[]{"Feed barn","Farm workshop"})
        {
            var shed=env.Find("Left-side buildings/"+name);float x=name=="Feed barn"?-16:-18,z=name=="Feed barn"?76:94,depth=name=="Feed barn"?14:12;
            Delete(shed.Cast<Transform>().Single(t=>t.name=="Side"&&Mathf.Abs(t.localPosition.x-x)<.01f));
            Start(shed,"Centre-facing doorway");foreach(int side in new[]{-1,1})Box("Door side",new Vector3(x,1.6f,z+side*(depth/4+.9f)),new Vector3(.22f,3.2f,depth/2-1.8f),red);Box("Door lintel",new Vector3(x,2.95f,z),new Vector3(.22f,.5f,3.6f),red);Finish();
            Start(shed,"Entrance lanterns");Lamp(new Vector3(x+.45f,2.55f,z),false);float mid=name=="Feed barn"?-25:-26;Lamp(new Vector3(mid,2.55f,z-depth/2-.5f),false);Lamp(new Vector3(mid,2.55f,z+depth/2+.5f),false);Finish();
            Start(shed,name=="Feed barn"?"Feed storage":"Repair workshop");
            if(name=="Feed barn")
            {
                for(int i=0;i<3;i++){Box("Stacked feed bale",new Vector3(-31+i*1.7f,.59f,71.2f),new Vector3(1.6f,1,1.2f),hay);Box("Bale tie",new Vector3(-31+i*1.7f,1.1f,71.2f),new Vector3(.12f,.025f,1.2f),wood,false);}
                Shelf(new Vector3(-30,.09f,78),4);for(int i=0;i<5;i++)Box("Feed sack",new Vector3(-31.5f+i*.7f,1.42f,78),new Vector3(.55f,.6f,.55f),hay);
                Table(new Vector3(-20,.09f,80),2.5f,1.2f);Box("Feed bin",new Vector3(-20,.75f,71.5f),new Vector3(2,1.3f,1.2f),steel);
            }
            else
            {
                Table(new Vector3(-30,.09f,90.5f),5,1.3f);Shelf(new Vector3(-30,.09f,96.4f),4);
                Box("Tool board",new Vector3(-30,1.85f,89.7f),new Vector3(4.5f,1.1f,.12f),wood,false);
                for(int i=0;i<6;i++){Box("Hanging tool",new Vector3(-31.7f+i*.65f,1.8f,89.6f),new Vector3(.09f,.65f,.07f),steel,false);Box("Tool head",new Vector3(-31.7f+i*.65f,2.06f,89.6f),new Vector3(.3f,.12f,.1f),dark,false);}
                Box("Vice",new Vector3(-28.5f,1.25f,90.5f),new Vector3(.6f,.32f,.45f),steel);Box("Spare engine",new Vector3(-22,.7f,97),new Vector3(1.8f,1.2f,1.2f),steel);Table(new Vector3(-22,.09f,90.5f),2,1.2f);
            }
            Finish();
        }
    }
    static void Barn(Transform barn)
    {
        Start(barn,"Side hay loft");Box("Loft main deck",new Vector3(32,3,72.95f),new Vector3(5,.22f,9.9f),wood); Box("Loft stair side",new Vector3(30.25f,3,81.5f),new Vector3(1.5f,.22f,7.2f),wood); Box("Loft wall strip",new Vector3(34.05f,3,81.5f),new Vector3(.9f,.22f,7.2f),wood); Box("Loft upper landing",new Vector3(32,3,85.55f),new Vector3(5,.22f,.9f),wood);
        foreach(float z in new[]{69f,76f,84.5f}){Box("Loft post",new Vector3(29.6f,1.5f,z),new Vector3(.25f,3,.25f),wood);Box("Rail post",new Vector3(29.6f,3.6f,z),new Vector3(.16f,1,.16f),wood);}
        Box("Loft rail",new Vector3(29.6f,4,77),new Vector3(.12f,.12f,16),wood);Steps(31,77.9f,2.6f,7.2f,.11f,3,12);
        foreach(float z in new[]{69.5f,72f})Box("Loft hay",new Vector3(33,3.65f,z),new Vector3(2,1.1f,1.5f),hay);Finish();
        Start(barn,"Barn repair benches");Table(new Vector3(19,0,70),3,1.2f);Table(new Vector3(19,0,85),3,1.2f);Shelf(new Vector3(32,0,87.8f),3);Finish();
        Start(barn,"Dismantled farm tractor");
        Box("Exposed chassis",new Vector3(25,.8f,78),new Vector3(2.1f,.35f,4.8f),steel);foreach(float z in new[]{76.3f,79.7f})Box("Axle",new Vector3(25,.7f,z),new Vector3(3.8f,.25f,.3f),dark);
        foreach(float x in new[]{23.4f,26.6f})Box("Rear tire",new Vector3(x,.9f,79.7f),new Vector3(.65f,1.8f,1.8f),dark);
        Box("Engine block",new Vector3(25,1.4f,76.8f),new Vector3(1.4f,.85f,1.5f),steel);for(int i=0;i<4;i++)Box("Engine ribs",new Vector3(25,1.86f,76.2f+i*.35f),new Vector3(1.5f,.08f,.1f),dark,false);
        Box("Driver seat",new Vector3(25,1.4f,79.1f),new Vector3(1,.24f,.8f),dark);Box("Seat back",new Vector3(25,1.8f,79.5f),new Vector3(1,.8f,.18f),dark);
        foreach(float x in new[]{24f,26f})Box("Roll bar",new Vector3(x,2.1f,79.8f),new Vector3(.14f,2.7f,.14f),red);Box("Roll bar top",new Vector3(25,3.45f,79.8f),new Vector3(2.1f,.14f,.14f),red);
        Box("Detached hood",new Vector3(21.5f,.5f,78.3f),new Vector3(1.6f,.6f,2),red,true,Quaternion.Euler(0,15,0));Box("Removed front tire",new Vector3(22,.3f,75.8f),new Vector3(1.15f,.6f,1.15f),dark);Finish();
        Start(barn,"Barn doorway lanterns");Lamp(new Vector3(16.5f,3,76),false);Lamp(new Vector3(25,2.55f,65.5f),false);Lamp(new Vector3(26.5f,3,89.5f),false);Finish();
    }
    static void Lamp(Vector3 p,bool post)
    {
        if(post)Box("Small lamp post",p-Vector3.up*1.1f,new Vector3(.13f,2.2f,.13f),steel);
        Box("Lantern cap",p+Vector3.up*.21f,new Vector3(.5f,.1f,.5f),dark,false);Box("Warm lamp glass",p,new Vector3(.25f,.32f,.25f),glow,false);Box("Lantern base",p-Vector3.up*.2f,new Vector3(.4f,.08f,.4f),steel,false);
        var t=Group(group,"Baked warm light");t.localPosition=p-Vector3.up*.25f;var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=post?8:12;l.range=post?7:8;l.color=new Color(1,.72f,.4f);l.shadows=LightShadows.Soft;
    }
    static void Exterior(Transform root)
    {
        Start(root,"Pond lanterns");foreach(var p in new[]{new Vector3(11.8f,2.2f,125),new Vector3(17,2.2f,139),new Vector3(32,2.2f,140),new Vector3(38,2.2f,130),new Vector3(28,2.2f,120.5f)})Lamp(p,true);Finish();
        Start(root,"Low poly boundary fence");foreach(float x in new[]{-47f,47f})
        {for(float z=2;z<=158;z+=4)Box("Boundary post",new Vector3(x,RailFarmSilos.HeightV5(x,z)+.65f,z),new Vector3(.16f,1.3f,.16f),wood);for(float z=2;z<158;z+=4)foreach(float y in new[]{.45f,1f})Beam("Boundary rail",new Vector3(x,RailFarmSilos.HeightV5(x,z)+y,z),new Vector3(x,RailFarmSilos.HeightV5(x,z+4)+y,z+4),.11f,wood);}
        Finish();
        var waterMat=AssetDatabase.LoadAssetAtPath<Material>(RailFarmPaddock.Folder+"/Shallow water opaque.mat");
        Start(root,"Shallow ditch water");Box("Water plane",new Vector3(12,-.35f,30.5f),new Vector3(3.3f,.02f,37),waterMat,false);Finish();
        var volume=Group(root,"Ditch wet footsteps");volume.localPosition=new Vector3(12,-.77f,30.5f);var w=volume.gameObject.AddComponent<WaterVolume>();w.size=new Vector3(3.3f,.86f,37);w.waterSurface=AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/Water.asset");
        Start(root,"Cart shed entrance lanterns");Lamp(new Vector3(23,2.45f,105.5f),false);Lamp(new Vector3(23,2.45f,111.5f),false);Finish();
    }
    static void Silo(Transform barn)
    {
        // West silo: preserve the other silo and all unrelated barn edits.
        foreach(var t in barn.Cast<Transform>().Where(t=>Mathf.Abs(t.localPosition.x-22)<.01f&&(t.name=="Grain silo"||t.name=="Silo band")).ToArray())Delete(t);
        var silo=Group(barn,"Enterable grain silo");
        // Sixteen solid wall panels, with a 2.7m high door in the west face at grain height.
        Start(silo,"Silo shell and doorway");
        for(int i=0;i<16;i++)
        {float a=(i+.5f)*Mathf.PI/8;var q=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);var p=new Vector3(22+4.9f*Mathf.Cos(a),6,98+4.9f*Mathf.Sin(a));float length=2*5*Mathf.Sin(Mathf.PI/16)+.025f;
            if(i==7||i==8){Box("Wall below door",new Vector3(p.x,3.75f,p.z),new Vector3(.22f,7.5f,length),steel,true,q);Box("Door header",new Vector3(p.x,11.1f,p.z),new Vector3(.22f,1.8f,length),steel,true,q);}
            else Box("Silo wall",p,new Vector3(.22f,12,length),steel,true,q);
        }
        // Walkable grain surface and a west landing reaching through both door panels.
        Box("Door landing",new Vector3(16.6f,7.4f,98),new Vector3(3.3f,.2f,3.5f),steel);
        Lamp(new Vector3(16.8f,10,98),false);Lamp(new Vector3(22,10.8f,98),false);Finish();
        var source=AssetDatabase.LoadAssetAtPath<Mesh>(RailFarmSilos.Folder+"/Meshes/Cylinder.asset");var grain=MeshObject(silo,"Grain surface",source,hay,true);grain.transform.localPosition=new Vector3(22,7.25f,98);grain.transform.localScale=new Vector3(4.82f,.5f,4.82f);
        // West/north wraparound access avoids the closely spaced east silo.
        // East side is avoided because the second silo is only 11m away: use the outside west/north half with two turns.
        Start(silo,"Wraparound access rails");
        var points=new[]{new Vector3(15,0,90.5f),new Vector3(15,4,106),new Vector3(22,5.5f,106),new Vector3(22,6,110),new Vector3(15,6.5f,110),new Vector3(15,7.5f,98)};
        // The short upper return is outside the northern roof edge of the cart shed.
        for(int i=1;i<points.Length;i++)Ramp(silo,"Silo ramp "+i,points[i-1],points[i],2.1f,steel);
        Finish();
    }
    static void Ramp(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material)
    {
        var delta=b-a;var side=Vector3.Cross(Vector3.up,delta).normalized*width/2;var v=new[]{a-side,a+side,b+side,b-side,a-side-Vector3.up*.18f,a+side-Vector3.up*.18f,b+side-Vector3.up*.18f,b-side-Vector3.up*.18f};
        var m=new Mesh();m.vertices=v;m.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};m.RecalculateNormals();UV(m);MeshObject(parent,name,SaveMesh(name,m),material,true);
        foreach(int s in new[]{-1,1}){Beam("Ramp handrail",a+side*s+Vector3.up,b+side*s+Vector3.up,.1f,steel);for(float t=0;t<=1;t+=.25f){var p=Vector3.Lerp(a,b,t)+side*s;Beam("Ramp post",p,p+Vector3.up,.1f,steel);}}
    }
    static void Loot(RailBlock b,Transform house)
    {
        void Add(RailSpawnKind kind,Vector3 p,string label)
        {var g=RailSpawnerPrefabs.Instantiate(kind,b.gameObject.scene);g.transform.SetParent(b.transform.Find(kind+" Spawns"),false);g.name=kind+" - Farm polish "+label;g.transform.localPosition=p+Vector3.up*(.05f-RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold).min.y);RailSpawnPool.SetPoints(b.spawnCandidates,kind,RailSpawnPool.Points(b.spawnCandidates,kind).Append(g.transform).ToArray());PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(g);}
        foreach(var p in new[]{new Vector3(19.5f,7.5f,98),new Vector3(22,7.5f,95.5f),new Vector3(24.5f,7.5f,98),new Vector3(22,7.5f,100.5f)})Add(RailSpawnKind.Gold,p,"Silo grain");
        foreach(var p in new[]{new Vector3(20,7.5f,100),new Vector3(24,7.5f,96),new Vector3(24,7.5f,100)})Add(RailSpawnKind.Coal,p,"Silo grain");
        foreach(var p in new[]{new Vector3(16,6.4f,135),new Vector3(20,6.4f,135),new Vector3(23,6.4f,135),new Vector3(26,6.4f,135),new Vector3(16,6.4f,140),new Vector3(20,6.4f,140),new Vector3(23,6.4f,140),new Vector3(26,6.4f,140),new Vector3(28,6.4f,138)})Add(RailSpawnKind.Gold,b.transform.InverseTransformPoint(house.TransformPoint(p)),"Attic discovery");
    }
    public static void FinishTouches(GameObject rootOverride=null)
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(!rootOverride&&(stage==null||stage.assetPath!=RailFarmSilos.PrefabPath))throw new Exception("Open Farm prefab");
        Init();meshId=1000;var root=rootOverride?rootOverride:stage.prefabContentsRoot;var b=root.GetComponent<RailBlock>();var farm=root.transform.Find("Env/"+RailFarmSilos.RootName);var h=farm.Find("Farmhouse");var barn=farm.Find("Barn and silos");var silo=barn.Find("Enterable grain silo");
        Delete(silo.Find("Access junctions and supports"));
        foreach(var level in new[]{"Basement","Ground floor","Upper floor"})
        {float ceiling=level=="Basement"?0:level=="Ground floor"?3.2f:6.4f;foreach(Transform t in h.Find(level)){if(!t.GetComponent<BoxCollider>()||Mathf.Abs(t.localPosition.y+t.localScale.y/2-(ceiling-.24f))>.003f)continue;var s=t.localScale;s.y+=.04f;var p=t.localPosition;p.y+=.02f;Resize(t,p,s);}}
        Delete(silo.Find("Wraparound access rails"));Start(silo,"Access junctions and supports");
        var ramps=silo.Cast<Transform>().Where(t=>t.name.StartsWith("Silo ramp ")).OrderBy(t=>t.name).ToArray();
        var route=new[]{new Vector3(15,0,90.5f),new Vector3(15,4,106),new Vector3(22,5.5f,106),new Vector3(22,6,110),new Vector3(15,6.5f,110),new Vector3(15,7.5f,98)};
        for(int i=0;i<ramps.Length;i++)
        {
            var a=route[i];var end=route[i+1];var side=Vector3.Cross(Vector3.up,end-a).normalized*1.05f;var direction=(end-a).normalized;
            foreach(int sign in new[]{-1,1}){var from=a+direction*1.15f+side*sign;var to=end-direction*1.15f+side*sign;Beam("Access handrail",from+Vector3.up,to+Vector3.up,.1f,steel);for(int j=0;j<=3;j++){var p=Vector3.Lerp(from,to,j/3f);Beam("Access upright",p,p+Vector3.up,.1f,steel);}}
            if(i>0)Box("Turning landing",a-Vector3.up*.1f,new Vector3(2.1f,.2f,2.1f),steel);
        }
        foreach(var p in new[]{new Vector3(15,4,106),new Vector3(15,6.5f,110),new Vector3(22,6,110)})
        {Box("Access support",new Vector3(p.x,p.y/2,p.z),new Vector3(.22f,p.y,.22f),steel);}
        Finish();Delete(barn.Find("Loft work light"));Start(barn,"Loft work light");Lamp(new Vector3(32,4.8f,72.5f),false);Finish();
        int index=0;foreach(var t in RailSpawnPool.Candidates(b,RailSpawnKind.Gold).Where(t=>t.name.Contains("Silo base")))
        {t.localPosition=index++==0?new Vector3(20,7.55f,96):new Vector3(23,7.55f,99.5f);t.name=t.name.Replace("Silo base","Accessible silo grain");PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
        PrefabUtility.SaveAsPrefabAsset(root,RailFarmSilos.PrefabPath);AssetDatabase.SaveAssets();
    }
}
#endif

