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

/// <summary>Approved farm V5: west orchard and extensive dense corn. Editor-only, explicit installation.</summary>
public static partial class RailFarmSilos
{
    public const string Id="farm-silos-v1", RootName="Farm authored v3", Folder="Assets/DuckRails/FarmSilos";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/Bake.unity";
    public const int LightCount=18;
    static Mesh cube,octa;
    static readonly Dictionary<string,Mesh> meshes=new();
    static Material grass,dirt,wood,white,red,roof,steel,hay,leaves,apple,glass,stone;
    public static readonly Vector2[] ApplePositions={new(9,124),new(9,136),new(9,148),new(19,151),new(39,124),new(41,136),new(41,149),new(16,119),new(32,119)};
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static void Guard(){if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Duck Rails Edit Mode, no bake");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save scene work first");}
    [MenuItem("Duck Rails/Authoring/Farm Silos/Install approved farm")]
    public static void Install()
    {
        Guard();Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();meshes.Clear();Materials();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");
        if(!cube)throw new Exception("Missing UV cube");octa=Octa();
        var scene=EditorSceneManager.OpenScene(BakeScene);var b=Object.FindFirstObjectByType<RailBlock>();if(!b||b.id!=Id)throw new Exception("Wrong block");
        var track=b.track.ToArray();var bounds=b.enemyBounds;
        foreach(Transform t in b.transform.Cast<Transform>().ToArray())if(t.name==RootName||t.name=="Block ground"||t.name.StartsWith("Barn ")||t.name=="Grain silo"||t.name=="Pen fence"||t.name=="FARM SILOS"||t.name.StartsWith("STOP ")||t.name=="Left-side buildings"||t.name=="Baked block sample lights")Object.DestroyImmediate(t.gameObject);
        var root=Group(b.transform,RootName);GroundV5(root);Farm(root);House(root);RelocateHouse(root);OuterV5(root);CharacterV5(root);
        // Retain the conventional west-side group for catalogue tooling.
        var left=Group(b.transform,"Left-side buildings");Shed(left,"Feed barn",-34,69,18,14,3.2f);Shed(left,"Farm workshop",-34,88,16,12,3.2f);
        HayStack(left,"Feed roof hay pyramid",-27,63,6,6,3.2f,5);
        Lamp(left,new Vector3(-25,2.9f,76));Lamp(left,new Vector3(-26,2.9f,94));
        for(int v=0;v<b.variations.Length;v++)
        {
            float dx=(v-1)*.5f;
            var coal=new[]{new Vector3(-25,.5f,75),new Vector3(-24,.5f,95),new Vector3(-16,.5f,101),new Vector3(24,.5f,72),new Vector3(29,.5f,87),new Vector3(23,.5f,108)};
            var enemies=new[]{new Vector3(13,.2f,65),new Vector3(23,.2f,72),new Vector3(30,.2f,84),new Vector3(13,.2f,95),new Vector3(27,.2f,108),new Vector3(35,.2f,112)};
            for(int i=0;i<6;i++){Set(b.variations[v].coal[i],coal[i]+Vector3.right*dx);Set(b.variations[v].enemies[i],enemies[i]+Vector3.right*dx);}
            Set(b.variations[v].gold[0],new Vector3(-30,.5f,94+v));Set(b.variations[v].gold[1],new Vector3(20,.5f,84+v));
        }
        var baker=b.GetComponentInChildren<CrawlGraphBaker>();baker.seeds=b.variations.SelectMany(v=>v.enemies).ToArray();Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Farm graph bake failed");PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        if(!track.SequenceEqual(b.track)||bounds!=b.enemyBounds)throw new Exception("Contract changed");
        var lighting=b.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(float z=2;z<160;z+=8)foreach(float x in new[]{-40f,-26f,-12f,0f,12f,22f,32f,42f})foreach(float y in new[]{.6f,2f,5f})if(!Physics.CheckSphere(new Vector3(x,HeightV5(x,z)+y,z),.2f,1<<6))points.Add(new Vector3(x,HeightV5(x,z)+y,z));
        foreach(float y in new[]{-2.6f,-1.2f,.7f,2f,3.9f,5.2f,7.1f,8.3f})foreach(float x in new[]{17f,23f,28f,31.5f})foreach(float z in new[]{131f,137f,143f})if(!Physics.CheckSphere(HousePoint(new Vector3(x,y,z)),.2f,1<<6))points.Add(HousePoint(new Vector3(x,y,z)));
        probes.probePositions=points.ToArray();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        RailCatalogLightingAuthoring.BakeOpen();Debug.Log("Farm installed; offline graph baked; lighting export queued");
    }
    static void Set(Transform t,Vector3 p){t.localPosition=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    static Material Mat(string n,string source,Color tint)
    {
        string path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville "+source+".mat");if(!original)throw new Exception("Missing Quackville material "+source);
        if(!m){m=new Material(original);AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(m);return m;
    }
    static void Materials()
    {
        grass=Mat("Pasture","grass",new Color(.65f,.72f,.48f));dirt=Mat("Farm lanes","gravel",new Color(.63f,.52f,.36f));wood=Mat("Old timber","timber",new Color(.63f,.48f,.31f));
        white=Mat("White farmhouse siding","timber",new Color(.95f,.94f,.87f));red=Mat("Barn red timber","timber",new Color(.65f,.23f,.17f));roof=Mat("Dark shingles","dark roofing",new Color(.44f,.47f,.46f));
        steel=Mat("Silo metal","weathered paint",new Color(.69f,.73f,.72f));hay=Mat("Golden hay","timber",new Color(.9f,.69f,.25f));leaves=Mat("Apple foliage","foliage",new Color(.32f,.53f,.22f));
        apple=Mat("Red apples","foliage",new Color(.85f,.12f,.07f));glass=Mat("Dark window glass","weathered paint",new Color(.12f,.23f,.26f));stone=Mat("Foundation stone","concrete",new Color(.65f,.64f,.59f));
        CropTile(white,"Weathered white boards",128,192);CropTile(red,"Red barn boards",64,64);
    }
    static void CropTile(Material material,string name,int x,int y)
    {
        Directory.CreateDirectory(Folder+"/Textures");string path=Folder+"/Textures/"+name+".png";
        if(!File.Exists(path))
        {
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Quackville/Quackville_Town_Diffuse.jpg");
            var rt=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var tex=new Texture2D(64,64,TextureFormat.RGB24,false);
            try{Graphics.Blit(atlas,rt);RenderTexture.active=rt;tex.ReadPixels(new Rect(x,256-y-64,64,64),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));EditorUtility.SetDirty(material);
    }
    static Mesh Save(string n,Mesh m)
    {
        m.name=n;string path=Folder+"/Meshes/"+n+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved)
        {
            // Mesh setters refresh GPU buffers as well as serialized geometry on repeat installs.
            saved.Clear();saved.name=n;saved.indexFormat=m.indexFormat;saved.vertices=m.vertices;saved.normals=m.normals;saved.uv=m.uv;saved.uv2=m.uv2;saved.tangents=m.tangents;
            saved.subMeshCount=m.subMeshCount;for(int i=0;i<m.subMeshCount;i++)saved.SetTriangles(m.GetTriangles(i),i);saved.bounds=m.bounds;saved.UploadMeshData(false);
            Object.DestroyImmediate(m);EditorUtility.SetDirty(saved);return saved;
        }
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject MeshGo(Transform p,string n,Mesh mesh,Material mat,Vector3 pos,Vector3 scale,bool collide=false)
    {
        var g=Group(p,n).gameObject;g.layer=6;g.transform.localPosition=pos;g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.25f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(collide)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material mat,bool collide=true)
    {
        string key=Hash128.Compute(size.ToString("F4")).ToString();if(!meshes.TryGetValue(key,out var mesh))
        {mesh=Object.Instantiate(cube);var v=mesh.vertices;var norms=mesh.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=(Mathf.Abs(norms[i].y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(norms[i].x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}mesh.uv=uv;mesh=Save("Box-"+key,mesh);meshes.Add(key,mesh);}
        var g=MeshGo(p,n,mesh,mat,pos,size);if(collide)g.AddComponent<BoxCollider>();return g;
    }
    static void Slab(Transform p,string n,float x,float z,float w,float d,float y,Material mat)=>Box(p,n,new Vector3(x+w/2,y-.1f,z+d/2),new Vector3(w,.2f,d),mat);
    static void Pad(Transform p,float x,float z,float w,float d)=>Box(p,"Dirt lane",new Vector3(x+w/2,.012f,z+d/2),new Vector3(w,.018f,d),dirt,false);
    static void Wall(Transform p,string n,bool az,float axis,float a,float b,float y,float h,Material mat,params float[] doors)
    {
        float cursor=a;for(int i=0;i<=doors.Length;i+=2){float end=i<doors.Length?doors[i]:b;Seg(cursor,end,y,h);if(i<doors.Length){Seg(end,doors[i+1],y+2.65f,h-2.65f);cursor=doors[i+1];}}
        void Seg(float lo,float hi,float bottom,float height){if(hi<=lo||height<=0)return;Box(p,n,az?new Vector3(axis,bottom+height/2,(lo+hi)/2):new Vector3((lo+hi)/2,bottom+height/2,axis),az?new Vector3(.22f,height,hi-lo):new Vector3(hi-lo,height,.22f),mat);}
    }
    static void Ground(Transform p)
    {
        var g=Group(p,"Ground with basement excavation");
        Slab(g,"West ground",-48,0,62,160,0,grass);Slab(g,"East ground",34,0,14,160,0,grass);
        Slab(g,"South ground",14,0,20,128,0,grass);Slab(g,"North seam",14,153,20,7,0,grass);Slab(g,"Rear garden",14,146,14,7,0,grass);
        // Basement and open exterior cellar stairwell have no ground slab above them.
        foreach(float z in new[]{26f,132f})foreach(float x in new[]{-43f,44f})
        {var go=MeshGo(g,"Low perimeter pasture bank",Mound(),grass,new Vector3(x,0,z),new Vector3(3,1,12),true);}
    }
    static Mesh Mound()
    {
        if(meshes.TryGetValue("mound",out var m))return m;
        m=new Mesh();m.vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1),new Vector3(0,.8f,0)};m.triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4};m.uv=m.vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);m=Save("Pasture-bank",m);meshes["mound"]=m;return m;
    }
    static void Lamp(Transform p,Vector3 pos)
    {Box(p,"Lamp housing",pos,new Vector3(.5f,.12f,.35f),steel,false);var l=Group(p,"Baked farm light").gameObject.AddComponent<Light>();l.transform.localPosition=pos-Vector3.up*.12f;l.transform.localRotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=50;l.range=18;l.spotAngle=125;l.innerSpotAngle=80;l.color=new Color(1,.87f,.67f);l.shadows=LightShadows.Soft;}
    static void Shed(Transform root,string name,float x,float z,float w,float d,float h)
    {var p=Group(root,name);Slab(p,"Floor",x,z,w,d,0,wood);Wall(p,"Side",true,x,z,z+d,0,h,red);Wall(p,"Side",true,x+w,z,z+d,0,h,red);Wall(p,"Rear",false,z+d,x,x+w,0,h,red,x+w/2-1.5f,x+w/2+1.5f);Wall(p,"Front",false,z,x,x+w,0,h,red,x+w/2-1.5f,x+w/2+1.5f);Slab(p,"Walkable roof",x-.2f,z-.2f,w+.4f,d+.4f,h,roof);Box(p,"Crates",new Vector3(x+2,.6f,z+d-2),new Vector3(2,1.2f,1.5f),wood);}
    static void HayStack(Transform root,string name,float cx,float z,float w,float d,float h,int levels,float y=0)
    {var p=Group(root,name);for(int tier=0;tier<levels;tier++){float inset=tier*(w-2)/(2*levels);float zz=z+tier*d/levels;float width=w-2*inset;int count=Mathf.Max(1,Mathf.RoundToInt(width/1.5f));for(int j=0;j<count;j++){var pos=new Vector3(cx-width/2+(j+.5f)*width/count,y+(tier+.5f)*h/levels,zz+(d-(zz-z))/2);Box(p,"Hay bale tier "+tier,pos,new Vector3(width/count-.035f,h/levels-.025f,d-(zz-z)),hay);}}}
    static void Farm(Transform root)
    {
        var p=Group(root,"Barn and silos");Slab(p,"Barn floor",17,66,18,23,0,wood);
        Wall(p,"Barn west",true,17,66,89,0,5.2f,red,73,79);Wall(p,"Barn east",true,35,66,89,0,5.2f,red);Wall(p,"Barn south",false,66,17,35,0,5.2f,red,23,27);Wall(p,"Barn north",false,89,17,35,0,5.2f,red,24,29);
        PitchedRoof(p,"Barn roof",17,66,18,23,5.2f,8.2f,roof);Slab(p,"Lean-to roof",17,60,18,6,2.7f,roof);
        foreach(float x in new[]{17.2f,34.8f})Box(p,"Lean-to post",new Vector3(x,1.35f,60.3f),new Vector3(.25f,2.7f,.25f),wood);
        HayStack(p,"Barn hay pyramid",27,54,8,6,2.7f,5);HayStack(p,"Upper hay pyramid",27,60,6,6,2.6f,5,2.7f);
        foreach(float x in new[]{21f,31f})Box(p,"Barn storage stack",new Vector3(x,.75f,82),new Vector3(2,1.5f,2),hay);
        foreach(float x in new[]{22f,33f}){Cylinder(p,"Grain silo",new Vector3(x,6,98),5,12,steel);Cone(p,"Silo cap",new Vector3(x,12,98),5,2,steel);foreach(float y in new[]{1f,4f,8f,11.8f})Cylinder(p,"Silo band",new Vector3(x,y,98),5.04f,.12f,roof);}
        var t=Group(root,"Water tower");foreach(float x in new[]{-26.5f,-21.5f})foreach(float z in new[]{55.5f,60.5f})Box(t,"Tower leg",new Vector3(x,4,z),new Vector3(.4f,8,.4f),steel);
        foreach(float z in new[]{55.5f,60.5f})Beam(t,new Vector3(-26.5f,.5f,z),new Vector3(-21.5f,7,z),.2f,steel);
        Cylinder(t,"Water tank",new Vector3(-24,10,58),4,4,steel);Cone(t,"Tank cap",new Vector3(-24,12,58),4,1.5f,roof);
        foreach(var pos in new[]{new Vector3(23,4.6f,71),new Vector3(29,4.6f,83),new Vector3(25,2.4f,63)})Lamp(p,pos);
        foreach(var pos in new[]{new Vector3(12,5,61),new Vector3(12,5,102),new Vector3(-13,5,64),new Vector3(-13,5,104)}){Box(root,"Yard light post",pos/2+new Vector3(pos.x/2,0,pos.z/2),new Vector3(.15f,pos.y,.15f),steel);Lamp(root,pos);}
    }
    static void Beam(Transform p,Vector3 a,Vector3 b,float width,Material mat){var g=Box(p,"Brace",(a+b)/2,new Vector3(width,(b-a).magnitude,width),mat);g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Cylinder(Transform p,string n,Vector3 pos,float radius,float height,Material mat)=>MeshGo(p,n,RoundMesh(false),mat,pos,new Vector3(radius,height,radius),true);
    static void Cone(Transform p,string n,Vector3 pos,float radius,float height,Material mat)=>MeshGo(p,n,RoundMesh(true),mat,pos,new Vector3(radius,height,radius),true);
    static Mesh RoundMesh(bool cone)
    {
        string key=cone?"Cone":"Cylinder";if(meshes.TryGetValue(key,out var m))return m;var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c){int k=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.y),new Vector2(b.x,b.y),new Vector2(c.x,c.y)});tr.AddRange(new[]{k,k+1,k+2});}
        for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;var aa=new Vector3(Mathf.Cos(a),cone?0:-.5f,Mathf.Sin(a));var bb=new Vector3(Mathf.Cos(b),cone?0:-.5f,Mathf.Sin(b));if(cone)Tri(aa,Vector3.up,bb);else{Tri(aa,aa+Vector3.up,bb+Vector3.up);Tri(aa,bb+Vector3.up,bb);Tri(Vector3.up*.5f,bb+Vector3.up,aa+Vector3.up);}Tri(new Vector3(0,cone?0:-.5f,0),aa,bb);}
        m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);m=Save(key,m);meshes[key]=m;return m;
    }
    static void PitchedRoof(Transform p,string name,float x,float z,float w,float d,float eave,float ridge,Material mat)
    {
        // Ridge runs east-west; stairs arrive at the attic's full-height centre.
        var g=Group(p,name);float rise=ridge-eave,run=d/2+.4f,len=Mathf.Sqrt(run*run+rise*rise),angle=Mathf.Atan2(rise,run)*Mathf.Rad2Deg;
        foreach(int s in new[]{-1,1}){var slab=Box(g,"Roof slope",new Vector3(x+w/2,(ridge+eave)/2,z+d/2+s*run/2),new Vector3(w+.8f,.22f,len),mat);slab.transform.localRotation=Quaternion.Euler(s*angle,0,0);}
        foreach(float xx in new[]{x,x+w}){var m=new Mesh();m.vertices=new[]{new Vector3(0,eave,z),new Vector3(0,ridge,z+d/2),new Vector3(0,eave,z+d),new Vector3(0,eave,z),new Vector3(0,ridge,z+d/2),new Vector3(0,eave,z+d)};m.triangles=new[]{0,1,2,5,4,3};m.uv=m.vertices.Select(v=>new Vector2(v.z,v.y)/3).ToArray();m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);m=Save(name+"-gable-"+xx,m);MeshGo(g,"Gable wall",m,mat==roof?white:red,new Vector3(xx,0,0),Vector3.one,true);}
    }
    static void House(Transform root)
    {
        var p=Group(root,"Farmhouse");var cellar=Group(p,"Basement");Slab(cellar,"Basement floor",14,128,20,18,-3.2f,stone);
        Wall(cellar,"Foundation",true,14,128,146,-3.2f,3.2f,stone);Wall(cellar,"Foundation",true,34,128,146,-3.2f,3.2f,stone);Wall(cellar,"Foundation",false,128,14,34,-3.2f,3.2f,stone);Wall(cellar,"Rear cellar doorway",false,146,14,34,-3.2f,3.2f,stone,29,32.5f);
        Wall(cellar,"Utility partition",true,23,128,146,-3.2f,3.2f,stone,135,138);
        Box(cellar,"Cellar shelf",new Vector3(16,-2.25f,143),new Vector3(2,1.9f,3),wood);Box(cellar,"Utility tank",new Vector3(25,-2.2f,143),new Vector3(1.5f,2,1.5f),steel);
        var ext=Group(p,"Exterior cellar stair");Slab(ext,"Cellar exit landing",28,146,6,1,-3.2f,stone);Stair(ext,29,147,3.2f,5.8f,-3.2f,3.2f,18);
        Wall(ext,"Retaining wall",true,28,146,153,-3.2f,3.2f,stone);Wall(ext,"Retaining wall",true,34,146,153,-3.2f,3.2f,stone);
        foreach(float x in new[]{28f,34f})Box(ext,"Cellar guard",new Vector3(x,.6f,149),new Vector3(.12f,1.2f,6),wood);
        for(int floor=0;floor<3;floor++)
        {
            float y=floor*3.2f;var f=Group(p,floor==0?"Ground floor":floor==1?"Upper floor":"Attic");
            Slab(f,"Floor west",14,128,15.5f,18,y,wood);Slab(f,"Floor east",33.5f,128,.5f,18,y,wood);Slab(f,"Stair bottom landing",29.5f,128,4,1.8f,y,wood);Slab(f,"Stair top landing",29.5f,137.2f,4,8.8f,y,wood);
            Stair(f,30,130,3,7.2f,y-3.2f,3.2f,18);
            Box(f,"Stairwell west guard",new Vector3(29.5f,y+.55f,133.5f),new Vector3(.12f,1.1f,7.4f),white);
            if(floor<2)
            {
                WindowWall(f,false,128,14,34,y,new[]{17f,21f,27f,31f},floor==0?new Vector2(22.5f,25.5f):(Vector2?)null);
                WindowWall(f,false,146,14,34,y,new[]{17f,22f,26f},floor==0?new Vector2(29,32.5f):(Vector2?)null);
                WindowWall(f,true,14,128,146,y,new[]{132f,138f,143f},null);WindowWall(f,true,34,128,146,y,new[]{132f,141f},null);
                // Central hall, connected front/back, with broad room doors.
                Wall(f,"West hall partition",true,22,128,146,y,3.2f,white,130,133,139,142);
                Wall(f,"East hall partition",true,27.5f,128,146,y,3.2f,white,130,133,139,142);
                Wall(f,"West room division",false,137,14,22,y,3.2f,white);
                if(floor==0){Box(f,"Dining table",new Vector3(18,y+.7f,132),new Vector3(3,1.4f,2),wood);Box(f,"Kitchen counter",new Vector3(16,y+.5f,143),new Vector3(2,1,4),white);Box(f,"Living bench",new Vector3(29,y+.4f,143),new Vector3(2,.8f,1),wood);}
                else foreach(float z in new[]{132f,142f})Box(f,"Bed",new Vector3(17,y+.4f,z),new Vector3(2.2f,.8f,3),white);
            }
            else{Box(f,"Attic storage",new Vector3(18,7,137),new Vector3(2,1.2f,2),wood);}
            Lamp(f,new Vector3(19,y+2.8f,134));Lamp(f,new Vector3(25,y+2.8f,141));
        }
        Lamp(cellar,new Vector3(19,-.4f,134));Lamp(cellar,new Vector3(26,-.4f,141));
        PitchedRoof(p,"House roof",14,128,20,18,6.7f,11,roof);
        foreach(float z in new[]{128f,146f})Wall(p,"Attic knee wall",false,z,14,34,6.4f,.4f,white);
        foreach(float x in new[]{14f,34f})Wall(p,"Attic knee wall",true,x,128,146,6.4f,.4f,white);
        // Broad porch and its front-facing decorative gable evoke the reference photo.
        Slab(p,"Front porch",14,124,20,4,0,wood);Slab(p,"Porch canopy",13.6f,123.6f,20.8f,4.4f,2.95f,roof);
        foreach(float x in new[]{14.4f,18.5f,22f,26f,30f,33.6f})Box(p,"Porch column",new Vector3(x,1.45f,124.3f),new Vector3(.2f,2.9f,.2f),white);
        Lamp(p,new Vector3(24,2.7f,126));Box(p,"Chimney",new Vector3(16,8.4f,142),new Vector3(1.1f,6,1.1f),stone);
        // Small front gable projects from the main roof; window is an opaque rough blockout pane.
        var front=Group(p,"Front gable");
        var face=new Mesh();face.vertices=new[]{new Vector3(20,6.7f,127.5f),new Vector3(24,10,127.5f),new Vector3(28,6.7f,127.5f)};face.triangles=new[]{0,1,2};face.uv=new[]{Vector2.zero,new Vector2(1,1),new Vector2(2,0)};face.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(face);MeshGo(front,"White gable face",Save("Front-gable",face),white,Vector3.zero,Vector3.one);
        foreach(int side in new[]{-1,1}){var slope=Box(front,"Gable roof",new Vector3(24+side*2,8.35f,131),new Vector3(5.3f,.18f,7.6f),roof);slope.transform.localRotation=Quaternion.Euler(0,0,-side*39.5f);}
        Box(front,"Gable window",new Vector3(24,8,127.39f),new Vector3(1.1f,1.4f,.15f),glass,false);
        // Exterior routes are authored in world space by OuterV5.
    }
    static void WindowWall(Transform p,bool az,float axis,float a,float b,float y,float[] windows,Vector2? door)
    {
        var holes=windows.Select(c=>(a:c-.65f,b:c+.65f,door:false)).ToList();if(door.HasValue)holes.Add((door.Value.x,door.Value.y,true));holes=holes.OrderBy(h=>h.a).ToList();float cursor=a;
        foreach(var h in holes){Wall(p,"White siding",az,axis,cursor,h.a,y,3.2f,white);Wall(p,"Window header",az,axis,h.a,h.b,y+2.65f,.55f,white);if(!h.door){Wall(p,"Window sill",az,axis,h.a,h.b,y,.8f,white);var pos=az?new Vector3(axis,y+1.7f,(h.a+h.b)/2):new Vector3((h.a+h.b)/2,y+1.7f,axis);Box(p,"Window glass",pos,az?new Vector3(.12f,1.8f,h.b-h.a):new Vector3(h.b-h.a,1.8f,.12f),glass);Box(p,"Window mullion",pos,az?new Vector3(.16f,1.8f,.07f):new Vector3(.07f,1.8f,.16f),white,false);}cursor=h.b;}Wall(p,"White siding",az,axis,cursor,b,y,3.2f,white);
    }
    static void Stair(Transform p,float x,float z,float w,float run,float bottom,float rise,int n)
    {var t=Group(p,"Stair flight");for(int i=0;i<n;i++){float h=(i+1)*rise/n;Box(t,"Tread "+i,new Vector3(x+w/2,bottom+h-.11f,z+(i+.5f)*run/n),new Vector3(w,.22f,run/n),wood);}}
    static Mesh Octa()
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();var ring=new[]{Vector3.right,Vector3.forward,Vector3.left,Vector3.back};for(int s=-1;s<=1;s+=2)for(int i=0;i<4;i++){int k=v.Count;v.Add(Vector3.up*s);v.Add(ring[s==1?(i+1)%4:i]);v.Add(ring[s==1?i:(i+1)%4]);uv.AddRange(new[]{new Vector2(.5f,1),Vector2.zero,Vector2.right});tri.AddRange(new[]{k,k+1,k+2});}var smooth=new List<Vector3>();for(int i=0;i<v.Count;i+=3){var a=v[i];var b=v[i+1];var c=v[i+2];var ab=(a+b).normalized;var bc=(b+c).normalized;var ca=(c+a).normalized;smooth.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}var m=new Mesh();m.SetVertices(smooth);m.SetUVs(0,smooth.Select(q=>new Vector2(Mathf.Atan2(q.z,q.x)/(2*Mathf.PI)+.5f,q.y*.5f+.5f)).ToList());m.SetTriangles(Enumerable.Range(0,smooth.Count).ToArray(),0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);return Save("Low-poly foliage",m);
    }
    static void Tree(Transform p,Vector2 pos,bool fruit)
    {
        var t=Group(p,fruit?"Apple tree":"Field tree");Box(t,"Trunk",new Vector3(pos.x,1.8f,pos.y),new Vector3(.45f,3.6f,.45f),wood);
        foreach(var q in new[]{new Vector3(-.9f,3.3f,0),new Vector3(.9f,3.6f,.2f),new Vector3(0,4.5f,0)})MeshGo(t,"Leaf crown",octa,leaves,new Vector3(pos.x,0,pos.y)+q,new Vector3(1.8f,1.6f,1.8f));
        if(fruit)for(int i=0;i<9;i++){float a=i*2.4f;MeshGo(t,"Apple",octa,apple,new Vector3(pos.x+Mathf.Cos(a)*1.45f,3.1f+(i%3)*.45f,pos.y+Mathf.Sin(a)*1.45f),Vector3.one*.18f);}
    }
    static void Corn(Transform p,float x,float z,float w,float d,float gap)
    {
        var t=Group(p,"Corn field");Pad(t,x,z,w,d);var stalks=new List<CombineInstance>();var ears=new List<CombineInstance>();
        for(float xx=x+1;xx<x+w;xx+=2.5f)for(float zz=z+1;zz<z+d;zz+=1.3f)
        {if(zz>gap&&zz<gap+3)continue;float h=1.8f+.2f*Mathf.Sin(xx+zz);stalks.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(new Vector3(xx,h/2,zz),Quaternion.identity,new Vector3(.09f,h,.09f))});foreach(int s in new[]{-1,1})stalks.Add(new CombineInstance{mesh=octa,transform=Matrix4x4.TRS(new Vector3(xx+s*.3f,h*.6f,zz),Quaternion.Euler(0,0,s*-30),new Vector3(.5f,.12f,.15f))});ears.Add(new CombineInstance{mesh=octa,transform=Matrix4x4.TRS(new Vector3(xx+.1f,h*.8f,zz),Quaternion.identity,new Vector3(.1f,.25f,.1f))});}
        void Combined(List<CombineInstance> list,string n,Material mat){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(list.ToArray(),true,true);Unwrapping.GenerateSecondaryUVSet(m);m=Save(n+"-"+x+"-"+z,m);var go=MeshGo(t,n,m,mat,Vector3.zero,Vector3.one);go.GetComponent<MeshRenderer>().scaleInLightmap=.08f;}
        Combined(stalks,"Corn stalks and leaves",leaves);Combined(ears,"Corn ears",hay);
    }
    static void Outer(Transform root)
    {
        var p=Group(root,"Fields and orchard");Pad(p,-39,10,3,140);Pad(p,38,10,3,140);Pad(p,-36,47,74,3);Pad(p,-36,113,74,3);
        Corn(p,-33,13,21,29,26);Corn(p,13,12,21,15,1000);Corn(p,13,31,21,15,1000);Corn(p,-33,122,21,29,135);
        foreach(var pos in ApplePositions)Tree(p,pos,true);foreach(float z in new[]{12f,44f,66f,105f,151f})Tree(p,new Vector2(-43,z),false);foreach(float z in new[]{12f,45f,104f})Tree(p,new Vector2(43,z),false);
        Shed(p,"Cart shed",16,106,14,5,2.7f);HayStack(p,"Field hay pyramid",-18,115,8,6,2.1f,4);
        foreach(float z in new[]{17f,36f,124f,143f}){Fence(p,-35,z,-35,z+7);if(z<100)Fence(p,36,z,36,z+7);}
        Fence(p,-32,106,-15,106);Fence(p,-32,106,-32,111);Fence(p,-32,111,-23,111);Fence(p,-19,111,-15,111);Fence(p,-15,106,-15,111);
        var wagon=Group(p,"Old hay wagon");Box(wagon,"Wagon bed",new Vector3(-27,.9f,44.5f),new Vector3(7,.3f,2.8f),wood);Box(wagon,"Hay load",new Vector3(-27,1.6f,44.5f),new Vector3(5,1.1f,2.2f),hay);
        foreach(float x in new[]{-29.5f,-24.5f})foreach(float z in new[]{43f,46f})Box(wagon,"Wheel",new Vector3(x,.5f,z),new Vector3(.8f,1,.3f),roof);
        var tractor=Group(p,"Abandoned tractor");Box(tractor,"Engine",new Vector3(19,1,50.5f),new Vector3(2.5f,1.2f,1.8f),red);Box(tractor,"Seat",new Vector3(21,1,50.5f),new Vector3(1,.4f,1),roof);foreach(float x in new[]{18f,21.5f})foreach(float z in new[]{49.4f,51.6f})Box(tractor,"Wheel",new Vector3(x,.65f,z),new Vector3(1.1f,1.3f,.5f),roof);
    }
    static void Fence(Transform p,float x,float z,float xx,float zz){var a=new Vector3(x,0,z);var b=new Vector3(xx,0,zz);int n=Mathf.CeilToInt(Vector3.Distance(a,b)/3);for(int i=0;i<=n;i++)Box(p,"Fence post",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*.65f,new Vector3(.15f,1.3f,.15f),wood);foreach(float y in new[]{.45f,1f}){var g=Box(p,"Fence rail",(a+b)/2+Vector3.up*y,new Vector3(.1f,.12f,Vector3.Distance(a,b)),wood);g.transform.rotation=Quaternion.LookRotation(b-a);}}
}
#endif




