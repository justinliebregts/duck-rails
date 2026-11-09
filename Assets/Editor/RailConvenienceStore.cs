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
using Object = UnityEngine.Object;

/// <summary>Explicit authoring of the approved November 4 store plan; never run at runtime.</summary>
public static partial class RailConvenienceStore
{
    public const string RootName="Convenience store authored v5";
    public const string PrefabPath="Assets/DuckRails/Blocks/convenience-store-v1.prefab";
    public const string BakeScene="Assets/DuckRails/Lighting/Blocks/convenience-store-v1/convenience-store-v1-bake.unity";
    public const string Folder="Assets/DuckRails/ConvenienceStore";
    public const int LightCount=30;
    static Mesh cube;
    static Material lens, neon, glass;
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static void GuardEditor()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)
            throw new InvalidOperationException("Use Duck Rails Edit Mode with no active bake.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Install approved layout")]
    public static void Install()
    {
        GuardEditor();RailDepotMaterials.BeginSharedPass(Folder+"/Meshes");
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/convenience-store-v1/Cube Block UV.asset");
        lens=Special("Lamp lens",new Color(.85f,.88f,.72f),new Color(.6f,.65f,.45f));
        neon=Special("Neon pink",new Color(.85f,.25f,.51f),new Color(2.5f,.18f,.9f));
        glass=Special("Opaque cold glass",new Color(.19f,.35f,.39f),Color.black);
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();
        if(!b||b.id!="convenience-store-v1")throw new InvalidOperationException("Wrong block.");
        var track=b.track.ToArray();var bounds=b.enemyBounds;
        foreach(Transform t in b.transform.Cast<Transform>().ToArray())
            if(t.name.StartsWith("Store ")||t.name=="Shop aisle"||t.name=="Checkout"||t.name=="Canopy"||t.name=="Trackside post"||t.name=="CONVENIENCE STORE"||t.name.StartsWith("STOP ")||t.name=="Left-side buildings"||t.name=="Baked block sample lights"||t.name.StartsWith("Convenience store authored v"))
                Object.DestroyImmediate(t.gameObject);
        var root=Group(b.transform,RootName);
        BuildV5(root);
        var ground=b.transform.Find("Block ground");
        GrassGround(ground);
        for(int v=0;v<b.variations.Length;v++)
        {
            float s=(v-1)*.5f;
            var coal=new[]{new Vector3(-24+s,.5f,79),new Vector3(-22+s,.5f,38),new Vector3(-20+s,.5f,98.5f),new Vector3(24+s,.5f,75),new Vector3(30+s,.5f,95),new Vector3(25+s,.5f,125)};
            var enemies=new[]{new Vector3(24+s,.2f,75),new Vector3(32+s,.2f,80),new Vector3(15+s,.2f,98),new Vector3(34+s,.2f,64),new Vector3(15+s,.2f,125),new Vector3(35+s,.2f,125)};
            for(int i=0;i<6;i++){Position(b.variations[v].coal[i],coal[i]);Position(b.variations[v].enemies[i],enemies[i]);}
            Position(b.variations[v].gold[0],new Vector3(-35,.5f,74+v));Position(b.variations[v].gold[1],new Vector3(38,.5f,75+v));
        }
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();
        // Preserve chunk origin (8,-2,32); extend north to cover relocated restaurants.
        baker.transform.localPosition=new Vector3(27,4,88);baker.size=new Vector3(38,12,112);
        b.enemyBounds=new Bounds(new Vector3(26,4,85),new Vector3(34,12,80));
        PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new InvalidOperationException("Store crawlgraph bake failed.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        if(!track.SequenceEqual(b.track))throw new InvalidOperationException("Track changed.");        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(float z=2;z<160;z+=8)foreach(float x in new[]{-40f,-24f,-10f,0f,12f,20f,28f,36f,43f})foreach(float y in new[]{.6f,2f,4.5f})
        {var p=new Vector3(x,y,z);if(!Physics.CheckSphere(p,.2f,1<<6))points.Add(p);}
        probes.probePositions=points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        RailCatalogLightingAuthoring.BakeOpen();Debug.Log("Approved store installed; graph baked; lighting bake/export queued.");
    }
    static void Position(Transform t,Vector3 p){t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    static Material Special(string n,Color color,Color emission)
    {
        string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);
        if(emission.maxColorComponent>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,string kind="prop",bool collision=true,Material special=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=n;go.layer=6;go.transform.SetParent(p,false);go.transform.localPosition=pos;go.transform.localScale=size;
        var f=go.GetComponent<MeshFilter>();f.sharedMesh=cube;var r=go.GetComponent<MeshRenderer>();
        if(special)r.sharedMaterial=special;else RailDepotMaterials.Map(f,r,kind,0,Folder+"/Meshes");
        r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=collision?.4f:.15f;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
        if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Pad(Transform p,string n,float x,float z,float w,float d,string kind="concrete")
    {Box(p,n,new Vector3(x+w/2,.009f,z+d/2),new Vector3(w,.012f,d),kind,false);}
    static void Prop(Transform p,string n,float x,float z,float w,float d,float h=1.2f,string kind="prop",float y=0)
    {Box(p,n,new Vector3(x+w/2,y+h/2,z+d/2),new Vector3(w,h,d),kind);}
    static void Wall(Transform p,bool alongZ,float axis,float a,float b,float h=3.6f,params float[] openings)
    {
        float cursor=a;
        for(int i=0;i<=openings.Length;i+=2)
        {float end=i<openings.Length?openings[i]:b;Segment(cursor,end,0,h);if(i<openings.Length){Segment(end,openings[i+1],2.7f,h-2.7f);cursor=openings[i+1];}}
        void Segment(float lo,float hi,float y,float height){if(hi<=lo||height<=0)return;Box(p,"Wall",alongZ?new Vector3(axis,y+height/2,(lo+hi)/2):new Vector3((lo+hi)/2,y+height/2,axis),alongZ?new Vector3(.2f,height,hi-lo):new Vector3(hi-lo,height,.2f),"wall");}
    }
    static void Sign(Transform p,string text,Vector3 pos,float width,bool north=true,Color? color=null)
    {
        var t=Group(p,"Sign "+text).gameObject.AddComponent<TextMeshPro>();t.text=text;t.fontSize=4;t.alignment=TextAlignmentOptions.Center;t.color=color??Color.white;
        t.rectTransform.sizeDelta=new Vector2(width,1.1f);t.transform.localPosition=pos;t.transform.localRotation=Quaternion.Euler(0,north?180:0,0);
    }
    static void Lamp(Transform p,string name,float x,float y,float z,bool post=false,Color? tint=null)
    {
        if(post)Prop(p,"Lamp post",x-.08f,z-.08f,.16f,.16f,y,"steel");
        Box(p,"Fixture",new Vector3(x,y,z),new Vector3(.8f,.12f,.45f),"steel",false,lens);
        var g=Group(p,name);g.localPosition=new Vector3(x,y-.15f,z);g.localRotation=Quaternion.Euler(90,0,0);
        var l=g.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.color=tint??new Color(.85f,.92f,1);l.intensity=65;l.range=20;l.spotAngle=120;l.innerSpotAngle=75;l.shadows=LightShadows.Soft;
    }
    static void Store(Transform root)
    {
        var p=Group(root,"Store");Pad(p,"Store floor",14,58,24,18);Wall(p,true,14,58,76);Wall(p,true,38,58,76);Wall(p,false,58,14,38,3.6f,24,27);
        // Glazed shopfront with a generous central doorway.
        foreach(var pair in new[]{new Vector2(14,23),new Vector2(26,38)})
        {
            float w=pair.y-pair.x,x=(pair.x+pair.y)/2;Box(p,"Front sill",new Vector3(x,.35f,76),new Vector3(w,.7f,.2f),"wall");
            Box(p,"Shopfront glass",new Vector3(x,1.65f,76),new Vector3(w,1.9f,.1f),"steel",true,glass);
        }
        Box(p,"Front lintel",new Vector3(26,3.15f,76),new Vector3(24,.9f,.25f),"wall");
        Wall(p,false,61,14,38,3.6f,23.2f,26.2f);
        var roofs=Group(p,"Roofs");Box(roofs,"Roof",new Vector3(26,3.8f,67),new Vector3(24.5f,.25f,18.5f),"roof");Box(roofs,"Front canopy",new Vector3(26,3.25f,77),new Vector3(25,.18f,2),"roof");
        Sign(p,"QUACK MART",new Vector3(26,3.2f,77.12f),14);
        for(int i=0;i<3;i++)
        {
            var a=Group(p,"Shelf run "+(i+1));float x=17.8f+i*4.5f;
            Prop(a,"Shelf back",x+.65f,64, .2f,7,1.7f,"steel");
            foreach(float y in new[]{.15f,.7f,1.25f})Box(a,"Shelf",new Vector3(x+.75f,y,67.5f),new Vector3(1.5f,.12f,7),"steel");
            foreach(float z in new[]{64.5f,66.5f,68.5f,70f})Prop(a,"Stock carton",x+.15f,z,.45f,.55f,.35f,"timber",1.31f);
        }
        Prop(p,"Checkout counter",14.8f,73.3f,5,1.5f,.95f,"prop");Prop(p,"Cash register",17.5f,73.5f,.65f,.65f,.3f,"steel",.95f);
        for(int i=0;i<5;i++)
        {
            float z=63.8f+i*1.65f;Prop(p,"Fridge cabinet",36.1f,z,1.4f,1.5f,2.3f,"steel");
            Box(p,"Fridge door glass",new Vector3(36.04f,1.3f,z+.75f),new Vector3(.08f,1.8f,1.3f),"steel",false,glass);
            Box(p,"Fridge handle",new Vector3(35.96f,1.25f,z+.2f),new Vector3(.1f,.6f,.08f),"steel",false);
        }
        Prop(p,"Chest freezer",31.8f,63,2,3.2f,1,"steel");Box(p,"Freezer glass lid",new Vector3(32.8f,1.04f,64.6f),new Vector3(1.7f,.08f,2.9f),"steel",false,glass);
        Prop(p,"Stockroom shelving",15,58.8f,5,1.2f,2,"timber");Prop(p,"Delivery cartons",30,58.8f,2,1.5f,1.1f,"timber");
        foreach(float x in new[]{20f,31f})foreach(float z in new[]{65f,72f})Lamp(p,"Baked shop fixture",x,3.45f,z);
        Lamp(p,"Baked stockroom fixture",26,3.4f,59.5f);
        var service=Group(root,"Service loop");Pad(service,"West walk",11,54,3,34);Pad(service,"East dumpster lane",38,54,6,34);Pad(service,"Rear walk",11,54,33,4);
        foreach(float z in new[]{61f,67f}){Prop(service,"Dumpster",41,z,2,3,1.5f,"prop");Box(service,"Dumpster lid",new Vector3(42,1.55f,z+1.5f),new Vector3(2.1f,.12f,3.1f),"steel");}
        Lamp(service,"Baked service fixture",39,4,70,true);
    }
    static void Plaza(Transform root)
    {
        var p=Group(root,"Restaurant plaza");Pad(p,"Front sidewalk",11,100,30,2);Pad(p,"Rear sidewalk",8,114,36,3);Pad(p,"West bypass",8,100,3,17);Pad(p,"East bypass",41,100,3,17);
        string[] names={"LAST SLICE","DUCK DINER","NOODLE BOX"};
        for(int i=0;i<3;i++)
        {
            float x=11+i*10;var r=Group(p,names[i]);Pad(r,"Restaurant floor",x,102,10,12);
            Wall(r,true,x,102,114);if(i==2)Wall(r,true,x+10,102,114);Wall(r,false,102,x,x+10,3.6f,x+3,x+6);Wall(r,false,114,x,x+10,3.6f,x+3,x+6);
            Wall(r,false,110,x,x+10,3.6f,x+3,x+6);
            Box(r,"Roof",new Vector3(x+5,3.8f,108),new Vector3(10.1f,.25f,12.5f),"roof");
            Box(r,"Faded fascia",new Vector3(x+5,3.1f,101.85f),new Vector3(9.7f,.8f,.2f),"prop");Sign(r,names[i],new Vector3(x+5,3.1f,101.72f),9,false);
            Prop(r,"Dining table",x+1,104,1.5f,1.5f,.75f,"timber");Prop(r,"Booth bench",x+.4f,106.5f,2,1,.7f,"prop");
            Prop(r,"Service counter",x+7,107,2,2,.95f,"steel");Prop(r,"Kitchen range",x+7,111.5f,2,1.5f,1,"steel");Prop(r,"Kitchen sink",x+.6f,111.5f,2,1.5f,.9f,"steel");
            Lamp(r,"Baked dining fixture",x+5,3.4f,106);Lamp(r,"Baked kitchen fixture",x+5,3.4f,112);
        }
    }
    static void Vehicle(Transform p,float x,float z,bool van=false)
    {
        var v=Group(p,van?"Abandoned delivery van":"Abandoned car");float w=van?2.7f:2.3f,d=van?5.4f:4.6f;
        Prop(v,"Body",x,z,w,d,.75f,"prop",.3f);Prop(v,"Cabin",x+.15f,z+1,w-.3f,d-2,van?1.5f:.8f,"steel",1.05f);
        foreach(float xx in new[]{x-.08f,x+w-.25f})foreach(float zz in new[]{z+.65f,z+d-1.2f})Prop(v,"Wheel",xx,zz,.35f,.7f,.7f,"roof");
    }
    static void Street(Transform root)
    {
        var p=Group(root,"Road and parking");Pad(p,"Road",-48,90,96,8,"ground");Pad(p,"South sidewalk",-42,88,84,2);Pad(p,"North sidewalk",-42,98,84,2);
        Pad(p,"Store parking",10,76,32,12,"ground");Pad(p,"Overflow parking",-38,65,25,23,"ground");
        for(float x=-45;x<48;x+=7)Pad(p,"Road centre stripe",x,93.9f,3,.15f,"yellow");
        for(float x=15;x<=33;x+=3)Pad(p,"Parking stripe",x,77,.1f,5);Pad(p,"Parking end stripe",15,77,18,.1f);
        for(float z=90;z<98;z+=1.3f)Pad(p,"Crosswalk stripe",29,z,3,.6f);
        Vehicle(p,16,78);Vehicle(p,28,78);Vehicle(p,-35,67);Vehicle(p,-25,70);
        Prop(p,"Bus shelter back",-36,86.6f,8,.15f,2.7f,"steel");Box(p,"Bus shelter roof",new Vector3(-32,2.8f,85.5f),new Vector3(8.2f,.15f,3),"roof");Prop(p,"Shelter bench",-35,85.5f,5,.7f,.6f,"timber");
        foreach(float x in new[]{-35.8f,-28.2f})Prop(p,"Shelter post",x,84.1f,.15f,.15f,2.8f,"steel");
        Prop(p,"Neon pylon post",36.3f,84.8f,.35f,.35f,7,"steel");Box(p,"Neon pylon board",new Vector3(36.5f,6,85),new Vector3(6,2.3f,.35f),"roof");
        foreach(float y in new[]{4.9f,7.1f})Box(p,"Neon tube",new Vector3(36.5f,y,85.22f),new Vector3(6,.08f,.08f),"steel",false,neon);
        foreach(float x in new[]{33.55f,39.45f})Box(p,"Neon tube",new Vector3(x,6,85.22f),new Vector3(.08f,2.2f,.08f),"steel",false,neon);
        Sign(p,"QUACK MART",new Vector3(36.5f,6.2f,85.24f),5.6f,true,new Color(1,.45f,.72f));Sign(p,"OPEN 24H",new Vector3(36.5f,5.35f,85.24f),4,true);
        Lamp(p,"Baked neon spill",36.5f,6,84,false,new Color(1,.35f,.6f));
        foreach(float x in new[]{-7f,7f})
        {
            float z=x<0?90:98;Prop(p,"Crossing gate pedestal",x-.35f,z-.35f,.7f,.7f,1.2f,"steel");
            // Raised static gate: no animation, no obstruction across pedestrian/rail routes.
            Prop(p,"Raised crossing gate",x-.12f,z-.12f,.24f,.24f,4,"yellow",1.2f);
            Box(p,"Crossing signal head",new Vector3(x,2.8f,z),new Vector3(1.3f,.5f,.25f),"steel");
            Sign(p,"RAIL",new Vector3(x,3.5f,z+.15f),2,true);
        }
        foreach(float x in new[]{-11f,11f}){Prop(p,"Stop sign post",x,88.2f,.12f,.12f,2.6f,"steel");Box(p,"Stop sign",new Vector3(x,2.25f,88.2f),new Vector3(.8f,.8f,.1f),"prop");Sign(p,"STOP",new Vector3(x,2.25f,88.27f),.75f,true);}
        foreach(float z in new[]{86f,87.5f,89f})Prop(p,"Traffic bollard",41.2f,z,.35f,.35f,.75f,"yellow");
        Lamp(p,"Baked parking lamp",12,5,85,true);Lamp(p,"Baked overflow lamp",-15,5,86,true);
    }
    static void Outer(Transform root)
    {
        var p=Group(root,"Outer thirds");Pad(p,"West roadside path",-40,10,5,140);Pad(p,"East approach path",35,10,5,42);Pad(p,"East departure path",35,120,5,30);
        Pad(p,"Old car lot",-32,18,19,24,"ground");foreach(float z in new[]{21f,30f}){Vehicle(p,-28,z);Vehicle(p,-20,z+2);}
        Pad(p,"Utility pad",16,25,14,12);Prop(p,"Utility cabinet A",19,28,4,5,2.1f,"steel");Prop(p,"Utility cabinet B",25,30,3,4,1.8f,"steel");
        Pad(p,"Auto shop apron",-32,115,20,10,"ground");Vehicle(p,-27,118);
        var shop=Group(p,"Shuttered auto shop");Wall(shop,true,-31,125,137);Wall(shop,true,-13,125,137);Wall(shop,false,137,-31,-13);Wall(shop,false,125,-31,-13);
        Box(shop,"Roof",new Vector3(-22,3.8f,131),new Vector3(18.5f,.25f,12.5f),"roof");Box(shop,"Closed shutter",new Vector3(-22,1.4f,124.85f),new Vector3(9,2.8f,.1f),"steel");Sign(shop,"AUTO REPAIR / CLOSED",new Vector3(-22,3.15f,124.72f),16,false);
        Pad(p,"Delivery parking",13,128,18,10,"ground");Vehicle(p,17,131,true);
        foreach(float z in new[]{14f,26f,38f,120f,139f,147.5f})foreach(float x in new[]{-33f,32f})
        {foreach(float zz in new[]{z,z+3,z+6})Prop(p,"Fence remnant post",x,zz,.15f,.15f,1.5f,"timber");foreach(float y in new[]{.55f,1.2f})Box(p,"Broken fence rail",new Vector3(x,y,z+3),new Vector3(.12f,.12f,6),"timber");}
        foreach(float z in new[]{12f,19f,47f,53f,123f,145f,151f})foreach(float x in new[]{-43f,43f})Tree(p,x,z);
        foreach(float z in new[]{12f,18f,46f,49f,145f,151f}){Tree(p,22,z);Tree(p,-25,z+1);}
        Lamp(p,"Baked auto shop lamp",-22,4.2f,123,true);
    }
    static void Tree(Transform p,float x,float z)
    {
        var t=Group(p,"Pine silhouette");Prop(t,"Trunk",x-.25f,z-.25f,.5f,.5f,4,"timber");
        for(int tier=0;tier<3;tier++)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/CampgroundGreybox/Meshes/Pine-tier-"+tier+".asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville foliage.mat");
            if(!mesh||!material)throw new InvalidOperationException("Missing reusable campground tree asset.");
            var go=Group(t,"Needles").gameObject;go.transform.localPosition=new Vector3(x,1.7f+tier*1.5f,z);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.15f;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
        }
    }
}
#endif






