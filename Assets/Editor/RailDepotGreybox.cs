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

/// <summary>Explicit, depot-only authoring pass for the approved scaled study.</summary>
public static class RailDepotGreybox
{
    public const string PrefabPath = "Assets/DuckRails/Blocks/maintenance-depot-v1.prefab";
    public const string BakeScene = "Assets/DuckRails/Lighting/Blocks/maintenance-depot-v1/Bake.unity";
    public const string RootName = "Depot greybox v3";
    const string Folder = "Assets/DuckRails/DepotGreybox";
    static Material walls, roofs, paving, fence, lens;
    static Mesh cube;

    [MenuItem("Duck Rails/Authoring/Depot/Install scaled greybox (resets depot layout)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Exit Play/bake first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        walls = Material("Walls", new Color(.58f,.60f,.62f));
        roofs = Material("Roof", new Color(.35f,.37f,.39f));
        paving = Material("Paths", new Color(.48f,.50f,.49f));
        fence = Material("Fence", new Color(.38f,.40f,.41f));
        lens = Material("Lamp lens", new Color(.85f,.84f,.72f));
        lens.EnableKeyword("_EMISSION"); lens.SetColor("_EmissionColor",new Color(.7f,.65f,.4f));
        cube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/maintenance-depot-v1/Cube Block UV.asset");
        if (!cube) throw new InvalidOperationException("Missing depot UV cube.");
        var scene = EditorSceneManager.OpenScene(BakeScene);
        var block = Object.FindFirstObjectByType<RailBlock>();
        if (!block || block.id != "maintenance-depot-v1") throw new InvalidOperationException("Wrong bake scene.");
        var beforeTrack = block.track.ToArray();
        // Only the original placeholder layout and this pass's owned subtree are replaced.
        foreach (Transform t in block.transform.Cast<Transform>().ToArray())
            if (t.name.StartsWith("Repair depot") || t.name == "Workbench" || t.name == "Service trench" ||
                t.name == "Left-side buildings" || t.name == "Baked block sample lights" ||
                t.name == "Trackside post" || t.name.StartsWith("Depot greybox v"))
                Object.DestroyImmediate(t.gameObject);
        var root = Group(block.transform, RootName);
        PartsStore(root); Workshop(root); RepairShed(root);
        var yard = Group(root,"Work yard 20 x 16");
        Patch(yard,"Yard footprint",26,106,20,16);
        Fence(yard,16,114,36,114); Fence(yard,36,98,36,114); Fence(yard,16,98,26,98); Fence(yard,30,98,36,98);
        Fence(yard,16,98,16,103); Fence(yard,16,109,16,114);
        Sign(yard,"WORK YARD",new Vector3(16,2,111),Quaternion.Euler(0,90,0),2.5f);
        var loading = Group(root,"Loading space 16 x 10");
        Patch(loading,"Loading footprint",-24,83,16,10);
        Fence(loading,-32,78,-32,88); Fence(loading,-32,78,-16,78); Fence(loading,-32,88,-16,88);
        var service = Group(root,"Service passage 24 x 4");
        Patch(service,"Service footprint",28,55,24,4);
        Fence(service,16,53,40,53);
        var paths=Group(root,"Flush walking paths - 3 m");
        Path(paths,-12,65,-12,101); Path(paths,-24,65,-12,65); Path(paths,-24,101,-12,101);
        Path(paths,-24,83,-12,83); Path(paths,-12,80,10,80); Path(paths,10,55,10,106);
        Path(paths,10,80,24,80); Path(paths,10,106,26,106); Path(paths,10,55,28,55);
        Path(paths,26,55,26,63); Path(paths,28,95,28,103);
        var lights=Group(root,"Six baked scale-study lights");
        Lamp(lights,"L1 Parts store",-15.5f,3.5f,101,new Vector3(-12,0,101),false);
        Lamp(lights,"L2 Workshop",-15.5f,3.5f,65,new Vector3(-12,0,65),false);
        Lamp(lights,"L3 Bay",16,5,80,new Vector3(20,0,80),false);
        Lamp(lights,"L4 Yard",18,4,108,new Vector3(27,0,106),true);
        Lamp(lights,"L5 Shed",30,5.5f,76,new Vector3(27,0,76),false);
        Lamp(lights,"L6 Side exit",26,3.5f,58.5f,new Vector3(26,0,55),false);
        // Reposition existing markers; preserve stable IDs, arrays, counts and nested marker prefabs.
        for (int v=0;v<block.variations.Length;v++)
        {
            float shift=v-1;
            var coal=new[]{new Vector3(-27+shift,3.5f,102.7f),new Vector3(-24+shift,3.5f,69),new Vector3(-25+shift,.3f,83),
                new Vector3(26+shift,.3f,85),new Vector3(29+shift,.3f,109),new Vector3(28+shift,4.3f,69)};
            var enemies=new[]{new Vector3(11,.2f,49),new Vector3(12,.2f,111),new Vector3(27,.2f,68),
                new Vector3(31,.2f,85),new Vector3(24,.2f,106),new Vector3(11,.2f,94)};
            for(int i=0;i<block.variations[v].coal.Length;i++) SetPosition(block.variations[v].coal[i],coal[i]);
            for(int i=0;i<block.variations[v].enemies.Length;i++) SetPosition(block.variations[v].enemies[i],enemies[i]+Vector3.right*shift*.5f);
            for(int i=0;i<block.variations[v].gold.Length;i++) SetPosition(block.variations[v].gold[i],i==0?new Vector3(-37,3.5f,99+v):new Vector3(31,4.3f,90+v*.5f));
        }
        var baker=block.GetComponentInChildren<CrawlGraphBaker>();
        baker.transform.localPosition=new Vector3(26,4,80); baker.size=new Vector3(36,12,96);
        block.enemyBounds=new Bounds(new Vector3(26,4,80),new Vector3(34,12,70));
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        baker.seeds=block.variations.SelectMany(v=>v.enemies).ToArray();
        Physics.SyncTransforms();
        if (!CrawlGraphBake.Bake(baker,false)) throw new InvalidOperationException("Depot graph bake failed.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        // Geometry replacement invalidates old bindings; the queued bake/export supplies current bindings.
        block.GetComponent<RailBakedBlockLighting>().bindings=Array.Empty<RailBakedBlockLighting.Binding>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(block.GetComponent<RailBakedBlockLighting>());
        PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
        if (!beforeTrack.SequenceEqual(block.track)) throw new InvalidOperationException("Track changed.");
        // Fresh probes avoid putting old probes inside the new shells.
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();
        var positions=new List<Vector3>();
        for(float z=1;z<160;z+=8) foreach(float x in new[]{-40f,-26f,-12f,0f,10f,20f,30f,40f})
            foreach(float y in new[]{.6f,2f,3.8f,4.6f,6f,7.5f})
            {var p=new Vector3(x,y,z);if(!Physics.CheckSphere(p,.2f,1<<6,QueryTriggerInteraction.Ignore))positions.Add(p);}
        probes.probePositions=positions.ToArray();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        RailCatalogLightingAuthoring.BakeOpen();
        Debug.Log("Depot greybox installed; graph baked; block lighting bake/export queued.");
    }
    static Material Material(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",0);m.SetFloat("_Metallic",0);
        return m;
    }
    static Transform Group(Transform parent,string name)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material material,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.layer=6;g.transform.SetParent(parent,false);
        g.transform.localPosition=pos;g.transform.localScale=size;
        g.GetComponent<MeshFilter>().sharedMesh=cube;
        var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveGI=ReceiveGI.Lightmaps;
        r.scaleInLightmap=collision?.5f:.15f;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);
        if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static void Patch(Transform p,string n,float x,float z,float w,float d)
    {Box(p,n,new Vector3(x,.008f,z),new Vector3(w,.008f,d),paving,false);}
    static void Path(Transform p,float x1,float z1,float x2,float z2)
    {
        var delta=new Vector3(x2-x1,0,z2-z1);
        var g=Box(p,"Path 3 m",new Vector3((x1+x2)*.5f,.018f,(z1+z2)*.5f),new Vector3(3,.008f,delta.magnitude+3),paving,false);
        g.transform.localRotation=Quaternion.LookRotation(delta);
    }
    // V3 blueprint coordinates: each building origin is its southwest corner.
    static Transform Shell(Transform root,string name,float x,float z,float w,float d,float h)
    {
        var b=Group(root,name);b.localPosition=new Vector3(x,0,z);
        Patch(b,"Footprint "+w+" x "+d,w/2,d/2,w,d);
        Box(b,"Simple roof",new Vector3(w/2,h+.1f,d/2),new Vector3(w,.2f,d),roofs);
        return b;
    }
    // Openings are exact clear intervals. Headers retain 2.6 m interior headroom.
    static void Wall(Transform p,bool vertical,float fixedAxis,float start,float end,float y,float h,params float[] doors)
    {
        float cursor=start;
        for(int i=0;i<=doors.Length;i+=2)
        {
            float stop=i<doors.Length?doors[i]:end;
            Segment(cursor,stop,y,h);
            if(i<doors.Length){if(h>2.6f)Segment(stop,doors[i+1],y+2.6f,h-2.6f);cursor=doors[i+1];}
        }
        void Segment(float a,float b,float floor,float height)
        {
            if(b-a<.001f)return;
            Box(p,"Wall",vertical?new Vector3(fixedAxis,floor+height/2,(a+b)/2):new Vector3((a+b)/2,floor+height/2,fixedAxis),
                vertical?new Vector3(.2f,height,b-a):new Vector3(b-a,height,.2f),walls);
        }
    }
    static void Prop(Transform p,string name,float x,float z,float w,float d,float y=0,float h=1.2f)
    {Box(p,name,new Vector3(x+w/2,y+h/2,z+d/2),new Vector3(w,h,d),paving);}
    static void Room(Transform p,string name,float x,float z,float y)
    {Sign(p,name,new Vector3(x,y+.035f,z),Quaternion.Euler(90,0,0),6);}
    static void Fill(Transform p,float x,float z,float y)
    {Lamp(p,"Interior study light",x,y,z,p.TransformPoint(new Vector3(x,y-2,z)),false);}
    static void Guard(Transform p,Vector3 a,Vector3 b)
    {
        var delta=b-a;
        // Solid low guards are deliberate greybox collision, including for crouched ducks.
        var g=Box(p,"Guard 1.1 m",(a+b)/2+Vector3.up*.55f,new Vector3(.12f,1.1f,delta.magnitude),fence);
        g.transform.localRotation=Quaternion.LookRotation(delta);
    }
    static void Deck(Transform p,float w,float d,float y,params Rect[] holes)
    {
        var deck=Group(p,"Floor 2 deck");
        var xs=new[]{0f,w}.Concat(holes.SelectMany(r=>new[]{r.xMin,r.xMax})).Distinct().OrderBy(v=>v).ToArray();
        var zs=new[]{0f,d}.Concat(holes.SelectMany(r=>new[]{r.yMin,r.yMax})).Distinct().OrderBy(v=>v).ToArray();
        for(int i=0;i<xs.Length-1;i++)for(int j=0;j<zs.Length-1;j++)
        {
            var mid=new Vector2((xs[i]+xs[i+1])/2,(zs[j]+zs[j+1])/2);
            if(holes.Any(r=>r.Contains(mid)))continue;
            Box(deck,"Upper slab",new Vector3(mid.x,y-.1f,mid.y),new Vector3(xs[i+1]-xs[i],.2f,zs[j+1]-zs[j]),roofs);
        }
    }
    static void VoidGuard(Transform p,Rect r,float y)
    {
        Guard(p,new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin));
        Guard(p,new Vector3(r.xMin,y,r.yMax),new Vector3(r.xMax,y,r.yMax));
        Guard(p,new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMin,y,r.yMax));
        Guard(p,new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax));
    }
    static void Stairs(Transform p,string name,Rect r,float rise)
    {
        var t=Group(p,"Stair "+name);int count=Mathf.CeilToInt(rise/.18f);
        for(int i=0;i<count;i++)
        {
            float h=rise*(i+1)/count;
            Box(t,"Tread "+(i+1),new Vector3(r.center.x,h/2,r.yMin+(i+.5f)*r.height/count),
                new Vector3(r.width,h,r.height/count),paving);
        }
        foreach(float x in new[]{r.xMin,r.xMax})
        {
            Guard(t,new Vector3(x,0,r.yMin),new Vector3(x,rise,r.yMax));
            Guard(t,new Vector3(x,rise,r.yMin),new Vector3(x,rise,r.yMax));
        }
        Guard(t,new Vector3(r.xMin,rise,r.yMin),new Vector3(r.xMax,rise,r.yMin));
    }
    static void PartsStore(Transform root)
    {
        var b=Shell(root,"Parts store",-42.4f,90.2f,26.4f,21.6f,7.2f);
        Wall(b,true,.1f,0,21.6f,0,7.2f);Wall(b,true,26.3f,0,21.6f,0,7.2f,7.8f,13.8f);
        Wall(b,false,.1f,0,26.4f,0,7.2f,3,6);Wall(b,false,21.5f,0,26.4f,0,7.2f);
        var a=new Rect(10,2.4f,2.4f,6);var c=new Rect(22,2.4f,2.4f,6);
        Deck(b,26.4f,21.6f,3.2f,a,c);Stairs(b,"A",a,3.2f);Stairs(b,"B",c,3.2f);
        var f=Group(b,"Floor 1 rooms and props");
        Wall(f,true,9,0,21.6f,0,3,8,10.4f,16,18.4f);Wall(f,false,13.6f,0,9,0,3,4,6.4f);
        Prop(f,"Bulk pallets",1,2,3,3);Prop(f,"Bulk racks",1,7,3,3,0,2);Prop(f,"Returns crates",1,15,3,2);
        Prop(f,"Picking rack",13,14,1.4f,4,0,2);Prop(f,"Picking rack",17,14,1.4f,4,0,2);
        Prop(f,"Picking rack",15,3,1.4f,4,0,2);Prop(f,"Picking rack",18.5f,3,1.4f,4,0,2);Prop(f,"Counter",22,14,2,4);
        Room(f,"BULK STOCK",5,11,0);Room(f,"RETURNS",5,20,0);Room(f,"PICKING HALL",18,12,0);
        Fill(b,5,6,2.8f);Fill(b,5,18,2.8f);Fill(b,17,11,2.8f);Fill(b,20,19,2.8f);
        f=Group(b,"Floor 2 rooms and props");
        Wall(f,true,9,0,21.6f,3.2f,4,9,11.4f);Wall(f,false,14.4f,0,9,3.2f,4,4,6.4f);
        Wall(f,false,7,0,9,3.2f,4,4,6.4f);Wall(f,false,14.4f,9,26.4f,3.2f,4,16,18.4f);
        Prop(f,"Archive files",1,16,2,3,3.2f,2);Prop(f,"Archive files",6,16,2,3,3.2f,2);
        Prop(f,"Office desk",1,9,3,1.5f,3.2f,.9f);Prop(f,"Staff table",4,2,3,1.5f,3.2f,.9f);
        Prop(f,"Overflow crates",11,17,3,2,3.2f);Prop(f,"Overflow racks",21,16,2,4,3.2f,2);Prop(f,"Sort table",15,9,4,1.5f,3.2f,.9f);
        Room(f,"ARCHIVE",4,20,3.2f);Room(f,"OFFICE",5,12,3.2f);Room(f,"STAFF ROOM",5,5,3.2f);Room(f,"SORTING",18,12,3.2f);
        Fill(b,4,18,6.8f);Fill(b,5,11,6.8f);Fill(b,5,4,6.8f);Fill(b,17,11,6.8f);Fill(b,17,19,6.8f);
        Sign(b,"PARTS STORE",new Vector3(26.5f,3.7f,10.8f),Quaternion.Euler(0,-90,0),5);
    }
    static void Workshop(Transform root)
    {
        var b=Shell(root,"Workshop",-35.2f,54.2f,19.2f,21.6f,6.8f);
        Wall(b,true,.1f,0,21.6f,0,6.8f,10,12.4f);Wall(b,true,19.1f,0,21.6f,0,6.8f,7.8f,13.8f);
        Wall(b,false,.1f,0,19.2f,0,6.8f);Wall(b,false,21.5f,0,19.2f,0,6.8f);
        var a=new Rect(1,2.4f,2.4f,6);var c=new Rect(15.5f,12,2.4f,6);var v=new Rect(6,8,7,5.6f);
        Deck(b,19.2f,21.6f,3.2f,a,c,v);Stairs(b,"A",a,3.2f);Stairs(b,"B",c,3.2f);VoidGuard(b,v,3.2f);
        var f=Group(b,"Floor 1 rooms and props");
        Wall(f,false,15,0,8,0,3,4,6.4f);Wall(f,true,8,15,21.6f,0,3);
        Wall(f,true,6,0,8,0,3,3,5.4f);Wall(f,false,8,6,12,0,3);Wall(f,true,12,0,8,0,3);
        Prop(f,"Office desk",1,17,3,1.5f,0,.9f);Prop(f,"Tools",7,1,1.2f,3,0,2);Prop(f,"Repair bench",7,10.8f,4,1.5f,0,.9f);
        Prop(f,"Machine",13,3,3,2,0,1.6f);Prop(f,"Bench",10,18,3,1.5f,0,.9f);
        Room(f,"OFFICE",4,20,0);Room(f,"TOOL CAGE",9,6,0);Room(f,"REPAIR FLOOR",10,14,0);
        Fill(b,4,19,2.8f);Fill(b,9,5,2.8f);Fill(b,14,10,2.8f);Fill(b,12,19,2.8f);Fill(b,3,10,2.8f);
        f=Group(b,"Floor 2 rooms and props");Wall(f,false,16,0,9,3.2f,3.6f,5,7.4f);Wall(f,true,9,16,21.6f,3.2f,3.6f);
        Prop(f,"Supervisor desk",1,18,3,1.5f,3.2f,.9f);Prop(f,"Lockers",10,18,2,1,3.2f,1.8f);Prop(f,"Parts bins",6,2,6,1.3f,3.2f);
        Room(f,"SUPERVISOR",5,20,3.2f);Room(f,"INSPECTION GALLERY",10,6,3.2f);
        Fill(b,4,19,6.4f);Fill(b,14,19,6.4f);Fill(b,10,5,6.4f);Fill(b,3,11,6.4f);Fill(b,16,10,6.4f);
        Sign(b,"WORKSHOP",new Vector3(19.3f,3.7f,10.8f),Quaternion.Euler(0,-90,0),5);
    }
    static void RepairShed(Transform root)
    {
        var b=Shell(root,"Repair shed",16,59,24,36,8.4f);
        // The track-facing bay retains 4 m headroom.
        Wall(b,true,.1f,0,17,0,8.4f);Wall(b,true,.1f,25,36,0,8.4f);Wall(b,true,.1f,17,25,4,4.4f);
        Wall(b,true,23.9f,0,36,0,8.4f);Wall(b,false,.1f,0,24,0,8.4f,8,12);Wall(b,false,35.9f,0,24,0,8.4f,10,14);
        var a=new Rect(19,2.4f,2.4f,7.2f);var c=new Rect(19,26.4f,2.4f,7.2f);var v=new Rect(3,11,14,14);
        Deck(b,24,36,4,a,c,v);Stairs(b,"A",a,4);Stairs(b,"B",c,4);VoidGuard(b,v,4);
        var f=Group(b,"Floor 1 rooms and props");
        Wall(f,false,28,0,9,0,3.8f,4,6.4f);Wall(f,true,9,28,36,0,3.8f);
        Wall(f,true,17,12,24,0,3.8f,14,16.4f,20,22.4f);foreach(float z in new[]{12f,18f,24f})Wall(f,false,z,17,24,0,3.8f);
        Prop(f,"Dispatch desk",1,30,3,1.5f,0,.9f);Prop(f,"Tool racks",21,13,1.2f,3,0,2);Prop(f,"Compressor",20,19,2.5f,2,0,1.7f);
        Prop(f,"Repair stand",5,10,4,5,0,1.1f);Prop(f,"Repair stand",11,18,4,5,0,1.1f);Prop(f,"Crates",2,2,3,2);
        Room(f,"DISPATCH",5,34,0);Room(f,"TOOLS",20,16.8f,0);Room(f,"COMPRESSOR",20,23,0);Room(f,"REPAIR BAY",10,26,0);
        Fill(b,5,33,3.6f);Fill(b,20,15,3.6f);Fill(b,20,22,3.6f);Fill(b,12,8,3.6f);Fill(b,12,28,3.6f);Fill(b,21,3,3.6f);
        f=Group(b,"Floor 2 rooms and props");
        Wall(f,false,28,0,17,4,4.4f,4,6.4f,11,13.4f);Wall(f,true,9,28,36,4,4.4f);
        Wall(f,false,8,0,17,4,4.4f,6,8.4f,12,14.4f);Wall(f,true,10,0,8,4,4.4f);
        Prop(f,"Shift office desk",1,31,3,1.5f,4,.9f);Prop(f,"Break table",11,31,3,1.5f,4,.9f);
        Prop(f,"Spare racks",1,2,2,3,4,2);Prop(f,"Control console",11,2,4,1.4f,4,1.1f);
        Room(f,"SHIFT OFFICE",5,34,4);Room(f,"BREAK ROOM",13,34,4);Room(f,"SPARE PARTS",6,6,4);Room(f,"CONTROL",13,6,4);
        Fill(b,5,33,8);Fill(b,13,33,8);Fill(b,5,4,8);Fill(b,13,4,8);Fill(b,21,16,8);Fill(b,2,18,8);Fill(b,10,26,8);Fill(b,20,33,8);
        Sign(b,"REPAIR SHED",new Vector3(-.1f,4.6f,21),Quaternion.Euler(0,90,0),6);
    }
    static void Fence(Transform p,float x1,float z1,float x2,float z2)
    {
        Vector3 a=new Vector3(x1,0,z1),b=new Vector3(x2,0,z2),d=b-a;
        int n=Mathf.CeilToInt(d.magnitude/4);
        for(int i=0;i<=n;i++)Box(p,"Fence post 1.4 m",Vector3.Lerp(a,b,i/(float)n)+Vector3.up*.7f,new Vector3(.16f,1.4f,.16f),fence);
        foreach(float y in new[]{.45f,1.15f})
        {var g=Box(p,"Fence rail",(a+b)/2+Vector3.up*y,new Vector3(.12f,.12f,d.magnitude),fence);g.transform.localRotation=Quaternion.LookRotation(d);}
    }
    static void Sign(Transform p,string text,Vector3 position,Quaternion rotation,float width)
    {
        var t=Group(p,text).gameObject.AddComponent<TextMeshPro>();
        t.text=text;t.fontSize=3;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;
        t.rectTransform.sizeDelta=new Vector2(width,.6f);t.transform.localPosition=position;t.transform.localRotation=rotation;
    }
    static void Lamp(Transform p,string name,float x,float y,float z,Vector3 target,bool post)
    {
        var t=Group(p,name);t.localPosition=new Vector3(x,y,z);
        if(post)Box(p,"Yard lamp post",new Vector3(x,y/2,z),new Vector3(.14f,y,.14f),fence);
        var fixture=Box(t,"Fixture",Vector3.zero,new Vector3(.6f,.15f,.3f),lens,false);
        fixture.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        var g=Group(t,"Baked light");g.localPosition=Vector3.down*.2f;
        g.localRotation=Quaternion.LookRotation(target-g.position);
        var l=g.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;
        l.color=new Color(.88f,.91f,1);l.intensity=60;l.range=22;l.spotAngle=120;l.innerSpotAngle=70;l.shadows=LightShadows.Soft;
    }
    static void SetPosition(Transform t,Vector3 p)
    {t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}

    [MenuItem("Duck Rails/Authoring/Depot/Load depot-first review route")]
    public static void ReviewRoute()
    {
        var catalog=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/DuckRails/Blocks"})
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Select(g=>g.GetComponent<RailBlock>()).Where(b=>b&&b.id!="bridge-finale-v1").ToArray();
        int seed=0;
        while(RailRoute.Choose("review",catalog,seed).blocks[0]!="maintenance-depot-v1")seed++;
        var route=RailRoute.Choose("review",catalog,seed);
        RailTestTools.LoadPreset(new RailTestPreset {seed=seed,route=JsonUtility.ToJson(route),scenario="Depot scale review",roles="Manual VR review"});
        Debug.Log("Depot-first preset loaded for the next private run. Clear via Test Center > Restore normal random routes.");
    }
}
#endif
