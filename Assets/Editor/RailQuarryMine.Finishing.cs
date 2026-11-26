#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class RailQuarryMine
{
    static void RemoveNamed(Transform parent,params string[] names){
        foreach(var item in parent.Cast<Transform>().Where(t=>names.Contains(t.name)).ToArray())
            Object.DestroyImmediate(item.gameObject);
    }

    static void RefreshWorkings(Transform root){
        RemoveNamed(root,"Excavated terrain","Underground office","Tunnel timber supports","Mine seep puddles");
        Terrain(root);Interior(root);MinePuddles(root);
        var old=root.Find("Summit shaft hoist");
        var kept=old.Cast<Transform>().Where(t=>t.name=="Wall rubble").ToArray();
        foreach(var stone in kept)stone.SetParent(root,true);
        Object.DestroyImmediate(old.gameObject);SummitHoist(root);
        var rebuilt=root.Find("Summit shaft hoist");RemoveNamed(rebuilt,"Wall rubble");
        foreach(var stone in kept)stone.SetParent(rebuilt,true);
    }

    static Material Lens(string name,Color color,float emission){
        var m=Finish(name,color,emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;return m;
    }
    static void ColdShaftLamp(Transform p,int tier,Vector3 pos){
        string name="Shaft marker "+tier;
        Lamp(p,name,pos,false,11);
        var source=p.Find(name).GetComponent<Light>();source.range=7.5f;source.color=new(.58f,.76f,.96f);
        p.Cast<Transform>().Last(t=>t.name=="Lamp diffuser").GetComponent<Renderer>().sharedMaterial=Lens("Shaft frosted lens",new(.56f,.73f,.87f),.27f);
    }

    static void OfficeBoard(Transform office){
        RemoveNamed(office,"Timber office screen");
        var screen=Group(office,"Timber office screen");
        screen.localPosition=new(11,-6.13f,83.8f);
        // Individual closed planks and back rails give the freestanding screen two finished faces.
        for(int i=0;i<11;i++)
            Box(screen,"Timber plank",new((i-5)*.49f,1.56f,0),new(.484f,3.12f,.18f),wood);
        foreach(float y in new[]{.49f,2.64f})
            Box(screen,"Back rail",new(0,y,.147f),new(5.48f,.19f,.112f),wood);
        foreach(float x in new[]{-2.69f,2.69f})
            Box(screen,"End post",new(x,1.58f,0),new(.17f,3.16f,.32f),wood);
        Box(screen,"Office noticeboard",new(0,2.03f,-.152f),new(2.42f,.95f,.12f),wood);
    }

    static readonly System.Collections.Generic.List<(Vector3 a,Vector3 b,Vector3 ta,Vector3 tb)> officeReturns=new();
    static float OfficeContour(Vector3 p)=>RoundedRoom(p.x,p.z,13.5f,80.25f,8.4f,5.75f);

    static void CollectOfficeWall(Vector3 a,Vector3 b,Vector3 ta,Vector3 tb){
        if((a-b).sqrMagnitude<1e-8f || Mathf.Abs(OfficeContour(a))>.16f ||
           Mathf.Abs(OfficeContour(b))>.16f || Mathf.Abs(OfficeContour((a+b)*.5f))>.16f)return;
        officeReturns.Add((a,b,ta,tb));
    }

    static void BuildOfficeWallReturns(Transform p){
        var skin=new Surface();
        var ends=new System.Collections.Generic.Dictionary<Vector3Int,int>();
        Vector3Int Key(Vector3 at)=>Vector3Int.RoundToInt(at*1000);
        foreach(var span in officeReturns){
            foreach(var at in new[]{span.a,span.b}){
                var k=Key(at);ends.TryGetValue(k,out int count);ends[k]=count+1;
            }
        }
        Vector3 Outward(Vector3 at){
            float x=OfficeContour(at+.03f*Vector3.right)-OfficeContour(at-.03f*Vector3.right);
            float z=OfficeContour(at+.03f*Vector3.forward)-OfficeContour(at-.03f*Vector3.forward);
            return new Vector3(x,0,z).normalized*.48f;
        }
        foreach(var (a,b,ta,tb) in officeReturns){
            var da=Outward(a);var db=Outward(b);
            var ba=a+da-Vector3.up*.07f;var bb=b+db-Vector3.up*.07f;
            var ca=ta+da-Vector3.up*.10f;var cb=tb+db-Vector3.up*.10f;
            skin.Quad(bb,cb,ca,ba);skin.Quad(ta,ca,cb,tb);skin.Quad(a,b,bb,ba);
            if(ends[Key(a)]==1) skin.Quad(ba,ca,ta,a);
            if(ends[Key(b)]==1) skin.Quad(b,tb,cb,bb);
        }
        skin.Build(p,"Office rock wall returns",rock);
        officeReturns.Clear();
    }

    static void CaveLanterns(Transform root,Transform tunnels){
        var enamel=Lens("Field lantern graphite enamel",new(.24f,.25f,.235f),.15f);
        var lens=Lens("Field lantern opal diffuser",new(.86f,.85f,.79f),.76f);
        var tank=RingMesh("Field lantern five sided base",new[]{.18f,.18f,.092f},new[]{0f,.105f,.155f},5,0);
        var hood=RingMesh("Field lantern five sided hood",new[]{.164f,.13f,.063f},new[]{.417f,.45f,.50f},5,0);
        var diffuser=RingMesh("Field lantern solid diffuser",new[]{.099f,.115f},new[]{.153f,.42f},5,0);
        // The frame and bail share one opaque mesh across every lantern.
        var parts=new System.Collections.Generic.List<CombineInstance>();
        void Part(Mesh mesh,Matrix4x4 matrix)=>parts.Add(new CombineInstance{mesh=mesh,transform=matrix});
        Part(tank,Matrix4x4.identity);Part(hood,Matrix4x4.identity);
        void Strap(Vector3 a,Vector3 b){
            Part(cube,Matrix4x4.TRS((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,b-a),
                new Vector3(.019f,(b-a).magnitude,.026f)));
        }
        Strap(new(-.12f,.44f,0),new(-.10f,.64f,0));
        Strap(new(-.10f,.64f,0),new(.10f,.64f,0));
        Strap(new(.10f,.64f,0),new(.12f,.44f,0));
        var combined=new Mesh();combined.CombineMeshes(parts.ToArray());
        var frame=Save("Field lantern opaque frame",combined);
        var sites=new[]{new Vector2(-26.9f,58.1f),new(-23,88),new(-14.3f,105.5f),new(5,98.2f),
            new(-4.4f,88f),new(9.6f,80.1f),new(-.7f,65.2f),new(8.05f,61),new(-9.2f,108),new(-44.5f,107.1f)};
        foreach(var q in sites){
            var p=Group(tunnels,"Camping lantern");
            p.localPosition=new(q.x,Mathf.Min(GroundMeshHeight(q.x,q.y),MineFloor(q.x,q.y)),q.y);
            p.localRotation=Quaternion.Euler(0,12+q.x*.6f,0);
            MeshGo(p,"Gallery lamp housing",frame,enamel,false);
            MeshGo(p,"Opal diffuser",diffuser,lens,false);
            var l=Group(p,"Gallery lamp").gameObject.AddComponent<Light>();
            l.transform.localPosition=new(0,.32f,0);l.type=LightType.Point;
            l.lightmapBakeType=LightmapBakeType.Baked;l.color=new(1,.975f,.93f);l.intensity=23;l.range=15;l.shadows=LightShadows.Soft;
            // The fixture is much smaller than a lightmap texel. Let the mantle light both sides.
            foreach(var r in p.GetComponentsInChildren<MeshRenderer>()){
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);
                r.receiveGI=ReceiveGI.LightProbes;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var collider=r.GetComponent<Collider>();if(collider)Object.DestroyImmediate(collider);
            }
        }
    }

    static void Sheave(Transform rig,Vector3 center,Material iron){
        var ring=new Surface();
        Vector3 P(int i,float radius,float z){float a=i*Mathf.PI/8;return center+new Vector3(radius*Mathf.Cos(a),radius*Mathf.Sin(a),z);}
        for(int i=0;i<16;i++){
            int j=i+1;
            ring.Quad(P(i,1.25f,-.105f),P(j,1.25f,-.105f),P(j,1.25f,.105f),P(i,1.25f,.105f));
            ring.Quad(P(i,1.09f,.105f),P(j,1.09f,.105f),P(j,1.09f,-.105f),P(i,1.09f,-.105f));
            ring.Quad(P(i,1.09f,-.105f),P(j,1.09f,-.105f),P(j,1.25f,-.105f),P(i,1.25f,-.105f));
            ring.Quad(P(i,1.25f,.105f),P(j,1.25f,.105f),P(j,1.09f,.105f),P(i,1.09f,.105f));
            if(i%2==0)Beam(rig,"Sheave spoke",P(i,.22f,0),P(i,1.13f,0),.12f,iron);
        }
        ring.Build(rig,"Cast hoist sheave rim",iron,false);
    }

    static void QuarryFinishing(Transform root){
        var yard=root.Find("Surface machinery and gravel");var pump=yard.Find("Northwest dewatering station");
        FixBuildings(pump.Find("Timber pump house"),yard.Find("Weighbridge cabin and scale"));
        SimplifyPump(pump);
        var water=yard.Find("Sump water");water.localScale=new(8.6f,1,12.4f);
        RemoveNamed(root,"Shaft landing and portal stone","Southwest portal frame","Quarry stair and pit lights");
        PlaceStone(root);
        PortalFrame(root);
        PitLighting(root);
    }

    static void PortalFrame(Transform root){
        var g=Group(root,"Southwest portal frame");
        float z=50;
        while(z<56 && Height(-25,z)<MineRoof(-25,z)+.24f)z+=.08f;
        float roof=MineRoof(-25,z);
        Vector3 left=new(-28.5f,roof-.13f,z),right=new(-21.55f,roof-.13f,z);
        foreach(var end in new[]{left,right}){
            var foot=new Vector3(end.x,MineFloor(end.x,z)-.25f,z);
            Beam(g,"Portal upright",foot,end,.37f,wood);
            var inner=end+Vector3.right*(end.x< -25?.68f:-.68f);
            Beam(g,"Portal knee brace",end-Vector3.up*.72f,inner,.18f,wood);
        }
        Beam(g,"Portal lintel",left-Vector3.right*.24f,right+Vector3.right*.24f,.39f,wood);
    }

    static void PlaceStone(Transform root){
        // Thin the original loose scatter by a position-based selection so repeat builds are stable.
        int Cell(Transform t){var q=t.localPosition;return Mathf.Abs(Mathf.RoundToInt(q.x*37)+Mathf.RoundToInt(q.z*59));}
        void Thin(Transform p,float size){
            foreach(var t in p.Cast<Transform>().ToArray()){
                if(!t.TryGetComponent<MeshFilter>(out _) || t.name!="Fractured stone" && t.name!="Broken rock")continue;
                var s=t.localScale;if(Mathf.Max(s.x,s.z)<size && Cell(t)%3!=0)Object.DestroyImmediate(t.gameObject);
            }
        }
        Thin(root.Find("Mine wall rubble and spalls"),.47f);
        Thin(root.Find("Surface machinery and gravel/Quarry faces and talus"),1.48f);
        var g=Group(root,"Shaft landing and portal stone");
        var meshes=new[]{"Split slate slab","Angular rubble block","Jointed granite buttress"}.Select(n=>AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+n+".asset")).ToArray();
        void Stone(string name,float x,float y,float z,float w,float h,float d,int i){
            var go=MeshGo(g,name,meshes[i%3],rock);go.transform.localPosition=new(x,y,z);
            go.transform.localRotation=Quaternion.Euler(-3,i*71,5);go.transform.localScale=new(w,h,d);
        }
        var debris=new[]{new Vector2(-44.8f,104.2f),new(-45.0f,105.4f),new(-44.8f,108.2f),new(-43.4f,109.1f),new(-40.2f,109.0f),
            new(-39.2f,107.8f),new(-38.2f,106.1f),new(-36.2f,105.4f),new(-34.1f,100.9f),new(-31.6f,103.7f)};
        for(int i=0;i<debris.Length;i++){var q=debris[i];float s=.49f+.13f*(i%4);Stone("Landing spoil",q.x,MineFloor(q.x,q.y)-.12f,q.y,s,.57f+.13f*(i%3),s*1.24f,i);}
        for(int i=0;i<5;i++){
            float z=33+i*4.0f;float y=Height(-25,z)+1.15f+.36f*(i%3);float x=-26;
            while(x>-34 && GroundMeshHeight(x,z)<y)x-=.08f;
            Stone("Exposed portal wall rock",x+.32f,y-.49f,z,.85f+.11f*(i%2),.99f,1.20f+.16f*(i%3),i);
        }
    }

    static void SimplifyPump(Transform pump){
        var pipe=RingMesh("Low polygon hose section",new[]{1f,1f},new[]{-.5f,.5f},8,0);
        var bolt=RingMesh("Hexagonal flange bolt",new[]{1f,1f},new[]{-.5f,.5f},6,0);
        int rib=0,fastener=0;
        foreach(var f in pump.GetComponentsInChildren<MeshFilter>().ToArray()){
            bool isBolt=f.name=="Flange bolt";
            if(!isBolt && f.name!="Discharge hose" && f.name!="Hose joint" && f.name!="Hose rib" && f.name!="Suction flange")continue;
            var target=isBolt?bolt:pipe;
            if(f.sharedMesh==target)continue;
            if(f.name=="Hose rib" && rib++%2==1 || isBolt && fastener++%2==1){Object.DestroyImmediate(f.gameObject);continue;}
            f.sharedMesh=target;if(f.TryGetComponent<MeshCollider>(out var c))c.sharedMesh=target;
        }
    }

    // Both roof slopes share the ridge. The end panels fill the complete triangular gable.
    static void Roof(Transform hut,string prefix,bool acrossX,float eave,float peak,float half,float length,float wallHalf,float wallEnd,float wallTop){
        var roof=new Surface();var boarding=new Surface();
        Vector3 P(float u,float y,float v)=>acrossX?new(u,y,v):new(v,y,u);
        void Face(Surface s,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal){
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0)s.Quad(a,b,c,d);else s.Quad(d,c,b,a);
        }
        void Strip(float halfWidth,float begin,float end,float lift,float depth){
            foreach(int sign in new[]{-1,1}){
                float u=halfWidth*sign;float edge=Mathf.Lerp(peak,eave,halfWidth/half)+lift;
                var a=P(0,peak+lift,begin);var b=P(u,edge,begin);var c=P(u,edge,end);var d=P(0,peak+lift,end);
                var down=Vector3.down*depth;
                Face(roof,a,b,c,d,Vector3.up);Face(roof,a+down,b+down,c+down,d+down,Vector3.down);
                Face(roof,b,b+down,c+down,c,P(sign,0,0));
                Face(roof,a,a+down,b+down,b,P(0,0,-1));Face(roof,d,c,c+down,d+down,P(0,0,1));
            }
        }
        Strip(half,-length,length,0,.14f);
        if(acrossX)for(int i=0;i<9;i++){
            float v=-2.64f+i*.66f;Strip(half-.04f,v-.034f,v+.034f,.049f,.073f);
        }
        foreach(int sign in new[]{-1,1}){
            float end=sign*wallEnd,inner=end-sign*.14f;
            float sideTop=Mathf.Lerp(peak,eave,wallHalf/half)-.12f;
            void Panel(float v,int facing){
                var a=P(-wallHalf,wallTop,v);var b=P(-wallHalf,sideTop,v);var c=P(0,peak-.12f,v);
                var d=P(wallHalf,sideTop,v);var e=P(wallHalf,wallTop,v);var n=P(0,0,facing);
                Face(boarding,a,b,d,e,n);
                if(Vector3.Dot(Vector3.Cross(c-b,d-b),n)>0)boarding.Tri(b,c,d);else boarding.Tri(d,c,b);
            }
            Panel(end,sign);Panel(inner,-sign);
            foreach(int edge in new[]{-1,1}){
                Face(boarding,P(0,peak-.12f,end),P(wallHalf*edge,sideTop,end),P(wallHalf*edge,sideTop,inner),P(0,peak-.12f,inner),Vector3.up);
                Face(boarding,P(wallHalf*edge,wallTop,end),P(wallHalf*edge,wallTop,inner),P(wallHalf*edge,sideTop,inner),P(wallHalf*edge,sideTop,end),P(edge,0,0));
            }
        }
        roof.Build(hut,prefix+" folded roof",steel);boarding.Build(hut,prefix+" gable ends",wood);
    }
    static void FixBuildings(Transform hut,Transform cabin){
        RemoveNamed(hut,"Sloped tin roofing","Roof standing seam","Pump house folded roof","Pump house gable ends");
        RemoveNamed(cabin,"Pitched corrugated roof","Weighbridge folded roof","Weighbridge gable ends");
        foreach(var t in hut.Cast<Transform>().Where(t=>t.name=="Side siding"))t.localScale=new(.18f,3.28f,4.512f);
        var drum=hut.Find("Oil drum");if(drum){var p=drum.localPosition;p.x=-3.95f;drum.localPosition=p;}
        foreach(var t in cabin.Cast<Transform>().Where(t=>t.name=="Cabin side")){
            var pos=t.localPosition;pos.z=.029f;t.localPosition=pos;t.localScale=new(.2f,2.8f,4.251f);
        }
        foreach(var t in cabin.Cast<Transform>().Where(t=>t.name=="Scale end marking"))t.localScale=new(2.97f,.025f,.22f);
        Roof(hut,"Pump house",true,3.64f,4.68f,3.47f,2.85f,3.09f,2.408f,3.54f);
        Roof(cabin,"Weighbridge",false,2.99f,3.57f,2.65f,3.50f,2.32f,3.075f,2.86f);
    }

    static void PitLighting(Transform root){
        var lights=Group(root,"Quarry stair and pit lights");
        var white=Lens("Quarry flood glass",new(.75f,.85f,1f),.85f);
        var amber=Lens("Stone step lamp glass",new(.97f,.57f,.20f),.61f);
        var backing=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Split slate slab.asset");
        foreach(int end in new[]{1,4,8,12}){
            var a=Climb[end-1];var b=Climb[end];var step=Vector3.Lerp(a,b,.54f);
            var flat=new Vector3(b.x-a.x,0,b.z-a.z).normalized;var inward=Vector3.Cross(Vector3.up,flat);
            if(inward.x>0)inward=-inward;
            var basePos=step+inward*2.64f;
            var socket=Group(lights,"Rock light at stair "+end);socket.localPosition=new(basePos.x,GroundMeshHeight(basePos.x,basePos.z)-.19f,basePos.z);
            socket.localRotation=Quaternion.LookRotation(-inward);
            var stone=MeshGo(socket,"Bedrock socket",backing,rock);stone.transform.localScale=new(.84f,1.32f,.68f);
            Solid(socket,"Recessed iron plate",new(0,.80f,.42f),new(.49f,.47f,.23f),steel,false);
            Solid(socket,"Step lamp housing",new(0,.84f,.55f),new(.38f,.34f,.17f),steel,false);
            Solid(socket,"Inset lens",new(0,.81f,.65f),new(.29f,.19f,.046f),amber,false);
            var l=Group(socket,"Step lamp").gameObject.AddComponent<Light>();l.transform.localPosition=new(0,.83f,.74f);
            l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new(1,.70f,.37f);l.intensity=48;l.range=10;l.shadows=LightShadows.Soft;
        }
        void Flood(float x,float z,Vector3 aim,float intensity){
            var mast=Group(lights,"Pit floodlight mast");mast.localPosition=new(x,GroundMeshHeight(x,z),z);
            Solid(mast,"Concrete foot",new(0,.17f,0),new(1.13f,.47f,1.08f),gravel);
            Solid(mast,"Tower",new(0,3.11f,0),new(.23f,6.10f,.23f),steel);
            var head=Group(mast,"Aimed floodlight");head.localPosition=new(0,6.12f,0);
            head.localRotation=Quaternion.LookRotation(aim-(mast.localPosition+head.localPosition));
            Solid(head,"Mounting yoke",new(0,-.19f,-.13f),new(1.21f,.23f,.37f),steel,false);
            Solid(head,"Pit flood housing",Vector3.zero,new(1.36f,.76f,.39f),steel,false);
            Solid(head,"Flood lens",new(0,0,.211f),new(1.17f,.58f,.05f),white,false);
            var l=Group(head,"Pit flood").gameObject.AddComponent<Light>();l.transform.localPosition=new(0,0,.31f);
            l.type=LightType.Spot;l.spotAngle=74;l.innerSpotAngle=41;l.range=46;l.intensity=intensity;l.color=new(.74f,.84f,1);l.shadows=LightShadows.Soft;l.lightmapBakeType=LightmapBakeType.Baked;
        }
        Flood(45.8f,45,new(22,-2.8f,66),2350);Flood(32.3f,85.5f,new(14.1f,-2.25f,86),1580);Flood(18.5f,111,new(30,-3.4f,82),2150);
        var bounce=Group(lights,"Aqua water bounce").gameObject.AddComponent<Light>();bounce.transform.localPosition=new(26.1f,-4.03f,91.5f);
        bounce.type=LightType.Point;bounce.lightmapBakeType=LightmapBakeType.Baked;bounce.color=new(.23f,.75f,.71f);bounce.intensity=20;bounce.range=13;bounce.shadows=LightShadows.Soft;
    }
}
#endif
