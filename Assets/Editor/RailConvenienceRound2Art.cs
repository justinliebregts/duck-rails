#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    struct ClipVertex2
    {public Vector3 p,n;public Vector2 u,v;public static ClipVertex2 Lerp(ClipVertex2 a,ClipVertex2 b,float t)=>new(){p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,u=Vector2.Lerp(a.u,b.u,t),v=Vector2.Lerp(a.v,b.v,t)};}
    static Mesh CutBox2(Mesh src,Transform source,Transform space,Bounds box,string name)
    {
        var points=src.vertices;var normals=src.normals;var uv=src.uv;var uv2=src.uv2;var dst=new List<ClipVertex2>();var sub=new List<int[]>();
        float Coord(ClipVertex2 v,int axis)=>space.InverseTransformPoint(source.TransformPoint(v.p))[axis];
        List<ClipVertex2> Clip(List<ClipVertex2> polygon,int axis,float value,bool positive)
        {
            var output=new List<ClipVertex2>();if(polygon.Count==0)return output;
            for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=(Coord(a,axis)-value)*(positive?1:-1),db=(Coord(b,axis)-value)*(positive?1:-1);if(da>=0)output.Add(a);if((da>=0)!=(db>=0))output.Add(ClipVertex2.Lerp(a,b,da/(da-db)));}return output;
        }
        for(int s=0;s<src.subMeshCount;s++)
        {
            var ids=new List<int>();var tris=src.GetTriangles(s);
            void Add(List<ClipVertex2> p){if(p.Count<3)return;int start=dst.Count;dst.AddRange(p);for(int i=1;i<p.Count-1;i++)ids.AddRange(new[]{start,start+i,start+i+1});}
            for(int k=0;k<tris.Length;k+=3)
            {
                var polygon=new List<ClipVertex2>();for(int j=0;j<3;j++){int i=tris[k+j];polygon.Add(new ClipVertex2{p=points[i],n=normals[i],u=uv.Length>i?uv[i]:Vector2.zero,v=uv2.Length>i?uv2[i]:Vector2.zero});}
                for(int axis=0;axis<3&&polygon.Count>0;axis++)foreach(bool low in new[]{true,false}){float cut=low?box.min[axis]:box.max[axis];Add(Clip(polygon,axis,cut,!low));polygon=Clip(polygon,axis,cut,low);}
            }
            sub.Add(ids.ToArray());
        }
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(dst.Select(v=>v.p).ToArray());mesh.SetNormals(dst.Select(v=>v.n).ToArray());mesh.SetUVs(0,dst.Select(v=>v.u).ToArray());mesh.SetUVs(1,dst.Select(v=>v.v).ToArray());mesh.subMeshCount=sub.Count;for(int i=0;i<sub.Count;i++)mesh.SetTriangles(sub[i],i);return Save2(name,mesh);
    }
    static Material Carpet2()
    {
        const int n=128;var tex=new Texture2D(n,n,TextureFormat.RGB24,true,false){name="Party carpet tile",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};var pixels=new Color[n*n];var rng=new System.Random(7312);
        for(int i=0;i<pixels.Length;i++){float noise=(float)rng.NextDouble()*.025f;pixels[i]=new Color(.045f+noise,.025f+noise,.09f+noise);}
        void Dot(int x,int y,Color c){x=(x%n+n)%n;y=(y%n+n)%n;pixels[y*n+x]=c;}
        var pink=new Color(.85f,.13f,.48f);var yellow=new Color(.8f,.82f,.12f);
        for(int k=0;k<28;k++){int x=rng.Next(n),y=rng.Next(n);var c=k%2==0?pink:yellow;for(int i=0;i<8;i++)for(int w=0;w<2;w++)Dot(x+i,y+(i<4?i:8-i)+w,c);}
        for(int k=0;k<12;k++){int x=rng.Next(n),y=rng.Next(n);for(int a=0;a<24;a++){float f=a*Mathf.PI/12;Dot(x+Mathf.RoundToInt(Mathf.Cos(f)*4),y+Mathf.RoundToInt(Mathf.Sin(f)*4),k%2==0?pink:yellow);}}
        tex.SetPixels(pixels);tex.Apply();AssetDatabase.CreateAsset(tex,R2Folder+"/Party carpet texture.asset");var mat=Mat2("Arcade confetti carpet",Color.white);mat.SetTexture("_BaseMap",tex);return mat;
    }
    static void ArcadeRound2()
    {
        var arcade=authored.Find("Arcade");var g=Group(arcade,"Round two party interior");var carpet=Carpet2();
        Detail(g,"Party carpet main room",new Vector3(-25.95f,.035f,76),new Vector3(13.7f,.025f,19.7f),carpet,false);
        Detail(g,"Party carpet back room",new Vector3(-35,.035f,76),new Vector3(3.7f,.025f,19.7f),carpet,false);
        var wall=Mat2("Arcade midnight violet",new Color(.1f,.045f,.16f));
        foreach(float z in new[]{66.13f,85.87f})
        {
            Detail(g,"Party wall lining",new Vector3(-28,1.85f,z),new Vector3(17.7f,3.7f,.035f),wall,false);
            foreach(float y in new[]{.65f,2.9f})Detail(g,"Yellow pink wall neon",new Vector3(-28,y,z+(z<70?.03f:-.03f)),new Vector3(17.5f,.055f,.04f),y<1?pink2:yellow2,false);
            for(int i=0;i<10;i++){float x=-36+i*1.7f;Beam(g,"Party wall zigzag",new Vector3(x,1.3f,z+(z<70?.06f:-.06f)),new Vector3(x+.7f,1.8f,z+(z<70?.06f:-.06f)),.045f,i%2==0?pink2:yellow2,false);}
        }
        foreach(var wallPart in arcade.Cast<Transform>().Where(t=>t.name=="Wall"&&Mathf.Abs(t.localPosition.x+33)<.1f))
        {
            var c=wallPart.GetComponent<BoxCollider>();if(!c)continue;float z=wallPart.localPosition.z,h=wallPart.localScale.y,d=wallPart.localScale.z,y=wallPart.localPosition.y;
            foreach(float x in new[]{-33.13f,-32.87f})Detail(g,"Party partition lining",new Vector3(x,y,z),new Vector3(.035f,h-.03f,d-.03f),wall,false);
        }
        foreach(var r in arcade.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Marquee"||r.name=="Screen")){if(r.sharedMaterial&&r.sharedMaterial.IsKeywordEnabled("_EMISSION"))r.sharedMaterial=r.name=="Marquee"?yellow2:pink2;}
        var source=arcade.Find("Arcade cabinet 0-0");var pivot=new Vector3(-31.45f,0,83.95f);var back=Group(g,"Rear room arcade cabinets");
        for(int i=0;i<8;i++)
        {
            var t=Object.Instantiate(source.gameObject,back).transform;t.name="Rear room cabinet "+(i+1);foreach(Transform c in t)c.localPosition-=pivot;t.localScale=new Vector3(.85f,.95f,.7f);
            t.localPosition=i<6?new Vector3(-33.85f,0,73.8f+i*1.85f):new Vector3(-36.3f,0,i==6?67.6f:75.4f);t.localRotation=Quaternion.Euler(0,i<6?90:-90,0);
            foreach(var r in t.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Marquee"||r.name=="Screen"))r.sharedMaterial=i%2==0?yellow2:pink2;
        }
        var existing=arcade.GetComponentsInChildren<Light>();foreach(var light in existing){light.color=light.name.Contains("rear")?new Color(1,.12f,.5f):new Color(1,.9f,.1f);}
        foreach(var spec in new[]{new Vector3(-35,3.5f,74),new Vector3(-35,3.5f,82)}){var l=Group(g,"Baked rear room party light").gameObject.AddComponent<Light>();l.transform.localPosition=spec;l.transform.localRotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=22;l.range=8;l.spotAngle=100;l.color=spec.z<80?new Color(1,.85f,.15f):new Color(1,.1f,.5f);}
    }
    static void AtlasRound2(RailBlock block)
    {
        var renderers=block.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponent<TMP_Text>()&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>m&&m.HasProperty("_EmissionColor")&&m.GetColor("_EmissionColor").maxColorComponent>0&&!m.GetTexture("_BaseMap")).OrderBy(m=>m.name).ToArray();
        int width=Mathf.NextPowerOfTwo(materials.Length*8);var baseTex=new Texture2D(width,8,TextureFormat.RGBA32,false,false){name="Neon palette base",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var emissionTex=new Texture2D(width,8,TextureFormat.RGBAHalf,false,true){name="Neon palette HDR emission",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        for(int x=0;x<width;x++)for(int y=0;y<8;y++){int i=Mathf.Min(materials.Length-1,x/8);baseTex.SetPixel(x,y,materials[i].GetColor("_BaseColor"));emissionTex.SetPixel(x,y,materials[i].GetColor("_EmissionColor"));}
        baseTex.Apply();emissionTex.Apply();AssetDatabase.CreateAsset(baseTex,R2Folder+"/Neon base atlas.asset");AssetDatabase.CreateAsset(emissionTex,R2Folder+"/Neon emission atlas.asset");
        var atlas=Mat2("Shared neon atlas",Color.white);atlas.SetTexture("_BaseMap",baseTex);atlas.EnableKeyword("_EMISSION");atlas.SetTexture("_EmissionMap",emissionTex);atlas.SetColor("_EmissionColor",Color.white);atlas.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        var palette=materials.Select((m,i)=>(m,i)).ToDictionary(x=>x.m,x=>new Vector2((x.i*8+4f)/width,.5f));int changed=0;
        foreach(var r in renderers)
        {
            var mats=r.sharedMaterials;if(!mats.Any(m=>palette.ContainsKey(m)))continue;var f=r.GetComponent<MeshFilter>();var src=f.sharedMesh;var vs=src.vertices;var ns=src.normals;var uv=src.uv;var uv2=src.uv2;var vertices=new List<Vector3>();var normals=new List<Vector3>();var u=new List<Vector2>();var v=new List<Vector2>();var sub=new List<int[]>();
            for(int s=0;s<src.subMeshCount;s++){var triangles=src.GetTriangles(s);int start=vertices.Count;var indices=new int[triangles.Length];for(int j=0;j<triangles.Length;j++){int idx=triangles[j];vertices.Add(vs[idx]);normals.Add(ns[idx]);u.Add(s<mats.Length&&palette.TryGetValue(mats[s],out var cell)?cell:uv.Length>idx?uv[idx]:Vector2.zero);v.Add(uv2.Length>idx?uv2[idx]:Vector2.zero);indices[j]=start+j;}sub.Add(indices);}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,u);mesh.SetUVs(1,v);mesh.subMeshCount=sub.Count;for(int i=0;i<sub.Count;i++)mesh.SetTriangles(sub[i],i);
            f.sharedMesh=Save2("Neon UV "+changed,mesh);r.sharedMaterials=mats.Select(m=>palette.ContainsKey(m)?atlas:m).ToArray();changed++;
        }
        File.WriteAllText(R2Evidence+"/neon-atlas.txt",$"{materials.Length} mesh emissive materials -> one shared URP material, {changed} renderers remapped.\nBase and HDR emission atlas, point filtered, padded eight-pixel cells, UV1 lightmaps retained.\nTMP CutePixel materials unchanged; other blocks and original source materials unchanged.\n"+string.Join("\n",materials.Select((m,i)=>i+": "+m.name+" emission "+m.GetColor("_EmissionColor"))));
    }
    static Transform MascotBase2(string name,Vector3 at,float yaw)
    {var p=Group(r2,name);p.localPosition=at;p.localRotation=Quaternion.Euler(0,yaw,0);Detail(p,"Rooftop model mounting plinth",new Vector3(0,.12f,0),new Vector3(3.4f,.24f,2.3f),metal);return p;}
    static void RooftopModels2()
    {
        var cheese=Mat2("Pizza cheese",new Color(.92f,.64f,.14f));var crust=Mat2("Pizza crust",new Color(.58f,.27f,.08f));var roast=Mat2("Roast duck glaze",new Color(.46f,.16f,.055f));var pinkPaint=Mat2("Quack Mart pink",new Color(.92f,.12f,.46f));
        var pizza=MascotBase2("Last Slice rooftop pizza",new Vector3(15,3.94f,121.9f),180);
        var verts=new[]{new Vector3(-1.8f,3.3f,.25f),new Vector3(1.8f,3.3f,.25f),new Vector3(0,.35f,.25f),new Vector3(-1.8f,3.3f,-.25f),new Vector3(1.8f,3.3f,-.25f),new Vector3(0,.35f,-.25f)};
        var mesh=new Mesh{vertices=verts,triangles=new[]{0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5}};mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);MeshModel2(pizza,"Giant cheese slice",Save2("Pizza wedge",mesh),cheese);
        var crustBar=Cylinder(pizza,"Thick pizza crust",new Vector3(0,3.3f,0),.24f,3.9f,crust,10,false);crustBar.localRotation=Quaternion.Euler(0,0,90);
        foreach(var p in new[]{new Vector2(-.85f,2.75f),new Vector2(.55f,2.65f),new Vector2(-.3f,1.9f),new Vector2(.22f,1.2f)}){var t=Cylinder(pizza,"Pepperoni",new Vector3(p.x,p.y,.3f),.28f,.08f,red2,10,false);t.localRotation=Quaternion.Euler(90,0,0);}
        var duck=MascotBase2("Duck Diner rooftop roast duck",new Vector3(25,3.94f,121.9f),180);Cylinder(duck,"Serving platter",new Vector3(0,.35f,0),1.65f,.16f,ivory2,14,false);Blob(duck,"Roast duck body",new Vector3(0,1.1f,0),new Vector3(2.5f,1.55f,2),roast);
        foreach(float x in new[]{-1.1f,1.1f}){Blob(duck,"Roasted wing",new Vector3(x,1.05f,0),new Vector3(.6f,1,.95f),roast);Blob(duck,"Drumstick",new Vector3(x*.7f,.8f,1),new Vector3(.7f,.75f,1.05f),roast);Blob(duck,"Drumstick bone",new Vector3(x*.7f,.85f,1.55f),new Vector3(.25f,.26f,.6f),ivory2,3,6);}
        var bowl=MascotBase2("Noodle Box rooftop ramen",new Vector3(35,3.94f,121.9f),180);var bowlMat=Mat2("Ramen bowl lacquer",new Color(.65f,.055f,.04f));
        var bv=new List<Vector3>();var bt=new List<int>();for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;bv.Add(new Vector3(Mathf.Cos(angle)*.85f,.3f,Mathf.Sin(angle)*.85f));bv.Add(new Vector3(Mathf.Cos(angle)*1.6f,1.55f,Mathf.Sin(angle)*1.6f));}for(int i=0;i<16;i++){int a=i*2,b=(i*2+2)%32;bt.AddRange(new[]{a,a+1,b+1,a,b+1,b});}var bm=new Mesh();bm.SetVertices(bv);bm.SetTriangles(bt,0);bm.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(bm);MeshModel2(bowl,"Large ramen bowl",Save2("Ramen bowl",bm),bowlMat);
        Ring(bowl,"Bowl rim",new Vector3(0,1.55f,0),1.6f,.1f,ivory2);Cylinder(bowl,"Broth",new Vector3(0,1.45f,0),1.5f,.08f,crust,16,false);
        for(int j=0;j<7;j++)for(int i=0;i<7;i++){float x=-1.1f+i*.3f,z=-.9f+j*.28f;Beam(bowl,"Curly ramen noodle",new Vector3(x,1.52f,z+Mathf.Sin(i*1.7f+j)*.07f),new Vector3(x+.28f,1.52f,z+Mathf.Sin((i+1)*1.7f+j)*.07f),.065f,cheese,false);}
        Blob(bowl,"Ramen egg white",new Vector3(-.7f,1.62f,.35f),new Vector3(.85f,.2f,1),ivory2,3,8);Blob(bowl,"Egg yolk",new Vector3(-.7f,1.76f,.35f),new Vector3(.43f,.12f,.5f),cheese,3,8);
        foreach(float offset in new[]{-.16f,.16f})Beam(bowl,"Oversized chopstick",new Vector3(.25f+offset,1.4f,.1f),new Vector3(1.5f+offset,4,-.4f),.1f,wood,false);
        var q=MascotBase2("Quack Mart rooftop googly Q",new Vector3(24.2f,4.48f,74),-90);TorusQ2(q,pinkPaint);Detail(q,"Q tail",new Vector3(.9f,.8f,.1f),new Vector3(.4f,1.6f,.65f),pinkPaint,false,Quaternion.Euler(0,0,42));
        foreach(float x in new[]{-.55f,.55f}){Blob(q,"Googly eye",new Vector3(x,3.1f,.52f),new Vector3(.95f,1,.45f),ivory2,4,10);Blob(q,"Wobbly pupil",new Vector3(x+(x<0?.12f:-.08f),3.02f+(x<0?.08f:-.06f),.76f),new Vector3(.4f,.47f,.16f),black2,4,8);}
        var source=prior.Find("Scattered abandoned cars").Cast<Transform>().First(t=>!t.name.Contains("overturned"));var car=Object.Instantiate(source.gameObject,r2).transform;car.name="Car Wash rooftop car";car.localPosition=new Vector3(30,4.5f,45);car.localScale=Vector3.one*.7f;car.localRotation=Quaternion.Euler(0,-90,0);float low=car.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y);car.position+=Vector3.up*(4.45f-low);
        foreach(var c in car.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
    }
    static void MeshModel2(Transform p,string n,Mesh mesh,Material mat)
    {var t=Group(p,n);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.4f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);}
    static void TorusQ2(Transform p,Material mat)
    {
        var v=new List<Vector3>();var ts=new List<int>();int sides=16,tube=6;
        for(int i=0;i<sides;i++)for(int j=0;j<tube;j++){float a=i*Mathf.PI*2/sides,b=j*Mathf.PI*2/tube;v.Add(new Vector3(Mathf.Cos(a)*(1.2f+.32f*Mathf.Cos(b)),2+Mathf.Sin(a)*(1.4f+.32f*Mathf.Cos(b)),Mathf.Sin(b)*.35f));}
        // Outside-facing winding around the tube (angular direction, then tube direction).
        for(int i=0;i<sides;i++)for(int j=0;j<tube;j++){int a=i*tube+j,b=((i+1)%sides)*tube+j,c=i*tube+(j+1)%tube,d=((i+1)%sides)*tube+(j+1)%tube;ts.AddRange(new[]{a,b,c,b,d,c});}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(ts,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);MeshModel2(p,"Big pink Q ring",Save2("Googly Q torus",m),mat);
    }
}
#endif
