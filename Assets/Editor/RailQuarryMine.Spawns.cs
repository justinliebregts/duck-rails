#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class RailQuarryMine
{
    // Each cluster is pinned to authored scenery or a tunnel destination.
    sealed class SpawnArea
    {
        public string name; public bool underground; public int coal,gold;
        public Vector2[] anchors;
        public SpawnArea(string name,bool underground,int coal,int gold,params Vector2[] anchors){
            this.name=name;this.underground=underground;this.coal=coal;this.gold=gold;this.anchors=anchors;
        }
    }
    static SpawnArea[] QuarrySpawnAreas()=>new[]{
        new SpawnArea("South gate and weighbridge",false,12,5,new(-10,8),new(-14,27),new(-22,33),new(-32,22),new(-19,48)),
        new SpawnArea("Old-growth and haul approach",false,11,5,new(31,12),new(39,22),new(12,15),new(24,26),new(42,40)),
        new SpawnArea("West climbing shelves",false,13,9,new(-19,69),new(-28,80),new(-35,72),new(-38,89),new(-13,56)),
        new SpawnArea("Summit hoist and ridge",false,8,7,new(-42,100),new(-39,110),new(-43,92),new(-36,118)),
        new SpawnArea("Pump house and settling basin",false,9,6,new(-33,139),new(-14,130),new(-30,147),new(-39,132)),
        new SpawnArea("East pit and sump",false,11,6,new(25,51),new(21,74),new(19,88),new(26,98),new(14,46)),
        new SpawnArea("Crane bench and upper road",false,9,5,new(37,57),new(38,77),new(37,92),new(31,109)),
        new SpawnArea("Ore siding and spoil yard",false,11,6,new(14,143),new(23,136),new(39,143),new(30,126),new(14,119)),
        new SpawnArea("West drift and shaft landing",true,9,6,new(-25,60),new(-25,78),new(-24,93),new(-35,103),new(-43,106)),
        new SpawnArea("South gallery and crosscut",true,9,6,new(-20,57),new(-13,64),new(-7,73),new(1,63),new(11,59)),
        new SpawnArea("Office and central gallery",true,8,6,new(11,81),new(7,77),new(-1,82),new(-10,91),new(-6,87)),
        new SpawnArea("North gallery and borer",true,10,7,new(-13,108),new(1,101),new(10,92),new(-11,118),new(-13,129))
    };
    sealed class SpawnFit{
        readonly Bounds[] bounds=new Bounds[3];
        readonly Collider[] hits=new Collider[16];
        readonly PhysicsScene scene;readonly RailBlock block;readonly CrawlGraph graph;
        public SpawnFit(RailBlock b){
            block=b;scene=b.gameObject.scene.GetPhysicsScene();graph=CrawlGraph.Load(b.graph);
            bounds[(int)RailSpawnKind.Coal]=RailLootPlacementWindow.ItemBounds(false);
            bounds[(int)RailSpawnKind.Gold]=RailLootPlacementWindow.ItemBounds(true);
            bounds[(int)RailSpawnKind.Enemy]=new Bounds(Vector3.up*.4f,Vector3.one*.8f);
            var solid=b.GetComponentsInChildren<BoxCollider>().First(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6);
            if(scene.OverlapBox(solid.bounds.center,Vector3.one*.02f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0)
                throw new InvalidOperationException("Quarry placement physics unavailable");
        }
        bool Hit(Vector3 origin,float depth,out RaycastHit h)=>scene.Raycast(origin,Vector3.down,out h,depth,1<<6,QueryTriggerInteraction.Ignore);
        public bool Valid(Vector3 p,RailSpawnKind kind){
            var b=bounds[(int)kind];var foot=p+b.center-Vector3.up*b.extents.y;
            foreach(float x in new[]{-b.extents.x,b.extents.x})foreach(float z in new[]{-b.extents.z,b.extents.z})
                if(!Hit(foot+new Vector3(x,.12f,z),.42f,out var h)||h.normal.y<.78f)return false;
            return scene.OverlapBox(p+b.center,b.extents*.98f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0 &&
                (kind!=RailSpawnKind.Enemy||RailLootPlacementWindow.EnemyLocation(block,p,graph));
        }
        public bool Resolve(Vector3 from,float depth,RailSpawnKind kind,out Vector3 point){
            point=default;if(!Hit(from,depth,out var h)||h.normal.y<.86f)return false;
            var b=bounds[(int)kind];var n=h.normal;
            float lift=(Mathf.Abs(n.x)*b.extents.x+Mathf.Abs(n.z)*b.extents.z-n.x*b.center.x-n.z*b.center.z)/n.y;
            point=h.point+Vector3.up*(.05f-b.min.y+lift);
            return Valid(point,kind);
        }
    }
    sealed class Spot{public string label;public Vector3 pos;public RailSpawnKind kind;public int anchor;public bool prop;}
    static List<Spot> PlanQuarrySpawns(RailBlock b){
        var fit=new SpawnFit(b);var plan=new List<Spot>();var areas=QuarrySpawnAreas();
        var solids=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        var usableProps=new HashSet<string>{"Desk top","Locker","Scale platform","Supply crate","Oil drum",
            "Stone sill","Skid footing","Motor bed","Spare tackle crate","Winch sill","Rear collar","Crane pad",
            "Crawler tread","Engine housing","Counterweight","Cab canopy","Footing","Weighbridge folded roof",
            "Pump house folded roof","Wagon bed","Bore spoil","Tail conveyor","Spoil bank","Basin edge stone"};
        var supports=solids.Where(c=>usableProps.Contains(c.name)).ToArray();
        var water=b.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Settling basin water"||r.name=="Sump water").Select(r=>r.bounds).ToArray();
        int Quadrant(Vector3 p)=>(p.x<0?0:1)+(p.z<80?0:2);
        foreach(var kind in new[]{RailSpawnKind.Gold,RailSpawnKind.Coal,RailSpawnKind.Enemy}){
            var enemyPool=new List<Spot>();
            foreach(var area in areas){
                var choices=new List<Spot>();
                void Consider(Vector3 ray,float depth,int anchor,string label,bool prop){
                    if(!fit.Resolve(ray,depth,kind,out var p))return;
                    if(!area.underground && (Mathf.Abs(p.x)<6 || HoistSite(p.x,p.z,-2.2f)))return;
                    if(!area.underground && water.Any(w=>p.y<w.max.y+.12f &&
                        Mathf.Pow((p.x-w.center.x)/w.extents.x,2)+Mathf.Pow((p.z-w.center.z)/w.extents.z,2)<1))return;
                    if(area.underground && (!Tunnel(p.x,p.z)||Height(p.x,p.z)<MineRoof(p.x,p.z)+.12f))return;
                    choices.Add(new Spot{kind=kind,pos=p,label=area.name+" - "+label,anchor=anchor,prop=prop});
                }
                for(int k=0;k<area.anchors.Length;k++){
                    var a=area.anchors[k];
                    for(int j=0;j<81;j++){
                        float r=.82f*Mathf.Sqrt(j),angle=j*2.399963f+k;
                        float x=a.x+r*Mathf.Cos(angle),z=a.y+r*Mathf.Sin(angle);
                        if(x< -46||x>46||z<3||z>157)continue;
                        float y=area.underground?MineFloor(x,z):Height(x,z);
                        Consider(new(x,y+1.25f,z),1.75f,k,"landmark "+(k+1),false);
                    }
                    if(kind==RailSpawnKind.Enemy)continue;
                    foreach(var c in supports){
                        var q=c.bounds.center;
                        if(Vector2.Distance(new(q.x,q.z),a)>8||Mathf.Abs(c.bounds.max.y-(area.underground?MineFloor(q.x,q.z):Height(q.x,q.z)))>(kind==RailSpawnKind.Gold?5:2.9f))continue;
                        foreach(var d in new[]{Vector2.zero,new(.62f,.55f),new(-.62f,-.55f),new(.62f,-.55f),new(-.62f,.55f)}){
                            var cast=new Vector3(q.x+d.x*c.bounds.extents.x,c.bounds.max.y+.18f,q.z+d.y*c.bounds.extents.z);
                            if(c.Raycast(new Ray(cast,Vector3.down),out var hit,.98f))
                                Consider(hit.point+Vector3.up*.09f,.18f,k,c.name,true);
                        }
                    }
                }
                if(kind==RailSpawnKind.Enemy){enemyPool.AddRange(choices);continue;}
                var used=new int[area.anchors.Length];var local=new List<Spot>();
                int target=kind==RailSpawnKind.Coal?area.coal:area.gold;
                while(local.Count<target){
                    var pick=choices.Where(c=>plan.All(p=>(p.pos-c.pos).sqrMagnitude>(p.kind==kind?4f:1f)))
                        .OrderBy(c=>used[c.anchor]).ThenByDescending(c=>c.prop)
                        .ThenByDescending(c=>local.Count==0?0:local.Min(p=>(p.pos-c.pos).sqrMagnitude)).FirstOrDefault();
                    if(pick==null)throw new InvalidOperationException("Insufficient "+kind+" sites at "+area.name+" "+local.Count+"/"+target);
                    plan.Add(pick);local.Add(pick);used[pick.anchor]++;
                }
            }
            if(kind!=RailSpawnKind.Enemy)continue;
            var enemies=new List<Spot>();
            float Dist(Spot a,Spot other)=>new Vector2(a.pos.x-other.pos.x,a.pos.z-other.pos.z).sqrMagnitude;
            for(int q=0;q<4;q++)for(int j=0;j<7;j++){
                bool cave=j<2;
                var pick=enemyPool.Where(c=>Quadrant(c.pos)==q && (c.pos.y< -2.6f&&Tunnel(c.pos.x,c.pos.z)&&Height(c.pos.x,c.pos.z)>MineRoof(c.pos.x,c.pos.z))==cave &&
                    plan.All(p=>(p.pos-c.pos).sqrMagnitude>1f)&&enemies.All(e=>Dist(e,c)>22f))
                    .OrderByDescending(c=>enemies.Count==0?c.pos.z:enemies.Min(e=>Dist(e,c))).FirstOrDefault();
                if(pick==null)throw new InvalidOperationException("No enemy site q="+q+" index="+j);
                enemies.Add(pick);plan.Add(pick);
            }
        }
        return plan;
    }
    static void SaveSpawnPlan(RailBlock b,List<Spot> plan){
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind))){
            var group=b.transform.Find(kind+" Spawns");if(!group)group=Group(b.transform,kind+" Spawns");
            var old=RailSpawnPool.Candidates(b,kind);var next=new List<Transform>();
            foreach(var spot in plan.Where(s=>s.kind==kind)){
                int i=next.Count;var t=i<old.Length?old[i]:null;
                if(!t || PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=AssetDatabase.LoadAssetAtPath<GameObject>(RailSpawnerPrefabs.Path(kind))){
                    if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
                    t=RailSpawnerPrefabs.Instantiate(kind,b.gameObject.scene).transform;t.SetParent(group,false);
                }
                t.position=spot.pos;t.rotation=Quaternion.identity;t.name=kind+" "+(i+1).ToString("D3")+" - "+spot.label;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);next.Add(t);
            }
            for(int i=next.Count;i<old.Length;i++)UnityEngine.Object.DestroyImmediate(old[i].gameObject);
            RailSpawnPool.SetPoints(b.spawnCandidates,kind,next.ToArray());
        }
    }
    public static bool ValidatePlacements(RailBlock b){
        var fit=new SpawnFit(b);
        foreach(RailSpawnKind k in Enum.GetValues(typeof(RailSpawnKind))){
            var points=RailSpawnPool.Available(b,k);
            int minimum=k==RailSpawnKind.Coal?RailResourceBalance.CoalCount(8):k==RailSpawnKind.Gold?RailResourceBalance.GoldCount(8):12;
            if(points.Length<minimum||points.Select(t=>t.position).Distinct().Count()!=points.Length)return false;
            foreach(var t in points)if(!fit.Valid(t.position,k))throw new InvalidOperationException("Invalid saved marker "+t.name+" "+t.position);
        }
        return true;
    }
}
#endif