using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DuckNavigationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static object Kind(string name) => Enum.Parse(typeof(DuckCompanion).GetNestedType("NavigationPlan", BindingFlags.NonPublic), name);
    static void Begin(DuckCompanion d, string kind, Vector3 goal, Bounds region = default)
        => Call(d, "BeginNavigation", Kind(kind), goal, region, Vector3.forward);

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project only");
        try
        {
            Budget();
            SearchParityAndAllocations();
            MeasureAllocationEvents();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void ContinueChecks()
    {
        try
        {
            // Existing enemy graph tests cover walls/ceilings, partial routes, cancellation and fairness.
            foreach (string method in new[] { "SearchCases", "MoverCases", "SchedulerCases" })
            {
                var m = typeof(ResumablePathChecks).GetMethod(method, Static);
                if (method == "MoverCases") { m.Invoke(null, new object[] { false }); m.Invoke(null, new object[] { true }); }
                else m.Invoke(null, null);
            }
            EnemySchedulingChecks.Run();
            DuckHomeChecks.Run();
            DuckInteriorStairChecks.Run();
            DuckTruckChecks.Run();
            DuckSidewaysClimbChecks.Run();
            DuckFenceChecks.Run();
            Debug.Log("DuckNavigationChecks: ALL PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Budget()
    {
        typeof(NavigationWorkBudget).GetMethod("Reset", Static).Invoke(null, null);
        Check(NavigationWorkBudget.TryBegin(1, true, out long start, out long deadline, true), "Duck gets first half");
        Check((deadline - start) <= Stopwatch.Frequency / 2000, "Duck slice exceeds shared half");
        while (Stopwatch.GetTimestamp() < deadline) { }
        NavigationWorkBudget.End(start, true);
        Check(!NavigationWorkBudget.TryBegin(1, true, out _, out _, true), "Repeated Duck work bypasses its lane cap");
        if (NavigationWorkBudget.TryBegin(1, true, out start, out deadline))
        {
            while (Stopwatch.GetTimestamp() < deadline) { }
            NavigationWorkBudget.End(start);
        }
        Check(!NavigationWorkBudget.TryBegin(1, false, out _, out _), "Combined work bypasses frame budget");
        typeof(NavigationWorkBudget).GetMethod("Reset", Static).Invoke(null, null);
        Debug.Log("Shared budget PASS: repeated slices and both lanes share one frame allowance");
    }

    static void SearchParityAndAllocations()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(PhotonNetwork).GetField("offlineMode", Static).SetValue(null, true);
        typeof(PhotonNetwork).GetField("offlineModeRoom", Static).SetValue(null,
            new Photon.Realtime.Room("Navigation fixture", new Photon.Realtime.RoomOptions(), true));
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 6;
        floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(100, 1, 100);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.layer = 6;
        obstacle.transform.position = new Vector3(2, 1, 0); obstacle.transform.localScale = new Vector3(1, 2, 4);
        var go = new GameObject("Navigation Duck"); go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>(); Call(duck, "Awake");
        go.transform.position = Vector3.up * .04f;
        Set(duck, "environment", (LayerMask)(1 << 6)); Call(duck, "PrepareNavigation");
        Physics.SyncTransforms();
        var clear = (Func<Vector3, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, bool>), duck, typeof(DuckCompanion).GetMethod("ClearBody", Private));
        var sweep = (Func<Vector3, Vector3, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, Vector3, bool>), duck, typeof(DuckCompanion).GetMethod("ClearBodySweep", Private));
        var plan = (Func<Vector3, Bounds, int, float, bool, IEnumerable<int>>)Delegate.CreateDelegate(
            typeof(Func<Vector3, Bounds, int, float, bool, IEnumerable<int>>), duck, typeof(DuckCompanion).GetMethod("PlanFloor", Private));
        var region = new Bounds(go.transform.position, new Vector3(40, 10, 40));
        var goal = new Vector3(6, .04f, 0);
        Action optimized = () => {
            Set(duck, "floorBuilt", false);
            using var work = plan(goal, region, 2048, .8f, false).GetEnumerator();
            while (work.MoveNext()) { }
        };
        optimized();
        Debug.Log("Floor fixture reachable nodes: " + ((Dictionary<Vector2Int, Vector3>)Get(duck, "floorPositions")).Count);
        var expected = LegacyFloor(go.transform.position, goal, region, clear, sweep);
        var actual = (Vector3[])Get(duck, "floorRoute");
        Check((bool)Get(duck, "floorSucceeded") && expected.Length == actual.Length, "Floor route length parity");
        for (int i = 0; i < actual.Length; i++) Check((actual[i] - expected[i]).sqrMagnitude < .000001f, "Floor route node parity");
        allocationOriginal = () => LegacyFloor(go.transform.position, goal, region, clear, sweep);
        allocationOptimized = optimized;

        // Same production coroutine must survive time-sliced execution, including reconstruction.
        Begin(duck, "Interior", goal, region);
        var advance = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), duck, typeof(DuckCompanion).GetMethod("AdvanceNavigation", Private));
        int slices = 0;
        while (duck.NavigationPending)
        {
            advance(Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10000);
            Check(++slices < 20000, "Planner failed to terminate");
        }
        Check(slices > 1 && ((Vector3[])Get(duck, "levelRoute")).Length > 0, "Sliced interior route missing");
        Debug.Log("Sliced floor/route PASS: " + slices + " slices, ordered route matches original");

        // Interleave an enemy request with Duck's request on the same graph.
        var managerGo = new GameObject("Navigation manager"); managerGo.SetActive(false);
        var manager = managerGo.AddComponent<EnemyManager>();
        var graph = (CrawlGraph)typeof(ResumablePathChecks).GetMethod("Line", Static).Invoke(null, new object[] { 100, 100 });
        var enemyFinder = new CrawlPathfinder(graph) { DirectSearchDistance = float.MaxValue };
        Set(manager, "<Graph>k__BackingField", graph); Set(manager, "<Pathfinder>k__BackingField", enemyFinder);
        typeof(EnemyManager).GetField("<Instance>k__BackingField", Static).SetValue(null, manager);
        Call(duck, "PrepareNavigation");
        var ownFinder = (CrawlPathfinder)Get(duck, "companionPathfinder");
        ownFinder.DirectSearchDistance = float.MaxValue;
        Check(!ReferenceEquals(ownFinder, enemyFinder), "Duck shares mutable enemy search state");
        var owner = new object(); var result = new int[128];
        Set(manager, "_pathDeadline", 1L);
        Check(manager.StepPathRequest(owner, 7, 0, 99, result, out _, out _) == EnemyManager.PathStatus.Pending, "Enemy must be pending");
        Begin(duck, "Route", new Vector3(10, .04f, 0));
        while (duck.NavigationPending) advance(Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10000);
        Check(manager.HasPathRequest(owner, 7) && enemyFinder.IsSearching, "Duck displaced enemy owner/search");
        Set(manager, "_pathDeadline", long.MaxValue);
        Check(manager.StepPathRequest(owner, 7, 0, 99, result, out int count, out bool complete) == EnemyManager.PathStatus.Found
            && complete && count == 100 && result[99] == 99, "Enemy got Duck's result");
        Debug.Log("Concurrent ownership PASS: pending enemy search survives Duck completion with its original destination");

        Begin(duck, "Interior", goal, region); advance(Stopwatch.GetTimestamp() + Stopwatch.Frequency / 100000);
        Call(duck, "ResetNavigationAfterAuthorityChange");
        Check(!duck.NavigationPending && NavigationWorkBudget.PendingDuckPlans == 0, "Authority reset leaves local work");
        Begin(duck, "Interior", goal, region); Set(duck, "followingPaused", true); Call(duck, "TickNavigation");
        Check(!duck.NavigationPending, "Wait command failed to cancel");
        Set(duck, "followingPaused", false);
        Begin(duck, "Interior", goal, region);
        typeof(PhotonNetwork).GetField("offlineModeRoom", Static).SetValue(null, null);
        Call(duck, "TickNavigation");
        Check(!duck.NavigationPending, "Non-authority continued planning");
        Debug.Log("Network lifecycle PASS: authority reset, non-authority and wait cancellation");
        // Objects remain alive for the asynchronous allocation capture; the next fixture clears the scene.
    }

    static Action allocationOriginal, allocationOptimized;
    static void MeasureAllocationEvents()
    {
        double original = AllocationEvents(allocationOriginal), optimized = AllocationEvents(allocationOptimized);
        Debug.Log($"Duck floor GC allocation EVENTS (2048 expansions): original {original:F1}; resumable {optimized:F1} per completed search. Counts, not bytes.");
        Check(original > 0 && optimized < original / 4, "Expected >75% fewer managed allocations");
        ContinueChecks();
    }
    static double AllocationEvents(Action action)
    {
        action();
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 100000,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(recorder.Valid, "Allocation recorder unavailable");
        for (int i = 0; i < 3; i++) action();
        recorder.Stop();
        Check(recorder.Count < recorder.Capacity, "Allocation recorder overflow");
        // GC.Alloc.Value is nanoseconds; Count counts allocation events. Byte sizes
        // require raw profiler metadata, unavailable in this headless editor.
        return recorder.Count / 3d;
    }

    // Original floor-grid algorithm, with the same collision delegates and search
    // parameters. The fixture uses no avoided ledge and no home-only restriction.
    static Vector3[] LegacyFloor(Vector3 origin, Vector3 goal, Bounds building,
        Func<Vector3, bool> clear, Func<Vector3, Vector3, bool> sweep)
    {
        var positions = new Dictionary<Vector2Int, Vector3>();
        var parents = new Dictionary<Vector2Int, Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        positions[Vector2Int.zero] = origin; visited.Add(Vector2Int.zero); queue.Enqueue(Vector2Int.zero);
        building.Expand(4); int examined = 0;
        while (queue.Count > 0 && examined++ < 2048)
        {
            var current = queue.Dequeue(); Vector3 from = positions[current];
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                var next = current + new Vector2Int(x, z); if (visited.Contains(next)) continue;
                var sample = new Vector3(origin.x + next.x * .8f, from.y, origin.z + next.y * .8f);
                if (sample.x < building.min.x || sample.x > building.max.x || sample.z < building.min.z || sample.z > building.max.z
                    || !Physics.Raycast(sample + Vector3.up * .35f, Vector3.down, out var hit, .8f, 1 << 6, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(hit.normal)) { visited.Add(next); continue; }
                Vector3 point = hit.point + Vector3.up * .04f;
                if (!clear(point)) { visited.Add(next); continue; }
                if (!sweep(from, point)) continue;
                visited.Add(next); positions[next] = point; parents[next] = current; queue.Enqueue(next);
            }
        }
        foreach (var entry in positions)
        {
            if ((entry.Value - goal).sqrMagnitude >= 1 || !sweep(entry.Value, goal)) continue;
            var route = new List<Vector3> { goal }; var current = entry.Key;
            while (current != Vector2Int.zero) { route.Add(positions[current]); current = parents[current]; }
            route.Reverse(); return route.ToArray();
        }
        return Array.Empty<Vector3>();
    }
}
