#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Offline concept-2 cockpit authoring, preserving the original control identities.</summary>
public static class RailCockpitArt
{
    public const string Controls = "Assets/DuckRails/Train/Prefabs/CockpitControls.prefab";
    public const string Folder = "Assets/DuckRails/Train/YardMule/Cockpit";
    const int AtlasSize = 256;
    const float Density = 32;
    static readonly Quaternion Slope = Quaternion.Euler(-25, 0, 0);
    static Texture2D atlas, emission;
    static int px, py, row;
    static Material material;
    static float Height(float z) => .23f + (z - .02f) * Mathf.Tan(25 * Mathf.Deg2Rad);
    static Vector3 Surface(float x, float z, float lift = 0) => new Vector3(x, Height(z), z) + Slope * Vector3.up * lift;

    sealed class Shape
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        public void Face(Vector3[] points, int color, Vector3 normal)
        {
            points=(Vector3[])points.Clone();
            if (Vector3.Dot(Vector3.Cross(points[1]-points[0], points[2]-points[0]),normal)<0) Array.Reverse(points);
            var n=Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;
            var u=Vector3.ProjectOnPlane(Vector3.right,n).normalized;
            if(u.sqrMagnitude<.5f)u=Vector3.ProjectOnPlane(Vector3.forward,n).normalized;
            var v=Vector3.Cross(n,u);
            float minU=points.Min(p=>Vector3.Dot(p,u)),minV=points.Min(p=>Vector3.Dot(p,v));
            float width=points.Max(p=>Vector3.Dot(p,u))-minU,height=points.Max(p=>Vector3.Dot(p,v))-minV;
            var rect=Island(width,height,color); int start=vertices.Count;
            foreach(var p in points){vertices.Add(p);uvs.Add(new Vector2(rect.x+(Vector3.Dot(p,u)-minU)*Density/AtlasSize,rect.y+(Vector3.Dot(p,v)-minV)*Density/AtlasSize));}
            for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        public void Box(Vector3 center, Vector3 size, int color, Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;var p=new Vector3[8];
            for(int i=0;i<8;i++)p[i]=center+q*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3}})
            {var face=f.Select(i=>p[i]).ToArray();Face(face,color,face.Aggregate(Vector3.zero,(a,b)=>a+b)/4-center);}
        }
        public void Slab(Vector2[] outline,float lift,float depth,int topColor,int sideColor)
        {
            var a=outline.Select(p=>Surface(p.x,p.y,lift)).ToArray();var b=a.Select(p=>p-Slope*Vector3.up*depth).ToArray();
            // The outline is convex; the small central brow is authored separately.
            Face(a,topColor,Slope*Vector3.up);Face(b,sideColor,-(Slope*Vector3.up));
            for(int i=0;i<a.Length;i++){int j=(i+1)%a.Length;Face(new[]{a[i],a[j],b[j],b[i]},sideColor,Vector3.Cross(a[j]-a[i],b[i]-a[i]));}
        }
        public Mesh Save(string name)
        {
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
    }
    static Rect Island(float width,float height,int color)
    {
        int w=Mathf.CeilToInt(width*Density)+1,h=Mathf.CeilToInt(height*Density)+1;
        if(px+w+4>AtlasSize){px=0;py+=row;row=0;}
        if(py+h+4>AtlasSize)throw new InvalidOperationException("Cockpit atlas overflow");
        var palette=new[]{new Color32(218,153,32,255),new Color32(49,53,57,255),new Color32(24,28,31,255),new Color32(255,230,164,255),new Color32(255,179,48,255)};
        for(int y=-2;y<h+2;y++)for(int x=-2;x<w+2;x++)
        {
            float patch=Mathf.Round(Mathf.PerlinNoise((Mathf.Clamp(x,0,w-1)/2)*2/11f+17,(Mathf.Clamp(y,0,h-1)/2)*2/11f+17)*7)/7;
            Color c=palette[color];if(color<3)c*=.93f+.14f*patch;
            atlas.SetPixel(px+2+x,py+2+y,c);emission.SetPixel(px+2+x,py+2+y,color>=3?c:Color.black);
        }
        var r=new Rect((px+2.5f)/AtlasSize,(py+2.5f)/AtlasSize,width*Density/AtlasSize,height*Density/AtlasSize);
        px+=w+4;row=Mathf.Max(row,h+4);return r;
    }
    static GameObject Part(Transform parent,string name,Shape shape)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=6;
        go.AddComponent<MeshFilter>().sharedMesh=shape.Save(name);go.AddComponent<MeshRenderer>().sharedMaterial=material;
        GameObjectUtility.SetStaticEditorFlags(go,0);return go;
    }
    static void SaveAtlas()
    {
        foreach(var pair in new[]{(atlas,"CockpitPixels"),(emission,"CockpitEmission")})
        {
            pair.Item1.Apply();string path=Folder+"/"+pair.Item2+".png";File.WriteAllBytes(path,pair.Item1.EncodeToPNG());AssetDatabase.ImportAsset(path);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=AtlasSize;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=AtlasSize,format=TextureImporterFormat.RGB24,textureCompression=TextureImporterCompression.Uncompressed});imp.SaveAndReimport();Object.DestroyImmediate(pair.Item1);
        }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/CockpitPixels.png"));material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/CockpitEmission.png"));material.SetColor("_EmissionColor",Color.white*1.2f);material.EnableKeyword("_EMISSION");material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        material.SetFloat("_Smoothness",0);material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(material);
    }
    [MenuItem("Duck Rails/Art/Build Integrated Cockpit")]
    public static void Build()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new InvalidOperationException("Duck Rails Edit Mode required");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();px=py=row=0;
        atlas=new Texture2D(AtlasSize,AtlasSize,TextureFormat.RGB24,false);emission=new Texture2D(AtlasSize,AtlasSize,TextureFormat.RGB24,false);
        atlas.SetPixels(Enumerable.Repeat(Color.black,AtlasSize*AtlasSize).ToArray());emission.SetPixels(Enumerable.Repeat(Color.black,AtlasSize*AtlasSize).ToArray());
        material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cockpit.mat");if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/Cockpit.mat");}
        var root=PrefabUtility.LoadPrefabContents(Controls);
        try
        {
            var t=root.transform;var old=t.Find("Integrated dash art");if(old)Object.DestroyImmediate(old.gameObject);
            foreach(string name in new[]{"Dash","Dash support -0.6","Dash support 0.6","Gauge backing"}){var legacy=t.Find(name);if(legacy)legacy.gameObject.SetActive(false);}
            var art=new GameObject("Integrated dash art").transform;art.SetParent(t,false);
            var body=new Shape();
            body.Slab(new[]{new Vector2(-1.76f,-.18f),new Vector2(1.76f,-.18f),new Vector2(1.76f,.2f),new Vector2(1.65f,.32f),new Vector2(-1.65f,.32f),new Vector2(-1.76f,.2f)},0,.08f,0,1);
            body.Slab(new[]{new Vector2(-1.69f,-.155f),new Vector2(1.69f,-.155f),new Vector2(1.69f,.18f),new Vector2(1.59f,.26f),new Vector2(-1.59f,.26f),new Vector2(-1.69f,.18f)},.008f,.012f,1,1);
            var housing=Part(art,"Integrated housing",body);var collider=housing.AddComponent<MeshCollider>();collider.sharedMesh=housing.GetComponent<MeshFilter>().sharedMesh;collider.convex=true;
            var details=new Shape();
            details.Box(Surface(0,.285f,.035f),new Vector3(1.62f,.055f,.075f),0,Slope);
            // Gauge bezel and inset screen; text and segments remain independent/live.
            details.Box(Surface(-.48f,.025f,.021f),new Vector3(.67f,.025f,.35f),1,Slope);
            details.Box(Surface(-.48f,.025f,.037f),new Vector3(.61f,.012f,.295f),2,Slope);
            foreach(float side in new[]{-1f,1f})
            {
                details.Box(Surface(side*1.04f,.015f,.016f),new Vector3(.17f,.008f,.033f),3,Slope);
                // Wall brackets leave the floor and hand approach open.
                details.Box(new Vector3(side*1.45f,.145f,.285f),new Vector3(.07f,.27f,.07f),1);
            }
            var fill=new Shape();
            for(int i=0;i<10;i++)
            {
                float x=-.732f+i*.056f;
                details.Box(Surface(x,-.065f,.047f),new Vector3(.045f,.006f,.055f),1,Slope);
                var center=Surface(x,-.065f,.052f);var right=Vector3.right*.0225f;var up=Slope*Vector3.forward*.0275f;
                fill.Face(new[]{center-right-up,center+right-up,center+right+up,center-right+up},4,Slope*Vector3.up);
            }
            // Retain the mesh asset identity after removing the decorative handles.
            Part(art,"Dash trim and handles",details).name="Dash trim";
            var segments=Part(art,"Fuel segments",fill).GetComponent<MeshFilter>();
            var fuel=root.GetComponent<RailCockpitFuelBar>()??root.AddComponent<RailCockpitFuelBar>();fuel.segments=segments;fuel.capacity=40;
            var gauge=t.Find("Fuel gauge").GetComponent<TMP_Text>();gauge.transform.localPosition=Surface(-.48f,.065f,.052f);gauge.transform.localRotation=Slope*Quaternion.Euler(90,0,0);gauge.rectTransform.sizeDelta=new Vector2(.56f,.16f);gauge.fontSize=.65f;gauge.enableAutoSizing=false;gauge.text="FUEL  10.0";gauge.ForceMeshUpdate(true,true);
            var throttle=t.Find("Throttle");throttle.localPosition=new Vector3(.05f,.295f,-.035f);throttle.localRotation=Quaternion.identity;
            var horn=t.Find("Horn");horn.localPosition=Surface(.55f,.015f,.022f);horn.localRotation=Slope;
            var label=t.Find("Throttle label").GetComponent<TMP_Text>();label.transform.localPosition=Surface(.05f,-.12f,.02f);label.transform.localRotation=Slope*Quaternion.Euler(90,0,0);label.fontSize=.3f;label.rectTransform.sizeDelta=new Vector2(.4f,.07f);
            SaveAtlas();PrefabUtility.SaveAsPrefabAsset(root,Controls);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("Installed integrated cockpit in existing nested control prefab.");
    }
}
#endif
