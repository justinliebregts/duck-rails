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

/// <summary>Explicit, motel-only authoring pass for the approved scaled study.</summary>
public static class RailMotelGreybox
{
    public const string PrefabPath = "Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
    public const string BakeScene = "Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/roadside-motel-v1-bake.unity";
    public const string RootName = "Motel greybox v1";
    const string Folder = "Assets/DuckRails/MotelGreybox";
    static Material walls, roofs, paving, fence, lens;
    static Mesh cube;

    [MenuItem("Duck Rails/Authoring/Motel/Install scaled greybox (resets motel layout)")]
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
        cube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");
        if (!cube) throw new InvalidOperationException("Missing motel UV cube.");
        var scene = EditorSceneManager.OpenScene(BakeScene);
        var block = Object.FindFirstObjectByType<RailBlock>();
        if (!block || block.id != "roadside-motel-v1") throw new InvalidOperationException("Wrong bake scene.");
        var beforeTrack = block.track.ToArray();
        // Only the original placeholder layout and this pass's owned subtree are replaced.
        foreach (Transform t in block.transform.Cast<Transform>().ToArray())
            if (t.name.StartsWith("Motel room ") || t.name == "Courtyard planter" || t.name == "MOTEL\nVACANCY" ||
                t.name == "Left-side buildings" || t.name == "Baked block sample lights" || t.name.StartsWith("Motel greybox v"))
                Object.DestroyImmediate(t.gameObject);
        var root = Group(block.transform, RootName);
        BuildMotel(root); BuildOffice(root); BuildYard(root); BuildExterior(root);
        RailMotelEdgeBanks.Build(root);
        // Reposition existing markers; preserve stable IDs, arrays, counts and nested marker prefabs.
        for (int v=0;v<block.variations.Length;v++)
        {
            float shift=v-1;
            var coal=new[]{new Vector3(-30+shift,.3f,96),new Vector3(-20+shift,.3f,84),new Vector3(-30+shift,.3f,59),
                new Vector3(33+shift,.3f,66),new Vector3(34+shift,3.9f,84),new Vector3(15+shift,3.9f,103)};
            var enemies=new[]{new Vector3(14,.2f,55),new Vector3(17,.2f,80),new Vector3(33,.2f,66),
                new Vector3(34,.2f,89),new Vector3(17,.2f,104),new Vector3(23,.2f,110)};
            for(int i=0;i<block.variations[v].coal.Length;i++) SetPosition(block.variations[v].coal[i],coal[i]);
            for(int i=0;i<block.variations[v].enemies.Length;i++) SetPosition(block.variations[v].enemies[i],enemies[i]+Vector3.right*shift*.5f);
            for(int i=0;i<block.variations[v].gold.Length;i++) SetPosition(block.variations[v].gold[i],i==0?new Vector3(-20,.3f,95+v):new Vector3(32.5f,3.9f,103+v*.5f));
        }
        var baker=block.GetComponentInChildren<CrawlGraphBaker>();
        // Keep the shared catalogue origin (8,-2,32); assembly translates whole 16-cell chunks.
        baker.transform.localPosition=new Vector3(27,4,80); baker.size=new Vector3(38,12,96);
        block.enemyBounds=new Bounds(new Vector3(27,4,80),new Vector3(36,12,70));
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        baker.seeds=block.variations.SelectMany(v=>v.enemies).ToArray();
        Physics.SyncTransforms();
        if (!CrawlGraphBake.Bake(baker,false)) throw new InvalidOperationException("Motel graph bake failed.");
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
        Debug.Log("Motel greybox installed; graph baked; block lighting bake/export queued.");
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
    static void BuildMotel(Transform root)
    {
        var m=Group(root,"Motel");
        Patch(m,"Courtyard",17.5f,73,15,42);
        Box(m,"East upper slab",new Vector3(34,3.5f,74),new Vector3(12,.2f,40),roofs);
        Box(m,"North upper slab",new Vector3(25,3.5f,103),new Vector3(30,.2f,12),roofs);
        var roof=Group(m,"Roofs");
        Box(roof,"East roof",new Vector3(34,7.3f,74),new Vector3(12,.2f,40),roofs);
        Box(roof,"North roof",new Vector3(25,7.3f,103),new Vector3(30,.2f,12),roofs);
        for(int floor=0;floor<2;floor++)
        {
            float y=floor*3.6f;float wallHeight=floor==0?3.4f:3.6f; var f=Group(m,"Floor "+(floor+1));
            Wall(f,true,39.9f,54,94,y,wallHeight);
            for(int i=0;i<=5;i++) Wall(f,false,i==0?54.1f:i==5?93.9f:54+i*8,28,40,y,wallHeight);
            for(int i=0;i<5;i++)
            {
                float z=54+i*8; int id=(floor+1)*100+i+1;
                // Three-metre opening leaves >=2.4 m useful passage beside the inward ajar leaf.
                Wall(f,true,28.1f,z,z+8,y,wallHeight,z+1,z+4);
                if(i%2==0)
                {
                    var door=Box(f,"Ajar door "+id,new Vector3(29.3f,y+1.25f,z+1.25f),new Vector3(2.4f,2.5f,.1f),fence);
                    door.transform.localRotation=Quaternion.Euler(0,-10,0);
                }
                Wall(f,false,z+4,36,40,y,wallHeight);Wall(f,true,36,z+4,z+8,y,wallHeight,z+4.6f,z+7);
                Prop(f,"Bed "+id,32,z+1,3,2.2f,y,.7f);Prop(f,"Desk "+id,38,z+.5f,1.3f,2.5f,y,.8f);
                Prop(f,"Bathroom fixture "+id,38.3f,z+5,1,1,y,.6f);
                Room(f,id.ToString(),30.5f,z+5.5f,y);
                Fill(f,32,z+5.5f,y+3.1f);
            }
            Wall(f,false,108.9f,10,40,y,wallHeight);
            if(floor==0)Wall(f,true,10.1f,97,109,y,wallHeight,101,103.4f);
            else Wall(f,true,10.1f,97,109,y,wallHeight);
            Wall(f,true,39.9f,97,109,y,wallHeight);
            Wall(f,true,20,97,109,y,wallHeight);Wall(f,true,30,97,109,y,wallHeight);
            for(int i=0;i<3;i++)
            {
                float x=10+i*10;
                Wall(f,false,97.1f,x,x+10,y,wallHeight,x+1,x+3.4f);
                if(floor==1)
                {
                    Wall(f,true,x+6,105,109,y,wallHeight);Wall(f,false,105,x+6,x+10,y,wallHeight,x+6.5f,x+8.9f);
                    Prop(f,"Bed "+(206+i),x+5,100,3,2.2f,y,.7f);Prop(f,"Desk "+(206+i),x+.5f,106,2,1.2f,y,.8f);
                    Room(f,(206+i).ToString(),x+3,103,y);
                }
                else
                {
                    Room(f,new[]{"RECEPTION","LAUNDRY","LINEN"}[i],x+4,104,0);
                    if(i==0){Prop(f,"Reception counter",15,100,1.2f,5);Prop(f,"Reception sofa",11,105,3,1.2f,0,.7f);}
                    if(i==1){Prop(f,"Washers",27.5f,100,1.5f,5);Prop(f,"Folding table",23,103,2,1.5f,0,.8f);}
                    if(i==2){Prop(f,"Linen racks",38,100,1.2f,6,0,2);Prop(f,"Staff table",34,103,2,1.5f,0,.8f);}
                }
                Fill(f,x+4,103,y+3.1f);
            }
        }
        var balcony=Group(m,"Balcony");
        Box(balcony,"East walk",new Vector3(26.5f,3.5f,74),new Vector3(3,.2f,40),roofs);
        Box(balcony,"North walk",new Vector3(25,3.5f,95.5f),new Vector3(30,.2f,3),roofs);
        // Outside stair landings join the balcony sideways (A) and at its north edge (B).
        ExteriorStair(m,"A",new Rect(22.6f,54,2.4f,6.4f));
        ExteriorStair(m,"B",new Rect(10,85.2f,2.4f,6.4f));
        Box(balcony,"Landing A",new Vector3(23.8f,3.5f,61.6f),new Vector3(2.4f,.2f,2.4f),roofs);
        Box(balcony,"Landing B",new Vector3(11.2f,3.5f,92.8f),new Vector3(2.4f,.2f,2.4f),roofs);
        Rail(balcony,25,54,25,60.4f);Rail(balcony,25,62.8f,25,94);
        Rail(balcony,25,54,28,54);Rail(balcony,10,94,10,97);Rail(balcony,12.4f,94,25,94);Rail(balcony,40,94,40,97);
        Rail(balcony,22.6f,60.4f,22.6f,62.8f);Rail(balcony,22.6f,62.8f,25,62.8f);
        Rail(balcony,10,91.6f,10,94);Rail(balcony,12.4f,91.6f,12.4f,94);
        foreach(float z in new[]{58f,74f,90f}){Lamp(m,"Balcony fixture",27.5f,6.8f,z,new Vector3(25,3.6f,z),false);Lamp(m,"Walk fixture",27.5f,3.2f,z,new Vector3(24,0,z),false);}
    }
    static void ExteriorStair(Transform p,string name,Rect r)
    {
        var t=Group(p,"Stair "+name);const int count=20;const float rise=3.6f;
        for(int i=0;i<count;i++)
        {
            float h=rise*(i+1)/count;
            Box(t,"Tread "+(i+1),new Vector3(r.center.x,h/2,r.yMin+(i+.5f)*r.height/count),new Vector3(r.width,h,r.height/count),paving);
        }
        foreach(float x in new[]{r.xMin,r.xMax})
            Guard(t,new Vector3(x,0,r.yMin),new Vector3(x,rise,r.yMax));
    }
    // Greybox rails have open visible bars and one invisible collision barrier for duck-sized bodies.
    static void Rail(Transform p,float x,float z,float u,float v)
    {
        var t=Group(p,"Balcony guard");var a=new Vector3(x,3.6f,z);var b=new Vector3(u,3.6f,v);var d=b-a;
        var collider=t.gameObject.AddComponent<BoxCollider>();t.gameObject.layer=6;
        t.localPosition=(a+b)/2+Vector3.up*.55f;t.localRotation=Quaternion.LookRotation(d);
        collider.size=new Vector3(.1f,1.1f,d.magnitude);
        Box(t,"Top rail",new Vector3(0,.5f,0),new Vector3(.1f,.1f,d.magnitude),fence,false);
        for(float q=-d.magnitude/2;q<=d.magnitude/2+.01f;q+=.5f)Box(t,"Vertical bar",new Vector3(0,0,q),new Vector3(.05f,1.1f,.05f),fence,false);
    }
    static void BuildOffice(Transform root)
    {
        var b=Group(root,"Office and garage");
        Box(b,"Roof",new Vector3(-26,3.7f,90),new Vector3(20,.2f,24),roofs);
        Wall(b,true,-35.9f,78,102,0,3.6f);Wall(b,true,-16.1f,78,102,0,3.6f,82,84.4f,95,97.4f);
        Wall(b,false,78.1f,-36,-16,0,3.6f,-33,-27);Wall(b,false,101.9f,-36,-16,0,3.6f);
        Wall(b,true,-24,78,102,0,3.6f,82,84.4f,95,97.4f);
        Wall(b,false,90,-36,-16,0,3.6f,-32,-29.6f,-22,-19.6f);
        Prop(b,"Garage bench",-35,80,1.5f,6,0,.9f);Prop(b,"Garage lift",-31,84,3,3,0,.15f);
        Prop(b,"Office desk",-20,79.5f,3,1.5f,0,.8f);Prop(b,"Office files",-17.8f,86,1,2,0,1.7f);
        Prop(b,"Storage racks",-35,94,1.5f,6,0,2);Prop(b,"Staff table",-22,98,3,1.5f,0,.8f);
        Room(b,"GARAGE",-29,87,0);Room(b,"OFFICE",-20,86,0);Room(b,"STORAGE",-30,99,0);Room(b,"STAFF",-20,94,0);
        foreach(float x in new[]{-30f,-20f})foreach(float z in new[]{84f,96f})Fill(b,x,z,3.2f);
        Sign(b,"OFFICE",new Vector3(-15.95f,3,83.2f),Quaternion.Euler(0,-90,0),3);
    }
    static void BuildYard(Transform root)
    {
        var p=Group(root,"Transformer yard");Patch(p,"Gravel pad",-26,59,32,24);
        Chain(p,"South fence",-42,47,-10,47);Chain(p,"West south",-42,47,-42,57);Chain(p,"West north",-42,61,-42,71);
        Chain(p,"East south",-10,47,-10,55);Chain(p,"East north",-10,59,-10,71);
        Chain(p,"North west",-42,71,-30,71);Chain(p,"North east",-24,71,-10,71);
        Chain(p,"Bent west south",-42,57,-43.3f,56);Chain(p,"Bent west north",-42,61,-43.3f,62);
        Chain(p,"Bent east south",-10,55,-8.7f,54);Chain(p,"Bent east north",-10,59,-8.7f,60);
        int n=0;
        foreach(float x in new[]{-38f,-28f,-18f})foreach(float z in new[]{52f,61f})
        {
            var t=Group(p,"Transformer "+(++n));
            Box(t,"Concrete base",new Vector3(x+2,.15f,z+2.5f),new Vector3(4,.3f,5),paving);
            Box(t,"Transformer body",new Vector3(x+2,1.5f,z+2.5f),new Vector3(3,2.4f,3.5f),fence);
            for(int i=0;i<6;i++)foreach(float side in new[]{x+.3f,x+3.7f})Box(t,"Cooling fin",new Vector3(side,1.5f,z+.7f+i*.65f),new Vector3(.5f,2,.1f),roofs,false);
            foreach(float dz in new[]{1.5f,3.5f}){Box(t,"Insulator",new Vector3(x+2,3,z+dz),new Vector3(.35f,.65f,.35f),walls);Box(t,"Terminal",new Vector3(x+2,3.4f,z+dz),new Vector3(.55f,.15f,.55f),fence,false);}
        }
        Lamp(p,"Transformer yard light",-40,5,69,new Vector3(-26,0,59),true);
        Lamp(p,"Transformer gate light",-12,5,70,new Vector3(-23,0,59),true);
        Sign(p,"TRANSFORMERS",new Vector3(-35,2,71.1f),Quaternion.Euler(0,180,0),7);
    }
    static void BuildExterior(Transform root)
    {
        var p=Group(root,"Exterior paths and props");
        Patch(p,"Garage apron",-23,74.5f,26,7);Patch(p,"Office approach",-12.5f,90,7,24);
        Patch(p,"Rear service lane",42.5f,82.5f,5,61);Patch(p,"North service lane",27.5f,111,35,4);
        Chain(p,"Rear boundary",45,52,45,113);Chain(p,"North boundary",10,113,45,113);
        foreach(float z in new[]{58f,70f})Vehicle(p,"Abandoned car",11, z,2.7f,4.8f);
        Vehicle(p,"Maintenance van",-19,72,3,5);
        Prop(p,"Apron crates",-14,73,3,3,0,1.3f);
        Prop(p,"Dumpster A",40.4f,65,1.8f,3,0,1.4f);Prop(p,"Dumpster B",40.4f,70,1.8f,3,0,1.4f);
        foreach(float z in new[]{58f,64f,70f,76f})
        {
            Box(p,"Parking stripe",new Vector3(12.5f,.02f,z),new Vector3(5,.01f,.1f),lens,false);
        }
        Box(p,"Motel sign post",new Vector3(11,3,49),new Vector3(.3f,6,.3f),fence);
        Box(p,"Motel sign board",new Vector3(11,5,49),new Vector3(.3f,2,5),walls);
        Sign(p,"MOTEL",new Vector3(10.8f,5,49),Quaternion.Euler(0,90,0),4.5f);
        Lamp(p,"Motel sign lamp",10.7f,6.2f,49,new Vector3(10,3,49),false);
        Lamp(p,"Rear service lamp",40.2f,5,78,new Vector3(43,0,80),false);
    }
    static void Vehicle(Transform p,string name,float x,float z,float w,float d)
    {
        var t=Group(p,name);Prop(t,"Body",x,z,w,d,0,.9f);Prop(t,"Cabin",x+.15f,z+1,w-.3f,d-2,.9f,.8f);
    }
    static void Chain(Transform p,string name,float x,float z,float u,float v)
    {
        var t=Group(p,name);var a=new Vector3(x,0,z);var b=new Vector3(u,0,v);float length=Vector3.Distance(a,b);
        t.localPosition=a;t.localRotation=Quaternion.LookRotation(b-a);
        t.gameObject.layer=6;var c=t.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,1.1f,length/2);c.size=new Vector3(.08f,2.2f,length);
        for(float q=0;q<length;q+=3)Box(t,"Fence post",new Vector3(0,1.15f,q),new Vector3(.1f,2.3f,.1f),fence,false);
        Box(t,"End post",new Vector3(0,1.15f,length),new Vector3(.1f,2.3f,.1f),fence,false);
        foreach(float h in new[]{.1f,2.15f})Box(t,"Fence rail",new Vector3(0,h,length/2),new Vector3(.06f,.06f,length),fence,false);
        // Thin crossed strips form a single double-sided mesh, not thousands of GameObjects.
        var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
        for(int sign=-1;sign<=1;sign+=2)for(float offset=-2.2f;offset<length+2.2f;offset+=.4f)
        {
            float ya=0,yb=2.2f;
            if(sign==1){ya=Mathf.Max(0,-offset);yb=Mathf.Min(2.2f,length-offset);}
            else{ya=Mathf.Max(0,offset-length);yb=Mathf.Min(2.2f,offset);}
            if(yb<=ya)continue;
            var start=new Vector3(0,ya,offset+sign*ya);var end=new Vector3(0,yb,offset+sign*yb);
            var side=new Vector3(0,-sign,1).normalized*.012f;int k=vertices.Count;
            vertices.AddRange(new[]{start-side,start+side,end+side,end-side});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
            indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3,k+2,k+1,k,k+3,k+2,k});
        }
        string path=Folder+"/Fence-"+name.Replace(' ','-')+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool fresh=!mesh;if(fresh)mesh=new Mesh();else mesh.Clear();
        mesh.name=Path.GetFileNameWithoutExtension(path);mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
        if(fresh)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
        var go=Group(t,"Chain-link mesh").gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=fence;
        r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.1f;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
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

    [MenuItem("Duck Rails/Authoring/Motel/Load motel-first review route")]
    public static void ReviewRoute()
    {
        var catalog=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/DuckRails/Blocks"})
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Select(g=>g.GetComponent<RailBlock>()).Where(b=>b&&b.id!="bridge-finale-v1").ToArray();
        int seed=0;
        while(RailRoute.Choose("review",catalog,seed).blocks[0]!="roadside-motel-v1")seed++;
        var route=RailRoute.Choose("review",catalog,seed);
        RailTestTools.LoadPreset(new RailTestPreset {seed=seed,route=JsonUtility.ToJson(route),scenario="Motel scale review",roles="Manual VR review"});
        Debug.Log("Motel-first preset loaded for the next private run. Clear via Test Center > Restore normal random routes.");
    }
}
#endif



