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

/// <summary>Explicit authoring of approved Canyon. Geometry and navigation are never generated at runtime.</summary>
public static class RailCanyon
{
    public const string Id="canyon-v1", RootName="Canyon authored v1", Folder="Assets/DuckRails/Canyon";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", LightingFolder="Assets/DuckRails/Lighting/Blocks/"+Id;
    public const string BakeScene=LightingFolder+"/Bake.unity";
    public const int LightCount=12;
    static Material sand,rock,wood,steel,green,water;
    static Mesh cube;
    static readonly Dictionary<string,Mesh> boxes=new();
    public static readonly Vector3[] South={new(-36,0,44),new(-36,0,50),new(-12,-2.7f,50),new(28,-4,50),new(34,-4,50),new(34,-4,58),new(28,-4,58),new(-28,-8,58),new(-34,-8,58),new(-34,-8,66),new(-28,-8,66),new(28,-12,66),new(34,-12,66),new(34,-12,74)};
    public static Vector3[] North=>South.Select(p=>new Vector3(p.x,p.y,160-p.z)).ToArray();
    public static readonly Vector3[] Landings={new(34,-4,54),new(-34,-8,62),new(34,-12,70),new(34,-4,106),new(-34,-8,98),new(34,-12,90)};
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static float Segment(Vector2 p,Vector3 a,Vector3 b,out float t)
    {var aa=new Vector2(a.x,a.z);var d=new Vector2(b.x-a.x,b.z-a.z);t=Mathf.Clamp01(Vector2.Dot(p-aa,d)/d.sqrMagnitude);return Vector2.Distance(p,aa+d*t);}
    public static bool Shaft(float x,float z)=>x>=-30&&x< -24&&z>=116&&z<122;
    public static bool Cave(float x,float z)=>(x>=-30&&x< -24&&z>=89&&z<122)||(x>=-34&&x< -20&&z>=113&&z<125)||(x>=10&&x<18&&z>=82&&z<110);
    public static float CaveFloor(float x,float z)=>x<0?-12-4*Mathf.Clamp01((z-89)/24):-12-6*Mathf.Clamp01((z-82)/24);
    public static float Creek(float x)=>80+1.5f*Mathf.Sin(x*.12f);
    public static float Height(float x,float z)
    {
        float s=Mathf.Min(z,160-z);
        float h=s<50?0:s<51?Mathf.Lerp(0,-4,s-50):s<57?-4:s<58?Mathf.Lerp(-4,-8,s-57):s<65?-8:s<66?Mathf.Lerp(-8,-12,s-65):-12;
        // Broad switchbacks are sculpted into the cliff rather than hovering over solid ground.
        var path=z<80?South:North;float near=999,route=0;
        for(int i=1;i<path.Length;i++){float d=Segment(new Vector2(x,z),path[i-1],path[i],out float t);if(d<near){near=d;route=Mathf.Lerp(path[i-1].y,path[i].y,t);}}
        if(near<=2.5f)h=route;else if(near<3.5f)h=Mathf.Lerp(route,h,near-2.5f);
        foreach(var p in Landings)if(Mathf.Abs(x-p.x)<=5&&Mathf.Abs(z-p.z)<=3)h=p.y;
        if(s>=74)h=-12-.35f*Mathf.Clamp01(1-Mathf.Abs(z-Creek(x))/3f);
        // Raised lateral rock margins close the playable canyon; all four seams remain flat.
        if(Mathf.Abs(x)>40)h=Mathf.Lerp(h,0,Mathf.Clamp01((Mathf.Abs(x)-40)/2));
        if(Mathf.Abs(x)>=42||z<=6||z>=154)h=0;
        return h;
    }
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
    sealed class Surface
    {
        readonly List<Vector3> v=new();readonly List<Vector2> uv=new();readonly List<int> tr=new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int k=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add((Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3);tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
        public void Build(Transform p,string n,Material mat,bool collision=true){if(v.Count==0)return;var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);MeshGo(p,n,Save(n,m),mat,collision);}
    }
    static void Terrain(Transform root)
    {
        var p=Group(root,"Terraced canyon and hollow caves");
        for(int zz=0;zz<160;zz+=16)for(int xx=-48;xx<48;xx+=16)
        {
            var rim=new Surface();var cliff=new Surface();var underground=new Surface();
            for(int z=zz;z<zz+16;z++)for(int x=xx;x<xx+16;x++)
            {
                var a=new Vector3(x,Height(x,z),z);var b=new Vector3(x,Height(x,z+1),z+1);var c=new Vector3(x+1,Height(x+1,z+1),z+1);var d=new Vector3(x+1,Height(x+1,z),z);
                bool cave=Cave(x+.5f,z+.5f),shaft=Shaft(x+.5f,z+.5f);float low=Mathf.Min(a.y,b.y,c.y,d.y);
                float floorA=CaveFloor(x,z),floorB=CaveFloor(x,z+1),roofA=floorA+2.7f,roofB=floorB+2.7f;
                bool roof=low>=Mathf.Max(roofA,roofB)+.1f;
                if(!shaft&&(!cave||roof))(low<-.01f?cliff:rim).Quad(a,b,c,d);
                if(cave)
                {
                    underground.Quad(new(x,floorA,z),new(x,floorB,z+1),new(x+1,floorB,z+1),new(x+1,floorA,z));
                    if(roof&&!shaft)underground.Quad(new(x,roofA,z),new(x+1,roofA,z),new(x+1,roofB,z+1),new(x,roofB,z+1));
                    Edge(x-1,z,a,b);Edge(x,z+1,b,c);Edge(x+1,z,c,d);Edge(x,z-1,d,a);
                    void Edge(int nx,int nz,Vector3 u,Vector3 v)
                    {
                        float uf=CaveFloor(u.x,u.z),vf=CaveFloor(v.x,v.z),ur=uf+2.7f,vr=vf+2.7f;
                        if(!Cave(nx+.5f,nz+.5f)){float uy=Mathf.Clamp(u.y,uf,ur),vy=Mathf.Clamp(v.y,vf,vr);if(uy>uf+.01f||vy>vf+.01f)underground.Quad(new(u.x,uf,u.z),new(u.x,uy,u.z),new(v.x,vy,v.z),new(v.x,vf,v.z));}
                        if(shaft&&!Shaft(nx+.5f,nz+.5f))underground.Quad(new(u.x,ur,u.z),new(u.x,u.y,u.z),new(v.x,v.y,v.z),new(v.x,vr,v.z));
                    }
                }
            }
            rim.Build(p,"Rim "+xx+" "+zz,sand);cliff.Build(p,"Cliff "+xx+" "+zz,rock);underground.Build(p,"Cave "+xx+" "+zz,rock);
        }
        var creek=new Surface();for(int x=-40;x<40;x++){float a=Creek(x),b=Creek(x+1);creek.Quad(new(x,-12.06f,a-1.8f),new(x,-12.06f,a+1.8f),new(x+1,-12.06f,b+1.8f),new(x+1,-12.06f,b-1.8f));}creek.Build(root,"Shallow creek surface",water,false);
        var terraces=Group(root,"Exploration landings");for(int i=0;i<Landings.Length;i++)Group(terraces,"Plateau "+(i+1)).localPosition=Landings[i];
    }
    static void Railway(Transform root)
    {
        var p=Group(root,"Rickety railway trestle");
        Box(p,"Continuous physical train deck",new(0,-.25f,80),new(8,.5f,72),wood);
        // Flat approach ballast meets the bridge deck even where the underlying canyon falls away.
        Box(p,"South ballast support",new(0,-.25f,22),new(8,.5f,44),sand);Box(p,"North ballast support",new(0,-.25f,138),new(8,.5f,44),sand);
        foreach(float x in new[]{-1.5f,1.5f})Box(p,"Track rail",new(x,.06f,80),new(.12f,.12f,160),steel,false);
        for(int z=1;z<160;z+=2)Box(p,"Sleeper",new(0,.02f,z),new(3.8f,.04f,.35f),wood,false);
        foreach(float z in new[]{68f,78f,88f})
        {
            foreach(float x in new[]{-4.5f,4.5f})Box(p,"Trestle pier",new(x,-6.25f,z),new(.55f,11.5f,.55f),wood);
            Beam(p,"Cross brace",new(-4.5f,-11.8f,z),new(4.5f,-.5f,z),.28f,wood);Beam(p,"Cross brace",new(4.5f,-11.8f,z),new(-4.5f,-.5f,z),.28f,wood);
            Box(p,"Deck cross bearer",new(0,-.6f,z),new(10,.4f,.6f),steel);
        }
        var cat=Group(root,"Broken worker catwalk and scaffolding");
        foreach(float x in new[]{-5.8f,5.8f})
        {
            foreach(var span in new[]{new Vector2(66,75),new Vector2(77,85),new Vector2(87,96)})
            {Box(cat,"Worker walkway segment",new(x,-3.15f,(span.x+span.y)/2),new(2.4f,.3f,span.y-span.x),wood);Beam(cat,"Outer worker handrail",new(x+Mathf.Sign(x)*1.1f,-2.1f,span.x),new(x+Mathf.Sign(x)*1.1f,-2.1f,span.y),.12f,steel);}
            for(int i=0;i<15;i++)Box(cat,"Access stair",new(x,-i*.2f-.1f,66+i*.4f),new(2.4f,.2f,.4f),wood);
            Box(cat,"Deck stair landing",new(Mathf.Sign(x)*4.7f,-.1f,65.5f),new(3.4f,.2f,2),wood);
            foreach(float z in new[]{72f,82f,92f}){Box(cat,"Scaffold landing",new(x,-3.15f,z),new(3.4f,.3f,3),wood);foreach(float xx in new[]{x-1.5f,x+1.5f})Beam(cat,"Scaffold upright",new(xx,-12,z),new(xx,-1.9f,z),.12f,steel);}
        }
    }
    public static float FootbridgeY(float z)=>-.8f*Mathf.Sin(Mathf.PI*Mathf.Clamp01((z-43)/74));
    static void Footbridge(Transform root)
    {
        var p=Group(root,"East pedestrian suspension bridge");
        for(int z=43;z<117;z++)
        {
            float a=FootbridgeY(z),b=FootbridgeY(z+1);var deck=Box(p,"Plank deck",new(24,(a+b)/2-.12f,z+.5f),new(3.2f,.24f,1.01f),wood);deck.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(b-a,1)*Mathf.Rad2Deg,0,0);
            foreach(float x in new[]{22.35f,25.65f}){float ca=a+1.2f+1.8f*Mathf.Pow((z-80)/37f,2),cb=b+1.2f+1.8f*Mathf.Pow((z+1-80)/37f,2);Beam(p,"Suspension cable",new(x,ca,z),new(x,cb,z+1),.09f,steel,false);Beam(p,"Pedestrian handrail",new(x,a+1,z),new(x,b+1,z+1),.1f,wood);if(z%3==0)Beam(p,"Cable hanger",new(x,a,z),new(x,ca,z),.06f,steel,false);}
        }
        foreach(float z in new[]{43f,117f}){Box(p,"Rim landing",new(24,-.12f,z),new(5,.24f,4),wood);foreach(float x in new[]{22.35f,25.65f})Box(p,"Anchor post",new(x,1.5f,z),new(.3f,3,.3f),wood);Box(p,"Rim connection",new(-5,.012f,z<80?40:120),new(60,.02f,3),sand,false);}
    }
    static void Lamp(Transform root,string name,Vector3 p)
    {Box(root,name+" housing",p,new(.4f,.16f,.4f),steel,false);var l=Group(root,name).gameObject.AddComponent<Light>();l.transform.localPosition=p-Vector3.up*.15f;l.transform.localRotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=65;l.range=18;l.spotAngle=125;l.innerSpotAngle=85;l.color=new(1,.79f,.52f);l.shadows=LightShadows.Soft;}
    static void Dressing(Transform root)
    {
        var p=Group(root,"Cactus and rim maintenance scenery");var rng=new System.Random(731);
        foreach(float side in new[]{-1f,1f})foreach(float z in new[]{10f,20f,31f,132f,143f,151f})
        {
            float x=side*(15+rng.Next(24));float h=1.7f+(float)rng.NextDouble()*1.3f;
            Box(p,"Cactus trunk",new(x,h/2,z),new(.48f,h,.48f),green);Box(p,"Cactus arm",new(x+.6f,h*.55f,z),new(1.2f,.35f,.35f),green);Box(p,"Cactus arm tip",new(x+1,h*.7f,z),new(.35f,h*.4f,.35f),green);
            Box(p,"Sandstone outcrop",new(x-side*4,.5f,z+2),new(2.8f,1,2),rock);
        }
        foreach(var c in new[]{new Vector3(-25,0,25),new Vector3(30,0,135)})
        {Box(p,"Tool shelter roof",c+new Vector3(0,3.1f,0),new(10,.25f,7),wood);Box(p,"Tool shelter back",c+new Vector3(0,1.5f,3.4f),new(10,3,.2f),wood);foreach(float x in new[]{-4.8f,4.8f})Box(p,"Tool shelter post",c+new Vector3(x,1.5f,-3.2f),new(.25f,3,.25f),wood);Box(p,"Maintenance crates",c+new Vector3(-3,.65f,1.8f),new(2,1.3f,2),wood);Lamp(root,"Shelter worklight",c+new Vector3(0,2.8f,0));}
        foreach(var q in Landings){Box(p,"Ledge supplies",q+new Vector3(3.5f,.5f,0),new(1,1,1),wood);Lamp(root,"Plateau worklight",q+new Vector3(3.5f,2.7f,0));}
        Lamp(root,"Shaft cave worklight",new(-27,-13.6f,119));Lamp(root,"Cave exit worklight",new(-27,-12,103));Lamp(root,"East cave worklight",new(14,-15.4f,105));Lamp(root,"Underbridge worklight",new(5,-.8f,82));
    }
    public static void Guard(){if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Duck Rails Edit Mode without active bake");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save current scene changes first");}
    [MenuItem("Duck Rails/Authoring/Canyon/Install approved Canyon")]
    public static void Install()
    {
        Guard();Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(LightingFolder);AssetDatabase.Refresh();boxes.Clear();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");if(!cube)throw new Exception("Missing UV cube");
        sand=Mat("Canyon sand","gravel",new(.8f,.65f,.43f));rock=Mat("Layered sandstone","concrete",new(.72f,.43f,.26f));wood=Mat("Weathered bridge timber","timber",new(.55f,.39f,.23f));steel=Mat("Bridge iron","weathered paint",new(.3f,.34f,.33f));green=Mat("Cactus","foliage",new(.34f,.48f,.24f));water=Mat("Shallow creek","lake water",new(.23f,.52f,.54f));
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=new GameObject("Canyon");var b=go.AddComponent<RailBlock>();b.id=Id;b.displayName="Canyon";b.tags=new[]{"canyon","vertical","bridge","straight"};b.length=160;
        b.entry=Group(go.transform,"Track Entry");b.exit=Group(go.transform,"Track Exit");b.exit.localPosition=new(0,0,160);b.stop=Group(go.transform,"Suggested Stop");b.stop.localPosition=new(0,0,80);b.track=Enumerable.Range(0,81).Select(i=>new Vector3(0,0,i*2)).ToArray();
        b.enemyBounds=new Bounds(new(0,-5,80),new(80,16,70));b.enemySurfaceRailExclusion=new Bounds(new(0,6,80),new(16,20,160));
        var root=Group(go.transform,RootName);Terrain(root);Railway(root);Footbridge(root);Dressing(root);
        var blocker=Group(root,"Railway navigation exclusion").gameObject.AddComponent<CrawlGraphBlocker>();var bx=blocker.GetComponent<BoxCollider>();bx.isTrigger=true;bx.center=b.enemySurfaceRailExclusion.center;bx.size=b.enemySurfaceRailExclusion.size;
        b.variations=new RailSpawnSet[3];
        var coal=new[]{new Vector3(-34,-7.5f,62),new(-34,-7.5f,98),new(-27,-15.5f,113),new(34,-3.5f,54),new(34,-11.5f,90),new(34,-3.5f,106)};
        for(int v=0;v<3;v++)
        {var parent=Group(go.transform,"Spawn variation "+v);float dx=(v-1)*.5f;b.variations[v]=new RailSpawnSet{coal=coal.Select((p,i)=>RailCoalAuthoring.Create(parent,"CoalSpawner "+i,p+Vector3.right*dx)).ToArray(),enemies=Landings.Select((p,i)=>{var t=Group(parent,"Enemy plateau "+i);t.localPosition=p+new Vector3(dx,.2f,0);return t;}).ToArray()};}
        RailEconomyBuilder.AddGold(b);
        var baker=Group(go.transform,"Authored crawlgraph settings").gameObject.AddComponent<CrawlGraphBaker>();baker.transform.localPosition=new(0,-2,80);baker.size=new(80,32,128);baker.cellSize=1;baker.agentRadius=.25f;baker.allowCeilings=false;baker.minIslandNodes=10;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();baker.drawNodes=false;
        string graph="Assets/DuckRails/Blocks/"+Id+".bytes";if(!File.Exists(graph))File.WriteAllBytes(graph,new byte[8]);AssetDatabase.ImportAsset(graph);baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(graph);b.graph=baker.graphAsset;Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Canyon graph bake failed");
        var lighting=go.AddComponent<RailBakedBlockLighting>();lighting.lightingScene=LightingFolder+"/"+Id+"-Lighting.unity";
        PrefabUtility.SaveAsPrefabAssetAndConnect(go,PrefabPath,InteractionMode.AutomatedAction);
        var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();
        for(int z=3;z<160;z+=8)foreach(float x in new[]{-40f,-32f,-24f,-12f,0f,12f,24f,32f,40f})foreach(float dy in new[]{.7f,2.5f}){float y=Height(x,z);if(Cave(x,z))y=CaveFloor(x,z);var q=new Vector3(x,y+dy,z);if(!Physics.CheckSphere(q,.2f,1<<6))points.Add(q);}
        foreach(float x in new[]{-5f,0f,5f,24f})for(int z=44;z<=116;z+=8)foreach(float y in new[]{.7f,2.5f})points.Add(new(x,y,z));probes.probePositions=points.ToArray();
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new(.045f,.055f,.075f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new(.018f,.024f,.033f);
        var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingFolder+"/Lighting.lighting");if(!settings){settings=new LightingSettings{name="Canyon Lighting",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=6,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=32,indirectSampleCount=128,environmentSampleCount=64,maxBounces=4,indirectScale=1.5f};AssetDatabase.CreateAsset(settings,LightingFolder+"/Lighting.lighting");}Lightmapping.lightingSettings=settings;
        EditorSceneManager.SaveScene(scene,BakeScene);AssetDatabase.SaveAssets();Debug.Log("Canyon prefab and graph installed; run checks, register, then Bake Open Block.");
    }
    public static void Register()
    {
        Guard();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();if(!route)throw new Exception("Missing route");var b=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<RailBlock>();if(!route.catalog.Any(x=>x.id==Id))route.catalog=route.catalog.Concat(new[]{b}).ToArray();EditorUtility.SetDirty(route);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(BakeScene);
    }
}
#endif

