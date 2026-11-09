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

/// <summary>Campground-only authored terrain and Quackville rough surface pass.</summary>
public static partial class RailCampgroundGreybox
{
    public const string Id="lakeside-campground-v1";
    public const string RootName="Campground greybox v1";
    public const string Folder="Assets/DuckRails/CampgroundGreybox";
    public const string BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/"+Id+"-bake.unity";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab";
    static Material grass, gravel, timber, roof, concrete, foliage, water, metal;
    static Mesh cube;
    static readonly Vector2[] Shore={new(-29,109),new(-22,108),new(-19,103),new(-20,97),new(-17,91),new(-15,85),new(-17,78),new(-21,73),new(-20,65),new(-23,57),new(-27,51),new(-33,53),new(-36,59),new(-35,67),new(-31,73),new(-32,78),new(-38,83),new(-37,91),new(-34,97),new(-35,103)};
    // World-space path heights explicitly grade the two cabin routes.
    public static readonly Vector3[][] Paths={
        new[]{new Vector3(9,0,65),new Vector3(24,.35f,62),new Vector3(27,1,76),new Vector3(27,2,99),new Vector3(15,1,99),new Vector3(15,0,80),new Vector3(9,0,80)},
        new[]{new Vector3(15,0,80),new Vector3(27,1,76)},
        new[]{new Vector3(-3,0,65),new Vector3(-10,0,65),new Vector3(-10,0,78),new Vector3(-9,0,88),new Vector3(-3,0,88),new Vector3(-3,0,106),new Vector3(0,0,106)},
        new[]{new Vector3(-9,0,88),new Vector3(-11,0,88),new Vector3(-11,0,97)},
        new[]{new Vector3(-10,0,65),new Vector3(-11,0,62),new Vector3(-11,0,57)},
        new[]{new Vector3(15,1,99),new Vector3(20,1,110),new Vector3(20,1,117),new Vector3(20,1,126),new Vector3(23,1,140),new Vector3(13,1,144)},
        new[]{new Vector3(27,1,76),new Vector3(34,1,76)},
        new[]{new Vector3(27,2,99),new Vector3(34,2,99)},
        new[]{new Vector3(24,.35f,62),new Vector3(24,0,56),new Vector3(26,0,36),new Vector3(18,0,32),new Vector3(18,0,22),new Vector3(9,0,18)},
        new[]{new Vector3(24,0,56),new Vector3(34,0,56),new Vector3(34,0,50)},
        new[]{new Vector3(-10,0,65),new Vector3(-5,0,48),new Vector3(-14,0,44),new Vector3(-14,0,29),new Vector3(-3,0,24)},
        new[]{new Vector3(0,0,106),new Vector3(-4,0,106),new Vector3(-15,0,116),new Vector3(-25,1,127),new Vector3(-21,.3f,140),new Vector3(-5,0,145)},
        new[]{new Vector3(-15,0,116),new Vector3(-24,-.1f,113)},
        new[]{new Vector3(27,2,99),new Vector3(20,1,110)}
    };
    static readonly (Rect rect,float y)[] Pads={
        (new Rect(28.5f,70.5f,11,11),1),(new Rect(28.5f,93.5f,11,11),2),
        (new Rect(14.5f,116.5f,11,10),1),(new Rect(11.5f,21.5f,13,11),0),
        (new Rect(28.5f,45.5f,11,9),0),(new Rect(-16.5f,52.5f,11,10),0),
        (new Rect(-16.5f,90.5f,11,13),0),(new Rect(-34,24,19,16),0),
        (new Rect(24,30,16,13),0),(new Rect(13,130,22,13),1),
        (new Rect(-31,109,13,7),-.1f)};

