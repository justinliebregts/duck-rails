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

/// <summary>Approved abandoned Passenger Station. Explicit editor authoring only.</summary>
public static partial class RailAbandonedStation
{
    public const string Id="passenger-station-v1", RootName="Abandoned underground station v2";
    public const string Folder="Assets/DuckRails/AbandonedStation", PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab";
    public const string BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/Bake.unity";
    static Mesh cube;
    static Material concrete,paint,steel,roof,wood,yellow,gravel,grass;
    static readonly Dictionary<string,Mesh> meshes=new();
    public static int LightCount => 52;
    public static float RailHeight(float z)
    {
        float t=Mathf.Clamp01((Mathf.Min(z,160-z)-6)/39);
        return -4*t*t*(3-2*t);
    }
    static Transform Group(Transform p,string n){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
    static Material Mat(string n,string source,Color color)
    {
        string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){var src=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville "+source+".mat");if(!src)throw new Exception(source);m=new Material(src);AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m,bool solid=true)
    {
        var g=Group(p,n).gameObject;g.layer=6;g.transform.localPosition=pos;g.transform.localScale=size;
        string key=Hash128.Compute(size.ToString("F4")).ToString();
        if(!meshes.TryGetValue(key,out var mesh))
        {
            mesh=Object.Instantiate(cube);var v=mesh.vertices;var ns=mesh.normals;var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=(Mathf.Abs(ns[i].y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(ns[i].x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}
            mesh.uv=uv;string path=Folder+"/Meshes/Box-"+key+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);meshes.Add(key,mesh);
        }
        g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.22f;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(solid)g.AddComponent<BoxCollider>();return g;
    }
    static void Slab(Transform p,string n,float x,float z,float w,float d,float y,Material m)=>Box(p,n,new(x+w/2,y-.15f,z+d/2),new(w,.3f,d),m);
    static void Wall(Transform p,string n,bool alongZ,float axis,float a,float b,float y,float h,Material m,params float[] doors)
    {
        float cursor=a;
        for(int i=0;i<=doors.Length;i+=2)
        {
            float end=i<doors.Length?doors[i]:b;Seg(cursor,end,y,h);
            if(i<doors.Length){Seg(end,doors[i+1],y+2.7f,h-2.7f);cursor=doors[i+1];}
        }
        void Seg(float lo,float hi,float bottom,float height){if(hi<=lo||height<=0)return;Box(p,n,alongZ?new(axis,bottom+height/2,(lo+hi)/2):new((lo+hi)/2,bottom+height/2,axis),alongZ?new(.25f,height,hi-lo):new(hi-lo,height,.25f),m);}
    }
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,float depth,Material m,bool solid=true)
    {var g=Box(p,n,(a+b)/2,new(width,depth,(b-a).magnitude),m,solid);g.transform.localRotation=Quaternion.LookRotation(b-a);}
    static void Sign(Transform p,string text,Vector3 pos,float yaw=0,float size=.55f)
    {
        var t=Group(p,"Sign "+text);t.localPosition=pos;t.localRotation=Quaternion.Euler(0,yaw-180,0);
        float width=Mathf.Clamp(text.Length*.28f,4,12);
        Box(t,"Sign backing",new(0,0,.05f),new(width,1,.12f),steel,false);
        var label=Group(t,"Lettering").gameObject.AddComponent<TextMeshPro>();label.transform.localPosition=new(0,0,-.025f);
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");label.text=text;label.fontSize=3;label.enableAutoSizing=true;label.fontSizeMin=1.5f;label.fontSizeMax=3;
        label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new(width-.3f,.8f);label.color=new(.85f,.83f,.67f);
    }
    static void Lamp(Transform p,Vector3 pos,bool warm=false,float range=20)
    {
        Box(p,"Fixture",pos+Vector3.up*.15f,new(.7f,.18f,.35f),yellow,false);
        var l=Group(p,"Baked station fixture").gameObject.AddComponent<Light>();l.transform.localPosition=pos;l.transform.localRotation=Quaternion.Euler(90,0,0);
        l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=range>=24?45:18;l.range=range;l.color=warm?new(1,.77f,.48f):new(.68f,.82f,1);l.shadows=LightShadows.Soft;
    }
    static void Room(Transform p,string name,float x,float z,float w,float d,float y,bool sideDoors=true)
    {
        var t=Group(p,name);Slab(t,"Floor",x,z,w,d,y,concrete);
        Wall(t,"Side wall",true,x,z,z+d,y,3.5f,paint,sideDoors?new[]{z+d/2-1.5f,z+d/2+1.5f}:Array.Empty<float>());
        Wall(t,"Side wall",true,x+w,z,z+d,y,3.5f,paint,sideDoors?new[]{z+d/2-1.5f,z+d/2+1.5f}:Array.Empty<float>());
        Wall(t,"Back wall",false,z+d,x,x+w,y,3.5f,paint);
        Wall(t,"Front wall",false,z,x,x+w,y,3.5f,paint,sideDoors?Array.Empty<float>():new[]{x+w/2-1.5f,x+w/2+1.5f});
        Slab(t,"Room ceiling",x,z,w,d,y+3.65f,roof);
        Box(t,"Service counter",new(x+w/2,y+.5f,z+d-1.1f),new(w*.55f,1,.8f),wood);
        for(int i=0;i<3;i++){Box(t,"Shelving",new(x+1.5f+i*2.5f,y+.9f,z+1.3f),new(1.8f,1.8f,.55f),steel);for(int j=0;j<2;j++)Box(t,"Abandoned stock",new(x+1.4f+i*2.5f,y+.45f+j*.65f,z+1.65f),new(.65f,.4f,.55f),wood);}
        Sign(t,name.ToUpper(),new(x+w/2,y+2.8f,z-.16f),180,.25f);Lamp(t,new(x+w/2,y+3.25f,z+d/2),true,12);
    }
    static void Stairs(Transform p,string name,float x,float z,float width,float run,float bottom,float top,int count,bool escalator)
    {
        var t=Group(p,name);float step=run/count,rise=(top-bottom)/count;
        for(int i=0;i<count;i++)
        {
            float h=(i+1)*rise;Box(t,"Tread "+i,new(x+width/2,bottom+h/2,z+(i+.5f)*step),new(width,h,step+.008f),escalator?steel:concrete);
            Box(t,"Tread edge",new(x+width/2,bottom+h+.012f,z+i*step+.04f),new(width,.024f,.08f),yellow,false);
        }
        foreach(float xx in new[]{x-.1f,x+width+.1f})
        {Beam(t,escalator?"Stopped escalator handrail":"Stair handrail",new(xx,bottom+1,z),new(xx,top+1,z+run),.15f,.18f,steel);for(int i=0;i<=count;i+=6)Box(t,"Rail post",new(xx,bottom+i*rise+.5f,z+i*step),new(.12f,1,.12f),steel);}
        if(escalator)foreach(float xx in new[]{x-.18f,x+width+.18f})Beam(t,"Escalator side casing",new(xx,bottom+.35f,z),new(xx,top+.35f,z+run),.25f,.7f,paint);
    }
    static void GroundAndTrack(Transform p,RailBlock b)
    {
        var t=Group(p,"Graded railway and terrain");
        b.track=Enumerable.Range(0,161).Select(z=>new Vector3(0,RailHeight(z),z)).ToArray();b.stop.localPosition=new(0,-4,80);
        for(int z=0;z<160;z++)
        {
            float ya=RailHeight(z),yb=RailHeight(z+1);
            Beam(t,"Rail bed",new(0,ya-.15f,z),new(0,yb-.15f,z+1),6,.3f,gravel);
            foreach(float x in new[]{-.8f,.8f})Beam(t,"Visual rail",new(x,ya+.08f,z),new(x,yb+.08f,z+1),.1f,.12f,steel,false);
            if(z%2==0)Box(t,"Visual sleeper",new(0,ya+.02f,z+.4f),new(2.4f,.08f,.25f),wood,false);
        }
        foreach(float z in new[]{0f,154f})Slab(t,"Flat seam",-48,z,96,6,0,grass);
        foreach(float x in new[]{-48f,44f})Slab(t,"Flat side boundary",x,6,4,148,0,grass);
        // RailRoute places the existing midpoint shop at (-16,0,148) on any selected middle block.
        Slab(t,"Reserved midpoint shop pad",-44,140,38,14,0,concrete);
        // Side floors are tiled to leave two actual open stairwells for the underpass.
        for(int x=-44;x<44;x++)for(int z=45;z<115;z++)
        {
            if(x>=-3&&x<3)continue;
            if((x>=-34&&x< -30||x>=30&&x<34)&&z>=48&&z<62)continue;
            // Consolidate floor into long strips, interrupted only where stairs descend.
            if(z!=45)continue;
            bool stair=x>=-34&&x< -30||x>=30&&x<34;
            if(stair){Slab(t,"Platform floor",x,45,1,3,-3,concrete);Slab(t,"Platform floor",x,62,1,53,-3,concrete);}
            else Slab(t,"Platform floor",x,45,1,70,x>=19&&x<24?-3.4f:-3,concrete);
        }
        foreach(float side in new[]{-1f,1f})
        {
            for(int z=6;z<45;z++)OuterSegment(z,side);
            for(int z=115;z<154;z++)OuterSegment(z,side);
            Box(t,"Platform safety stripe",new(side*3.18f,-2.98f,80),new(.24f,.025f,70),yellow,false);
        }
        void OuterSegment(int z,float side)
        {
            float h0=RailHeight(z),h1=RailHeight(z+1);
            // Smooth pedestrian walkways reach platform height at the station and seam at each end.
            float a0=Mathf.Clamp01((Mathf.Min(z,160-z)-6)/12),a1=Mathf.Clamp01((Mathf.Min(z+1,159-z)-6)/12);
            Beam(t,"Tunnel side walkway",new(side*4.5f,h0+a0-.15f,z),new(side*4.5f,h1+a1-.15f,z+1),3,.3f,concrete);
            // Raised flanks form the covered cutting, tapering to the flat seams.
            float hump=8*Mathf.Sin(Mathf.PI*(z+.5f-6)/148);
            if(!(side<0&&z>=140))Box(t,"Terrain flank",new(side*27,(hump-10)/2,z+.5f),new(34,hump+10,1),gravel);
        }
    }
    static void Shell(Transform p)
    {
        var t=Group(p,"Station shell and tunnel portals");
        foreach(float x in new[]{-44.2f,44.2f})Box(t,"Outer retaining wall",new(x,2.7f,80),new(.4f,11.4f,70),concrete);
        foreach(float z in new[]{44.8f,115.2f})
        {
            Wall(t,"End wall",false,z,-44,-6,-3,11.4f,paint);
            Wall(t,"End wall",false,z,6,44,-3,11.4f,paint);
            Box(t,"Portal lintel",new(0,7.7f,z),new(12,1.4f,.5f),concrete);
        }
        Slab(t,"High chamber roof",-44.4f,44.6f,88.8f,70.8f,9,roof);
        foreach(float z in new[]{53f,72f,92f,111f})
        {
            foreach(float x in new[]{-21f,27f})Box(t,"Hall column",new(x,2.65f,z),new(.65f,11.3f,.65f),concrete);
            Box(t,"Roof truss",new(0,8.15f,z),new(88,.45f,.4f),steel);
        }
        for(int z=8;z<152;z+=2)
        {
            if(z>=44&&z<116)continue;
            float y=RailHeight(z+1);
            foreach(float x in new[]{-6.2f,6.2f})
                if(!(x<0&&z>=140))Box(t,"Tunnel wall",new(x,y+3.6f,z+1),new(.4f,7.5f,2.02f),paint);
            Box(t,"Tunnel roof",new(0,y+7.4f,z+1),new(12.8f,.4f,2.02f),roof);
            if(z%8==0){foreach(float x in new[]{-5.8f,5.8f})Box(t,"Tunnel rib",new(x,y+3.6f,z+1),new(.15f,7.2f,.2f),steel);}
        }
        foreach(float z in new[]{16f,32f,126f,144f})Lamp(t,new(0,RailHeight(z)+6.8f,z),false,19);
        // Side maintenance alcoves occupy the approach/departure thirds, reached from walkway gaps.
        foreach(float z in new[]{28f,132f})foreach(float side in new[]{-1f,1f})
        {
            float floor=RailHeight(z)+1;float x=side<0?-16:6;
            Room(t,z<80?"Pump alcove":"Ventilation room",x,z-4,10,8,floor);
            Slab(t,"Alcove landing",side<0?-7:4,z-2,3,4,floor,concrete);
            Wall(t,"Sealed outer alcove wall",true,side*16,z-4,z+4,floor,3.5f,paint);
            Box(t,"Alcove outer terrain",new(side*30,0,z),new(28,12,10),gravel);
        }
    }
    static void PublicAndStaff(Transform p)
    {
        var west=Group(p,"West shops and staff corridor");var east=Group(p,"East rooms and staff corridor");
        string[] westNames={"Tickets","Mini mart","Stockroom","Staff lockers"};string[] eastNames={"Toilets","Kiosk","Electrical room","Workshop"};
        for(int i=0;i<4;i++){Room(west,westNames[i],-36,63+i*8,13,8,-3);Room(east,eastNames[i],30,63+i*8,9,8,-3);}
        foreach(float x in new[]{-40f,41.5f})foreach(float z in new[]{57f,78f,101f})Lamp(p,new(x,.2f,z),true,18);
        foreach(float x in new[]{-7f,7f,27.5f})foreach(float z in new[]{58f,77f,96f})Lamp(p,new(x,5.7f,z),false,24);
        foreach(float z in new[]{66f,76f})foreach(float x in new[]{-9f,11f,28f})
        {
            Box(p,"Waiting bench seat",new(x,-2.5f,z),new(2.6f,.2f,.7f),wood);
            foreach(float dx in new[]{-.9f,.9f})Box(p,"Bench leg",new(x+dx,-2.8f,z),new(.13f,.4f,.5f),steel);
        }
        Sign(p,"PLATFORM 1  /  DEPARTURES",new(-9,1.1f,62),180,.35f);
        Sign(p,"ABANDONED LINE",new(22,1.2f,63),180,.35f);
        Sign(p,"SHOPS  /  CONCOURSE",new(0,4.7f,100),180,.4f);
        // Lower cross-passage has a roof below the running track, with holes at stair landings.
        var under=Group(p,"Staff underpass");Slab(under,"Underpass floor",-34,44,68,5,-7,concrete);
        Slab(under,"Underpass ceiling",-30,44,60,5,-4.45f,roof);
        Wall(under,"South wall",false,44,-34,34,-7,2.5f,paint);
        Wall(under,"North wall",false,49,-30,30,-7,2.5f,paint);
        foreach(float x in new[]{-34.15f,34.15f})Wall(under,"Stairwell side",true,x,44,62,-7,4,paint);
        foreach(float x in new[]{-30.15f,30.15f})Wall(under,"Stairwell inner",true,x,49,62,-7,4,paint);
        Stairs(under,"West staff stair",-34,49,4,13,-7,-3,24,false);
        Stairs(under,"East staff stair",30,49,4,13,-7,-3,24,false);
        foreach(float x in new[]{-24f,0f,24f})Lamp(under,new(x,-4.8f,46.5f),true,20);
        // Upper route links all public sides, with two stair/escalator pairs.
        var upper=Group(p,"Upper shopping concourse");Slab(upper,"Cross concourse deck",-40,100,81,10,3.2f,concrete);
        foreach(float x in new[]{-14f,10f})Stairs(upper,"Public stairs",x,82,3,18,-3,3.2f,36,false);
        foreach(float x in new[]{-19f,15f})Stairs(upper,"Stopped escalator",x,82,2.5f,18,-3,3.2f,36,true);
        // Front guards leave the four arrival landings open.
        foreach(var r in new[]{new Vector2(-40,-19.2f),new Vector2(-16.3f,-14.2f),new Vector2(-10.8f,9.8f),new Vector2(13.2f,14.8f),new Vector2(17.7f,41)})
            Box(upper,"Concourse front guard",new((r.x+r.y)/2,3.8f,100),new(r.y-r.x,1.2f,.15f),steel);
        Box(upper,"Concourse back guard",new(.5f,3.8f,110),new(81,1.2f,.15f),steel);
        foreach(float x in new[]{-40f,41f})Box(upper,"Concourse end guard",new(x,3.8f,105),new(.15f,1.2f,10),steel);
        Room(upper,"Upper newsagent",-38,103,12,7,3.2f,false);Room(upper,"Upper cafe",28,103,12,7,3.2f,false);
        foreach(float x in new[]{-16f,4f,23f})Lamp(upper,new(x,7.7f,105),false,20);
    }
    static void Wreck(Transform p)
    {
        var t=Group(p,"Walk-through three-car wreck");
        Beam(t,"South wreck access ramp",new(21.5f,-3.55f,61.5f),new(21.5f,-3.15f,64),2,.3f,steel);
        Beam(t,"North wreck access ramp",new(21.5f,-3.15f,98.5f),new(21.5f,-3.55f,101),2,.3f,steel);
        for(int z=61;z<101;z+=2)
        {foreach(float x in new[]{20.7f,22.3f})Box(t,"Disused visual rail",new(x,-3.32f,z+1),new(.09f,.1f,2),steel,false);Box(t,"Disused sleeper",new(21.5f,-3.36f,z+1),new(2.6f,.06f,.2f),wood,false);}
        for(int i=0;i<3;i++)
        {
            float z=64+i*11.5f;var car=Group(t,"Broken carriage "+(i+1));Slab(car,"Carriage floor",19,z,5,11.5f,-3,wood);
            foreach(float x in new[]{19f,24f})
            {
                Wall(car,"Rusty body",true,x,z,z+11.5f,-3,2.8f,paint,z+4,z+7);
                // Broken windows are represented by thin dark insets above low seats.
                foreach(float dz in new[]{1.5f,9.5f})Box(car,"Broken window inset",new(x,-1.2f,z+dz),new(.27f,.8f,2),roof,false);
            }
            foreach(float zz in new[]{z,z+11.5f})Wall(car,"Open gangway",false,zz,19,24,-3,2.8f,steel,20.5f,22.5f);
            Slab(car,"Carriage roof",18.9f,z-.05f,5.2f,11.6f,.05f,roof);
            foreach(float x in new[]{19.5f,23.5f})foreach(float dz in new[]{2f,9f})Box(car,"Old seats",new(x,-2.5f,z+dz),new(.7f,1,2.4f),wood);
            Lamp(car,new(21.5f,-.35f,z+5.5f),true,13);
        }
    }
    static void Set(Transform t,Vector3 p){t.localPosition=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Install approved station")]
    public static void Install()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Duck Rails Edit Mode, no bake");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save current scene first");
        Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();meshes.Clear();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");if(!cube)throw new Exception("Missing cube UV");
        concrete=Mat("Worn station concrete","concrete",new(.7f,.7f,.65f));paint=Mat("Faded station green","weathered paint",new(.52f,.63f,.55f));steel=Mat("Oxidized station metal","weathered paint",new(.36f,.41f,.42f));roof=Mat("Dark station roof","dark roofing",new(.4f,.43f,.44f));wood=Mat("Abandoned timber","timber",new(.57f,.43f,.3f));yellow=Mat("Faded safety yellow","weathered paint",new(.82f,.65f,.25f));gravel=Mat("Tunnel rock and ballast","gravel",new(.5f,.5f,.45f));grass=Mat("Outer ground","grass",new(.48f,.55f,.4f));
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();if(!b||b.id!=Id)throw new Exception("Wrong block");
        var keep=new HashSet<Transform>{b.entry,b.exit,b.stop,b.GetComponentInChildren<CrawlGraphBaker>().transform};
        foreach(var v in b.variations)foreach(var marker in v.coal.Concat(v.enemies).Concat(v.gold)) {var t=marker;while(t.parent!=b.transform)t=t.parent;keep.Add(t);}
        foreach(Transform t in b.transform.Cast<Transform>().ToArray())if(!keep.Contains(t))Object.DestroyImmediate(t.gameObject);
        var root=Group(b.transform,RootName);BuildMetro(root,b);
        b.displayName="Abandoned Underground Station";b.tags=new[]{"interior","underground","graded"};
        b.enemyBounds=new Bounds(new(0,-4,80),new(88,22,112));
        b.enemySurfaceRailExclusion = default;
        for(int v=0;v<b.variations.Length;v++)
        {
            var coal=new[]{new Vector3(-7,-2.5f,67),new(-12,-2.5f,76),new(-7,-2.5f,98),new(16,-7.5f,67),new(16,-7.5f,83),new(21.5f,-7.5f,92)};
            var enemy=new[]{new Vector3(-7,-2.8f,68),new(-7,-2.8f,90),new(0,-3.8f,80),new(7,-2.8f,68),new(16,-7.8f,85),new(21.5f,-7.8f,92)};
            for(int i=0;i<6;i++){Set(b.variations[v].coal[i],coal[i]+Vector3.forward*((v-1)*.4f));Set(b.variations[v].enemies[i],enemy[i]+Vector3.forward*((v-1)*.3f));}
            Set(b.variations[v].gold[0],new(-12,-2.5f,88+(v-1)*.2f));Set(b.variations[v].gold[1],new(21.5f,-7.5f,90+(v-1)*.3f));
        }
        foreach(var variation in b.variations)foreach(var marker in variation.coal.Concat(variation.enemies).Concat(variation.gold)) Set(marker,marker.localPosition+Vector3.left*12);
        // Carve the alcove entry out of tunnel wall segments and terrain instead of hiding a blocked door.
        foreach(var t in root.GetComponentsInChildren<Transform>().ToArray())
        {
            if(t.name=="Tunnel wall" && (Mathf.Abs(t.localPosition.z-28)<2.1f||Mathf.Abs(t.localPosition.z-132)<2.1f))Object.DestroyImmediate(t.gameObject);
            if(t && t.name=="Terrain flank" && (Mathf.Abs(t.localPosition.z-28)<5||Mathf.Abs(t.localPosition.z-132)<5))Object.DestroyImmediate(t.gameObject);
        }
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();baker.transform.localPosition=new(0,-2,80);baker.size=new(112,32,128);baker.cellSize=1;baker.allowCeilings=false;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Station graph bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);PrefabUtility.RecordPrefabInstancePropertyModifications(baker.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(b);PrefabUtility.RecordPrefabInstancePropertyModifications(b.stop);
        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(int z=8;z<154;z+=6)foreach(float x in new[]{-40f,-28f,-16f,-7f,0f,7f,16f,22f,28f,35f,41f})foreach(float y in new[]{-7.4f,-6.5f,-2.4f,-1.5f,1f,3.8f})
        {if((z<44||z>116)&&(Mathf.Abs(x)>5||y<RailHeight(z)))continue;var q=new Vector3(x,y,z);if(!Physics.CheckSphere(q,.22f,1<<6,QueryTriggerInteraction.Ignore))points.Add(q);}
        foreach(float z in new[]{0f,159f})foreach(float x in new[]{-4f,0f,4f})points.Add(new(x,2,z));probes.probePositions=points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Abandoned station installed, graph baked. Lights="+root.GetComponentsInChildren<Light>().Length+". Run physical checks, then bake lighting.");
    }
}
#endif
