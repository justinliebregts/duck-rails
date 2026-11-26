#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Quarry plant: summit derrick, crawler crane, wheel loader and tunnel borer.
// Each machine is a few merged low-poly meshes (one per Quackville material) with simple box colliders.
// Cabs are open-windowed shells with a real doorway, floor and access steps.
public static partial class RailQuarryMine
{
    static readonly Vector3 CraneSite=new(36.1f,1.5f,75);

    sealed class Kit
    {
        sealed class Part{public readonly List<Vector3> v=new();public readonly List<Vector2> uv=new();public readonly List<int> t=new();}
        readonly Dictionary<Material,Part> parts=new();
        public int Triangles=>parts.Values.Sum(p=>p.t.Count/3);

        // Convex polygon. Winding is fixed from the outward hint.
        public void Face(Material m,Vector3 outward,params Vector3[] p)
        {
            if(!parts.TryGetValue(m,out var part))parts[m]=part=new Part();
            var n=Vector3.zero;
            for(int i=0;i<p.Length;i++){var a=p[i];var b=p[(i+1)%p.Length];n+=new Vector3((a.y-b.y)*(a.z+b.z),(a.z-b.z)*(a.x+b.x),(a.x-b.x)*(a.y+b.y));}
            if(n.sqrMagnitude<1e-10f)return;
            if(Vector3.Dot(n,outward)<0){Array.Reverse(p);n=-n;}
            n.Normalize();int k=part.v.Count;
            foreach(var q in p){part.v.Add(q);part.uv.Add((Mathf.Abs(n.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(n.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y))/3);}
            for(int i=1;i<p.Length-1;i++){part.t.Add(k);part.t.Add(k+i);part.t.Add(k+i+1);}
        }
        public void Box(Material m,Vector3 c,Vector3 s,Quaternion? r=null)
        {
            var q=r??Quaternion.identity;
            Vector3 X=q*Vector3.right*s.x*.5f,Y=q*Vector3.up*s.y*.5f,Z=q*Vector3.forward*s.z*.5f;
            Face(m,X,c+X-Y-Z,c+X+Y-Z,c+X+Y+Z,c+X-Y+Z);Face(m,-X,c-X-Y-Z,c-X+Y-Z,c-X+Y+Z,c-X-Y+Z);
            Face(m,Y,c-X+Y-Z,c+X+Y-Z,c+X+Y+Z,c-X+Y+Z);Face(m,-Y,c-X-Y-Z,c+X-Y-Z,c+X-Y+Z,c-X-Y+Z);
            Face(m,Z,c-X-Y+Z,c+X-Y+Z,c+X+Y+Z,c-X+Y+Z);Face(m,-Z,c-X-Y-Z,c+X-Y-Z,c+X+Y-Z,c-X+Y-Z);
        }
        // Box section from a to b: w across, d along the up hint.
        public void Beam(Material m,Vector3 a,Vector3 b,float w,float d=-1,Vector3? up=null)
        {
            var dir=b-a;if(dir.sqrMagnitude<1e-8f)return;
            var u=up??(Mathf.Abs(dir.normalized.y)>.99f?Vector3.forward:Vector3.up);
            Box(m,(a+b)*.5f,new(w,d<0?w:d,dir.magnitude),Quaternion.LookRotation(dir,u));
        }
        public void Cyl(Material m,Vector3 a,Vector3 b,float r,int sides=10,float r2=-1,bool caps=true)
        {
            if(r2<0)r2=r;var axis=(b-a).normalized;
            var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);
            Vector3 Ring(Vector3 c,float rad,int i){float t=2*Mathf.PI*(i+.5f)/sides;return c+(u*Mathf.Cos(t)+v*Mathf.Sin(t))*rad;}
            for(int i=0;i<sides;i++){
                var hint=(Ring(a,1,i)+Ring(a,1,i+1))*.5f-a;
                if(r2>0&&r>0)Face(m,hint,Ring(a,r,i),Ring(a,r,i+1),Ring(b,r2,i+1),Ring(b,r2,i));
                else if(r>0)Face(m,hint,Ring(a,r,i),Ring(a,r,i+1),b);
                else Face(m,hint,a,Ring(b,r2,i+1),Ring(b,r2,i));
            }
            if(!caps)return;
            if(r>0)Face(m,-axis,Enumerable.Range(0,sides).Select(i=>Ring(a,r,i)).ToArray());
            if(r2>0)Face(m,axis,Enumerable.Range(0,sides).Select(i=>Ring(b,r2,i)).ToArray());
        }
        // Profile points are (radius, distance along axis). Faces point away from the axis point at insideH.
        public void Lathe(Material m,Vector3 o,Vector3 axis,Vector2[] profile,int sides,float insideH,bool invert=false)
        {
            axis.Normalize();
            var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);
            Vector3 Ring(Vector2 p,int i){float t=2*Mathf.PI*(i+.5f)/sides;return o+axis*p.y+(u*Mathf.Cos(t)+v*Mathf.Sin(t))*p.x;}
            var inside=o+axis*insideH;
            for(int j=0;j<profile.Length-1;j++){
                var p=profile[j];var q=profile[j+1];if(p.x<=0&&q.x<=0)continue;
                for(int i=0;i<sides;i++){
                    var pts=p.x<=0?new[]{Ring(p,i),Ring(q,i),Ring(q,i+1)}:q.x<=0?new[]{Ring(p,i),Ring(q,i),Ring(p,i+1)}:new[]{Ring(p,i),Ring(q,i),Ring(q,i+1),Ring(p,i+1)};
                    var hint=pts.Aggregate(Vector3.zero,(s,x)=>s+x)/pts.Length-inside;
                    Face(m,invert?-hint:hint,pts);
                }
            }
        }
        // Simple polygon in the (u,v) plane through o, extruded to the given thickness.
        public void Prism(Material m,Vector3 o,Vector3 u,Vector3 v,Vector2[] poly,float thickness)
        {
            var n=Vector3.Cross(u,v).normalized*thickness*.5f;
            Vector3 W(Vector2 p,float side)=>o+u*p.x+v*p.y+n*side;
            float area=0;for(int i=0;i<poly.Length;i++){var a=poly[i];var b=poly[(i+1)%poly.Length];area+=a.x*b.y-b.x*a.y;}
            for(int i=0;i<poly.Length;i++){
                var a=poly[i];var b=poly[(i+1)%poly.Length];var e=b-a;
                var o2=area>0?new Vector2(e.y,-e.x):new Vector2(-e.y,e.x);
                Face(m,u*o2.x+v*o2.y,W(a,-1),W(b,-1),W(b,1),W(a,1));
            }
            var idx=Enumerable.Range(0,poly.Length).ToList();if(area<0)idx.Reverse();
            float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            void Cap(int a,int b,int c){Face(m,n,W(poly[a],1),W(poly[b],1),W(poly[c],1));Face(m,-n,W(poly[a],-1),W(poly[b],-1),W(poly[c],-1));}
            int guard=0;
            while(idx.Count>3&&guard++<500){
                bool cut=false;
                for(int i=0;i<idx.Count&&!cut;i++){
                    int a=idx[(i+idx.Count-1)%idx.Count],b=idx[i],c=idx[(i+1)%idx.Count];
                    if(Cross(poly[b]-poly[a],poly[c]-poly[b])<=1e-7f)continue;
                    bool blocked=idx.Any(k=>k!=a&&k!=b&&k!=c&&Cross(poly[b]-poly[a],poly[k]-poly[a])>=0&&Cross(poly[c]-poly[b],poly[k]-poly[b])>=0&&Cross(poly[a]-poly[c],poly[k]-poly[c])>=0);
                    if(blocked)continue;
                    Cap(a,b,c);idx.RemoveAt(i);cut=true;
                }
                if(!cut)break;
            }
            if(idx.Count==3)Cap(idx[0],idx[1],idx[2]);
        }
        // Thin plate spanning a to b, as wide as given along the across direction.
        public void Plate(Material m,Vector3 a,Vector3 b,float width,float thickness,Vector3 across)
            =>Box(m,(a+b)*.5f,new(thickness,width,(b-a).magnitude),Quaternion.LookRotation(b-a,across));
        public void Build(Transform parent,string name,bool probeLit=false)
        {
            foreach(var pair in parts){
                var part=pair.Value;if(part.t.Count==0)continue;
                var mesh=new Mesh();if(part.v.Count>65000)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(part.v);mesh.SetUVs(0,part.uv);mesh.SetTriangles(part.t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                Unwrapping.GenerateSecondaryUVSet(mesh);
                var go=MeshGo(parent,name+" "+pair.Key.name,Save("Machine "+name+" "+pair.Key.name,mesh),pair.Key,false);
                if(!probeLit)continue;
                // Ropes, lattice and rails are too thin for useful lightmap texels.
                GameObjectUtility.SetStaticEditorFlags(go,0);go.GetComponent<MeshRenderer>().receiveGI=ReceiveGI.LightProbes;
            }
        }
    }