    public static float LakeDistance(Vector2 p)
    {
        bool inside=false;float d=float.MaxValue;
        for(int i=0,j=Shore.Length-1;i<Shore.Length;j=i++)
        {
            Vector2 a=Shore[j],b=Shore[i],v=b-a;
            d=Mathf.Min(d,Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude)));
            if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside?-d:d;
    }
    static float Distance(Rect r,float x,float z)=>new Vector2(Mathf.Max(r.xMin-x,0,x-r.xMax),Mathf.Max(r.yMin-z,0,z-r.yMax)).magnitude;
    public static float Height(float x,float z)
    {
        if(Mathf.Abs(x)>=41||z<=7||z>=153||x>=-4&&x<=10)return 0;
        float y=0;
        if(x< -4)
        {
            float d=LakeDistance(new Vector2(x,z));
            y=d<0?-.8f-1.2f*Mathf.SmoothStep(0,1,-d/5):-.8f*(1-Mathf.SmoothStep(0,1,d/4));
        }
        else foreach(var pad in Pads.Where(a=>a.y>0))y=Mathf.Max(y,pad.y*(1-Mathf.SmoothStep(0,1,Distance(pad.rect,x,z)/14)));
        if(x< -10&&z>117)y=Mathf.Max(y,1.5f*(1-Mathf.SmoothStep(0,1,new Vector2((x+28)/13,(z-130)/15).magnitude)));
        float nearest=99,grade=0;
        foreach(var path in Paths)for(int i=1;i<path.Length;i++)
        {
            var a=new Vector2(path[i-1].x,path[i-1].z);var v=new Vector2(path[i].x,path[i].z)-a;
            float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x,z)-a,v)/v.sqrMagnitude),d=Vector2.Distance(new Vector2(x,z),a+v*t);
            if(d<nearest){nearest=d;grade=Mathf.Lerp(path[i-1].y,path[i].y,t);}
        }
        y=Mathf.Lerp(y,grade,1-Mathf.SmoothStep(0,1,(nearest-1.7f)/2));
        foreach(var pad in Pads)y=Mathf.Lerp(y,pad.y,1-Mathf.SmoothStep(0,1,Distance(pad.rect,x,z)/1.5f));
        // A dry shallow gully under the footbridge; the paths grade its banks, not its bed.
        if(x>11&&x<33&&z>110&&z<116)y-=.9f*Mathf.Sin(Mathf.PI*(z-110)/6)*Mathf.SmoothStep(0,1,Mathf.Min(x-11,33-x)/3);
        float edge=Mathf.Min((41-Mathf.Abs(x))/3,(z-7)/5,(153-z)/5);
        return y*Mathf.SmoothStep(0,1,edge);
    }
    [MenuItem("Duck Rails/Authoring/Campground/Install greybox and rough Quackville materials")]
    public static void Install()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Use Duck Rails Edit Mode, no bake running");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene work first");
        Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Textures");AssetDatabase.Refresh();
        PrepareMaterials();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");
        if(!cube)throw new Exception("Missing lightmap UV cube");
        var scene=EditorSceneManager.OpenScene(BakeScene);var block=Object.FindFirstObjectByType<RailBlock>();
        if(!block||block.id!=Id)throw new Exception("Wrong block");
        var track=block.track.ToArray();
        foreach(var t in block.transform.Cast<Transform>().ToArray())
            if(t.name==RootName||t.name=="Block ground"||t.name.StartsWith("Camp cabin")||t.name=="Lake surface"||t.name=="Dock"||t.name.StartsWith("Picnic table")||t.name=="Left-side buildings"||t.name=="Baked block sample lights")Object.DestroyImmediate(t.gameObject);
        var root=Group(block.transform,RootName);
        Terrain(root);
        Cabin(root,"Cabin 1",29,71,10,10,1,true);
        Cabin(root,"Cabin 2",29,94,10,10,2,true);
        Cabin(root,"Cabin 3",15,117,10,9,1,false);
        Cabin(root,"Reception",12,22,12,10,0,false);
        Cabin(root,"Washrooms",29,46,10,8,0,false);
        var left=Group(block.transform,"Left-side buildings");
        Cabin(left,"Supply cabin",-16,53,10,9,0,false);
        Cabin(left,"Boathouse",-16,91,10,12,0,false);
        Box(root,"Dock timber deck",new Vector3(-19,-.35f,79.5f),new Vector3(10,.3f,3),timber);
        Ramp(root,"Dock approach",new Vector3(-14,-.2f,79.5f),new Vector3(-10,0,79.5f),3,timber);
        foreach(float x in new[]{-23f,-19f,-15f})foreach(float z in new[]{78.15f,80.85f})Box(root,"Dock pile",new Vector3(x,-1.05f,z),new Vector3(.2f,2.3f,.2f),timber);
        // Low campfire ring and benches: static props, no new damage or interaction rules.
        for(int i=0;i<10;i++){float a=i*Mathf.PI/5;float x=19+Mathf.Cos(a)*1.2f,z=83+Mathf.Sin(a)*1.2f;Box(root,"Fire ring stone",new Vector3(x,Height(x,z)+.15f,z),new Vector3(.5f,.3f,.45f),concrete);}
        foreach(float z in new[]{80.5f,86f})Box(root,"Camp bench",new Vector3(20,Height(20,z)+.4f,z),new Vector3(4,.8f,.6f),timber);
        BuildExpansion(root);
        foreach(var p in new[]{new Vector3(12,4,66),new Vector3(13,4,94),new Vector3(-7,4,68),new Vector3(-6,4,106)})Lamp(root,p,40);
        // Stable marker objects and authored variation arrays survive the layout replacement.
        for(int v=0;v<block.variations.Length;v++)
        {
            float shift=(v-1)*.5f;
            var coal=new[]{new Vector3(-12,0,57),new Vector3(-22,-.2f,79.5f),new Vector3(-12,0,97),new Vector3(33,1,76),new Vector3(33,2,99),new Vector3(20,Height(20,108),108)};
            var enemies=new[]{new Vector3(17,0,65),new Vector3(19,0,89),new Vector3(26,0,77),new Vector3(26,0,98),new Vector3(20,0,108),new Vector3(35,0,88)};
            for(int i=0;i<coal.Length;i++)Set(block.variations[v].coal[i],coal[i]+new Vector3(shift,.6f,0));
            for(int i=0;i<enemies.Length;i++){var p=enemies[i];p.x+=shift;p.y=Height(p.x,p.z)+.3f;Set(block.variations[v].enemies[i],p);}
            for(int i=0;i<block.variations[v].gold.Length;i++)Set(block.variations[v].gold[i],i%2==0?new Vector3(-11+shift,.6f,99):new Vector3(22+shift,Height(22+shift,108)+.6f,108));
        }
        // Match the shared (8,-2,32) origin; runtime assembly translates whole 16-cell chunks.
        var baker=block.GetComponentInChildren<CrawlGraphBaker>();baker.transform.localPosition=new Vector3(24,4,80);baker.size=new Vector3(32,12,96);baker.seeds=block.variations.SelectMany(v=>v.enemies).ToArray();
        block.enemyBounds=new Bounds(new Vector3(24,4,80),new Vector3(30,12,70));
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);PrefabUtility.RecordPrefabInstancePropertyModifications(baker.transform);
        Physics.SyncTransforms();if(!CrawlGraphBake.Bake(baker,false))throw new Exception("Campground navigation bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        var lighting=block.GetComponent<RailBakedBlockLighting>();lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
        if(!track.SequenceEqual(block.track))throw new Exception("Track changed");
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();
        for(float z=1;z<160;z+=8)foreach(float x in new[]{-40f,-30f,-20f,-10f,0f,12f,20f,28f,36f,44f})foreach(float h in new[]{.6f,2f,5f})
        {var p=new Vector3(x,Height(x,z)+h,z);if(!Physics.CheckSphere(p,.2f,1<<6,QueryTriggerInteraction.Ignore))points.Add(p);}
        probes.probePositions=points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        RailCatalogLightingAuthoring.BakeOpen();Debug.Log("Campground installed with Quackville surfaces, baked crawlgraph; lighting bake/export queued.");
    }
    static Transform Group(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
    static void Set(Transform t,Vector3 p){t.localPosition=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    static void PrepareMaterials()
    {
        grass=Tile("Quackville grass",new RectInt(0,0,64,64),new Color(.7f,.76f,.58f));
        gravel=Tile("Quackville gravel",new RectInt(64,0,64,64),new Color(.78f,.72f,.61f));
        timber=Tile("Quackville timber",new RectInt(192,64,64,64),new Color(.7f,.63f,.51f));
        roof=Tile("Quackville dark roofing",new RectInt(128,64,64,64),new Color(.6f,.64f,.62f));
        concrete=Tile("Quackville concrete",new RectInt(128,128,64,64),new Color(.68f,.69f,.65f));
        foliage=Tile("Quackville foliage",new RectInt(0,0,64,64),new Color(.25f,.4f,.3f));
        metal=Tile("Quackville weathered paint",new RectInt(0,192,64,64),new Color(.66f,.7f,.53f));
        water=Tile("Quackville lake water",new RectInt(64,192,64,64),new Color(.45f,.7f,.8f,.88f));
        water.SetFloat("_Surface",1);water.SetFloat("_Blend",0);water.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);water.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);water.SetFloat("_ZWrite",0);
        water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");water.SetOverrideTag("RenderType","Transparent");water.renderQueue=3000;water.SetFloat("_Cull",0);EditorUtility.SetDirty(water);
    }
    static Material Tile(string name,RectInt rect,Color tint)
    {
        string texturePath=Folder+"/Textures/"+name+".png";
        if(!File.Exists(texturePath))
        {
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Quackville/Quackville_Town_Diffuse.jpg");
            var rt=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;
            var tex=new Texture2D(rect.width,rect.height,TextureFormat.RGB24,false);
            try{Graphics.Blit(atlas,rt);RenderTexture.active=rt;tex.ReadPixels(new Rect(rect.x,atlas.height-rect.y-rect.height,rect.width,rect.height),0,0);tex.Apply();File.WriteAllBytes(texturePath,tex.EncodeToPNG());}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
            AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));mat.SetColor("_BaseColor",tint);mat.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh SaveMesh(string name,Mesh mesh)
    {
        mesh.name=name;
        string path=Folder+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved)
        {
            saved.Clear();saved.name=name;saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;
            saved.normals=mesh.normals;saved.uv=mesh.uv;saved.uv2=mesh.uv2;saved.tangents=mesh.tangents;
            saved.subMeshCount=mesh.subMeshCount;for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);
            saved.bounds=mesh.bounds;saved.UploadMeshData(false);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static GameObject MeshObject(Transform p,string name,Mesh mesh,Material mat,bool collision=true)
    {
        var g=Group(p,name).gameObject;g.layer=6;g.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.3f;
        if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);return g;
    }
    static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat)
    {
        string key="Box-"+Hash128.Compute(size.ToString("F4"));var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+key+".asset");
        if(!mesh)
        {
            mesh=Object.Instantiate(cube);var uv=new Vector2[mesh.vertexCount];var v=mesh.vertices;var n=mesh.normals;
            for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=Mathf.Abs(n[i].y)>.5f?new Vector2(q.x,q.z)/4:Mathf.Abs(n[i].x)>.5f?new Vector2(q.z,q.y)/4:new Vector2(q.x,q.y)/4;}
            mesh.uv=uv;mesh=SaveMesh(key,mesh);
        }
        var g=MeshObject(p,name,mesh,mat,false);g.transform.localPosition=pos;g.transform.localScale=size;g.AddComponent<BoxCollider>();return g;
    }
    static void Terrain(Transform p)
    {
        var verts=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var tri=new List<int>();var bankTri=new List<int>();
        var waterVerts=new List<Vector3>();var waterTri=new List<int>();
        for(int z=0;z<160;z++)for(int x=-48;x<48;x++)
        {
            int k=verts.Count;foreach(var q in new[]{new Vector2(x,z),new Vector2(x,z+1),new Vector2(x+1,z+1),new Vector2(x+1,z)})
            {verts.Add(new Vector3(q.x,Height(q.x,q.y),q.y));uv.Add(q/4);uv2.Add(new Vector2((q.x+48)/96,q.y/160));}
            var target=x<0&&verts.Skip(k).Take(4).Average(v=>v.y)<-.03f?bankTri:tri;
            target.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            WaterTriangle(verts[k],verts[k+1],verts[k+2]);WaterTriangle(verts[k],verts[k+2],verts[k+3]);
        }
        var m=new Mesh{name="Campground sculpted ground"};m.SetVertices(verts);m.subMeshCount=2;m.SetTriangles(tri,0);m.SetTriangles(bankTri,1);m.SetUVs(0,uv);m.SetUVs(1,uv2);m.RecalculateNormals();m.RecalculateBounds();
        MeshObject(p,"Sculpted ground",SaveMesh("Sculpted-ground",m),grass).GetComponent<MeshRenderer>().sharedMaterials=new[]{grass,gravel};
        var w=new Mesh{name="Irregular lake surface"};w.SetVertices(waterVerts);w.SetTriangles(waterTri,0);w.SetUVs(0,waterVerts.Select(a=>new Vector2(a.x,a.z)/4).ToList());w.RecalculateNormals();w.RecalculateBounds();
        var lake=MeshObject(p,"Lake water (no collision)",SaveMesh("Lake-water",w),water,false);GameObjectUtility.SetStaticEditorFlags(lake,0);lake.GetComponent<MeshRenderer>().receiveGI=ReceiveGI.LightProbes;lake.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        int index=0;foreach(var path in Paths)for(int i=1;i<path.Length;i++)PathSurface(p,path[i-1],path[i],index++);
        void WaterTriangle(Vector3 a,Vector3 b,Vector3 c)
        {
            var input=new[]{a,b,c};var output=new List<Vector3>();
            for(int j=0;j<3;j++)
            {
                var from=input[j];var to=input[(j+1)%3];bool fi=from.y<-.8f,ti=to.y<-.8f;
                if(fi)output.Add(new Vector3(from.x,-.79f,from.z));
                if(fi!=ti){var q=Vector3.Lerp(from,to,(-.8f-from.y)/(to.y-from.y));q.y=-.79f;output.Add(q);}
            }
            int k=waterVerts.Count;waterVerts.AddRange(output);for(int j=1;j<output.Count-1;j++)waterTri.AddRange(new[]{k,k+j,k+j+1});
        }
    }
    static void PathSurface(Transform root,Vector3 a,Vector3 b,int index)
    {
        var delta=b-a;delta.y=0;var side=Vector3.Cross(Vector3.up,delta).normalized*1.5f;int steps=Mathf.CeilToInt(delta.magnitude*2);
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int i=0;i<=steps;i++)foreach(float s in new[]{-1f,1f}){var q=Vector3.Lerp(a,b,(float)i/steps)+side*s;q.y=Height(q.x,q.z)+.025f;v.Add(q);uv.Add(new Vector2((s+1)*.375f,i*.125f));}
        for(int i=0;i<steps;i++){int k=i*2;tris.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        var m=new Mesh{name="Gravel path"};m.SetVertices(v);m.SetTriangles(tris,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(m);
        MeshObject(root,"Gravel path "+index,SaveMesh("Path-"+index,m),gravel,false);
    }
    static void Ramp(Transform p,string name,Vector3 a,Vector3 b,float width,Material mat)
    {
        var d=b-a;var side=Vector3.Cross(Vector3.up,d).normalized*width/2;var mesh=new Mesh{name=name};
        mesh.vertices=new[]{a-side,a+side,b+side,b-side};mesh.triangles=new[]{0,2,1,0,3,2};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);
        MeshObject(p,name,SaveMesh(name,mesh),mat);
    }
    static void Cabin(Transform parent,string name,float x,float z,float w,float d,float y,bool west)
    {
        var p=Group(parent,name);Box(p,"Foundation",new Vector3(x+w/2,y-.15f,z+d/2),new Vector3(w,.3f,d),concrete);
        // Two 2.6 m clear openings keep each search room from becoming a single-exit trap.
        Wall(true,x,z,z+d,west);Wall(true,x+w,z,z+d,false);Wall(false,z,x,x+w,!west);Wall(false,z+d,x,x+w,true);
        Box(p,"Roof",new Vector3(x+w/2,y+3.3f,z+d/2),new Vector3(w+.6f,.25f,d+.6f),roof);
        Box(p,name.Contains("cabin")||name.StartsWith("Cabin")?"Bed":"Workbench",new Vector3(x+w-2,y+.45f,z+2),new Vector3(2.8f,.9f,1.5f),timber);
        Box(p,"Shelf",new Vector3(x+w-1,y+.9f,z+d-2),new Vector3(1,1.8f,2),metal);
        Lamp(p,new Vector3(x+w/2,y+2.9f,z+d/2),24);
        void Wall(bool vertical,float axis,float start,float end,bool opening)
        {
            float mid=(start+end)/2;
            if(opening){Segment(start,mid-1.3f,y,3.2f);Segment(mid+1.3f,end,y,3.2f);Segment(mid-1.3f,mid+1.3f,y+2.6f,.6f);}
            else Segment(start,end,y,3.2f);
            void Segment(float a,float b,float h,float height){Box(p,"Timber wall",vertical?new Vector3(axis,h+height/2,(a+b)/2):new Vector3((a+b)/2,h+height/2,axis),vertical?new Vector3(.2f,height,b-a):new Vector3(b-a,height,.2f),timber);}
        }
    }
    static void Tree(Transform p,Vector2 q)
    {
        float h=Height(q.x,q.y);var t=Group(p,"Pine silhouette");
        Box(t,"Trunk",new Vector3(q.x,h+2,q.y),new Vector3(.5f,4,.5f),timber);
        // Four-sided tiered evergreen silhouette; simple geometry and one shared atlas material.
        for(int tier=0;tier<3;tier++)
        {
            float radius=2.6f-tier*.55f,baseY=h+1.7f+tier*1.5f;
            var m=new Mesh();m.vertices=new[]{new Vector3(-radius,0,-radius),new Vector3(radius,0,-radius),new Vector3(radius,0,radius),new Vector3(-radius,0,radius),new Vector3(0,3.5f,0)};m.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0};m.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,new Vector2(.5f,.5f)};m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);
            string key="Pine-tier-"+tier;var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+key+".asset");if(existing)Object.DestroyImmediate(m);else existing=SaveMesh(key,m);
            var go=MeshObject(t,"Needles",existing,foliage,false);go.transform.localPosition=new Vector3(q.x,baseY,q.y);
        }
    }
    static void Lamp(Transform p,Vector3 pos,float intensity)
    {
        var g=Group(p,"Baked campground lamp");g.localPosition=pos;g.localRotation=Quaternion.Euler(90,0,0);
        var l=g.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(1,.84f,.64f);l.intensity=intensity;l.range=14;l.spotAngle=120;l.innerSpotAngle=70;l.shadows=LightShadows.Soft;
    }
    [MenuItem("Duck Rails/Authoring/Campground/Load campground-first review route")]
    public static void ReviewRoute()
    {
        var catalog=AssetDatabase.FindAssets("t:Prefab",new[]{RailWeek3Builder.Folder}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(g=>g.GetComponent<RailBlock>()).Where(b=>b&&b.id!="bridge-finale-v1").ToArray();
        int seed=0;while(RailRoute.Choose("review",catalog,seed).blocks[0]!=Id)seed++;
        RailTestTools.LoadPreset(new RailTestPreset{seed=seed,route=JsonUtility.ToJson(RailRoute.Choose("review",catalog,seed)),scenario="Campground greybox review",roles="Manual VR review"});
        Debug.Log("Campground-first review route loaded; restore normal random routes in Test Center when finished.");
    }
}
#endif


