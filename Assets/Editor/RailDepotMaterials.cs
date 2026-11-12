#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Surface-only pass: existing atlas regions, metre-scaled UVs, unchanged collision.</summary>
public static class RailDepotMaterials
{
    public const string Folder="Assets/DuckRails/DepotMaterials";
    static readonly string[] Names={"Stained plaster","Faded olive paint","Exposed brick","Worn concrete","Oxidized steel","Dark roofing","Old timber","Worn yellow","Ground asphalt"};
    // Top-left pixel rectangles: Town is 256x256; Industrial is 256x128.
    static readonly Rect[] Tiles={new Rect(0,64,64,64),new Rect(0,192,64,64),new Rect(64,0,32,32),new Rect(128,128,64,64),new Rect(32,64,64,32),new Rect(128,64,64,64),new Rect(192,64,64,64),new Rect(0,32,32,32),new Rect(128,0,64,64)};
    static readonly Color[] Tints={new Color(.78f,.76f,.68f),new Color(.65f,.67f,.48f),new Color(.65f,.57f,.48f),new Color(.65f,.65f,.59f),new Color(.48f,.52f,.48f),new Color(.4f,.43f,.43f),new Color(.58f,.53f,.43f),new Color(.65f,.58f,.32f),new Color(.48f,.49f,.47f)};
    static Material[] materials;
    static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();

