#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// One-shot adaptation of the CURRENT prefab, never a replay of the original builder.
public static class RailFinalePolish
{
    const string Path = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    const string Folder = "Assets/DuckRails/FinaleFort/Polish";
    static Transform env, art, additions;
    static Material concrete, steel, timber, rock, grass, water;
    static Mesh cube;
    static int meshId;
    static Transform Group(Transform p, string name) { var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform; }
    static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/FinaleFort/"+n+".mat");
    static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.name=name; mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(mesh.vertexCount>0)Unwrapping.GenerateSecondaryUVSet(mesh);
        string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static GameObject MeshGo(Transform p,string n,Mesh mesh,Material mat,bool collision=true)
    {
        var g=Group(p,n).gameObject;g.layer=6;g.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.3f;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);
        if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material mat,bool collision=true)
    {
        var g=MeshGo(p,n,cube,mat,false);g.transform.localPosition=pos;g.transform.localScale=size;
        if(collision)g.AddComponent<BoxCollider>();return g;
    }
    class MeshBuild
    {
        public List<Vector3> v=new(); public List<Vector2> uv=new();public List<int> t=new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int n=v.Count;v.AddRange(new[]{a,b,c,d});var normal=Vector3.Cross(b-a,c-a).normalized;
         foreach(var q in new[]{a,b,c,d})uv.Add(Mathf.Abs(normal.y)>.5f?new(q.x/3,q.z/3):Mathf.Abs(normal.x)>.5f?new(q.z/3,q.y/3):new(q.x/3,q.y/3));t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        public Mesh Build(string name){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);return SaveMesh(name,m);}
    }
    static void Ramp(string name,Vector3 a,Vector3 b,float width)
    {
        var d=new Vector3(b.x-a.x,0,b.z-a.z).normalized;var r=Vector3.Cross(Vector3.up,d)*width/2;
        var m=new MeshBuild();var al=a-r;var ar=a+r;var bl=b-r;var br=b+r;var dl=bl;dl.y=a.y-.2f;var dr=br;dr.y=dl.y;var cl=al-Vector3.up*.2f;var cr=ar-Vector3.up*.2f;
        m.Quad(al,bl,br,ar);m.Quad(cl,cr,dr,dl);m.Quad(cl,dl,bl,al);m.Quad(ar,br,dr,cr);m.Quad(bl,dl,dr,br);m.Quad(al,ar,cr,cl);
        MeshGo(additions,name,m.Build(name),concrete);
    }
    static void Gables()
    {
        foreach(var house in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Village cottage").ToArray())
        {
            var q=house.position;var m=new MeshBuild();float x=q.x,z=q.z;
            m.Quad(new(x+4.1f,3.8f,z-3.49f),new(x-4.1f,3.8f,z-3.49f),new(x,5.46f,z-3.49f),new(x,5.46f,z-3.49f));
            m.Quad(new(x-4.1f,3.8f,z+3.49f),new(x+4.1f,3.8f,z+3.49f),new(x,5.46f,z+3.49f),new(x,5.46f,z+3.49f));
            // Sloped closure below the existing pitched roof and enclosed sides.
            m.Quad(new(x-4.1f,3.8f,z+3.49f),new(x-4.1f,3.8f,z-3.49f),new(x,5.46f,z-3.49f),new(x,5.46f,z+3.49f));
            m.Quad(new(x,5.46f,z+3.49f),new(x,5.46f,z-3.49f),new(x+4.1f,3.8f,z-3.49f),new(x+4.1f,3.8f,z+3.49f));
            MeshGo(additions,"Closed cottage gable "+x+" "+z,m.Build("Gable-"+(meshId++)),Mat("Cottage cream"));
        }
    }
    static float Hill(float x,float z)=>Mathf.Max(Mathf.SmoothStep(0,1,Mathf.InverseLerp(27,53,Mathf.Abs(x)))*(18+5*Mathf.Sin(z*.065f)+3*Mathf.Cos(z*.14f)),Mathf.SmoothStep(0,1,Mathf.InverseLerp(320,360,z))*(27+4*Mathf.Sin(x*.13f)));
    static float StreamX(float z)=>17+4*Mathf.Sin(z*.09f);
    static float ValleyY(float x,float z)
    {
        float taper=Mathf.Clamp01((z-193)/5)*Mathf.Clamp01((263-z)/5);
        float channel=-.6f*Mathf.Clamp01((3.2f-Mathf.Abs(x-StreamX(z)))/1.5f)*taper;
        return Hill(x,z)+channel;
    }
    static void Valley()
    {
        var old=art.GetComponentsInChildren<Transform>().First(t=>t.name=="Continuous valley hills and far ridge");
        var xs=new SortedSet<float>();var zs=new SortedSet<float>();for(int x=-65;x<=65;x+=5)xs.Add(x);for(int x=7;x<=27;x++)xs.Add(x);
        for(int z=160;z<=380;z+=5)zs.Add(z);for(int z=193;z<=263;z++)zs.Add(z);
        var xx=xs.ToArray();var zz=zs.ToArray();var m=new MeshBuild();
        for(int i=0;i<xx.Length-1;i++)for(int j=0;j<zz.Length-1;j++){float a=xx[i],b=xx[i+1],c=zz[j],d=zz[j+1];m.Quad(new(a,ValleyY(a,c),c),new(a,ValleyY(a,d),d),new(b,ValleyY(b,d),d),new(b,ValleyY(b,c),c));}
        var mesh=m.Build("Recessed valley ground");old.GetComponent<MeshFilter>().sharedMesh=mesh;old.GetComponent<MeshCollider>().sharedMesh=mesh;
        // Remove the old flat slab under the stream; keep the tunnel approach solid.
        var bank=env.Find("Far bank");bank.localPosition=new(0,-.5f,135);bank.localScale=new(70,1,50);
        Box(additions,"Far meadow ground continuation",new(0,-.5f,395),new(70,1,30),grass);
        foreach(var t in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Stream ribbon").ToArray())Object.DestroyImmediate(t.gameObject);
        m=new MeshBuild();for(int z=196;z<260;z++){float wa=1.8f*Mathf.Min(1,(z-196)/3f,(260-z)/3f);float wb=1.8f*Mathf.Min(1,(z+1-196)/3f,(260-z-1)/3f);m.Quad(new(StreamX(z)-wa,-.25f,z),new(StreamX(z+1)-wb,-.25f,z+1),new(StreamX(z+1)+wb,-.25f,z+1),new(StreamX(z)+wa,-.25f,z));}
        MeshGo(additions,"Recessed stream water plane",m.Build("Stream water plane"),water,false);
    }
    static void Cliffs()
    {
        foreach(var t in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Tunnel ridge shoulder"||t.name=="Tunnel ridge crown").ToArray())Object.DestroyImmediate(t.gameObject);
        // Separate rock lobes leave the authored 11 m clear tunnel intact.
        foreach(int s in new[]{-1,1})
        {
            var m=new MeshBuild();float[] zs={119.5f,128,139,150,161,175};
            for(int i=0;i<zs.Length-1;i++)
            {
                float za=zs[i],zb=zs[i+1],innerA=i==0?6.8f:7,innerB=7;
                float ha=i==5?Hill(s*48,za):17+3*Mathf.Sin(i*1.7f+s),hb=i==4?Hill(s*48,zb):17+3*Mathf.Sin((i+1)*1.7f+s);
                float outerA=65,outerB=65;
                if(i==4){innerA=47;innerB=47;}
                Vector3 a=new(s*innerA,0,za),b=new(s*outerA,0,za),c=new(s*outerB,0,zb),d=new(s*innerB,0,zb);
                Vector3 at=new(s*(innerA+4),ha*.78f,za),bt=new(s*(outerA-5),ha,za),ct=new(s*(outerB-5),hb,zb),dt=new(s*(innerB+4),hb*.78f,zb);
                if(s>0){m.Quad(a,at,bt,b);m.Quad(d,c,ct,dt);m.Quad(at,dt,ct,bt);m.Quad(a,d,dt,at);m.Quad(b,bt,ct,c);}
                else{m.Quad(a,b,bt,at);m.Quad(d,dt,ct,c);m.Quad(at,bt,ct,dt);m.Quad(a,at,dt,d);m.Quad(b,c,ct,bt);}
            }
            MeshGo(additions,(s<0?"West":"East")+" faceted tunnel cliff",m.Build("Cliff-"+s),rock);
        }
        // Crown closes above the tunnel without a rectangular skyline.
        var crown=new MeshBuild();float[] x={-11,-5,3,11};float[] y={14,17,15.5f,14};
        for(int i=0;i<3;i++){crown.Quad(new(x[i],y[i],119.5f),new(x[i],y[i],161),new(x[i+1],y[i+1],161),new(x[i+1],y[i+1],119.5f));crown.Quad(new(x[i],7.8f,119.5f),new(x[i],y[i],119.5f),new(x[i+1],y[i+1],119.5f),new(x[i+1],7.8f,119.5f));crown.Quad(new(x[i+1],7.8f,161),new(x[i+1],y[i+1],161),new(x[i],y[i],161),new(x[i],7.8f,161));}
        MeshGo(additions,"Faceted cliff crown",crown.Build("Cliff crown"),rock);
        foreach(var t in env.Cast<Transform>().Where(t=>t.name=="Tunnel wall")){t.localPosition=new(Mathf.Sign(t.localPosition.x)*6.3f,3.5f,140);t.localScale=new(1.6f,7.04f,40.2f);}
        env.Find("Tunnel roof").localScale=new(14.2f,1,40.2f);
        foreach(float z in new[]{119.7f,160.3f}){foreach(float s in new[]{-1f,1f})Box(additions,"Concrete tunnel portal pier",new(s*6.3f,3.5f,z),new(1.6f,7,.9f),concrete);Box(additions,"Concrete tunnel lintel",new(0,7.5f,z),new(14.2f,1,.9f),concrete);}
    }
    static Material Emissive(string name,Color tint,float intensity)
    {
        var tex=new Texture2D(16,16,TextureFormat.RGB24,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,name=name};
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)tex.SetPixel(x,y,(x%4==0||y%4==0)?new Color(.45f,.45f,.45f):Color.white);tex.Apply();AssetDatabase.CreateAsset(tex,Folder+"/"+name+".asset");
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetTexture("_BaseMap",tex);m.SetTexture("_EmissionMap",tex);m.SetColor("_BaseColor",tint);m.SetColor("_EmissionColor",tint*intensity);m.EnableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    static void Spot(Transform p,string n,Vector3 pos,Vector3 target,Color color,float intensity,float range,float angle)
    {var t=Group(p,n);t.localPosition=pos;t.localRotation=Quaternion.LookRotation(target-pos);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.color=color;l.intensity=intensity;l.range=range;l.spotAngle=angle;l.innerSpotAngle=angle*.65f;l.shadows=LightShadows.Soft;}
    static void Fixtures()
    {
        var white=Emissive("Floodlight lens",new(1,.93f,.78f),3);var red=Emissive("Red warning lens",new(1,.025f,.008f),3);
        foreach(var old in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Floodlight").ToArray())
        {
            var p=old.position;Object.DestroyImmediate(old.gameObject);var head=Group(additions,"Sports field floodlight array");head.localPosition=p;head.rotation=Quaternion.LookRotation(new Vector3(0,1.5f,p.z<50?50:59)-p);
            Box(head,"Steel mounting crossbar",new(0,0,-.3f),new(3.2f,.15f,.2f),steel,false);
            for(int i=0;i<3;i++){Box(head,"Floodlight housing",new((i-1)*1.02f,0,0),new(.94f,.72f,.4f),steel,false);Box(head,"Emissive floodlight lens",new((i-1)*1.02f,0,.205f),new(.78f,.56f,.03f),white,false);}
            Spot(additions,"Baked compound floodlight",p+head.forward*.3f,new(0,1.5f,p.z<50?50:59),new(1,.93f,.78f),95,52,112);
        }
        foreach(var post in env.Cast<Transform>().Where(t=>t.name=="Warning post").ToArray())
        {var p=post.position;p.y+=post.lossyScale.y/2;Box(additions,"Emissive red warning cap",p+Vector3.up*.13f,new(.34f,.26f,.34f),red,false);Spot(additions,"Baked red warning pool",p+new Vector3(0,.06f,-.2f),p+new Vector3(0,-3,-1),new(1,.03f,.01f),8,7,105);}
    }
    static void Crate(Vector3 p,bool stacked)
    {
        var t=Group(additions,stacked?"Stacked supply crates":"Supply crate");t.localPosition=p;
        for(int k=0;k<(stacked?2:1);k++){Box(t,"Timber crate",new(0,.65f+k*1.3f,0),new(1.3f,1.3f,1.3f),timber);foreach(float x in new[]{-.52f,.52f})Box(t,"Crate steel strap",new(x,.65f+k*1.3f,0),new(.07f,1.32f,1.32f),steel,false);}
    }
    static void Access()
    {
        foreach(var t in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="Concrete stair tread"||t.name=="River recovery tread").ToArray())Object.DestroyImmediate(t.gameObject);
        foreach(int s in new[]{-1,1})
        {
            Ramp("South inward ramp "+s,new(s*8,0,40),new(s*12,2,40),3);
            Ramp("Middle ramp "+s,new(s*27,2,48),new(s*27,4,56),3);
            Ramp("Upper short ramp "+s,new(s*26,4,64),new(s*26,8,72),3);
            Ramp("River lower ramp "+s,new(s*16,-11.5f,88.5f),new(s*35.5f,-5.75f,88.5f),3);
            Ramp("River upper ramp "+s,new(s*35.5f,-5.75f,83),new(s*16,0,83),3);
            for(int i=0;i<4;i++)Box(additions,"North bastion climb block",new(s*(8.5f+i*.9f),(i+1)*.4f,67),new(1.3f,(i+1)*.8f,2),concrete);
            Box(additions,"Low approach concrete wall",new(s*47,.6f,15.5f),new(.6f,1.2f,31),concrete);
        }
        foreach(var t in art.GetComponentsInChildren<Transform>().Where(t=>t.name=="River end rock wall")){t.localPosition=new(Mathf.Sign(t.localPosition.x)*46,-3,95);t.localScale=new(2,18,30);}
        // Move former on-stair coal onto its adjacent flat roof; retain counts and identity.
        foreach(var t in env.root.Find("Coal Spawns").Cast<Transform>().Where(t=>t.name.Contains("27 63")))t.localPosition=new(Mathf.Sign(t.localPosition.x)*23,4.2f,62);
        Crate(new(-29,2,41),true);Crate(new(-29,2,42.6f),false);Crate(new(-22,2,46),false);
        Crate(new(22,2,38),false);Crate(new(23.6f,2,38),false);Crate(new(25.2f,2,38),false);Crate(new(29,2,45),true);
        foreach(var p in new[]{new Vector3(-34,4,51),new(-34,4,62),new(34,4,41),new(34,4,53),new(34,4,65)})Crate(p,false);
    }
    static List<Rect> Subtract(Rect a,Rect b)
    {
        float x0=Mathf.Max(a.xMin,b.xMin),x1=Mathf.Min(a.xMax,b.xMax),y0=Mathf.Max(a.yMin,b.yMin),y1=Mathf.Min(a.yMax,b.yMax);
        if(x1<=x0+.0001f||y1<=y0+.0001f)return new(){a};var list=new List<Rect>();
        void Add(float x,float y,float w,float h){if(w>.0001f&&h>.0001f)list.Add(new(x,y,w,h));}
        Add(a.xMin,a.yMin,x0-a.xMin,a.height);Add(x1,a.yMin,a.xMax-x1,a.height);Add(x0,a.yMin,x1-x0,y0-a.yMin);Add(x0,y1,x1-x0,a.yMax-y1);return list;
    }
    static void JoinConcrete()
    {
        // Trim only coplanar/hidden box faces. Original objects, colliders and paths survive.
        var boxes=art.GetComponentsInChildren<BoxCollider>().Where(c=>c.GetComponent<MeshRenderer>()?.sharedMaterial==concrete && Quaternion.Angle(c.transform.rotation,Quaternion.identity)<.01f).Concat(new[]{env.Find("Finale approach").GetComponent<BoxCollider>()}).ToArray();
        var bounds=boxes.Select(c=>new Bounds(c.transform.TransformPoint(c.center),Vector3.Scale(c.size,c.transform.lossyScale))).ToArray();
        for(int i=0;i<boxes.Length;i++)
        {
            var m=new MeshBuild();var a=bounds[i];
            for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
            {
                int u=(axis+1)%3,v=(axis+2)%3;float plane=sign<0?a.min[axis]:a.max[axis];var faces=new List<Rect>{new(a.min[u],a.min[v],a.size[u],a.size[v])};
                for(int j=0;j<boxes.Length;j++)if(j!=i){var b=bounds[j];bool interior=plane>b.min[axis]+.0001f&&plane<b.max[axis]-.0001f;bool behind=sign>0?Mathf.Abs(plane-b.min[axis])<.0001f:Mathf.Abs(plane-b.max[axis])<.0001f;bool same=i>j&&Mathf.Abs(plane-(sign>0?b.max[axis]:b.min[axis]))<.0001f;
                    if(interior||behind||same)faces=faces.SelectMany(f=>Subtract(f,new Rect(b.min[u],b.min[v],b.size[u],b.size[v]))).ToList();}
                foreach(var f in faces){Vector3 Point(float x,float y){var q=Vector3.zero;q[axis]=plane;q[u]=x;q[v]=y;return boxes[i].transform.InverseTransformPoint(q);}var p=Point(f.xMin,f.yMin);var q=Point(f.xMax,f.yMin);var r=Point(f.xMax,f.yMax);var s=Point(f.xMin,f.yMax);if(sign>0)m.Quad(p,q,r,s);else m.Quad(p,s,r,q);}
            }
            for(int k=0;k<m.v.Count;k++){var q=boxes[i].transform.TransformPoint(m.v[k]);int start=k/4*4;var n=Vector3.Cross(m.v[start+1]-m.v[start],m.v[start+2]-m.v[start]).normalized;m.uv[k]=Mathf.Abs(n.y)>.5f?new(q.x/3,q.z/3):Mathf.Abs(n.x)>.5f?new(q.z/3,q.y/3):new(q.x/3,q.y/3);}
            boxes[i].GetComponent<MeshFilter>().sharedMesh=m.Build("Joined concrete-"+i);
        }
    }
    public static void Apply()
    {
        RailCanyon.Guard();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            env=root.transform.Find("Env");if(!env)throw new Exception("Missing current Env root");art=env.Find(RailFinaleFort.RootName);
            if(env.Find("November 11 polish"))throw new Exception("Already applied; do not rebuild the current prefab");
            additions=Group(env,"November 11 polish");concrete=Mat("Pale fortress concrete");steel=Mat("Fort teal steel");timber=Mat("Timber");rock=Mat("Ridge rock");grass=Mat("Dawn grass");water=Mat("Stream blue");
            cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
            Gables();Valley();Cliffs();Fixtures();Access();JoinConcrete();
            // Thin ballast surface leaves all authored rails, sleepers and collision untouched.
            var ballast=new Material(Mat("Fort yard concrete"));ballast.SetColor("_BaseColor",new(.72f,.68f,.6f));AssetDatabase.CreateAsset(ballast,Folder+"/Track ballast.mat");
            Box(additions,"Victory railway gravel bed",new(0,-.025f,210),new(5,.06f,200),ballast,false);
            PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();Debug.Log("Finale polish installed; bake/export and validation pending.");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void RefineSurfaces()
    {
        RailCanyon.Guard();var root=PrefabUtility.LoadPrefabContents(Path);
        try{
            env=root.transform.Find("Env");art=env.Find(RailFinaleFort.RootName);additions=env.Find("November 11 polish");
            // Restore metre-scaled texels on the clipped structural faces and new boxes.
            foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh && (f.sharedMesh.name.StartsWith("Joined concrete-") || f.transform.IsChildOf(additions)&&f.GetComponent<BoxCollider>())))
            {
                var src=f.sharedMesh;var m=Object.Instantiate(src);var verts=m.vertices;var ns=m.normals;var uv=new Vector2[verts.Length];
                for(int i=0;i<verts.Length;i++){var q=f.transform.TransformPoint(verts[i]);var n=f.transform.TransformDirection(ns[i]);uv[i]=Mathf.Abs(n.y)>.5f?new(q.x/3,q.z/3):Mathf.Abs(n.x)>.5f?new(q.z/3,q.y/3):new(q.x/3,q.y/3);}
                m.uv=uv;f.sharedMesh=SaveMesh("Surface-"+(meshId++),m);
            }
            // Ballast gets a restrained, repeating coarse stone texture.
            var ballast=additions.Find("Victory railway gravel bed");var bm=Object.Instantiate(ballast.GetComponent<MeshFilter>().sharedMesh);var bv=bm.vertices;var bn=bm.normals;var bu=new Vector2[bv.Length];
            for(int i=0;i<bv.Length;i++){var q=ballast.TransformPoint(bv[i]);bu[i]=new(q.x/2,q.z/2);}bm.uv=bu;ballast.GetComponent<MeshFilter>().sharedMesh=SaveMesh("Ballast tiled",bm);
            var stone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cliff stone.mat");if(!stone){stone=new Material(Mat("Pale fortress concrete"));stone.SetColor("_BaseColor",new(.55f,.58f,.55f));AssetDatabase.CreateAsset(stone,Folder+"/Cliff stone.mat");}
            foreach(int sign in new[]{-1,1})
            {
                var m=new MeshBuild();float[] xs={6.8f,14,23,33,44,55,65};
                for(int i=0;i<xs.Length-1;i++)
                {
                    Vector3[] Ring(int j){float x=xs[j];float front=119.1f+1.4f*Mathf.Sin(j*2.3f+sign);float h=15+3*Mathf.Sin(j*1.7f+sign)+x*.045f;
                        return new[]{new Vector3(sign*x,0,front),new(sign*(x-.8f),h*.44f,front-1.1f),new(sign*(x-1.2f),h,front+3),new(sign*(x-1),h+1.7f,140),new(sign*x,Mathf.Max(11,Hill(sign*x,160)),160)};}
                    var a=Ring(i);var b=Ring(i+1);
                    for(int j=0;j<4;j++){if(sign>0)m.Quad(a[j],a[j+1],b[j+1],b[j]);else m.Quad(a[j],b[j],b[j+1],a[j+1]);}
                    if(sign>0)m.Quad(a[4],new(sign*xs[i],0,160),new(sign*xs[i+1],0,160),b[4]);else m.Quad(b[4],new(sign*xs[i+1],0,160),new(sign*xs[i],0,160),a[4]);
                    if(i==0){for(int j=0;j<4;j++){var baseA=a[j];baseA.y=0;var baseB=a[j+1];baseB.y=0;if(sign>0)m.Quad(baseA,baseB,a[j+1],a[j]);else m.Quad(baseA,a[j],a[j+1],baseB);}}
                    if(i==xs.Length-2){for(int j=0;j<4;j++){var baseA=b[j];baseA.y=0;var baseB=b[j+1];baseB.y=0;if(sign>0)m.Quad(baseA,b[j],b[j+1],baseB);else m.Quad(baseA,baseB,b[j+1],b[j]);}}
                }
                // Outer shoulders overlap the existing grassy hills, closing the side holes.
                float xa=sign*47,xb=sign*65;var p=new Vector3(xa,Mathf.Max(11,Hill(xa,160)),160);var q=new Vector3(xb,Mathf.Max(11,Hill(xb,160)),160);var r=new Vector3(xb,Hill(xb,175)-.15f,175);var s=new Vector3(xa,Hill(xa,175)-.15f,175);
                if(sign>0){m.Quad(p,s,r,q);m.Quad(new(xa,0,160),new(xa,0,175),s,p);m.Quad(q,r,new(xb,0,175),new(xb,0,160));}else{m.Quad(q,r,s,p);m.Quad(p,s,new(xa,0,175),new(xa,0,160));m.Quad(new(xb,0,160),new(xb,0,175),r,q);}
                var g=additions.Find((sign<0?"West":"East")+" faceted tunnel cliff");var mesh=m.Build("Cliff-"+sign);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshCollider>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=stone;
            }
            additions.Find("Faceted cliff crown").GetComponent<Renderer>().sharedMaterial=stone;
            PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void FinishSeams()
    {
        RailCanyon.Guard();var root=PrefabUtility.LoadPrefabContents(Path);
        try{env=root.transform.Find("Env");art=env.Find(RailFinaleFort.RootName);concrete=Mat("Pale fortress concrete");JoinConcrete();PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
