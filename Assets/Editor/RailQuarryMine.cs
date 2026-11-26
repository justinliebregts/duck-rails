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

/// <summary>Explicit authoring of the mountain quarry. No runtime generation.</summary>
public static partial class RailQuarryMine
{
    public const string Id="quarry-v1", RootName="Mountain quarry", Folder="Assets/DuckRails/QuarryMine";
    public const string PrefabPath="Assets/DuckRails/Blocks/"+Id+".prefab", BakeScene="Assets/DuckRails/Lighting/Blocks/"+Id+"/"+Id+"-bake.unity";
    public const int LightCount=37;
    static Material rock,fines,gravel,wood,steel,yellow,glass,railPaint;
    static Mesh cube;
    static readonly Dictionary<string,Mesh> boxes=new();
    // The crosscut follows the entry drift, then turns through the south, middle and north workings.
    public static readonly Vector2[] Crosscut = {
        new(-25,54),new(-19,56),new(-12,62),new(-7,70),new(-6,84),new(-7,104),new(-10,113),new(-13,121)
    };
    public static readonly Vector3[] Haul = {
        new(10,0,13),new(17,0,19),new(25,.2f,24),new(35.4f,.5f,31),new(40.1f,.9f,40),
        new(41.7f,1.35f,52),new(42,1.5f,65),new(42,1.5f,80),new(40,1.3f,93),
        new(36,.85f,105),new(30,.4f,114),new(22,0,120),new(14,0,124)
    };
    // The lower service road forks from the entrance and reaches the original mine-floor junction.
    public static readonly Vector3[] PitRoad = {
        new(10,0,13),new(9.9f,0,18),new(9.8f,-.48f,24),new(11,-1.60f,37),new(13,-2.39f,46),
        new(17,-2.83f,49),new(21,-3.27f,46),new(22.75f,-3.98f,38),new(27.1f,-4.40f,36),
        new(29.5f,-4.84f,40.45f),new(29.2f,-5.53f,49),new(30,-6,55),new(27,-6,57)
    };
    static float Toe(float z) => 10.7f+2.4f*Mathf.Sin(z*.065f)+1.1f*Mathf.Sin(z*.18f);
    static Vector3 Ledge(float inward,float y,float z) => new(-Toe(z)-inward,y,z);
    public static readonly Vector3[] Climb = {
        Ledge(1,0,59),Ledge(7,3.4f,66),Ledge(8,3.4f,74),Ledge(8.8f,3.4f,83),
        Ledge(14,6.9f,90),Ledge(16,6.9f,87),Ledge(16,6.9f,78),Ledge(17,6.9f,73),
        Ledge(22,10.6f,66),Ledge(24,10.6f,70),Ledge(24,10.6f,81),Ledge(25,10.6f,88),
        Ledge(30,14.4f,96),Ledge(31,14.4f,100)
    };

