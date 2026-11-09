using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real physics/graph traversal and synthetic Photon checks in a disposable batch project.</summary>
public static class SmasherWanderExplorationChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, P).Invoke(o, a);
    static void Check(bool b, string label) { if (!b) throw new Exception(label); }
    sealed class Actor : Player { public Actor(int id, bool local = false) : base("descent test", id, local) { } }

    public static void Run()
    {
        Check(Application.isBatchMode, "Batch mode only");
        var failures = new List<string>();
        foreach (Action test in new Action[] { TieredArcs, TieredRevalidation, TierContinuation, RetreatTierProfile, SelectionBalance, UpwardExploration, ReturnSafetyAndMemory,
            BroadAndSmallRoofs, StagedDescent, WalkingPreference,
            CancellationAndRevalidation, BackoffAndAllocations, CrowdScheduling, Network, PoliceRoofs, SmasherTallBuildingChecks.Run })
        {
            try { test(); Debug.Log("WANDER EXPLORATION PASS: " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.Name); Debug.LogException(e); }
        }
        Check(failures.Count == 0, "Exploration failures: " + string.Join(", ", failures));
        Debug.Log("SmasherWanderExplorationChecks: ALL PASSED");
        // This is the batch entry point. Package background tasks can otherwise
        // keep -quit waiting after every synchronous regression has completed.
        UnityEditor.EditorApplication.Exit(0);
    }

    static GameObject Box(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Support"); go.layer = 6; go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size; return go;
    }

    // One sorted chunk is enough for nearest-node and connected-wander production queries.
    static CrawlGraph Graph(Func<int, int, float> height)
    {
        var g = new CrawlGraph { Origin = new Vector3(-40.5f, -.5f, -40.5f), CellSize = 1,
            DimX = 81, DimY = 32, DimZ = 81, ChunkCells = 128, ChunksX = 1, ChunksY = 1, ChunksZ = 1 };
        var points = new List<Vector3>();
        for (int z = -36; z <= 36; z++) for (int x = -36; x <= 36; x++) points.Add(new Vector3(x, height(x, z), z));
        int Key(Vector3 p) { g.CellOf(p, out int x, out int y, out int z); return g.KeyOf(x, y, z); }
        points.Sort((a, b) => Key(a).CompareTo(Key(b)));
        int n = points.Count; g.NodeCount = n; g.ChunkStart = new[] { 0, n };
        g.NodePos = new float[n * 3]; g.NodeKey = new int[n]; g.NodeNormal = new int[n];
        g.NodeSurface = new byte[n]; g.NodeCluster = new int[n]; g.EdgeStart = new int[n + 1];
        var index = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < n; i++)
        {
            Vector3 p = points[i]; index[new Vector2Int((int)p.x, (int)p.z)] = i;
            g.NodePos[i * 3] = p.x; g.NodePos[i * 3 + 1] = p.y; g.NodePos[i * 3 + 2] = p.z;
            g.NodeKey[i] = Key(p); g.NodeNormal[i] = CrawlGraph.PackNormal(Vector3.up);
        }
        var edges = new List<int>();
        foreach (var p in points)
        {
            int i = index[new Vector2Int((int)p.x, (int)p.z)]; g.EdgeStart[i] = edges.Count;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                if (index.TryGetValue(new Vector2Int((int)p.x, (int)p.z) + d, out int to)
                    && Mathf.Abs(points[to].y - p.y) <= .3f) edges.Add(to);
        }
        g.EdgeStart[n] = edges.Count; g.EdgeTarget = edges.ToArray();
        return g;
    }

    static Enemy Setup(float halfWidth, float height, out ZombieSmasherJump jump, out EnemyManager manager,
        out GameObject floor, bool staged = false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        floor = Box(new Vector3(0, -.5f, 0), new Vector3(100, 1, 100));
        if (staged) Box(new Vector3(0, 5, 0), new Vector3(16, 10, 16));
        Box(new Vector3(0, height / 2, 0), new Vector3(halfWidth * 2, height, halfWidth * 2));
        manager = SmasherTallBuildingChecks.Manager();
        Set(manager, "worldGraph", Graph((x, z) => Mathf.Abs(x) < halfWidth && Mathf.Abs(z) < halfWidth
            ? height : staged && Mathf.Abs(x) < 8 && Mathf.Abs(z) < 8 ? 10 : 0));
        var enemy = SmasherTallBuildingChecks.MakeActor(out jump);
        enemy.Teleport(Vector3.up * height, Quaternion.identity); Set(enemy, "_hadAuthority", true);
        Set(jump, "_nextWanderCheck", 0f);
        Physics.SyncTransforms();
        return enemy;
    }

    static int Plan(Enemy enemy, int desired = -1, bool choose = true)
    {
        var ground = enemy.GetComponent<GroundLocomotion>();
        var jump = enemy.GetComponent<ZombieSmasherJump>();
        var manager = Object.FindFirstObjectByType<EnemyManager>(FindObjectsInactive.Include);
        // Geometry regressions select a particular intention through the real RNG
        // policy. Distribution and deadline behavior are checked separately.
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (choose) Set(jump, "_nextWanderCheck", 0f);
            ground.RequestWander(manager); ground.RunPathRequest(manager);
            if (choose && Get<int>(jump, "_wanderElevation") != desired) { ground.ClearPath(); continue; }
            int services = 1;
            while (ground.WantsPath && services++ < 66) ground.RunPathRequest(manager);
            Check(!ground.WantsPath && services <= 65, "Sixteen candidates, at most two services per direction plus setup");
            return services;
        }
        throw new Exception("Could not select exploration intention " + desired);
    }

    static int ReachStreet(Enemy enemy, ZombieSmasherJump jump, EnemyManager manager, Action<int> observe = null)
    {
        int landings = 0; jump.Landed += () => landings++;
        var ground = enemy.GetComponent<GroundLocomotion>();
        for (int frame = 0; frame < 6000; frame++)
        {
            if (ground.WantsPath) ground.RunPathRequest(manager);
            enemy.TickAuthority(manager, 1f / 30, false);
            observe?.Invoke(frame);
            if (!jump.IsJumping && landings > 0 && enemy.Root.position.y < 1) return landings;
            // Batch edit-mode Time.time is constant. Advance only the planner/cooldown
            // deadlines after landing; movement and wander pauses use their real dt.
            if (!jump.IsJumping && landings > 0)
            {
                Set(jump, "_nextCheck", 0f);
                if (ground.PathDone && !ground.WantsPath && !Get<bool>(jump, "_hasWanderExit")) Plan(enemy);
            }
        }
        throw new Exception($"Did not reach street: pos={enemy.Root.position}, state={enemy.CurrentState}, "
            + $"landings={landings}, exit={Get<bool>(jump, "_hasWanderExit")}, path={ground.PathDone}, pending={ground.WantsPath}");
    }

    static void FindPlan(Enemy enemy, ZombieSmasherJump jump, EnemyManager manager, int intention)
    {
        var ground = enemy.GetComponent<GroundLocomotion>();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Plan(enemy, intention);
            if (Get<bool>(jump, "_hasWanderExit")) return;
            for (int frame = 0; frame < 2000 && !ground.PathDone; frame++)
                enemy.TickAuthority(manager, 1f / 30, false);
        }
        throw new Exception("No reversible exploration route from " + enemy.Root.position);
    }

    static void TieredArcs()
    {
        float[] walls = { 0, 9, 11.5f, 15, 25 };
        float[] peaks = { 1.25f, 3, 6, 12, 0 };
        for (int i = 0; i < walls.Length; i++)
        {
            Setup(4, 8, out var jump, out var manager, out _);
            if (walls[i] > 0) Box(new Vector3(0, walls[i] / 2, 10.6f), new Vector3(30, walls[i], .2f));
            Physics.SyncTransforms(); Set(jump, "_wanderElevation", -1);
            var result = jump.PlanWanderExit(manager, new Vector3(0, 8, 3), Vector3.forward, true);
            if (i >= 2)
            {
                Check(result == ZombieSmasherJump.WanderPlanResult.Pending && Get<int>(jump, "_wanderArcAttempt") == 2
                    && !Get<bool>(jump, "_wanderReturnPending"), "Higher tiers yield after exactly two blocked arcs");
                result = jump.PlanWanderExit(manager, new Vector3(0, 8, 3), Vector3.forward, true);
            }
            if (peaks[i] > 0)
            {
                Check(result == ZombieSmasherJump.WanderPlanResult.Pending && Get<bool>(jump, "_wanderReturnPending"),
                    "Forward arc completes before the separate return check, wall=" + walls[i]);
                Check(Mathf.Abs(Get<float>(jump, "_apex") - peaks[i]) < .001f,
                    $"Lowest clear tier for wall {walls[i]}: expected {peaks[i]}, got {Get<float>(jump, "_apex")}");
            }
            else Check(result == ZombieSmasherJump.WanderPlanResult.Rejected && Get<int>(jump, "_wanderArcAttempt") == 0,
                "All four blocked tiers reject and clear the continuation");
            Debug.Log($"ARC TIER PASS wall={walls[i]} peak={Get<float>(jump, "_apex")} result={result}");
        }
        // A low configured cap collapses every tier to the same height. Do not
        // spend an extra service testing duplicate trajectories.
        Setup(4, 8, out var cappedJump, out var cappedManager, out _);
        Box(new Vector3(0, 12.5f, 10.6f), new Vector3(30, 25, .2f)); Physics.SyncTransforms();
        Set(cappedJump, "maxJumpHeight", 1.25f); Set(cappedJump, "_wanderElevation", -1);
        Check(cappedJump.PlanWanderExit(cappedManager, new Vector3(0, 8, 3), Vector3.forward, true)
            == ZombieSmasherJump.WanderPlanResult.Rejected, "Duplicate capped tiers finish in one service");
    }

    static void TieredRevalidation()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        Box(new Vector3(0, 5.4f, 10.6f), new Vector3(30, 10.8f, .2f)); Physics.SyncTransforms();
        Vector3 from = new Vector3(0, 8, 3);
        enemy.Teleport(from, Quaternion.identity); Set(enemy, "_hadAuthority", true);
        Set(jump, "_wanderElevation", -1);
        for (int service = 0; service < 4; service++)
        {
            var result = jump.PlanWanderExit(manager, from, Vector3.forward, true);
            Debug.Log($"TIER REVALIDATION SETUP service={service} result={result} attempt={Get<int>(jump, "_wanderArcAttempt")} apex={Get<float>(jump, "_apex")}");
            Check(result == (service == 3 ? ZombieSmasherJump.WanderPlanResult.Found : ZombieSmasherJump.WanderPlanResult.Pending),
                "Forward and reverse searches both resume, service=" + service);
        }
        var ground = enemy.GetComponent<GroundLocomotion>(); Set(ground, "_wanderJump", jump);
        Check(jump.TickWanderExit(enemy) && ground.WantsPath, "Takeoff uses the navigation queue");
        for (int service = 0; service < 4; service++)
        {
            ground.RunPathRequest(manager);
            Check(ground.WantsPath == (service < 3) && Get<bool>(jump, "_wanderReady") == (service == 3),
                "Takeoff cannot launch before both resumed directions finish, service=" + service);
        }
        Check(Get<float>(jump, "_apex") == 6f, "Takeoff retains the selected forward arc after reverse checking");
        Check(CompleteJump(enemy, jump, manager) == 1 && enemy.Root.position.y < .1f,
            "Resumed 6-unit takeoff completes and lands once");
    }

    static void TierContinuation()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out var floor);
        var wall = Box(new Vector3(0, 12.5f, 10.6f), new Vector3(30, 25, .2f));
        Physics.SyncTransforms(); Set(jump, "_wanderElevation", -1);
        Vector3 from = new Vector3(0, 8, 3);
        jump.PlanWanderExit(manager, from, Vector3.forward, true);
        Check(Get<int>(jump, "_wanderArcAttempt") == 2, "Fixture has a partially evaluated candidate");
        // The initial support can disappear between navigation services.
        floor.SetActive(false); Physics.SyncTransforms();
        Check(jump.PlanWanderExit(manager, from, Vector3.forward, true) == ZombieSmasherJump.WanderPlanResult.Rejected,
            "Continuation rejects removed landing support");
        floor.SetActive(true); Physics.SyncTransforms();
        jump.PlanWanderExit(manager, from, Vector3.forward, true);
        enemy.Teleport(from, Quaternion.identity);
        Check(Get<int>(jump, "_wanderArcAttempt") == 0 && !Get<bool>(jump, "_wanderForwardPending"),
            "Teleport discards partially tested tiers");
        Set(jump, "_wanderElevation", -1); jump.PlanWanderExit(manager, from, Vector3.forward, true);
        enemy.SetRemoteState(Enemy.State.Chase); jump.TickJump(enemy, manager, .02f);
        Check(Get<int>(jump, "_wanderArcAttempt") == 0 && !Get<bool>(jump, "_wanderForwardPending"),
            "State change discards partially tested tiers");
        enemy.SetRemoteState(Enemy.State.Wander);
        wall.SetActive(false); Physics.SyncTransforms();
        Set(jump, "_wanderElevation", -1);
        var result = ZombieSmasherJump.WanderPlanResult.Pending;
        for (int i = 0; i < 4 && result == ZombieSmasherJump.WanderPlanResult.Pending; i++)
            result = jump.PlanWanderExit(manager, from, Vector3.forward, true);
        Check(result == ZombieSmasherJump.WanderPlanResult.Found, "Commit clear fixture before takeoff revalidation");
        wall.SetActive(true); Physics.SyncTransforms();
        // Reverse revalidation is clear without the wall; now block only the
        // forward phase to exercise both resumed takeoff services deterministically.
        Set(jump, "_wanderReturnValidated", true); Set(jump, "_nextCheck", 0f);
        Check(!jump.ValidateWanderTakeoff(enemy, manager.SurfaceMask) && Get<int>(jump, "_wanderArcAttempt") == 2,
            "Takeoff revalidation yields after its first two arcs");
        Check(jump.ValidateWanderTakeoff(enemy, manager.SurfaceMask) && !Get<bool>(jump, "_wanderReady")
            && !jump.HasWanderExit, "Blocked resumed takeoff cancels without launching");

        // Force every forward candidate through all tiers without reflection in
        // the measured region. This includes the continuation and reset paths.
        Set(jump, "_wanderElevation", -1);
        for (int i = 0; i < 10; i++)
        {
            jump.PlanWanderExit(manager, from, Vector3.forward, true);
            jump.PlanWanderExit(manager, from, Vector3.forward, true);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < 200; i++)
        {
            jump.PlanWanderExit(manager, from, Vector3.forward, true);
            jump.PlanWanderExit(manager, from, Vector3.forward, true);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - stamp) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        Check(allocated == 0, "Resumed tier planning allocates zero bytes: " + allocated);
        Debug.Log($"TIER CONTINUATION PROFILE services=400 allocated={allocated} mean={ms / 400:F3}ms");
    }

    static void RetreatTierProfile()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        enemy.SetRemoteState(Enemy.State.Retreat);
        // All escape directions hit this roof, exercising the bounded failed scan.
        Box(new Vector3(0, 9.4f, 0), new Vector3(90, .2f, 90)); Physics.SyncTransforms();
        var reset = (Action)Delegate.CreateDelegate(typeof(Action), jump, "ResetJump");
        for (int i = 0; i < 5; i++) { reset(); jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.forward); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        bool launched = false;
        for (int i = 0; i < 200; i++)
        {
            reset(); launched |= jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.forward);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - stamp) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        Check(!launched && allocated == 0, "Blocked retreat tiers reject without allocations");
        float deadline = Get<float>(jump, "_nextRetreatCheck");
        for (int i = 0; i < 100; i++) jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.forward);
        Check(deadline == Get<float>(jump, "_nextRetreatCheck"), "Retreat keeps its retry backoff");
        Debug.Log($"RETREAT TIER PROFILE scans=200 allocated={allocated} mean={ms / 200:F3}ms");
    }

    static void SelectionBalance()
    {
        var pick = (Func<float, float, int>)Delegate.CreateDelegate(typeof(Func<float, float, int>),
            typeof(ZombieSmasherJump).GetMethod("SelectWanderElevation", BindingFlags.Static | BindingFlags.NonPublic));
        foreach (float linger in new[] { 0f, 120f })
        {
            int walk = 0, up = 0, down = 0;
            for (int i = 0; i < 1000; i++)
            {
                int intent = pick((i + .5f) / 1000, linger);
                if (intent == 0) walk++; else if (intent > 0) up++; else down++;
            }
            Check(walk == (linger == 0 ? 600 : 200) && up == down,
                "Balanced 60/20/20 choices; lingering increases up/down equally");
        }
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        UnityEngine.Random.InitState(724);
        int[] counts = new int[3];
        for (int i = 0; i < 1000; i++)
        {
            Set(jump, "_nextWanderCheck", 0f);
            counts[jump.ChooseWanderElevation(enemy) + 1]++;
            float deadline = Get<float>(jump, "_nextWanderCheck");
            Check(deadline >= Time.time + 10 && deadline <= Time.time + 20, "Exploration interval is 10–20 seconds");
            Check(jump.ChooseWanderElevation(enemy) == 0 && Get<float>(jump, "_nextWanderCheck") == deadline,
                "No per-frame reroll or deadline extension");
        }
        Check(counts[1] > 550 && counts[1] < 650 && Math.Abs(counts[0] - counts[2]) < 70,
            "Real randomized choices retain the intended distribution");
        // Upward intention on a roof with no higher destination must not become a drop.
        Plan(enemy, 1);
        Check(!Get<bool>(jump, "_hasWanderExit") && !jump.IsJumping, "Failed ascent falls back to walking, not descent");
        enemy.Root.position += Vector3.right * 3;
        Check(!jump.WanderExplorationDue(enemy), "Movement does not bypass exploration backoff");
        Set(jump, "_wanderLevelSince", Time.time - 120); Set(jump, "_wanderLevelKnown", true);
        Set(jump, "_nextWanderCheck", 0f); jump.ChooseWanderElevation(enemy);
        Check(Get<float>(jump, "_wanderLevelSince") <= Time.time - 119, "Horizontal wandering preserves lingering history");
        enemy.Root.position += Vector3.up * 3; jump.ChooseWanderElevation(enemy);
        Check(Get<float>(jump, "_wanderLevelSince") == Time.time, "A new elevation resets lingering history");
        Debug.Log($"WANDER CHOICES walk={counts[1]} up={counts[2]} down={counts[0]}");
    }

    static int CompleteJump(Enemy enemy, ZombieSmasherJump jump, EnemyManager manager, Action<int> observe = null)
    {
        int landings = 0; Action landed = () => landings++; jump.Landed += landed;
        var ground = enemy.GetComponent<GroundLocomotion>();
        try
        {
            for (int frame = 0; frame < 6000; frame++)
            {
                if (ground.WantsPath) ground.RunPathRequest(manager);
                enemy.TickAuthority(manager, 1f / 30, false); observe?.Invoke(frame);
                if (landings > 0 && !jump.IsJumping) return landings;
            }
            throw new Exception("Exploration jump never landed from " + enemy.Root.position);
        }
        finally { jump.Landed -= landed; }
    }

    static void UpwardExploration()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        enemy.Teleport(new Vector3(0, 0, -7), Quaternion.identity); Set(enemy, "_hadAuthority", true);
        FindPlan(enemy, jump, manager, 1);
        Check(Get<Vector3>(jump, "_wanderLanding").y > 7.9f, "Wander ascent selects the roof");
        Check(CompleteJump(enemy, jump, manager) == 1 && enemy.Root.position.y > 7.9f,
            "A wandering smasher can climb to a higher roof");
        Check(Get<bool>(jump, "_wanderMemory") && Get<Vector3>(jump, "_wanderLastFrom").y < .1f,
            "Remember actual departure and landing after a successful exploration jump");
        Check(!jump.WanderExplorationDue(enemy), "Landing starts a fresh exploration pause");
    }

    static void ReturnSafetyAndMemory()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        Set(jump, "maxJumpHeight", 4f); Set(jump, "_wanderElevation", -1);
        var forward = jump.PlanWanderExit(manager, new Vector3(0, 8, 3), Vector3.forward, false);
        Check(forward == ZombieSmasherJump.WanderPlanResult.Pending, "One-way descent is physically possible");
        var reverse = jump.PlanWanderExit(manager, new Vector3(0, 8, 3), Vector3.forward, false);
        Check(reverse == ZombieSmasherJump.WanderPlanResult.Rejected && !Get<bool>(jump, "_hasWanderExit"),
            "Do not voluntarily enter a depression without a return route");

        enemy = Setup(4, 8, out jump, out manager, out var floor);
        Plan(enemy); Vector3 previousLanding = Get<Vector3>(jump, "_wanderLanding");
        Set(jump, "_wanderMemory", true); Set(jump, "_wanderLastJump", Time.time);
        Set(jump, "_wanderLastFrom", previousLanding); Set(jump, "_wanderLastTo", enemy.Root.position);
        Set(jump, "_wanderDirection", 0); Plan(enemy);
        Check(Get<bool>(jump, "_hasWanderExit") && (Get<Vector3>(jump, "_wanderLanding") - previousLanding).sqrMagnitude >= 9,
            "Prefer a different landing instead of immediately returning to the last location");

        // Keep exactly one lower landing direction. A recent return remains usable
        // when the bounded search finds no alternative, rather than trapping the enemy.
        floor.SetActive(false);
        Box(new Vector3(0, -.5f, 6), new Vector3(3, 1, 3)); Physics.SyncTransforms();
        Set(jump, "_wanderLastFrom", new Vector3(0, 0, 6)); Set(jump, "_wanderDirection", 0); Plan(enemy);
        Check(Get<bool>(jump, "_hasWanderExit") && (Get<Vector3>(jump, "_wanderLanding") - new Vector3(0, 0, 6)).sqrMagnitude < 9,
            "Only safe exit can override the backtracking penalty");
        var ground = enemy.GetComponent<GroundLocomotion>();
        enemy.Root.position = Get<Vector3>(jump, "_wanderTakeoff");
        Set(ground, "_pathIndex", Get<int>(ground, "_pathCount"));
        Set(jump, "maxJumpHeight", 4f); Set(jump, "_nextCheck", 0f);
        jump.TickWanderExit(enemy); ground.RunPathRequest(manager);
        Check(!Get<bool>(jump, "_wanderReady") && !Get<bool>(jump, "_hasWanderExit"),
            "Revalidate return safety before committing to takeoff");
    }

    static void BroadAndSmallRoofs()
    {
        foreach (float width in new[] { 20f, 1f })
        {
            var enemy = Setup(width, 11, out var jump, out var manager, out _);
            var ground = enemy.GetComponent<GroundLocomotion>();
            Set(ground, "_pauseTimer", .5f);
            enemy.TickAuthority(manager, .1f, false);
            Check(!ground.WantsPath && !jump.IsJumping, "Normal wander pause is preserved");
            FindPlan(enemy, jump, manager, -1);
            Check(Get<bool>(jump, "_hasWanderExit"), "Find an exit even on a single-node roof");
            float walked = 0;
            int count = ReachStreet(enemy, jump, manager, _ => {
                if (!jump.IsJumping) walked = Mathf.Max(walked, Vector3.ProjectOnPlane(enemy.Root.position, Vector3.up).magnitude);
            });
            Check(count == 1 && enemy.CurrentState == Enemy.State.Wander, "Descend once and remain wandering");
            if (width == 20) Check(walked > 2, "Broad roof needs a walking approach");
        }
    }

    static void StagedDescent()
    {
        var enemy = Setup(2, 20, out var jump, out var manager, out _, true);
        Plan(enemy);
        Check(ReachStreet(enemy, jump, manager) == 2, "Use intermediate roof instead of an excessive direct drop");
    }

    static void WalkingPreference()
    {
        var enemy = Setup(20, 11, out var jump, out var manager, out _);
        // Connected descending route stands in for the baked stair/ramp graph.
        Set(manager, "worldGraph", Graph((x, z) => 11 - Mathf.Clamp(x, 0, 12) * .25f));
        Plan(enemy);
        var ground = enemy.GetComponent<GroundLocomotion>();
        int goal = Get<int>(ground, "_goalNode");
        Check(goal >= 0 && manager.Graph.GetPosition(goal).y <= 9.75f, "Downward intention can use a connected walking route");
        Check(!Get<bool>(jump, "_hasWanderExit") && !jump.IsJumping, "Walking route avoids jump planning");
    }

    static void CancellationAndRevalidation()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out var floor);
        var ground = enemy.GetComponent<GroundLocomotion>();
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Set(jump, "_nextWanderCheck", 0f); ground.RequestWander(manager); ground.RunPathRequest(manager);
            if (Get<bool>(ground, "_wanderSearch")) break;
        }
        Check(ground.WantsPath && Get<bool>(ground, "_wanderSearch"), "Cancellation fixture has queued exploration");
        enemy.SetRemoteState(Enemy.State.Chase); ground.RunPathRequest(manager);
        Check(!ground.WantsPath && !Get<bool>(jump, "_hasWanderExit"), "Chase cancels queued wander planning");
        enemy.SetRemoteState(Enemy.State.Wander); Set(jump, "_nextWanderCheck", 0f); Plan(enemy);
        Check(Get<bool>(jump, "_hasWanderExit"), "Exit committed for cancellation fixture");
        enemy.SetRemoteState(Enemy.State.Chase); jump.TickJump(enemy, manager, .02f);
        Check(!Get<bool>(jump, "_hasWanderExit") && !jump.IsJumping, "Chase cancels committed exit before takeoff");
        enemy.SetRemoteState(Enemy.State.Wander); Set(jump, "_nextWanderCheck", 0f); Plan(enemy);
        enemy.Root.position = Get<Vector3>(jump, "_wanderTakeoff");
        Set(ground, "_pathIndex", Get<int>(ground, "_pathCount"));
        floor.SetActive(false); Physics.SyncTransforms();
        Set(jump, "_nextCheck", Time.time + 10f);
        Check(jump.TickWanderExit(enemy) && !ground.WantsPath, "Arrival respects existing jump cooldown");
        Set(jump, "_nextCheck", 0f);
        Check(jump.TickWanderExit(enemy) && ground.WantsPath, "Arrival queues budgeted revalidation");
        ground.RunPathRequest(manager);
        Check(!Get<bool>(jump, "_wanderReady") && !Get<bool>(jump, "_hasWanderExit") && !jump.IsJumping,
            "Removed landing support cancels the exit safely");
        floor.SetActive(true); Physics.SyncTransforms(); Set(jump, "_nextWanderCheck", 0f); Plan(enemy);
        enemy.Teleport(Vector3.up * 8, Quaternion.identity);
        Check(!Get<bool>(jump, "_hasWanderExit") && !ground.WantsPath, "Teleport clears exit and queued work");
    }

    static void BackoffAndAllocations()
    {
        var enemy = Setup(4, 8, out var jump, out var manager, out _);
        Box(new Vector3(0, 9.4f, 0), new Vector3(90, .2f, 90)); Physics.SyncTransforms();
        var ground = enemy.GetComponent<GroundLocomotion>();
        Plan(enemy);
        Check(!Get<bool>(jump, "_hasWanderExit"), "Ceiling prevents unsafe exits");
        float deadline = Get<float>(jump, "_nextWanderCheck");
        Check(deadline >= Time.time + 19.9f && !jump.WanderExplorationDue(enemy), "Failed scan backs off for 20–30 seconds");
        Plan(enemy, choose: false);
        Check(Get<float>(jump, "_nextWanderCheck") == deadline && !Get<bool>(ground, "_wanderSearch"),
            "Normal wandering does not restart a blocked scan during backoff");
        // Warm all scratch buffers; keep reflection and reporting out of measured work.
        for (int i = 0; i < 3; i++) { Set(jump, "_nextWanderCheck", 0f); Plan(enemy); }
        long allocated = 0; double worst = 0;
        for (int pass = 0; pass < 20; pass++)
        {
            Set(jump, "_nextWanderCheck", 0f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            ground.RequestWander(manager);
            int services = 0;
            while (ground.WantsPath && services++ < 66)
            {
                long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
                ground.RunPathRequest(manager);
                worst = Math.Max(worst, (System.Diagnostics.Stopwatch.GetTimestamp() - stamp) * 1000d / System.Diagnostics.Stopwatch.Frequency);
            }
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Check(allocated == 0, "Wander exit planning allocated " + allocated);
        Debug.Log($"WANDER EXPLORATION PROFILE cycles=20 allocated={allocated} worstService={worst:F3}ms");
    }

    static void CrowdScheduling()
    {
        var first = Setup(4, 8, out var firstJump, out var manager, out _);
        Box(new Vector3(0, 9.4f, 0), new Vector3(90, .2f, 90)); Physics.SyncTransforms();
        var actors = new List<Enemy> { first };
        var motors = new GroundLocomotion[30];
        for (int i = 1; i < 30; i++)
        {
            var enemy = SmasherTallBuildingChecks.MakeActor(out var jump);
            enemy.Teleport(Vector3.up * 8, Quaternion.identity); Set(enemy, "_hadAuthority", true);
            Set(jump, "_nextWanderCheck", 0f); actors.Add(enemy);
        }
        for (int i = 0; i < 30; i++) { motors[i] = actors[i].GetComponent<GroundLocomotion>(); motors[i].RequestWander(manager); }
        var service = (Action<List<Enemy>>)Delegate.CreateDelegate(typeof(Action<List<Enemy>>), manager, "ServicePathRequest");
        int[] previous = new int[30];
        int frames = 0;
        for (; frames < 30 * 65; frames++)
        {
            for (int i = 0; i < 30; i++) previous[i] = Get<int>(motors[i], "_wanderCandidate");
            service(actors);
            int changed = 0;
            for (int i = 0; i < 30; i++)
                if (previous[i] != Get<int>(motors[i], "_wanderCandidate")) changed++;
            Check(changed <= 1, "Only one enemy advances its exit scan per navigation service");
        }
        foreach (var motor in motors) Check(!motor.WantsPath, "Every waiting smasher receives a bounded planning turn");
        Debug.Log($"WANDER CROWD PASS actors=30 services={frames} no scan bursts or starvation");
    }

    static void Network()
    {
        NetworkCase(false);
        NetworkCase(true);
        NetworkCase(false, true);
    }

    static void NetworkCase(bool upward, bool tiered = false)
    {
        var enemy = Setup(4, tiered ? 8 : 11, out var jump, out var manager, out _);
        if (tiered)
        {
            Box(new Vector3(0, 4.5f, 10.6f), new Vector3(30, 9, .2f));
            enemy.Teleport(new Vector3(0, 8, 3), Quaternion.identity); Set(enemy, "_hadAuthority", true);
            Physics.SyncTransforms();
        }
        if (upward) { enemy.Teleport(new Vector3(0, 0, -8), Quaternion.identity); Set(enemy, "_hadAuthority", true); }
        var remote = SmasherTallBuildingChecks.MakeActor(out var remoteJump);
        var ownerNet = enemy.gameObject.AddComponent<EnemyNetSync>();
        var remoteNet = remote.gameObject.AddComponent<EnemyNetSync>();
        Call(ownerNet, "Awake"); Call(remoteNet, "Awake"); Call(enemy, "Awake"); Call(remote, "Awake");
        var client = PhotonNetwork.NetworkingClient;
        var oldRoom = client.CurrentRoom; var oldLocal = client.LocalPlayer; var oldState = client.State;
        try
        {
            var owner = new Actor(1); var observer = new Actor(2, true);
            var room = new Room("synthetic wander descent", new RoomOptions());
            room.LoadBalancingClient = client; room.AddPlayer(owner); room.AddPlayer(observer); client.CurrentRoom = room;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, observer); client.State = ClientState.Joined;
            ownerNet.photonView.ControllerActorNr = 1; remoteNet.photonView.ControllerActorNr = 1;
            typeof(PhotonView).GetProperty("IsMine").SetValue(ownerNet.photonView, true);
            if (tiered)
            {
                Set(jump, "_wanderElevation", -1);
                var result = ZombieSmasherJump.WanderPlanResult.Pending;
                for (int i = 0; i < 4 && result == ZombieSmasherJump.WanderPlanResult.Pending; i++)
                    result = jump.PlanWanderExit(manager, enemy.Root.position, Vector3.forward, true);
                Check(result == ZombieSmasherJump.WanderPlanResult.Found, "Network fixture has a reversible intermediate arc");
                // Direct candidate setup bypasses BuildWanderPath, which normally
                // binds this motor to the traversal used by queued revalidation.
                Set(enemy.GetComponent<GroundLocomotion>(), "_wanderJump", jump);
            }
            else FindPlan(enemy, jump, manager, upward ? 1 : -1);
            bool receivedAir = false;
            void Send(int frame)
            {
                var stream = new PhotonStream(true, null); Call(stream, "SetWriteStream", new List<object>(), 0);
                ownerNet.OnPhotonSerializeView(stream, default);
                byte[] bytes = EnemyPosePacket.Serialize(stream.ToArray()[0]);
                Check(bytes.Length == EnemyPosePacket.PacketBytes, "No additional network payload");
                remoteNet.OnPhotonSerializeView(new PhotonStream(false, new[] { EnemyPosePacket.Deserialize(bytes) }),
                    new PhotonMessageInfo(owner, 1000 + frame * 33, remoteNet.photonView));
                Check(remote.CurrentState == Enemy.State.Wander && Get<bool>(remote, "_airborne") == jump.IsJumping,
                    "Remote receives Wander state and descent airborne flag");
                receivedAir |= jump.IsJumping;
                if (tiered && jump.IsJumping) Check(Mathf.Abs(Get<float>(jump, "_apex") - 3f) < .001f,
                    "Network fixture uses the 3-unit fallback in actual flight");
            }
            int frames = 0;
            CompleteJump(enemy, jump, manager, frame => {
                frames = frame; if (frame % 3 == 0 || !jump.IsJumping) Send(frame);
                Check(!remoteJump.TickJump(remote, manager, 1f / 30) && !remoteJump.WanderExplorationDue(remote),
                    "Observer never plans or simulates wandering jumps");
                remote.ApplyRemote(1f / 30);
            });
            Send(frames + 1);
            for (int i = 0; i < 90; i++) remote.ApplyRemote(1f / 30);
            Check(receivedAir && !Get<bool>(remote, "_airborne")
                && Vector3.Distance(remote.Root.position, enemy.Root.position) < .01f, "Remote settles at exploration landing");
            // Midair ownership changes must use the existing safe-fall recovery.
            remote.Teleport(enemy.Root.position + Vector3.up * 5, Quaternion.identity);
            remoteNet.photonView.ControllerActorNr = 2; remoteNet.OnControllerChange(observer, owner);
            for (int i = 0; i < 90; i++) remote.TickAuthority(manager, 1f / 30, false);
            Check(!remoteJump.IsJumping && Mathf.Abs(remote.Root.position.y - enemy.Root.position.y) < .1f,
                "New authority finishes the fall safely");
            Debug.Log($"WANDER NETWORK PASS upward={upward} intermediateArc={tiered}: 22-byte transport, observer, landing, handoff");
        }
        finally
        {
            Object.DestroyImmediate(enemy.gameObject); Object.DestroyImmediate(remote.gameObject);
            client.CurrentRoom = oldRoom; typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, oldLocal);
            client.State = oldState;
        }
    }

    static void PoliceRoofs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Quackville.unity", OpenSceneMode.Single); Physics.SyncTransforms();
        var manager = SmasherTallBuildingChecks.Manager();
        var graph = CrawlGraph.Load(System.IO.File.ReadAllBytes("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        Set(manager, "worldGraph", graph);
        Set(manager, "<Pathfinder>k__BackingField", new CrawlPathfinder(graph));
        foreach (Vector3 roof in new[] { new Vector3(80.23f, 10.46f, 41), new Vector3(85, 7.91f, 34) })
        {
            var enemy = SmasherTallBuildingChecks.MakeActor(out var jump);
            enemy.Teleport(roof, Quaternion.identity); Set(enemy, "_hadAuthority", true); Set(jump, "_nextWanderCheck", 0f);
            FindPlan(enemy, jump, manager, -1);
            int landings = ReachStreet(enemy, jump, manager);
            Check(enemy.Root.position.y < 1, "Police smasher returns to street");
            Debug.Log($"POLICE WANDER EXIT start={roof} end={enemy.Root.position} jumps={landings}");
            Object.DestroyImmediate(enemy.gameObject);
        }
    }
}
