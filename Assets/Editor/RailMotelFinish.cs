#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Object=UnityEngine.Object;

// Second approved feedback batch; changes saved assets once, never replaces the motel wholesale.
public static class RailMotelFinish
{
    const string F="Assets/DuckRails/MotelLeisure/Finish";
    static Transform p;
    static Mesh cube;
    static Material concrete,wood,green,steel,white,blue,dark,yellow,orange,door,plaster,olive;
    static int serial;
    static Transform Group(Transform a,string name){var t=new GameObject(name).transform;t.SetParent(a,false);return t;}
    static Texture2D Crop(string name,Rect tile,bool gray=false)
    {
        string path=F+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved)return saved;
        var source=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Quackville/Quackville_Town_Diffuse.jpg");
        var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);Graphics.Blit(source,rt);var prev=RenderTexture.active;RenderTexture.active=rt;var read=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);read.ReadPixels(new Rect(0,0,source.width,source.height),0,0);read.Apply();RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);
        var tex=new Texture2D((int)tile.width,(int)tile.height,TextureFormat.RGBA32,true);tex.name=name;
        var colors=read.GetPixels((int)tile.x,source.height-(int)tile.yMax,(int)tile.width,(int)tile.height);
        if(gray){float avg=colors.Average(c=>c.grayscale);for(int i=0;i<colors.Length;i++){float v=Mathf.Clamp(.8f+(colors[i].grayscale-avg)*1.3f,.45f,1);colors[i]=new Color(v,v,v,1);}}
        tex.SetPixels(colors);tex.Apply();tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Repeat;tex.anisoLevel=0;AssetDatabase.CreateAsset(tex,path);Object.DestroyImmediate(read);return tex;
    }
    static Material Mat(string name,Color tint,Texture2D tex)
    {string path=F+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",tint);m.SetTexture("_BaseMap",tex);m.SetFloat("_Smoothness",.05f);return m;}
    static void Init()
    {
        Directory.CreateDirectory(F);AssetDatabase.Refresh();cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");
        var grain=Crop("Quackville neutral concrete pixels",new Rect(128,128,64,64),true);
        concrete=Mat("Continuous stair concrete",new Color(.48f,.5f,.48f),grain);white=Mat("White painted concrete",new Color(.85f,.86f,.81f),grain);blue=Mat("Blue painted trim",new Color(.12f,.28f,.55f),grain);
        wood=Mat("Weathered timber",new Color(.65f,.58f,.45f),Crop("Quackville timber",new Rect(192,64,64,64)));
        green=Mat("Putting felt",new Color(.22f,.43f,.25f),grain);steel=Mat("Dull textured steel",new Color(.35f,.4f,.4f),grain);dark=Mat("Dark pixel surfaces",new Color(.045f,.07f,.075f),grain);
        yellow=Mat("Duck yellow",new Color(.86f,.65f,.16f),grain);orange=Mat("Duck bill",new Color(.85f,.31f,.06f),grain);
        door=Mat("Dirty green doors",new Color(.32f,.42f,.29f),Crop("Quackville worn paint",new Rect(0,192,64,64)));
        plaster=Mat("Joined plaster",new Color(.78f,.76f,.68f),Crop("Quackville plaster",new Rect(0,64,64,64)));
        olive=Mat("Joined lower olive",new Color(.65f,.67f,.48f),Crop("Quackville olive",new Rect(0,192,64,64)));serial=0;
    }
    static Mesh Save(Mesh mesh,string name)
    {mesh.name=name;mesh.RecalculateBounds();if(mesh.uv2.Length!=mesh.vertexCount)Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,F+"/"+name+"-"+serial+++".asset");return mesh;}
    static void Project(Mesh mesh,Transform t)
    {
        var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];
        for(int i=0;i<v.Length;i++){var q=t.TransformPoint(v[i]);var normal=t.TransformDirection(n[i]);uv[i]=(Mathf.Abs(normal.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(normal.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3;}
        mesh.uv=uv;
    }
    static Transform Box(string name,Vector3 pos,Vector3 size,Material m,bool solid=true)
    {
        var t=Group(p,name);t.localPosition=pos;t.localScale=size;t.gameObject.layer=6;var mesh=Object.Instantiate(cube);Project(mesh,t);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=Save(mesh,name);var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.scaleInLightmap=.25f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);if(solid)t.gameObject.AddComponent<BoxCollider>();return t;
    }
    static void Beam(string name,Vector3 a,Vector3 b,float w,Material m)
    {var t=Box(name,(a+b)/2,new Vector3(w,w,(b-a).magnitude),m);t.localRotation=Quaternion.LookRotation(b-a);Project(t.GetComponent<MeshFilter>().sharedMesh,t);}
    static void Delete(Transform root,Func<Transform,bool> test){foreach(var t in root.Cast<Transform>().Where(test).ToArray())Object.DestroyImmediate(t.gameObject);}
    static void MeshObject(string name,Mesh mesh,Material[] mats,bool collision=false)
    {var t=Group(p,name);t.gameObject.layer=6;t.gameObject.AddComponent<MeshFilter>().sharedMesh=Save(mesh,name);var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.scaleInLightmap=.5f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);if(collision)t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;}
    // Exact union of axis-aligned wall volumes. Removes hidden/coplanar intersection faces.
    static void Walls(Transform floor,float baseY)
    {
        var boxes=floor.Cast<Transform>().Where(t=>t.name=="Revised wall"||t.name.StartsWith("Closed room solid")).Select(t=>t.GetComponent<BoxCollider>()).Where(c=>c).ToArray();
        var bounds=boxes.Select(c=>new Bounds(c.transform.localPosition,c.transform.localScale)).ToArray();
        float[][] axis=new float[3][];
        for(int d=0;d<3;d++){int dim=d;axis[d]=bounds.SelectMany(b=>new[]{Mathf.Round(b.min[dim]*10000)/10000,Mathf.Round(b.max[dim]*10000)/10000}).Concat(d==1?new[]{baseY+1.3f}:Array.Empty<float>()).Distinct().OrderBy(x=>x).ToArray();}
        int nx=axis[0].Length-1,ny=axis[1].Length-1,nz=axis[2].Length-1;var solid=new bool[nx,ny,nz];
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)for(int z=0;z<nz;z++){var q=new Vector3((axis[0][x]+axis[0][x+1])/2,(axis[1][y]+axis[1][y+1])/2,(axis[2][z]+axis[2][z+1])/2);solid[x,y,z]=bounds.Any(b=>b.Contains(q));}
        var vs=new List<Vector3>();var uv=new List<Vector2>();var tri=new[]{new List<int>(),new List<int>()};
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)for(int z=0;z<nz;z++)if(solid[x,y,z])
        {
            var lo=new Vector3(axis[0][x],axis[1][y],axis[2][z]);var hi=new Vector3(axis[0][x+1],axis[1][y+1],axis[2][z+1]);
            for(int d=0;d<3;d++)foreach(int sign in new[]{-1,1})
            {
                var idx=new[]{x,y,z};idx[d]+=sign;if(idx[0]>=0&&idx[0]<nx&&idx[1]>=0&&idx[1]<ny&&idx[2]>=0&&idx[2]<nz&&solid[idx[0],idx[1],idx[2]])continue;
                int u=(d+1)%3,v=(d+2)%3;var a=lo;var b=lo;var c=lo;var e=lo;a[d]=b[d]=c[d]=e[d]=sign<0?lo[d]:hi[d];b[u]=c[u]=hi[u];c[v]=e[v]=hi[v];
                int k=vs.Count;var corners=sign>0?new[]{a,b,c,e}:new[]{a,e,c,b};vs.AddRange(corners);uv.AddRange(corners.Select(q=>(d==0?new Vector2(q.z,q.y):d==1?new Vector2(q.x,q.z):new Vector2(q.x,q.y))/3));
                tri[(lo.y+hi.y)/2-baseY<1.3f?1:0].AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
        }
        var m=new Mesh();m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.SetVertices(vs);m.SetUVs(0,uv);m.subMeshCount=2;m.SetTriangles(tri[0],0);m.SetTriangles(tri[1],1);m.RecalculateNormals();p=floor;MeshObject("Joined wall shell",m,new[]{plaster,olive});
        foreach(var c in boxes){Object.DestroyImmediate(c.GetComponent<MeshRenderer>());Object.DestroyImmediate(c.GetComponent<MeshFilter>());}
    }
    static void BoardWindowsAndDoors(Transform motel)
    {
        foreach(var floor in motel.Cast<Transform>().Where(t=>t.name.StartsWith("Floor")))
        {
            p=floor;
            foreach(var t in floor.Cast<Transform>().Where(t=>t.name.StartsWith("Closed window recess")).ToArray())
            {var pos=t.localPosition;bool east=t.localScale.x<1;Object.DestroyImmediate(t.gameObject);for(int i=0;i<5;i++)Box("Window timber boarding",pos+(east?Vector3.left:Vector3.back)*.08f+Vector3.up*(-.6f+i*.3f),east?new Vector3(.14f,.28f,2.3f):new Vector3(2.3f,.28f,.14f),wood);}
            foreach(var t in floor.Cast<Transform>().Where(t=>t.name.StartsWith("Closed door")||t.name.StartsWith("Open door leaf")||t.name.StartsWith("Connecting internal door")).ToArray())
            {
                t.GetComponent<MeshRenderer>().sharedMaterial=door;
                bool thinX=t.localScale.x<.3f;var pos=t.localPosition;float extent=thinX?t.localScale.z:t.localScale.x;
                var hp=pos+Vector3.down*.15f+(thinX?new Vector3(-.1f,0,extent*.32f):new Vector3(extent*.32f,0,-.12f));
                Box("Door handle plate",hp,thinX?new Vector3(.06f,.26f,.12f):new Vector3(.12f,.26f,.06f),steel);
                Beam("Door handle",hp+(thinX?Vector3.left:Vector3.back)*.08f,hp+(thinX?Vector3.left:Vector3.back)*.08f+(thinX?Vector3.forward:Vector3.right)*.18f,.055f,steel);
            }
        }
    }
    static void Rooms(Transform motel)
    {
        foreach(int id in new[]{101,102,104,202,204,207})
        {
            float y=id>=200?3.6f:0;bool north=id==207;float z=north?0:54+(id%100-1)*8;var floor=motel.Find(id>=200?"Floor 2":"Floor 1");p=Group(floor,"Room furnishings "+id);
            Vector3 dress=north?new Vector3(24,y,106.8f):new Vector3(34.1f,y,z+4.4f);
            Box("Dresser cabinet",dress+Vector3.up*.6f,new Vector3(2.2f,1.2f,.65f),wood);
            for(int i=0;i<3;i++){Box("Drawer face",dress+new Vector3(0,.25f+i*.32f,-.35f),new Vector3(2,.28f,.07f),wood);Box("Drawer pull",dress+new Vector3(0,.25f+i*.32f,-.41f),new Vector3(.38f,.05f,.06f),steel);}
            Box("CRT television",dress+Vector3.up*1.7f,new Vector3(1.15f,.85f,.65f),dark);Box("CRT screen",dress+new Vector3(-.06f,1.73f,-.34f),new Vector3(.85f,.58f,.035f),blue);
            var bath=north?new Vector3(28,y,107):new Vector3(38,y,z+6);
            Box("Toilet pedestal",bath+new Vector3(0,.25f,0),new Vector3(.45f,.5f,.65f),white);Box("Toilet bowl",bath+new Vector3(0,.52f,-.1f),new Vector3(.7f,.22f,.9f),white);Box("Toilet seat opening",bath+new Vector3(0,.64f,-.17f),new Vector3(.42f,.025f,.5f),dark);Box("Toilet cistern",bath+new Vector3(0,.85f,.36f),new Vector3(.7f,.75f,.22f),white);
            var sink=bath+new Vector3(-1.1f,.85f,0);Box("Sink basin",sink,new Vector3(.75f,.2f,.55f),white);Box("Sink pedestal",sink-Vector3.up*.475f,new Vector3(.25f,.75f,.25f),white);Box("Sink well",sink+Vector3.up*.11f,new Vector3(.5f,.025f,.32f),dark);Beam("Sink tap",sink+new Vector3(0,.1f,.2f),sink+new Vector3(0,.35f,.2f),.05f,steel);
            if(!north){Box("Shower tray",new Vector3(39.1f,y+.08f,z+4.9f),new Vector3(1.1f,.16f,1.1f),white);Beam("Shower riser",new Vector3(39.65f,y+.2f,z+4.9f),new Vector3(39.65f,y+2.2f,z+4.9f),.05f,steel);Box("Shower head",new Vector3(39.45f,y+2.2f,z+4.9f),new Vector3(.45f,.08f,.25f),steel);}
            Delete(floor,t=>t.name=="Bathroom fixture "+id);
        }
    }
    static void FacadeLights(Transform motel)
    {
        Delete(motel,t=>t.name=="Balcony fixture"||t.name=="Walk fixture");
        foreach(var floor in motel.Cast<Transform>().Where(t=>t.name.StartsWith("Floor")))
        {
            Delete(floor,t=>t.name=="Broken room fixture"||t.name=="Room frontage light");p=floor;
            foreach(var t in floor.Cast<Transform>().Where(t=>t.name=="Working room fixture").ToArray())
            {var q=t.localPosition;bool east=q.z<94;var l=Group(p,"White fixture baked light");l.localPosition=q+(east?Vector3.left:Vector3.back)*.25f-Vector3.up*.15f;l.localRotation=Quaternion.LookRotation((east?Vector3.left:Vector3.back)*2-Vector3.up*3);var light=l.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.lightmapBakeType=LightmapBakeType.Baked;light.intensity=30;light.range=11;light.spotAngle=110;light.color=new Color(1,.9f,.76f);light.shadows=LightShadows.Soft;}
        }
    }
    static void Stairs(Transform stair)
    {
        var steps=stair.Cast<Transform>().Where(t=>t.name.StartsWith("Tread ")).OrderBy(t=>t.localPosition.z).ToArray();if(steps.Length==0)return;
        var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
        void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int k=verts.Count;verts.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;uv.AddRange(new[]{a,b,c,d}.Select(v=>(Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y):Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z):new Vector2(v.x,v.y))/3));tris.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
        float previous=0;
        foreach(var t in steps)
        {var lo=t.localPosition-t.localScale/2;var hi=t.localPosition+t.localScale/2;
            Q(new Vector3(lo.x,hi.y,lo.z),new Vector3(lo.x,hi.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(hi.x,hi.y,lo.z));
            Q(new Vector3(lo.x,previous,lo.z),new Vector3(lo.x,hi.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(hi.x,previous,lo.z));
            Q(new Vector3(lo.x,0,lo.z),new Vector3(lo.x,0,hi.z),new Vector3(lo.x,hi.y,hi.z),new Vector3(lo.x,hi.y,lo.z));
            Q(new Vector3(hi.x,0,hi.z),new Vector3(hi.x,0,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(hi.x,hi.y,hi.z));
            previous=hi.y;Object.DestroyImmediate(t.GetComponent<MeshRenderer>());Object.DestroyImmediate(t.GetComponent<MeshFilter>());
        }
        var last=steps.Last();var a0=last.localPosition-last.localScale/2;var a1=last.localPosition+last.localScale/2;
        Q(new Vector3(a0.x,0,a1.z),new Vector3(a1.x,0,a1.z),new Vector3(a1.x,a1.y,a1.z),new Vector3(a0.x,a1.y,a1.z));
        var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();StairLightmapUV(mesh);p=stair;MeshObject("Continuous concrete stair skin",mesh,new[]{concrete});
    }
    public static void StairLightmapUV(Mesh mesh)
    {
        // Continuous charts on each stair side, avoiding an independent lightmap chart per tread.
        mesh.RecalculateBounds();var bounds=mesh.bounds;var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];var n=normals[i];int chart;Vector2 q;
            if(Mathf.Abs(n.x)>.5f){chart=n.x<0?0:1;q=new Vector2((v.z-bounds.min.z)/bounds.size.z,(v.y-bounds.min.y)/bounds.size.y);}
            else if(Mathf.Abs(n.y)>.5f){chart=n.y>0?2:5;q=new Vector2((v.x-bounds.min.x)/bounds.size.x,(v.z-bounds.min.z)/bounds.size.z);}
            else{chart=n.z<0?3:4;q=new Vector2((v.x-bounds.min.x)/bounds.size.x,(v.y-bounds.min.y)/bounds.size.y);}
            uv[i]=new Vector2((chart%3+.03f+q.x*.94f)/3,(chart/3+.03f+q.y*.94f)/2);
        }
        mesh.uv2=uv;EditorUtility.SetDirty(mesh);
    }
    static void Diving(Transform pool)
    {
        Delete(pool,t=>t.name=="Diving stair"||t.name=="Diving stair handrail");
        foreach(var board in pool.Cast<Transform>().Where(t=>t.name.StartsWith("Diving board")).ToArray())
        {
            float x=board.localPosition.x,top=board.localPosition.y+board.localScale.y/2,edge=board.localPosition.z+board.localScale.z/2;int count=Mathf.CeilToInt(top/.2f);float rise=top/count;
            p=Group(pool,"Supported diving access "+top.ToString("F1"));var stair=p;
            for(int i=0;i<count;i++){float h=rise*(i+1);Box("Tread "+i,new Vector3(x,h/2,edge+(count-i-.5f)*.3f),new Vector3(1.5f,h,.3f),concrete);}
            foreach(float dx in new[]{-.68f,.68f})
            {
                for(int i=0;i<count;i+=2){float y=rise*(i+1),z=edge+(count-i-.5f)*.3f;float railY=Mathf.Lerp(top+1,rise+1,(z-edge)/((count-.5f)*.3f));Beam("Diving anchored post",new Vector3(x+dx,y,z),new Vector3(x+dx,railY,z),.075f,steel);}
                Beam("Diving top post",new Vector3(x+dx,top,edge),new Vector3(x+dx,top+1,edge),.075f,steel);
                Beam("Supported diving handrail",new Vector3(x+dx,rise+1,edge+(count-.5f)*.3f),new Vector3(x+dx,top+1,edge),.09f,steel);
            }
            // Last step directly meets the board edge at exactly the board's top height.
        }
    }
    static void Roof(Transform motel)
    {
        p=Group(motel,"Roof HVAC and pipes");
        foreach(var q in new[]{new Vector3(34,7.4f,63),new Vector3(34,7.4f,84),new Vector3(24,7.4f,103)})
        {Box("HVAC base",q+Vector3.up*.12f,new Vector3(3.4f,.24f,2.6f),dark);Box("HVAC cabinet",q+Vector3.up*.95f,new Vector3(3,1.5f,2.3f),steel);Box("Fan recess",q+Vector3.up*1.73f,new Vector3(1.6f,.05f,1.6f),dark);for(int i=0;i<8;i++)Box("HVAC louvre",q+new Vector3(0,.4f+i*.16f,-1.18f),new Vector3(2.6f,.06f,.07f),dark);Beam("Roof pipe",q+new Vector3(1,0,1.8f),q+new Vector3(1,1.2f,1.8f),.2f,steel);Beam("Pipe elbow",q+new Vector3(1,1.2f,1.8f),q+new Vector3(2,1.2f,1.8f),.2f,steel);}
        Beam("Long service pipe",new Vector3(38,7.65f,60),new Vector3(38,7.65f,90),.18f,steel);for(int z=61;z<91;z+=6)Box("Pipe saddle",new Vector3(38,7.52f,z),new Vector3(.5f,.24f,.2f),dark);
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static Mesh Extrude(Vector2[] polygon,float bottom,float top)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c){int k=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{a,b,c}.Select(q=>new Vector2(q.x,q.z)/3));tr.AddRange(new[]{k,k+1,k+2});}
        float area=0;for(int i=0;i<polygon.Length;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Length]);if(area<0)Array.Reverse(polygon);
        var ids=Enumerable.Range(0,polygon.Length).ToList();int safety=0;
        while(ids.Count>2&&safety++<200)
        {bool found=false;for(int j=0;j<ids.Count;j++){int ia=ids[(j+ids.Count-1)%ids.Count],ib=ids[j],ic=ids[(j+1)%ids.Count];var a=polygon[ia];var b=polygon[ib];var c=polygon[ic];if(Cross(b-a,c-b)<=.00001f)continue;
            bool contains=ids.Where(k=>k!=ia&&k!=ib&&k!=ic).Any(k=>Cross(b-a,polygon[k]-a)>=-.0001f&&Cross(c-b,polygon[k]-b)>=-.0001f&&Cross(a-c,polygon[k]-c)>=-.0001f);if(contains)continue;
            Tri(new Vector3(a.x,top,a.y),new Vector3(c.x,top,c.y),new Vector3(b.x,top,b.y));Tri(new Vector3(a.x,bottom,a.y),new Vector3(b.x,bottom,b.y),new Vector3(c.x,bottom,c.y));ids.RemoveAt(j);found=true;break;}if(!found)throw new Exception("Polygon triangulation failed");}
        for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];var lo=new Vector3(a.x,bottom,a.y);var hi=new Vector3(a.x,top,a.y);var bl=new Vector3(b.x,bottom,b.y);var bh=new Vector3(b.x,top,b.y);Tri(lo,hi,bh);Tri(lo,bh,bl);}
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(tr,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();return mesh;
    }
    static Vector2[] Poly(params float[] coords){var a=new Vector2[coords.Length/2];for(int i=0;i<a.Length;i++)a[i]=new Vector2(coords[i*2],coords[i*2+1]);return a;}
    static void Golf(Transform golf)
    {
        var shapes=new[]{
            Poly(-3,-4,-.5f,-4,-.5f,2,3,2,3,4.5f,-3,4.5f),
            Poly(-1.5f,-4.5f,1.5f,-4.5f,1.5f,2,4,2,4,4.5f,-4,4.5f,-4,2,-1.5f,2),
            Poly(-3,-4,3,-4,3,-1,.5f,-1,.5f,1,4,1,4,4.5f,-3,4.5f,-3,2.5f,-1.5f,2.5f,-1.5f,-2,-3,-2),
            Poly(-3,-4,3,-4,3,-2,-.5f,-2,-.5f,2,3,2,3,4,-3,4),
            Poly(-1.8f,-4.5f,1.8f,-4.5f,1.8f,4.5f,-1.8f,4.5f),
            Poly(-3,-4,3,-4,3,4.5f,-3,4.5f,-3,2,-.5f,2,-.5f,-1.5f,-3,-1.5f),
            Poly(-3,-4,-.5f,-4,-.5f,0,3,0,3,4.5f,-3,4.5f),
            Poly(-4,-4,2,-4,2,4.5f,-.5f,4.5f,-.5f,-1.5f,-4,-1.5f),
            Poly(-3,-4,3,-2,3,1,0,3,0,4.5f,-3,4.5f,-3,1.5f,.5f,-.5f,-3,-2)
        };
        string[] names={"L bend","T finish","Extended zigzag","C loop","Long straight","Side return","Hook finish","Offset dogleg","Diagonal chicane"};
        var holes=golf.Cast<Transform>().Where(t=>t.name.StartsWith("Golf hole")).ToArray();
        for(int i=0;i<holes.Length;i++)
        {
            var hole=holes[i];Delete(hole,t=>t.name=="Putting surface"||t.name=="Golf obstacle");hole.name="Golf hole "+(i+1)+" - "+names[i];p=hole;MeshObject("Unique putting outline",Extrude(shapes[i],0,.2f),new[]{green},true);
            var flag=hole.Find("Random flag");flag.localPosition=new Vector3(i==1?2.8f:i==2?2.5f:i==5?1.5f:i==8?-1.5f:0,.2f,3.7f);
            if(i==4)
            {
                p=Group(hole,"Shoot-through house");Box("House left wall",new Vector3(-.95f,.85f,0),new Vector3(.45f,1.3f,2),wood);Box("House right wall",new Vector3(.95f,.85f,0),new Vector3(.45f,1.3f,2),wood);Box("House lintel",new Vector3(0,1.5f,0),new Vector3(2.35f,.3f,2),white);
                foreach(float sign in new[]{-1f,1f}){var t=Box("Pitched house roof",new Vector3(sign*.65f,1.95f,0),new Vector3(1.5f,.12f,2.4f),blue);t.localRotation=Quaternion.Euler(0,0,-sign*28);}
            }
            if(i==6)
            {
                p=Group(hole,"Restored duck obstacle");Box("Duck body",new Vector3(0,.9f,2.2f),new Vector3(1.9f,1.15f,1.3f),yellow);Box("Duck head",new Vector3(.65f,1.65f,2.2f),new Vector3(.85f,.9f,.85f),yellow);Box("Duck bill",new Vector3(1.25f,1.55f,2.2f),new Vector3(.65f,.25f,.65f),orange);foreach(float z in new[]{1.75f,2.65f})Box("Duck eye",new Vector3(.8f,1.8f,z),new Vector3(.15f,.15f,.04f),dark);
            }
        }
        var shack=golf.Find("Moved north-facing golf shack");p=Group(shack,"Golf shack interior");Box("Golf counter",new Vector3(0,.75f,.65f),new Vector3(4,1.2f,.8f),wood);Box("Counter top",new Vector3(0,1.4f,.65f),new Vector3(4.2f,.14f,1),white);
        for(int i=0;i<5;i++){var a=new Vector3(-2.8f+i*.22f,.2f,1.9f);var b=a+new Vector3(.35f,1.65f,.15f);Beam("Leaning golf club shaft",a,b,.035f,steel);Box("Golf club head",a,new Vector3(.28f,.12f,.12f),steel);Beam("Golf club grip",b-Vector3.up*.25f,b,.055f,dark);}
    }
    static void Signs(Transform leisure)
    {
        foreach(var parent in new[]{leisure.Find("Mini golf/Moved north-facing golf shack"),leisure.Find("Roller rink")})
        {
            var label=parent.GetComponentsInChildren<TMP_Text>().First(t=>t.text=="GOLF"||t.text=="SKATE RENTAL");
            string text=label.text;var pos=label.transform.localPosition;var rot=label.transform.localEulerAngles.y;float width=text=="GOLF"?4.5f:9;Object.DestroyImmediate(label.gameObject);RailMotelRevision.AddMeshSign(parent,text,pos,width,.8f,rot,white);
        }
    }
    static void Rink(Transform rink)
    {
        Delete(rink,t=>t.name=="White rink wall"||t.name=="Blue rink top stripe"||t.name=="Faded skating stripe");p=rink;
        // Two U-shaped continuous extrusions form clean corners while retaining both entrance gaps.
        foreach(bool east in new[]{false,true})
        {
            var polygon=east?Poly(29,118.89f,42.11f,118.89f,42.11f,145.11f,29,145.11f,29,144.89f,41.89f,144.89f,41.89f,119.11f,29,119.11f):
                Poly(23,118.89f,23,119.11f,10.11f,119.11f,10.11f,144.89f,23,144.89f,23,145.11f,9.89f,145.11f,9.89f,118.89f);
            MeshObject("Joined white rink wall",Extrude((Vector2[])polygon.Clone(),0,.8f),new[]{white},true);
            MeshObject("Joined blue coping",Extrude((Vector2[])polygon.Clone(),.8f,.96f),new[]{blue},true);
        }
        // Continuous mitered rings; each quad shares the same inner/outer corner line.
        for(int k=0;k<3;k++)
        {
            float x0=10.7f+k*.8f,x1=41.3f-k*.8f,z0=119.7f+k*.8f,z1=144.3f-k*.8f,w=.65f;
            var outer=new[]{new Vector3(x0,.212f,z0),new Vector3(x0,.212f,z1),new Vector3(x1,.212f,z1),new Vector3(x1,.212f,z0)};
            var inner=new[]{new Vector3(x0+w,.212f,z0+w),new Vector3(x0+w,.212f,z1-w),new Vector3(x1-w,.212f,z1-w),new Vector3(x1-w,.212f,z0+w)};
            var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<4;i++){int j=(i+1)%4,n=v.Count;v.AddRange(new[]{outer[i],outer[j],inner[j],inner[i]});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,v.Select(q=>new Vector2(q.x,q.z)/3).ToList());mesh.RecalculateNormals();
            var mat=AssetDatabase.LoadAssetAtPath<Material>(k==0?"Assets/DuckRails/MotelLeisure/Faded coral.mat":k==1?"Assets/DuckRails/MotelLeisure/Old yellow fiberglass.mat":"Assets/DuckRails/MotelLeisure/Faded teal.mat");
            MeshObject("Mitered rink stripe "+k,mesh,new[]{mat});
        }
    }
    static void TextureAll(GameObject root)
    {
        var grain=Crop("Quackville neutral concrete pixels",new Rect(128,128,64,64),true);
        var mapping=new Dictionary<Material,Material>();
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponent<TMP_Text>()))
        {
            var mats=r.sharedMaterials;bool projected=false;
            for(int i=0;i<mats.Length;i++)
            {
                var m=mats[i];if(!m||m.mainTexture)continue;
                if(!mapping.TryGetValue(m,out var replacement))
                {replacement=new Material(m);replacement.name=m.name+" Quackville pixels";replacement.SetTexture("_BaseMap",grain);if(replacement.IsKeywordEnabled("_EMISSION"))replacement.SetTexture("_EmissionMap",grain);AssetDatabase.CreateAsset(replacement,F+"/"+replacement.name+".mat");mapping[m]=replacement;}
                mats[i]=replacement;projected=true;
            }
            r.sharedMaterials=mats;
            // New tinted/cropped-material meshes also need metre-sized texels, not stretched cube UVs.
            if(projected||mats.Any(m=>m&&AssetDatabase.GetAssetPath(m).StartsWith(F)))
            {
                var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var mesh=Object.Instantiate(f.sharedMesh);Project(mesh,f.transform);if(mesh.uv2.Length!=mesh.vertexCount)Unwrapping.GenerateSecondaryUVSet(mesh);f.sharedMesh=Save(mesh,"Pixel surface");
                var collider=f.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=f.sharedMesh;
            }
        }
    }
    public static void Install()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Save prefab edits first");StageUtility.GoToMainStage();Init();
        var go=PrefabUtility.LoadPrefabContents(RailMotelGreybox.PrefabPath);
        try
        {
            var motel=go.transform.Find("Env/Motel greybox v1/Motel");var leisure=go.transform.Find("Env/"+RailMotelLeisure.Root);if(motel.Find("Roof HVAC and pipes"))throw new Exception("Batch already installed");
            BoardWindowsAndDoors(motel);Rooms(motel);FacadeLights(motel);
            foreach(var floor in motel.Cast<Transform>().Where(t=>t.name.StartsWith("Floor")))Walls(floor,floor.name=="Floor 2"?3.6f:0);
            Stairs(motel.Find("Stair A"));Stairs(motel.Find("Stair B"));Diving(leisure.Find("Municipal pool"));Roof(motel);Golf(leisure.Find("Mini golf"));Signs(leisure);Rink(leisure.Find("Roller rink"));
            var cars=go.transform.Find("Env/Motel greybox v1/Exterior paths and props").Cast<Transform>().Where(t=>t.name=="Abandoned car").ToArray();
            var grey=Mat("Light grey car paint",new Color(.72f,.75f,.74f),Crop("Quackville neutral concrete pixels",new Rect(128,128,64,64),true));
            for(int i=0;i<cars.Length;i++)foreach(var r in cars[i].GetComponentsInChildren<MeshRenderer>().Where(r=>new[]{"Car chassis","Hood","Trunk","Car roof","Window pillar"}.Contains(r.name)))r.sharedMaterial=i==0?grey:blue;
            TextureAll(go);PrefabUtility.SaveAsPrefabAsset(go,RailMotelGreybox.PrefabPath);AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(go);}PrefabStageUtility.OpenPrefab(RailMotelGreybox.PrefabPath);
    }
}
#endif
