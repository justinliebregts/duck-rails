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

/// <summary>Explicit authoring of the approved twin-mine V5. No runtime generation.</summary>
public static class RailQuarryMine
{
    public const string Id="quarry-v1", RootName="Twin mine authored v5", Folder="Assets/DuckRails/QuarryMine";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/Bake.unity";
    public const int LightCount=10;
    static Material rock,grass,gravel,wood,steel,yellow,glass;
    static Mesh cube;
    static readonly Dictionary<string,Mesh> boxes=new();
    public static readonly Vector2[][] Routes={
        new[]{new Vector2(-25,92),new Vector2(-15,108),new Vector2(0,101),new Vector2(15,88),new Vector2(25,72)},
        new[]{new Vector2(-25,92),new Vector2(-12,92),new Vector2(12,72),new Vector2(25,72)},
        new[]{new Vector2(-25,92),new Vector2(-15,76),new Vector2(0,63),new Vector2(15,56),new Vector2(25,72)}};
    public static readonly Vector3[] Haul={new(10,0,12),new(34,0,23),new(37,0,30),new(37,-6,112),new(26,-6,112),new(26,-6,72)};
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    public static float Segment(Vector2 p,Vector2 a,Vector2 b,out float t){t=Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude);return Vector2.Distance(p,Vector2.Lerp(a,b,t));}
    public static bool Tunnel(float x,float z)
    {
        var p=new Vector2(x,z);
        if(x>=7&&x<=15&&z>=77&&z<=84)return true;
        if(x>=9&&x<=13&&z>=71&&z<=79)return true;
        return Routes.Any(r=>Enumerable.Range(1,r.Length-1).Any(i=>Segment(p,r[i-1],r[i],out _)<=2.8f));
    }
    static float RectRatio(float x,float z,float cx,float cz,float ix,float iz,float ox,float oz)
        =>Mathf.Max((Mathf.Abs(x-cx)-ix)/(ox-ix),(Mathf.Abs(z-cz)-iz)/(oz-iz));
    public static float Height(float x,float z)
    {
        float h=0;
        float w=RectRatio(x,z,-26,90.5f,7,18,15,31.5f);
        if(w<1)h=Mathf.Lerp(-6,2,Mathf.Clamp01(w));
        else if(x< -9&&x> -44&&z>56&&z<125)h=2*Mathf.Clamp01(1-(w-1)*4);
        float e=RectRatio(x,z,26,73,6,38,15,55);
        if(e<1)h=Mathf.Lerp(-6,0,Mathf.Clamp01(e));
        // The full-width haul road is graded into the terrain, with level end landings.
        if(x>7)
        {
            float nearest=99,ramp=0;
            for(int i=1;i<Haul.Length;i++){var a=Haul[i-1];var b=Haul[i];float d=Segment(new Vector2(x,z),new Vector2(a.x,a.z),new Vector2(b.x,b.z),out float t);if(d<nearest){nearest=d;ramp=Mathf.Lerp(a.y,b.y,t);}}
            if(nearest<=2.5f)h=ramp;else if(nearest<3.5f)h=Mathf.Lerp(ramp,h,nearest-2.5f);
        }
        // Preserve exact flat seams and an uninterrupted railway deck.
        if(Mathf.Abs(x)>=42||z<=6||z>=154||Mathf.Abs(x)<=7)h=0;
        return h;
    }
    static Material Mat(string n,string source,Color tint)
    {
        string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){var src=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville "+source+".mat");if(!src)throw new Exception("Missing material "+source);m=new Material(src);AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(m);return m;
    }
    static Mesh Save(string n,Mesh m)
    {
        string path=Folder+"/Meshes/"+n+".asset";m.name=n;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old)
        {
            // Refresh GPU buffers as well as serialized data when a material split changes vertex counts.
            old.Clear();old.name=n;old.indexFormat=m.indexFormat;old.vertices=m.vertices;old.normals=m.normals;old.uv=m.uv;old.uv2=m.uv2;old.tangents=m.tangents;
            old.subMeshCount=m.subMeshCount;for(int i=0;i<m.subMeshCount;i++)old.SetTriangles(m.GetTriangles(i),i);old.bounds=m.bounds;old.UploadMeshData(false);
            Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;
        }
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject MeshGo(Transform p,string n,Mesh m,Material mat,bool collide=true)
    {
        var g=Group(p,n).gameObject;g.layer=6;g.AddComponent<MeshFilter>().sharedMesh=m;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.3f;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(collide)g.AddComponent<MeshCollider>().sharedMesh=m;return g;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material mat,bool collide=true)
    {
        string key=Hash128.Compute(size.ToString("F4")).ToString();if(!boxes.TryGetValue(key,out var m)){m=Object.Instantiate(cube);var v=m.vertices;var ns=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=(Mathf.Abs(ns[i].y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(ns[i].x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}m.uv=uv;m=Save("Box-"+key,m);boxes[key]=m;}
        var g=MeshGo(p,n,m,mat,false);g.transform.localPosition=pos;g.transform.localScale=size;if(collide)g.AddComponent<BoxCollider>();return g;
    }
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float w,Material mat){var go=Box(p,n,(a+b)/2,new Vector3(w,(b-a).magnitude,w),mat);go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    sealed class Surface
    {
        readonly List<Vector3> v=new();readonly List<Vector2> uv=new();readonly List<int> tr=new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int k=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add((Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3);tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
        public void Build(Transform p,string n,Material mat){if(v.Count==0)return;var m=new Mesh();m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);MeshGo(p,n,Save(n,m),mat);}
    }
    static void Terrain(Transform root)
    {
        var p=Group(root,"Excavated terrain");
        // Tile the surface and union the three tunnel corridors before emitting walls.
        // This leaves junctions open and removes the old ground slab from the void.
        for(int zz=0;zz<160;zz+=16)for(int xx=-48;xx<48;xx+=16)
        {
            var top=new Surface();var surrounding=new Surface();var ballast=new Surface();var underground=new Surface();
            for(int z=zz;z<zz+16;z++)for(int x=xx;x<xx+16;x++)
            {
                var a=new Vector3(x,Height(x,z),z);var b=new Vector3(x,Height(x,z+1),z+1);var c=new Vector3(x+1,Height(x+1,z+1),z+1);var d=new Vector3(x+1,Height(x+1,z),z);
                bool tunnel=Tunnel(x+.5f,z+.5f);float low=Mathf.Min(a.y,b.y,c.y,d.y);
                if(!tunnel||low>=-3)
                {
                    var target=low<-.01f||Mathf.Max(a.y,b.y,c.y,d.y)>.01f?top:Mathf.Abs(x+.5f)<5?ballast:surrounding;
                    target.Quad(a,b,c,d);
                }
                if(tunnel)
                {
                    underground.Quad(new(x,-6,z),new(x,-6,z+1),new(x+1,-6,z+1),new(x+1,-6,z));
                    if(low>=-3)underground.Quad(new(x,-3,z),new(x+1,-3,z),new(x+1,-3,z+1),new(x,-3,z+1));
                    Edge(x-1,z,a,b);Edge(x,z+1,b,c);Edge(x+1,z,c,d);Edge(x,z-1,d,a);
                    void Edge(int nx,int nz,Vector3 u,Vector3 v)
                    {
                        if(Tunnel(nx+.5f,nz+.5f))return;
                        float uy=Mathf.Clamp(u.y,-6,-3),vy=Mathf.Clamp(v.y,-6,-3);
                        if(uy<=-5.999f&&vy<=-5.999f)return;
                        underground.Quad(new(u.x,-6,u.z),new(u.x,uy,u.z),new(v.x,vy,v.z),new(v.x,-6,v.z));
                    }
                }
            }
            top.Build(p,"Ground tile "+xx+" "+zz,rock);
            surrounding.Build(p,"Scrub tile "+xx+" "+zz,grass);
            ballast.Build(p,"Ballast tile "+xx+" "+zz,gravel);
            underground.Build(p,"Tunnel shell "+xx+" "+zz,rock);
        }
    }
    static void Lamp(Transform p,string n,Vector3 pos,bool warm=false,float power=45)
    {
        Box(p,n+" housing",pos,new Vector3(.6f,.12f,.35f),steel,false);var l=Group(p,n).gameObject.AddComponent<Light>();l.transform.localPosition=pos-Vector3.up*.12f;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.range=warm?12:17;l.intensity=power;l.color=warm?new Color(1,.75f,.4f):new Color(.67f,.8f,1);l.shadows=LightShadows.Soft;
    }
    static void Interior(Transform root)
    {
        var p=Group(root,"Underground office");
        Box(p,"Office rear partition",new(11,-4.5f,83.8f),new(7.8f,3,.2f),wood);
        Box(p,"Desk top",new(12.7f,-5.15f,81.5f),new(2.4f,.15f,1),wood);
        foreach(float x in new[]{11.8f,13.6f})Box(p,"Desk leg",new(x,-5.6f,81.5f),new(.15f,.8f,.8f),steel);
        Box(p,"Locker",new(7.8f,-4.9f,82),new(1,2.2f,1),steel);
        Box(p,"Office noticeboard",new(11,-4.1f,83.65f),new(2.5f,1.3f,.1f),yellow,false);
        Lamp(p,"Office work light",new(11,-3.25f,80.2f),true,65);
        Lamp(p,"Office doorway glow",new(11,-3.25f,74.5f),true,28);
        var tunnels=Group(root,"Tunnel timber supports");
        foreach(var route in Routes)for(int i=1;i<route.Length;i++)
        {
            var a=route[i-1];var b=route[i];var delta=(b-a).normalized;var side=new Vector2(-delta.y,delta.x);
            for(float distance=5;distance<Vector2.Distance(a,b)-2;distance+=8)
            {
                var q=a+delta*distance;if(Height(q.x,q.y)<-2.5f)continue;
                var left=new Vector3(q.x+side.x*2.45f,-4.5f,q.y+side.y*2.45f);var right=new Vector3(q.x-side.x*2.45f,-4.5f,q.y-side.y*2.45f);
                Box(tunnels,"Timber upright",left,new(.2f,3,.2f),wood);Box(tunnels,"Timber upright",right,new(.2f,3,.2f),wood);
                Beam(tunnels,"Roof crossbeam",left+Vector3.up*1.35f,right+Vector3.up*1.35f,.2f,wood);
            }
        }
        Lamp(tunnels,"North tunnel marker",new(0,-3.3f,101),false,20);Lamp(tunnels,"South tunnel marker",new(0,-3.3f,63),false,18);
        Lamp(root,"West portal light",new(-25,-2.7f,92),true,36);Lamp(root,"East portal light",new(25,-2.7f,72),true,36);
    }
    static void WestClimb(Transform root)
    {
        var p=Group(root,"West rim climbing ledges");
        // Broad half-metre hand-climb shelves, continuous from surface over the rim.
        for(int i=0;i<5;i++){float z=54+i*1.5f;float y=Mathf.Min(2,i*.6f);Box(p,"Outer climb shelf "+i,new(-26,y-.25f,z),new(5,.5f,1.6f),rock);}
        for(int i=0;i<17;i++){float z=61+i*.9f;float y=2-i*.5f;Box(p,"Inner descent shelf "+i,new(-26,y-.25f,z),new(5,.5f,1.05f),rock);}
    }
    static void Machine(Transform root,string name,Vector3 pos,bool excavator=false)
    {
        var p=Group(root,name);p.localPosition=pos;
        foreach(float x in new[]{-1.7f,1.7f})Box(p,"Heavy tread",new(x,.6f,0),new(.9f,1.2f,5.5f),steel);
        Box(p,"Machine body",new(0,1.6f,0),new(3.5f,1.3f,4),yellow);
        Box(p,"Cab",new(-.6f,2.8f,-.6f),new(1.9f,1.7f,2),steel);
        Box(p,"Cab window",new(-.6f,2.9f,.43f),new(1.5f,1.1f,.08f),glass,false);
        if(excavator){Beam(p,"Excavator boom",new(1,2,1),new(1,5,5),.6f,yellow);Beam(p,"Excavator stick",new(1,5,5),new(1,1,7),.5f,yellow);Box(p,"Bucket",new(1,.7f,7),new(2,1.3f,1.6f),steel);}
        else{Beam(p,"Loader arm",new(0,1.5f,1),new(0,.7f,4),.5f,yellow);Box(p,"Loader bucket",new(0,.5f,4),new(4,1,1.7f),steel);}
    }
    static void Crane(Transform root)
    {
        var p=Group(root,"Crane");Box(p,"Crane pad",new(30,.1f,137),new(12,.2f,10),gravel);
        Box(p,"Crane base",new(30,1,137),new(6,1.8f,6),steel);
        for(int y=2;y<13;y+=2){foreach(float x in new[]{28.5f,31.5f})foreach(float z in new[]{135.5f,138.5f})Box(p,"Tower column",new(x,y+1,z),new(.3f,2,.3f),yellow);Beam(p,"Tower diagonal",new(28.5f,y,135.5f),new(31.5f,y+2,135.5f),.18f,yellow);Beam(p,"Tower diagonal",new(28.5f,y,138.5f),new(31.5f,y+2,138.5f),.18f,yellow);}
        Box(p,"Operator cab",new(30,12.5f,135),new(3,2,2),steel);Box(p,"Crane cab glazing",new(30,12.6f,133.96f),new(2.5f,1.3f,.08f),glass,false);
        foreach(float x in new[]{29f,31f}){Beam(p,"Lattice boom lower",new(x,14,142),new(x,14,112),.22f,yellow);Beam(p,"Lattice boom upper",new(x,15.5f,142),new(x,15.5f,112),.22f,yellow);for(int z=112;z<142;z+=3)Beam(p,"Boom bracing",new(x,14,z),new(x,15.5f,z+3),.14f,yellow);}
        Box(p,"Counterweight",new(30,14.5f,142),new(4,2,3),rock);Beam(p,"Hoist cable",new(30,14,112),new(30,-1,112),.065f,steel);Box(p,"Hook block",new(30,-1,112),new(.6f,.7f,.5f),yellow);
        Lamp(p,"Crane worklight",new(30,10,134),false,90);
    }
    static void Spoil(Transform root,Vector3 pos,Vector3 size,int index)
    {
        var s=new Surface();var a=pos+new Vector3(-size.x/2,0,-size.z/2);var b=pos+new Vector3(-size.x/2,0,size.z/2);var c=pos+new Vector3(size.x/2,0,size.z/2);var d=pos+new Vector3(size.x/2,0,-size.z/2);var tip=pos+Vector3.up*size.y;
        s.Quad(a,b,tip,tip);s.Quad(b,c,tip,tip);s.Quad(c,d,tip,tip);s.Quad(d,a,tip,tip);s.Build(root,"Gravel pile "+index,gravel);
    }
    static void Outer(Transform root)
    {
        var p=Group(root,"Surface machinery and gravel");Crane(p);Machine(p,"Excavator",new(-29,0,35),true);Machine(p,"Loader",new(23,0,148));
        var truck=Group(p,"Haul truck");truck.localPosition=new(12,0,24);Box(truck,"Truck body",new(0,1,0),new(3,1.5f,5),yellow);Box(truck,"Truck cab",new(0,2.2f,-1.4f),new(2.5f,1.6f,1.8f),steel);Box(truck,"Gravel bed",new(0,2,1.3f),new(3.2f,1,2.8f),gravel);
        int i=0;foreach(var q in new[]{new Vector3(-36,0,18),new(-22,0,18),new(-36,0,133),new(-36,0,48),new(37,0,11),new(-20,0,142)})Spoil(p,q,new(8,2.5f,9),i++);
        foreach(float z in new[]{146f,150f})Box(p,"Spare steel stock",new(-32,.6f,z),new(12,1.2f,1.5f),steel);
        foreach(var q in new[]{new Vector3(-10,4,60),new Vector3(9,4,116),new Vector3(9,4,28)}){Box(p,"Yard lamp post",new(q.x,2,q.z),new(.18f,4,.18f),steel);Lamp(p,"Yard worklight",q,false,55);}
    }
    static void Guard(){if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Duck Rails Edit Mode with no bake");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save current scene work first");}
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Install approved V5")]
    public static void Install()
    {
        Guard();Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();boxes.Clear();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");if(!cube)throw new Exception("Missing cube UV");
        rock=Mat("Quarry rock","concrete",new(.55f,.54f,.49f));grass=Mat("Scrub ground","grass",new(.5f,.57f,.4f));gravel=Mat("Gravel","gravel",new(.66f,.62f,.49f));wood=Mat("Tunnel timber","timber",new(.5f,.36f,.22f));steel=Mat("Machinery steel","weathered paint",new(.37f,.43f,.43f));yellow=Mat("Faded machinery yellow","weathered paint",new(.88f,.63f,.18f));glass=Mat("Cab glass","weathered paint",new(.12f,.23f,.27f));
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();if(!b||b.id!=Id)throw new Exception("Wrong block");
        foreach(Transform t in b.transform.Cast<Transform>().ToArray())
            if(t.name==RootName||t.name=="Block ground"||t.name=="Quarry rim"||t.name=="Lower terrace"||t.name=="Access ramp"||t.name.StartsWith("Conveyor")||t.name=="QUARRY"||t.name.StartsWith("STOP ")||t.name=="Left-side buildings"||t.name=="Baked block sample lights")Object.DestroyImmediate(t.gameObject);
        var root=Group(b.transform,RootName);Terrain(root);WestClimb(root);Interior(root);Outer(root);
        b.enemyBounds=new Bounds(new Vector3(0,-1,80),new Vector3(80,12,70));
        b.enemySurfaceRailExclusion=new Bounds(new Vector3(0,8,80),new Vector3(14,20,160));
        var blocker=Group(root,"Surface railway navigation exclusion").gameObject.AddComponent<CrawlGraphBlocker>();var bx=blocker.GetComponent<BoxCollider>();bx.isTrigger=true;bx.center=b.enemySurfaceRailExclusion.center;bx.size=b.enemySurfaceRailExclusion.size;
        for(int v=0;v<b.variations.Length;v++)
        {
            float dx=(v-1)*.4f;var coal=new[]{new Vector3(-25,-5.5f,83),new(-25,-5.5f,99),new(-13,-5.5f,106),new(26,-5.5f,52),new(26,-5.5f,74),new(26,-5.5f,104)};
            var enemies=new[]{new Vector3(-25,-5.8f,92),new(-8,-5.8f,104.7f),new(0,-5.8f,82),new(0,-5.8f,63),new(26,-5.8f,62),new(26,-5.8f,100)};
            for(int i=0;i<6;i++){Set(b.variations[v].coal[i],coal[i]+Vector3.right*dx);Set(b.variations[v].enemies[i],enemies[i]+Vector3.right*dx);}
            Set(b.variations[v].gold[0],new Vector3(9,-5.5f,81+v*.3f));Set(b.variations[v].gold[1],new Vector3(-25,-5.5f,106+v*.4f));
        }
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();baker.transform.localPosition=new(0,-2,80);baker.size=new(80,32,128);baker.cellSize=1;baker.allowCeilings=false;baker.surfaceMask=1<<6;baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Mine graph bake failed");PrefabUtility.RecordPrefabInstancePropertyModifications(baker);PrefabUtility.RecordPrefabInstancePropertyModifications(baker.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(int z=4;z<160;z+=8)foreach(float x in new[]{-40f,-26f,-12f,0f,12f,26f,38f})foreach(float y in new[]{.7f,2.5f,5f})points.Add(new Vector3(x,y,z));
        for(int z=20;z<130;z+=4)for(int x=-34;x<36;x+=4)if(Tunnel(x,z)||Height(x,z)<-5.9f)foreach(float y in new[]{-5.4f,-4.2f})if(!Physics.CheckSphere(new Vector3(x,y,z),.2f,1<<6))points.Add(new(x,y,z));
        probes.probePositions=points.ToArray();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Twin mine V5 installed and graph baked. Run geometry checks before Bake lighting.");
    }
    static void Set(Transform t,Vector3 p){t.localPosition=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    public static void RefreshTerrainMaterials()
    {
        Guard();rock=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Quarry rock.mat");grass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Scrub ground.mat");gravel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Gravel.mat");
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RootName);
        Object.DestroyImmediate(root.Find("Excavated terrain").gameObject);Terrain(root);Physics.SyncTransforms();
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Graph bake failed");PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
#endif
