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

    internal static void BeginSharedPass(string meshFolder)
    {
        Directory.CreateDirectory(meshFolder);AssetDatabase.Refresh();meshes.Clear();
        materials=Names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+n+".mat")).ToArray();
        if(materials.Any(m=>!m))throw new InvalidOperationException("Install Quackville depot materials first");
    }
    internal static void Map(MeshFilter f,MeshRenderer r,string kind,float floor,string meshFolder=Folder+"/Meshes")
    {
        var size=f.transform.lossyScale;float y=f.transform.position.y-floor;
        string key=kind+"_"+size.ToString("F4")+"_"+(kind=="wall"?y.ToString("F4"):"0");
        if(!meshes.TryGetValue(key,out var mesh))
        {
            // Always derive UV2 from the original UV-unwrapped cube, including repeat passes.
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/maintenance-depot-v1/Cube Block UV.asset");
            mesh=MakeMesh(source,size,y,kind);string path=meshFolder+"/"+kind+"-"+Hash128.Compute(key)+".asset";
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
    static Mesh MakeMesh(Mesh source,Vector3 size,float originY,string kind)
    {
        var verts=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var normals=new List<Vector3>();
        var tris=Enumerable.Range(0,Names.Length).Select(_=>new List<int>()).ToArray();
        var sv=source.vertices;var sn=source.normals;var su=source.uv2;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal);
            Vector3 v=Vector3.Cross(normal,u);
            float w=Vector3.Scale(u,size).magnitude,h=Vector3.Scale(v,size).magnitude;
            float step=kind=="wall"?.5f:2;
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
