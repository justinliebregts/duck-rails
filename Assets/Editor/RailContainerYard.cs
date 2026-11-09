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
using Object = UnityEngine.Object;

/// <summary>Approved V6: irregular double-depth playground clusters and south freight crane.</summary>
public static class RailContainerYard
{
    public const string Id="container-yard-v1", RootName="Container yard authored v6", Folder="Assets/DuckRails/ContainerYard";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/"+Id+"-bake.unity";
    public const int LightCount=16;
    static readonly Dictionary<string,Mesh> meshes=new();
    static Mesh cube;
    static Material[] paint;
    static Material steel,wood,orange,yellow,rubber,glass,gravel,concrete,grass;
    public static void Guard()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Duck Rails Edit Mode with no bake running");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save pending scene edits first");
    }
    static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Material Mat(string name,string source,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){var src=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville "+source+".mat");if(!src)throw new Exception("Missing material "+source);m=new Material(src);AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.04f);EditorUtility.SetDirty(m);return m;
    }
    static Mesh Save(string name,Mesh m)
    {
        m.name=name;string path=Folder+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!old){AssetDatabase.CreateAsset(m,path);return m;}
        old.Clear();old.indexFormat=m.indexFormat;old.vertices=m.vertices;old.normals=m.normals;old.uv=m.uv;old.uv2=m.uv2;old.subMeshCount=m.subMeshCount;
        for(int i=0;i<m.subMeshCount;i++)old.SetTriangles(m.GetTriangles(i),i);old.bounds=m.bounds;old.UploadMeshData(false);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;
    }
    static GameObject Render(Transform parent,string name,Mesh mesh,Material mat)
    {
        var g=Group(parent,name).gameObject;g.layer=6;g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.25f;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);return g;
    }
    static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat,bool collision=true)
    {
        string key="Box-"+Hash128.Compute(size.ToString("F4"));
        if(!meshes.TryGetValue(key,out var m))
        {
            m=Object.Instantiate(cube);var v=m.vertices;var n=m.normals;var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=(Mathf.Abs(n[i].y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n[i].x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}
            m.uv=uv;m=Save(key,m);meshes[key]=m;
        }
        var g=Render(p,name,m,mat);g.transform.localPosition=pos;g.transform.localScale=size;if(collision)g.AddComponent<BoxCollider>();return g;
    }
    static void Beam(Transform p,string name,Vector3 a,Vector3 b,float width,Material mat,bool collision=true)
    {var g=Box(p,name,(a+b)/2,new(width,(b-a).magnitude,width),mat,collision);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Collider(Transform p,string name,Vector3 center,Vector3 size)
    {var c=Group(p,name).gameObject.AddComponent<BoxCollider>();c.gameObject.layer=6;c.center=center;c.size=size;}

    // One shared corrugated mesh per length/opening type. Ribs are visual; broad collision stays smooth for hands.
    static Mesh ContainerMesh(float length,int opening)
    {
        string key="Container-"+length+"-"+opening;if(meshes.TryGetValue(key,out var m))return m;
        var pieces=new List<CombineInstance>();
        void Part(Vector3 pos,Vector3 size){pieces.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(pos,Quaternion.identity,size)});}
        Part(new(0,.08f,0),new(length,.16f,3));Part(new(0,2.92f,0),new(length,.16f,3));
        foreach(float z in new[]{-1.43f,1.43f})
        {
            Part(new(0,1.5f,z),new(length,2.84f,.14f));
            for(float x=-length/2+.4f;x<length/2-.2f;x+=.45f)Part(new(x,1.5f,z+Mathf.Sign(z)*.085f),new(.09f,2.66f,.06f));
        }
        foreach(float x in new[]{-length/2+.09f,length/2-.09f})foreach(float z in new[]{-1.41f,1.41f})Part(new(x,1.5f,z),new(.18f,3,.18f));
        if(opening==0||opening==2)Part(new(-length/2+.08f,1.5f,0),new(.16f,2.84f,3));
        if(opening==0)Part(new(length/2-.08f,1.5f,0),new(.16f,2.84f,3));
        m=new Mesh();m.CombineMeshes(pieces.ToArray(),true,true);var vs=m.vertices;var ns=m.normals;var uv=new Vector2[vs.Length];
        for(int i=0;i<vs.Length;i++)uv[i]=(Mathf.Abs(ns[i].y)>.5f?new Vector2(vs[i].x,vs[i].z):Mathf.Abs(ns[i].x)>.5f?new Vector2(vs[i].z,vs[i].y):new Vector2(vs[i].x,vs[i].y))/3;
        m.uv=uv;Unwrapping.GenerateSecondaryUVSet(m);m=Save(key,m);meshes[key]=m;return m;
    }
    static Transform Container(Transform parent,string name,Vector3 pos,float length,int opening,int color)
    {
        var p=Group(parent,name);p.localPosition=pos;Render(p,"Corrugated steel shell",ContainerMesh(length,opening),paint[color%paint.Length]);
        if(opening==0)Collider(p,"Closed container collision",new(0,1.5f,0),new(length,3,3));
        else
        {
            Collider(p,"Interior floor",new(0,.08f,0),new(length,.16f,3));Collider(p,"Walkable roof",new(0,2.92f,0),new(length,.16f,3));
            foreach(float z in new[]{-1.43f,1.43f})Collider(p,"Interior side",new(0,1.5f,z),new(length,2.84f,.14f));
            if(opening==2)Collider(p,"Search room rear",new(-length/2+.08f,1.5f,0),new(.16f,2.84f,3));
            // Folded doors stand alongside the opening, outside its usable width.
            foreach(float z in new[]{-1.57f,1.57f})Box(p,"Folded open door",new(length/2-.7f,1.5f,z),new(1.4f,2.8f,.1f),paint[color%paint.Length]);
            Lamp(p,new(0,2.65f,0));
        }
        if(opening==0)foreach(float z in new[]{-.65f,.65f})Box(p,"Door locking bar",new(length/2+.025f,1.5f,z),new(.06f,2.5f,.06f),steel,false);
        return p;
    }
    static void Lamp(Transform p,Vector3 pos)
    {
        Box(p,"Lamp housing",pos,new(.5f,.15f,.35f),yellow,false);var l=Group(p,"Baked yard light").gameObject.AddComponent<Light>();l.transform.localPosition=pos-Vector3.up*.13f;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=17;l.range=13;l.color=new(1,.79f,.51f);l.shadows=LightShadows.Soft;
    }
    static void Steps(Transform root,Vector3 start,Vector3 direction)
    {
        var t=Group(root,"Pallet climb");
        for(int i=0;i<10;i++)
        {
            float h=(i+1)*.3f;Vector3 pos=start+direction*(i+.5f)*.5f+Vector3.up*h/2;
            var g=Box(t,"Pallet step "+i,pos,new(2.4f,h,.5f),wood);g.transform.localRotation=Quaternion.LookRotation(direction);
            var across=Vector3.Cross(Vector3.up,direction);
            for(int j=-1;j<=1;j++){var slat=Box(t,"Pallet slat",pos+Vector3.up*(h/2+.015f)+across*j*.75f,new(.65f,.03f,.47f),wood,false);slat.transform.localRotation=g.transform.localRotation;}
        }
    }
    static void Yard(Transform root,int sign)
    {
        var side=Group(root,sign<0?"West yard":"East yard");
        float[] zs=sign<0?new[]{51f,63f,72.5f,84.5f,94.5f,106.5f}:new[]{51.5f,62f,73f,84f,95f,106f};
        float[] offsets=sign<0?new[]{1.5f,-.5f,1f,0f,1.5f,-1f}:new[]{-1f,1.5f,-.5f,1f,0f,1.5f};
        for(int row=0;row<6;row++)
        {
            var r=Group(side,"Row "+(row+1));bool longInner=row==1||row==2||row==4;
            for(int j=0;j<2;j++)
            {
                int open=(row==1&&j==0)||(row==4&&j==1)?1:row==3&&j==0?2:0;
                float length=j==0?(longInner?16:12):(longInner?10:14);
                float x=j==0?(longInner?18:16):(longInner?37:35);
                float z=zs[row]+(j==0?0:offsets[row]);
                var cluster=Group(r,j==0?"Inner cluster":"Outer cluster");cluster.localPosition=new(sign*x,0,z);
                // The northern and southern outer islands turn slightly into the lanes, independently on each bank.
                float yaw=j==1&&row==0?(sign<0?-8:6):j==1&&row==5?(sign<0?5:-7):0;
                if(yaw!=0){length=12;cluster.localPosition=new(sign*34,0,z);cluster.localRotation=Quaternion.Euler(0,yaw,0);}
                bool terrace=row==2&&j==0;
                for(int depth=0;depth<2;depth++)
                {
                    int o=depth==0?open:0;var c=Container(cluster,o==1?"Open passage":o==2?"Open search container":"Paired ground container",new(0,0,depth*3),length,o,row+j+depth+(sign<0?0:2));
                    if(sign<0)c.localRotation=Quaternion.Euler(0,180,0);
                    if(terrace)continue;
                    int tiers=depth==0?1+(row+j+(sign<0?1:0))%2:(row==0||row==5?4:3);
                    // Each upper tier is shorter and offset on a supporting box, leaving visible ledges.
                    for(int tier=1;tier<tiers;tier++)
                    {
                        float len=length-tier*1.5f;float shift=sign*((row%2==0?1:-1)*tier*.55f);
                        Container(cluster,"Offset upper stack",new(shift,tier*3,depth*3),len,0,row+j+depth+tier);
                    }
                }
                if(terrace)
                {
                    Container(cluster,"Upper roof landing",new(sign*3,3,3),6,0,2);
                    Steps(cluster,new(sign*-5,3,3),Vector3.right*sign);
                    Steps(side,new(sign*31,0,z),Vector3.left*sign);
                    Steps(side,new(sign*31,0,z+3),Vector3.left*sign);
                }
            }
        }
        foreach(float z in new[]{57.5f,101.5f})
        {Box(side,"Yard lamp post",new(sign*8.9f,2.5f,z),new(.16f,5,.16f),steel);Lamp(side,new(sign*8.9f,5,z));}
    }
    static void Wheel(Transform p,Vector3 pos,float radius,float width)
    {
        const string key="Wheel";if(!meshes.TryGetValue(key,out var m))
        {
            var v=new List<Vector3>();var tri=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;Vector3 a0=new(-.5f,Mathf.Cos(a),Mathf.Sin(a)),a1=new(.5f,a0.y,a0.z),b0=new(-.5f,Mathf.Cos(b),Mathf.Sin(b)),b1=new(.5f,b0.y,b0.z);
                Add(a0,b0,b1);Add(a0,b1,a1);Add(new(-.5f,0,0),b0,a0);Add(new(.5f,0,0),a1,b1);
            }
            void Add(Vector3 a,Vector3 b,Vector3 c){int n=v.Count;v.AddRange(new[]{a,b,c});tri.AddRange(new[]{n,n+1,n+2});uv.AddRange(new[]{new Vector2(a.z,a.y),new Vector2(b.z,b.y),new Vector2(c.z,c.y)});}
            m=new Mesh();m.SetVertices(v);m.SetTriangles(tri,0);m.SetUVs(0,uv);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);m=Save(key,m);meshes[key]=m;
        }
        var g=Render(p,"Tyre",m,rubber);g.transform.localPosition=pos;g.transform.localScale=new(width,radius,radius);g.AddComponent<MeshCollider>().sharedMesh=m;
        Box(p,"Wheel hub",pos+Vector3.right*Mathf.Sign(pos.x)*width*.51f,new(.08f,radius,radius),yellow,false);
    }
    static void Gantry(Transform root)
    {
        var p=Group(root,"Cantilever gantry crane");p.localPosition=new(0,0,-102); // South/approach apron, centre Z=29.
        foreach(float x in new[]{14f,30f})foreach(float z in new[]{124f,138f})
        {
            Box(p,"Crane leg",new(x,8.6f,z),new(1.1f,17.2f,1.1f),orange);Box(p,"Wheel bogie",new(x,.65f,z),new(2,1.3f,3.5f),rubber);
        }
        foreach(float z in new[]{124f,138f}){Beam(p,"Portal brace",new(14,2,z),new(30,16.8f,z),.45f,steel);Box(p,"Portal crossbeam",new(22,16.7f,z),new(17,1,1.2f),orange);}
        foreach(float x in new[]{14f,30f})Box(p,"Crane longitudinal girder",new(x,17.1f,131),new(1.2f,1.2f,16),orange);
        foreach(float z in new[]{129.5f,132.5f})
        {
            Box(p,"High loading boom",new(12,17.6f,z),new(42,1.4f,.7f),orange);
            Beam(p,"Cable stay",new(29,22,z),new(-8,18.3f,z),.12f,steel,false);Beam(p,"Rear stay",new(29,22,z),new(33,18.3f,z),.12f,steel,false);
        }
        Box(p,"Crane mast",new(29,19.6f,131),new(.7f,5,3.5f),steel);
        for(float x=-8;x<=32;x+=4)Box(p,"Boom cross tie",new(x,17.6f,131),new(.3f,.4f,3),steel);
        Box(p,"Parked trolley",new(22,16.7f,131),new(4,.6f,3.8f),steel);
        foreach(float x in new[]{20.5f,23.5f})foreach(float z in new[]{130f,132f})Beam(p,"Parked hoist cable",new(x,16.4f,z),new(x,9,z),.07f,steel,false);
        Box(p,"Parked container spreader",new(22,8.8f,131),new(6,.45f,2.8f),yellow);
        Box(p,"Crane operator cabin",new(30,15.5f,127),new(2.5f,2.5f,3),orange);Box(p,"Crane cabin glass",new(28.72f,15.7f,127),new(.06f,1.5f,2.5f),glass,false);
        Lamp(p,new(14,7,124));Lamp(p,new(30,7,138));
        foreach(float z in new[]{121f,141f})Box(p,"Loading apron stripe",new(22,.02f,z),new(20,.025f,.2f),yellow,false);
    }
    static void ReachStacker(Transform root)
    {
        var p=Group(root,"Parked reach stacker");p.localPosition=new(-22,0,31);
        Box(p,"Heavy chassis",new(0,1.3f,0),new(4.8f,1.5f,8),yellow);Box(p,"Rear counterweight",new(0,2.3f,2.8f),new(4.9f,1.2f,2),yellow);
        foreach(float x in new[]{-2.5f,2.5f})foreach(float z in new[]{-2.5f,2.5f})Wheel(p,new(x,1.3f,z),1.3f,.8f);
        Box(p,"Operator cabin",new(-1,3.1f,-1.5f),new(2.3f,2.4f,2.8f),steel);
        Box(p,"Front cab glass",new(-1,3.3f,-2.93f),new(1.9f,1.5f,.06f),glass,false);Box(p,"Side cab glass",new(-2.18f,3.3f,-1.5f),new(.06f,1.5f,2.2f),glass,false);
        Beam(p,"Telescopic main boom",new(.6f,3,2),new(.6f,8,-6.6f),1.05f,yellow);Beam(p,"Inner boom",new(.6f,6,-3.2f),new(.6f,8.7f,-8),.7f,steel);
        foreach(float x in new[]{-.2f,1.3f})Beam(p,"Hydraulic cylinder",new(x,2,-1),new(x,6,-4),.22f,steel);
        Box(p,"Lifting spreader",new(.6f,8.2f,-8),new(9,.35f,2.6f),steel);
        Container(p,"Held scenery container",new(.6f,5,-8),12,0,1);
        for(int i=0;i<3;i++)Box(p,"Cab access step",new(-2.65f,.4f+i*.35f,-.8f+i*.3f),new(.65f,.16f,.7f),steel);
        Lamp(p,new(-1,4.4f,-2.4f));
    }
    static void Outer(Transform root)
    {
        var p=Group(root,"Outer storage and dispatch");
        foreach(int side in new[]{-1,1})foreach(float z in new[]{13f,39f,119f,149f})
        {
            if(side>0&&z==39)continue; // Keep the south crane's bogies and loading pad unobstructed.
            Container(p,"Spare storage",new(side*36,0,z),12,0,(int)z%4);
            if(z==13||z==149)Container(p,"Overflow stack",new(side*36,3,z),6,0,2);
            Box(p,"Pallet pile",new(side*26,.5f,z+3),new(2,1,2),wood);
        }
        var booth=Group(p,"Dispatch booth");booth.localPosition=new(-14,0,14);
        Box(booth,"Booth body",new(0,1.4f,0),new(5,2.8f,4),steel);Box(booth,"Booth roof",new(0,3,0),new(5.5f,.3f,4.5f),paint[2]);Box(booth,"Dispatch window",new(2.53f,1.7f,0),new(.05f,1.1f,2.5f),glass,false);Lamp(booth,new(2.9f,2.6f,0));
        var sideways=Container(p,"Sideways fallen container",new(-34,1.5f,44),8,0,1);sideways.localRotation=Quaternion.Euler(90,0,-7);
        var tilted=Container(p,"Tilted fallen container",new(34,1.86f,115),12,0,0);tilted.localRotation=Quaternion.Euler(0,0,18);
        Box(p,"Tilt support stack",new(39,1.7f,115),new(2,3.4f,2.6f),wood);
        var lodged=Container(p,"Lodged fallen container",new(-17,1.31f,114),10,0,2);lodged.localRotation=Quaternion.Euler(0,0,-15);
        Box(p,"Lodged support pallets",new(-21,1.18f,114),new(2,2.36f,2.6f),wood);
        foreach(float x in new[]{-45f,45f})for(float z=8;z<=152;z+=8)
        {
            Box(p,"Fence post",new(x,1.3f,z),new(.13f,2.6f,.13f),steel);
            if((int)z%24==0)continue;
            foreach(float y in new[]{.55f,1.5f,2.4f})Box(p,"Fence wire",new(x,y,z+3),new(.035f,.035f,6),steel,false);
            Box(p,"Weed patch",new(x+(x>0?-1:1),.12f,z),new(.7f,.24f,1.3f),grass,false);
        }
        foreach(float z in new[]{6f,154f})foreach(float x in new[]{-38f,-24f,24f,38f})Box(p,"Boundary gravel shoulder",new(x,-.05f,z),new(10,.1f,2),gravel,false);
        Lamp(p,new(-36,5.6f,149));Lamp(p,new(36,5.6f,13));
    }
    [MenuItem("Duck Rails/Authoring/Container Yard/Install approved yard")]
    public static void Install()
    {
        Guard();Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();meshes.Clear();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");if(!cube)throw new Exception("Missing UV cube");
        paint=new[]{Mat("Rust red","weathered paint",new(.67f,.24f,.15f)),Mat("Faded blue","weathered paint",new(.27f,.43f,.55f)),Mat("Sage steel","weathered paint",new(.42f,.53f,.35f)),Mat("Ochre steel","weathered paint",new(.72f,.57f,.3f))};
        steel=Mat("Dark steel","weathered paint",new(.28f,.32f,.32f));wood=Mat("Old pallets","timber",new(.7f,.53f,.32f));orange=Mat("Gantry orange","weathered paint",new(.95f,.39f,.08f));yellow=Mat("Machine yellow","weathered paint",new(.93f,.69f,.1f));rubber=Mat("Tyres","dark roofing",new(.16f,.17f,.16f));glass=Mat("Cab glass","dark roofing",new(.16f,.3f,.34f));gravel=Mat("Yard gravel","gravel",new(.52f,.5f,.44f));concrete=Mat("Yard concrete","concrete",new(.6f,.59f,.54f));grass=Mat("Yard weeds","grass",new(.45f,.5f,.27f));
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();if(!b||b.id!=Id)throw new Exception("Wrong bake scene");var track=b.track.ToArray();var bounds=b.enemyBounds;
        foreach(var t in b.transform.Cast<Transform>().ToArray())if(t.name==RootName||t.name=="Block ground"||t.name.StartsWith("Container ")||t.name=="Access ramp"||t.name=="CONTAINER YARD"||t.name.StartsWith("STOP ")||t.name=="Trackside post"||t.name=="Left-side buildings"||t.name=="Baked block sample lights")Object.DestroyImmediate(t.gameObject);
        var root=Group(b.transform,RootName);Box(root,"Block ground",new(0,-.5f,80),new(96,1,160),concrete);Yard(root,-1);Yard(root,1);Gantry(root);ReachStacker(root);Outer(root);
        for(int v=0;v<3;v++)
        {
            float dx=(v-1)*.25f;var coals=new[]{new Vector3(-18,.65f,63),new(-36,.65f,96),new(-22,6.5f,75.5f),new(18,.65f,62),new(36,.65f,95),new(22,6.5f,76)};
            var enemies=new[]{new Vector3(25,.2f,51.5f),new(29,.2f,62),new(29,.2f,80),new(25,.2f,84),new(29,.2f,95),new(25,.2f,106)};
            for(int i=0;i<6;i++){Set(b.variations[v].coal[i],coals[i]+Vector3.right*dx);Set(b.variations[v].enemies[i],enemies[i]+Vector3.right*dx);}
            Set(b.variations[v].gold[0],new(-16,.65f,84.5f));Set(b.variations[v].gold[1],new(16,.65f,84));
        }
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Container graph bake failed");PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        if(!track.SequenceEqual(b.track)||bounds!=b.enemyBounds)throw new Exception("Track/enemy contract changed");
        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(int z=4;z<160;z+=8)foreach(float x in new[]{-42f,-32f,-22f,-12f,0f,12f,22f,32f,42f})foreach(float y in new[]{.7f,2f,3.7f,6.7f,9.7f,12.7f})if(!Physics.CheckSphere(new(x,y,z),.2f,1<<6))points.Add(new(x,y,z));
        probes.probePositions=points.ToArray();Lightmapping.lightingSettings.lightmapResolution=4;EditorUtility.SetDirty(Lightmapping.lightingSettings);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Container Yard installed; graph baked. Ready for physical checks and lighting bake.");
    }
    static void Set(Transform t,Vector3 position){t.localPosition=position;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
}
#endif
