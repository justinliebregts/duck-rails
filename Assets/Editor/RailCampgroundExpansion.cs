#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;

public static partial class RailCampgroundGreybox
{
    static Material canvas, cream, rock;
    static void BuildExpansion(Transform root)
    {
        canvas=Tile("Quackville ochre canvas",new RectInt(0,64,64,64),new Color(.74f,.53f,.29f));
        cream=Tile("Quackville camper paint",new RectInt(0,64,64,64),new Color(.79f,.78f,.65f));
        rock=Tile("Quackville rock",new RectInt(192,0,64,64),new Color(.65f,.67f,.59f));
        var additions=Group(root,"Expanded campground V2");
        var sw=Group(additions,"Southwest tent clearing");Tent(sw,"Damaged tent",-27,31,true);Tent(sw,"Tent",-20,35,false);Picnic(sw,-19,26);Chair(sw,-23,25);Prop(sw,"Cooler",-16.5f,31,1,.65f,.7f,cream);Post(sw,"PITCH 01",-34,25);
        var se=Group(additions,"Southeast camper pitch");Camper(se,33,37);Tent(se,"Tent",25,34,false);Picnic(se,27,41);Chair(se,26,39);Post(se,"PITCH 02",38,31);
        var ne=Group(additions,"Northeast woodland pitches");Tent(ne,"Tent",17,135,false);Tent(ne,"Tent",29,136,false);Picnic(ne,27,130);Chair(ne,23,134);Prop(ne,"Cooler",31,140,1,.65f,.7f,cream);Post(ne,"PITCH 03",34,132);
        var wreck=Group(additions,"Fallen tree scene");
        Beam(wreck,"Fallen trunk",new Vector3(-32,1.4f,35),new Vector3(-23,.8f,29),.65f,timber);
        Beam(wreck,"Broken branch",new Vector3(-29,1.2f,33),new Vector3(-32,1.5f,30),.2f,timber);
        Beam(wreck,"Broken branch",new Vector3(-27,1.1f,32),new Vector3(-27,1.8f,36),.18f,timber);
        var beach=Group(additions,"Canoe beach");Canoe(beach,-28,112);Canoe(beach,-25,112);
        foreach(float z in new[]{110f,114f}){Box(beach,"Rack upright",new Vector3(-19,1,z),new Vector3(.15f,2.2f,.15f),timber);Box(beach,"Rack arm",new Vector3(-19,.9f,z),new Vector3(2.2f,.15f,.15f),timber);}
        Beam(beach,"Rack spine",new Vector3(-19,1.8f,110),new Vector3(-19,1.8f,114),.15f,timber);
        Post(beach,"CANOE LAUNCH",-16,114);
        var overlook=Group(additions,"Rocky overlook");
        foreach(var q in new[]{new Vector2(-32,130),new Vector2(-29,134),new Vector2(-32,136),new Vector2(-31,127)})Boulder(overlook,q);
        var bridge=Group(additions,"Gully footbridge");
        Box(bridge,"Bridge deck",new Vector3(20,.85f,113),new Vector3(3,.3f,6.2f),timber);
        foreach(float x in new[]{18.6f,21.4f})
        {
            Box(bridge,"Bridge handrail",new Vector3(x,2,113),new Vector3(.12f,.12f,6.2f),timber);
            foreach(float z in new[]{110f,113f,116f})Box(bridge,"Bridge post",new Vector3(x,1.5f,z),new Vector3(.12f,1.1f,.12f),timber);
        }
        for(int i=0;i<12;i++)Box(bridge,"Deck plank",new Vector3(20,1.015f,110.25f+i*.5f),new Vector3(2.7f,.025f,.46f),timber);
        var identity=Group(additions,"Campground signs and furnishings");
        Box(identity,"Entrance sign board",new Vector3(13,2,15),new Vector3(6,1.6f,.18f),timber);
        foreach(float x in new[]{10.2f,15.8f})Box(identity,"Sign post",new Vector3(x,1.4f,15),new Vector3(.18f,2.8f,.18f),timber);
        Label(identity,"LAKESIDE\nCAMPGROUND",new Vector3(13,2,14.89f),5.6f,Quaternion.identity);
        Box(identity,"Trail map board",new Vector3(11,1.1f,34),new Vector3(1.5f,1.2f,.12f),cream);
        Label(identity,"CAMP MAP\nLAKE  /  TRAILS",new Vector3(11,1.1f,33.92f),1.4f,Quaternion.identity);
        foreach(var q in new[]{new Vector2(25,54),new Vector2(-7,104),new Vector2(11,33)})Prop(identity,"Rubbish bin",q.x,q.y,.8f,1,.8f,metal);
        foreach(var q in new[]{new Vector2(12,20),new Vector2(27,43),new Vector2(-13,119),new Vector2(12,145)})Post(identity,"TRAIL",q.x,q.y);
        var woods=Group(additions,"Woodland clusters");
        var centers=new[]{new Vector2(-35,19),new Vector2(-17,17),new Vector2(29,17),new Vector2(36,27),new Vector2(-39,42),new Vector2(-12,43),new Vector2(39,48),new Vector2(39,67),new Vector2(39,86),new Vector2(-40,70),new Vector2(-40,95),new Vector2(39,110),new Vector2(-37,117),new Vector2(-11,119),new Vector2(-37,143),new Vector2(-18,146),new Vector2(15,146),new Vector2(36,146),new Vector2(39,130),new Vector2(-32,12),new Vector2(28,148),new Vector2(38,56)};
        var offsets=new[]{new Vector2(-3,-2),Vector2.zero,new Vector2(3,2),new Vector2(-2,4),new Vector2(2,-4),new Vector2(4,-1)};
        foreach(var center in centers)foreach(var offset in offsets)
        {
            var q=center+offset;
            if(Mathf.Abs(q.x)>40||q.y<9||q.y>150||q.x> -7&&q.x<13||LakeDistance(q)<3||Pads.Any(p=>Distance(p.rect,q.x,q.y)<2.8f)||PathDistance(q)<3.3f)continue;
            Tree(woods,q);
        }
        Debug.Log("Expanded campground: "+woods.childCount+" clustered pines, three campsite clearings, canoe launch, rocky overlook and footbridge.");
    }
    static float PathDistance(Vector2 p)
    {
        float best=999;
        foreach(var path in Paths)for(int i=1;i<path.Length;i++){var a=new Vector2(path[i-1].x,path[i-1].z);var d=new Vector2(path[i].x,path[i].z)-a;best=Mathf.Min(best,Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)));}
        return best;
    }
    static void Prop(Transform p,string name,float x,float z,float w,float h,float d,Material material)
    {Box(p,name,new Vector3(x,Height(x,z)+h/2,z),new Vector3(w,h,d),material);}
    static void Beam(Transform p,string name,Vector3 a,Vector3 b,float width,Material material)
    {var g=Box(p,name,(a+b)/2,new Vector3(width,width,Vector3.Distance(a,b)),material);g.transform.localRotation=Quaternion.LookRotation(b-a);}
    static void Label(Transform p,string text,Vector3 position,float width,Quaternion rotation)
    {
        var t=Group(p,"Sign lettering").gameObject.AddComponent<TextMeshPro>();t.text=text;t.fontSize=3;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.9f,.86f,.7f);t.rectTransform.sizeDelta=new Vector2(width,1.2f);t.transform.localPosition=position;t.transform.localRotation=rotation;
    }
    static void Post(Transform p,string text,float x,float z)
    {
        float h=Height(x,z);Box(p,"Wayfinding post",new Vector3(x,h+.8f,z),new Vector3(.15f,1.6f,.15f),timber);
        Box(p,"Wayfinding board",new Vector3(x,h+1.4f,z),new Vector3(1.8f,.5f,.12f),timber);Label(p,text,new Vector3(x,h+1.4f,z-.08f),1.7f,Quaternion.identity);
    }
    static void Picnic(Transform parent,float x,float z)
    {
        var p=Group(parent,"Picnic table");float h=Height(x,z);
        Box(p,"Tabletop",new Vector3(x,h+.85f,z),new Vector3(2.8f,.15f,1.2f),timber);
        foreach(float dz in new[]{-.95f,.95f}){Box(p,"Bench",new Vector3(x,h+.45f,z+dz),new Vector3(2.8f,.12f,.45f),timber);}
        foreach(float dx in new[]{-1f,1f})Box(p,"Table trestle",new Vector3(x+dx,h+.4f,z),new Vector3(.18f,.8f,1.9f),timber);
    }
    static void Chair(Transform p,float x,float z)
    {float y=Height(x,z);Box(p,"Folding chair seat",new Vector3(x,y+.45f,z),new Vector3(.65f,.1f,.65f),canvas);Box(p,"Folding chair back",new Vector3(x,y+.85f,z+.3f),new Vector3(.65f,.7f,.08f),canvas);foreach(float dx in new[]{-.27f,.27f})Box(p,"Chair frame",new Vector3(x+dx,y+.25f,z),new Vector3(.05f,.5f,.6f),metal);}
    static void Tent(Transform parent,string name,float x,float z,bool crushed)
    {
        var p=Group(parent,name);float y=Height(x,z),h=crushed?1.1f:2.4f;
        Vector3[] v={new(-2,0,-2.5f),new(0,h,-2.5f),new(2,0,-2.5f),new(-2,0,2.5f),new(0,h,2.5f),new(2,0,2.5f)};
        // Open front, pitched sides and closed rear; collision follows the cloth shell.
        int[] faces={0,3,4,0,4,1,1,4,5,1,5,2,3,5,4};var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int i=0;i<faces.Length;i++){var q=v[faces[i]];verts.Add(q);uv.Add(new Vector2(q.z/4,(q.x+q.y)/4));tris.Add(i);}
        var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);
        var g=MeshObject(p,"Canvas shell",SaveMesh(crushed?"Crushed-tent":"Tent-shell",mesh),canvas);g.transform.localPosition=new Vector3(x,y,z);
        Box(p,"Tent groundsheet",new Vector3(x,y+.025f,z),new Vector3(4,.05f,5),metal);
        if(!crushed)Box(p,"Tent ridge pole",new Vector3(x,y+h-.08f,z),new Vector3(.06f,.06f,5),timber);
    }
    static void Camper(Transform parent,float x,float z)
    {
        var p=Group(parent,"Abandoned camper");float h=Height(x,z);
        Box(p,"Camper body",new Vector3(x,h+1.6f,z),new Vector3(3.4f,2.5f,6),cream);
        Box(p,"Camper roof",new Vector3(x,h+2.9f,z),new Vector3(3.6f,.2f,6.2f),roof);
        foreach(float dx in new[]{-1.75f,1.75f})foreach(float dz in new[]{-1.8f,1.8f})Box(p,"Wheel",new Vector3(x+dx,h+.5f,z+dz),new Vector3(.3f,.8f,.8f),roof);
        foreach(float dz in new[]{-1.8f,1.8f})Box(p,"Dark camper window",new Vector3(x-1.71f,h+1.9f,z+dz),new Vector3(.025f,.7f,1.3f),roof);
        Box(p,"Camper door",new Vector3(x-1.73f,h+1.45f,z),new Vector3(.04f,1.9f,.9f),metal);
        Beam(p,"Trailer tow bar",new Vector3(x,h+.45f,z-3),new Vector3(x,h+.45f,z-4.2f),.15f,metal);
    }
    static void Canoe(Transform p,float x,float z)
    {
        // Low-poly open hull: tapered keel and gunwale ring, empty interior readable from above.
        var ring=new[]{new Vector2(0,-2.5f),new Vector2(.65f,-1.4f),new Vector2(.75f,.8f),new Vector2(0,2.5f),new Vector2(-.75f,.8f),new Vector2(-.65f,-1.4f)};
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
        for(int i=0;i<ring.Length;i++){var a=ring[i];var b=ring[(i+1)%ring.Length];int k=v.Count;v.AddRange(new[]{new Vector3(a.x,.5f,a.y),new Vector3(b.x,.5f,b.y),new Vector3(b.x*.3f,.06f,b.y*.7f),new Vector3(a.x*.3f,.06f,a.y*.7f)});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3,k+2,k+1,k,k+3,k+2,k});}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.SetUVs(0,uv);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);
        var g=MeshObject(p,"Canoe hull",SaveMesh("Canoe-hull",m),metal);g.transform.localPosition=new Vector3(x,Height(x,z)+.05f,z);
        foreach(float dz in new[]{-1f,1f})Box(p,"Canoe seat",new Vector3(x,Height(x,z)+.4f,z+dz),new Vector3(1,.1f,.3f),timber);
    }
    static void Boulder(Transform p,Vector2 q)
    {
        var m=new Mesh();m.vertices=new[]{new Vector3(-1.5f,0,-1),new Vector3(1.5f,0,-1),new Vector3(1.2f,0,1),new Vector3(-1,0,1.3f),new Vector3(-.6f,1.5f,-.5f),new Vector3(.8f,1.2f,.5f)};m.triangles=new[]{0,4,1,1,4,5,1,5,2,2,5,3,3,5,4,3,4,0,4,3,5};m.uv=m.vertices.Select(a=>new Vector2(a.x,a.z)/4).ToArray();m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);
        var g=MeshObject(p,"Overlook boulder",SaveMesh("Overlook-rock",m),rock);g.transform.localPosition=new Vector3(q.x,Height(q.x,q.y),q.y);
    }
}
#endif


