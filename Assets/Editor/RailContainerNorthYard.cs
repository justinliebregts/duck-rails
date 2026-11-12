#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Game.AI;
using Game.AI.Editor;
using TMPro;
using Object=UnityEngine.Object;

// Additive installation of the approved November 12 north-yard layout.
public static class RailContainerNorthYard
{
    public const string Root="North repair and loading yard";
    public const string Folder="Assets/DuckRails/ContainerYard/NorthYard";
    public const string Evidence="docs/validation/container-north-yard";
    static Material steel,red,blue,wood,concrete,rubber,glass,white,yellow;
    static readonly Dictionary<string,Mesh> cache=new();
    static Transform Group(Transform p,string name,Vector3 pos=default)
    {var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
    static Material Existing(string name)=>AssetDatabase.LoadAssetAtPath<Material>(RailContainerYard.Folder+"/"+name+".mat");
    static Material Tint(string name,Material source,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(source);m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);}return m;
    }
    static Mesh Save(string key,Mesh m)
    {
        m.name=key;string path=Folder+"/"+key+".asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){Object.DestroyImmediate(m);return old;}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static Mesh BoxMesh(Vector3 size)
    {
        string key="Box-"+Hash128.Compute(size.ToString("F4"));if(cache.TryGetValue(key,out var m))return m;
        m=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/container-yard-v1/Cube Block UV.asset"));
        var v=m.vertices;var normals=m.normals;var uv=new Vector2[v.Length];
        for(int i=0;i<v.Length;i++){v[i]=Vector3.Scale(v[i],size);var p=v[i];uv[i]=(Mathf.Abs(normals[i].y)>.5f?new Vector2(p.x,p.z):Mathf.Abs(normals[i].x)>.5f?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/3;}
        m.vertices=v;m.uv=uv;m.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(m);cache[key]=Save(key,m);return cache[key];
    }
    static Transform Render(Transform parent,string name,Vector3 pos,Mesh mesh,Material mat,bool meshCollision=false)
    {
        var t=Group(parent,name,pos);t.gameObject.layer=6;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.scaleInLightmap=.35f;r.receiveGI=ReceiveGI.Lightmaps;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
        if(meshCollision)t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;return t;
    }
    static Transform Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat,bool collision=true)
    {var t=Render(p,name,pos,BoxMesh(size),mat);if(collision)t.gameObject.AddComponent<BoxCollider>().size=size;return t;}
    static void Beam(Transform p,string name,Vector3 a,Vector3 b,float width,Material mat,bool collision=true)
    {var t=Box(p,name,(a+b)/2,new Vector3(width,(b-a).magnitude,width),mat,collision);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static Mesh Revolve(string key,float[] z,float[] radius,int segments)
    {
        if(cache.TryGetValue(key,out var existing))return existing;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c)
        {
            var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<.0000001f)return;
            if(Vector3.Dot(n,(a+b+c)/3)<0)(b,c)=(c,b);
            n=Vector3.Cross(b-a,c-a).normalized;int start=vertices.Count;
            foreach(var p in new[]{a,b,c}){vertices.Add(p);uv.Add((Mathf.Abs(n.z)>.7f?new Vector2(p.x,p.y):new Vector2(Mathf.Atan2(p.y,p.x)*1.5f,p.z))/3);}
            triangles.AddRange(new[]{start,start+1,start+2});
        }
        for(int j=1;j<z.Length;j++)for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
            var a0=new Vector3(Mathf.Cos(a)*radius[j-1],Mathf.Sin(a)*radius[j-1],z[j-1]);
            var b0=new Vector3(Mathf.Cos(b)*radius[j-1],Mathf.Sin(b)*radius[j-1],z[j-1]);
            var a1=new Vector3(Mathf.Cos(a)*radius[j],Mathf.Sin(a)*radius[j],z[j]);
            var b1=new Vector3(Mathf.Cos(b)*radius[j],Mathf.Sin(b)*radius[j],z[j]);
            Tri(a0,b0,b1);Tri(a0,b1,a1);
        }
        var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(m);cache[key]=Save(key,m);return cache[key];
    }
    static Transform Cylinder(Transform p,string name,Vector3 center,float radius,float length,Material mat,Vector3 axis,bool collision=true)
    {
        string key="Cylinder-"+Hash128.Compute(radius.ToString("F3")+"-"+length.ToString("F3"));
        var m=Revolve(key,new[]{-length/2,-length/2,length/2,length/2},new[]{0f,radius,radius,0f},12);
        var t=Render(p,name,center,m,mat,collision);t.localRotation=Quaternion.FromToRotation(Vector3.forward,axis);return t;
    }
    static void Sign(Transform p,string text,Vector3 pos,Vector3 rotation,Vector2 size)
    {
        var t=Group(p,"Label "+text.Replace('\n',' '),pos);t.localRotation=Quaternion.Euler(rotation);
        var tmp=t.gameObject.AddComponent<TextMeshPro>();tmp.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        tmp.text=text;tmp.fontSize=4;tmp.enableAutoSizing=true;tmp.fontSizeMin=1;tmp.fontSizeMax=4;tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(.14f,.12f,.1f);tmp.rectTransform.sizeDelta=size;tmp.ForceMeshUpdate();
        tmp.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    static void Hollow(Transform p,string name,Vector3 pos,float yaw,bool dismantled)
    {
        var t=Group(p,name,pos);t.localRotation=Quaternion.Euler(0,yaw,0);var mat=dismantled?red:blue;
        Box(t,"Container floor",new(0,.08f,0),new(12,.16f,3),mat);
        Box(t,"Container roof",new(0,2.92f,0),new(12,.16f,3),mat);
        Box(t,"Rear wall",new(-5.92f,1.5f,0),new(.16f,2.84f,3),mat);
        Box(t,"North side wall",new(0,1.5f,1.43f),new(12,2.84f,.14f),mat);
        if(!dismantled)Box(t,"South side wall",new(0,1.5f,-1.43f),new(12,2.84f,.14f),mat);
        else foreach(float x in new[]{-4.9f,4.9f})Box(t,"Remaining side panel",new(x,1.5f,-1.43f),new(2.2f,2.84f,.14f),mat);
        foreach(float side in new[]{-1f,1f})
        {
            for(float x=-5.6f;x<5.8f;x+=.45f)if(!dismantled||side>0||Mathf.Abs(x)>3.8f)
                Box(t,"Corrugation",new(x,1.5f,side*1.53f),new(.09f,2.66f,.07f),mat,false);
            foreach(float x in new[]{-5.91f,5.91f})Box(t,"Corner post",new(x,1.5f,side*1.41f),new(.18f,3,.18f),steel);
            if(!dismantled)Box(t,"Folded door",new(5.3f,1.5f,side*1.6f),new(1.4f,2.8f,.12f),mat);
        }
    }
    static void Bench(Transform p,Vector3 pos)
    {
        var t=Group(p,"Repair workbench",pos);Box(t,"Bench top",new(0,1,0),new(3.2f,.16f,1),wood);
        foreach(float x in new[]{-1.35f,1.35f})foreach(float z in new[]{-.32f,.32f})Box(t,"Bench leg",new(x,.46f,z),new(.12f,.92f,.12f),steel);
        Box(t,"Lower shelf",new(0,.24f,0),new(2.8f,.1f,.75f),wood);
        Box(t,"Vise base",new(-1.1f,1.14f,-.2f),new(.45f,.16f,.35f),steel);Box(t,"Vise jaw",new(-1.1f,1.32f,-.2f),new(.3f,.2f,.18f),steel);
        Box(t,"Toolbox",new(.7f,1.3f,.08f),new(.65f,.45f,.4f),red);
    }
    static void Shed(Transform root)
    {
        var p=Group(root,"Open maintenance shed",new(-33,0,134));
        Box(p,"Concrete service pad",new(0,-.045f,0),new(18,.15f,16),concrete);
        foreach(float x in new[]{-8.7f,8.7f})foreach(float z in new[]{-7.7f,0f,7.7f})
        {Box(p,"Steel column",new(x,2.5f,z),new(.26f,5,.26f),steel);Box(p,"Column foot",new(x,.15f,z),new(.6f,.3f,.6f),concrete);}
        foreach(float x in new[]{-8.7f,8.7f})Box(p,"Eave girder",new(x,4.95f,0),new(.2f,.3f,16),steel);
        foreach(float z in new[]{-7.7f,0f,7.7f})
        {Beam(p,"Roof truss",new(-9,5,z),new(0,5.8f,z),.2f,steel);Beam(p,"Roof truss",new(0,5.8f,z),new(9,5,z),.2f,steel);Box(p,"Tie beam",new(0,5,z),new(18,.18f,.18f),steel);}
        foreach(int sign in new[]{-1,1})
        {var roof=Box(p,"Pitched metal roof",new(sign*4.5f,5.4f,0),new(9.05f,.16f,16),steel);roof.localRotation=Quaternion.Euler(0,0,-sign*Mathf.Atan2(.8f,9)*Mathf.Rad2Deg);}
        Box(p,"West cladding",new(-8.9f,1.8f,0),new(.16f,3.6f,16),blue);
        Box(p,"North cladding",new(0,1.8f,7.9f),new(18,3.6f,.16f),blue);
        for(float z=-7.7f;z<8;z+=.6f)Box(p,"Wall rib",new(-8.79f,1.8f,z),new(.1f,3.5f,.08f),blue,false);
        Hollow(p,"Partially dismantled container",new(1,.03f,1),0,true);
        Bench(p,new(-4.5f,.03f,5.7f));Bench(p,new(2,.03f,5.7f));
        for(int i=0;i<3;i++){var door=Box(p,"Spare leaning door",new(-7.5f+i*1.6f,1.4f,-5.6f),new(1.4f,2.8f,.12f),i==1?yellow:red);door.localRotation=Quaternion.Euler(-12,0,0);}
        foreach(float x in new[]{-2f,3f})for(int i=0;i<6;i++)
        {
            var link=Group(p,"Hanging chain link",new(x,4.8f-i*.22f,-3));link.localRotation=Quaternion.Euler(0,i%2*90,0);
            Beam(link,"Chain",new(-.07f,.1f,0),new(.07f,.1f,0),.035f,steel,false);Beam(link,"Chain",new(-.07f,-.1f,0),new(.07f,-.1f,0),.035f,steel,false);
            Beam(link,"Chain",new(-.07f,-.1f,0),new(-.07f,.1f,0),.035f,steel,false);Beam(link,"Chain",new(.07f,-.1f,0),new(.07f,.1f,0),.035f,steel,false);
        }
        Box(p,"Repair sign board",new(8.93f,4.1f,-1.5f),new(.08f,.85f,4),white);Sign(p,"CONTAINER\nREPAIRS",new(8.99f,4.1f,-1.5f),new(0,-90,0),new(3.8f,.8f));
    }
    static void Wheel(Transform p,Vector3 pos,float radius,float width)
    {Cylinder(p,"Rubber wheel",pos,radius,width,rubber,Vector3.right);Cylinder(p,"Wheel hub",pos+Vector3.right*Mathf.Sign(pos.x)*width*.51f,radius*.48f,.07f,steel,Vector3.right);}
    static void Truck(Transform root)
    {
        var p=Group(root,"Parked semi and open trailer",new(25,0,134));
        Box(p,"Trailer frame",new(0,1.02f,2.5f),new(3.6f,.35f,12.8f),steel);
        Box(p,"Trailer deck",new(0,1.22f,2.5f),new(3.4f,.16f,13),wood);
        Hollow(p,"Open trailer container",new(0,1.3f,2.84f),-90,false); // North opening at Z142.84, floor meets dock at 1.46 m.
        foreach(float x in new[]{-1.55f,1.55f})foreach(float z in new[]{-6.7f,-3.9f,5.5f,7.2f})Wheel(p,new(x,.65f,z),.65f,.45f);
        Box(p,"Truck chassis",new(0,.75f,-6),new(2.8f,.45f,5.8f),steel);
        Box(p,"Cab lower body",new(0,1.35f,-7.2f),new(2.9f,1.1f,3.4f),red);
        Box(p,"Cab upper body",new(0,2.4f,-6.75f),new(2.85f,1.4f,2.4f),red);
        Box(p,"Windshield",new(0,2.55f,-7.97f),new(2.5f,.85f,.06f),glass);
        foreach(float x in new[]{-1.46f,1.46f})
        {Box(p,"Side window",new(x,2.55f,-6.7f),new(.06f,.85f,1.5f),glass);Box(p,"Door handle",new(x*1.03f,1.93f,-6.2f),new(.08f,.08f,.35f),steel);Box(p,"Cab step",new(x,.65f,-6.3f),new(.45f,.16f,1.1f),steel);Beam(p,"Mirror arm",new(x,2.5f,-7.7f),new(x*1.25f,2.5f,-7.7f),.07f,steel);Box(p,"Mirror",new(x*1.25f,2.6f,-7.7f),new(.12f,.45f,.24f),steel);}
        Box(p,"Front grille",new(0,1.35f,-8.93f),new(1.9f,.65f,.08f),steel);Box(p,"Front bumper",new(0,.85f,-8.97f),new(3.1f,.25f,.15f),steel);
        foreach(float x in new[]{-1.1f,1.1f})Box(p,"Headlamp",new(x,1.45f,-8.96f),new(.42f,.24f,.07f),white);
        var dock=Group(root,"Raised loading platform");
        Box(dock,"Dock slab",new(26,.73f,144.5f),new(10,1.46f,3),concrete);
        Box(dock,"Threshold plate",new(25,1.455f,142.92f),new(2.65f,.06f,.25f),steel);
        for(int i=0;i<7;i++){float h=(i+1)*1.46f/7;Box(dock,"Dock step "+i,new(17+(i+.5f)*4/7,h/2,144),new(4f/7,h,2),concrete);}
        foreach(float z in new[]{143f,145.95f})for(float x=22;x<31;x+=2){if(z==143&&x<27)continue;Box(dock,"Dock edge post",new(x,1.96f,z),new(.09f,1,.09f),steel);}
        Beam(dock,"North dock rail",new(21,2.46f,145.95f),new(31,2.46f,145.95f),.09f,steel);
        Beam(dock,"East dock rail",new(30.95f,2.46f,143),new(30.95f,2.46f,145.95f),.09f,steel);
        Box(dock,"Hazard edge",new(28.5f,1.47f,143.07f),new(4.7f,.025f,.15f),yellow,false);
    }
    static void Forklift(Transform root)
    {
        var p=Group(root,"Parked forklift",new(-16,0,123.5f));p.localRotation=Quaternion.Euler(0,-12,0);
        Box(p,"Counterweight",new(0,.85f,.8f),new(2.15f,1.4f,1.3f),yellow);
        Box(p,"Body",new(0,.6f,0),new(2.1f,.9f,3),yellow);
        foreach(float x in new[]{-1.05f,1.05f})foreach(float z in new[]{-.9f,.9f})Wheel(p,new(x,.43f,z),.43f,.34f);
        Box(p,"Driver seat",new(0,1.2f,.25f),new(.75f,.2f,.8f),rubber);Box(p,"Seat back",new(0,1.55f,.6f),new(.75f,.65f,.18f),rubber);
        foreach(float x in new[]{-.85f,.85f})foreach(float z in new[]{-.8f,.85f})Box(p,"Overhead guard post",new(x,1.8f,z),new(.09f,2.2f,.09f),steel);
        Box(p,"Overhead guard",new(0,2.9f,0),new(2,.14f,2.1f),yellow);
        foreach(float x in new[]{-.7f,.7f}){Box(p,"Lift mast",new(x,1.8f,-1.4f),new(.14f,3.2f,.17f),steel);Box(p,"Fork upright",new(x,.72f,-1.58f),new(.16f,1.2f,.12f),steel);Box(p,"Fork blade",new(x,.18f,-2.05f),new(.22f,.1f,1.1f),steel);}
        Box(p,"Lift crossbar",new(0,1.1f,-1.48f),new(1.6f,.2f,.15f),steel);
        Cylinder(p,"Steering column",new(0,1.4f,-.6f),.07f,.7f,steel,Vector3.up);Cylinder(p,"Steering wheel",new(0,1.77f,-.6f),.24f,.08f,rubber,Vector3.up);
    }
    static void Climb(Transform root)
    {
        var p=Group(root,"Fallen container and pallet approach");
        var shell=Render(p,"Fallen container",new(-15,1.5f,134),AssetDatabase.LoadAssetAtPath<Mesh>(RailContainerYard.Folder+"/Meshes/Container-12-0.asset"),red);
        shell.localRotation=Quaternion.Euler(90,0,0);var c=shell.gameObject.AddComponent<BoxCollider>();c.center=new(0,1.5f,0);c.size=new(12,3,3);
        // Broad pallet steps reach a continuous 3 m-high landing beside the fallen box.
        for(int i=0;i<10;i++)
        {
            float h=(i+1)*.3f;float z=128+(i+.5f)*.6f;
            Box(p,"Pallet stair "+i,new(-19.5f,h/2,z),new(2.4f,h,.6f),wood);
            for(int slat=0;slat<3;slat++)Box(p,"Pallet top slat",new(-20.3f+slat*.8f,h+.012f,z),new(.7f,.024f,.56f),wood,false);
        }
        Box(p,"Pallet landing",new(-19.5f,1.5f,134),new(2.4f,3,1.2f),wood);
    }
    static void Tanks(Transform root)
    {
        var tanks=Group(root,"Horizontal propane storage");
        var mesh=Revolve("Horizontal propane vessel",new[]{-5.5f,-5.3f,-4.75f,-4f,4f,4.75f,5.3f,5.5f},new[]{0f,.75f,1.3f,1.5f,1.5f,1.3f,.75f,0f},16);
        foreach(float x in new[]{35f,41f})
        {
            var p=Group(tanks,"Propane tank "+x,new(x,2.2f,133.5f));Render(p,"Rounded propane vessel",Vector3.zero,mesh,white,true);
            foreach(float z in new[]{-3.5f,3.5f})
            {
                Box(p,"Concrete saddle base",new(0,-1.92f,z),new(3.8f,.56f,.8f),concrete);
                // Supports meet the vessel at the underside and shoulders rather than float below it.
                Box(p,"Saddle centre",new(0,-1.49f,z),new(1.2f,.35f,.65f),concrete);
                foreach(int side in new[]{-1,1}){var support=Box(p,"Saddle cheek",new(side*1.17f,-1.2f,z),new(.55f,1.1f,.65f),concrete);support.localRotation=Quaternion.Euler(0,0,-side*28);}
            }
            Cylinder(p,"Top service neck",new(0,1.63f,.6f),.16f,.4f,steel,Vector3.up);
            Cylinder(p,"Valve body",new(0,1.9f,.6f),.22f,.18f,red,Vector3.up);
            Beam(p,"Valve handle",new(-.34f,2.04f,.6f),new(.34f,2.04f,.6f),.08f,red);
            Cylinder(p,"End outlet",new(0,-.2f,-5.5f),.12f,.7f,steel,Vector3.forward);
            Cylinder(p,"Outlet downpipe",new(0,-.9f,-5.8f),.12f,1.4f,steel,Vector3.up);
            Box(p,"Propane warning panel",new(-1.54f,0,0),new(.07f,1.15f,3.4f),yellow);
            Sign(p,"PROPANE\nNO SMOKING",new(-1.59f,0,0),new(0,90,0),new(3.2f,1.05f));
            foreach(float dx in new[]{-1.9f,1.9f})Box(p,"Protective bollard",new(dx,-1.55f,-6.3f),new(.16f,1.3f,.16f),yellow);
        }
    }
    static void Floodlight(Transform root,Vector3 position,Vector3 target)
    {
        var p=Group(root,position.x<0?"North yard floodlight west":"North yard floodlight east",position);Box(p,"Concrete footing",new(0,.3f,0),new(.8f,.6f,.8f),concrete);Box(p,"Light pole",new(0,4.3f,0),new(.18f,8.6f,.18f),steel);
        var head=Group(p,"Floodlight head",new(0,8.55f,0));head.rotation=Quaternion.LookRotation(target-head.position);
        Box(head,"Lamp casing",Vector3.zero,new(1.2f,.5f,.3f),steel);Box(head,"Lamp face",new(0,0,.16f),new(1.05f,.35f,.04f),white);
        var l=Group(head,"Baked floodlight",new(0,0,.22f)).gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=110;l.range=30;l.spotAngle=110;l.innerSpotAngle=70;l.color=new(1,.89f,.72f);l.shadows=LightShadows.Soft;
    }
    static void Combine(Transform group)
    {
        var filters=group.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>()&&!f.GetComponent<TMP_Text>()).ToArray();
        foreach(var batch in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var pieces=batch.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pieces,true,true);Unwrapping.GenerateSecondaryUVSet(mesh);
            var combined=Render(group,"Combined "+batch.Key.name,Vector3.zero,Save(group.name+"-"+batch.Key.name,mesh),batch.Key);
            foreach(var f in batch){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
        }
    }
    [MenuItem("Duck Rails/Authoring/Container Yard/Install approved north yard")]
    public static void Install()
    {
        RailContainerYard.Guard();var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&(stage.assetPath!=RailContainerYard.PrefabPath||stage.scene.isDirty))throw new Exception("Save target prefab edits first");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);Directory.CreateDirectory("Temp/container-north-yard");
        File.Copy(RailContainerYard.PrefabPath,"Temp/container-north-yard/before.prefab",true);AssetDatabase.Refresh();cache.Clear();
        steel=Existing("Dark steel");red=Existing("Rust red");blue=Existing("Faded blue");wood=Existing("Old pallets");concrete=Existing("Yard concrete");rubber=Existing("Tyres");glass=Existing("Cab glass");yellow=Existing("Machine yellow");white=Tint("Weathered propane white",blue,new(.81f,.8f,.71f));
        if(stage!=null)StageUtility.GoToMainStage();var prefab=PrefabUtility.LoadPrefabContents(RailContainerYard.PrefabPath);
        try
        {
            var env=RailBlock.FindEnvironment(prefab.transform,RailContainerYard.RootName);if(env.Find(Root))throw new Exception("North yard already installed; edit the existing group rather than rebuilding it");
            var root=Group(env,Root);Shed(root);Truck(root);Forklift(root);Climb(root);Tanks(root);
            Floodlight(root,new(-22,0,142),new(-32,0,131));Floodlight(root,new(18,0,148),new(31,0,134));
            foreach(var group in root.Cast<Transform>().ToArray())Combine(group);
            PrefabUtility.SaveAsPrefabAsset(prefab,RailContainerYard.PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(RailContainerYard.BakeScene);BakeNavigation();
        Debug.Log("Approved north yard installed additively; existing container and spawn transforms retained.");
    }
    public static void BakeNavigation()
    {
        var b=Object.FindFirstObjectByType<RailBlock>();if(!b||b.id!=RailContainerYard.Id)throw new Exception("Open Container Yard bake scene");
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Offline graph bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);EditorSceneManager.SaveScene(b.gameObject.scene);AssetDatabase.SaveAssets();
    }
}
#endif