    static GameObject Col(Transform p,string name,Vector3 center,Vector3 size,Quaternion? rot=null)
    {
        var g=Group(p,name).gameObject;g.layer=6;g.transform.localPosition=center;
        if(rot.HasValue)g.transform.localRotation=rot.Value;
        g.AddComponent<BoxCollider>().size=size;return g;
    }
    static void ColBeam(Transform p,string name,Vector3 a,Vector3 b,float w,float d,Vector3? up=null)
    {
        var dir=b-a;var u=up??(Mathf.Abs(dir.normalized.y)>.99f?Vector3.forward:Vector3.up);
        Col(p,name,(a+b)*.5f,new(w,d,dir.magnitude),Quaternion.LookRotation(dir,u));
    }
    static Mesh roundCollider;
    static void ColRound(Transform p,string name,Vector3 a,Vector3 b,float radius)
    {
        if(!roundCollider)roundCollider=RingMesh("Machine round collider",new[]{1f,1f},new[]{-.5f,.5f},12,0);
        var g=Group(p,name).gameObject;g.layer=6;g.transform.localPosition=(a+b)*.5f;
        g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=new(radius,Vector3.Distance(a,b),radius);
        g.AddComponent<MeshCollider>().sharedMesh=roundCollider;
    }

    // Open-windowed cab shell between min and max. Walls: 0 = min z, 1 = max z, 2 = min x, 3 = max x.
    // The doorway runs from doorA to doorB along its wall and reaches the header.
    static void Cab(Kit k,Transform cols,string name,Material paint,Material trim,Vector3 min,Vector3 max,int doorWall,float doorA,float doorB,float sill=.9f,float head=.12f)
    {
        const float t=.06f;float y0=min.y,y1=max.y;
        void Seg(int wall,float a,float b,float lo,float hi,Material m,bool collide){
            if(b-a<.01f)return;
            Vector3 c,s;
            if(wall<2){c=new((a+b)/2,(lo+hi)/2,wall==0?min.z+t/2:max.z-t/2);s=new(b-a,hi-lo,t);}
            else{c=new(wall==2?min.x+t/2:max.x-t/2,(lo+hi)/2,(a+b)/2);s=new(t,hi-lo,b-a);}
            k.Box(m,c,s);if(collide)Col(cols,name+" wall",c,s);
        }
        for(int w=0;w<4;w++){
            float a=w<2?min.x:min.z+t,b=w<2?max.x:max.z-t;
            if(w==doorWall){
                Seg(w,a,doorA,y0,y0+sill,paint,true);Seg(w,doorB,b,y0,y0+sill,paint,true);
                Seg(w,doorA-.07f,doorA,y0+sill,y1-head,trim,false);Seg(w,doorB,doorB+.07f,y0+sill,y1-head,trim,false);
            }else{
                Seg(w,a,b,y0,y0+sill,paint,true);
                if(b-a>1.7f)Seg(w,(a+b)/2-.035f,(a+b)/2+.035f,y0+sill,y1-head,trim,false);
            }
            Seg(w,a,b,y1-head,y1,paint,true);
        }
        foreach(float x in new[]{min.x+.04f,max.x-.04f})foreach(float z in new[]{min.z+.04f,max.z-.04f})
            k.Box(trim,new(x,(y0+sill+y1-head)/2,z),new(.08f,y1-head-y0-sill,.08f));
    }

    // Open treads from a landing edge down to the ground, with solid collision blocks beneath each tread.
    static void Steps(Kit k,Transform cols,Material tread,Material side,Vector3 top,Vector3 run,float drop,int rises,float depth,float width)
    {
        run.y=0;run.Normalize();var q=Quaternion.LookRotation(run);var across=q*Vector3.right;float rise=drop/rises;
        for(int i=1;i<rises;i++){
            float y=top.y-rise*i;var c=top+run*(depth*(i-.5f));
            k.Box(tread,new(c.x,y-.025f,c.z),new(width,.05f,depth),q);
            float h=y-(top.y-drop);Col(cols,"Access step",new(c.x,y-h/2,c.z),new(width,h,depth),q);
        }
        var a=top-Vector3.up*.1f;var b=top+run*(depth*(rises-.45f))-Vector3.up*(drop-.03f);
        foreach(float o in new[]{-width/2-.025f,width/2+.025f})k.Beam(side,a+across*o,b+across*o,.05f,.16f);
    }
    static void Seat(Kit k,Material cloth,Material frame,Vector3 floor,Vector3 facing)
    {
        var q=Quaternion.LookRotation(facing);
        k.Box(frame,floor+Vector3.up*.2f,new(.16f,.4f,.16f));
        k.Box(cloth,floor+Vector3.up*.45f,new(.48f,.1f,.46f),q);
        k.Box(cloth,floor+Vector3.up*.78f-q*Vector3.forward*.22f,new(.46f,.6f,.08f),q*Quaternion.Euler(-8,0,0));
    }
    static Material DarkIron=>Mat("Machine dark iron","weathered paint",new(.17f,.18f,.18f));
    static Material BrightSteel=>Mat("Machine bright steel","weathered paint",new(.86f,.9f,1f));
    static Material WireRope=>Mat("Machine wire rope","weathered paint",new(.22f,.21f,.19f));
    static Material SeatCloth=>Mat("Machine seat canvas","ochre canvas",new(.5f,.4f,.28f));

