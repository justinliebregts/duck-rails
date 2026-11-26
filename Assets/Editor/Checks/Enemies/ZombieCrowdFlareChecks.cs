using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ZombieCrowdFlareChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    delegate Vector3 NeighborLimit(Vector3 from, Vector3 to, out Enemy blocker, bool snapshot);
    static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }

    [MenuItem("Tools/Duck Rails/Validation/Enemies/Zombie spacing and flare exits")]
    public static void Run()
    {
        var a = new GameObject("Moving zombie").AddComponent<Enemy>();
        var b = new GameObject("Stationary zombie").AddComponent<Enemy>();
        try
        {
            var awake = typeof(Enemy).GetMethod("Awake", Private);
            awake.Invoke(a, null);
            awake.Invoke(b, null);
            if (!Enemy.Active.Contains(b)) Enemy.Active.Add(b);
            Vector3 origin = new Vector3(8000, 0, 0);
            b.transform.position = origin;
            a.transform.position = origin + Vector3.left * 2;
            a.SetRootPose(origin + Vector3.right * 2, Quaternion.identity);
            float gap = origin.x - a.Root.position.x;
            Check(gap >= .71f && gap < .74f, "Fast sweep must stop at body radius plus clearance");
            Check(b.Root.position == origin, "Stationary zombie displaced");
            a.transform.position = origin + Vector3.left * .2f;
            a.SetRootPose(origin, Quaternion.identity);
            Check(a.Root.position.x < origin.x - .19f, "Overlap got deeper");
            a.SetRootPose(origin + Vector3.left * 2, Quaternion.identity);
            Check(a.Root.position == origin + Vector3.left * 2, "Could not escape an old overlap");
            a.transform.position += Vector3.up * 3;
            a.SetRootPose(origin + Vector3.up * 3 + Vector3.right * 2, Quaternion.identity);
            Check(a.Root.position.x > origin.x, "Body on lower floor blocked another floor");
            b.IsDormant = true;
            a.transform.position = origin + Vector3.left * 2;
            a.SetRootPose(origin, Quaternion.identity);
            Check(a.Root.position == origin, "Pooled zombie reserved a spot");
        }
        finally
        {
            Enemy.Active.Remove(a);
            Enemy.Active.Remove(b);
            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
        }
        CheckEscape();
        CheckClimbRouteContact();
        CheckContactReorientation();
        CheckCrowdLanding();
        CheckIndoorMovement();
        CheckNetworkSpacing();
        CheckRemoteLanding();
        CheckAllocations();
        Debug.Log("ZombieCrowdFlareChecks PASS");
    }

    static void CheckAllocations()
    {
        const int nodes = 512;
        var graph = new CrawlGraph {NodeCount=nodes, NodePos=new float[nodes*3], NodeNormal=new int[nodes],
            EdgeStart=new int[nodes+1], EdgeTarget=new int[(nodes-1)*2]};
        int edge = 0;
        for (int i=0;i<nodes;i++)
        {
            graph.NodePos[i*3]=i*.02f;
            graph.NodeNormal[i]=CrawlGraph.PackNormal(Vector3.up);
            graph.EdgeStart[i]=edge;
            if(i>0) graph.EdgeTarget[edge++]=i-1;
            if(i+1<nodes) graph.EdgeTarget[edge++]=i+1;
        }
        graph.EdgeStart[nodes]=edge;
        var threats=(List<Vector3>)typeof(Enemy).GetField("threats",Private).GetValue(null);
        var visited=(HashSet<int>)typeof(Enemy).GetField("visited",Private).GetValue(null);
        var blocked=new HashSet<int>{nodes-1};
        var plan=(Func<CrawlGraph,int,bool,float,int,HashSet<int>,int>)Delegate.CreateDelegate(
            typeof(Func<CrawlGraph,int,bool,float,int,HashSet<int>,int>),typeof(Enemy).GetMethod("SelectEscape",Private));
        var actor = new GameObject("Allocation crowd mover").AddComponent<Enemy>();
        var neighbor = new GameObject("Allocation crowd neighbor").AddComponent<Enemy>();
        typeof(Enemy).GetMethod("Awake",Private).Invoke(actor,null);
        typeof(Enemy).GetMethod("Awake",Private).Invoke(neighbor,null);
        if(!Enemy.Active.Contains(neighbor)) Enemy.Active.Add(neighbor);
        var limit=(NeighborLimit)Delegate.CreateDelegate(typeof(NeighborLimit),actor,
            typeof(Enemy).GetMethod("LimitNeighbors",Private));
        Vector3 origin = new Vector3(8000,0,0);
        neighbor.transform.position=origin;
        try
        {
            threats.Clear();
            for(int i=0;i<64;i++) threats.Add(new Vector3(-i*.01f,0,0));
            for(int i=0;i<20;i++)
            {
                plan(graph,0,true,6,-1,blocked);
                limit(origin+Vector3.left*2,origin+Vector3.right*2,out _,false);
            }
            Check(visited.Count == 384,"Flare graph exploration respects its 384-node limit");
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++)
            {
                plan(graph,0,true,6,-1,blocked);
                limit(origin+Vector3.left*2,origin+Vector3.right*2,out _,false);
            }
            long delta=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Check(delta==0,"Warmed spacing and 64-flare bounded searches allocated "+delta+" bytes");
            Debug.Log("ZombieCrowdFlareChecks allocation PASS, 0 bytes in 100 warmed searches and movement queries");
        }
        finally
        {
            threats.Clear();
            Enemy.Active.Remove(actor);
            Enemy.Active.Remove(neighbor);
            Object.DestroyImmediate(actor.gameObject);
            Object.DestroyImmediate(neighbor.gameObject);
        }
    }

    static void CheckNetworkSpacing()
    {
        var actor = new GameObject("Remote crowd interpolation");
        var other = new GameObject("Remote crowd blocker").AddComponent<Enemy>();
        var enemy = actor.AddComponent<Enemy>();
        var sync = actor.AddComponent<EnemyNetSync>();
        typeof(Enemy).GetMethod("Awake", Private).Invoke(enemy, null);
        typeof(Enemy).GetMethod("Awake", Private).Invoke(other, null);
        typeof(EnemyNetSync).GetMethod("Awake", Private).Invoke(sync, null);
        if (!Enemy.Active.Contains(other)) Enemy.Active.Add(other);
        var apply = (Action<Transform,float>)Delegate.CreateDelegate(typeof(Action<Transform,float>),
            sync, typeof(EnemyNetSync).GetMethod("ApplySample", Private));
        void Set(string field, object value) => typeof(EnemyNetSync).GetField(field, Private).SetValue(sync, value);
        Vector3 origin = new Vector3(8000,0,0);
        other.transform.position = origin;
        try
        {
            foreach (int hz in new[] {36,72,90,144})
            {
                foreach (bool recoveringHit in new[] {false,true})
                {
                    enemy.Teleport(origin + Vector3.left * 2, Quaternion.identity);
                    sync.ResetRemotePose(origin + Vector3.right * 2, Quaternion.identity);
                    Set("_hasSample",true);
                    Set("_reconciling",recoveringHit);
                    for (int i=0;i<hz*3;i++) apply(enemy.Root,1f/hz);
                    Check(Vector3.Distance(enemy.Root.position,origin+Vector3.right*2)<.01f,
                        "Remote convergence past a blocked interpolated route at "+hz+" Hz, recovering hit="+recoveringHit);
                    Check(!(bool)typeof(EnemyNetSync).GetField("_reconciling",Private).GetValue(sync),
                        "Completed crowd reconciliation releases defensive hit protection");
                    Check(other.Root.position == origin,"Remote correction displaced another zombie");
                }
                enemy.Teleport(origin + Vector3.left * 2, Quaternion.identity);
                sync.ResetRemotePose(origin, Quaternion.identity);
                Set("_hasSample",true);
                for(int i=0;i<hz*2;i++) apply(enemy.Root,1f/hz);
                Check(Vector3.Distance(enemy.Root.position,origin)>.71f,"Occupied snapshot destination must remain blocked");
                other.transform.position = origin + Vector3.forward*3;
                for(int i=0;i<hz*2;i++) apply(enemy.Root,1f/hz);
                Check(Vector3.Distance(enemy.Root.position,origin)<.01f,"Converges once the neighbor releases the destination");
                other.transform.position = origin;
                enemy.Teleport(origin + Vector3.left * 2, Quaternion.identity);
                sync.ResetRemotePose(origin + Vector3.right * 2, Quaternion.identity);
                Set("_hasSample",true);
                Set("_pendingHit",1);
                for(int i=0;i<hz*2;i++) apply(enemy.Root,1f/hz);
                Check(enemy.Root.position.x<origin.x && sync.ProtectsLocalHitter,
                    "Unconfirmed local hit retains protection and cannot be snapped through the crowd");
            }
        }
        finally
        {
            Enemy.Active.Remove(enemy);
            Enemy.Active.Remove(other);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(other.gameObject);
        }
        Debug.Log("ZombieCrowdFlareChecks remote spacing PASS");
    }

    public static void CheckRemoteLanding()
    {
        var actor = new GameObject("Remote Smasher descending around a body");
        actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        actor.AddComponent<ZombieSmasherJump>();
        var sync = actor.AddComponent<EnemyNetSync>();
        var other = new GameObject("Stationary landing blocker").AddComponent<Enemy>();
        typeof(Enemy).GetMethod("Awake", Private).Invoke(enemy, null);
        typeof(Enemy).GetMethod("Awake", Private).Invoke(other, null);
        typeof(EnemyNetSync).GetMethod("Awake", Private).Invoke(sync, null);
        if (!Enemy.Active.Contains(other)) Enemy.Active.Add(other);
        var apply = (Action<Transform,float>)Delegate.CreateDelegate(typeof(Action<Transform,float>),
            sync, typeof(EnemyNetSync).GetMethod("ApplySample", Private));
        var body = typeof(Enemy).GetMethod("SpacingBody", Private);
        object[] a = {Vector3.zero,0f,0f}, b = {Vector3.zero,0f,0f};
        body.Invoke(enemy,a); body.Invoke(other,b);
        float gap = (float)a[2]+(float)b[2]+.12f;
        Vector3 origin = new Vector3(2000,45,0);
        other.transform.position = origin;
        try
        {
            foreach (int hz in new[] {36,72,90,144,360})
            foreach (Vector3 direction in new[] {Vector3.forward,Vector3.back,Vector3.right})
            {
                Vector3 target = origin+direction*(gap+.05f);
                enemy.Teleport(origin+Vector3.up*((float)b[1]+gap+.5f),Quaternion.identity);
                sync.ResetRemotePose(target,Quaternion.identity);
                typeof(EnemyNetSync).GetField("_hasSample",Private).SetValue(sync,true);
                float dt=1f/hz;
                for(int i=0;i<hz*3;i++)
                {
                    Vector3 before=enemy.Root.position;
                    apply(enemy.Root,dt);
                    Vector3 p=enemy.Root.position;
                    float allowed=Vector3.Distance(before,target)*(1-Mathf.Exp(-12f*dt))+.002f;
                    Check(Vector3.Distance(before,p)<=allowed,"Remote landing snaps at "+hz+" Hz, frame "+i);
                    Vector3 offset=p+(Vector3)a[0]-origin-(Vector3)b[0];
                    offset.y-=Mathf.Clamp(offset.y,-(float)a[1],(float)b[1]);
                    Check(offset.magnitude>=gap-.002f,"Remote landing overlaps the lower body");
                }
                Check(Vector3.Distance(enemy.Root.position,target)<.01f,"Remote landing fails to converge at "+hz+
                    " Hz: "+enemy.Root.position.ToString("F4")+" -> "+target.ToString("F4"));
                Check(other.Root.position==origin,"Remote landing displaces its blocker");
            }
        }
        finally
        {
            Enemy.Active.Remove(enemy); Enemy.Active.Remove(other);
            Object.DestroyImmediate(actor); Object.DestroyImmediate(other.gameObject);
        }
        Debug.Log("Remote crowded landing PASS at 36/72/90/144/360 Hz");
    }

    static void CheckCrowdLanding()
    {
        var fixture=new GameObject("Crowded landing fixture");
        Vector3 origin=new Vector3(7500,0,0);
        var floor=new GameObject("Landing floor");floor.transform.SetParent(fixture.transform);floor.layer=6;
        floor.transform.position=origin+Vector3.down*.5f;floor.AddComponent<BoxCollider>().size=new Vector3(20,1,20);
        var actor=new GameObject("Falling Smasher");actor.transform.SetParent(fixture.transform);
        var ground=actor.AddComponent<GroundLocomotion>();
        var enemy=actor.AddComponent<Enemy>();var jump=actor.AddComponent<ZombieSmasherJump>();
        var other=new GameObject("Zombie below landing");other.transform.SetParent(fixture.transform);
        other.AddComponent<GroundLocomotion>();var neighbor=other.AddComponent<Enemy>();
        var managerObject=new GameObject("Landing manager");managerObject.transform.SetParent(fixture.transform);managerObject.SetActive(false);
        var manager=managerObject.AddComponent<EnemyManager>();
        typeof(EnemyManager).GetField("surfaceMask",Private).SetValue(manager,(LayerMask)(1<<6));
        typeof(Enemy).GetMethod("Awake",Private).Invoke(enemy,null);
        typeof(Enemy).GetMethod("Awake",Private).Invoke(neighbor,null);
        typeof(ZombieSmasherJump).GetMethod("Awake",Private).Invoke(jump,null);
        if(!Enemy.Active.Contains(neighbor))Enemy.Active.Add(neighbor);
        var walls=new GameObject("Landing enclosure");walls.transform.SetParent(fixture.transform);
        var wallShapes=new List<BoxCollider>();
        for(int side=0;side<4;side++)
        {
            var wall=new GameObject("Low landing barrier");wall.transform.SetParent(walls.transform);wall.layer=6;
            wall.transform.position=origin+(side==0?Vector3.forward:side==1?Vector3.back:side==2?Vector3.left:Vector3.right)*.65f+Vector3.up*.61f;
            var shape=wall.AddComponent<BoxCollider>();shape.size=side<2?new Vector3(1.4f,1.22f,.1f):new Vector3(.1f,1.22f,1.4f);
            wallShapes.Add(shape);
        }
        var flareObject=new GameObject("Flare beside occupied landing");flareObject.transform.SetParent(fixture.transform);
        flareObject.transform.position=origin+Vector3.forward*2;
        var flare=flareObject.AddComponent<DeathFlare>();
        typeof(DeathFlare).GetMethod("OnEnable",Private).Invoke(flare,null);
        typeof(DeathFlare).GetMethod("Initialize",Private).Invoke(flare,new object[]{0f});
        var ceiling=new GameObject("Intermittent landing ceiling");ceiling.transform.SetParent(fixture.transform);ceiling.layer=6;
        ceiling.AddComponent<BoxCollider>().size=new Vector3(5,.1f,5);ceiling.SetActive(false);
        int impacts=0;
        jump.Landed+=()=>impacts++;
        try
        {
            foreach(int scenario in new[]{0,1,2,3,4,5,6})
            {
                bool sealedLanding=scenario==2 || scenario==6;
                walls.SetActive(scenario>0);
                foreach(var shape in wallShapes)
                {
                    Vector3 size=shape.size;size.y=sealedLanding?3f:1.22f;shape.size=size;
                    Vector3 position=shape.transform.position;position.y=size.y*.5f;shape.transform.position=position;
                }
                neighbor.IsDormant=false;
                neighbor.Teleport(origin,Quaternion.identity);
                enemy.Teleport(origin+Vector3.up*2,Quaternion.identity);
                enemy.SetRemoteState(Enemy.State.Retreat);
                impacts=0;
                bool hopped=false;
                bool interrupted=false;
                int blockedHops=0,releaseCeiling=-1;
                Vector3 previous=enemy.Root.position;
                int frames=72*(scenario==6?28:scenario==2?25:scenario==5?15:6);
                for(int frame=0;frame<frames;frame++)
                {
                    if(scenario==6 && frame==72*3)
                    {
                        Check((bool)typeof(ZombieSmasherJump).GetProperty("WaitingForLandingSpace",Private).GetValue(jump),
                            "Authority reset must start during a blocked landing");
                        enemy.ResetBrain();enemy.SetRemoteState(Enemy.State.Retreat);interrupted=true;
                    }
                    if(frame==releaseCeiling)ceiling.SetActive(false);
                    // This synchronous physics fixture advances dt, not Unity's clock.
                    typeof(NavigationWorkBudget).GetMethod("Reset",Private).Invoke(null,null);
                    typeof(ZombieSmasherJump).GetField("lastLandingSearchFrame",Private).SetValue(null,-1);
                    Physics.SyncTransforms();
                    typeof(Enemy).GetMethod("TickFallRecovery",Private).Invoke(enemy,new object[]{manager,1f/72});
                    jump.TickJump(enemy,manager,1f/72);
                    Vector3 distance=enemy.Root.position-neighbor.Root.position;
                    distance.y-=Mathf.Clamp(distance.y,-.1f,.1f);
                    Check(distance.magnitude>=.699f,"Falling body must not overlap the zombie below it");
                    Check(!ground.Overlaps(enemy.Root.position,1<<6),"Crowded landing crossed a barrier");
                    Check(Vector3.Distance(previous,enemy.Root.position)<.2f,"Crowded landing must move continuously");
                    previous=enemy.Root.position;
                    hopped|=(bool)typeof(ZombieSmasherJump).GetField("_landingRecovery",Private).GetValue(jump);
                    if((scenario==3 || scenario==4) && !interrupted && hopped && jump.IsJumping &&
                        (float)typeof(ZombieSmasherJump).GetField("_elapsed",Private).GetValue(jump)>.15f)
                    {
                        interrupted=true;
                        if(scenario==3) { enemy.ResetBrain(); enemy.SetRemoteState(Enemy.State.Retreat); }
                        else jump.NudgeTrajectory(Vector3.right,.4f,1<<6);
                    }
                    // A moving overhead obstruction arrives just after planning,
                    // twice. Once it leaves, a later retry must still be possible.
                    if(scenario==5 && blockedHops<2 && !ceiling.activeSelf && jump.IsJumping && !jump.IsFalling &&
                        (bool)typeof(ZombieSmasherJump).GetField("_landingRecovery",Private).GetValue(jump) &&
                        (float)typeof(ZombieSmasherJump).GetField("_elapsed",Private).GetValue(jump)<.001f)
                    {
                        ground.GetCapsuleSize(out _,out float height);
                        ceiling.transform.position=new Vector3(origin.x,enemy.Root.position.y+height+.08f,origin.z);
                        ceiling.SetActive(true);releaseCeiling=frame+72;blockedHops++;
                    }
                }
                if(sealedLanding)
                {
                    Check(impacts==0 && jump.IsJumping,"Sealed landing must not report a false impact");
                    Check((float)typeof(Enemy).GetField("_fallTime",Private).GetValue(enemy)<1,
                        "Supported body contact must not trigger the unsupported-fall teleport");
                    neighbor.IsDormant=true;
                    for(int frame=0;frame<72*2;frame++){Physics.SyncTransforms();jump.TickJump(enemy,manager,1f/72);}
                }
                bool landed=!jump.IsJumping && ground.HasGroundSupport(enemy.Root.position,1<<6);
                Debug.Log("Crowded landing: case="+scenario+" supported="+landed+" hop="+hopped+" impacts="+impacts+" position="+enemy.Root.position);
                Check(landed && (scenario==3?impacts<=1:impacts==1),"Falling Smasher must reach supported ground without duplicate impacts");
                if(scenario==1)Check(hopped,"Low enclosed landing must use a collision-checked corrective hop");
                if(sealedLanding)Check(!hopped,"Recovery hop must not cross a sealed enclosure");
                if(scenario==3 || scenario==4 || scenario==6)Check(interrupted,"Recovery interruption must actually occur");
                if(scenario==5)Check(blockedHops==2,"Both moving obstructions must interrupt recovery hops");
            }
            // Allocation measurement needs an executable arc regardless of which
            // obstruction scenario ran last.
            walls.SetActive(false);ceiling.SetActive(false);Physics.SyncTransforms();
            var planHop=(Func<Enemy,EnemyManager,Vector3,bool>)Delegate.CreateDelegate(
                typeof(Func<Enemy,EnemyManager,Vector3,bool>),jump,typeof(ZombieSmasherJump).GetMethod("TryLandingHop",Private));
            var resetBudget=(Action)Delegate.CreateDelegate(typeof(Action),typeof(NavigationWorkBudget).GetMethod("Reset",Private));
            var resetFrame=(Action)Delegate.CreateDelegate(typeof(Action),typeof(ZombieSmasherJump).GetMethod("ResetCheckPhases",Private));
            for(int warm=0;warm<32;warm++){resetBudget();resetFrame();planHop(enemy,manager,Vector3.forward);}
            int planned=0;
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int sample=0;sample<100;sample++)
            {
                resetBudget();resetFrame();
                if(planHop(enemy,manager,Vector3.forward))planned++;
            }
            long delta=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Check(planned>0 && delta==0,"Warmed recovery-hop searches must execute without managed allocations; bytes="+delta);
            Debug.Log("Crowded landing allocation PASS, 0 bytes in 100 warmed recovery-hop searches");
        }
        finally{typeof(DeathFlare).GetMethod("OnDisable",Private).Invoke(flare,null);Enemy.Active.Remove(enemy);Enemy.Active.Remove(neighbor);Object.DestroyImmediate(fixture);}
    }

    public static void CheckContactReorientation()
    {
        var actor=new GameObject("Reorienting crowd body");
        var climb=actor.AddComponent<ClimbLocomotion>();
        var enemy=actor.AddComponent<Enemy>();
        var other=new GameObject("Reorientation neighbor").AddComponent<Enemy>();
        typeof(Enemy).GetMethod("Awake",Private).Invoke(enemy,null);
        typeof(Enemy).GetMethod("Awake",Private).Invoke(other,null);
        if(!Enemy.Active.Contains(other))Enemy.Active.Add(other);
        var normal=typeof(ClimbLocomotion).GetField("_surfaceUp",Private);
        var apply=typeof(ClimbLocomotion).GetMethod("ApplyMovementPose",Private);
        float radius=(float)typeof(ClimbLocomotion).GetMethod("WorldRadius",Private).Invoke(climb,null);
        float clearance=radius+.02f;
        var axes=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
        Vector3 origin=new Vector3(2000,45,0);
        int cases=0,failures=0;
        float worstGap=float.PositiveInfinity;
        string worstCase="";
        try
        {
            foreach(Vector3 before in axes)
            foreach(Vector3 after in axes)
            {
                if(Mathf.Abs(Vector3.Dot(before,after))>.01f)continue;
                Vector3 forward=Vector3.Cross(before,after);
                foreach(Vector3 step in new[]{forward*.1f,before*.15f,after*.15f,(before+after)*.15f,(before-after)*.15f})
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                foreach(float margin in new[]{.01f,.08f})
                {
                    Vector3 direction=new Vector3(x,y,z);
                    if(direction==Vector3.zero)continue;
                    other.Teleport(origin+before*clearance+direction.normalized*(radius+.3f+.12f+margin)-Vector3.up*.3f,Quaternion.identity);
                    enemy.Teleport(origin,Quaternion.LookRotation(forward,before));
                    Vector3 next=origin+(before-after)*clearance+step;
                    normal.SetValue(climb,after);
                    apply.Invoke(climb,new object[]{enemy,next,before,1f/72,540f});
                    Vector3 center=enemy.Root.position+climb.SurfaceNormal*clearance;
                    float gap=Vector3.Distance(center,other.Root.position+Vector3.up*.3f)-radius-.3f;
                    if(gap<.118f)failures++;
                    if(gap<worstGap)
                    {
                        worstGap=gap;
                        worstCase=before+" -> "+after+" step="+step+" neighbor="+direction+" margin="+margin+
                            " gap="+gap+" pose="+enemy.Root.position.ToString("F4");
                    }
                    cases++;
                }
            }
        }
        finally {Enemy.Active.Remove(enemy);Enemy.Active.Remove(other);Object.DestroyImmediate(actor);Object.DestroyImmediate(other.gameObject);}
        Check(failures==0,"Contact rotation violates crowd clearance in "+failures+"/"+cases+" cases; worst "+worstCase);
        Debug.Log("Crowd contact reorientation PASS "+cases+" transitions");
    }

    static void CheckClimbRouteContact()
    {
        var fixture=new GameObject("Climbing flare contact fixture");
        Vector3 origin=new Vector3(7000,0,0);
        var wall=new GameObject("Supported wall");wall.transform.SetParent(fixture.transform);
        wall.transform.position=origin+new Vector3(.5f,1,0);wall.layer=6;
        wall.AddComponent<BoxCollider>().size=new Vector3(1,3,4);
        var actor=new GameObject("Turning spider");actor.transform.SetParent(fixture.transform);
        var climb=actor.AddComponent<ClimbLocomotion>();
        var enemy=actor.AddComponent<Enemy>();
        Enemy neighbor=null;
        typeof(Enemy).GetMethod("Awake",Private).Invoke(enemy,null);
        var graph=new CrawlGraph {Origin=origin+new Vector3(-.5f,.5f,-.5f),CellSize=1,DimX=1,DimY=1,DimZ=2,
            ChunkCells=16,ChunksX=1,ChunksY=1,ChunksZ=1,ChunkStart=new[]{0,2},NodeCount=2,NodeKey=new[]{0,1},
            NodePos=new[]{origin.x,1f,0f,origin.x,1f,1f},
            NodeNormal=new[]{CrawlGraph.PackNormal(Vector3.left),CrawlGraph.PackNormal(Vector3.left)},
            EdgeStart=new[]{0,1,2},EdgeTarget=new[]{1,0}};
        try
        {
            enemy.Teleport(origin+Vector3.up,Quaternion.LookRotation(Vector3.forward,Vector3.left));
            // The visible rotation lags the exact contact normal during a corner turn.
            enemy.SetRootPose(enemy.Root.position,Quaternion.LookRotation(Vector3.forward,(Vector3.left+Vector3.up).normalized));
            Physics.SyncTransforms();
            Check(climb.HasSurfaceSupport(enemy.Root.position,1<<6),"Spider starts supported against the wall");
            typeof(Enemy).GetMethod("PlanEscape",Private).Invoke(enemy,new object[]{graph});
            bool replanSupported=Vector3.Dot(climb.SurfaceNormal,Vector3.left)>.999f && climb.HasSurfaceSupport(enemy.Root.position,1<<6);
            var floor=new GameObject("Corner floor");floor.transform.SetParent(fixture.transform);floor.layer=6;
            floor.transform.position=origin+new Vector3(-2,-.5f,0);floor.AddComponent<BoxCollider>().size=new Vector3(4,1,4);
            neighbor=new GameObject("Corner neighbor").AddComponent<Enemy>();neighbor.transform.SetParent(fixture.transform);
            typeof(Enemy).GetMethod("Awake",Private).Invoke(neighbor,null);
            neighbor.Teleport(origin+Vector3.left*.78f,Quaternion.identity);
            if(!Enemy.Active.Contains(neighbor))Enemy.Active.Add(neighbor);
            Vector3 previous=origin+Vector3.up*.22f;
            enemy.Teleport(previous,Quaternion.LookRotation(Vector3.forward,Vector3.left));
            Physics.SyncTransforms();
            Check(climb.HasSurfaceSupport(previous,1<<6),"Corner transition begins on a supported wall");
            typeof(ClimbLocomotion).GetField("_surfaceUp",Private).SetValue(climb,Vector3.up);
            typeof(ClimbLocomotion).GetMethod("ApplyMovementPose",Private).Invoke(climb,
                new object[]{enemy,origin+Vector3.left*.23f,Vector3.left,.1f,540f});
            bool transitionSupported=climb.HasSurfaceSupport(enemy.Root.position,1<<6) && !climb.Overlaps(enemy.Root.position,1<<6);
            neighbor.Teleport(origin+Vector3.back*4,Quaternion.identity);
            enemy.Teleport(previous,Quaternion.LookRotation(Vector3.forward,Vector3.left));
            typeof(ClimbLocomotion).GetField("_surfaceUp",Private).SetValue(climb,Vector3.up);
            typeof(ClimbLocomotion).GetMethod("ApplyMovementPose",Private).Invoke(climb,
                new object[]{enemy,origin+Vector3.left*.23f,Vector3.left,.1f,540f});
            bool transitionCompleted=Vector3.Distance(enemy.Root.position,origin+Vector3.left*.23f)<.001f && climb.HasSurfaceSupport(enemy.Root.position,1<<6);
            Debug.Log("Spider contact checks: replanSupported="+replanSupported+" blockedTransitionSupported="+transitionSupported+" releasedTransitionCompleted="+transitionCompleted);
            Check(replanSupported && transitionSupported && transitionCompleted,
                "Flare replans and neighbor-blocked corner transitions must preserve valid surface contact");
        }
        finally{Enemy.Active.Remove(enemy);if(neighbor)Enemy.Active.Remove(neighbor);Object.DestroyImmediate(fixture);}
    }

    static void CheckBlockedExits(EnemyManager manager, Enemy enemy, CrawlGraph original, GameObject fixture, Vector3 origin)
    {
        var wall=new GameObject("Two blocked flare exits");
        wall.transform.SetParent(fixture.transform);
        wall.transform.position=origin+new Vector3(4,1,0);
        wall.layer=6;
        wall.AddComponent<BoxCollider>().size=new Vector3(.5f,2,3);
        var graph=new CrawlGraph { Origin=original.Origin, CellSize=1, DimX=16, DimY=1, DimZ=16,
            ChunkCells=16,ChunksX=1,ChunksY=1,ChunksZ=1,ChunkStart=new[]{0,8},
            NodeCount=8,NodeKey=new[]{1,3,5,13,21,29,65,193},NodePos=new float[24],NodeNormal=new int[8],
            EdgeStart=new[]{0,2,5,7,8,10,11,13,14},EdgeTarget=new[]{1,6,2,4,0,1,3,2,1,5,4,0,7,6} };
        var positions=new[]{new Vector3(1,0,0),new Vector3(3,0,0),new Vector3(5,0,0),new Vector3(13,0,0),
            new Vector3(5,0,1),new Vector3(13,0,1),new Vector3(1,0,4),new Vector3(1,0,12)};
        for(int i=0;i<positions.Length;i++)
        {
            var p=origin+positions[i];
            graph.NodePos[i*3]=p.x;graph.NodePos[i*3+1]=p.y;graph.NodePos[i*3+2]=p.z;
            graph.NodeNormal[i]=CrawlGraph.PackNormal(Vector3.up);
        }
        var tick=(Func<EnemyManager,float,bool>)Delegate.CreateDelegate(typeof(Func<EnemyManager,float,bool>),enemy,
            typeof(Enemy).GetMethod("TickFlareFear",Private));
        try
        {
            typeof(EnemyManager).GetProperty("Graph").SetValue(manager,graph);
            enemy.Teleport(origin+positions[1],Quaternion.identity);
            Physics.SyncTransforms();
            for(int frame=0;frame<72*20;frame++)
            {
                typeof(Enemy).GetField("lastEscapeFrame",Private).SetValue(null,-1);
                typeof(NavigationWorkBudget).GetField("spent",Private).SetValue(null,0L);
                typeof(NavigationWorkBudget).GetField("enemySpent",Private).SetValue(null,0L);
                tick(manager,1f/72);
                if(enemy.Root.position.z>origin.z+9)break;
            }
            Check(enemy.Root.position.z>origin.z+9,
                "Must remember both blocked exits and take the remaining indoor detour; position="+(enemy.Root.position-origin));
            var ground=enemy.GetComponent<GroundLocomotion>();
            var blocked=(HashSet<int>)typeof(GroundLocomotion).GetProperty("EscapeBlockedNodes",Private).GetValue(ground);
            Check(blocked.Contains(2) && blocked.Contains(4),"Both failed exits remain remembered during the detour");
            var expiry=(Dictionary<int,float>)typeof(GroundLocomotion).GetField("_blockedExpiry",Private).GetValue(ground);
            foreach(int node in blocked)expiry[node]=Time.time-1;
            blocked=(HashSet<int>)typeof(GroundLocomotion).GetProperty("EscapeBlockedNodes",Private).GetValue(ground);
            Check(blocked.Count==0,"Temporary crowd restrictions expire so reopened exits can be retried");
        }
        finally
        {
            Object.DestroyImmediate(wall);
            typeof(EnemyManager).GetProperty("Graph").SetValue(manager,original);
            enemy.Teleport(origin+new Vector3(1,0,12),Quaternion.identity);
        }
    }

    static void CheckIndoorMovement()
    {
        var fixture = new GameObject("Flare escape movement fixture");
        Vector3 origin = new Vector3(6000, 0, 0);
        GameObject Part(string name, Vector3 offset)
        {
            var part = new GameObject(name);
            part.transform.SetParent(fixture.transform);
            part.transform.position = origin + offset;
            return part;
        }
        void Box(string name, Vector3 offset, Vector3 size)
        {
            var part = Part(name,offset);
            part.layer = 6;
            part.AddComponent<BoxCollider>().size = size;
        }
        var planFrame = typeof(Enemy).GetField("lastEscapeFrame", Private);
        int oldFrame = (int)planFrame.GetValue(null);
        var budgetFrame = typeof(NavigationWorkBudget).GetField("frame", Private);
        var budgetSpent = typeof(NavigationWorkBudget).GetField("spent", Private);
        var budgetEnemySpent = typeof(NavigationWorkBudget).GetField("enemySpent", Private);
        int oldBudgetFrame = (int)budgetFrame.GetValue(null);
        long oldBudgetSpent = (long)budgetSpent.GetValue(null);
        long oldEnemySpent = (long)budgetEnemySpent.GetValue(null);
        var flare = Part("burning flare",Vector3.zero).AddComponent<DeathFlare>();
        try
        {
            Box("floor",new Vector3(1,-.5f,6),new Vector3(14,1,22));
            Box("corner",new Vector3(4,1,4),new Vector3(3,2,5));
            var managerObject = Part("test graph",Vector3.zero);
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<EnemyManager>();
            var graph = new CrawlGraph { Origin=origin-Vector3.one*.5f, CellSize=1, DimX=16, DimY=1, DimZ=16,
                ChunkCells=16,ChunksX=1,ChunksY=1,ChunksZ=1,ChunkStart=new[]{0,4},
                NodeCount=4, NodeKey=new[]{1,3,65,193},NodePos=new float[12],NodeNormal=new int[4],
                EdgeStart=new[]{0,2,3,5,6},EdgeTarget=new[]{1,2,0,0,3,2} };
            var positions = new[]{new Vector3(1,0,0),new Vector3(3,0,0),new Vector3(1,0,4),new Vector3(1,0,12)};
            for (int i=0;i<4;i++)
            {
                Vector3 v = origin + positions[i];
                graph.NodePos[i*3] = v.x; graph.NodePos[i*3+1] = v.y; graph.NodePos[i*3+2] = v.z;
                graph.NodeNormal[i] = CrawlGraph.PackNormal(Vector3.up);
            }
            typeof(EnemyManager).GetProperty("Graph").SetValue(manager,graph);
            typeof(EnemyManager).GetField("surfaceMask",Private).SetValue(manager,(LayerMask)(1<<6));
            var actor = Part("ground zombie",positions[1]);
            actor.AddComponent<GroundLocomotion>();
            var enemy = actor.AddComponent<Enemy>();
            typeof(Enemy).GetMethod("Awake",Private).Invoke(enemy,null);
            typeof(DeathFlare).GetMethod("OnEnable",Private).Invoke(flare,null);
            typeof(DeathFlare).GetMethod("Initialize",Private).Invoke(flare,new object[]{0f});
            Physics.SyncTransforms();
            var tick = (Func<EnemyManager,float,bool>)Delegate.CreateDelegate(typeof(Func<EnemyManager,float,bool>),
                enemy,typeof(Enemy).GetMethod("TickFlareFear",Private));
            budgetFrame.SetValue(null,Time.frameCount);
            budgetSpent.SetValue(null,long.MaxValue/2);
            planFrame.SetValue(null,-1);
            tick(manager,1f/72);
            Check(actor.GetComponent<GroundLocomotion>().PathDone,
                "Flare planning must defer when shared navigation has spent the frame budget");
            bool detoured = false;
            for(int frame=0;frame<360;frame++)
            {
                // A synchronous edit-mode test has a single engine frame number.
                planFrame.SetValue(null,-1);
                budgetSpent.SetValue(null,0L);
                budgetEnemySpent.SetValue(null,0L);
                if (!tick(manager,1f/72)) break;
                if (frame == 0)
                    Check(NavigationWorkBudget.SpentMilliseconds > 0,
                        "Flare planning must charge elapsed work to shared navigation");
                detoured |= (enemy.Root.position-origin).sqrMagnitude < 2.5f*2.5f;
            }
            Check(detoured && enemy.Root.position.z>9,"Walked around the corner through a closer waypoint and escaped");
            CheckBlockedExits(manager,enemy,graph,fixture,origin);
            // Move a second zombie into the last known safe pose before a wall rollback.
            var other = Part("occupied rollback",positions[3]).AddComponent<Enemy>();
            typeof(Enemy).GetMethod("Awake",Private).Invoke(other,null);
            if (!Enemy.Active.Contains(other)) Enemy.Active.Add(other);
            try
            {
                var ground = actor.GetComponent<GroundLocomotion>();
                void Set(string field, object value) => typeof(GroundLocomotion).GetField(field,Private).SetValue(ground,value);
                actor.transform.position = origin + positions[2];
                Set("_hasPreviousBottom",true);
                Set("_previousBottom",origin + new Vector3(7,.5f,4));
                Set("_hasSafePose",true);
                Set("_safePosition",other.Root.position);
                Set("_safeRotation",Quaternion.identity);
                Check(!ground.ValidateSmasherPose(enemy,1<<6) && enemy.Root.position == origin+positions[2],
                    "Smasher rollback must not take an occupied position");
                other.IsDormant = true;
                Check(ground.ValidateSmasherPose(enemy,1<<6) && enemy.Root.position == other.Root.position,
                    "Released rollback position becomes available");

                // A zombie that was previously stalled may subsequently reach a queue.
                // Waiting for the front body must cancel the old respawn countdown.
                enemy.Teleport(origin+positions[2],Quaternion.identity);
                other.Teleport(enemy.Root.position+Vector3.forward*.9f,Quaternion.identity);
                other.IsDormant=false;
                Box("queue left wall",new Vector3(.5f,1,5),new Vector3(.2f,2,4));
                Box("queue right wall",new Vector3(1.5f,1,5),new Vector3(.2f,2,4));
                Physics.SyncTransforms();
                var route=(int[])typeof(GroundLocomotion).GetField("_path",Private).GetValue(ground);
                route[0]=3;
                Set("_pathCount",1); Set("_pathIndex",0); Set("_goalNode",3);
                Set("_stalled",true); Set("_stallOrigin",enemy.Root.position); Set("_stallTime",10f);
                ground.TickMove(enemy,manager,1f/72);
                Check(!(bool)typeof(GroundLocomotion).GetField("_stalled",Private).GetValue(ground)
                    && (float)typeof(GroundLocomotion).GetField("_stallTime",Private).GetValue(ground)==0,
                    "Intentional crowd queue cancels an earlier stuck-respawn countdown");
                Check(Vector3.Distance(enemy.Root.position,other.Root.position)>.8f,"Queue maintains body clearance");
            }
            finally { Enemy.Active.Remove(other); }
        }
        finally
        {
            planFrame.SetValue(null,oldFrame);
            budgetFrame.SetValue(null,oldBudgetFrame);
            budgetSpent.SetValue(null,oldBudgetSpent);
            budgetEnemySpent.SetValue(null,oldEnemySpent);
            typeof(DeathFlare).GetMethod("OnDisable",Private).Invoke(flare,null);
            Object.DestroyImmediate(fixture);
        }
    }

    static void CheckEscape()
    {
        // Node 1 goes closer before turning a corner. Node 4 is farther away but disconnected.
        var graph = new CrawlGraph { NodeCount = 5, NodePos = new float[] {
            3,0,0, 2,0,0, 2,0,5, 2,0,12, 30,0,0 },
            NodeNormal = new int[5], EdgeStart = new[] {0,1,3,5,6,6}, EdgeTarget = new[] {1,0,2,1,3,2} };
        Array.Fill(graph.NodeNormal, CrawlGraph.PackNormal(Vector3.up));
        var threats = (List<Vector3>)typeof(Enemy).GetField("threats", Private).GetValue(null);
        var plan = typeof(Enemy).GetMethod("SelectEscape", Private);
        var path = (int[])typeof(Enemy).GetField("escapePath", Private).GetValue(null);
        int Plan(bool ground) => (int)plan.Invoke(null, new object[]{graph, 0, ground, 6f, -1, null});
        try
        {
            threats.Clear(); threats.Add(Vector3.zero);
            Check(Plan(true) == 3 && path[0] == 1 && path[2] == 3, "Reachable doorway detour");
            threats.Add(new Vector3(2, 0, 12));
            Check(Plan(true) == 2 && path[1] == 2, "Second flare blocks the exit; choose best reachable spot");
            graph.NodeNormal[1] = CrawlGraph.PackNormal(Vector3.left);
            Check(Plan(true) == 0, "Ground zombie must hold in a sealed corner");
            Check(Plan(false) == 2, "Climber can use a wall route");
        }
        finally { threats.Clear(); }
    }
}
