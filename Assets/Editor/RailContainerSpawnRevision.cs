#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
using Object=UnityEngine.Object;

/// <summary>Approved redistribution over the current saved yard, with no geometry regeneration.</summary>
public static class RailContainerSpawnRevision
{
    public const string Evidence="docs/validation/container-yard-redistribution";
    const string GroundMaterial="Assets/DuckRails/ContainerYard/Polish/Yard ground broad repeat.mat";
    class Site { public string area,name,key; public Vector3 ray; public Collider surface; public bool high; public int order; }
    class Placement { public RailSpawnKind kind; public string area,name,key; public Vector3 position; public bool high; }
    static RailBlock block;
    static PhysicsScene physics;
    static Collider[] solids;
    static readonly Collider[] hits=new Collider[128];
    static CrawlGraph graph;
    static List<Site> sites;
    static List<Placement> plan;
    static Bounds Envelope(RailSpawnKind k)=>k==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,Vector3.one*.8f):RailLootPlacementWindow.ItemBounds(k==RailSpawnKind.Gold);
    static string Area(Vector3 p)=>p.z<45?"South machinery":p.z<118?(p.x<0?"West maze":"East maze"):(p.x<0?"North repair":"North loading");
    static void Init()
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||stage==null||stage.assetPath!=RailContainerYard.PrefabPath||EditorApplication.isPlaying)throw new Exception("Open Duck Rails Container Yard in Prefab Mode");
        block=stage.prefabContentsRoot.GetComponent<RailBlock>();physics=stage.scene.GetPhysicsScene();Physics.SyncTransforms();
        solids=block.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();graph=CrawlGraph.Load(block.graph);sites=new();plan=new();Directory.CreateDirectory(Evidence);
        var control=solids.First(c=>c.name=="Block ground");if(physics.OverlapBox(control.bounds.center,Vector3.one*.1f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0)throw new Exception("Physics positive control failed");
    }
    static bool Valid(Vector3 p,RailSpawnKind kind)
    {
        var bounds=Envelope(kind);
        foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
        {var foot=p+bounds.center+new Vector3(x,-bounds.extents.y,z);if(!solids.Any(c=>c.Raycast(new Ray(foot+Vector3.up*.12f,Vector3.down),out _,.3f)))return false;}
        return physics.OverlapBox(p+bounds.center,bounds.extents*.97f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0 && (kind!=RailSpawnKind.Enemy||RailLootPlacementWindow.EnemyLocation(block,p,graph));
    }
    static bool Resolve(Site s,RailSpawnKind kind,out Vector3 p)
    {
        p=default;var ray=new Ray(s.ray,Vector3.down);
        var found=(s.surface?new[]{s.surface}:solids).Select(c=>c.Raycast(ray,out var h,1.4f)?h:default).Where(h=>h.collider&&h.normal.y>.995f).OrderBy(h=>h.distance).FirstOrDefault();
        if(!found.collider)return false;p=found.point+Vector3.up*(.05f-Envelope(kind).min.y);return Valid(p,kind);
    }
    static void AddSurface(Collider c)
    {
        var b=c.bounds;if(b.max.y>6.15f||b.size.x<.65f||b.size.z<.65f)return;
        string key=AnimationUtility.CalculateTransformPath(c.transform,block.transform);string area=Area(b.center);
        // Sampling stays on named authored props; no broad floor scatter or runtime navigation changes.
        float[] fractions={.15f,.35f,.65f,.85f};
        foreach(float a in fractions)foreach(float d in new[]{.2f,.8f,.5f})
        {
            var ray=new Vector3(Mathf.Lerp(b.min.x,b.max.x,a),b.max.y+.55f,Mathf.Lerp(b.min.z,b.max.z,d));
            sites.Add(new Site{area=area,name=c.name+" / "+c.transform.parent.name,key=key,ray=ray,surface=c,high=b.max.y>1.65f,order=sites.Count});
        }
        sites.Add(new Site{area=area,name=c.name+" / "+c.transform.parent.name,key=key,ray=b.center+Vector3.up*(b.extents.y+.55f),surface=c,high=b.max.y>1.65f,order=sites.Count});
    }
    static void AddGround(string area,string name,string key,Vector3 p)
    {sites.Add(new Site{area=area,name=name,key=key,ray=p+Vector3.up*.8f,high=false,order=sites.Count});}
    static void Sites()
    {
        var names=new HashSet<string>{"Closed container collision","Walkable roof","Interior floor","Container floor","Container roof","Fallen container","Pallet pile","Rear counterweight","Booth roof","Wheel bogie","Bench top","Lower shelf","Dock slab","Trailer deck","Concrete saddle base","Pallet landing","Cab upper body","Heavy chassis"};
        foreach(var c in solids.Where(c=>names.Contains(c.name)))AddSurface(c);
        foreach(var c in solids.Where(c=>c.name=="Closed container collision"&&c.bounds.min.y<.2f))
        {
            var b=c.bounds;var key=AnimationUtility.CalculateTransformPath(c.transform,block.transform)+" ground";
            foreach(float x in new[]{b.min.x-.7f,b.max.x+.7f})foreach(float z in new[]{b.min.z+.65f,b.max.z-.65f})
                AddGround(Area(b.center),"Container end recess",key,new Vector3(x,0,z));
        }
        // Work areas and tank service corners, kept out of stairs and the through-route.
        foreach(var p in new[]{new Vector3(-40,0,128),new(-40,0,131),new(-40,0,138),new(-38,0,140.8f),new(-34,0,140.8f),new(-28,0,140.8f),new(-25,0,138),new(-25,0,130)})
            AddGround("North repair","Shed service corner","shed ground",p);
        foreach(var p in new[]{new Vector3(33,0,128),new(33,0,139.5f),new(37.8f,0,129),new(38.2f,0,138.5f),new(43.5f,0,130),new(43.5f,0,137),new(29,0,126),new(29,0,140),new(30,0,145.8f)})
            AddGround("North loading","Tank or loading service corner","tank loading ground",p);
        foreach(var c in solids.Where(c=>c.name=="Wheel bogie"||c.name=="Heavy chassis"||c.name=="Booth body"||c.name=="Rear counterweight"))
        {var b=c.bounds;foreach(var d in new[]{new Vector3(b.extents.x+.7f,0,0),new Vector3(-b.extents.x-.7f,0,0),new Vector3(0,0,b.extents.z+.7f),new Vector3(0,0,-b.extents.z-.7f)})AddGround("South machinery",c.name+" service return",c.name+" ground",new Vector3(b.center.x,0,b.center.z)+d);}
    }
    static List<string> Build()
    {
        Init();Sites();var log=new List<string>();
        var quotas=new[]{("South machinery",20,16,8),("West maze",54,44,22),("East maze",54,44,22),("North repair",20,18,9),("North loading",20,20,10)};
        // Give interior floors, worktops and named service corners first choice; gold reserves half its pool for climbs.
        foreach(var kind in new[]{RailSpawnKind.Gold,RailSpawnKind.Coal})foreach(var q in quotas)
        {
            int wanted=kind==RailSpawnKind.Coal?q.Item2:q.Item3;
            int low=kind==RailSpawnKind.Gold?wanted-q.Item4:q.Item1.Contains("maze")?24:q.Item1=="South machinery"?16:12;
            for(int tier=0;tier<2;tier++)
            {
                int target=tier==0?low:wanted-low;int count=0;
                var choices=sites.Where(s=>s.area==q.Item1&&s.high==(tier==1)).Select(s=>(site:s,ok:Resolve(s,kind,out var p),pos:p)).Where(s=>s.ok).ToList();
                while(count<target)
                {
                    var choice=choices.Where(s=>!plan.Any(p=>p.kind!=RailSpawnKind.Enemy&&Vector3.Distance(p.position,s.pos)<(p.kind==kind?1.8f:.8f)))
                        .OrderBy(s=>plan.Count(p=>p.kind==kind&&p.key==s.site.key)).ThenBy(s=>s.site.name.Contains("floor")||s.site.name.Contains("Bench")?0:1)
                        .ThenBy(s=>(s.site.order*71+(int)kind*113)%997).FirstOrDefault();
                    if(choice.site==null)break;
                    plan.Add(new Placement{kind=kind,area=choice.site.area,name=choice.site.name,key=choice.site.key,position=choice.pos,high=tier==1});choices.Remove(choice);count++;
                }
                log.Add((count==target?"PASS ":"FAIL ")+kind+" "+q.Item1+(tier==0?" accessible ":" climb ")+count+"/"+target);
            }
        }
        var enemySites=new[]{
            new Vector3(-40,.8f,18),new(-18,.8f,15),new(-10,.8f,36),new(-39,.8f,35),new(-43,.8f,61),new(-18,.8f,63),new(-29,.8f,73),new(-10,.8f,77),
            new Vector3(12,.8f,12),new(37,.8f,20),new(10,.8f,38),new(37,.8f,40),new(29,.8f,57),new(18,.8f,62),new(43,.8f,74),new(12,.8f,77),
            new Vector3(-10,.8f,89),new(-30,.8f,88),new(-42,.8f,104),new(-28,.8f,108),new(-24,.8f,122),new(-33,.8f,130),new(-16,3.6f,135),new(-35,.8f,144),
            new Vector3(0,.8f,80),new(29,.8f,90),new(43,.8f,107),new(18,.8f,110),new(24,.8f,122),new(25,2.1f,136),new(38,.8f,133),new(32,.8f,146)};
        string[] names={"west outer storage","dispatch return","stacker approach","west machine perimeter","west outside lane","west passage","west terrace return","west rail return","east outer storage","east supply lane","crane inner approach","crane outer exit","east cross aisle","east passage","east outside lane","east rail return","west search approach","west middle return","west departure stacks","west departure lane","forklift approach","repair shed","fallen roof","shed rear return","railway crossing","east middle return","east departure stacks","east departure lane","truck approach","trailer interior","propane service aisle","dock departure"};
        for(int i=0;i<enemySites.Length;i++)
        {
            bool found=false;
            foreach(var offset in new[]{Vector3.zero,Vector3.right,Vector3.left,Vector3.forward,Vector3.back,Vector3.right*2,Vector3.left*2,Vector3.forward*2,Vector3.back*2,Vector3.right*4,Vector3.left*4,Vector3.forward*4,Vector3.back*4})
            {var s=new Site{ray=enemySites[i]+offset};if(!Resolve(s,RailSpawnKind.Enemy,out var p)||plan.Any(a=>a.kind==RailSpawnKind.Enemy&&Vector3.Distance(a.position,p)<3))continue;plan.Add(new Placement{kind=RailSpawnKind.Enemy,area=Area(p),name=names[i],key=names[i],position=p,high=p.y>1.65f});found=true;break;}
            log.Add((found?"PASS ":"FAIL ")+"Enemy supported/nav "+names[i]);
        }
        return log;
    }
    public static void Preview()
    {
        var log=Build();File.WriteAllLines(Evidence+"/plan.txt",log.Concat(plan.Select(p=>p.kind+" | "+p.area+" | "+p.name+" | "+p.position.ToString("F3"))));
        Debug.Log("Yard plan "+string.Join(", ",plan.GroupBy(p=>p.kind).Select(g=>g.Key+"="+g.Count()))+"\n"+string.Join("\n",log.Where(s=>s.StartsWith("FAIL"))));
    }
    public static void Apply()
    {
        var log=Build();if(log.Any(s=>s.StartsWith("FAIL")))throw new Exception("Resolve proposed placement shortfalls before applying");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();Directory.CreateDirectory("Temp/container-yard-redistribution");File.Copy(RailContainerYard.PrefabPath,"Temp/container-yard-redistribution/before.prefab",true);
        Undo.RegisterFullObjectHierarchyUndo(block.gameObject,"Redistribute Container Yard spawns");
        var ground=RailBlock.FindEnvironment(block.transform,RailContainerYard.RootName).Find("Block ground").GetComponent<MeshRenderer>();
        var mat=AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
        if(!mat){mat=new Material(ground.sharedMaterial){name="Yard ground broad repeat"};mat.SetTextureScale("_BaseMap",Vector2.one*.25f);mat.SetTextureScale("_MainTex",Vector2.one*.25f);AssetDatabase.CreateAsset(mat,GroundMaterial);}
        ground.sharedMaterial=mat;
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {
            var old=RailSpawnPool.Candidates(block,kind);var entries=plan.Where(p=>p.kind==kind).ToArray();
            var group=block.transform.Find(kind+" Spawns");if(!group){group=new GameObject(kind+" Spawns").transform;group.SetParent(block.transform,false);}
            var points=new List<Transform>();
            for(int i=0;i<entries.Length;i++)
            {
                var t=i<old.Length?old[i]:RailSpawnerPrefabs.Instantiate(kind,stage.scene).transform;
                if(i>=old.Length){Undo.RegisterCreatedObjectUndo(t.gameObject,"Add yard enemy candidate");t.SetParent(group,false);}
                var p=entries[i];t.position=p.position;t.rotation=Quaternion.identity;t.name=kind+" "+(i+1).ToString("D3")+" - "+p.area+" - "+p.name+(p.high?" (climb)":"");
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);points.Add(t);
            }
            for(int i=entries.Length;i<old.Length;i++)Undo.DestroyObjectImmediate(old[i].gameObject);
            RailSpawnPool.SetPoints(block.spawnCandidates,kind,points.ToArray());
        }
        EditorUtility.SetDirty(block);PrefabUtility.SaveAsPrefabAsset(block.gameObject,stage.assetPath);AssetDatabase.SaveAssets();Validate();
    }
    public static void Validate()
    {
        Init();var log=new List<string>();void Check(bool ok,string label)=>log.Add((ok?"PASS ":"FAIL ")+label);
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {
            var points=RailSpawnPool.Candidates(block,kind);Check(points.Length==(kind==RailSpawnKind.Coal?168:kind==RailSpawnKind.Gold?142:32),kind+" saved count "+points.Length);
            foreach(var t in points){Check(Valid(t.position,kind),"Footprint support/clearance/navigation "+t.name);Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native marker prefab "+t.name);}
            Check(points.Select(t=>t.position).Distinct().Count()==points.Length,"Unique "+kind+" positions");
        }
        for(int crew=1;crew<=8;crew++)for(int seed=0;seed<100;seed++)
        {
            foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold}){int wanted=kind==RailSpawnKind.Coal?RailResourceBalance.CoalCount(crew):RailResourceBalance.GoldCount(crew);var points=RailSpawnPool.Select(block,kind,seed,wanted);Check(points.Length==wanted&&points.Distinct().Count()==wanted,kind+" crew/seed "+crew+"/"+seed);}
            var enemies=RailEnemySpawnBalance.Select(block,seed,crew);int quota=RailEnemySpawnBalance.PerQuadrant(crew);
            Check(enemies.Length==quota*4&&enemies.Distinct().Count()==enemies.Length&&Enumerable.Range(0,4).All(q=>enemies.Count(t=>RailEnemySpawnBalance.Quadrant(block,t)==q)==quota)&&enemies.All(a=>enemies.All(b=>a==b||Vector2.Distance(new Vector2(a.position.x,a.position.z),new Vector2(b.position.x,b.position.z))>=15)),"Enemy count/quadrant/spacing "+crew+"/"+seed);
        }
        Check(block.coalCount==6&&block.goldCount==2&&block.enemyCount==5,"Per-run authoring fields retained");
        var ground=RailBlock.FindEnvironment(block.transform,RailContainerYard.RootName).Find("Block ground").GetComponent<Renderer>();
        Check(ground.sharedMaterial.mainTextureScale==Vector2.one*.25f,"Ground texture repeats four times less per axis");
        File.WriteAllLines(Evidence+"/checks.txt",log);var failures=log.Where(s=>s.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(string.Join("\n",failures.Take(25)));Debug.Log("YARD REDISTRIBUTION: "+log.Count+" checks passed");
    }
}
#endif