    // 40-tonne class lattice crawler crane on the east bench. Local -Z is the boom direction.
    static void Crane(Transform root)
    {
        var p=Group(root,"Crane");
        Box(p,"Crane pad",new(0,.072f,0),new(8.64f,.144f,7.2f),gravel);
        var dark=DarkIron;var bright=BrightSteel;var rope=WireRope;var cloth=SeatCloth;
        var body=new Kit();var rig=new Kit();const float g=.144f;
        foreach(float s in new[]{-1f,1f}){
            float x=s*1.6f;
            body.Prism(dark,new(x,g,0),Vector3.forward,Vector3.up,new Vector2[]{new(-2.25f,0),new(2.25f,0),new(2.6f,.3f),new(2.6f,.7f),new(2.3f,.95f),new(-2.3f,.95f),new(-2.6f,.7f),new(-2.6f,.3f)},.76f);
            body.Box(steel,new(x+s*.39f,g+.46f,0),new(.04f,.4f,4.1f));
            foreach(float z in new[]{-2.12f,2.12f})body.Cyl(steel,new(x+s*.38f,g+.5f,z),new(x+s*.46f,g+.5f,z),.36f,8);
            foreach(float z in new[]{-1.1f,0,1.1f})body.Cyl(steel,new(x+s*.38f,g+.2f,z),new(x+s*.45f,g+.2f,z),.15f,8);
            Col(p,"Crawler tread",new(x,g+.475f,0),new(.76f,.95f,5.2f));
        }
        // The carbody clears the pad by 0.6 m.
        body.Box(steel,new(0,g+.82f,0),new(2.46f,.42f,2.1f));Col(p,"Carbody",new(0,g+.82f,0),new(2.46f,.42f,2.1f));
        body.Cyl(dark,new(0,g+1.03f,0),new(0,1.3f,0),1.05f,12);
        body.Box(steel,new(-.4f,1.375f,.075f),new(3.7f,.15f,4.85f));Col(p,"Upper deck",new(-.4f,1.375f,.075f),new(3.7f,.15f,4.85f));
        // Machinery house, counterweight and exhaust.
        body.Box(yellow,new(.3f,2.4f,.95f),new(2.3f,1.9f,3.1f));Col(p,"Engine housing",new(.3f,2.42f,.95f),new(2.3f,1.94f,3.1f));
        body.Box(steel,new(.3f,3.39f,.95f),new(2.42f,.08f,3.22f));
        for(int i=0;i<5;i++)body.Box(dark,new(1.46f,1.95f+i*.17f,1.4f),new(.04f,.08f,1.5f));
        body.Box(steel,new(-.86f,2.35f,1.3f),new(.03f,1.6f,.8f));
        body.Cyl(dark,new(1.0f,3.4f,2.0f),new(1.0f,4.15f,2.0f),.09f,8);
        body.Prism(dark,new(0,2.05f,0),Vector3.right,Vector3.forward,new Vector2[]{new(-1.62f,2.5f),new(1.47f,2.5f),new(1.47f,3.0f),new(1.17f,3.3f),new(-1.32f,3.3f),new(-1.62f,3.0f)},1.6f);
        Col(p,"Counterweight",new(-.075f,2.05f,2.9f),new(3.09f,1.6f,.8f));
        // Operator cab beside the boom foot, entered from the left catwalk.
        Vector3 cabMin=new(-2.25f,1.45f,-2.35f),cabMax=new(-.95f,3.45f,-.35f);
        Cab(body,p,"Operator cab",yellow,dark,cabMin,cabMax,1,-2.02f,-1.18f);
        body.Box(yellow,new(-1.6f,3.51f,-1.5f),new(1.42f,.12f,2.5f));Col(p,"Cab canopy",new(-1.6f,3.51f,-1.5f),new(1.42f,.12f,2.5f));
        body.Box(dark,new(-1.6f,1.85f,-2.12f),new(1.1f,.8f,.3f));Col(p,"Operator console",new(-1.6f,1.85f,-2.12f),new(1.1f,.8f,.3f));
        foreach(float x in new[]{-1.9f,-1.3f})body.Beam(bright,new(x,2.25f,-2.1f),new(x,2.6f,-1.95f),.04f);
        Seat(body,cloth,dark,new(-1.6f,1.45f,-1.25f),Vector3.back);
        Steps(body,p,steel,dark,new(-2.25f,1.45f,.65f),Vector3.left,1.45f-g,4,.36f,.9f);
        foreach(float z in new[]{1.2f,2.47f})rig.Box(dark,new(-2.2f,1.95f,z),new(.05f,1.0f,.05f));
        foreach(float y in new[]{1.95f,2.45f})rig.Beam(dark,new(-2.2f,y,1.2f),new(-2.2f,y,2.47f),.04f);
        // Hoist drum, boom foot and A-frame gantry.
        foreach(float x in new[]{-.55f,.55f})body.Box(steel,new(x,1.72f,-2.0f),new(.08f,.55f,.5f));
        body.Cyl(dark,new(-.45f,1.95f,-1.05f),new(.45f,1.95f,-1.05f),.3f,10);
        foreach(float x in new[]{-.5f,.5f})body.Box(steel,new(x,1.75f,-1.05f),new(.06f,.6f,.5f));
        Vector3 foot=new(0,1.95f,-2.0f),tip=new(0,10.4f,-15.4f);
        var along=(tip-foot).normalized;var normal=-Vector3.Cross(along,Vector3.right).normalized;float length=Vector3.Distance(foot,tip);
        Vector3 apex=new(0,5.3f,1.3f);
        foreach(float s in new[]{-1f,1f}){
            rig.Beam(steel,new(s*.6f,3.4f,-.4f),new(s*.45f,apex.y,apex.z),.1f);
            rig.Beam(steel,new(s*.6f,3.4f,2.3f),new(s*.45f,apex.y,apex.z),.1f);
            rig.Beam(rope,new(s*.45f,apex.y,apex.z),tip+Vector3.right*s*.5f+normal*.45f,.035f);
        }
        rig.Cyl(dark,apex-Vector3.right*.5f,apex+Vector3.right*.5f,.15f,8);
        rig.Beam(rope,new(0,2.24f,-1.05f),tip+normal*.42f+along*-.2f,.03f);
        // Lattice boom in three probe-lit lengths: tapered butt, parallel bays and tapered head.
        var nodes=new List<float>{0,1.6f};int bays=9;
        for(int i=1;i<=bays;i++)nodes.Add(1.6f+(length-3.0f)*i/bays);
        nodes.Add(length);
        var boom=new[]{new Kit(),new Kit(),new Kit()};
        float Depth(float s)=>s<1.6f?Mathf.Lerp(.24f,1,s/1.6f):s>length-1.4f?Mathf.Lerp(1,.34f,(s-(length-1.4f))/1.4f):1;
        Vector3 At(float s,float x,float n)=>foot+along*s+Vector3.right*x*.55f+normal*n*Depth(s)*.5f;
        for(int j=0;j<nodes.Count-1;j++){
            float a=nodes[j],b=nodes[j+1];var k=boom[Mathf.Min(2,(int)((a+b)*.5f/length*3))];
            foreach(float x in new[]{-1f,1f})foreach(float n in new[]{-1f,1f})k.Beam(yellow,At(a,x,n),At(b,x,n),.1f,-1,normal);
            float flip=j%2==0?1:-1;
            foreach(float n in new[]{-1f,1f})k.Beam(steel,At(a,-flip,n),At(b,flip,n),.06f,-1,normal);
            foreach(float x in new[]{-1f,1f})k.Beam(steel,At(a,x,-flip),At(b,x,flip),.06f,-1,Vector3.right);
            if(j%2==1||j==nodes.Count-2){
                foreach(float n in new[]{-1f,1f})k.Beam(yellow,At(b,-1,n),At(b,1,n),.07f,-1,normal);
                foreach(float x in new[]{-1f,1f})k.Beam(yellow,At(b,x,-1),At(b,x,1),.07f,-1,Vector3.right);
            }
        }
        Col(p,"Lattice boom",(foot+tip)*.5f,new(1.1f,1,length),Quaternion.LookRotation(along,normal));
        foreach(float x in new[]{-.22f,.22f})boom[2].Cyl(dark,tip+Vector3.right*(x-.06f),tip+Vector3.right*(x+.06f),.38f,10);
        foreach(float x in new[]{-.5f,.5f})boom[2].Box(steel,tip+Vector3.right*x,new(.05f,.6f,.7f),Quaternion.LookRotation(along,normal));
        // Hook block, four-leg sling and loaded ore skip over the pit.
        float hookZ=tip.z-.38f;
        boom[2].Beam(rope,new(0,tip.y,hookZ),new(0,3.75f,hookZ),.03f);
        var load=new Kit();
        load.Box(dark,new(0,3.5f,hookZ),new(.3f,.5f,.24f));load.Cyl(yellow,new(-.18f,3.55f,hookZ),new(.18f,3.55f,hookZ),.2f,8);
        load.Box(steel,new(0,3.16f,hookZ),new(.08f,.22f,.08f));
        foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})load.Beam(rope,new(0,3.08f,hookZ),new(x*.95f,2.28f,hookZ+z*.85f),.03f);
        load.Box(steel,new(0,1.13f,hookZ),new(2.0f,.06f,1.8f));
        foreach(float x in new[]{-1f,1f})load.Box(yellow,new(x*.97f,1.7f,hookZ),new(.06f,1.2f,1.8f));
        foreach(float z in new[]{-1f,1f})load.Box(yellow,new(0,1.7f,hookZ+z*.87f),new(1.88f,1.2f,.06f));
        load.Box(steel,new(0,2.31f,hookZ),new(2.1f,.06f,1.9f));
        load.Box(rock,new(0,2.14f,hookZ),new(1.86f,.3f,1.66f));
        load.Box(rock,new(-.3f,2.38f,hookZ+.2f),new(.7f,.3f,.6f),Quaternion.Euler(12,30,8));load.Box(rock,new(.4f,2.36f,hookZ-.3f),new(.55f,.28f,.5f),Quaternion.Euler(-9,62,14));
        Col(p,"Suspended ore skip",new(0,1.72f,hookZ),new(2.0f,1.24f,1.8f));
        body.Build(p,"Crane body");rig.Build(p,"Crane rigging",true);load.Build(p,"Crane load",true);
        for(int i=0;i<3;i++)boom[i].Build(p,"Crane boom "+(i+1),true);
        Lamp(p,"Crane worklight",new(-1.6f,3.39f,-2.53f),true,65);
    }

    // Articulated wheel loader, roughly a 19-tonne machine. Local +Z is the bucket.
    static void Loader(Transform root)
    {
        var p=Group(root,"Articulated quarry loader");p.localPosition=new(13,0,145);p.localRotation=Quaternion.Euler(0,115,0);
        var paint=Mat("Loader marigold paint","weathered paint",new(1f,.66f,.14f));
        var rubber=Mat("Machine tyre rubber","weathered paint",new(.075f,.08f,.085f));
        var dark=DarkIron;var bright=BrightSteel;var cloth=SeatCloth;
        var body=new Kit();var gear=new Kit();
        var tyre=new Vector2[]{new(.45f,-.27f),new(.74f,-.31f),new(.82f,-.2f),new(.82f,.2f),new(.74f,.31f),new(.45f,.27f)};
        foreach(float s in new[]{-1f,1f})foreach(float z in new[]{-1.65f,1.65f}){
            var c=new Vector3(s*1.07f,.82f,z);
            body.Lathe(rubber,c,Vector3.right,tyre,12,0);
            body.Cyl(paint,c-Vector3.right*.2f,c+Vector3.right*.2f,.46f,12);
            body.Cyl(dark,c+Vector3.right*s*.2f,c+Vector3.right*s*.3f,.17f,8);
            ColRound(p,"Tyre",c-Vector3.right*.31f,c+Vector3.right*.31f,.82f);
        }
        foreach(float z in new[]{-1.65f,1.65f})body.Cyl(dark,new(-.86f,.82f,z),new(.86f,.82f,z),.15f,8);
        // Rear frame, engine hood and counterweight.
        body.Box(dark,new(0,.95f,-1.6f),new(1.3f,.6f,2.9f));Col(p,"Rear chassis",new(0,.95f,-1.6f),new(1.3f,.6f,2.9f));
        body.Prism(paint,new(0,0,0),Vector3.forward,Vector3.up,new Vector2[]{new(-3.2f,1.25f),new(-1.26f,1.25f),new(-1.26f,2.32f),new(-2.9f,2.14f),new(-3.2f,1.9f)},1.8f);
        Col(p,"Engine bonnet",new(0,1.72f,-2.23f),new(1.8f,.94f,1.94f));
        body.Box(dark,new(0,1.6f,-3.215f),new(1.4f,.46f,.04f));
        for(int i=0;i<4;i++)body.Box(dark,new(.92f,1.5f+i*.16f,-2.2f),new(.04f,.07f,1.2f));
        body.Box(paint,new(0,.95f,-3.32f),new(2.5f,.6f,.36f));Col(p,"Counterweight",new(0,.95f,-3.32f),new(2.5f,.6f,.36f));
        body.Cyl(dark,new(.55f,2.2f,-2.0f),new(.55f,3.05f,-2.0f),.07f,8);
        body.Cyl(dark,new(-.5f,2.2f,-2.3f),new(-.5f,2.62f,-2.3f),.13f,8);
        // Rear fender decks. The left one is the walkway from the steps to the cab door.
        body.Box(paint,new(-1.09f,1.72f,-1.37f),new(.74f,.06f,2.56f));Col(p,"Fender deck",new(-1.09f,1.72f,-1.37f),new(.74f,.06f,2.56f));
        body.Box(paint,new(1.09f,1.72f,-1.4f),new(.74f,.06f,2.5f));Col(p,"Fender deck",new(1.09f,1.72f,-1.4f),new(.74f,.06f,2.5f));
        foreach(float s in new[]{-1f,1f})body.Box(paint,new(s*1.09f,1.54f,-2.63f),new(.74f,.3f,.05f));
        Steps(body,p,dark,dark,new(-1.1f,1.75f,-.09f),Vector3.forward,1.75f,4,.28f,.7f);
        foreach(float z in new[]{-.12f,-2.6f})gear.Box(dark,new(-1.43f,2.25f,z),new(.04f,1.0f,.04f));
        gear.Beam(dark,new(-1.43f,2.75f,-.12f),new(-1.43f,2.75f,-2.6f),.04f);
        // ROPS cab with open windows.
        body.Box(dark,new(0,1.44f,-.45f),new(1.2f,.38f,1.3f));
        body.Box(dark,new(0,1.69f,-.45f),new(1.44f,.12f,1.5f));Col(p,"Cab floor",new(0,1.69f,-.45f),new(1.44f,.12f,1.5f));
        Cab(body,p,"Operator cab",paint,dark,new(-.72f,1.75f,-1.2f),new(.72f,3.65f,.3f),2,-1.02f,-.24f,.85f,.1f);
        body.Box(paint,new(0,3.7f,-.45f),new(1.7f,.1f,1.8f));Col(p,"Cab roof",new(0,3.7f,-.45f),new(1.7f,.1f,1.8f));
        Seat(body,cloth,dark,new(.1f,1.75f,-.7f),Vector3.forward);
        body.Beam(dark,new(.1f,1.75f,.12f),new(.1f,2.5f,-.05f),.07f);
        body.Cyl(dark,new(.1f,2.5f,-.05f),new(.1f,2.53f,-.06f),.19f,8);
        foreach(float x in new[]{.42f,.52f})body.Beam(bright,new(x,2.25f,-.45f),new(x,2.5f,-.38f),.03f);
        body.Box(dark,new(.47f,2.0f,-.5f),new(.2f,.5f,.4f));
        // Articulation and front frame with the loader tower.
        body.Cyl(bright,new(0,.6f,0),new(0,1.36f,0),.11f,8);
        foreach(float y in new[]{.68f,1.28f})body.Box(dark,new(0,y,.02f),new(.8f,.08f,.7f));
        foreach(float s in new[]{-1f,1f}){
            body.Cyl(dark,new(s*.5f,.98f,-.5f),new(s*.54f,.98f,.1f),.07f,8);body.Cyl(bright,new(s*.54f,.98f,.1f),new(s*.56f,.98f,.5f),.04f,8);
        }
        body.Box(dark,new(0,.95f,1.27f),new(1.2f,.6f,2.06f));Col(p,"Front chassis",new(0,.95f,1.27f),new(1.2f,.6f,2.06f));
        foreach(float s in new[]{-1f,1f}){
            body.Prism(paint,new(s*.5f,0,0),Vector3.forward,Vector3.up,new Vector2[]{new(.36f,1.25f),new(1.3f,1.25f),new(1.14f,2.2f),new(.76f,2.32f),new(.5f,2.1f)},.1f);
            body.Box(paint,new(s*1.09f,1.7f,1.7f),new(.66f,.05f,1.2f));body.Box(paint,new(s*1.09f,1.56f,1.1f),new(.66f,.3f,.05f));
        }
        Col(p,"Loader tower",new(0,1.75f,.83f),new(1.1f,1.0f,.9f));
        body.Cyl(paint,new(-.5f,2.16f,.85f),new(.5f,2.16f,.85f),.1f,8);
        // Lift arms, cylinders and Z-bar tilt linkage.
        var arm=new Vector2[]{new(.78f,2.3f),new(1.9f,2.05f),new(2.9f,1.36f),new(3.62f,.74f),new(3.72f,.42f),new(3.45f,.38f),new(2.7f,.95f),new(1.8f,1.6f),new(.74f,1.95f)};
        foreach(float s in new[]{-1f,1f}){
            body.Prism(paint,new(s*.68f,0,0),Vector3.forward,Vector3.up,arm,.1f);
            ColBeam(p,"Bucket lift arm",new(s*.68f,2.1f,.8f),new(s*.68f,.55f,3.65f),.12f,.36f);
            Vector3 a=new(s*.46f,.88f,.95f),b=new(s*.46f,1.42f,2.55f);
            body.Cyl(dark,a,Vector3.Lerp(a,b,.62f),.09f,8);body.Cyl(bright,Vector3.Lerp(a,b,.62f),b,.05f,8);
        }
        body.Cyl(paint,new(-.68f,1.22f,2.72f),new(.68f,1.22f,2.72f),.09f,8);
        body.Prism(paint,new(0,0,0),Vector3.forward,Vector3.up,new Vector2[]{new(2.56f,1.12f),new(2.88f,1.16f),new(2.78f,2.18f),new(2.52f,2.18f)},.09f);
        body.Cyl(dark,new(0,2.16f,.9f),new(0,2.16f,2.0f),.1f,8);body.Cyl(bright,new(0,2.16f,2.0f),new(0,2.12f,2.62f),.055f,8);
        body.Beam(dark,new(0,1.2f,2.82f),new(0,1.12f,3.5f),.08f,.12f);
        // Bucket resting on the ground: flat floor, faceted back, side cutters and teeth.
        var back=new[]{new Vector3(0,.03f,3.75f),new(0,.2f,3.5f),new(0,.6f,3.42f),new(0,1.05f,3.55f),new(0,1.36f,3.86f)};
        body.Plate(paint,new(0,.03f,4.72f),back[0],2.9f,.06f,Vector3.right);
        for(int i=0;i<back.Length-1;i++)body.Plate(paint,back[i],back[i+1],2.9f,.06f,Vector3.right);
        var cheek=new Vector2[]{new(4.85f,.0f),new(3.75f,.0f),new(3.5f,.18f),new(3.4f,.6f),new(3.53f,1.07f),new(3.85f,1.4f),new(4.3f,1.0f)};
        foreach(float s in new[]{-1f,1f})body.Prism(paint,new(s*1.47f,.01f,0),Vector3.forward,Vector3.up,cheek,.06f);
        body.Box(dark,new(0,.035f,4.8f),new(2.98f,.07f,.26f));
        for(int i=0;i<7;i++)body.Prism(bright,new(-1.32f+i*.44f,0,0),Vector3.forward,Vector3.up,new Vector2[]{new(4.86f,.0f),new(5.2f,.03f),new(4.86f,.12f)},.14f);
        foreach(float s in new[]{-1f,1f})body.Box(dark,new(s*.68f,.6f,3.4f),new(.14f,.5f,.2f));
        Col(p,"Bucket sole",new(0,.04f,4.3f),new(2.9f,.08f,1.1f));
        Col(p,"Curved bucket back",new(0,.7f,3.5f),new(2.9f,1.36f,.14f),Quaternion.Euler(-10,0,0));
        foreach(float s in new[]{-1f,1f})Col(p,"Bucket side plate",new(s*1.47f,.6f,4.1f),new(.06f,1.1f,1.3f));
        body.Build(p,"Loader body");gear.Build(p,"Loader rails",true);
    }

    // Timber stiff-leg derrick with a geared hand crab winch over the summit shaft.
    static void SummitHoist(Transform root)
    {
        var rig=Group(root,"Summit shaft hoist");rig.localPosition=new(-42,14.4f,106);
        var iron=DarkIron;var bright=BrightSteel;var rope=WireRope;
        var timber=Mat("Derrick timber","timber",new(.62f,.45f,.27f));
        // A three-sided collar leaves the south lip accessible from the summit stairs.
        foreach(float x in new[]{-3.48f,3.62f})Solid(rig,"Shaft collar",new(x,.11f,0),new(x<0?.96f:1.24f,.22f,6.96f),wood);
        Solid(rig,"Rear collar",new(0,.11f,3.5f),new(5.998f,.22f,1f),wood);
        Rail(rig,new(-3.82f,.22f,-3.39f),new(-3.82f,.22f,3.63f));
        Rail(rig,new(-3.82f,.22f,3.8f),new(4.96f,.22f,3.8f));
        Solid(rig,"Winch sill",new(4.55f,.22f,.1f),new(2.72f,.42f,6.5f),wood);
        var body=new Kit();var lines=new Kit();const float sill=.43f;
        // Mast, two stiff legs and a luffing boom reaching over the shaft corner.
        Vector3 mastFoot=new(3.65f,sill,-1.74f),mastHead=new(3.65f,7.43f,-1.74f);
        body.Box(timber,(mastFoot+mastHead)*.5f,new(.36f,7,.36f));ColBeam(rig,"Derrick mast",mastFoot,mastHead,.36f,.36f);
        body.Box(iron,mastFoot+Vector3.up*.07f,new(.62f,.14f,.62f));
        foreach(float y in new[]{1.0f,7.1f})body.Box(iron,new(3.65f,y,-1.74f),new(.44f,.16f,.44f));
        var legHead=new Vector3(3.65f,7.1f,-1.74f);
        foreach(var legFoot in new[]{new Vector3(5.7f,sill,-.5f),new Vector3(3.65f,sill,3.0f)}){
            body.Beam(timber,legHead,legFoot,.26f);ColBeam(rig,"Derrick stiff leg",legHead,legFoot,.26f,.26f);
            body.Box(iron,legFoot+Vector3.up*.06f,new(.5f,.12f,.5f));
        }
        Vector3 heel=new(3.4f,1.0f,-1.74f),boomTip=new(-1.45f,5.2f,-1.74f);
        body.Beam(timber,heel,boomTip,.28f);ColBeam(rig,"Derrick boom",heel,boomTip,.28f,.28f);
        var along=(boomTip-heel).normalized;
        foreach(float s in new[]{.12f,.5f,.93f})body.Box(iron,Vector3.Lerp(heel,boomTip,s),new(.34f,.34f,.12f),Quaternion.LookRotation(along));
        // Head and boom-tip sheaves with the topping lift and hoist rope.
        Vector3 head=new(3.65f,7.62f,-1.74f),sheave=new(-1.5f,4.9f,-1.74f);
        foreach(var c in new[]{head,sheave}){
            body.Cyl(iron,c-Vector3.forward*.05f,c+Vector3.forward*.05f,.22f,10);
            foreach(float z in new[]{-.09f,.09f})body.Box(iron,c+new Vector3(0,c==head?-.12f:.14f,z),new(.12f,.5f,.03f));
        }
        foreach(float z in new[]{-.14f,.14f})lines.Beam(rope,new(3.5f,7.3f,-1.74f+z),boomTip+new Vector3(.1f,.16f,z),.03f);
        Vector3 drum=new(4.55f,1.05f,.1f);
        lines.Beam(rope,drum+new Vector3(-.1f,.33f,-.3f),head+new Vector3(.2f,.08f,0),.035f);
        lines.Beam(rope,head+new Vector3(-.2f,.08f,0),sheave+new Vector3(.1f,.2f,0),.035f);
        // Hand crab winch: cast cheeks, lagged drum, spur gear, pinion shaft and two cranks.
        var cheek=new Vector2[]{new(3.95f,sill),new(5.15f,sill),new(4.85f,1.55f),new(4.25f,1.55f)};
        foreach(float z in new[]{-.7f,.9f}){
            body.Prism(iron,new(0,0,z),Vector3.right,Vector3.up,cheek,.08f);
            Col(rig,"Winch cheek",new(4.55f,.99f,z),new(1.0f,1.12f,.08f));
        }
        body.Cyl(timber,drum+Vector3.forward*-.62f,drum+Vector3.forward*.74f,.28f,10);
        foreach(float z in new[]{-.6f,.62f})body.Cyl(iron,drum+Vector3.forward*(z-.03f),drum+Vector3.forward*(z+.03f),.44f,10);
        body.Cyl(rope,drum+Vector3.forward*-.5f,drum+Vector3.forward*.3f,.33f,10);
        body.Cyl(iron,drum+Vector3.forward*.7f,drum+Vector3.forward*.78f,.5f,12);
        Vector3 shaft=new(4.95f,1.42f,0);
        body.Cyl(bright,shaft+Vector3.forward*-.95f,shaft+Vector3.forward*1.15f,.04f,6);
        body.Cyl(iron,shaft+Vector3.forward*.68f,shaft+Vector3.forward*.8f,.13f,8);
        foreach(float x in new[]{4.12f,4.98f})body.Cyl(bright,new(x,.72f,-.74f),new(x,.72f,.94f),.03f,6);
        body.Beam(iron,shaft+new Vector3(0,0,-.95f),shaft+new Vector3(.26f,-.26f,-.95f),.05f,.08f);
        body.Cyl(timber,shaft+new Vector3(.26f,-.26f,-.97f),shaft+new Vector3(.26f,-.26f,-1.27f),.045f,6);
        body.Beam(iron,shaft+new Vector3(0,0,1.15f),shaft+new Vector3(-.26f,.26f,1.15f),.05f,.08f);
        body.Cyl(timber,shaft+new Vector3(-.26f,.26f,1.17f),shaft+new Vector3(-.26f,.26f,1.47f),.045f,6);
        body.Beam(iron,new(4.3f,1.52f,.82f),new(4.48f,1.62f,.82f),.04f,.1f);
        // Riveted ore kibble hanging at the south-west corner; the middle of the shaft stays clear.
        Vector3 kibble=new(-1.72f,.41f,-1.74f);
        lines.Beam(rope,new(kibble.x,sheave.y,kibble.z),new(kibble.x,1.95f,kibble.z),.035f);
        foreach(float s in new[]{-1f,1f})lines.Beam(iron,new(kibble.x,1.95f,kibble.z),kibble+new Vector3(s*.44f,.9f,0),.04f);
        var tub=new Kit();
        tub.Lathe(iron,kibble,Vector3.up,new Vector2[]{new(0,0),new(.36f,0),new(.52f,.42f),new(.46f,.92f),new(.4f,.92f)},8,.4f);
        tub.Lathe(rock,kibble,Vector3.up,new Vector2[]{new(.4f,.92f),new(.2f,.99f),new(0,1.02f)},8,.4f);
        foreach(float h in new[]{.42f,.86f})tub.Lathe(bright,kibble,Vector3.up,new Vector2[]{new(h<.5f?.52f:.465f,h-.035f),new(h<.5f?.545f:.49f,h),new(h<.5f?.52f:.465f,h+.035f)},8,h);
        Col(rig,"Ore kibble",kibble+Vector3.up*.46f,new(.98f,.92f,.98f));
        body.Build(rig,"Derrick frame");lines.Build(rig,"Derrick ropes",true);tub.Build(rig,"Derrick kibble",true);
        Solid(rig,"Lamp upright",new(5.65f,1.98f,-2.32f),new(.14f,3.55f,.14f),iron);
        Solid(rig,"Winch lamp bracket",new(5.31f,3.72f,-2.32f),new(.79f,.13f,.16f),iron);
        Lamp(rig,"Winch work light",new(4.98f,3.61f,-2.32f),true,67);
        Solid(rig,"Spare tackle crate",new(4.57f,.82f,2.48f),new(1.03f,.79f,.84f),timber);
        var slab=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset");
        for(int tier=0;tier<5;tier++){
            float y=-1.2f-tier*3.35f;
            foreach(float x in new[]{-2.96f,2.96f})Beam(rig,"Headframe timber",new(x,y,-3),new(x,y,3),.22f,timber);
            foreach(float z in new[]{-2.96f,2.96f})Beam(rig,"Headframe timber",new(-2.85f,y,z),new(2.85f,y,z),.215f,timber);
            for(int j=0;j<4;j++){var b=MeshGo(rig,"Wall rubble",slab,rock);float offset=1.7f*Mathf.Sin(1.82f*tier+j);
                b.transform.localPosition=new(j<2?(j==0?-3f:3f):offset,y-.9f,j<2?offset:(j==2?-3f:3f));
                b.transform.localScale=new(.46f,.63f,.52f);b.transform.localRotation=Quaternion.Euler(0,47*j+19*tier,0);}
            if(tier%2==0)ColdShaftLamp(rig,tier,new(-2.90f,y-.14f,.77f));
        }
    }

    // 4.2 m open gripper tunnel boring machine on its assembly rails, with a trailing backup deck and cab.
    static void BoringMachine(Transform root)
    {
        var rig=Group(root,"Tunnel boring machine");rig.localPosition=new(-13,-6,130);
        var railSteel=Finish("Cutter steel",new(.40f,.45f,.49f));
        var orange=Mat("Borer orange paint","weathered paint",new(1f,.5f,.14f));
        var cutter=Mat("Borer cutter steel","weathered paint",new(.66f,.72f,.82f));
        var dark=DarkIron;var bright=BrightSteel;var cloth=SeatCloth;
        // Assembly bay, rails and spoil.
        foreach(float z in new[]{-7f,-2.5f,2.2f,6.5f}){
            foreach(float side in new[]{-1f,1f})Solid(rig,"Bore bay rib",new(side*5.55f,2.3f,z),new(.23f,4.6f,.28f),steel);
            Solid(rig,"Bore bay crossbeam",new(0,4.68f,z),new(11.2f,.22f,.28f),steel);
        }
        for(int j=-7;j<=7;j+=2)Solid(rig,"Machine track sleeper",new(0,.04f,j),new(3.18f,.23f,.29f),wood,false);
        foreach(float side in new[]{-1f,1f}){
            Solid(rig,"Machine rail",new(side*1.17f,.21f,0),new(.15f,.19f,15.55f),railSteel,false);
            for(int j=0;j<7;j++){float z=-6.35f+2.08f*j;float xx=side*(3.83f+.46f*Mathf.Sin(j*1.9f));
                var debris=MeshGo(rig,"Bore spoil",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset"),rock);
                debris.transform.localPosition=new(xx,MineFloor(-13+xx,130+z)+6-.11f,z);debris.transform.localScale=new(.65f+.13f*(j%3),.67f+.19f*(j%4),.79f);debris.transform.localRotation=Quaternion.Euler(0,51*j,0);}
        }
        var axis=new Vector3(0,2.25f,0);var body=new Kit();var gear=new Kit();
        // Cutterhead: domed face, disc cutters on a spiral, gauge cutters and muck buckets.
        var headGroup=Group(rig,"Cutter head");var head=new Kit();
        head.Lathe(cutter,axis,Vector3.forward,new Vector2[]{new(0,7.28f),new(1.25f,7.28f),new(1.9f,7.12f),new(2.1f,6.88f),new(2.1f,6.55f),new(0,6.55f)},16,6.9f);
        for(int i=0;i<16;i++){
            float radius=i<4?.3f:.55f+(i-4)*.113f,angle=(i<4?i*90+45:i*137.5f)*Mathf.Deg2Rad;
            var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);var c=axis+radial*radius+Vector3.forward*(radius>1.3f?7.28f-(radius-1.25f)*.25f:7.3f);
            head.Cyl(bright,c-radial*.04f,c+radial*.04f,.17f,8);
            head.Box(dark,c-Vector3.forward*.02f,new(.2f,.42f,.1f),Quaternion.Euler(0,0,angle*Mathf.Rad2Deg));
        }
        for(int i=0;i<8;i++){
            float angle=(i*45+22.5f)*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            var q=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
            if(i%2==0)head.Cyl(bright,axis+radial*1.98f+Vector3.forward*7.03f,axis+radial*2.07f+Vector3.forward*7.0f,.17f,8);
            else head.Box(dark,axis+radial*2.06f+Vector3.forward*6.74f,new(.14f,.56f,.36f),q);
        }
        for(int i=0;i<4;i++){float angle=i*90*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            head.Box(cutter,axis+radial*1.05f+Vector3.forward*7.3f,new(1.1f,.16f,.06f),Quaternion.Euler(0,0,i*90));}
        head.Build(headGroup,"Cutter head");
        ColRound(rig,"Cutter head shell",axis+Vector3.forward*6.55f,axis+Vector3.forward*7.28f,2.1f);
        // Cutterhead shield, main bearing and drive motors.
        body.Cyl(orange,axis+Vector3.forward*5.25f,axis+Vector3.forward*6.5f,2.0f,16);
        foreach(float z in new[]{5.3f,6.42f})body.Cyl(dark,axis+Vector3.forward*(z-.05f),axis+Vector3.forward*(z+.05f),2.04f,16,-1,false);
        ColRound(rig,"Segmented shield",axis+Vector3.forward*5.25f,axis+Vector3.forward*6.5f,2.0f);
        body.Cyl(steel,axis+Vector3.forward*4.55f,axis+Vector3.forward*5.25f,1.25f,12);
        foreach(float x in new[]{-.74f,.74f})foreach(float y in new[]{-.74f,.74f})
            body.Cyl(dark,axis+new Vector3(x,y,3.8f),axis+new Vector3(x,y,4.55f),.25f,8);
        // Main beam with the gripper carriage and thrust cylinders.
        body.Box(orange,axis+Vector3.forward*1.9f,new(.9f,1.0f,5.4f));Col(rig,"Main beam",axis+Vector3.forward*1.9f,new(.9f,1.0f,5.4f));
        body.Box(steel,axis+Vector3.forward*2.3f,new(1.3f,1.4f,1.5f));
        body.Cyl(dark,axis+new Vector3(-1.7f,0,2.3f),axis+new Vector3(1.7f,0,2.3f),.3f,10);
        foreach(float s in new[]{-1f,1f}){
            body.Box(steel,axis+new Vector3(s*1.85f,0,2.3f),new(.18f,2.0f,1.5f));
            foreach(float up in new[]{-1f,1f})body.Box(steel,axis+new Vector3(s*1.72f,up*1.2f,2.3f),new(.18f,.6f,1.5f),Quaternion.Euler(0,0,-s*up*28));
            Col(rig,"Gripper shoe",axis+new Vector3(s*1.82f,0,2.3f),new(.3f,2.7f,1.5f));
            foreach(float y in new[]{-.42f,.42f}){
                Vector3 a=axis+new Vector3(s*1.22f,y,2.95f),b=axis+new Vector3(s*1.5f,y,5.25f);
                body.Cyl(dark,a,Vector3.Lerp(a,b,.62f),.13f,8);body.Cyl(bright,Vector3.Lerp(a,b,.62f),b,.07f,8);
            }
        }
        // Rail undercarriage beneath the machine.
        foreach(float s in new[]{-1f,1f}){
            float x=s*1.17f;
            body.Box(dark,new(x,.72f,2.25f),new(.3f,.25f,6.3f));Col(rig,"Rail bogie frame",new(x,.72f,2.25f),new(.3f,.25f,6.3f));
            foreach(float z in new[]{-.5f,1.5f,3.4f,5.0f})body.Cyl(cutter,new(x-.07f,.585f,z),new(x+.07f,.585f,z),.28f,10);
            body.Beam(steel,new(x,.84f,5.6f),new(s*1.02f,1.2f,5.6f),.24f,.3f);
        }
        foreach(float z in new[]{-.5f,.8f,3.9f})body.Box(dark,new(0,.72f,z),new(2.04f,.2f,.3f));
        foreach(float s in new[]{-1f,1f})foreach(float z in new[]{-.5f,.8f})body.Box(steel,new(s*.56f,1.3f,z),new(.22f,.96f,.3f));
        // Backup deck on its own bogies: operator cab, power cabinets and tail conveyor.
        body.Box(steel,new(0,.84f,-3.3f),new(3.6f,.12f,4.6f));Col(rig,"Backup deck",new(0,.84f,-3.3f),new(3.6f,.12f,4.6f));
        foreach(float s in new[]{-1f,1f})foreach(float z in new[]{-1.7f,-4.9f}){
            body.Cyl(cutter,new(s*1.17f-.07f,.585f,z),new(s*1.17f+.07f,.585f,z),.28f,10);
            body.Box(dark,new(s*1.33f,.66f,z),new(.08f,.3f,.5f));
        }
        body.Box(dark,new(0,.74f,-.85f),new(.3f,.12f,.5f));
        Vector3 cabMin=new(-1.8f,.9f,-3.7f),cabMax=new(-.2f,2.9f,-1.4f);
        Cab(body,rig,"Operator cab",orange,dark,cabMin,cabMax,0,-1.45f,-.6f);
        body.Box(orange,new(-1.0f,2.95f,-2.7f),new(1.76f,.1f,2.8f));Col(rig,"Cab roof",new(-1.0f,2.95f,-2.7f),new(1.76f,.1f,2.8f));
        body.Box(dark,new(-1.0f,1.3f,-1.62f),new(1.3f,.8f,.3f));Col(rig,"Operator console",new(-1.0f,1.3f,-1.62f),new(1.3f,.8f,.3f));
        foreach(float x in new[]{-1.35f,-.7f})body.Box(cutter,new(x,1.9f,-1.6f),new(.5f,.36f,.06f),Quaternion.Euler(12,0,0));
        Seat(body,cloth,dark,new(-1.0f,.9f,-2.4f),Vector3.forward);
        Steps(body,rig,steel,dark,new(-1.3f,.9f,-5.6f),Vector3.back,.9f,3,.3f,.8f);
        body.Box(steel,new(1.5f,1.55f,-3.2f),new(.55f,1.3f,3.4f));Col(rig,"Power cabinets",new(1.5f,1.55f,-3.2f),new(.55f,1.3f,3.4f));
        for(int i=0;i<3;i++)body.Box(dark,new(1.79f,1.6f,-4.3f+i*1.1f),new(.03f,1.0f,.9f));
        Solid(rig,"Tail conveyor",new(.55f,1.04f,-3.1f),new(1.16f,.42f,4.7f),steel);
        foreach(float x in new[]{.02f,1.08f})Solid(rig,"Conveyor lip",new(x,1.33f,-3.1f),new(.12f,.28f,4.7f),orange);
        for(int i=0;i<7;i++)gear.Box(dark,new(.55f,1.29f,-5.18f+i*.61f),new(.91f,.085f,.09f));
        gear.Plate(dark,new(.5f,1.27f,-.8f),new(.2f,1.8f,.9f),.7f,.06f,Vector3.right);
        body.Build(rig,"Borer body");gear.Build(rig,"Borer fittings",true);
        Lamp(rig,"Service flood",new(-1.0f,2.84f,-3.9f),true,65);
        Lamp(rig,"Cutter inspection lamp",new(2.9f,4.52f,6.5f),false,28);
    }

    static void MachineAssets()
    {
        Material Load(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+n+".mat") ?? throw new InvalidOperationException("Missing Quarry material "+n);
        boxes.Clear();roundSection=null;roundCollider=null;
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");
        if(!cube)throw new InvalidOperationException("Missing Quarry blockout cube mesh");
        rock=Load("Quarried slate");gravel=Load("Rolled haul gravel");railPaint=Load("Safety rail yellow");
        wood=Load("Tunnel timber");steel=Load("Machinery steel");yellow=Load("Faded machinery yellow");glass=Load("Cab glass");
    }

    // Replaces only the four machines in the saved prefab, then rebakes the crawl graph. Lighting needs a rebake afterwards.
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Rebuild machines")]
    public static void RebuildMachines()
    {
        Guard();MachineAssets();
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var block=prefab.GetComponent<RailBlock>();
            var root=RailBlock.FindEnvironment(block.transform,RootName);
            if(!root)throw new InvalidOperationException("Expected the mountain Quarry");
            var yard=root.Find("Surface machinery and gravel");
            void Swap(Transform parent,string name,Action build,Action<Transform> place=null){
                var old=parent.Find(name);int index=old?old.GetSiblingIndex():parent.childCount;
                var kept=old&&name=="Summit shaft hoist"?old.Cast<Transform>().Where(t=>t.name=="Wall rubble").ToArray():Array.Empty<Transform>();
                foreach(var stone in kept)stone.SetParent(parent,true);
                if(old)Object.DestroyImmediate(old.gameObject);
                build();var made=parent.Find(name);place?.Invoke(made);
                if(kept.Length>0){RemoveNamed(made,"Wall rubble");foreach(var stone in kept)stone.SetParent(made,true);}
                made.SetSiblingIndex(index);
            }
            Swap(yard,"Crane",()=>Crane(yard),crane=>{crane.localRotation=Quaternion.Euler(0,90,0);crane.localPosition=CraneSite;});
            Swap(yard,"Articulated quarry loader",()=>Loader(yard));
            Swap(root,"Summit shaft hoist",()=>SummitHoist(root));
            Swap(root,"Tunnel boring machine",()=>BoringMachine(root));
            var lighting=prefab.GetComponent<RailBakedBlockLighting>();
            lighting.bindings=Array.Empty<RailBakedBlockLighting.Binding>();
            lighting.CaptureBakedFixtures();
            PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath,out bool saved);
            if(!saved)throw new IOException("Could not save Quarry prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(BakeScene);
        var quarry=Object.FindFirstObjectByType<RailBlock>();
        if(!quarry || quarry.id!=Id)throw new InvalidOperationException("Wrong block in Quarry scene");
        var baker=quarry.GetComponentInChildren<CrawlGraphBaker>();
        Physics.SyncTransforms();
        if(!CrawlGraphBake.Bake(baker,false))throw new InvalidOperationException("Quarry graph bake failed");
        PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        PrefabUtility.ApplyPrefabInstance(quarry.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Debug.Log("Quarry machines rebuilt and graph baked. Run the Quarry checks, then Bake Open Block.");
    }

    // Player-sized probes: stand outside each cab door, walk in, stand inside, and climb each access step.
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Check machine cabs")]
    public static void CheckMachines()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Use Edit Mode, finish bake");
        if(SceneManager.GetActiveScene().path!=BakeScene)EditorSceneManager.OpenScene(BakeScene);
        Physics.SyncTransforms();
        var block=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(block.transform,RootName);
        var lines=new List<string>();
        void Check(bool yes,string text)=>lines.Add((yes?"PASS ":"FAIL ")+text);
        bool Stand(Vector3 p)=>Physics.Raycast(p+Vector3.up*.3f,Vector3.down,out var hit,.6f,1<<6)&&Mathf.Abs(hit.point.y-p.y)<.08f
            &&!Physics.CheckCapsule(p+Vector3.up*.42f,p+Vector3.up*1.33f,.32f,1<<6);
        void Probe(string label,Transform machine,Vector3 outside,Vector3 inside,Vector3 stepTop,Vector3 stepRun,float drop,int rises,float depth){
            Vector3 a=machine.TransformPoint(outside),b=machine.TransformPoint(inside);
            Check(Stand(a),label+" landing outside the door "+a);
            Check(Stand(b),label+" standing room inside the cab "+b);
            Check(!Physics.CapsuleCast(a+Vector3.up*.42f,a+Vector3.up*1.33f,.3f,(b-a).normalized,Vector3.Distance(a,b),1<<6),label+" doorway passes a 1.65 m player");
            Check(Physics.Raycast(b+Vector3.up*1.2f,Vector3.up,out var roof,1.6f,1<<6)&&roof.point.y-b.y>=1.75f,label+" headroom");
            float rise=drop/rises;
            for(int i=1;i<=rises;i++){
                var local=stepTop+stepRun.normalized*(depth*(i-.5f))-Vector3.up*rise*i;
                if(i==rises)local=stepTop+stepRun.normalized*(depth*(rises-.5f)+.25f)-Vector3.up*drop;
                var foot=machine.TransformPoint(local);
                Check(Physics.Raycast(foot+Vector3.up*.3f,Vector3.down,out var hit,.75f,1<<6)&&Mathf.Abs(hit.point.y-foot.y)<.12f,label+" step "+i+" of "+rises+" rise "+rise.ToString("F2")+" m "+foot);
            }
        }
        var yard=root.Find("Surface machinery and gravel");
        Probe("Crane",yard.Find("Crane"),new(-1.6f,1.45f,.45f),new(-1.6f,1.45f,-1.0f),new(-2.25f,1.45f,.65f),Vector3.left,1.306f,4,.36f);
        Probe("Loader",yard.Find("Articulated quarry loader"),new(-1.09f,1.75f,-.62f),new(-.05f,1.75f,-.62f),new(-1.1f,1.75f,-.09f),Vector3.forward,1.75f,4,.28f);
        Probe("Borer",root.Find("Tunnel boring machine"),new(-1.02f,.9f,-4.5f),new(-1.02f,.9f,-2.9f),new(-1.3f,.9f,-5.6f),Vector3.back,.9f,3,.3f);
        foreach(var name in new[]{"Surface machinery and gravel/Crane","Surface machinery and gravel/Articulated quarry loader","Summit shaft hoist","Tunnel boring machine"}){
            var t=root.Find(name);
            int tris=t.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            lines.Add("INFO "+t.name+": "+tris+" triangles, "+t.GetComponentsInChildren<Renderer>().Length+" renderers, "+t.GetComponentsInChildren<Collider>().Length+" colliders");
            foreach(var m in t.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Crane ")||r.name.StartsWith("Loader ")||r.name.StartsWith("Derrick ")||r.name.StartsWith("Borer ")||r.name.StartsWith("Cutter head ")).SelectMany(r=>r.sharedMaterials).Distinct())
                Check(m.mainTexture&&(AssetDatabase.GetAssetPath(m.mainTexture).Contains("/Quackville ")||AssetDatabase.GetAssetPath(m.mainTexture).StartsWith(Folder)),"Quackville or existing Quarry texture on "+m.name);
        }
        Check(ValidatePlacements(block),"All saved coal, gold and enemy markers still fit");
        Directory.CreateDirectory("docs/validation/quarry-machines");File.WriteAllLines("docs/validation/quarry-machines/checks.txt",lines);
        if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL"))));
        Debug.Log("MACHINE CHECKS: "+lines.Count(l=>l.StartsWith("PASS"))+" passed");
    }

    public static void CaptureMachines(string tag)
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Finish play/bake first");
        EditorSceneManager.OpenScene(BakeScene);Directory.CreateDirectory("output/quarry-machines");
        bool async=EditorSettings.asyncShaderCompilation;EditorSettings.asyncShaderCompilation=false;
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.1f,.13f,.16f);cam.farClipPlane=500;cam.nearClipPlane=.1f;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=2.4f;fill.transform.rotation=Quaternion.Euler(38,-31,0);
        var torch=new GameObject("Temporary review torch").AddComponent<Light>();torch.type=LightType.Point;torch.range=22;torch.intensity=14;torch.enabled=false;
        try
        {
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.41f,.48f);
            Shot("hoist",new(-31.0f,22.6f,95.1f),new(-40.3f,16.65f,105.7f),55);
            Shot("hoist-winch",new(-32.6f,17.3f,110.6f),new(-37.6f,15.6f,106),55);
            Shot("crane",new(46,8,61),new(34.6f,5,77),55);
            Shot("crane-cab",new(41.2f,4.6f,80.6f),new(35.2f,3.6f,76.4f),55);
            Shot("loader",new(19,3.4f,137.6f),new(13.6f,1.6f,145.2f),55);
            torch.enabled=true;torch.intensity=5;Shot("loader-cab",new(14.3f,2.7f,148.9f),new(12.9f,2.2f,144.9f),62);
            fill.enabled=false;torch.enabled=true;torch.intensity=14;RenderSettings.ambientLight=new Color(.2f,.21f,.24f);
            Shot("borer",new(-17,-3.4f,122),new(-12.9f,-3.75f,133),72);
            Shot("borer-cutterhead",new(-17.8f,-3.4f,137.4f),new(-13,-3.9f,134.5f),78);
            Shot("borer-cab",new(-15.6f,-4.0f,122.6f),new(-13.8f,-4.5f,127.4f),68);
            cam.nearClipPlane=6.05f;torch.range=40;torch.intensity=30;Shot("borer-face",new(-14.6f,-3.35f,143.8f),new(-13,-3.8f,135.5f),43);
        }
        finally{EditorSettings.asyncShaderCompilation=async;Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);Object.DestroyImmediate(torch.gameObject);EditorSceneManager.OpenScene(BakeScene);}
        void Shot(string n,Vector3 eye,Vector3 target,float fov){
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;torch.transform.position=eye+Vector3.up*.4f;
            var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("output/quarry-machines/"+tag+"-"+n+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif
