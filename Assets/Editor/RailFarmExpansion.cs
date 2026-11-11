#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class RailFarmSilos
{
    public static readonly Vector3 HouseCentre=new(-27,1.5f,134);
    public static Vector3 HousePoint(Vector3 p)=>HouseCentre+Quaternion.Euler(0,-90,0)*(p-new Vector3(24,0,137));
    public static readonly Rect[] CornAreas={new(-46,6,35,48),new(17,6,29,48),new(10,117,36,37)};
    public static readonly Vector2[][] CornPaths={
        new[]{new Vector2(-15,6),new Vector2(-21,16),new Vector2(-18,25),new Vector2(-30,33),new Vector2(-27,44),new Vector2(-20,54)},
        new[]{new Vector2(22,6),new Vector2(29,17),new Vector2(25,28),new Vector2(34,37),new Vector2(28,48),new Vector2(27,54)},
        new[]{new Vector2(13,117),new Vector2(23,125),new Vector2(32,132),new Vector2(24,143),new Vector2(33,154)}
    };
    static readonly Vector2[] OrchardTrees={new(-40,119),new(-31,118),new(-20,118),new(-11,124),new(-11,145),new(-20,151),new(-31,151),new(-41,151),new(-44,131)};
    public static float HeightV5(float x,float z)
    {
        float dx=Mathf.Max(-44-x,0,x+14),dz=Mathf.Max(120-z,0,z-149);
        float orchard=1.5f*(1-Mathf.SmoothStep(0,1,Mathf.Sqrt(dx*dx+dz*dz)/10));
        float seam=Mathf.Clamp01(Mathf.Min((x+48)/2,(48-x)/2,(z-4)/2,(156-z)/3));
        float ditch= -.8f*Mathf.Clamp01(1-Mathf.Abs(x-12)/3)*Mathf.Clamp01((z-8)/5)*Mathf.Clamp01((53-z)/5);
        // East-side fields have small rolling shoulders; railway and central yard remain flat.
        float rolling=x>20&&z<53?.35f*Mathf.Sin((x-20)*.12f)*Mathf.Sin(z*.08f)*Mathf.Clamp01((53-z)/8):0;
        return (orchard+ditch+rolling)*seam;
    }
    public static bool Excavation(float x,float z)=>(x> -36&&x< -18&&z>124&&z<144)||(x> -43&&x<= -36&&z>138&&z<144);
    static void RelocateHouse(Transform root)
    {
        var h=root.Find("Farmhouse");h.localRotation=Quaternion.Euler(0,-90,0);h.localPosition=HouseCentre-h.localRotation*new Vector3(24,0,137);
    }
    static void GroundV5(Transform root)
    {
        var p=Group(root,"Ground with basement excavation");var v=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();var tr=new List<int>();
        for(int z=0;z<160;z++)for(int x=-48;x<48;x++)
        {
            if(Excavation(x+.5f,z+.5f))continue;int k=v.Count;
            foreach(var q in new[]{new Vector2(x,z),new Vector2(x,z+1),new Vector2(x+1,z+1),new Vector2(x+1,z)}){v.Add(new Vector3(q.x,HeightV5(q.x,q.y),q.y));uv.Add(q/4);uv2.Add(new Vector2((q.x+48)/96,q.y/160));}
            tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
        }
        var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,uv2);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();MeshGo(p,"Sculpted orchard and ditch",Save("Ground-V5",m),grass,Vector3.zero,Vector3.one,true).GetComponent<MeshRenderer>().scaleInLightmap=.15f;
    }
    static float PathDistance(Vector2 q,Vector2[] path)
    {float d=float.MaxValue;for(int i=1;i<path.Length;i++){var a=path[i-1];var ab=path[i]-a;d=Mathf.Min(d,Vector2.Distance(q,a+ab*Mathf.Clamp01(Vector2.Dot(q-a,ab)/ab.sqrMagnitude)));}return d;}
    public static bool CropOpening(int field,Vector2 p)
    {
        if(PathDistance(p,CornPaths[field])<2.2f)return true;
        if(field==0&&Vector2.Distance(p,new Vector2(-27,44.5f))<4.8f)return true;
        if(field==1&&(Vector2.Distance(p,new Vector2(29,28))<3.3f||Vector2.Distance(p,new Vector2(20,50.5f))<4))return true;
        return false;
    }
    static void DirtRoute(Transform root,string name,Vector2[] path,float width)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();float dist=0;
        for(int i=1;i<path.Length;i++)
        {
            var a=path[i-1];var b=path[i];int steps=Mathf.CeilToInt(Vector2.Distance(a,b));var side=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;
            for(int j=0;j<steps;j++){var from=Vector2.Lerp(a,b,(float)j/steps);var to=Vector2.Lerp(a,b,(float)(j+1)/steps);int k=v.Count;foreach(var q in new[]{from-side,to-side,to+side,from+side}){v.Add(new Vector3(q.x,HeightV5(q.x,q.y)+.025f,q.y));uv.Add(q/3);}tri.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});dist++;}
        }
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);MeshGo(root,name,Save(name,m),dirt,Vector3.zero,Vector3.one);
    }
    [MenuItem("Duck Rails/Authoring/Farm Silos/Update corn only")]
    public static void UpdateCornOnly()
    {
        Guard(); meshes.Clear(); Materials();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BakeScene);
        var b=Object.FindFirstObjectByType<RailBlock>();
        var p=b.transform.Find("Env/"+RootName+"/Fields and orchard")??b.transform.Find(RootName+"/Fields and orchard");
        foreach(var t in p.Cast<Transform>().Where(t=>t.name.StartsWith("Corn field ")).ToArray())Object.DestroyImmediate(t.gameObject);
        var plant=CornPlant();for(int i=0;i<CornAreas.Length;i++)if(i!=2||!b.transform.Find("Env/"+RailFarmPaddock.RootName))DenseCorn(p,i,plant);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static Mesh CornPlant()
    {
        // Opaque double-sided leaf diamonds: broad enough to overlap adjacent plants at low eye height.
        var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool two=true){int k=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});tr.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});if(two)Face(d,c,b,a,false);}
        Face(new(-.055f,0,0),new(-.055f,4,0),new(.055f,4,0),new(.055f,0,0));Face(new(0,0,-.055f),new(0,4,-.055f),new(0,4,.055f),new(0,0,.055f));
        for(int i=0;i<5;i++)
        {
            float y=.4f+i*.7f,angle=i*2.4f;var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,dir)*.4f;var stem=Vector3.up*y;var mid=stem+dir*.58f+Vector3.up*.24f;var tip=stem+dir*1.05f-Vector3.up*.18f;
            Face(stem,mid-side,tip,mid+side);
        }
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,uv);m.SetTriangles(tr,0);m.RecalculateNormals();return Save("Dense-corn-plant",m);
    }
    static void DenseCorn(Transform root,int field,Mesh plant)
    {
        var p=Group(root,"Corn field "+field);Rect r=CornAreas[field];int count=0;
        // Small combined patches allow ordinary renderer frustum culling; no object per stalk.
        for(float x=r.xMin;x<r.xMax;x+=8)for(float z=r.yMin;z<r.yMax;z+=8)
        {
            var items=new List<CombineInstance>();
            for(float xx=x+.55f;xx<Mathf.Min(x+8,r.xMax)-.2f;xx+=.78f)for(float zz=z+.55f;zz<Mathf.Min(z+8,r.yMax)-.2f;zz+=.8f)
            {
                float px=xx+.12f*Mathf.Sin(zz*3+xx),pz=zz+.12f*Mathf.Cos(xx*4+zz);if(CropOpening(field,new Vector2(px,pz)))continue;
                float scale=.94f+.08f*Mathf.PerlinNoise(px,pz);items.Add(new CombineInstance{mesh=plant,transform=Matrix4x4.TRS(new Vector3(px,HeightV5(px,pz),pz),Quaternion.Euler(0,(px*41+pz*29)%360,0),new Vector3(1,scale,1))});count++;
            }
            if(items.Count==0)continue;var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(items.ToArray(),true,true);m=Save("Corn-V5-"+field+"-"+x+"-"+z,m);
            var g=MeshGo(p,"Dense corn patch",m,leaves,Vector3.zero,Vector3.one);var renderer=g.GetComponent<MeshRenderer>();GameObjectUtility.SetStaticEditorFlags(g,0);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveGI=ReceiveGI.LightProbes;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
        }
        Debug.Log("Farm corn field "+field+": "+count+" stalks in "+r.width*r.height+" square metres");
    }
    static void OuterV5(Transform root)
    {
        var p=Group(root,"Fields and orchard");var plant=CornPlant();for(int i=0;i<CornAreas.Length;i++)DenseCorn(p,i,plant);
        foreach(var pos in OrchardTrees){Tree(p,pos,true);var t=p.GetChild(p.childCount-1);t.localPosition=Vector3.up*HeightV5(pos.x,pos.y);}
        foreach(var pos in new[]{new Vector2(-43,60),new Vector2(-43,103),new Vector2(43,64),new Vector2(43,103)})Tree(p,pos,false);
        DirtRoute(p,"Farmyard link south",new[]{new Vector2(-25,68),new Vector2(-16,62),new Vector2(-8,62),new Vector2(5,62),new Vector2(15,65)},3);
        DirtRoute(p,"Farmyard link north",new[]{new Vector2(-25,101),new Vector2(-12,107),new Vector2(4,107),new Vector2(12,107),new Vector2(23,105)},3);
        DirtRoute(p,"Winding orchard drive",new[]{new Vector2(-12,107),new Vector2(-10,115),new Vector2(-12,122),new Vector2(-12,130),new Vector2(-14,134)},3);
        // No perimeter lanes. Field passages are gaps in vegetation, not a paved outline.
        Shed(p,"Cart shed",16,106,14,5,2.7f);HayStack(p,"Field hay pyramid",-18,112,8,6,2.1f,4);
        Fence(p,-32,106,-23,106);Fence(p,-32,106,-32,111);Fence(p,-32,111,-23,111);
        var wagon=Group(p,"Old hay wagon");Box(wagon,"Wagon bed",new Vector3(-27,.9f,44.5f),new Vector3(7,.3f,2.8f),wood);Box(wagon,"Hay load",new Vector3(-27,1.6f,44.5f),new Vector3(5,1.1f,2.2f),hay);
        foreach(float x in new[]{-29.5f,-24.5f})foreach(float z in new[]{43f,46f})Box(wagon,"Wheel",new Vector3(x,.5f,z),new Vector3(.8f,1,.3f),roof);
        var tractor=Group(p,"Abandoned tractor");float ground=HeightV5(20,50.5f);tractor.localPosition=Vector3.up*ground;Box(tractor,"Engine",new Vector3(19,1,50.5f),new Vector3(2.5f,1.2f,1.8f),red);Box(tractor,"Seat",new Vector3(21,1,50.5f),new Vector3(1,.4f,1),roof);foreach(float x in new[]{18f,21.5f})foreach(float z in new[]{49.4f,51.6f})Box(tractor,"Wheel",new Vector3(x,.65f,z),new Vector3(1.1f,1.3f,.5f),roof);
        Fence(p,16,54,18,54);Fence(p,23,54,29,54);var broken=Box(p,"Broken fence",new Vector3(22,.5f,52),new Vector3(3,.12f,.15f),wood);broken.transform.rotation=Quaternion.Euler(0,25,20);
    }
    static void CharacterV5(Transform root)
    {
        var p=Group(root,"Character details");
        // Shallow, recoverable ditch and walkable bridge beside, never over, the railway.
        Slab(p,"Ditch footbridge",8,39.5f,8,3,.15f,wood);foreach(float z in new[]{39.6f,42.4f}){foreach(float x in new[]{8.3f,15.7f})Box(p,"Bridge post",new Vector3(x,.75f,z),new Vector3(.16f,1.5f,.16f),wood);Box(p,"Bridge rail",new Vector3(12,1.2f,z),new Vector3(7.5f,.14f,.14f),wood);}
        // Retaining details are broken runs so no perimeter wall blocks the orchard approaches.
        foreach(var seg in new[]{new Vector2(-40,148),new Vector2(-30,148),new Vector2(-40,120)})Box(p,"Low orchard retaining stones",new Vector3(seg.x,1,seg.y),new Vector3(6,1,.5f),stone);
        var h=root.Find("Farmhouse");Box(h,"Apple crates",new Vector3(19,.5f,125),new Vector3(2,1,1),wood);for(int i=0;i<5;i++)MeshGo(h,"Crate apples",octa,apple,new Vector3(18.4f+i*.3f,1.05f,125),Vector3.one*.14f);
        // Open loft window above the existing barn doors, with reachable interior landing.
        var barn=root.Find("Barn and silos");foreach(var t in barn.Cast<Transform>().Where(t=>t.name=="Barn south"&&Mathf.Abs(t.localPosition.x-25)<.01f&&t.localPosition.y>3).ToArray())Object.DestroyImmediate(t.gameObject);
        Box(barn,"Loft sill",new Vector3(25,2.85f,66),new Vector3(4,.4f,.22f),red);Slab(barn,"Loft landing",22,66,6,4,3.05f,wood);
        Box(barn,"Loading hoist beam",new Vector3(25,5.5f,64.6f),new Vector3(.25f,.3f,3.4f),wood);Box(barn,"Hoist rope",new Vector3(25,4.1f,63.3f),new Vector3(.055f,2.6f,.055f),wood,false);Box(barn,"Hoist hook",new Vector3(25,2.9f,63.3f),new Vector3(.35f,.35f,.12f),steel,false);
        var scare=Group(p,"Scarecrow clearing");scare.localPosition=new Vector3(29,HeightV5(29,28),28);Box(scare,"Post",new Vector3(0,1.1f,0),new Vector3(.15f,2.2f,.15f),wood);Box(scare,"Shirt",new Vector3(0,1.65f,0),new Vector3(.6f,.8f,.3f),red);Box(scare,"Arms",new Vector3(0,1.9f,0),new Vector3(1.7f,.18f,.2f),wood);MeshGo(scare,"Head",octa,hay,new Vector3(0,2.35f,0),Vector3.one*.28f);Box(scare,"Hat",new Vector3(0,2.65f,0),new Vector3(.8f,.08f,.6f),hay);
        var swing=Group(p,"Static tire swing");float sy=HeightV5(-20,118);Box(swing,"Rope",new Vector3(-19,sy+2.5f,118),new Vector3(.045f,2,.045f),wood,false);
        var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();for(int i=0;i<=16;i++)for(int j=0;j<=6;j++){float a=i*Mathf.PI/8,b=j*Mathf.PI/3;verts.Add(new Vector3((.48f+.16f*Mathf.Cos(b))*Mathf.Cos(a),(.48f+.16f*Mathf.Cos(b))*Mathf.Sin(a),.16f*Mathf.Sin(b)));uv.Add(new Vector2(i/16f,j/6f));if(i<16&&j<6){int k=i*7+j;tris.AddRange(new[]{k,k+7,k+8,k,k+8,k+1});}}var tire=new Mesh();tire.SetVertices(verts);tire.SetUVs(0,uv);tire.SetTriangles(tris,0);tire.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(tire);MeshGo(swing,"Tire",Save("Tire-swing",tire),roof,new Vector3(-19,sy+1.1f,118),Vector3.one);
    }
}
#endif