    public static readonly Vector2[][] Routes={
        new[]{new Vector2(-25,92),new Vector2(-15,108),new Vector2(0,101),new Vector2(15,88),new Vector2(25,72)},
        new[]{new Vector2(-25,92),new Vector2(-12,92),new Vector2(12,72),new Vector2(25,72)},
        new[]{new Vector2(-25,92),new Vector2(-15,76),new Vector2(0,63),new Vector2(15,56),new Vector2(25,72)}, Crosscut};
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    public static float Segment(Vector2 p,Vector2 a,Vector2 b,out float t){t=Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude);return Vector2.Distance(p,Vector2.Lerp(a,b,t));}
    static float RoundedRoom(float x,float z,float cx,float cz,float halfX,float halfZ){
        float dx=Mathf.Abs(x-cx)-halfX+.6f,dz=Mathf.Abs(z-cz)-halfZ+.6f;
        return Mathf.Min(Mathf.Max(dx,dz),0)+new Vector2(Mathf.Max(dx,0),Mathf.Max(dz,0)).magnitude-.6f;
    }

    // Negative inside the union. A continuous contour lets the shell follow diagonals,
    // including where two galleries join or emerge through an excavated bank.
    static float MineDistance(float x,float z){
        var p=new Vector2(x,z);
        float d=RoundedRoom(x,z,-25,79,3.4f,31);
        foreach(var path in Routes)
            for(int i=1;i<path.Length;i++)d=Mathf.Min(d,Segment(p,path[i-1],path[i],out _)-2.95f);
        d+=.22f*Mathf.Sin(.43f*z+.17f*x)+.12f*Mathf.Cos(.62f*x-.23f*z);
        d=Mathf.Min(d,Mathf.Min(RoundedRoom(x,z,-42,106,4.4f,4.4f),Segment(p,new(-42,106),new(-25,99),out _)-2.55f));
        return Mathf.Min(d,Mathf.Min(RoundedRoom(x,z,-13,128,6,10),
            Mathf.Min(RoundedRoom(x,z,13.5f,80.25f,8.4f,5.75f),RoundedRoom(x,z,11,75,2.4f,4))));
    }
    public static bool Tunnel(float x,float z) => MineDistance(x,z)<0;
    public static float MineFloor(float x,float z){
        float wave=.10f*Mathf.Sin(.44f*x+.23f*z)+.08f*Mathf.Sin(.39f*z-.12f*x);
        return -6+wave*Mathf.Clamp01((Height(x,z)+6)*.75f);
    }
    public static float MineRoof(float x,float z){
        float chamber=Mathf.SmoothStep(0,1,-RoundedRoom(x,z,-13,128,6.6f,10.8f)/1.3f);
        float center=-2.76f+.16f*Mathf.Sin(x*.32f+z*.21f)+.11f*Mathf.Cos(z*.47f);
        float arch=.75f*Mathf.Pow(Mathf.Clamp01(1+MineDistance(x,z)/1.7f),2);
        return Mathf.Lerp(center-arch,-.85f,chamber);
    }
    static float Terrace(float radius, float edge, float width) =>
        Mathf.Clamp01((edge - radius) / width);

    static float GradeRoad(float x, float z, Vector3[] route, float ground, float width)
    {
        float nearest = float.PositiveInfinity, road = ground;
        var point = new Vector2(x, z);
        for (int i = 1; i < route.Length; i++)
        {
            var a = route[i - 1]; var b = route[i];
            float distance = Segment(point, new(a.x,a.z), new(b.x,b.z), out float t);
            if (distance >= nearest) continue;
            nearest = distance;
            road = Mathf.Lerp(a.y, b.y, t);
        }
        return Mathf.Lerp(road, ground, Mathf.SmoothStep(0, 1, (nearest-width)/2));
    }

    public static float Height(float x,float z)
    {
        if(Mathf.Abs(x)<=6.8f || z<=6 || z>=154)return 0;
        if(x<0)
        {
            float taper=Mathf.SmoothStep(0,1,(z-7)/39)*Mathf.SmoothStep(0,1,(154-z)/31);
            // Shift the northern cliff back behind the sample yard while keeping the western boundary high.
            float recess=15*Mathf.SmoothStep(0,1,(z-105)/22);
            float u=-x-Toe(z)-recess;
            float fractured=.65f*Mathf.Sin(z*.47f)+.32f*Mathf.Sin(z*1.16f);
            float Bench(float start,float rise) => rise*Mathf.Clamp01((u-start+fractured)/1.5f);
            float ridge=taper*(Bench(3.3f,3.4f)+Bench(10.6f,3.5f)+Bench(18.4f,3.7f)+Bench(26.2f,3.8f));
            float h=GradeRoad(x,z,Climb,ridge,1.75f);
            if(z>=19 && z<=54){
                float cut=Mathf.Clamp01((5.4f-Mathf.Abs(x+25))/2);
                h=Mathf.Lerp(h,-6*Mathf.Clamp01((z-19)/30),cut*Mathf.Clamp01((54-z)/2));
            }
            var summit=Climb[Climb.Length-1];
            float deckEdge=Mathf.Max(Mathf.Abs(x-summit.x)-2.5f,Mathf.Abs(z-summit.z)-2.5f);
            float cabin=Mathf.Max((Mathf.Abs(x+14.5f)-6)/2,(Mathf.Abs(z-23)-4.5f)/2);
            float natural=PumpGrade(x,z,Mathf.Lerp(summit.y,h,Mathf.Clamp01(deckEdge/1.8f))*Mathf.Clamp01(cabin));
            float rim=Mathf.Max(Mathf.Abs(x+41.5f)-5.5f,Mathf.Abs(z-106)-6.1f);
            return Mathf.Lerp(14.4f,natural,Mathf.SmoothStep(0,1,rim/2.3f));
        }
        float ex=(x-25.8f+Mathf.Sin(z*.105f))/17,ez=(z-76)/52;
        float r=Mathf.Pow(Mathf.Pow(Mathf.Abs(ex),2.7f)+Mathf.Pow(Mathf.Abs(ez),2.7f),1/2.7f)
            +.028f*Mathf.Sin(.29f*z+.12f*x);
        float pit=-2*Terrace(r,1,.072f)-2*Terrace(r,.80f,.07f)-2*Terrace(r,.62f,.07f);
        // Two flooded cuts bend around a projecting stone bank.
        float poolX=x-23+.95f*Mathf.Sin((z-87)*.46f);
        float south=new Vector2((poolX+1.35f)/4.25f,(z-84.8f)/5.75f).magnitude;
        float north=new Vector2((poolX-1.15f)/3.45f,(z-91.5f)/5.6f).magnitude;
        float pool=Mathf.Min(south,north)+.065f*Mathf.Sin(z*.91f+x*.54f);
        float rockTongue=new Vector2((x-18.75f)/4.6f,(z-87.25f)/2.1f).magnitude;
        pool=Mathf.Max(pool,1.82f-rockTongue);
        float driftEdge=Segment(new(x,z),new(15,88),new(25,72),out _);
        pit-=.75f*Terrace(pool,1,.32f)*Mathf.SmoothStep(0,1,(driftEdge-.80f)/1.55f);
        float shelf=Mathf.Clamp01((x-ShelfLip(z)+1.25f)/1.5f)*Mathf.Clamp01((34-Mathf.Abs(z-75))/5);
        pit=Mathf.Lerp(pit,1.5f,shelf);
        float landing=Mathf.Max(Mathf.Abs(x-26)-2.2f,Mathf.Abs(z-104)-2.4f);
        pit=Mathf.Lerp(-6,pit,Mathf.Clamp01(landing/2));
        float upper=GradeRoad(x,z,Haul,pit,4.6f);
        return GradeRoad(x,z,PitRoad,upper,2.1f)*Mathf.Clamp01((47-x)/1.5f);
    }
    static float GroundMeshHeight(float x,float z){
        int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);
        float u=x-ix,v=z-iz,a=Height(ix,iz),c=Height(ix+1,iz+1);
        return v>=u?(1-v)*a+(v-u)*Height(ix,iz+1)+u*c:(1-u)*a+(u-v)*Height(ix+1,iz)+v*c;
    }
    static float RoadDistance(Vector3[] path,float x,float z){
        float near=float.PositiveInfinity;
        for(int i=1;i<path.Length;i++)near=Mathf.Min(near,Segment(new(x,z),new(path[i-1].x,path[i-1].z),new(path[i].x,path[i].z),out _));
        return near;
    }
    static float ShelfLip(float z) => 27.85f+.011f*(z-75)*(z-75)+.43f*Mathf.Sin(z*.22f);
    static bool Chamber(float x,float z) => x>=-19 && x<=-7 && z>=118 && z<=138;

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
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float w,Material mat){var go=Box(p,n,(a+b)/2,new Vector3(w,(b-a).magnitude,w),mat);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    sealed class Surface
    {
        readonly List<Vector3> v=new();readonly List<Vector2> uv=new();readonly List<int> tr=new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int k=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add((Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3);tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
        public void Tri(Vector3 a,Vector3 b,Vector3 c){
            var normal=Vector3.Cross(b-a,c-a);
            if(normal.sqrMagnitude<1e-12f)return;
            normal.Normalize();
            int first=v.Count;
            foreach(var point in new[]{a,b,c}){
                v.Add(point);
                uv.Add((Mathf.Abs(normal.y)>.5f?new Vector2(point.x,point.z):Mathf.Abs(normal.x)>.5f?new Vector2(point.z,point.y):new Vector2(point.x,point.y))/3);
            }
            tr.AddRange(new[]{first,first+1,first+2});
        }
        public void BuildGallery(Transform p,Material mat){
            var floors=new Surface();var ceilings=new Surface();var walls=new Surface();
            for(int i=0;i<tr.Count;i+=3){
                var a=v[tr[i]];var b=v[tr[i+1]];var c=v[tr[i+2]];
                float y=Vector3.Cross(b-a,c-a).normalized.y;
                var target=y>.00001f?floors:y<-.00001f?ceilings:walls;
                target.Tri(a,b,c);
            }
            floors.Build(p,"Tunnel shell floor",mat,terrainUV:true);
            ceilings.Build(p,"Tunnel shell ceiling",mat,terrainUV:true);
            walls.Build(p,"Tunnel shell walls",mat,terrainUV:true);
        }
        public void Build(Transform p,string n,Material mat,bool collide=true,bool terrainUV=false){
            if(v.Count==0)return;
            var m=new Mesh{indexFormat=IndexFormat.UInt32};
            m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();
            if(terrainUV) SetTerrainLightmapUV(m);
            else Unwrapping.GenerateSecondaryUVSet(m);
            var item=MeshGo(p,n,Save(n,m),mat,collide);
            if(terrainUV){
                var r=item.GetComponent<MeshRenderer>();
                r.scaleInLightmap=.9f;
                r.stitchLightmapSeams=true;
            }
        }
    }
    // Clip the same surface triangles for the overburden, soffit, floor and walls.
    // At daylight the top and soffit terminate on exactly the same intersection.
    struct MineVertex
    {
        public Vector2 p; public float h,f,r,d;
        public MineVertex(float x,float z){
            p=new(x,z);h=Height(x,z);d=MineDistance(x,z);f=MineFloor(x,z);r=MineRoof(x,z);
        }
        public static MineVertex Mix(MineVertex a,MineVertex b,float t) => new MineVertex{
            p=Vector2.LerpUnclamped(a.p,b.p,t),h=Mathf.LerpUnclamped(a.h,b.h,t),
            f=Mathf.LerpUnclamped(a.f,b.f,t),r=Mathf.LerpUnclamped(a.r,b.r,t),d=Mathf.LerpUnclamped(a.d,b.d,t)};
        public Vector3 At(float y)=>new(p.x,y,p.y);
    }
    static List<MineVertex> Clip(List<MineVertex> input,Func<MineVertex,float> field){
        var result=new List<MineVertex>(6);if(input.Count==0)return result;
        var a=input[input.Count-1];float da=field(a);
        foreach(var b in input){
            float db=field(b);
            if((da<0)!=(db<0))result.Add(MineVertex.Mix(a,b,da/(da-db)));
            if(db>=0)result.Add(b);
            a=b;da=db;
        }
        return result;
    }
    static void Skin(Surface surf,List<MineVertex> polygon,Func<MineVertex,float> height,bool reverse=false){
        for(int i=2;i<polygon.Count;i++){
            var a=polygon[0];var b=polygon[i-1];var c=polygon[i];
            var av=a.At(height(a));var bv=b.At(height(b));var cv=c.At(height(c));
            if(reverse)surf.Tri(av,cv,bv);else surf.Tri(av,bv,cv);
        }
    }
    static void MineTriangle(MineVertex a,MineVertex b,MineVertex c,Surface outside,Surface shell){
        if(a.d>0 && b.d>0 && c.d>0){outside.Tri(a.At(a.h),b.At(b.h),c.At(c.h));return;}
        var tri=new List<MineVertex>{a,b,c};
        Skin(outside,Clip(tri,v=>v.d),v=>v.h);
        var inside=Clip(tri,v=>-v.d);
        Skin(shell,Clip(inside,v=>v.h-v.f),v=>v.f);
        if(inside.Any(v=>v.f>v.h))Skin(outside,Clip(inside,v=>v.f-v.h),v=>v.h);
        var roof=Clip(inside,v=>v.h-v.r);
        Skin(shell,roof,v=>v.r,true);Skin(outside,roof,v=>v.h);
        // A triangle touching the exact office contour only along its edge has no interior.
        // Emitting its perimeter creates coincident, oppositely wound wall faces.
        if (!inside.Any(v => v.d < 0)) return;
        for(int i=0;i<inside.Count;i++){
            var u=inside[i];var v=inside[(i+1)%inside.Count];
            if(Mathf.Abs(u.d)>.00003f || Mathf.Abs(v.d)>.00003f)continue;
            // Split the wall at both clipping levels so neither a floor nor a lintel leaves a triangular slit.
            var cuts=new List<float>{0,1};
            void Split(float from,float to){if((from<0)!=(to<0))cuts.Add(from/(from-to));}
            Split(u.h-u.f,v.h-v.f);Split(u.h-u.r,v.h-v.r);cuts.Sort();
            for(int j=1;j<cuts.Count;j++){
                var l=MineVertex.Mix(u,v,cuts[j-1]);var r=MineVertex.Mix(u,v,cuts[j]);
                if(l.h<l.f+.00001f && r.h<r.f+.00001f)continue;
                var bl=l.At(l.f);var br=r.At(r.f);var tl=l.At(Mathf.Max(l.f,Mathf.Min(l.h,l.r)));var tr=r.At(Mathf.Max(r.f,Mathf.Min(r.h,r.r)));
                shell.Tri(bl,tl,tr);shell.Tri(bl,tr,br);
                CollectOfficeWall(bl,br,tl,tr);
            }
        }
    }
    public static int MergedTriangles {get;private set;}
    public static float MaximumMergeError {get;private set;}
    static bool ShaftCell(int x,int z)=>x>=-45 && x< -39 && z>=103 && z<109;
    static bool HoistSite(float x,float z,float margin=0)=>Mathf.Abs(x+41.5f)<5.7f+margin && Mathf.Abs(z-106)<6.3f+margin;
    static void Terrain(Transform root){
        var p=Group(root,"Excavated terrain");
        officeReturns.Clear();
        MergedTriangles=0;MaximumMergeError=0;
        var vertices=new MineVertex[97,161];
        for(int z=0;z<=160;z++)for(int x=-48;x<=48;x++)vertices[x+48,z]=new MineVertex(x,z);
        var underground=new Surface();
        var rockFaces=new Surface();
        // Keep terrain simplification local, but never cut a lightmap chart at a simplifier tile edge.
        var groundBands=Enumerable.Range(0,3).Select(_=>new Surface()).ToArray();
        var gravelBands=Enumerable.Range(0,3).Select(_=>new Surface()).ToArray();
        for(int zz=0;zz<160;zz+=16)for(int xx=-48;xx<48;xx+=16){
            int band=zz<16?0:zz<144?1:2;
            var top=rockFaces;var surrounding=groundBands[band];var ballast=gravelBands[band];
            MineVertex V(int x,int z)=>vertices[x+48,z];
            var targets=new Surface[16,16];var regular=new bool[16,16];var consumed=new bool[16,16];
            for(int z=zz;z<zz+16;z++)for(int x=xx;x<xx+16;x++){
                var a=V(x,z);var b=V(x,z+1);var c=V(x+1,z+1);var d=V(x+1,z);
                float low=Mathf.Min(a.h,b.h,c.h,d.h),high=Mathf.Max(a.h,b.h,c.h,d.h);
                var target=high-low>.56f || (x< -8 && low>.3f)?top:surrounding;
                if(Mathf.Abs(x+.5f)<5 || (x>6 && (RoadDistance(Haul,x+.5f,z+.5f)<3 || RoadDistance(PitRoad,x+.5f,z+.5f)<2.1f)))target=ballast;
                targets[x-xx,z-zz]=target;
                regular[x-xx,z-zz]=Mathf.Min(a.d,b.d,c.d,d.d)>0;
                if(x==-48 && Mathf.Max(a.h,b.h)>.01f)top.Quad(a.At(-.1f),b.At(-.1f),b.At(b.h),a.At(a.h));
                if(ShaftCell(x,z)){
                    underground.Quad(a.At(a.f),b.At(b.f),c.At(c.f),d.At(d.f));
                    void Wall(MineVertex u,MineVertex v)=>underground.Quad(u.At(u.r),u.At(u.h),v.At(v.h),v.At(v.r));
                    if(x==-45)Wall(a,b);if(z==108)Wall(b,c);if(x==-40)Wall(c,d);if(z==103)Wall(d,a);
                }else if(!regular[x-xx,z-zz]){
                    MineTriangle(a,b,c,target,underground);MineTriangle(a,c,d,target,underground);
                }
            }
            // Merge co-material rectangular patches to one plane. Edge samples stay on that plane
            // so an adjacent original triangle can meet a long edge without a visible crack.
            for(int z=zz;z<zz+16;z++)for(int x=xx;x<xx+16;x++){
                if(!regular[x-xx,z-zz] || consumed[x-xx,z-zz])continue;
                var target=targets[x-xx,z-zz];float origin=V(x,z).h,dx=V(x+1,z).h-origin,dz=V(x,z+1).h-origin;
                float Error(int u,int v)=>Mathf.Abs(V(u,v).h-(origin+dx*(u-x)+dz*(v-z)));
                bool Fits(int w,int h,out float maximum){
                    maximum=0;
                    for(int j=0;j<h;j++)for(int i=0;i<w;i++)
                        if(!regular[x+i-xx,z+j-zz] || consumed[x+i-xx,z+j-zz] || targets[x+i-xx,z+j-zz]!=target)return false;
                    for(int j=0;j<=h;j++)for(int i=0;i<=w;i++){
                        float e=Error(x+i,z+j);maximum=Mathf.Max(maximum,e);
                        if(e>((i==0 || i==w || j==0 || j==h)?.00005f:.075f))return false;
                    }
                    return true;
                }
                int width=1,depth=1,area=1;float maximum=0;
                for(int h=1;z+h<=zz+16;h++)for(int w=1;x+w<=xx+16;w++){
                    if(w*h<=area || !Fits(w,h,out float error))continue;
                    width=w;depth=h;area=w*h;maximum=error;
                }
                var a=V(x,z);var b=V(x,z+depth);var c=V(x+width,z+depth);var d=V(x+width,z);
                target.Quad(a.At(a.h),b.At(b.h),c.At(c.h),d.At(d.h));
                for(int j=0;j<depth;j++)for(int i=0;i<width;i++)consumed[x+i-xx,z+j-zz]=true;
                if(area>1){MergedTriangles+=2*(area-1);MaximumMergeError=Mathf.Max(MaximumMergeError,maximum+.00005f);}
            }
        }
        rockFaces.Build(p,"Ground tile quarry stone",rock,terrainUV:true);
        underground.BuildGallery(p,rock);
        for(int i=0;i<groundBands.Length;i++){
            groundBands[i].Build(p,"Scrub tile band "+i,fines,terrainUV:true);
            gravelBands[i].Build(p,"Ballast tile band "+i,gravel,terrainUV:true);
        }
        BuildOfficeWallReturns(p);
    }
    static void Lamp(Transform p,string n,Vector3 pos,bool warm=false,float power=45)
    {
        if(n!="Gate lantern"){
            var lens=AssetDatabase.LoadAssetAtPath<Material>(Folder+(warm?"/Amber service glass.mat":"/Pale service glass.mat"));
            var diffuser=Solid(p,"Lamp diffuser",pos-Vector3.up*.075f,new(.42f,.045f,.23f),lens,false);
            GameObjectUtility.SetStaticEditorFlags(diffuser,0);
            diffuser.GetComponent<MeshRenderer>().receiveGI=ReceiveGI.LightProbes;
        }
        Box(p,n+" housing",pos,new Vector3(.6f,.12f,.35f),steel,false);var l=Group(p,n).gameObject.AddComponent<Light>();l.transform.localPosition=pos-Vector3.up*.12f;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.range=warm?12:17;l.intensity=power;l.color=warm?new Color(1,.75f,.4f):new Color(.67f,.8f,1);l.shadows=LightShadows.Soft;
    }
    static void Interior(Transform root)
    {
        var p=Group(root,"Underground office");
        OfficeBoard(p);
        Box(p,"Desk top",new(12.7f,-5.15f,81.5f),new(2.4f,.15f,1),wood);
        foreach(float x in new[]{11.8f,13.6f})Box(p,"Desk leg",new(x,-5.6f,81.5f),new(.15f,.8f,.8f),steel);
        Box(p,"Locker",new(7.8f,-4.9f,82),new(1,2.2f,1),steel);
        var tunnels=Group(root,"Tunnel timber supports");
        foreach(var route in Routes)for(int i=1;i<route.Length;i++)
        {
            var a=route[i-1];var b=route[i];var delta=(b-a).normalized;var side=new Vector2(-delta.y,delta.x);
            for(float distance=5;distance<Vector2.Distance(a,b)-2;distance+=8)
            {
                var q=a+delta*distance;if(Height(q.x,q.y)<-2.5f)continue;
                bool junction=Routes.Any(other=>other!=route && Enumerable.Range(1,other.Length-1).Any(k=>Segment(q,other[k-1],other[k],out _)<5.6f));
                if(junction || Chamber(q.x,q.y))continue;
                Vector3 Seat(float sign){
                    float distance=2.1f;
                    while(distance<4.9f && MineDistance(q.x+side.x*sign*distance,q.y+side.y*sign*distance)<0)distance+=.12f;
                    var point=q+side*sign*(distance+.16f);
                    return new(point.x,MineRoof(q.x,q.y)-.20f,point.y);
                }
                var left=Seat(1);var right=Seat(-1);
                foreach(var end in new[]{left,right}){
                    float bottom=MineFloor(end.x,end.z)-.25f;
                    Beam(tunnels,"Timber upright",new(end.x,bottom,end.z),end,.26f,wood);
                }
                Beam(tunnels,"Roof crossbeam",left,right,.32f,wood);
                float cap=MineRoof(q.x,q.y)+.20f;
                Solid(tunnels,"Roof packing",new(q.x,(cap+left.y)/2,q.y),new(.68f,cap-left.y,.30f),wood);
            }
        }
        if(!root.Find("Tunnel boring machine"))BoringMachine(root);
        CaveLanterns(root,tunnels);
    }
    static GameObject Solid(Transform p,string name,Vector3 pos,Vector3 size,Material mat,bool collide=true)
    {
        var g=MeshGo(p,name,cube,mat,false);
        g.transform.localPosition=pos; g.transform.localScale=size;
        if(collide) g.AddComponent<BoxCollider>();
        return g;
    }

    static void Rail(Transform parent,Vector3 a,Vector3 b,bool grounded=false)
    {
        int spans=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3));
        Vector3 previous=default;
        for(int i=0;i<=spans;i++){
            var foot=Vector3.Lerp(a,b,i/(float)spans);
            if(grounded)foot.y=GroundMeshHeight(foot.x,foot.z);
            if(!grounded || !parent.Cast<Transform>().Any(t=>t.name=="Safety post" && (t.localPosition-foot-Vector3.up*.48f).sqrMagnitude<.0001f))
                Solid(parent,"Safety post",foot+Vector3.up*.48f,new(.13f,1,.13f),railPaint);
            if(i>0)foreach(float height in new[]{.46f,.92f}){
                var bar=Solid(parent,"Safety rail",(foot+previous)*.5f+Vector3.up*height,new(.085f,.085f,Vector3.Distance(previous,foot)),railPaint);
                bar.transform.localRotation=Quaternion.LookRotation(foot-previous);
            }
            previous=foot;
        }
    }

    static void Switchbacks(Transform root)
    {
        var climb=Group(root,"West rock ledges");
        // Flights occupy only the four short cuts between broad stone shelves.
        foreach(int i in new[]{1,4,8,12}){
            var a=Climb[i-1];var b=Climb[i];var flat=new Vector3(b.x-a.x,0,b.z-a.z);var rot=Quaternion.LookRotation(flat);
            int steps=Mathf.CeilToInt((b.y-a.y)/.24f);float run=flat.magnitude/steps;
            for(int j=0;j<steps;j++){
                var q=Vector3.Lerp(a,b,(j+.5f)/steps);q.y=Mathf.Lerp(a.y,b.y,(j+1f)/steps)-.12f;
                var step=Solid(climb,"Quarried stair tread",q,new(2.85f,.26f,run+.045f),rock);step.transform.localRotation=rot;
            }
            var side=rot*Vector3.right*1.56f;if(side.x<0)side=-side;Rail(climb,a+side,b+side);
        }
    }

    static void HeadframeYard(Transform root)
    {
        var yard=Group(root,"Weighbridge cabin and scale");
        yard.localPosition=new(-16,0,23);
        Solid(yard,"Footing",new(0,.12f,0),new(6.8f,.24f,5),gravel);
        Solid(yard,"Cabin back",new(0,1.5f,2.25f),new(6.3f,2.8f,.18f),wood);
        foreach(float side in new[]{-3f,3f})
            Solid(yard,"Cabin side",new(side,1.5f,0),new(.2f,2.8f,4.5f),wood);
        Solid(yard,"Door jamb",new(-2.7f,1.5f,-2.2f),new(.55f,2.8f,.2f),wood);
        Solid(yard,"Window sill wall",new(1, .67f,-2.2f),new(4,.98f,.2f),wood);
        Solid(yard,"Window header",new(1,2.6f,-2.2f),new(4,.5f,.2f),wood);
        foreach(float x in new[]{-.93f,1,2.93f}) Solid(yard,"Window mullion",new(x,1.78f,-2.2f),new(.12f,1.25f,.22f),steel);
        foreach(float side in new[]{-1f,1f})
            Solid(yard,"Pitched corrugated roof",new(0,3.2f,side*1.29f),new(7,.19f,2.85f),steel).transform.localRotation=Quaternion.Euler(side*13,0,0);
        Solid(yard,"Scale platform",new(5.3f,.09f,0),new(3,.18f,7.8f),steel);
        foreach(float z in new[]{-3.6f,3.6f})
            Solid(yard,"Scale end marking",new(5.3f,.19f,z),new(3,.025f,.22f),yellow,false);
        Entrance(root);
        PumpStation(root);
    }

    const float TurnRadius=20, ToeZ=117, HalfGauge=1.5f;
    static Vector3 TurnPoint(float distance,float offset){
        float angle=Mathf.Min(distance/TurnRadius,Mathf.PI*.5f);
        float extra=Mathf.Max(0,distance-TurnRadius*Mathf.PI*.5f);
        return new(TurnRadius-(TurnRadius-offset)*Mathf.Cos(angle)+extra,0,ToeZ+(TurnRadius-offset)*Mathf.Sin(angle));
    }
    static void HaulTurnout(Transform root){
        var yard=Group(root,"Quarry turnout and ore carts");
        var ribbon=new Surface();
        // Terminate each branch face against the existing stock rail at the same head height.
        // This also forms the frog without drawing coplanar steel through the crossing.
        void OutsideStock(Vector3 a,Vector3 b,Vector3 c,Vector3 d){
            var face=new List<Vector3>{a,b,c,d};
            List<Vector3> Trim(List<Vector3> poly,float x,float sign){
                var clipped=new List<Vector3>();if(poly.Count==0)return clipped;
                var u=poly.Last();float du=(u.x-x)*sign;
                foreach(var v in poly){float dv=(v.x-x)*sign;
                    if((dv<0)!=(du<0))clipped.Add(Vector3.LerpUnclamped(u,v,du/(du-dv)));
                    if(dv>=0)clipped.Add(v);u=v;du=dv;
                }return clipped;
            }
            foreach(var poly in new[]{Trim(face,-1.56f,-1),Trim(Trim(face,-1.44f,1),1.44f,-1),Trim(face,1.56f,1)})
                for(int i=2;i<poly.Count;i++)ribbon.Tri(poly[0],poly[i-1],poly[i]);
        }
        for(int i=0;i<108;i++)foreach(float rail in new[]{-HalfGauge,HalfGauge}){
            float s=i*.5f,t=s+.5f;var l=TurnPoint(s,rail-.06f);var r=TurnPoint(s,rail+.06f);
            var endL=TurnPoint(t,rail-.06f);var endR=TurnPoint(t,rail+.06f);var up=Vector3.up*.12f;
            OutsideStock(l+up,endL+up,endR+up,r+up);OutsideStock(l,endL,endL+up,l+up);OutsideStock(r,r+up,endR+up,endR);
        }
        ribbon.Build(yard,"Stock rail junction and frog",steel,false);
        for(int z=119;z<=131;z+=2){
            float sin=(z-ToeZ)/(TurnRadius-HalfGauge);
            float end=TurnRadius-(TurnRadius-HalfGauge)*Mathf.Sqrt(1-sin*sin)+.43f;
            // Each odd-metre main-line sleeper already spans the stock rails.
            float start=1.901f;
            Solid(yard,"Switch timber",new((start+end)/2,.02f,z),new(end-start,.04f,.35f),wood,false);
        }
        for(float d=19;d<54;d+=1.8f){
            var tangent=TurnPoint(d+.1f,0)-TurnPoint(d,0);
            var tie=Solid(yard,"Siding sleeper",TurnPoint(d,0)+Vector3.up*.02f,new(3.8f,.04f,.35f),wood,false);
            tie.transform.localRotation=Quaternion.LookRotation(tangent);
        }
        foreach(float x in new[]{-1.29f,1.29f})Solid(yard,"Frog check rail",new(x,.075f,127.2f),new(.085f,.09f,3.4f),steel,false);
        var ore=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Angular rubble block.asset");
        if(!ore)throw new InvalidOperationException("Missing quarry rubble mesh");
        var plank=Mat("Ore wagon boards","timber",new(.75f,.49f,.27f));
        var iron=Finish("Cart iron",new(.16f,.19f,.21f));var rim=Finish("Wheel tread",new(.43f,.47f,.49f));
        var rand=new System.Random(563);
        var cartWheel=RingMesh("Eight sided wagon wheel",new[]{1f,1f},new[]{-.5f,.5f},8,0);
        foreach(float x in new[]{22f,27.4f,32.8f}){
            var cart=Group(yard,"Ore skip");cart.localPosition=new(x,0,137);cart.localRotation=Quaternion.Euler(0,90,0);
            foreach(float z in new[]{-1.12f,1.12f}){
                Solid(cart,"Axle",new(0,.57f,z),new(3.24f,.19f,.19f),iron,false);
                foreach(float side in new[]{-1f,1f}){
                    var wheel=MeshGo(cart,"Iron wheel",cartWheel,rim,false);
                    wheel.transform.localPosition=new(side*1.5f,.57f,z);wheel.transform.localScale=new(.45f,.28f,.45f);
                    wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                    var hub=Solid(cart,"Axle cap",new(side*1.666f,.57f,z),new(.085f,.25f,.25f),iron,false);
                    hub.transform.localRotation=Quaternion.Euler(45,0,0);
                }
            }
            Solid(cart,"Steel underframe",new(0,.83f,0),new(2.55f,.24f,3.65f),iron);
            Solid(cart,"Wagon bed",new(0,1.05f,0),new(2.7f,.21f,3.52f),plank);
            for(int tier=0;tier<3;tier++){
                float half=1.32f+.10f*tier,y=1.35f+.37f*tier;
                foreach(float side in new[]{-1f,1f}){
                    Solid(cart,"Side board",new(side*half,y,0),new(.14f,.35f,3.6f),plank).transform.localRotation=Quaternion.Euler(0,0,-side*16);
                    Solid(cart,"End board",new(0,y,side*1.72f),new(2*half-.14f,.35f,.13f),plank);
                }
            }
            foreach(float side in new[]{-1f,1f})foreach(float z in new[]{-1.48f,1.48f}){
                Solid(cart,"Wagon strap",new(side*1.53f,1.71f,z),new(.065f,1.16f,.14f),iron).transform.localRotation=Quaternion.Euler(0,0,-side*16);
            }
            for(int i=0;i<6;i++){
                float xx=(i%2-.5f)*1.05f,zz=(i/2-1)*.87f;
                var chunk=MeshGo(cart,"Ore chunk",ore,i%3==0?rock:fines,false);chunk.transform.localPosition=new(xx,1.49f+(float)rand.NextDouble()*.18f,zz);
                chunk.transform.localScale=new(.48f,.83f,.48f);chunk.transform.localRotation=Quaternion.Euler(0,rand.Next(360),0);
            }
        }
    }
    static Mesh roundSection;
    static GameObject Tube(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material material,bool collide=false){
        if(!roundSection)roundSection=RingMesh("Industrial round section",new[]{1f,1f},new[]{-.5f,.5f},16,0);
        var obj=MeshGo(parent,name,roundSection,material,collide);
        obj.transform.localPosition=(a+b)*.5f;obj.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        obj.transform.localScale=new(radius,Vector3.Distance(a,b),radius);return obj;
    }
    static Mesh RingMesh(string name,float[] radii,float[] levels,int sides,float irregularity)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Vector3 Point(int j,int i){
            float angle=2*Mathf.PI*i/sides;
            float scale=radii[j]*(1+irregularity*Mathf.Sin(3*angle+.7f*j));
            return new(Mathf.Cos(angle)*scale,levels[j],Mathf.Sin(angle)*scale);
        }
        void Tri(Vector3 a,Vector3 b,Vector3 c){
            int start=vertices.Count; vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.right});
            triangles.AddRange(new[]{start,start+1,start+2});
        }
        for(int i=0;i<sides;i++){
            int k=(i+1)%sides;
            for(int j=0;j<levels.Length-1;j++){
                var a=Point(j,i);var b=Point(j+1,i);var c=Point(j+1,k);var d=Point(j,k);
                if(radii[j+1]>0)Tri(a,b,c);Tri(a,c,d);
            }
            if(radii[0]>0)Tri(Point(0,k),Vector3.up*levels[0],Point(0,i));
            if(radii[radii.Length-1]>0)Tri(Point(levels.Length-1,i),Vector3.up*levels[levels.Length-1],Point(levels.Length-1,k));
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);return Save(name,mesh);
    }

    static void Outcrops(Transform root)
    {
        var grove=Group(root,"Rim pines and broken rock");
        var boulder=RingMesh("Split quarry boulder",new[]{.85f,1,.68f},new[]{-.17f,.43f,.86f},7,.21f);
        var crown=RingMesh("Angular pine boughs",new[]{1f,.51f,.76f,.29f,.47f,0},new[]{.12f,.44f,.4f,.7f,.65f,1},7,.08f);
        var needles=Mat("Pine needles","grass",new(.16f,.32f,.27f));
        var rand=new System.Random(412);
        float Range(float a,float b)=>(float)(a+(b-a)*rand.NextDouble());
        foreach(var center in new[]{new Vector2(-41,21),new(-44,39),new(-45,64),new(-44,112),new(-42,136),new(-32,147),new(-14,148),new(44,112),new(45,128),new(16,150)})
        {
            for(int i=0;i<5;i++){
                float x=Mathf.Clamp(center.x+Range(-3.7f,3.7f),-44,44), z=center.y+Range(-4,4), h=Height(x,z);
                float size=Range(.8f,2.3f);if(HoistSite(x,z,size) || z>151 || RoadDistance(Climb,x,z)<size+2.8f || RoadDistance(Haul,x,z)<size+4)continue;
                var stone=MeshGo(grove,"Broken quarry stone",boulder,rock);
                stone.transform.localPosition=new(x,h-.18f,z);stone.transform.localScale=new(size*1.3f,size,size*.86f);
                stone.transform.localRotation=Quaternion.Euler(0,Range(0,360),0);
            }
            for(int i=0;i<3;i++){
                float x=Mathf.Clamp(center.x+Range(-3,3),-43,43),z=center.y+Range(-4,4);
                if(HoistSite(x,z,2) || z>151 || RoadDistance(Climb,x,z)<4 || RoadDistance(Haul,x,z)<5)continue;
                float h=Range(3,6.6f),ground=Height(x,z);
                Solid(grove,"Fir trunk",new(x,ground+h*.24f,z),new(.33f,h*.52f,.33f),wood);
                var tree=MeshGo(grove,"Fir crown",crown,needles,false);
                tree.transform.localPosition=new(x,ground+h*.15f,z);tree.transform.localScale=new(h*.28f,h*.87f,h*.28f);
                tree.transform.localRotation=Quaternion.Euler(0,Range(0,360),0);
            }
        }
        FracturedRock(root,crown,needles);
        var water=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/FarmSilos/Paddock/Shallow water opaque.mat");
        var pool=MeshGo(root,"Sump water",RingMesh("Quarry sump",new[]{1f,1f},new[]{-.04f,0},16,0),water,false);
        pool.transform.localPosition=new(23,-6.23f,88);pool.transform.localScale=new(6.4f,1,10.5f);
    }

    static void Loader(Transform root){
        var p=Group(root,"Articulated quarry loader");p.localPosition=new(13,0,145);p.localRotation=Quaternion.Euler(0,115,0);
        var paint=Finish("Loader marigold",new(.84f,.49f,.095f));
        var rubber=Finish("Industrial rubber",new(.075f,.084f,.088f));var glassBlue=Finish("Loader glazing",new(.16f,.30f,.36f));
        foreach(float side in new[]{-1f,1f})foreach(float z in new[]{-1.82f,1.73f}){
            float x=side*1.38f;
            Tube(p,"Tyre",new(x-.36f,.94f,z),new(x+.36f,.94f,z),.94f,rubber,true);
            Tube(p,"Wheel hub",new(x+side*.345f,.94f,z),new(x+side*.405f,.94f,z),.55f,paint);
            Tube(p,"Hub cap",new(x+side*.403f,.94f,z),new(x+side*.47f,.94f,z),.22f,steel);
            for(int j=0;j<14;j++){float a=j*Mathf.PI/7;
                var lug=Solid(p,"Tyre lug",new(x,.94f+.91f*Mathf.Cos(a),z+.91f*Mathf.Sin(a)),new(.71f,.11f,.24f),rubber,false);
                lug.transform.localRotation=Quaternion.Euler(a*Mathf.Rad2Deg,side*12,0);}
        }
        Solid(p,"Rear chassis",new(0,1.22f,-1.23f),new(2.63f,.62f,2.79f),steel);
        Solid(p,"Engine bonnet",new(0,1.99f,-2.12f),new(2.55f,1.14f,1.81f),paint);
        Solid(p,"Counterweight",new(0,1.13f,-3.08f),new(3.07f,.62f,.44f),paint);
        for(int j=0;j<6;j++)Solid(p,"Engine grille",new(0,1.63f+.13f*j,-3.036f),new(1.97f,.069f,.045f),rubber,false);
        Tube(p,"Exhaust stack",new(.9f,2.53f,-2.18f),new(.9f,3.65f,-2.18f),.12f,steel);
        Solid(p,"Cab pedestal",new(0,1.77f,-.36f),new(2.0f,.73f,1.87f),steel);
        Solid(p,"Operator cab",new(0,2.87f,-.36f),new(2.03f,1.85f,1.74f),glassBlue);
        foreach(float x in new[]{-1.03f,1.03f})foreach(float z in new[]{-1.24f,.53f})
            Solid(p,"Cab pillar",new(x,2.89f,z),new(.15f,1.98f,.15f),paint);
        Solid(p,"Cab roof",new(0,3.92f,-.36f),new(2.4f,.22f,2.14f),paint);
        Solid(p,"Articulation joint",new(0,1.28f,.75f),new(1.41f,.73f,1.37f),steel);
        foreach(float x in new[]{-.83f,.83f}){
            Beam(p,"Bucket lift arm",new(x,1.94f,1.29f),new(x,.82f,3.67f),.32f,paint);
            Tube(p,"Lift cylinder",new(x,.98f,1.27f),new(x,1.44f,2.24f),.16f,steel);
            Tube(p,"Polished piston",new(x,1.44f,2.24f),new(x,1.57f,2.58f),.095f,glassBlue);
        }
        Solid(p,"Bucket sole",new(0,.23f,3.98f),new(3.37f,.18f,1.43f),steel);
        Solid(p,"Curved bucket back",new(0,.67f,3.41f),new(3.5f,.94f,.20f),paint).transform.localRotation=Quaternion.Euler(-18,0,0);
        foreach(float x in new[]{-1.67f,1.67f})Solid(p,"Bucket side plate",new(x,.54f,3.93f),new(.146f,.62f,1.22f),paint);
        for(int i=0;i<6;i++)Solid(p,"Cutting tooth",new(-1.43f+i*.57f,.17f,4.76f),new(.23f,.14f,.46f),steel);
    }
    static void Crane(Transform root)
    {
        var p=Group(root,"Crane");
        Box(p,"Crane pad",new(30,.1f,137),new(12,.2f,10),gravel);
        foreach(float x in new[]{27.6f,32.4f})
            Solid(p,"Crawler tread",new(x,.88f,137),new(1.25f,1.35f,6.9f),steel);
        Solid(p,"Slewing base",new(30,1.8f,137),new(5.2f,.9f,5.2f),yellow);
        Solid(p,"Engine housing",new(31,3,138),new(2.7f,2.1f,4),yellow);
        Solid(p,"Operator cab",new(28.5f,3.45f,136),new(2.3f,2.7f,2.7f),yellow);
        Solid(p,"Cab front glazing",new(28.5f,3.8f,134.6f),new(1.93f,1.45f,.1f),glass,false);
        Solid(p,"Cab side glazing",new(27.32f,3.8f,136),new(.1f,1.45f,2.15f),glass,false);
        Solid(p,"Counterweight",new(30,2.85f,140),new(5.4f,1.65f,1.65f),steel);
        Vector3 foot=new(30,2.9f,134.8f), tip=new(30,13.5f,116);
        for(int i=0;i<2;i++){
            var side=Vector3.right*(i==0?-.72f:.72f);
            var a=foot+side;var b=tip+side;var up=Vector3.up*1.02f;
            Beam(p,"Boom chord",a,b,.21f,yellow);Beam(p,"Boom chord",a+up,b+up,.21f,yellow);
            for(int j=0;j<7;j++){
                var lower=Vector3.Lerp(a,b,j/7f);var upper=Vector3.Lerp(a,b,(j+1)/7f);
                Beam(p,"Lattice brace",lower,upper+up,.15f,steel);
                Beam(p,"Lattice brace",lower+up,upper,.13f,yellow);
            }
            Beam(p,"Backstay",new(30+side.x,4,140),b+up,.10f,steel);
        }
        Beam(p,"Head sheave",tip-Vector3.right*.8f,tip+Vector3.right*.8f,.34f,yellow);
        Beam(p,"Hoist cable",tip,new(30,3.6f,116),.09f,steel);
        foreach(float x in new[]{28.8f,31.2f})Beam(p,"Bucket sling",new(30,3.6f,116),new(x,2.2f,116),.10f,steel);
        Solid(p,"Suspended ore skip",new(30,1.5f,116),new(2.7f,1.6f,2.55f),yellow);
        Solid(p,"Ore in skip",new(30,2.32f,116),new(2.32f,.2f,2.2f),rock);
        var wheelMetal=Finish("Crane running gear",new(.25f,.255f,.23f));
        foreach(float side in new[]{-1f,1f}){
            float x=30+side*2.4f;
            for(int i=0;i<5;i++){
                float z=134.62f+i*1.18f;
                Tube(p,"Crawler road wheel",new(x+side*.59f,.91f,z),new(x+side*.71f,.91f,z),.49f,wheelMetal);
            }
            for(int j=0;j<11;j++)Solid(p,"Tread shoe",new(x,.29f,133.83f+j*.62f),new(1.39f,.19f,.34f),steel,false);
        }
        Tube(p,"Turntable bearing",new(30,1.59f,137),new(30,1.88f,137),2.85f,wheelMetal);
        Solid(p,"Cab canopy",new(28.5f,4.88f,136),new(2.61f,.17f,3.02f),yellow);
        for(int i=0;i<6;i++)Solid(p,"Engine louvre",new(32.405f,2.45f+i*.20f,138.18f),new(.10f,.095f,2.49f),steel,false);
        Tube(p,"Engine exhaust",new(31.5f,4.01f,138.98f),new(31.5f,5.15f,138.98f),.19f,steel);
        for(int i=0;i<4;i++)Beam(p,"Cab ladder rung",new(26.96f,1.68f+i*.36f,136.54f),new(26.96f,1.68f+i*.36f,137.22f),.09f,wheelMetal);
        foreach(float z in new[]{136.54f,137.22f})Beam(p,"Cab ladder stile",new(26.96f,1.44f,z),new(26.96f,2.9f,z),.10f,wheelMetal);
        Tube(p,"Hoist winch",new(28.55f,4.18f,139.36f),new(30.34f,4.18f,139.36f),.51f,wheelMetal);
        for(int j=0;j<6;j++)Tube(p,"Winch rope winding",new(28.66f+j*.24f,4.18f,139.36f),new(28.77f+j*.24f,4.18f,139.36f),.53f,steel);
        foreach(float x in new[]{29.42f,30.58f})Tube(p,"Boom pulley",new(x-.11f,13.55f,116),new(x+.11f,13.55f,116),.56f,wheelMetal);
        Tube(p,"Hook block",new(29.78f,3.77f,116),new(30.22f,3.77f,116),.32f,wheelMetal);
        foreach(float x in new[]{28.70f,31.30f})Solid(p,"Skip corner strap",new(x,1.48f,114.69f),new(.18f,1.65f,.11f),steel,false);
        Lamp(p,"Crane worklight",new(28.5f,4.75f,134.53f),true,65);
    }

    static Texture2D AggregateTexture()
    {
        const int size=192;
        string path=Folder+"/Crushed aggregate.png";
        var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(existing)return existing;
        var image=new Texture2D(size,size,TextureFormat.RGB24,false);
        var pixels=new Color32[size*size];var rng=new System.Random(172);
        for(int i=0;i<pixels.Length;i++){
            byte n=(byte)rng.Next(151,175);pixels[i]=new Color32(n,(byte)(n+1),(byte)(n+3),255);
        }
        for(int i=0;i<1150;i++){
            int x=rng.Next(size),y=rng.Next(size),length=rng.Next(1,5),rise=rng.Next(1,3),shade=rng.Next(128,195);
            for(int j=0;j<rise;j++)for(int k=0;k<length-j;k++){
                byte n=(byte)(shade+j*6);pixels[((y+j)%size)*size+(x+k)%size]=new(n,n,(byte)(n+2),255);
            }
        }
        image.SetPixels32(pixels);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Finish(string name,Color tint, float glow=0)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",.16f);
        if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",tint*glow);}
        EditorUtility.SetDirty(m);return m;
    }

    // Front-facing silhouettes for the enamel badge and clipped warning stripes.
    static Mesh Plaque(string name,params Vector2[] outline)
    {
        var m=new Mesh();var vertices=outline.Select(v=>new Vector3(v.x,v.y,0)).ToArray();
        var tris=new List<int>();
        for(int i=1;i<outline.Length-1;i++){
            int a=i,b=i+1;
            if(Vector3.Cross(vertices[a]-vertices[0],vertices[b]-vertices[0]).z>0){a=i+1;b=i;}
            tris.AddRange(new[]{0,a,b});
        }
        m.vertices=vertices;m.uv=outline;m.SetTriangles(tris,0);m.RecalculateNormals();
        Unwrapping.GenerateSecondaryUVSet(m);return Save(name,m);
    }

    static void Entrance(Transform parent)
    {
        var sign=Group(parent,"Quarry arrival sign");sign.localPosition=new(0,0,5.5f);
        var red=Finish("Oxide red enamel",new(.55f,.135f,.09f));
        var faded=Finish("Faded oxide enamel",new(.63f,.19f,.13f));
        var ivory=Finish("Quarry sign ivory",new(.98f,.84f,.56f),.16f);
        var dark=Finish("Iron hardware",new(.11f,.14f,.16f));
        var amber=Finish("Lantern glass",new(1,.65f,.17f),2);
        var lens=RingMesh("Gate lantern lens",new[]{.42f,.87f,1,.64f},new[]{0f,.22f,.70f,1},10,0);
        var shade=RingMesh("Gate lamp shade",new[]{1f,.96f,.26f},new[]{0f,.13f,.80f},10,0);
        var stripe=Plaque("Footing chevron",new(-.72f,-.35f),new(-.72f,-1.03f),new(-.42f,-1.03f),new(.72f,.40f),new(.72f,1.03f),new(.37f,1.03f));
        foreach(float x in new[]{-8.3f,8.3f}){
            Solid(sign,"Stone footing",new(x,.18f,0),new(2.05f,.36f,1.75f),rock);
            Solid(sign,"Squared timber post",new(x,4.65f,0),new(1.13f,8.9f,1.02f),wood);
            Solid(sign,"Black plinth",new(x,1.49f,-.02f),new(1.54f,2.3f,1.23f),dark);
            MeshGo(sign,"Yellow diagonal marking",stripe,railPaint,false).transform.localPosition=new(x,1.49f,-.643f);
            Solid(sign,"Lamp bracket",new(x,7.6f,-.88f),new(.14f,.16f,1.5f),dark,false);
            var lensGo=MeshGo(sign,"Warm lantern globe",lens,amber,false);lensGo.transform.localPosition=new(x,6.55f,-1.47f);lensGo.transform.localScale=new(.47f,.68f,.47f);
            var shadeGo=MeshGo(sign,"Black enamel lamp shade",shade,dark,false);shadeGo.transform.localPosition=new(x,7.17f,-1.47f);shadeGo.transform.localScale=new(.72f,.52f,.72f);
            Lamp(sign,"Gate lantern",new(x,6.62f,-1.46f),true,95);
        }
        BoundaryFences(parent,sign);
        Solid(sign,"Lintel",new(0,7.28f,0),new(17.65f,.85f,.79f),wood);
        for(int i=-8;i<=8;i++){
            float x=i*.81f;float top=9.12f+.45f*(Mathf.Abs(i)<6?1:0)+1.06f*Mathf.Sqrt(Mathf.Clamp01(1-x*x/7));
            Solid(sign,"Painted timber slat",new(x,(top+6.96f)/2,-.55f),new(.802f,top-6.96f,.32f),i%4==0?faded:red,false);
        }
        var mountain=Plaque("Quarry peak device",new(-1,0),new(.08f,1.2f),new(1,0));
        foreach(var q in new[]{new Vector3(-.75f,9.27f,.85f),new(.29f,9.27f,1.05f),new(1.18f,9.27f,.55f)}){
            var crest=MeshGo(sign,"Ivory mountain",mountain,ivory,false);crest.transform.localPosition=new(q.x,q.y,-.724f);crest.transform.localScale=Vector3.one*q.z;
        }
        RaisedLetters(sign,ivory);
    }

    static void BoundaryFences(Transform parent,Transform sign){
        var sides=Group(parent,"Timber boundary fences");
        foreach(float side in new[]{-1f,1f}){
            Vector3 previous=new(side*8.3f,0,0);
            for(int i=1;i<=7;i++){
                Vector3 next=new(side*Mathf.Lerp(8.3f,47,i/7f),0,0);
                Box(sign,"Entrance fence post",next+Vector3.up*.82f,new(.24f,1.73f,.24f),wood);
                foreach(float y in new[]{.51f,1.23f})Beam(sign,"Entrance fence rail",previous+Vector3.up*y,next+Vector3.up*y,.18f,wood);
                previous=next;
            }
            float x=47*side,at=1;
            Vector3 Foot(float z)=>new(x,GroundMeshHeight(x,z),z);
            void Post(Vector3 foot)=>Box(sides,"Boundary post",foot+Vector3.up*.64f,new(.16f,1.44f,.16f),wood);
            Post(Foot(at));
            while(at<159){
                float end=Mathf.Min(at+6,159);
                // Shorten spans at grade changes so that a single rail follows the mountainside.
                while(end-at>1){bool follows=true;
                    for(float z=at+.5f;z<end;z+=.5f)
                        if(Mathf.Abs(Foot(z).y-Mathf.Lerp(Foot(at).y,Foot(end).y,(z-at)/(end-at)))>.19f){follows=false;break;}
                    if(follows)break;end-=1;
                }
                var a=Foot(at);var b=Foot(end);Post(b);
                Beam(sides,"Boundary rail",a+Vector3.up*.91f,b+Vector3.up*.91f,.14f,wood);
                // Independent of decorative post spacing, the solid boundary follows each terrain triangle.
                for(float z=at;z<end;z+=1){
                    var u=Foot(z);var v=Foot(Mathf.Min(z+1,end));var wall=Group(sides,"Boundary collision");
                    wall.gameObject.layer=6;wall.localPosition=(u+v)*.5f+Vector3.up*.5f;
                    wall.gameObject.AddComponent<BoxCollider>().size=new(.17f,1.18f+Mathf.Abs(v.y-u.y),v.z-u.z);
                }
                at=end;
            }
        }
    }

    // Read the approved CutePixel glyphs and extrude only the visible glyph faces and outline.
    static void RaisedLetters(Transform sign,Material ivory){
        const string word="QUACKRRY";
        var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");font.ReadFontAssetDefinition();
        var atlas=font.atlasTexture;var rt=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var old=RenderTexture.active;var image=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false,true);
        try{
            Graphics.Blit(atlas,rt);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            var surf=new Surface();float advance=word.Sum(ch=>font.characterLookupTable[ch].glyph.metrics.horizontalAdvance);
            float scale=13.1f/advance,cursor=-advance*.5f;
            foreach(char ch in word){
                var glyph=font.characterLookupTable[ch].glyph;var rect=glyph.glyphRect;var metrics=glyph.metrics;
                int rows=18,cols=Mathf.Max(1,Mathf.RoundToInt((float)rect.width/rect.height*rows));
                var ink=new bool[cols+2,rows+2];
                for(int j=0;j<rows;j++)for(int i=0;i<cols;i++)ink[i+1,j+1]=image.GetPixel(rect.x+(int)((i+.5f)*rect.width/cols),rect.y+(int)((j+.5f)*rect.height/rows)).a>.50f;
                Vector3 Point(int i,int j,float z)=>new((cursor+metrics.horizontalBearingX+i*rect.width/(float)cols)*scale,8.19f+(j/(float)rows-.5f)*1.55f,z);
                const float front=-1.04f,back=-.70f;
                for(int j=0;j<rows;j++)for(int i=0;i<cols;i++){
                    if(!ink[i+1,j+1])continue;
                    if(i==0 || !ink[i,j+1]){
                        int end=i+1;while(end<cols && ink[end+1,j+1])end++;
                        surf.Quad(Point(i,j,front),Point(i,j+1,front),Point(end,j+1,front),Point(end,j,front));
                        surf.Quad(Point(end,j,back),Point(end,j+1,back),Point(i,j+1,back),Point(i,j,back));
                    }
                    void Edge(int ax,int ay,int bx,int by)=>surf.Quad(Point(ax,ay,front),Point(bx,by,front),Point(bx,by,back),Point(ax,ay,back));
                    if(!ink[i,j+1])Edge(i,j+1,i,j);
                    if(!ink[i+2,j+1])Edge(i+1,j,i+1,j+1);
                    if(!ink[i+1,j])Edge(i,j,i+1,j);
                    if(!ink[i+1,j+2])Edge(i+1,j+1,i,j+1);
                }
                cursor+=metrics.horizontalAdvance;
            }
            surf.Build(sign,word+" solid CutePixel letters",ivory,false);
        }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static void SummitHoist(Transform root){
        var rig=Group(root,"Summit shaft hoist");rig.localPosition=new(-42,14.4f,106);
        var iron=Finish("Headframe iron",new(.225f,.20f,.165f));var pale=Finish("Weathered headframe",new(.47f,.31f,.18f));
        void Timber(Vector3 a,Vector3 b,float w)=>Beam(rig,"Headframe timber",a,b,w,pale);
        // A three-sided collar leaves the south lip accessible from the summit stairs.
        foreach(float x in new[]{-3.48f,3.62f})Solid(rig,"Shaft collar",new(x,.11f,0),new(x<0?.96f:1.24f,.22f,6.96f),wood);
        Solid(rig,"Rear collar",new(0,.11f,3.5f),new(5.998f,.22f,1f),wood);
        Rail(rig,new(-3.82f,.22f,-3.39f),new(-3.82f,.22f,3.63f));
        Rail(rig,new(-3.82f,.22f,3.8f),new(4.96f,.22f,3.8f));
        Solid(rig,"Winch sill",new(4.55f,.22f,.1f),new(2.72f,.42f,6.5f),wood);
        foreach(float z in new[]{-1.46f,1.46f}){
            Timber(new(3.27f,.36f,z),new(4.57f,5.35f,z),.41f);
            Timber(new(5.87f,.36f,z),new(4.57f,5.28f,z),.36f);
            Solid(rig,"Bolted bearing plate",new(4.56f,5.15f,z),new(.77f,.56f,.16f),iron);
        }
        Timber(new(4.58f,5.28f,-1.6f),new(4.58f,5.28f,1.6f),.42f);
        void Reel(string name,Vector3 c,float radius,float width){
            Tube(rig,name,c-Vector3.forward*width/2,c+Vector3.forward*width/2,radius,iron);
        }
        var drum=new Vector3(4.54f,1.13f,-.1f);Reel("Winding drum",drum,.62f,2.3f);
        for(int j=0;j<11;j++)Reel("Wound rope",drum+Vector3.forward*(-1.02f+j*.20f),.66f,.10f);
        foreach(float z in new[]{-1.35f,1.14f})Solid(rig,"Winch bearing",new(4.54f,.88f,z),new(1.30f,.99f,.22f),iron);
        var wheel=new Vector3(4.57f,5.14f,-1.67f);Reel("Sheave hub",wheel,.26f,.47f);
        Sheave(rig,wheel,iron);
        var tip=new Vector3(-1.72f,4.65f,-1.74f);
        Timber(new(4.1f,2.45f,-1.6f),tip,.34f);Timber(new(4.5f,3.86f,-1.6f),tip,.24f);
        Reel("Jib sheave",tip,.36f,.31f);
        Beam(rig,"Hoist rope",drum+new Vector3(-.53f,0,-.93f),wheel+Vector3.left*1.15f,.07f,steel);
        Beam(rig,"Hoist rope",wheel+Vector3.up*1.17f,tip+Vector3.up*.32f,.07f,steel);
        Beam(rig,"Drop rope",tip,new(tip.x,1.70f,tip.z),.065f,steel);
        var skip=new Vector3(tip.x,.47f,tip.z);
        Solid(rig,"Bucket sole",skip,new(1.05f,.12f,1.05f),iron);
        foreach(float sign in new[]{-1f,1f}){
            Solid(rig,"Bucket plate",skip+new Vector3(sign*.52f,.33f,0),new(.10f,.68f,1.10f),iron);
            Solid(rig,"Bucket plate",skip+new Vector3(0,.33f,sign*.50f),new(.925f,.68f,.10f),iron);
            Beam(rig,"Bucket bail",skip+new Vector3(sign*.48f,.65f,0),new(tip.x+sign*.042f,1.70f,tip.z),.075f,steel);
        }
        Solid(rig,"Lamp upright",new(5.65f,1.98f,-2.32f),new(.14f,3.55f,.14f),iron);
        Solid(rig,"Winch lamp bracket",new(5.31f,3.72f,-2.32f),new(.79f,.13f,.16f),iron);
        Lamp(rig,"Winch work light",new(4.98f,3.61f,-2.32f),true,67);
        Solid(rig,"Spare tackle crate",new(4.57f,.82f,2.48f),new(1.03f,.79f,.84f),pale);
        var slab=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset");
        for(int tier=0;tier<5;tier++){
            float y=-1.2f-tier*3.35f;
            foreach(float x in new[]{-2.96f,2.96f})Timber(new(x,y,-3),new(x,y,3),.22f);
            foreach(float z in new[]{-2.96f,2.96f})Timber(new(-2.85f,y,z),new(2.85f,y,z),.215f);
            for(int j=0;j<4;j++){var b=MeshGo(rig,"Wall rubble",slab,rock);float offset=1.7f*Mathf.Sin(1.82f*tier+j);
                b.transform.localPosition=new(j<2?(j==0?-3f:3f):offset,y-.9f,j<2?offset:(j==2?-3f:3f));
                b.transform.localScale=new(.46f,.63f,.52f);b.transform.localRotation=Quaternion.Euler(0,47*j+19*tier,0);}
            if(tier%2==0)ColdShaftLamp(rig,tier,new(-2.90f,y-.14f,.77f));
        }
    }

    static void MinePuddles(Transform root){
        var water=Finish("Mine seep water",new(.15f,.24f,.265f));water.SetFloat("_Smoothness",.91f);
        var floor=new Surface();var rng=new System.Random(7003);
        var centers=new[]{new Vector2(-25,70),new(-25,89),new(-18,101),new(-5,103),new(4,97),new(-12,92),new(4,79),new(-17,79),new(-4,67),new(8,59),new(-7,74),new(-8,105),new(-12,119),new(-34,102)};
        foreach(var start in centers){
            var center=start+new Vector2((float)rng.NextDouble()-.5f,(float)rng.NextDouble()-.5f);
            float width=1.05f+.46f*(float)rng.NextDouble(),depth=.78f+.55f*(float)rng.NextDouble();
            int xmin=Mathf.FloorToInt(center.x-width-1),zmin=Mathf.FloorToInt(center.y-depth-1);
            for(int z=zmin;z<center.y+depth+1;z++)for(int x=xmin;x<center.x+width+1;x++){
                var a=new MineVertex(x,z);var b=new MineVertex(x,z+1);var c=new MineVertex(x+1,z+1);var d=new MineVertex(x+1,z);
                float Wet(MineVertex v){var q=v.p-center;return 1-(q.x*q.x/(width*width)+q.y*q.y/(depth*depth))+.15f*Mathf.Sin(v.p.x*2+v.p.y);}
                foreach(var tri in new[]{new List<MineVertex>{a,b,c},new List<MineVertex>{a,c,d}})
                    Skin(floor,Clip(Clip(tri,Wet),v=>-v.d-.12f),v=>v.f+.015f);
            }
        }
        floor.Build(root,"Mine seep puddles",water,false);
    }
    static void BoringMachine(Transform root)
    {
        var rig=Group(root,"Tunnel boring machine");rig.localPosition=new(-13,-6,130);
        var wheel=RingMesh("Mining cylinder",new[]{1f,1},new[]{-.5f,.5f},20,0);
        var cutter=Finish("Cutter steel",new(.40f,.45f,.49f));
        var orange=Finish("Borer safety orange",new(.91f,.39f,.085f));
        void Drum(string name,float y,float z,float radius,float length,Material mat){
            var g=MeshGo(rig,name,wheel,mat);g.transform.localPosition=new(0,y,z);g.transform.localScale=new(radius,length,radius);
            g.transform.localRotation=Quaternion.Euler(90,0,0);
        }
        Drum("Segmented shield",2.15f,3.8f,1.76f,4.7f,orange);
        Drum("Cutter head",2.25f,6.75f,2.19f,.64f,cutter);
        Drum("Spindle",2.25f,6.84f,.56f,1.1f,yellow);
        for(int i=0;i<12;i++){
            float angle=i*Mathf.PI/6;float dx=Mathf.Cos(angle),dy=Mathf.Sin(angle);
            var arm=Solid(rig,"Radial cutter arm",new(1.21f*dx,2.25f+1.21f*dy,7.12f),new(1.46f,.27f,.31f),yellow,false);
            arm.transform.localRotation=Quaternion.Euler(0,0,i*30);
            var tooth=Solid(rig,"Replaceable disc cutter",new(1.95f*dx,2.25f+1.95f*dy,7.24f),new(.38f,.35f,.57f),steel,false);
            tooth.transform.localRotation=arm.transform.localRotation;
        }
        foreach(float x in new[]{-1.17f,1.17f}){
            Solid(rig,"Crawler assembly",new(x,1.01f,.27f),new(.57f,.59f,5.5f),steel);
            Solid(rig,"Hydraulic thrust jack",new(x,1.29f,.2f),new(.42f,.44f,3.8f),yellow);
        }
        Solid(rig,"Operator cab",new(-.65f,1.96f,-1.14f),new(1.45f,1.7f,1.65f),orange);
        Solid(rig,"Rear cab glazing",new(-.65f,2.1f,-2.0f),new(1.15f,.92f,.09f),glass,false);
        Solid(rig,"Tail conveyor",new(.55f,1.04f,-3.1f),new(1.16f,.42f,4.7f),steel);
        foreach(float x in new[]{.02f,1.08f})Solid(rig,"Conveyor lip",new(x,1.33f,-3.1f),new(.12f,.28f,4.7f),orange);
        foreach(float z in new[]{-7f,-2.5f,2.2f,6.5f}){
            foreach(float side in new[]{-1f,1f})Solid(rig,"Bore bay rib",new(side*5.55f,2.3f,z),new(.23f,4.6f,.28f),steel);
            Solid(rig,"Bore bay crossbeam",new(0,4.68f,z),new(11.2f,.22f,.28f),steel);
        }
        Drum("Rear drive collar",2.15f,1.32f,1.61f,.35f,cutter);
        for(int i=0;i<6;i++){
            float a=i*Mathf.PI/3;Solid(rig,"Drive fastener",new(1.32f*Mathf.Cos(a),2.15f+1.32f*Mathf.Sin(a),1.12f),new(.21f,.21f,.17f),yellow,false);
        }
        for(int j=-7;j<=7;j+=2)Solid(rig,"Machine track sleeper",new(0,.04f,j),new(3.18f,.23f,.29f),wood,false);
        foreach(float side in new[]{-1f,1f}){
            float x=side*1.17f;
            Solid(rig,"Machine rail",new(x,.21f,0),new(.15f,.19f,15.55f),cutter,false);
            for(int j=-2;j<=2;j+=2)Tube(rig,"Bogie wheel",new(x-.27f,.64f,j),new(x+.27f,.64f,j),.34f,cutter);
            for(int j=0;j<3;j++)Tube(rig,"Thrust line",new(side*1.5f,1.41f+j*.16f,-1.64f),new(side*1.5f,1.41f+j*.16f,2.58f),.061f,cutter);
            for(int j=0;j<7;j++){float z=-6.35f+2.08f*j;float xx=side*(3.83f+.46f*Mathf.Sin(j*1.9f));
                var debris=MeshGo(rig,"Bore spoil",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset"),rock);
                debris.transform.localPosition=new(xx,MineFloor(-13+xx,130+z)+6-.11f,z);debris.transform.localScale=new(.65f+.13f*(j%3),.67f+.19f*(j%4),.79f);debris.transform.localRotation=Quaternion.Euler(0,51*j,0);}
        }
        Solid(rig,"Cab visor",new(-.65f,2.83f,-1.91f),new(1.64f,.13f,.59f),steel);
        for(int i=0;i<5;i++)Solid(rig,"Cab cooling slot",new(-1.394f,1.62f+i*.13f,-1.18f),new(.064f,.07f,1.12f),cutter,false);
        for(int i=0;i<7;i++)Solid(rig,"Conveyor flight",new(.55f,1.29f,-5.18f+i*.61f),new(.91f,.085f,.09f),cutter,false);
        for(int i=0;i<3;i++)Drum("Shield reinforcing ring",2.15f,2.15f+i*1.38f,1.81f,.13f,cutter);
        Lamp(rig,"Service flood",new(-.65f,2.72f,-2.16f),true,65);
        Lamp(rig,"Cutter inspection lamp",new(2.9f,4.52f,6.5f),false,28);
    }

    static void RoadFence(Transform root)
    {
        var fences=Group(root,"Yellow haul road fence");
        for(int side=-1;side<=1;side+=2){
            Vector3 Edge(int i){
                Vector3 t=Haul[Mathf.Min(i+1,Haul.Length-1)]-Haul[Mathf.Max(i-1,0)];
                Vector3 q=Haul[i]+Vector3.Cross(Vector3.up,t).normalized*(side*2.75f);
                q.y=Height(q.x,q.z);return q;
            }
            for(int i=1;i<Haul.Length-1;i++){
                if(i==1 || (side<0 && (i==6 || i==7 || i==8)))continue;
                var a=Edge(i-1);var b=Edge(i);
                for(int j=0;j<2;j++){var u=Vector3.Lerp(a,b,j*.5f);var v=Vector3.Lerp(a,b,(j+1)*.5f);
                    u.y=Height(u.x,u.z);v.y=Height(v.x,v.z);Rail(fences,u,v,true);}
            }
        }
        // Rock-lip rails around the platform leave its eastern entrance open to the road.
        for(int z=57;z<93;z+=3){
            float LipFence(float at)=>ShelfLip(at)+1.75f+2.4f*Mathf.Clamp01((65-at)/8);
            float x=LipFence(z),endX=LipFence(z+3);
            Rail(fences,new(x,Height(x,z),z),new(endX,Height(endX,z+3),z+3),true);
        }
    }

    static void FracturedRock(Transform root,Mesh crown,Material needles)
    {
        var geology=Group(root,"Quarry faces and talus");
        var meshes=new[]{
            RingMesh("Jointed granite buttress",new[]{.84f,1,.91f,.58f},new[]{-.22f,.21f,.70f,.95f},6,.26f),
            RingMesh("Split slate slab",new[]{.92f,1,.73f,.38f},new[]{-.18f,.29f,.73f,1},7,.32f),
            RingMesh("Angular rubble block",new[]{.78f,1,.68f},new[]{-.25f,.48f,.92f},5,.28f)};
        var colors=new[]{Finish("Blue grey granite",new(.27f,.31f,.4f)),Finish("Violet shale",new(.27f,.245f,.33f)),Finish("Fresh split rock",new(.37f,.37f,.45f)),Finish("Dark bedding",new(.18f,.205f,.26f))};
        foreach(var material in colors)material.SetTexture("_BaseMap",fines.GetTexture("_BaseMap"));
        var random=new System.Random(737);
        float R(float a,float b)=>(float)(a+(b-a)*random.NextDouble());
        void Rock(Transform p,float x,float y,float z,float w,float h,float d,bool collide=true){
            var g=MeshGo(p,"Fractured stone",meshes[random.Next(meshes.Length)],colors[random.Next(colors.Length)],collide);
            g.transform.localPosition=new(x,y,z);g.transform.localScale=new(w,h,d);g.transform.localRotation=Quaternion.Euler(R(-7,7),R(0,360),R(-8,8));
        }
        bool Clear(float x,float z,float radius){
            if(Mathf.Abs(x)<8+radius || Mathf.Abs(x)>47-radius || z<7+radius || z>153-radius)return false;
            if(x<0){
                if(HoistSite(x,z,radius))return false;
                if(x>-36 && z>119 || (Mathf.Abs(x+25)<6+radius && z<58) || (x>-22-radius && z<30))return false;
                return RoadDistance(Climb,x,z)>radius+2.3f;
            }
            if(z>124 || (x>29 && z>57 && z<92) || (Mathf.Abs(x-26)<2.2f+radius && z>48 && z<109)
                || (Mathf.Abs(x-23)<4.8f+radius && Mathf.Abs(z-88)<9+radius))return false;
            if(Tunnel(x,z) || RoadDistance(PitRoad,x,z)<radius+2.7f)return false;
            return RoadDistance(Haul,x,z)>radius+3.6f;
        }
        // Bedrock faces follow the strike of individual ledges. Large blocks key into the cut;
        // thin spalls and smaller angular debris collect below each exposure.
        for(int band=0;band<4;band++)for(float z=32+band*3;z<129;z+=R(3.4f,6.2f)){
            float x=-Toe(z)-(3.6f+7.5f*band)-15*Mathf.SmoothStep(0,1,(z-105)/22);
            float w=R(1.6f,3.5f),d=R(2.3f,4.5f);if(!Clear(x,z,Mathf.Max(w,d)))continue;
            float baseY=Mathf.Min(Height(x+2,z),Height(x+1,z+1));
            Rock(geology,x,baseY-.18f,z,w,Mathf.Max(2.1f,Height(x-2,z)-baseY+R(.45f,1.2f)),d);
            for(int k=0;k<4;k++){float xx=x+R(1.4f,4.8f),zz=z+R(-3,3),s=R(.38f,1.2f);
                if(Clear(xx,zz,s))Rock(geology,xx,Height(xx,zz)-s*.12f,zz,s,s*R(.51f,.95f),s*R(.66f,1.24f));}
        }
        // Shallow projecting columns break up the vertical faces beside the climbing route.
        for(int band=0;band<4;band++)for(float z=43+band*2;z<112;z+=R(3.2f,5.3f)){
            float x=-Toe(z)-(3.6f+band*7.55f);if(HoistSite(x,z,3) || x< -45 || RoadDistance(Climb,x,z)<4.25f || Mathf.Abs(x+25)<5.8f && z<58)continue;
            float lower=Height(x+2,z),upper=Height(x-2,z);
            if(upper-lower>1.8f)Rock(geology,x+.28f,lower-.27f,z,R(1,1.6f),upper-lower+R(.12f,.75f),R(1.5f,2.7f));
        }
        for(int i=0;i<156;i++){
            float x=R(9,43),z=R(31,119),w=R(.55f,2.8f);if(!Clear(x,z,w))continue;
            float y=Height(x,z);if(y>-.05f && x<39)continue;
            Rock(geology,x,y-.25f,z,w,R(.72f,1.85f)*w,w*R(.75f,1.15f));
        }
        for(int z=55;z<100;z+=3){
            float x=ShelfLip(z)-.6f,baseY=Height(x-1.3f,z);
            if(x<29 && Mathf.Abs(z-73)<8 || RoadDistance(PitRoad,x,z)<4 || RoadDistance(Haul,x,z)<5)continue;
            Rock(geology,x,baseY-.3f,z,1.5f,Mathf.Max(1.4f,Height(x+1,z)-baseY+.25f),R(1.85f,2.55f));
        }
        var corner=Group(root,"Southeast old growth");
        foreach(var p in new[]{new Vector3(32,10,23),new(44,13.8f,13),new(33,11,30),new(24,9.7f,20),new(43,10.6f,38)}){
            float h=p.y,y=Height(p.x,p.z);
            var trunk=MeshGo(corner,"Old growth bole",meshes[0],wood);trunk.transform.localPosition=new(p.x,y,p.z);trunk.transform.localScale=new(.072f*h,.77f*h,.075f*h);
            for(int i=0;i<2;i++){
                var tree=MeshGo(corner,"Old growth fir crown",crown,needles,false);tree.transform.localPosition=new(p.x,y+h*(.32f+i*.25f),p.z);
                tree.transform.localScale=new(h*(.31f-i*.1f),h*(.65f-i*.14f),h*(.29f-i*.09f));tree.transform.localRotation=Quaternion.Euler(0,R(0,360),0);
            }
        }
        foreach(var q in new[]{new Vector3(24,3.3f,12),new(32,4.5f,14),new(41,4.2f,17),new(44,3.7f,29),new(37,2.9f,19)}){
            Rock(corner,q.x,Height(q.x,q.z)-.3f,q.z,q.y,q.y*1.08f,q.y*.86f);
            for(int i=0;i<7;i++){float x=q.x+R(-4,4),z=q.z+R(-3,3),s=R(.4f,1.22f);if(Clear(x,z,s))Rock(corner,x,Height(x,z)-.12f,z,s,s*.78f,s*1.13f);}
        }
        var tip=Group(root,"Northeast rock loading yard");
        // The coarse core is buried under irregular pieces of blasted stone.
        var heap=MeshGo(tip,"Spoil bank",meshes[1],colors[1]);heap.transform.localPosition=new(30,-.2f,147);heap.transform.localScale=new(10,5.5f,6.4f);
        var heapMesh=heap.GetComponent<MeshFilter>().sharedMesh;
        var pileVertices=heapMesh.vertices.Select(v=>heap.transform.localPosition+Vector3.Scale(heap.transform.localScale,v)).ToArray();
        var pileIndices=heapMesh.triangles;
        float SpoilSurface(float x,float z){
            float y=GroundMeshHeight(x,z);
            for(int face=0;face<pileIndices.Length;face+=3){
                var a=pileVertices[pileIndices[face]];var b=pileVertices[pileIndices[face+1]];var c=pileVertices[pileIndices[face+2]];
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.00001f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;
                if(u>=0 && v>=0 && u+v<=1)y=Mathf.Max(y,u*a.y+v*b.y+(1-u-v)*c.y);
            }
            return y;
        }
        for(int i=0;i<35;i++){
            float a=R(0,Mathf.PI*2),r=Mathf.Sqrt(R(0,1)),x=30+10.8f*r*Mathf.Cos(a),z=146.8f+6.8f*r*Mathf.Sin(a);
            float s=R(.92f,1.85f);Rock(tip,x,SpoilSurface(x,z)-s*.26f,z,s,s*R(.7f,1.4f),s*R(.78f,1.25f),false);
        }
        foreach(float z in new[]{141.2f,144.5f,147.8f,151.1f}){
            Solid(tip,"Retaining post",new(40.9f,1.4f,z),new(.46f,2.8f,.48f),steel);
        }
        for(int j=0;j<4;j++)Solid(tip,"Retaining sleeper",new(41.1f,.32f+j*.58f,146.1f),new(.39f,.54f,10.2f),wood);
        for(int i=0;i<5;i++)Solid(tip,"Front retaining post",new(41-i*2.7f,1.12f,141),new(.42f,2.24f,.46f),steel);
        for(int i=0;i<3;i++)Solid(tip,"Front retaining sleeper",new(35.5f,.32f+.57f*i,141.2f),new(11.2f,.53f,.38f),wood);
    }

    static void ClearCornerTrees(Transform yard){
        var corner=yard.Find("Southeast old growth");
        var stones=yard.GetComponentsInChildren<MeshRenderer>().Where(r=>r.bounds.center.x>16 && r.bounds.center.z<46 &&
            (r.transform.parent==corner || r.transform.parent.name=="Quarry faces and talus") && !r.name.StartsWith("Old growth")).Select(r=>r.bounds).ToArray();
        var reserved=new List<Bounds>();
        foreach(var trunk in corner.Cast<Transform>().Where(t=>t.name=="Old growth bole").ToArray()){
            int i=trunk.GetSiblingIndex();var pieces=new[]{trunk,corner.GetChild(i+1),corner.GetChild(i+2)};
            var original=trunk.localPosition;var tree=trunk.GetComponent<Renderer>().bounds;
            foreach(var crown in pieces.Skip(1))tree.Encapsulate(crown.GetComponent<Renderer>().bounds);
            Vector3 best=default;float least=float.PositiveInfinity;
            for(int z=7;z<=46;z++)for(int x=15;x<=45;x++){
                var point=new Vector3(x,GroundMeshHeight(x,z),z);var move=point-original;
                float eastBank=Mathf.Lerp(17,41,Mathf.Clamp01((z-19)/22f));
                float score=move.sqrMagnitude+(i<9 && x<eastBank?1500:0);
                if(score>=least || RoadDistance(Haul,x,z)<4.5f || RoadDistance(PitRoad,x,z)<3.7f || point.y<-.15f)continue;
                var proposed=tree;proposed.center+=move;proposed.Expand(.24f);
                if(proposed.max.x>48 || proposed.min.z<2 || reserved.Any(a=>Vector2.Distance(new(a.center.x,a.center.z),new(proposed.center.x,proposed.center.z))<4.4f))continue;
                if(pieces.Any(t=>{var part=t.GetComponent<Renderer>().bounds;part.center+=move;part.Expand(.22f);return stones.Any(r=>r.Intersects(part));}))continue;
                least=score;best=move;
            }
            if(float.IsInfinity(least))throw new InvalidOperationException("No clear ground for southeast fir "+i);
            foreach(var item in pieces)item.localPosition+=best;tree.center+=best;reserved.Add(tree);
        }
    }
    static float BasinRadius(float x,float z){
        return Mathf.Sqrt(Mathf.Pow((x+20.6f)/8.4f,2)+Mathf.Pow((z-134)/9.2f,2));
    }
    static float PumpGrade(float x,float z,float ground){
        float edge=Mathf.Max(Mathf.Abs(x+23)-13.8f,Mathf.Abs(z-133.8f)-12.1f);
        float bank=1.55f-.72f*(1-Mathf.SmoothStep(0,1,(BasinRadius(x,z)-.75f)/.29f));
        return Mathf.Lerp(bank,ground,Mathf.SmoothStep(0,1,edge/4f));
    }
    static void PumpStation(Transform parent){
        var p=Group(parent,"Northwest dewatering station");
        var orange=Finish("Pump vermilion",new(.80f,.29f,.085f));var iron=Finish("Pump cast iron",new(.21f,.22f,.235f));
        var hose=Finish("Hose rubber",new(.09f,.115f,.14f));var brass=Finish("Hose clamps",new(.58f,.39f,.21f));
        var water=Finish("Pump basin water",new(.15f,.265f,.265f));water.SetFloat("_Smoothness",.88f);
        var basin=MeshGo(p,"Settling basin water",RingMesh("Pump basin surface",new[]{1f,1f},new[]{-.025f,0},48,0),water,false);
        basin.transform.localPosition=new(-20.6f,1.28f,134);basin.transform.localScale=new(9.6f,1,10.6f);
        var hut=Group(p,"Timber pump house");hut.localPosition=new(-32.7f,Height(-32.7f,140),140);
        Solid(hut,"Stone sill",new(0,.18f,0),new(6.65f,.36f,5.3f),rock);
        Solid(hut,"Rear siding",new(0,1.94f,2.35f),new(6.28f,3.28f,.18f),wood);
        foreach(float x in new[]{-3.05f,3.05f})Solid(hut,"Side siding",new(x,1.94f,0),new(.18f,3.28f,4.71f),wood);
        void Front(float left,float right,float bottom,float top)=>Solid(hut,"Timber siding",new((left+right)/2,(bottom+top)/2,-2.36f),new(right-left,top-bottom,.20f),wood);
        Front(-3.14f,-2.14f,.30f,3.62f);Front(-.1f,.82f,.30f,3.62f);Front(2.51f,3.14f,.30f,3.62f);
        Front(-2.14f,-.1f,.30f,1.34f);Front(-2.14f,-.1f,2.80f,3.62f);Front(.82f,2.51f,2.96f,3.62f);
        foreach(float side in new[]{-1f,1f}){
            Solid(hut,"Sloped tin roofing",new(side*1.64f,4.1f,0),new(3.62f,.21f,5.7f),steel).transform.localRotation=Quaternion.Euler(0,0,-side*17);
            for(int j=0;j<9;j++)Solid(hut,"Roof standing seam",new(side*1.64f,4.23f,-2.64f+j*.66f),new(3.66f,.075f,.074f),iron,false).transform.localRotation=Quaternion.Euler(0,0,-side*17);
        }
        Tube(hut,"Stovepipe",new(-1.35f,4.17f,1.16f),new(-1.35f,5.51f,1.16f),.23f,iron);
        Tube(hut,"Chimney cap",new(-1.35f,5.52f,1.16f),new(-1.35f,5.65f,1.16f),.34f,steel);
        var hinge=Group(hut,"Open door");hinge.localPosition=new(2.5f,.34f,-2.47f);hinge.localRotation=Quaternion.Euler(0,-79,0);
        Solid(hinge,"Door boards",new(-.81f,1.30f,0),new(1.62f,2.58f,.14f),wood);
        Solid(hinge,"Door brace",new(-.81f,1.31f,-.11f),new(.14f,2.26f,.08f),steel,false).transform.localRotation=Quaternion.Euler(0,0,-30);
        foreach(float y in new[]{1.35f,2.79f})Solid(hut,"Window casing",new(-1.12f,y,-2.50f),new(2.17f,.11f,.22f),wood);
        foreach(float x in new[]{-2.14f,-.10f})Solid(hut,"Window jamb",new(x,2.07f,-2.50f),new(.12f,1.44f,.21f),wood);
        Solid(hut,"Window mullion",new(-1.12f,2.07f,-2.48f),new(.08f,1.38f,.11f),wood,false);
        Solid(hut,"Window transom",new(-1.12f,2.07f,-2.48f),new(1.97f,.08f,.12f),wood,false);
        var motor=Group(hut,"Auxiliary pump and manifold");motor.localPosition=new(-1.04f,.36f,.22f);
        Solid(motor,"Motor bed",new(0,.19f,0),new(2.44f,.38f,1.04f),steel);
        Tube(motor,"Electric motor",new(-1,.84f,0),new(.24f,.84f,0),.48f,orange,true);
        for(int i=0;i<5;i++)Tube(motor,"Cooling fins",new(-.94f+.20f*i,.84f,0),new(-.86f+.20f*i,.84f,0),.53f,iron);
        Tube(motor,"Pump impeller",new(.18f,.83f,0),new(.95f,.83f,0),.54f,orange,true);
        Tube(motor,"Delivery riser",new(.71f,1.05f,0),new(.71f,2.15f,0),.15f,steel);
        Tube(motor,"Overhead pipe",new(.71f,2.12f,0),new(.71f,2.12f,2.2f),.15f,steel);
        Solid(hut,"Switch cabinet",new(-.8f,2.21f,2.16f),new(1.41f,1.27f,.29f),steel);
        Solid(hut,"Lamp backplate",new(-.4f,3.18f,2.14f),new(.55f,.25f,.38f),iron);
        Lamp(hut,"Pump room lamp",new(-.4f,3.05f,2.01f),true,52);
        Solid(hut,"Door lamp bracket",new(1.66f,3.32f,-2.81f),new(.13f,.16f,.75f),iron);
        Lamp(hut,"Pump house porch lamp",new(1.66f,3.21f,-3.14f),true,43);
        var skid=Group(p,"Diesel dewatering pump");skid.localPosition=new(-21,1.0f,135);
        foreach(float x in new[]{-1.44f,1.44f}){
            Solid(skid,"Skid runner",new(x,.17f,0),new(.28f,.48f,5.6f),orange);
            foreach(float z in new[]{-2.48f,2.48f})Solid(skid,"Skid footing",new(x,-.03f,z),new(.71f,.41f,.73f),rock);
        }
        foreach(float z in new[]{-2.20f,0,2.20f})Solid(skid,"Channel crossmember",new(0,.44f,z),new(3.02f,.21f,.32f),iron);
        Tube(skid,"Drive coupling",new(0,1.49f,-.23f),new(0,1.49f,.25f),.53f,iron);
        Tube(skid,"Bell housing",new(0,1.49f,-.03f),new(0,1.49f,.18f),.68f,orange);
        Solid(skid,"Engine mounting bed",new(-.1f,.55f,1.27f),new(1.75f,.28f,2.48f),iron);
        Solid(skid,"Diesel engine",new(-.1f,1.50f,1.27f),new(2.03f,1.85f,2.26f),orange);
        Solid(skid,"Radiator",new(-.1f,1.54f,2.44f),new(1.71f,1.43f,.11f),iron);
        for(int i=0;i<8;i++)Solid(skid,"Radiator slat",new(-.1f,.97f+i*.16f,2.52f),new(1.52f,.058f,.065f),steel,false);
        Solid(skid,"Raised rocker cover",new(.09f,2.55f,1.30f),new(1.19f,.28f,1.36f),iron);
        Tube(skid,"Filler cap",new(.13f,2.65f,1.17f),new(.13f,2.80f,1.17f),.19f,brass);
        foreach(float side in new[]{-1f,1f}){
            float x=-.1f+side*1.04f;
            Solid(skid,"Side cooling grille",new(x,1.61f,1.30f),new(.11f,1.28f,1.71f),iron,false);
            for(int i=0;i<6;i++)Solid(skid,"Side engine louvre",new(x+side*.085f,1.13f+i*.18f,1.30f),new(.10f,.073f,1.45f),steel,false);
        }
        Tube(skid,"Exhaust silencer",new(-.74f,2.56f,1.68f),new(-.74f,3.11f,1.68f),.24f,iron);
        Tube(skid,"Intake air cleaner",new(.74f,2.42f,1.96f),new(.74f,2.78f,1.96f),.28f,orange);
        foreach(float z in new[]{-.48f,-1.48f})Tube(skid,"Casing rib",new(0,1.49f,z-.068f),new(0,1.49f,z+.068f),1.105f,orange);
        Tube(skid,"Pressure gauge stem",new(.69f,2.16f,-1.23f),new(.69f,2.75f,-1.23f),.085f,brass);
        Tube(skid,"Pressure gauge",new(.69f,2.78f,-1.19f),new(.69f,2.78f,-1.43f),.245f,iron);
        Tube(skid,"Gauge dial",new(.69f,2.78f,-1.435f),new(.69f,2.78f,-1.46f),.185f,gravel);
        Beam(skid,"Gauge needle",new(.69f,2.78f,-1.48f),new(.78f,2.88f,-1.48f),.025f,iron);
        Tube(skid,"Exhaust",new(-.74f,2.37f,1.68f),new(-.74f,3.33f,1.68f),.15f,iron);
        Tube(skid,"Centrifugal pump casing",new(0,1.49f,-.18f),new(0,1.49f,-1.84f),1.06f,orange,true);
        Tube(skid,"Suction flange",new(0,1.49f,-1.81f),new(0,1.49f,-2.12f),.79f,iron);
        for(int i=0;i<10;i++){float a=i*Mathf.PI/5;float x=.67f*Mathf.Cos(a),y=1.49f+.67f*Mathf.Sin(a);
            Tube(skid,"Flange bolt",new(x,y,-2.10f),new(x,y,-2.19f),.073f,brass);}
        Tube(skid,"Priming neck",new(0,2.39f,-.89f),new(0,2.87f,-.89f),.26f,iron);
        var path=new List<Vector3>{new(-21,2.49f,132.86f),new(-21,2.45f,131.75f),new(-20.7f,1.97f,130.55f)};
        foreach(var q in new[]{new Vector2(-19.8f,129.4f),new(-18.8f,128.3f),new(-18.0f,126.7f),new(-18,124.4f),new(-19.2f,122.6f),new(-21.5f,120.9f),new(-23.6f,119.5f),new(-25,117.3f),new(-24.5f,115.6f)})
            path.Add(new(q.x,Height(q.x,q.y)+.30f,q.y));
        for(int i=1;i<path.Count;i++){
            var a=path[i-1];var b=path[i];var dir=(b-a).normalized;
            Tube(p,"Discharge hose",a-dir*.055f,b+dir*.055f,.32f,hose,true);
            int n=Mathf.CeilToInt((b-a).magnitude/.29f);
            for(int j=0;j<n;j++){var c=Vector3.Lerp(a,b,(j+.35f)/n);Tube(p,j==0?"Hose joint":"Hose rib",c-dir*.052f,c+dir*.052f,j==0?.37f:.345f,j==0?brass:iron);}
        }
        var ripples=new Surface();
        foreach(var origin in new[]{new Vector2(-20.7f,130.9f),new Vector2(-23.6f,137.5f)}){
            for(int band=0;band<3;band++){
                float radius=.70f+.47f*band;
                Vector3 At(float a,float r)=>new(origin.x+Mathf.Cos(a)*r,1.302f,origin.y+Mathf.Sin(a)*r*.78f);
                for(int j=0;j<12;j++){
                    float a=.3f+band*.45f+j*.24f,b=a+.24f;
                    ripples.Quad(At(a,radius),At(b,radius),At(b,radius+.055f),At(a,radius+.055f));
                }
            }
        }
        ripples.Build(p,"Disturbed basin water",Finish("Water ripple",new(.215f,.345f,.34f)),false);
        for(int i=0;i<26;i++){
            float a=i*2*Mathf.PI/26;float x=-20.6f+8.40f*Mathf.Cos(a),z=134+9.16f*Mathf.Sin(a);
            var stone=MeshGo(p,"Basin edge stone",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset"),rock);
            stone.transform.localPosition=new(x,Height(x,z)-.13f,z);stone.transform.localRotation=Quaternion.Euler(0,i*89,0);
            stone.transform.localScale=new(.56f+.17f*(i%3),.51f+.12f*(i%2),.73f);
        }
        for(int i=0;i<7;i++){
            float x=-37+i*3.6f;var foot=new Vector3(x,Height(x,147),147);
            Solid(p,"Timber fence post",foot+Vector3.up*.71f,new(.24f,1.52f,.24f),wood);
            if(i==0)continue;
            var previous=new Vector3(x-3.6f,Height(x-3.6f,147),147);
            foreach(float y in new[]{.47f,1.12f})Beam(p,"Timber fence rail",previous+Vector3.up*y,foot+Vector3.up*y,.15f,wood);
        }
        var pole=new Vector3(-13.2f,Height(-13.2f,131),131);
        Tube(p,"Pump bank lamppost",pole-Vector3.up*.14f,pole+Vector3.up*3.2f,.11f,iron,true);
        Beam(p,"Bank lamp arm",pole+Vector3.up*3.15f,pole+new Vector3(-.65f,3.15f,0),.13f,iron);
        Lamp(p,"Basin service lamp",pole+new Vector3(-.65f,3.04f,0),true,44);
        Solid(hut,"Supply crate",new(-2.75f,.55f,-3.32f),new(1.35f,1.1f,1.18f),wood);
        Tube(hut,"Oil drum",new(-3.76f,0,-1.65f),new(-3.76f,1.41f,-1.65f),.48f,iron,true);
    }
    static Vector3[] clearMarkers;
    static void MineDressing(Transform root){
        var rubble=Group(root,"Mine wall rubble and spalls");var mouths=Group(root,"Daylight portal rock");
        var forms=new[]{"Jointed granite buttress","Split slate slab","Angular rubble block"}.Select(n=>AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+n+".asset")).ToArray();
        var stone=new[]{"Blue grey granite","Violet shale","Dark bedding","Fresh split rock"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+n+".mat")).ToArray();
        var rng=new System.Random(11603);
        float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        bool Clear(Vector2 at,float radius){
            return clearMarkers.All(m=>m.y>=0 || Vector2.Distance(at,new(m.x,m.z))>radius+.72f);
        }
        void Place(Transform group,Vector2 at,float floor,float w,float h,float d,bool solid=true){
            var piece=MeshGo(group,"Broken rock",forms[rng.Next(forms.Length)],stone[rng.Next(stone.Length)],solid);
            piece.transform.localPosition=new(at.x,floor,at.y);piece.transform.localScale=new(w,h,d);
            piece.transform.localRotation=Quaternion.Euler(R(-9,9),R(0,360),R(-11,11));
        }
        void Scatter(Vector2[] run){
            for(int i=1;i<run.Length;i++){
                var a=run[i-1];var b=run[i];var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x);
                for(float s=1;s<(b-a).magnitude;s+=R(1.45f,3.05f)){
                    float sign=rng.Next(2)==0?-1:1;
                    var site=a+dir*s+side*sign*R(2.05f,2.71f);
                    if(Height(site.x,site.y)<MineRoof(site.x,site.y)+.2f || Chamber(site.x,site.y) || MineDistance(site.x,site.y)<-1.25f)continue;
                    if(site.x>6 && site.x<15 && site.y>73 && site.y<85)continue;
                    float size=R(.48f,1.16f);if(!Clear(site,size))continue;
                    Place(rubble,site,MineFloor(site.x,site.y)-.15f,size,R(.51f,1.64f),size*R(.7f,1.28f));
                    for(int j=0;j<4;j++){
                        var chip=site+dir*R(-1.1f,1.1f)-side*sign*R(.1f,.84f);float k=R(.12f,.39f);
                        if(Clear(chip,k) && MineDistance(chip.x,chip.y)<-.05f)Place(rubble,chip,MineFloor(chip.x,chip.y)-.025f,k,k*R(.38f,.92f),k*R(.71f,1.25f),k>.22f);
                    }
                }
            }
        }
        foreach(var run in Routes)Scatter(run);Scatter(new[]{new Vector2(-25,52),new Vector2(-25,109)});
        void Portal(Vector2 center,Vector2 travel,int index){
            var p=Group(mouths,"Mouth "+index);var side=new Vector2(-travel.y,travel.x);
            foreach(float sign in new[]{-1f,1f}){
                for(int attempt=0,i=0;attempt<80 && i<9;attempt++){
                    float w=i<3?R(.78f,1.23f):R(.28f,.69f);
                    var at=center+side*sign*(2.6f+w+R(-.1f,1.1f))+travel*R(-2.1f,3.2f);
                    float ground=Height(at.x,at.y);
                    if(!Clear(at,w)|| Mathf.Abs(at.x)<7 && ground>-1)continue;
                    bool blocksPath=Routes.Any(run=>Enumerable.Range(1,run.Length-1).Any(k=>Segment(at,run[k-1],run[k],out _)<1.35f+1.50f*w));
                    if(blocksPath || Segment(at,new(-25,42),new(-25,109),out _)<1.45f+1.45f*w)continue;
                    ground=GroundMeshHeight(at.x,at.y);
                    if(Mathf.Abs(ground-GroundMeshHeight(at.x+.65f*w,at.y))>1.2f || Mathf.Abs(ground-GroundMeshHeight(at.x,at.y+.65f*w))>1.2f)continue;
                    i++;
                    if(ground-MineRoof(at.x,at.y)<.3f && Tunnel(at.x,at.y))ground=Mathf.Min(ground,MineFloor(at.x,at.y));
                    Place(p,at,ground-w*.22f,w,w*R(.8f,1.65f),w*R(.78f,1.2f));
                }
            }
        }
        int count=0;
        void FindMouth(Vector2[] run){
            for(int i=1;i<run.Length;i++){
                var a=run[i-1];var b=run[i];int n=Mathf.CeilToInt(Vector2.Distance(a,b)*4);
                for(int j=1;j<=n;j++){
                    var previous=Vector2.Lerp(a,b,(j-1f)/n);var q=Vector2.Lerp(a,b,j/(float)n);
                    if(Height(previous.x,previous.y)>=MineRoof(previous.x,previous.y) && Height(q.x,q.y)<MineRoof(q.x,q.y)){
                        Portal(q,(b-a).normalized,++count);return;
                    }
                }
            }
            throw new InvalidOperationException("No daylight transition on mine approach");
        }
        FindMouth(new[]{new Vector2(-25,62),new Vector2(-25,45)});
        foreach(var path in Routes.Take(3))FindMouth(path);
    }
    static void RidgeTrees(Transform root){
        var grove=Group(root,"West ridge mixed conifers");var rng=new System.Random(229);
        var foliage=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Angular pine boughs.asset");
        RingMesh("Tapered fir trunk",new[]{1.24f,.86f,.40f},new[]{0f,.29f,1f},9,.08f);
        var shades=new[]{Finish("Fir blue green",new(.095f,.22f,.20f)),Finish("Fir deep moss",new(.12f,.23f,.14f)),Finish("Fir tips",new(.19f,.31f,.215f))};
        for(int i=0;i<46;i++){
            float z=26+(i%23)*4.85f+(float)rng.NextDouble()*1.75f;
            float x=i%3==0?-44.5f:-(28f+(float)rng.NextDouble()*16);
            float h=i%7==0?14.4f+(float)rng.NextDouble()*4:i%5==0?7.3f:3.9f+(float)rng.NextDouble()*6.6f;
            // Keep the arrival sightline to the winding gear open, with the tall tree on the south ridge.
            if(x>-36 && h>11 && z>90 && z<104){x=-43;z-=40;}
            float radius=i%5==0?1.13f:i%7==0?.81f:.27f+(float)rng.NextDouble()*.25f;
            if(HoistSite(x,z,h*.22f) || RoadDistance(Climb,x,z)<h*.24f+2.65f || (z>118 && x>-38))continue;
            float y=Height(x,z);var p=Group(grove,i%5==0?"Thick trunk fir":h>13?"Tall ridge fir":"Young ridge fir");p.localPosition=new(x,GroundMeshHeight(x,z)-.18f,z);
            var bole=MeshGo(p,"Trunk",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Tapered fir trunk.asset"),wood);
            bole.transform.localPosition=new(0,-.85f,0);bole.transform.localScale=new(radius,h*.81f,radius);
            for(int j=0;j<3;j++){
                var c=MeshGo(p,"Layered crown",foliage,shades[(i+j)%3],false);
                c.transform.localPosition=new(0,h*(.15f+j*.20f),0);float spread=i%5==0?1.43f:1f;
                c.transform.localScale=new(spread*h*(.26f-.043f*j),h*(.52f-.035f*j),spread*h*(.24f-.04f*j));
                c.transform.localRotation=Quaternion.Euler(0,rng.Next(360),0);}
        }
    }
    static void Outer(Transform root)
    {
        var p=Group(root,"Surface machinery and gravel");
        Crane(p);
        var crane=p.Find("Crane");
        crane.localRotation=Quaternion.Euler(0,90,0);
        crane.localScale=Vector3.one*.72f;
        crane.localPosition=new Vector3(36.1f,1.5f,75)-crane.localRotation*(new Vector3(30,0,137)*.72f);
        RoadFence(p);
        Loader(p);
        Outcrops(p);ClearCornerTrees(p);HeadframeYard(p);HaulTurnout(p);RidgeTrees(p);
        var gauge=Group(p,"Pit depth staff");gauge.localPosition=new(29,Height(29,80),80);
        for(int i=0;i<12;i++)Solid(gauge,"Half metre band",new(0,i*.5f+.25f,0),new(.43f,.5f,.22f),i%2==0?yellow:steel);
        foreach(var point in new[]{new Vector3(-15,0,19),Climb[3],Climb[9],Climb[Climb.Length-1],new Vector3(47,0,50),new Vector3(-27.5f,0,124),new Vector3(11,0,130)})
        {
            var lamp=Group(p,"Quarry lamp standard");lamp.localPosition=point+new Vector3(-2.35f,0,0);
            float ground=GroundMeshHeight(lamp.localPosition.x,lamp.localPosition.z);
            lamp.localPosition=new(lamp.localPosition.x,ground,lamp.localPosition.z);
            Solid(lamp,"Steel post",new(0,1.45f,0),new(.16f,2.9f,.16f),steel);
            Solid(lamp,"Bracket",new(.32f,2.85f,0),new(.78f,.12f,.15f),steel);
            Lamp(lamp,"Amber work lamp",new(.64f,2.73f,0),true,38);
        }
    }

    static void Guard()
    {
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            throw new InvalidOperationException("Quarry prefab missing from this project");
        if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish Play Mode or the active bake first");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene edits first");
    }

    [MenuItem("Duck Rails/Authoring/Quarry Mine/Build mountain quarry")]
    public static void Install()
    {
        Guard();
        boxes.Clear();
        // The point source lights the space. The small visible lens supplies no second baked emitter.
        foreach(var lens in new[]{Finish("Amber service glass",new(.92f,.60f,.22f),.65f),Finish("Pale service glass",new(.66f,.75f,.85f),.62f)}){
            lens.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(lens);
        }
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");
        if(!cube)throw new InvalidOperationException("Missing Quarry blockout cube mesh");
        rock=Finish("Quarried slate",new(.32f,.35f,.44f));

        var grain=AggregateTexture();
        fines=Finish("Quarry floor fines",new(.53f,.52f,.54f));fines.SetTexture("_BaseMap",grain);
        gravel=Finish("Rolled haul gravel",new(.70f,.66f,.59f));gravel.SetTexture("_BaseMap",grain);
        rock.SetTexture("_BaseMap",grain);railPaint=Finish("Safety rail yellow",new(.87f,.52f,.065f));

        wood=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Tunnel timber.mat");
        steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Machinery steel.mat");
        yellow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Faded machinery yellow.mat");
        glass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Cab glass.mat");
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var block=prefab.GetComponent<RailBlock>();
            var root=RailBlock.FindEnvironment(block.transform,RootName) ?? RailBlock.FindEnvironment(block.transform,"Twin mine authored v5");
            if(!root)throw new InvalidOperationException("Expected the existing authored Quarry");
            root.name=RootName;
            clearMarkers=block.spawnCandidates.coal.Concat(block.spawnCandidates.gold).Concat(block.spawnCandidates.enemies).Select(t=>block.transform.InverseTransformPoint(t.position)).ToArray();
            if(root.Find("Southwest portal frame")){
                RemoveNamed(root,"Excavated terrain");
                Terrain(root);
                OfficeBoard(root.Find("Underground office"));
                var supports=root.Find("Tunnel timber supports");
                RemoveNamed(supports,"Caged mine lantern","Camping lantern");
                CaveLanterns(root,supports);
            }else{
                if(root.Find("Surface machinery and gravel/Northwest dewatering station")){
                    // Artists edit the saved block directly. Rebuild only the changed terrain and workings.
                    RefreshWorkings(root);
                }else{
                    foreach(Transform child in root.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                    Terrain(root);Switchbacks(root);Interior(root);Outer(root);MineDressing(root);SummitHoist(root);MinePuddles(root);
                }
                QuarryFinishing(root);
            }
            TuneGate(root);QuarryAcoustics(block);
            var lighting=prefab.GetComponent<RailBakedBlockLighting>();
            lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();
            lighting.CaptureBakedFixtures();
            PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath,out bool saved);
            if(!saved)throw new IOException("Could not save Quarry prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene=EditorSceneManager.OpenScene(BakeScene);
        var quarry=Object.FindFirstObjectByType<RailBlock>();
        if(!quarry || quarry.id!=Id)throw new InvalidOperationException("Wrong block in Quarry scene");
        var baker=quarry.GetComponentInChildren<CrawlGraphBaker>();
        Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new InvalidOperationException("Quarry graph bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        PrefabUtility.ApplyPrefabInstance(quarry.gameObject,InteractionMode.AutomatedAction);
        var probes=Object.FindFirstObjectByType<LightProbeGroup>();
        var points=new List<Vector3>();
        for(int z=4;z<160;z+=6)for(int x=-44;x<=44;x+=6)
        {
            float floor=Height(x,z);
            foreach(float offset in new[]{.65f,2.6f,5f})points.Add(new Vector3(x,floor+offset,z));
            if(Tunnel(x,z)&&floor>-3)points.Add(new Vector3(x,-4.7f,z));
        }
        foreach(var route in Routes)foreach(var corner in route)points.Add(new Vector3(corner.x,-4.6f,corner.y));
        points.Add(new Vector3(11,-4.6f,81));
        probes.probePositions=points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }

}
#endif
