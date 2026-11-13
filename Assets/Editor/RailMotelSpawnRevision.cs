#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Explicit replacement pass over the current saved layout. Does not change runtime tuning.
public static class RailMotelSpawnRevision
{
    class Site { public string area,name;public Vector3 ray;public RailSpawnKind? only; }
    class Placement { public RailSpawnKind kind;public string area,name;public Vector3 position; }
    static List<Site> sites;
    static List<Placement> plan;
    static RailBlock block;static PhysicsScene physics;static Collider[] solids;static Collider[] hits=new Collider[128];
    static Game.AI.CrawlGraph graph;
    static void Add(string area,string name,float x,float y,float z,RailSpawnKind? only=null)=>sites.Add(new Site{area=area,name=name,ray=new Vector3(x,y,z),only=only});
    static void Prop(string area,Collider c,string label,params Vector2[] offsets)
    {foreach(var o in offsets)Add(area,label,c.bounds.center.x+o.x,c.bounds.max.y+.5f,c.bounds.center.z+o.y);}
    static bool Valid(Vector3 p,RailSpawnKind kind)
    {
        var bounds=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,new Vector3(.8f,.8f,.8f)):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
        foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
        {var q=p+new Vector3(bounds.center.x+x,bounds.min.y,bounds.center.z+z);if(!solids.Any(c=>c.Raycast(new Ray(q+Vector3.up*.12f,Vector3.down),out _,.3f)))return false;}
        return physics.OverlapBox(p+bounds.center,bounds.extents*.97f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0 && (kind!=RailSpawnKind.Enemy||RailLootPlacementWindow.EnemyLocation(block,p,graph));
    }
    static bool Resolve(Site site,RailSpawnKind kind,out Vector3 position)
    {
        position=default;var ray=new Ray(site.ray,Vector3.down);var surface=solids.Select(c=>c.Raycast(ray,out var h,1.6f)?h:default).Where(h=>h.collider&&h.normal.y>.995f).OrderBy(h=>h.distance).FirstOrDefault();if(!surface.collider)return false;
        var bounds=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,new Vector3(.8f,.8f,.8f)):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
        position=surface.point+Vector3.up*(.05f-bounds.min.y);return Valid(position,kind);
    }
    static void Init()
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||stage==null||stage.assetPath!=RailMotelGreybox.PrefabPath||EditorApplication.isPlaying)throw new Exception("Open Duck Rails motel Prefab Mode");
        block=stage.prefabContentsRoot.GetComponent<RailBlock>();physics=stage.scene.GetPhysicsScene();Physics.SyncTransforms();solids=block.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();graph=Game.AI.CrawlGraph.Load(block.graph);sites=new List<Site>();plan=new List<Placement>();
        var control=solids.First(c=>c.name=="East upper slab");if(physics.OverlapBox(control.bounds.center,Vector3.one*.1f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0)throw new Exception("Physics overlap positive control failed");
    }
    static void Sites()
    {
        foreach(int id in new[]{101,102,104,202,204,207})
        {
            string area="Room "+id;float y=id>=200?3.6f:0,z=54+(id%100-1)*8;
            foreach(string name in new[]{"Bed "+id,"Desk "+id}){var c=solids.First(c=>c.name==name);Prop(area,c,name,new Vector2(0,0),new Vector2(0,.7f),new Vector2(.55f,0),new Vector2(-.55f,0));}
            if(id==207){Add(area,"entry-side corner",21.4f,y+.7f,99);Add(area,"bathroom rear corner",28.7f,y+.7f,108);Add(area,"beside dresser",23,y+.7f,105);Add(area,"window side",24,y+.7f,108);Add(area,"bed foot",28.4f,y+.7f,103.4f);}
            else{Add(area,"window-side corner",30.8f,y+.7f,z+6.7f);Add(area,"rear bedroom corner",34.6f,y+.7f,z+7);Add(area,"bathroom rear corner",39,y+.7f,z+7.2f);Add(area,"desk side",37,y+.7f,z+2.8f);Add(area,"bed foot",31,y+.7f,z+2.8f);}
        }
        foreach(string name in new[]{"Reception counter","Folding table","Staff table","Washers","Linen racks","Reception sofa"})foreach(var c in solids.Where(c=>c.name==name))Prop("Motel services",c,name,Vector2.zero,new Vector2(0,1.6f),new Vector2(0,-1.6f));
        foreach(var v in new[]{new Vector3(12,1,99),new Vector3(18,1,107),new Vector3(22,1,108),new Vector3(25,1,99),new Vector3(33,1,108),new Vector3(36,1,99)})Add("Motel services","service room corner",v.x,v.y,v.z);
        foreach(var c in solids.Where(c=>c.name=="Seat"&&c.transform.parent.name.StartsWith("Changing bench")))Prop("Change rooms",c,c.transform.parent.name,new Vector2(-.9f,0),new Vector2(.9f,0));
        foreach(var c in solids.Where(c=>c.name=="Cubby shelf"&&c.bounds.center.y>1.5f&&c.bounds.center.y<2))for(int i=0;i<5;i++)Add("Change rooms","open cubby",c.bounds.min.x+1+i*2,c.bounds.max.y+.4f,c.bounds.center.z);
        foreach(var c in solids.Where(c=>c.name=="Freezer lid"||c.name=="Snack serving counter"))Prop("Food vendor",c,c.name,new Vector2(-.9f,0),new Vector2(.9f,0));
        foreach(var c in solids.Where(c=>c.name=="Supply shelf"&&c.bounds.center.y<2))Prop("Food vendor",c,"supply shelf",new Vector2(0,.9f));
        Add("Food vendor","stockroom corner",-17,1,107.5f);Add("Food vendor","beside fryer",-13,1,96);Add("Food vendor","freezer return",-21.5f,1,99);
        foreach(var c in solids.Where(c=>c.name=="Lounge seat"))Prop("Pool",c,"pool lounger",new Vector2(0,-.4f));
        Add("Pool","shallow basin corner",-33,-.8f,59);Add("Pool","deep basin corner",-33,-2.4f,79);Add("Pool","deep basin east corner",-19,-2.4f,78);Add("Pool","entry steps side",-19,-.8f,57);
        var golf=block.transform.Find("Env/"+RailMotelLeisure.Root+"/Mini golf");
        foreach(var hole in golf.Cast<Transform>().Where(t=>t.name.StartsWith("Golf hole")))
        {
            var m=hole.Find("Unique putting outline").GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var tr=m.triangles;
            var candidates=new List<Vector3>();for(int i=0;i<tr.Length;i+=3)if(Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]).normalized.y>.9f){var center=(v[tr[i]]+v[tr[i+1]]+v[tr[i+2]])/3;candidates.Add(hole.TransformPoint(center)+Vector3.up*.5f);}
            foreach(var q in candidates.OrderBy(p=>p.z))Add("Golf",hole.name,q.x,q.y,q.z);
        }
        foreach(var c in solids.Where(c=>c.name=="Counter top"&&c.transform.parent.name=="Golf shack interior"))Prop("Golf shack",c,"golf counter",new Vector2(-1.3f,0),new Vector2(1.3f,0));
        Add("Golf shack","club rack side",-18.3f,.8f,115.6f);Add("Golf shack","rear corner",-13.6f,.8f,112.9f);Add("Golf shack","counter return",-16,.8f,115.7f);
        foreach(var c in solids.Where(c=>c.name=="Bench seat"&&c.transform.parent.name=="Roller rink"))Prop("Rink",c,"covered bench "+c.bounds.center.z,new Vector2(-.9f,0),new Vector2(.9f,0));
        foreach(var c in solids.Where(c=>c.name=="Rental counter"))Prop("Rink",c,"skate rental counter",new Vector2(-3,0),Vector2.zero,new Vector2(3,0));
        foreach(var q in new[]{new Vector3(12,.8f,121),new Vector3(40,.8f,143),new Vector3(29,.8f,152),new Vector3(38,.8f,148)})Add("Rink","rink or rental corner",q.x,q.y,q.z);
        var parking=block.transform.Find("Env/"+RailMotelLeisure.Root+"/Arrival and public parking");
        foreach(var bus in parking.Cast<Transform>().Where(t=>t.Find("Bus roof")))
        {
            foreach(var local in new[]{new Vector3(-2.6f,.8f,-3.3f),new Vector3(2.6f,.8f,4.5f),new Vector3(0,3.5f,2.5f)}){var q=bus.TransformPoint(local);Add("Arrival",bus.name+(local.y>3?" roof climb":" parking return"),q.x,q.y,q.z,local.x<0?RailSpawnKind.Coal:RailSpawnKind.Gold);}
        }
        foreach(var c in solids.Where(c=>c.name=="Tree trunk"&&c.bounds.center.z<50))foreach(float x in new[]{-1.2f,1.2f})Add("Arrival","tree root "+c.bounds.center.ToString("F0"),c.bounds.center.x+x,.8f,c.bounds.center.z);
        foreach(var c in solids.Where(c=>c.name=="Bench seat"&&c.transform.parent==parking))Prop("Arrival",c,"bus waiting bench",new Vector2(-.9f,0),new Vector2(.9f,0));
    }
    static List<string> Build()
    {
        Init();Sites();var log=new List<string>();
        var quotas=new Dictionary<string,(int coal,int gold)>();foreach(int id in new[]{101,102,104,202,204,207})quotas["Room "+id]=(2,3);
        quotas["Motel services"]=(5,6);quotas["Change rooms"]=(3,4);quotas["Food vendor"]=(3,4);quotas["Pool"]=(4,4);quotas["Golf"]=(7,9);quotas["Golf shack"]=(2,2);quotas["Rink"]=(5,7);quotas["Arrival"]=(7,10);
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})foreach(var quota in quotas)
        {
            int wanted=kind==RailSpawnKind.Coal?quota.Value.coal:quota.Value.gold,count=0;
            foreach(var site in sites.Where(s=>s.area==quota.Key&&(!s.only.HasValue||s.only.Value==kind)).OrderBy(s=>kind==RailSpawnKind.Gold&&s.area=="Change rooms"&&s.name=="open cubby"?0:1))
            {
                int cap=site.area=="Golf"||site.name.StartsWith("Changing bench")||site.name.StartsWith("covered bench")||site.name.StartsWith("tree root")?1:site.name=="open cubby"||site.name=="skate rental counter"?2:99;
                if(plan.Count(p=>p.kind==kind&&p.area==site.area&&p.name==site.name)>=cap)continue;
                if(!Resolve(site,kind,out var pos)||plan.Any(p=>Vector3.Distance(p.position,pos)<(p.kind==kind?1.8f:.8f)))continue;
                plan.Add(new Placement{kind=kind,area=site.area,name=site.name,position=pos});if(++count==wanted)break;
            }
            log.Add((count==wanted?"PASS ":"FAIL ")+kind+" "+quota.Key+" "+count+"/"+wanted);
        }
        var enemySites=new[]{
            new Vector3(-42,1,18),new Vector3(-22,1,12),new Vector3(-11,1,40),new Vector3(-35,1,39),new Vector3(-40,1,71),new Vector3(-20,-.8f,59),new Vector3(-11,1,76),new Vector3(-30,-2.4f,75),
            new Vector3(12,1,12),new Vector3(36,1,15),new Vector3(18,1,36),new Vector3(43,1,43),new Vector3(18,1,73),new Vector3(31,1,59),new Vector3(26,4.3f,70),new Vector3(43,1,77),
            new Vector3(-40,1,89),new Vector3(-32,1,101),new Vector3(-13,1,97),new Vector3(-43,1,119),new Vector3(-24,1,123),new Vector3(-40,1,146),new Vector3(-12,1,145),new Vector3(-26,1,155),
            new Vector3(19,1,88),new Vector3(35,1,100),new Vector3(24,4.3f,102),new Vector3(43,1,113),new Vector3(15,1,124),new Vector3(36,1,139),new Vector3(13,1,151),new Vector3(37,1,155)};
        string[] enemyNames={"west bus perimeter","west coach approach","pool approach gate","west waiting area","west pool deck","shallow pool basin","east pool deck","deep pool basin","east bus approach","east coach perimeter","east parking aisle","motel approach tree","motel courtyard","room 101","upper motel walk","motel service lane","pool rear deck","changing rooms","food vendor","golf west entry","golf central aisle","duck green","golf east return","golf departure trees","motel north courtyard","linen room","upper room 207","dumpster service yard","rink west floor","rink east floor","rental west approach","rental north exit"};
        for(int i=0;i<enemySites.Length;i++)
        {
            bool found=false;foreach(var delta in new[]{Vector3.zero,Vector3.right,Vector3.left,Vector3.forward,Vector3.back,Vector3.right*2,Vector3.left*2,Vector3.forward*2,Vector3.back*2})
            {var site=new Site{ray=enemySites[i]+delta};if(!Resolve(site,RailSpawnKind.Enemy,out var p)||plan.Any(q=>q.kind==RailSpawnKind.Enemy&&Vector3.Distance(q.position,p)<2))continue;plan.Add(new Placement{kind=RailSpawnKind.Enemy,area="Quadrant "+(i/8),name=enemyNames[i],position=p});found=true;break;}
            log.Add((found?"PASS ":"FAIL ")+"Enemy support/navigation "+enemyNames[i]);
        }
        return log;
    }
    public static void Preview()
    {
        var log=Build();File.WriteAllLines("docs/validation/motel-leisure/spawn-plan.txt",log.Concat(plan.Select(p=>p.kind+" | "+p.area+" | "+p.name+" | "+p.position.ToString("F3"))));
        Debug.Log("Plan: "+string.Join(", ",plan.GroupBy(p=>p.kind).Select(g=>g.Key+"="+g.Count()))+"\n"+string.Join("\n",log.Where(l=>l.StartsWith("FAIL"))));
    }
    public static void Apply()
    {
        var log=Build();if(log.Any(l=>l.StartsWith("FAIL")))throw new Exception("Review spawn-plan failures before replacing markers");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();Undo.RegisterFullObjectHierarchyUndo(block.gameObject,"Replace motel spawn candidates");RailSpawnPool.Migrate(block);
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {
            foreach(var old in RailSpawnPool.Candidates(block,kind))Undo.DestroyObjectImmediate(old.gameObject);
            var group=block.transform.Find(kind+" Spawns");if(!group){group=new GameObject(kind+" Spawns").transform;group.SetParent(block.transform,false);}
            int i=0;var added=new List<Transform>();foreach(var p in plan.Where(p=>p.kind==kind))
            {var t=RailSpawnerPrefabs.Instantiate(kind,stage.scene).transform;Undo.RegisterCreatedObjectUndo(t.gameObject,"Replace motel spawn candidates");t.SetParent(group,false);t.position=p.position;t.name=kind+" "+(++i).ToString("D2")+" - "+p.area+" - "+p.name;PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);added.Add(t);}
            RailSpawnPool.SetPoints(block.spawnCandidates,kind,added.ToArray());
        }
        EditorUtility.SetDirty(block);PrefabUtility.SaveAsPrefabAsset(block.gameObject,stage.assetPath);AssetDatabase.SaveAssets();Validate();
    }
    public static void Validate()
    {
        Init();var log=new List<string>();void Check(bool ok,string s)=>log.Add((ok?"PASS ":"FAIL ")+s);
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {var points=RailSpawnPool.Candidates(block,kind);Check(points.Length==(kind==RailSpawnKind.Coal?48:kind==RailSpawnKind.Gold?64:32),kind+" candidate count "+points.Length);foreach(var t in points){Check(Valid(t.position,kind),"Supported and clear "+t.name);Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native reference "+t.name);}}
        for(int crew=1;crew<=8;crew++)for(int seed=0;seed<100;seed++)
        {
            foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold}){int n=kind==RailSpawnKind.Coal?RailResourceBalance.CoalCount(crew):RailResourceBalance.GoldCount(crew);var selected=RailSpawnPool.Select(block,kind,seed,n);Check(selected.Length==n&&selected.Distinct().Count()==n,kind+" crew="+crew+" seed="+seed);}
            var enemy=RailEnemySpawnBalance.Select(block,seed,crew);int quota=RailEnemySpawnBalance.PerQuadrant(crew);
            Check(enemy.Length==4*quota&&enemy.Distinct().Count()==enemy.Length&&Enumerable.Range(0,4).All(q=>enemy.Count(t=>RailEnemySpawnBalance.Quadrant(block,t)==q)==quota)&&enemy.All(a=>enemy.All(b=>a==b||Vector2.Distance(new Vector2(a.position.x,a.position.z),new Vector2(b.position.x,b.position.z))>=15)),"Enemy quota/spacing crew="+crew+" seed="+seed);
        }
        File.WriteAllLines("docs/validation/motel-leisure/spawn-revision-checks.txt",log);Debug.Log(log.Count+" spawn checks; "+log.Count(l=>l.StartsWith("FAIL"))+" failures\n"+string.Join("\n",log.Where(l=>l.StartsWith("FAIL")).Take(20)));
    }
}
#endif