    [MenuItem("Duck Rails/Authoring/Depot/Apply rough material pass")]
    public static void Apply()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets")throw new InvalidOperationException("Wrong project");
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish Play or baking first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first");
        Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();meshes.Clear();
        materials=new Material[Names.Length];
        for(int i=0;i<Names.Length;i++)
        {
            var path=Folder+"/"+Names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            string atlas=i==2||i==4||i==7?"Industrial":"Town";
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Quackville/Quackville_"+atlas+"_Diffuse.jpg"));
            m.SetColor("_BaseColor",Tints[i]);m.SetFloat("_Smoothness",.04f);m.SetFloat("_Metallic",0);EditorUtility.SetDirty(m);materials[i]=m;
        }
        var scene=EditorSceneManager.OpenScene(RailDepotGreybox.BakeScene);
        var block=Object.FindFirstObjectByType<RailBlock>();var root=block.transform.Find(RailDepotGreybox.RootName);
        if(!root)throw new InvalidOperationException("V3 depot required");
        var colliders=block.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()).ToArray();
        var track=block.track.ToArray();int count=0;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            var text=r.GetComponent<TMPro.TMP_Text>();
            if(text)
            {
                // TextMeshPro owns its mesh/material; it is not a building surface.
                if(r.sharedMaterials.Any(m=>m&&AssetDatabase.GetAssetPath(m).StartsWith(Folder)))
                {
                    text.fontSharedMaterial=text.font.material;text.ForceMeshUpdate(true);
                    var tf=r.GetComponent<MeshFilter>();tf.sharedMesh=text.mesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                continue;
            }
            if(r.name=="Fixture")continue;
            var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;
            string n=r.name.ToLowerInvariant();string kind=n=="wall"?"wall":n.StartsWith("tread ")?"tread":
                n.Contains("roof")?"roof":n.Contains("guard")||n.Contains("fence")||n.Contains("post")?"steel":
                n.Contains("slab")||n.Contains("footprint")||n.Contains("path")?"concrete":
                n.Contains("crates")||n.Contains("pallet")?"timber":"prop";
            float baseY=0;var b=r.transform;
            while(b&&b.parent!=root)b=b.parent;
            if(kind=="wall")
            {
                float y=r.bounds.min.y;
                float floor=b&&b.name=="Repair shed"?4:3.2f;
                if(y>=floor-.01f)baseY=floor;
            }
            Map(f,r,kind,baseY);count++;
        }
        // The ground is shared by the three buildings; give its visible outdoor area asphalt.
        foreach(var r in block.GetComponentsInChildren<MeshRenderer>())
            if(!r.transform.IsChildOf(root)&&r.name.ToLowerInvariant().Contains("ground")&&r.GetComponent<BoxCollider>())
            {Map(r.GetComponent<MeshFilter>(),r,"ground",0);count++;}
        if(!track.SequenceEqual(block.track)||!colliders.SequenceEqual(block.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID())))throw new InvalidOperationException("Geometry contract changed");
        PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs/validation/depot-materials");
        File.WriteAllText("docs/validation/depot-materials/apply.txt",count+" renderers mapped; "+meshes.Count+" shared meshes; original collision and track retained.\nExisting Town/Industrial atlas files unchanged.\n");
        RailCatalogLightingAuthoring.BakeOpen();Debug.Log("Depot material pass applied to "+count+" renderers; lighting bake queued.");
    }
    [MenuItem("Duck Rails/Authoring/Station/Apply Quackville rough materials")]
    public static void ApplyStation()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)
            throw new InvalidOperationException("Use Duck Rails Edit Mode with no bake running");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first");
        string meshFolder="Assets/DuckRails/StationMaterials/Meshes";
        Directory.CreateDirectory(meshFolder);AssetDatabase.Refresh();meshes.Clear();
        // Reuse the approved Quackville materials without retuning the depot or atlas import settings.
        materials=Names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+n+".mat")).ToArray();
        if(materials.Any(m=>!m))throw new InvalidOperationException("Missing Quackville rough-pass materials");
        var scene=EditorSceneManager.OpenScene(RailStationLightingAuthoring.BakeScene);
        var block=Object.FindFirstObjectByType<RailBlock>();
        var root=block.transform.Find(RailUndergroundStation.RootName);
        if(!root)throw new InvalidOperationException("Underground station greybox required");
        var colliders=block.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()).ToArray();
        var track=block.track.ToArray();int count=0;
        foreach(var r in block.GetComponentsInChildren<MeshRenderer>())
        {
            if(r.GetComponent<TMPro.TMP_Text>())continue;
            string n=r.name.ToLowerInvariant();
            if(n.Contains("fixture")||(!r.transform.IsChildOf(root)&&n!="station ground"))continue;
            var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;
            string kind=n.Contains("mountain")||n=="station ground"?"ground":
                n.Contains("safety edge")?"yellow":
                n.Contains("wall")||n.Contains("partition")||n.Contains("lintel")||n.Contains("recess")?"wall":
                n.Contains("ceiling")?"roof":n.Contains("guard")||n.Contains("shelving")||n.Contains("desk")?"steel":
                n.Contains("bench")?"timber":"concrete";
            Map(f,r,kind,n.Contains("tunnel")?0:1,meshFolder);count++;
        }
        if(!track.SequenceEqual(block.track)||!colliders.SequenceEqual(block.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID())))
            throw new InvalidOperationException("Station collision/track changed");
        PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("docs/validation/station-materials");
        File.WriteAllText("docs/validation/station-materials/apply.txt",count+" surfaces; "+meshes.Count+" station UV meshes; original collision/track retained. Existing Town/Industrial atlas files and shared materials unchanged.\n");
        RailStationLightingAuthoring.Bake();
        Debug.Log("Station Quackville material pass applied; bake started.");
    }
    internal static void BeginSharedPass(string meshFolder)
    {
        Directory.CreateDirectory(meshFolder);AssetDatabase.Refresh();meshes.Clear();
        materials=Names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+n+".mat")).ToArray();
        if(materials.Any(m=>!m))throw new InvalidOperationException("Install Quackville depot materials first");
    }
    internal static void Map(MeshFilter f,MeshRenderer r,string kind,float floor,string meshFolder=Folder+"/Meshes",float wallStep=.5f)
    {
        var size=f.transform.lossyScale;float y=f.transform.position.y-floor;
        string key=kind+"_"+size.ToString("F4")+"_"+(kind=="wall"?y.ToString("F4"):"0");
        if(kind=="wall" && wallStep!=.5f)key+="_step"+wallStep.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
        if(!meshes.TryGetValue(key,out var mesh))
        {
            // Always derive UV2 from the original UV-unwrapped cube, including repeat passes.
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/maintenance-depot-v1/Cube Block UV.asset");
            mesh=MakeMesh(source,size,y,kind,wallStep);string path=meshFolder+"/"+kind+"-"+Hash128.Compute(key)+".asset";
            mesh.name=Path.GetFileNameWithoutExtension(path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved)
            {
                // Use the Mesh API so both serialized data and the GPU vertex buffers update.
                saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;
                saved.normals=mesh.normals;saved.uv=mesh.uv;saved.uv2=mesh.uv2;saved.tangents=mesh.tangents;
                saved.subMeshCount=mesh.subMeshCount;
                for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);
                saved.bounds=mesh.bounds;saved.name=mesh.name;saved.UploadMeshData(false);EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(mesh);mesh=saved;
            }
            else AssetDatabase.CreateAsset(mesh,path);
            meshes.Add(key,mesh);
        }
        f.sharedMesh=mesh;r.sharedMaterials=materials;
        PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
    }
    static Mesh MakeMesh(Mesh source,Vector3 size,float originY,string kind,float wallStep=.5f)
    {
        var verts=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var normals=new List<Vector3>();
        var tris=Enumerable.Range(0,Names.Length).Select(_=>new List<int>()).ToArray();
        var sv=source.vertices;var sn=source.normals;var su=source.uv2;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal);
            Vector3 v=Vector3.Cross(normal,u);
            float w=Vector3.Scale(u,size).magnitude,h=Vector3.Scale(v,size).magnitude;
            float step=kind=="wall"?wallStep:2;
            var xs=Edges(w,step);var ys=Edges(h,step);
            // Add precise paint-band boundaries and the tread's front safety strip.
            if(kind=="wall"&&Mathf.Abs(v.y)>.5f){float boundary=1.3f-originY+h/2;if(boundary>0&&boundary<h)ys.Add(boundary);ys=ys.Distinct().OrderBy(a=>a).ToList();}
            if(kind=="tread"&&normal==Vector3.up){ys.Add(Mathf.Max(0,h-.07f));ys=ys.Distinct().OrderBy(a=>a).ToList();}
            Vector2 LightUv(float a,float b)
            {
                Vector2 Corner(float x,float z)
                {var p=normal*.5f+u*x+v*z;int best=-1;float dist=99;for(int k=0;k<sv.Length;k++){if(Vector3.Dot(sn[k],normal)<.99f)continue;float d=(sv[k]-p).sqrMagnitude;if(d<dist){dist=d;best=k;}}if(best<0)throw new Exception("Missing source face UV");return su[best];}
                return Vector2.Lerp(Vector2.Lerp(Corner(-.5f,-.5f),Corner(.5f,-.5f),a),Vector2.Lerp(Corner(-.5f,.5f),Corner(.5f,.5f),a),b);
            }
            for(int x=0;x<xs.Count-1;x++)for(int z=0;z<ys.Count-1;z++)
            {
                float mx=(xs[x]+xs[x+1])/2,mz=(ys[z]+ys[z+1])/2;
                int mat=kind=="yellow"?7:kind=="wall"?0:kind=="roof"?5:kind=="steel"||kind=="tread"?4:kind=="timber"?6:kind=="ground"?8:kind=="prop"?(normal.y>.5f?4:1):3;
                if(kind=="wall")
                {
                    float height=originY+(mz-h/2)*v.y;
                    if(height<1.3f)mat=1;
                    // Coherent irregular patches, expressed by surface material alone.
                    float wear=Mathf.PerlinNoise(mx*.20f+size.x,mz*.27f+size.z);
                    if(wear>.75f&&Mathf.Abs(normal.y)<.5f)mat=2;
                    else if(height<1.3f&&wear>.67f)mat=0;
                }
                if(kind=="tread"&&normal==Vector3.up&&mz>h-.07f)mat=7;
                int start=verts.Count;var tile=Tiles[mat];
                float tu=Mathf.Floor(mx/4)*4,tv=Mathf.Floor(mz/4)*4;
                foreach(var p in new[]{new Vector2(xs[x],ys[z]),new Vector2(xs[x+1],ys[z]),new Vector2(xs[x+1],ys[z+1]),new Vector2(xs[x],ys[z+1])})
                {
                    verts.Add(normal*.5f+u*(p.x/w-.5f)+v*(p.y/h-.5f));normals.Add(normal);uv2.Add(LightUv(p.x/w,p.y/h));
                    var texture=materials[mat].mainTexture;
                    uv.Add(new Vector2((tile.x+.5f+(p.x-tu)/4*(tile.width-1))/texture.width,1-(tile.y+.5f+(p.y-tv)/4*(tile.height-1))/texture.height));
                }
                tris[mat].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
        var mesh=new Mesh{name="Depot "+kind+" atlas UV"};if(verts.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv2);mesh.subMeshCount=Names.Length;
        for(int i=0;i<Names.Length;i++)mesh.SetTriangles(tris[i],i);
        mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    static List<float> Edges(float length,float step)
    {var result=new List<float>{0};for(float f=step;f<length-.0001f;f+=step)result.Add(f);result.Add(length);return result;}
}
#endif

