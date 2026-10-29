using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResumablePathChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
        SearchCases();
        MoverCases(false);
        MoverCases(true);
        SchedulerCases();
        // Navigation-relevant movement checks. The full GroundRecoveryChecks.Run suite
        // also has a ThinTerrainFalls failure reproduced on the unmodified baseline.
        typeof(GroundRecoveryChecks).GetMethod("RouteAvoidance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        GroundRecoveryChecks.SpiderSurfaceRecovery();
        foreach (float angle in new[] { 20f, 40f, 60f })
            typeof(GroundRecoveryChecks).GetMethod("RampSeam", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { angle });
        SpiderRoofLipChecks.Run();
        EnemySchedulingChecks.Run();
        Debug.Log("ResumablePathChecks: ALL PASSED (search slices, budgets, detours, climbing, mover cancellation, scheduler, recovery and traversal)");
        EditorApplication.Exit(0);
    }

    static CrawlGraph Line(int n, int clusterSize = 8)
    {
        int clusters = (n + clusterSize - 1) / clusterSize;
        var g = new CrawlGraph {
            Origin = Vector3.one * -.125f, CellSize = .25f, DimX = n, DimY = 1, DimZ = 1,
            ChunkCells = n, ChunksX = 1, ChunksY = 1, ChunksZ = 1,
            NodeCount = n, NodeKey = new int[n], NodePos = new float[n * 3],
            NodeNormal = new int[n], NodeSurface = new byte[n], NodeCluster = new int[n],
            EdgeStart = new int[n + 1], ChunkStart = new[] { 0, n },
            ClusterCount = clusters, ClusterComponent = new int[clusters], ClusterCenter = new float[clusters * 3],
            ClusterNode = new int[clusters], ClusterChunk = new int[clusters], ClusterEdgeStart = new int[clusters + 1]
        };
        var edges = new List<int>();
        for (int i = 0; i < n; i++)
        {
            g.NodeKey[i] = i; g.NodePos[i * 3] = i * .25f;
            g.NodeNormal[i] = CrawlGraph.PackNormal(Vector3.up); g.NodeCluster[i] = i / clusterSize;
            g.EdgeStart[i] = edges.Count;
            if (i > 0) edges.Add(i - 1);
            if (i + 1 < n) edges.Add(i + 1);
        }
        g.EdgeStart[n] = edges.Count; g.EdgeTarget = edges.ToArray(); edges.Clear();
        for (int i = 0; i < clusters; i++)
        {
            g.ClusterCenter[i * 3] = i * clusterSize * .25f;
            g.ClusterNode[i] = i * clusterSize;
            g.ClusterEdgeStart[i] = edges.Count;
            if (i > 0) edges.Add(i - 1);
            if (i + 1 < clusters) edges.Add(i + 1);
        }
        g.ClusterEdgeStart[clusters] = edges.Count; g.ClusterEdgeTarget = edges.ToArray();
        return g;
    }

    static int Finish(CrawlPathfinder finder, int work = 3)
    {
        int slices = 0;
        while (finder.IsSearching)
        {
            finder.Step(long.MaxValue, work);
            Check(finder.LastSliceWork <= work, "Work cap exceeded");
            Check(++slices < 100000, "Search failed to terminate");
        }
        return slices;
    }

    static void SearchCases()
    {
        var graph = Line(100);
        var finder = new CrawlPathfinder(graph) { DirectSearchDistance = float.MaxValue };
        var path = new int[128];
        finder.BeginSearch(0, 99, true);
        Check(!finder.Step(0) && finder.LastSliceWork == 0, "Expired budget must defer work");
        Check(Finish(finder) > 1, "Search did not resume across slices");
        Check(finder.CopyResult(path, out int count, out bool complete) && count == 100 && complete, "Full ground route");
        for (int i = 0; i < count; i++) Check(path[i] == i, "Ground route edge order");
        finder.BeginSearch(0, 99, true);
        finder.Step(long.MaxValue, 40);
        finder.Cancel();
        Check(!finder.CopyResult(path, out _, out _), "Cancelled result remained visible");
        finder.BeginSearch(50, 50);
        Check(finder.CopyResult(path, out count, out complete) && count == 1 && complete && path[0] == 50, "Same-node result");

        // Coarse planning, corridor-limited paths and reconstruction also yield.
        finder.DirectSearchDistance = 0;
        finder.BeginSearch(0, 99, true);
        Check(Finish(finder, 1) > 100 && finder.LastCoarseExpanded > 0, "Coarse search not exercised");
        Check(finder.CopyResult(path, out count, out complete) && !complete && path[count - 1] == 24, "Corridor partial route changed");
        var capped = new CrawlPathfinder(Line(200), 64) { DirectSearchDistance = float.MaxValue };
        capped.BeginSearch(0, 199);
        Finish(capped);
        Check(capped.CopyResult(path, out count, out complete) && !complete && path[count - 1] == 63, "Expansion-limit partial route changed");
        finder.DirectSearchDistance = float.MaxValue;
        finder.BeginSearch(0, 99);
        finder.Step(long.MaxValue, 100);
        finder.FinishPartial(); Finish(finder);
        Check(finder.CopyResult(path, out count, out complete) && !complete && count > 1, "Fairness timeout lost usable partial path");

        // Diamond detour: original direct route 0-1-3, alternative 0-2-3.
        var detour = Line(4, 4);
        detour.EdgeStart = new[] { 0, 2, 3, 4, 4 };
        detour.EdgeTarget = new[] { 1, 2, 3, 3 };
        detour.NodePos = new[] { 0f,0f,0f, 1f,0f,0f, 0f,0f,1f, 2f,0f,0f };
        var blocked = new HashSet<int> { 1 };
        var avoid = new CrawlPathfinder(detour);
        avoid.BeginSearch(0, 3, true, blocked);
        blocked.Clear(); blocked.Add(2); // Caller changes cannot mutate an active request.
        Finish(avoid, 1);
        Check(avoid.CopyResult(path, out count, out complete) && complete && path[1] == 2, "Obstacle snapshot changed mid-search");
        avoid.BeginSearch(0, 3, true); Finish(avoid);
        Check(avoid.CopyResult(path, out _, out complete) && complete && path[1] == 1, "Blocked nodes leaked to next request");
        avoid.BeginSearch(0, 3, true, new HashSet<int> { 1, 2 }); Finish(avoid);
        Check(!avoid.CopyResult(path, out _, out _), "Unreachable route reported progress");

        // The only route crosses wall and ceiling nodes: crawlers may traverse, ground enemies may not.
        var surfaces = Line(4, 1);
        surfaces.NodeSurface[1] = 1; surfaces.NodeSurface[2] = 2;
        surfaces.NodeNormal[1] = CrawlGraph.PackNormal(Vector3.left);
        surfaces.NodeNormal[2] = CrawlGraph.PackNormal(Vector3.down);
        var climb = new CrawlPathfinder(surfaces) { DirectSearchDistance = 0, CorridorClusters = 4 };
        climb.BeginSearch(0, 3); Finish(climb, 1);
        Check(climb.CopyResult(path, out count, out complete) && complete && count == 4, "Wall/ceiling route lost");
        climb.BeginSearch(0, 3, true); Finish(climb, 1);
        Check(!climb.CopyResult(path, out _, out _), "Ground route crossed wall/ceiling");

        // Warmed search buffers must not allocate while stepping.
        finder.BeginSearch(0, 99); Finish(finder);
        long before = GC.GetAllocatedBytesForCurrentThread();
        finder.BeginSearch(0, 99); Finish(finder);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "Resumable search allocates after warmup");
        var large = new CrawlPathfinder(Line(20000), 19000) { DirectSearchDistance = float.MaxValue };
        large.BeginSearch(0, 19999);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        large.Step(started + System.Diagnostics.Stopwatch.Frequency / 1000, int.MaxValue);
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        Check(large.IsSearching, "One millisecond budget ran an entire large search");
        Debug.Log($"Resumable search PASS: deadline slice {ms:F3} ms, {large.LastSliceWork} work units (desktop, not Quest timing)");
    }

    static void MoverCases(bool climbing)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var managerObject = new GameObject("Path manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        var graph = Line(100);
        Set(manager, "<Graph>k__BackingField", graph);
        Set(manager, "<Pathfinder>k__BackingField", new CrawlPathfinder(graph) { DirectSearchDistance = float.MaxValue });
        var actor = new GameObject("Path mover");
        IEnemyLocomotion mover = climbing ? actor.AddComponent<ClimbLocomotion>() : actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake");
        var oldPath = (int[])Get(mover, "_path"); oldPath[0] = 0; oldPath[1] = 1;
        Set(mover, "_pathCount", 2); Set(mover, "_pathIndex", 1);
        Call(mover, "RequestPath", 99);
        Set(manager, "_pathDeadline", 1L); // Force a pending search deterministically.
        mover.RunPathRequest(manager);
        Check(mover.WantsPath && (int)Get(mover, "_pathCount") == 2 && oldPath[1] == 1, "Pending search overwrote live route");
        int version = (int)Get(mover, "_pathVersion");
        mover.ClearPath();
        Check((int)Get(mover, "_pathVersion") != version, "ClearPath did not invalidate search");
        Call(mover, "RequestPath", 50);
        Set(manager, "_pathDeadline", long.MaxValue);
        mover.RunPathRequest(manager);
        Check(!mover.WantsPath && oldPath[(int)Get(mover, "_pathCount") - 1] == 50, "Stale goal applied after cancellation");

        Call(mover, "RequestPath", 99);
        Set(manager, "_pathDeadline", 1L); mover.RunPathRequest(manager);
        actor.transform.position = graph.GetPosition(8);
        Set(manager, "_pathDeadline", long.MaxValue); mover.RunPathRequest(manager);
        Check(!mover.WantsPath && (int)Get(mover, "_pathIndex") == 9, "Moving enemy backtracked to stale start");
        Check(EnemyManager.PathJoinIndex(graph, 90, new[] { 0, 1, 2 }, 3) < 0, "Disconnected join accepted");
        var floor = new GameObject("Chase floor"); floor.layer = 6;
        floor.transform.position = new Vector3(12, -.5f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(60, 1, 10);
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Physics.SyncTransforms();
        enemy.SetRemoteState(Enemy.State.Chase);
        float beforeMove = actor.transform.position.x;
        Call(mover, "RequestPath", 90);
        Set(manager, "_pathDeadline", 1L); mover.RunPathRequest(manager);
        for (int frame = 0; frame < 20; frame++) mover.TickMove(enemy, manager, .02f);
        Check(actor.transform.position.x > beforeMove + .1f && mover.WantsPath, "Chase stopped while replacement pending");
        Set(manager, "_pathDeadline", long.MaxValue); mover.RunPathRequest(manager);
        Check(!mover.WantsPath, "Chase replacement did not finish");
        Debug.Log("Mover replacement PASS: " + (climbing ? "climb" : "ground"));
    }

    static void SchedulerCases()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var managerObject = new GameObject("Queue manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        var graph = Line(20000);
        Set(manager, "<Graph>k__BackingField", graph);
        Set(manager, "<Pathfinder>k__BackingField", new CrawlPathfinder(graph, 19000) { DirectSearchDistance = float.MaxValue });
        Set(manager, "pathBudgetMilliseconds", .1f);
        Set(manager, "maxPathRequestFrames", 3);
        var enemies = new List<Enemy>();
        for (int i = 0; i < 2; i++)
        {
            var actor = new GameObject("Queued enemy");
            var probe = actor.AddComponent<ResumablePathProbe>();
            var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake"); Set(enemy, "_loco", probe);
            enemies.Add(enemy);
        }
        for (int frame = 0; frame < 100 && (enemies[0].WantsPath || enemies[1].WantsPath); frame++)
            Call(manager, "ServicePathRequest", enemies);
        Check(!enemies[0].WantsPath && !enemies[1].WantsPath, "Long request starved another enemy");
        var first = enemies[0].GetComponent<ResumablePathProbe>(); first.pending = true;
        Call(manager, "ServicePathRequest", enemies);
        enemies[0].IsDormant = true;
        Call(manager, "ServicePathRequest", enemies);
        Check(!manager.Pathfinder.IsSearching, "Dormant enemy search not cancelled");
        Debug.Log("Scheduler PASS: round-robin completion, finite request lifetime, dormant cancellation");
    }
}

public sealed class ResumablePathProbe : MonoBehaviour, IEnemyLocomotion
{
    public bool pending = true;
    readonly int[] result = new int[128];
    public bool PathDone => false;
    public bool WantsPath => pending;
    public void RunPathRequest(EnemyManager m)
    {
        var status = m.StepPathRequest(this, 0, 0, m.Graph.NodeCount - 1, result, out _, out _);
        pending = status == EnemyManager.PathStatus.Pending;
    }
    public void Reset() { pending = false; }
    public void ClearPath() { pending = false; }
    public void Interrupt() { pending = false; }
    public void BeginWanderPause() { }
    public bool TickWanderPause(float dt) => false;
    public void RequestWander(EnemyManager m) { }
    public void RequestPathToWorld(EnemyManager m, Vector3 p) { }
    public void ForcePathToWorld(EnemyManager m, Vector3 p) { }
    public void RequestRetreat(EnemyManager m, Vector3 p, float d) { }
    public void TickMove(Enemy e, EnemyManager m, float dt) { }
}
