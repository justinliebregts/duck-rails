#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// Offline mesh/atlas authoring. The game loads ordinary saved assets, with no runtime generator.
public static class RailYardMuleArt
{
    public const string Folder = "Assets/DuckRails/Train/YardMule";
    public const string Train = "Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab";
    public const string Shell = Folder + "/YardMuleShell.prefab";
    const string Evidence = "docs/validation/yard-mule";
    static Material material;
    static bool textureOnly, shellOnly;
    static Material hazardMaterial;
    public const float WidthScale=.8f, TexelsPerMetre=32;
    const int AtlasSize=1024, Padding=4;
    static Texture2D pixels, glow;
    static int packX,packY,rowHeight;
    static readonly List<RectInt> freeRects=new List<RectInt>();
    static Rect AllocateFace(float width,float height,int tile)
    {
        int w=Mathf.CeilToInt(width*TexelsPerMetre)+1, h=Mathf.CeilToInt(height*TexelsPerMetre)+1;
        int outerW=w+Padding*2,outerH=h+Padding*2;
        int best=-1,score=int.MaxValue;
        for(int i=0;i<freeRects.Count;i++)
        {
            var f=freeRects[i];if(f.width<outerW||f.height<outerH)continue;
            int s=Mathf.Min(f.width-outerW,f.height-outerH);if(s<score){score=s;best=i;}
        }
        if(best<0)throw new InvalidOperationException("Yard Mule atlas overflow: "+outerW+"x"+outerH);
        var slot=freeRects[best];freeRects.RemoveAt(best);
        int dw=slot.width-outerW,dh=slot.height-outerH;
        if(dw>dh)
        {
            if(dw>0)freeRects.Add(new RectInt(slot.x+outerW,slot.y,dw,slot.height));
            if(dh>0)freeRects.Add(new RectInt(slot.x,slot.y+outerH,outerW,dh));
        }
        else
        {
            if(dw>0)freeRects.Add(new RectInt(slot.x+outerW,slot.y,dw,outerH));
            if(dh>0)freeRects.Add(new RectInt(slot.x,slot.y+outerH,slot.width,dh));
        }
        int left=slot.x+Padding,bottom=slot.y+Padding;
        for(int y=-Padding;y<h+Padding;y++)for(int x=-Padding;x<w+Padding;x++)
        {
            var color=Pixel(tile,Mathf.Clamp(x,0,w-1),Mathf.Clamp(y,0,h-1),w,h);
            pixels.SetPixel(left+x,bottom+y,color);glow.SetPixel(left+x,bottom+y,tile==5||tile==6?color:Color.black);
        }
        packY=Mathf.Max(packY,slot.y+outerH);
        return new Rect((left+.5f)/AtlasSize,(bottom+.5f)/AtlasSize,width*TexelsPerMetre/AtlasSize,height*TexelsPerMetre/AtlasSize);
    }
    static Color Pixel(int tile,int x,int y,int w,int h)
    {
        Color32[] colors={new Color32(218,153,32,255),new Color32(49,53,57,255),new Color32(116,76,43,255),new Color32(226,165,38,255),new Color32(99,99,91,255),new Color32(255,224,151,255),new Color32(255,234,179,255),new Color32(28,31,34,255)};
        Color c=colors[tile];
        if(tile==3 && ((x+y)/8)%2==0)c=colors[1];
        if(tile==0)
        {
            // Broad joined patches with close tonal steps; no isolated high-contrast chips.
            float patch=SoftPatch(x,y,17);
            c*=.91f+.18f*patch;
        }
        if(tile==2)
        {
            int plank=x/10, along=y+(plank%3)*19;
            c*=.94f+((plank*13+along/64*7)%9)*.012f;
            c*=.94f+.12f*SoftPatch(x,y,41);
            if(x%10==0||along%64==0)c*=.84f;
            else if(x%10==1)c*=1.025f;
        }
        if(tile==1||tile==4||tile==7)
        {
            uint hash=unchecked((uint)((x/2)*73856093^(y/2)*19349663^tile*83492791));
            if(hash%37<3)c*=.82f;
            if((x<2||y<2||x>=w-2||y>=h-2)&&hash%5==0)c*=1.2f;
        }
        if(tile==6&&(x<2||y<2||x>=w-2||y>=h-2))c=colors[5];
        return c;
    }
    static float SoftPatch(int x,int y,int seed)
    {
        // Square two-texel blocks, grouped into larger irregular islands with gentle steps.
        float px=(x/2)*2,py=(y/2)*2;
        float n=Mathf.PerlinNoise(px/11f+seed,py/11f+seed)*.75f+
            Mathf.PerlinNoise(px/5f+seed*2,py/5f+seed*3)*.25f;
        return Mathf.Round(n*7)/7;
    }
    sealed class Geometry
    {
        public readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> indices = new List<int>();
        public void Face(Vector3[] points, int tile, Vector3 outward, Vector2? repeatingUvOffset=null)
        {
            // Bake width into geometry BEFORE constructing an orthonormal UV basis.
            points=points.Select(p=>new Vector3(p.x*WidthScale,p.y,p.z)).ToArray();
            if (Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),outward)<0) Array.Reverse(points);
            int start=v.Count;
            var normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;
            Vector3 u=Mathf.Abs(normal.y)>.99f?Vector3.right:Vector3.Cross(Vector3.up,normal).normalized;
            Vector3 w=Vector3.Cross(normal,u).normalized;
            float minU=points.Min(p=>Vector3.Dot(p,u)), maxU=points.Max(p=>Vector3.Dot(p,u));
            float minW=points.Min(p=>Vector3.Dot(p,w)), maxW=points.Max(p=>Vector3.Dot(p,w));
            var island=tile==3?new Rect():AllocateFace(maxU-minU,maxW-minW,tile);
            foreach(var p in points)
            {
                v.Add(p);
                uv.Add(tile==3?new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,w))+(repeatingUvOffset??Vector2.zero):
                    new Vector2(island.x+(Vector3.Dot(p,u)-minU)*TexelsPerMetre/AtlasSize,
                    island.y+(Vector3.Dot(p,w)-minW)*TexelsPerMetre/AtlasSize));
            }
            for(int j=1;j<points.Length-1;j++){indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);}
        }
        public void Box(Vector3 p, Vector3 size, int tile, Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;
            var c=new Vector3[8];
            for(int i=0;i<8;i++) c[i]=p+q*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            int[][] faces={new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3}};
            foreach(var f in faces)
            {
                var points=f.Select(i=>c[i]).ToArray();
                var outward=points.Aggregate(Vector3.zero,(a,b)=>a+b)/4-p;
                // Unfold the hazard top from the front edge: front V=y, top V=topY+frontZ-z.
                // Both faces then share UVs along their seam, retaining square texels.
                Vector2? repeatOffset=tile==3&&outward.normalized.y>.99f
                    ?new Vector2(0,points[0].y+points.Max(v=>v.z)):(Vector2?)null;
                Face(points,tile,outward,repeatOffset);
            }
        }
        // Extrude a convex XY profile along Z; flat face normals preserve deliberate facets.
        public void Profile(Vector2[] profile,float front,float back,int tile)
        {
            var a=profile.Select(p=>new Vector3(p.x,p.y,front)).ToArray();
            var b=profile.Select(p=>new Vector3(p.x,p.y,back)).ToArray();
            Face(a,tile,Vector3.forward);Face(b,tile,Vector3.back);
            var center=profile.Aggregate(Vector2.zero,(x,y)=>x+y)/profile.Length;
            for(int i=0;i<a.Length;i++){int j=(i+1)%a.Length;var n=(profile[i]+profile[j])*.5f-center;Face(new[]{a[i],b[i],b[j],a[j]},tile,new Vector3(n.x,n.y,0));}
        }
        public void Cylinder(Vector3 p,float radius,float depth,int tile,int sides=16)
        {
            var a=new Vector3[sides];var b=new Vector3[sides];
            for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2/sides;var d=new Vector3(0,Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);a[i]=p+d+Vector3.right*depth*.5f;b[i]=p+d-Vector3.right*depth*.5f;}
            Face(a,tile,Vector3.right);Face(b,tile,Vector3.left);
            for(int i=0;i<sides;i++){int j=(i+1)%sides;Face(new[]{a[i],a[j],b[j],b[i]},tile,(a[i]+b[j])*.5f-p);}
        }
        public Mesh Save(string name)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old)
            {
                old.Clear();old.SetVertices(v);old.SetUVs(0,uv);old.SetTriangles(indices,0);old.RecalculateNormals();old.RecalculateBounds();
                EditorUtility.SetDirty(old);Object.DestroyImmediate(mesh);return old;
            }
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
    static void Part(GameObject root,string name,Geometry geometry)
    {
        if(textureOnly)return;
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=geometry.Save(name);
        go.AddComponent<MeshRenderer>().sharedMaterial=name=="Hazard bumper"?hazardMaterial:material;
        // Non-static moving renderers do not contribute to baked GI. Match touchable outer surfaces.
        GameObjectUtility.SetStaticEditorFlags(go,0);
        if(name=="Faceted canopy" || name.StartsWith("Nose ") || name=="Large roof spotlight")
        {
            var c=go.AddComponent<MeshCollider>();c.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;c.convex=true;
        }
        if(name=="Hazard bumper")
        {
            var bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;
            var c=go.AddComponent<BoxCollider>();c.center=bounds.center;c.size=bounds.size;
        }
    }
    static void Atlas()
    {
        foreach(var item in new[]{new{t=pixels,n="Pixels"},new{t=glow,n="Emission"}})
        {
            item.t.Apply();string path=Folder+"/"+item.n+".png";File.WriteAllBytes(path,item.t.EncodeToPNG());Object.DestroyImmediate(item.t);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=AtlasSize;importer.SaveAndReimport();
        }
        material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/YardMule.mat");
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/YardMule.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Pixels.png"));material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Emission.png"));material.SetColor("_EmissionColor",Color.white*1.6f);material.EnableKeyword("_EMISSION");
        // URP's material validator strips _EMISSION with flags=None. BakedEmissive keeps the
        // shader feature, while non-static renderers above exclude this moving shell from GI.
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        material.SetFloat("_Smoothness",0);material.SetFloat("_Metallic",0);material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(material);
    }

    static void HazardTexture()
    {
        string path=Folder+"/HazardStripes.png";
        var t=new Texture2D(32,32,TextureFormat.RGB24,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)t.SetPixel(x,y,((x+y)/8)%2==0?new Color32(35,39,43,255):new Color32(226,165,38,255));
        t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=32;
        foreach(var platform in new[]{"Android","Standalone"})importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name=platform,overridden=true,maxTextureSize=32,format=TextureImporterFormat.RGB24,textureCompression=TextureImporterCompression.Uncompressed});
        importer.SaveAndReimport();
        hazardMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/HazardStripes.mat");
        if(!hazardMaterial){hazardMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(hazardMaterial,Folder+"/HazardStripes.mat");}
        hazardMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));hazardMaterial.SetColor("_BaseColor",Color.white);hazardMaterial.SetFloat("_Smoothness",0);hazardMaterial.SetFloat("_Metallic",0);hazardMaterial.SetFloat("_SpecularHighlights",0);hazardMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(hazardMaterial);
    }

    [MenuItem("Duck Rails/Art/Build Yard Mule")]
    public static void Build()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new InvalidOperationException("Duck Rails Edit Mode required");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();
        pixels=new Texture2D(AtlasSize,AtlasSize,TextureFormat.RGB24,false);glow=new Texture2D(AtlasSize,AtlasSize,TextureFormat.RGB24,false);
        pixels.SetPixels32(Enumerable.Repeat(new Color32(32,32,32,255),AtlasSize*AtlasSize).ToArray());
        glow.SetPixels32(Enumerable.Repeat(new Color32(0,0,0,255),AtlasSize*AtlasSize).ToArray());
        packX=packY=rowHeight=0;
        freeRects.Clear();freeRects.Add(new RectInt(0,0,AtlasSize,AtlasSize));
        material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/YardMule.mat");
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/YardMule.mat");}
        HazardTexture();
        var root=new GameObject("Yard Mule shell");
        try
        {
            var g=new Geometry();
            g.Box(new Vector3(0,-.47f,0),new Vector3(4.5f,.35f,9.8f),1);
            g.Box(new Vector3(0,-.165f,0),new Vector3(5,.27f,10),1);
            foreach(float s in new[]{-1f,1f})
            {
                // End caps sit inside the deck's +/-5m end planes, avoiding coplanar overlap.
                g.Box(new Vector3(s*2.48f,-.18f,0),new Vector3(.12f,.22f,9.94f),0);
                for(int i=0;i<10;i++)g.Box(new Vector3(s*2.55f,-.19f,-4.5f+i),new Vector3(.022f,.065f,.065f),4);
            }
            Part(root,"Deck and chassis",g);
            g=new Geometry();
            g.Face(new[]{new Vector3(-2.5f,0,-5),new Vector3(-2.5f,0,5),new Vector3(2.5f,0,5),new Vector3(2.5f,0,-5)},2,Vector3.up);
            Part(root,"Single wood deck",g);
            g=new Geometry();
            // Existing route rails are centered at +/-1.5m. Narrow shell, unchanged track gauge.
            foreach(float x in new[]{-1.875f,1.875f})foreach(float z in new[]{-3.3f,3.3f})
            {
                g.Cylinder(new Vector3(x,-.68f,z),.43f,.32f,7);
                g.Cylinder(new Vector3(x+Mathf.Sign(x)*.18f,-.68f,z),.29f,.045f,1);
                g.Cylinder(new Vector3(x+Mathf.Sign(x)*.215f,-.68f,z),.13f,.06f,4,12);
                g.Box(new Vector3(x,-.43f,z),new Vector3(.4f,.14f,.8f),1);
            }
            Part(root,"Flanged wheels",g);
            g=new Geometry();var marks=new Geometry();
            foreach(float x in new[]{-2.42f,2.42f})
            {
                float sign=Mathf.Sign(x);
                foreach(var span in new[]{new Vector2(-3,4),new Vector2(2,2),new Vector2(4,2)})
                {
                    float h=span.x==4?.2f:.32f;
                    g.Box(new Vector3(x,h/2,span.x),new Vector3(.14f,h,span.y),1);
                    g.Box(new Vector3(x,h+.03f,span.x),new Vector3(.16f,.06f,span.y),0);
                    int count=span.y==4?3:1;
                    for(int i=0;i<count;i++)marks.Box(new Vector3(x+sign*.076f,h*.52f,span.x+(i-(count-1)/2f)*1.2f),new Vector3(.012f,.055f,.35f),5);
                }
                foreach(float z in new[]{-4.9f,-1.05f,1.05f,2.95f})
                {
                    g.Box(new Vector3(x,.2f,z),new Vector3(.19f,.4f,.16f),0);
                    marks.Box(new Vector3(x+sign*.101f,.21f,z),new Vector3(.012f,.23f,.055f),5);
                    if(z< -4)marks.Box(new Vector3(x,.21f,z-.086f),new Vector3(.065f,.23f,.012f),5);
                }
            }
            Part(root,"Boarding rails",g);
            g=new Geometry();
            foreach(float x in new[]{-2.42f,2.42f})foreach(float z in new[]{3.05f,4.9f})
            {
                g.Box(new Vector3(x,.8f,z),new Vector3(.18f,1.6f,.18f),1);
                g.Box(new Vector3(x,.09f,z),new Vector3(.31f,.18f,.31f),1);
                g.Box(new Vector3(x,1.43f,z),new Vector3(.29f,.26f,.27f),1);
                g.Cylinder(new Vector3(x+Mathf.Sign(x)*.16f,1.43f,z),.055f,.035f,4,8);
            }
            Part(root,"Canopy supports",g);
            g=new Geometry();
            g.Profile(new[]{new Vector2(-2.58f,1.55f),new Vector2(-2.58f,1.68f),new Vector2(-2.28f,1.9f),new Vector2(2.28f,1.9f),new Vector2(2.58f,1.68f),new Vector2(2.58f,1.55f)},5.12f,2.88f,0);
            g.Box(new Vector3(0,1.54f,5.04f),new Vector3(5.12f,.13f,.14f),1);
            g.Box(new Vector3(0,1.54f,2.96f),new Vector3(5.12f,.13f,.14f),1);
            for(int i=-1;i<=1;i++)g.Box(new Vector3(i*.85f,1.913f,4),new Vector3(.055f,.025f,2.1f),1);
            foreach(float x in new[]{-2.57f,2.57f})
            {
                g.Box(new Vector3(x,1.59f,4),new Vector3(.04f,.12f,2.15f),1);
                foreach(float z in new[]{3.25f,4,4.75f})marks.Box(new Vector3(x+Mathf.Sign(x)*.025f,1.6f,z),new Vector3(.012f,.05f,.38f),5);
            }
            foreach(float z in new[]{2.878f,5.128f})foreach(float x in new[]{-1.75f,-.95f,.95f,1.75f})marks.Box(new Vector3(x,1.65f,z),new Vector3(.44f,.06f,.012f),5);
            Part(root,"Faceted canopy",g);
            g=new Geometry();
            // Separate convex cheeks leave a real opening; one combined convex hull would seal it.
            foreach(float sign in new[]{-1f,1f})
            {
                g=new Geometry();
                float x0=sign*1.35f,x1=sign*2.55f;
                var front=new[]{new Vector3(x0,-.29f,5.85f),new Vector3(x1,-.29f,5.85f),new Vector3(x1,.43f,5.52f),new Vector3(x0,.43f,5.52f)};
                var back=front.Select(p=>new Vector3(p.x,p.y,4.88f)).ToArray();
                g.Face(front,0,Vector3.forward);g.Face(back,0,Vector3.back);
                g.Face(new[]{front[0],front[1],back[1],back[0]},0,Vector3.down);
                g.Face(new[]{front[3],front[2],back[2],back[3]},0,Vector3.up);
                foreach(int j in new[]{0,1})g.Face(new[]{front[j],back[j],back[3-j],front[3-j]},0,Vector3.right*(j==0?-sign:sign));
                for(int i=0;i<3;i++)
                {
                    float y=-.09f+i*.14f,z=5.85f-(y+.29f)*(.33f/.72f)+.02f;
                    g.Box(new Vector3(sign*1.95f,y,z),new Vector3(.8f,.065f,.04f),1,Quaternion.Euler(-24.6f,0,0));
                }
                g.Box(new Vector3(sign*2.5f,-.4f,5.82f),new Vector3(.22f,.28f,.3f),1);
                Part(root,sign<0?"Nose left":"Nose right",g);
                marks.Box(new Vector3(sign*2.5f,-.39f,5.977f),new Vector3(.06f,.16f,.012f),5);
            }
            var bumper=new Geometry();bumper.Box(new Vector3(0,-.41f,5.82f),new Vector3(5.18f,.24f,.26f),3);Part(root,"Hazard bumper",bumper);
            Part(root,"Emissive safety markers",marks);
            g=new Geometry();
            var angle=Quaternion.Euler(12,0,0);var lamp=new Vector3(0,1.99f,5.08f);
            g.Box(new Vector3(0,1.79f,5.1f),new Vector3(1.05f,.12f,.4f),1);
            g.Box(lamp,new Vector3(.94f,.58f,.34f),1,angle);
            g.Box(lamp+angle*new Vector3(0,0,.177f),new Vector3(.74f,.38f,.025f),6,angle);
            foreach(float x in new[]{-.5f,.5f})g.Cylinder(lamp+new Vector3(x,0,0),.11f,.08f,4,12);
            Part(root,"Large roof spotlight",g);
            var light=new GameObject("Single forward spotlight");light.transform.SetParent(root.transform,false);light.transform.localPosition=lamp+angle*new Vector3(0,0,.23f);light.transform.localRotation=angle;
            var l=light.AddComponent<Light>();l.type=LightType.Spot;l.color=new Color(1,.87f,.65f);l.intensity=2.2f;l.range=32;l.spotAngle=65;l.innerSpotAngle=40;l.shadows=LightShadows.None;
            Atlas();
            if(!textureOnly)PrefabUtility.SaveAsPrefabAsset(root,Shell);
        }
        finally{Object.DestroyImmediate(root);}
        if(textureOnly||shellOnly){AssetDatabase.SaveAssets();Debug.Log("Updated Yard Mule art; assembly/module placements untouched.");return;}
        var train=PrefabUtility.LoadPrefabContents(Train);
        try
        {
            foreach(string name in new[]{"Hull and deck","Wheels","Side rails - rear stays open","Cockpit shell"})foreach(var r in train.transform.Find(name).GetComponentsInChildren<MeshRenderer>(true))r.enabled=false;
            train.transform.Find("Lighting").gameObject.SetActive(false);
            train.GetComponent<RailTrainAssembly>().chair.gameObject.SetActive(false);
            var old=train.transform.Find("Yard Mule shell");if(old)Object.DestroyImmediate(old.gameObject);
            var shell=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Shell),train.transform);
            shell.name="Yard Mule shell";
            // Resize collision authoring groups, never the gameplay assembly or module scales.
            foreach(string name in new[]{"Hull and deck","Side rails - rear stays open","Cockpit shell"})
                train.transform.Find(name).localScale=new Vector3(WidthScale,1,1);
            var assembly=train.GetComponent<RailTrainAssembly>();
            var cabin=(BoxCollider)new SerializedObject(train.GetComponent<RailTrain>().cabin).FindProperty("passengerCabin").objectReferenceValue;
            cabin.size=new Vector3(4,cabin.size.y,cabin.size.z);
            // Full-size modules fit inside the narrowed rails; their interaction anchors move together.
            var p=assembly.furnace.transform.localPosition;
            p=assembly.furnace.transform.Find("Front firebox art")?new Vector3(0,0,5.08f):new Vector3(-1.05f,p.y,p.z);
            assembly.furnace.transform.localPosition=p;
            p=assembly.radio.localPosition;p.x=1.55f;assembly.radio.localPosition=p;
            foreach(var point in assembly.supplyPoints){p=point.localPosition;p.x=Mathf.Clamp(p.x,-1.55f,1.55f);point.localPosition=p;}
            PrefabUtility.SaveAsPrefabAsset(train,Train);
        }
        finally{PrefabUtility.UnloadPrefabContents(train);}
        AssetDatabase.SaveAssets();Debug.Log("Yard Mule assets built and installed in DuckRailsTrain. Gameplay modules retained; chair hidden.");
    }

    [MenuItem("Duck Rails/Art/Refresh Yard Mule Textures Only")]
    public static void Retexture()
    {
        textureOnly=true;
        try{Build();}finally{textureOnly=false;}
    }
    public static void RebuildShell()
    {
        shellOnly=true;
        try{Build();}finally{shellOnly=false;}
    }

    public static void Validate()
    {
        var root=PrefabUtility.LoadPrefabContents(Train);
        try
        {
            var shell=root.transform.Find("Yard Mule shell");int edges=0,triangles=0;float maxError=0;
            foreach(var f in shell.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=f.sharedMesh;var verts=mesh.vertices;var uv=mesh.uv;var indices=mesh.triangles;
                float textureSize=f.name=="Hazard bumper"?32:AtlasSize;
                triangles+=indices.Length/3;
                for(int i=0;i<indices.Length;i+=3)for(int j=0;j<3;j++)
                {
                    int a=indices[i+j],b=indices[i+(j+1)%3];
                    float distance=Vector3.Distance(f.transform.TransformPoint(verts[a]),f.transform.TransformPoint(verts[b]));
                    if(distance<.000001f)throw new InvalidOperationException("Degenerate mesh edge: "+f.name);
                    float error=Mathf.Abs(Vector2.Distance(uv[a],uv[b])*textureSize/distance/TexelsPerMetre-1);
                    maxError=Mathf.Max(maxError,error);edges++;
                    if(error>.003f)throw new InvalidOperationException("UV stretch on "+f.name+": "+error);
                }
            }
            var deck=shell.Find("Single wood deck").GetComponent<MeshFilter>().sharedMesh;
            if(deck.vertexCount!=4||deck.triangles.Length!=6||deck.bounds.size!=new Vector3(4,0,10))throw new InvalidOperationException("Deck must be a single 4x10m quad");
            var slab=root.transform.Find("Hull and deck/Deck collision slab").GetComponent<BoxCollider>();
            if(Mathf.Abs(slab.transform.lossyScale.x*slab.size.x-4)>.001f)throw new InvalidOperationException("Deck collider width mismatch");
            if(root.transform.localScale!=Vector3.one||shell.localScale!=Vector3.one)throw new InvalidOperationException("Runtime scale must remain one");
            var assembly=root.GetComponent<RailTrainAssembly>();
            foreach(var t in new[]{assembly.furnace.transform,assembly.radio,assembly.controls})if(t.localScale!=Vector3.one)throw new InvalidOperationException("Gameplay module was scaled");
            foreach(var r in shell.GetComponentsInChildren<MeshRenderer>())if((GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0)throw new InvalidOperationException("Moving shell must not bake emission");
            string evidence="PASS "+edges+" triangle edges: uniform "+TexelsPerMetre+" texels/metre; worst relative error "+maxError.ToString("G6")+"\nPASS deck is one quad (4 vertices, 2 triangles), 4 x 10 metres\nPASS physical deck width 4 metres\nPASS assembly/shell/gameplay module scales remain one\nPASS shell does not contribute baked GI\nShell triangles: "+triangles+"\nRenderers: "+shell.GetComponentsInChildren<MeshRenderer>().Length+"\nAtlas: "+AtlasSize+" square, padding "+Padding+" pixels; used height "+(packY+rowHeight);
            File.WriteAllText(Evidence+"/narrow-uv-checks.txt",evidence);Debug.Log(evidence);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    public static void Preview()
    {
        Directory.CreateDirectory(Evidence);
        var preview=new PreviewRenderUtility();
        try
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Shell));preview.AddSingleGO(model);
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()) { renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false; }
            preview.ambientColor=new Color(.42f,.44f,.49f);preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-35,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(25,140,0);
            var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.08f,.105f);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.orthographic=true;camera.orthographicSize=4.9f;
            var positions=new[]{new Vector3(12,9,15),new Vector3(-12,8,-15),new Vector3(0,.5f,3.65f)};
            var names=new[]{"front","rear","driver"};
            for(int i=0;i<3;i++)
            {
                camera.orthographic=i<2;camera.fieldOfView=85;camera.transform.position=positions[i];camera.transform.LookAt(i==2?new Vector3(0,.5f,8):new Vector3(0,.4f,0));
                // Warm the SRP preview camera before saving (its first frame can be empty).
                for(int frame=0;frame<2;frame++)
                {
                    preview.BeginStaticPreview(new Rect(0,0,1600,1000));preview.Render(true);var image=preview.EndStaticPreview();
                    if(frame==1)File.WriteAllBytes(Evidence+"/"+names[i]+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
        }
        finally{preview.Cleanup();}
    }
}
#endif
