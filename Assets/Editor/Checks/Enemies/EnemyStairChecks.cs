using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only physics fixtures and a read-only audit of the shipped scene/graph.
public static class EnemyStairChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const int Mask = 1 << 6;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Private).Invoke(o, args);
    delegate Vector3 Move(Vector3 pos, Vector3 delta, LayerMask mask);

    public static void ChaseRoutingRegressions()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var graph = CrawlGraph.Load(System.IO.File.ReadAllBytes("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        var managerObject = new GameObject("Chase routing manager");
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "<Graph>k__BackingField", graph);
        Set(manager, "surfaceMask", (LayerMask)Mask);
        foreach (string name in new[] { "ZombieMinion", "ZombieBomber", "ZombieSmasher" })
        {
            var mover = Actor(name);
            var enemy = mover.gameObject.AddComponent<Enemy>();
            Call(enemy, "Awake");
            foreach (var position in new[] { new Vector3(98, 13.5f, 15), new Vector3(78, 4.5f, 39.5f) })
            {
                int node = (int)Call(mover, "CurrentNode", manager, position);
                Check(node >= 0 && Mathf.Abs(graph.GetPosition(node).y - position.y) < .2f,
                    name + " start node snapped to another storey at " + position);
            }
            Vector3 start = new Vector3(98, 13.5f, 15);
            enemy.Root.position = start;
            int next = graph.FindNearestGroundNode(new Vector3(97.5f, 13.5f, 15), 1);
            int upper = graph.FindNearestGroundNode(new Vector3(98, 17, 15), 1);
            Check(next >= 0 && upper >= 0, "Missing chase regression nodes");
            Get<int[]>(mover, "_path")[0] = next;
            Set(mover, "_pathCount", 1); Set(mover, "_pathIndex", 0);
            Set(mover, "_pathComplete", true); Set(mover, "_goalNode", upper);
            enemy.SetRemoteState(Enemy.State.Chase);
            Set(enemy, "_seen", true); Set(enemy, "_lastSeenPos", start + Vector3.up * 5);
            mover.TickMove(enemy, manager, 1f / 60);
            Check(Vector3.Distance(enemy.Root.position, start) > .01f,
                name + " stopped below an upstairs target instead of following the stair approach");
            enemy.Root.position = start;
            Set(mover, "_goalNode", graph.FindNearestGroundNode(start, 1));
            Set(enemy, "_lastSeenPos", start + Vector3.up * 1.5f);
            mover.TickMove(enemy, manager, 1f / 60);
            Check(Vector3.Distance(enemy.Root.position, start) < .001f,
                name + " no longer stops beside a target on the same floor");
            UnityEngine.Object.DestroyImmediate(mover.gameObject);
        }
        UnityEngine.Object.DestroyImmediate(managerObject);
        Debug.Log("EnemyStairChecks: CHASE START FLOOR / UPSTAIRS ROUTE / SAME-FLOOR STOP PASSED");
    }

    public static void Run()
    {
        Check(Application.isBatchMode, "Batch mode only");
        foreach (string name in new[] { "ZombieSmasher", "ZombieBomber", "ZombieMinion" })
        foreach (float dt in new[] { 1f / 90f, 1f / 30f })
        foreach (float riser in new[] { .2f, .3f })
            Flight(name, dt, riser);
        Reject("tall wall", false);
        Reject("low ceiling", true);
        SmasherBarrier();
        FlatCost();
        SpiderStairs();
        SpiderRoofLipChecks.Run();
        GroundRecoveryChecks.SpiderSurfaceRecovery();
        Debug.Log("EnemyStairChecks: ALL PASSED");
    }

    static GroundLocomotion Actor(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab");
        Check(prefab != null, "Missing " + name);
        // Use the production collider dimensions and root scale without running
        // audio/network/brain lifecycle code in this isolated motor fixture.
        var source = prefab.GetComponent<GroundLocomotion>();
        var sourceEnemy = prefab.GetComponent<Enemy>();
        var actor = new GameObject(name);
        var mover = actor.AddComponent<GroundLocomotion>();
        EditorUtility.CopySerialized(source, mover);
        Transform root = Get<Transform>(sourceEnemy, "root");
        actor.transform.localScale = root != null ? root.lossyScale : prefab.transform.lossyScale;
        return mover;
    }

    static void Flight(string name, float dt, float rise)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(30, 1, 10));
        const int count = 12;
        const float tread = .32f;
        for (int i = 0; i < count; i++)
            Box(new Vector3(1 + i * tread + tread / 2, (i + 1) * rise / 2, 0),
                new Vector3(tread, (i + 1) * rise, 4));
        Box(new Vector3(7, count * rise / 2, 0), new Vector3(4.32f, count * rise, 4));
        var mover = Actor(name);
        var move = (Move)Delegate.CreateDelegate(typeof(Move), mover, "MoveWithWalls");
        Physics.SyncTransforms();
        if (name == "ZombieSmasher")
        {
            var enemy = mover.gameObject.AddComponent<Enemy>();
            var jump = mover.gameObject.AddComponent<ZombieSmasherJump>();
            Call(enemy, "Awake"); Call(jump, "Awake");
            Set(enemy, "_lastSeenPos", new Vector3(5.5f, count * rise + 1.7f, 0));
            Check(!(bool)Call(jump, "TryPlan", enemy, (LayerMask)Mask), "Smasher must walk ordinary stairs instead of jumping");
        }
        Vector3 pos = Vector3.zero;
        float speed = Get<float>(mover, "chaseSpeed");
        for (int i = 0; i < 1500 && pos.x < 5.5f; i++)
        {
            int attempts = mover.StepAttempts;
            long before = GC.GetAllocatedBytesForCurrentThread();
            Vector3 next = move(pos, Vector3.right * speed * dt, Mask);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, name + " movement allocated " + allocated);
            Check(mover.StepAttempts - attempts <= 1, name + " attempted multiple steps in one tick");
            Check(next.x - pos.x <= speed * dt + GroundLocomotion.CapsuleSkin, name + " exceeded movement speed from " + pos.ToString("F5") + " to " + next.ToString("F5") + " max=" + speed * dt);
            Check(!mover.Overlaps(next, Mask), name + " embedded in stair " + next);
            Check(mover.HasGroundSupport(next, Mask), name + " lost support " + next);
            pos = next;
        }
        Check(pos.x >= 5.5f && Mathf.Abs(pos.y - count * rise) < .05f,
            name + " failed stairs dt=" + dt + " at " + pos + " attempts=" + mover.StepAttempts + " steps=" + mover.StepsTaken);
        Debug.Log($"STAIR PASS {name} riser={rise} dt={dt:F4} pos={pos} attempts={mover.StepAttempts} steps={mover.StepsTaken}");
    }

    static void SpiderStairs()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(20, 1, 10));
        for (int i = 0; i < 6; i++) Box(new Vector3(1 + i * .35f, (i + 1) * .1f, 0), new Vector3(.35f, (i + 1) * .2f, 4));
        Box(new Vector3(4, .6f, 0), new Vector3(2.65f, 1.2f, 4));
        var actor = new GameObject("Unmodified spider stair regression");
        var mover = actor.AddComponent<ClimbLocomotion>();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab").GetComponent<ClimbLocomotion>();
        EditorUtility.CopySerialized(source, mover);
        Physics.SyncTransforms();
        Vector3 pos = Vector3.zero, target = new Vector3(4, 1.2f, 0);
        var graph = new CrawlGraph();
        for (int i = 0; i < 400 && pos.x < 3.5f; i++)
        {
            object[] args = { pos, target, .05f, (LayerMask)Mask, graph, Vector3.zero };
            pos = (bool)Call(mover, "TryStepUp", args) ? (Vector3)args[5]
                : (Vector3)Call(mover, "MoveOnSurface", pos, target, Vector3.up, .05f, (LayerMask)Mask);
        }
        Check(pos.x >= 3.5f && Mathf.Abs(pos.y - 1.2f) < .1f, "Unmodified spider failed stairs at " + pos);
        Debug.Log("STAIR SPIDER PASS (unchanged mover and prefab)");
    }

    static void SmasherBarrier()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(30, 1, 10));
        Box(new Vector3(1.125f, .25f, 0), new Vector3(.04f, .5f, 4));
        var mover = Actor("ZombieSmasher");
        var enemy = mover.gameObject.AddComponent<Enemy>();
        var jump = mover.gameObject.AddComponent<ZombieSmasherJump>();
        Call(enemy, "Awake"); Call(jump, "Awake");
        Physics.SyncTransforms();
        Set(jump, "_start", Vector3.zero);
        Check(!(bool)Call(jump, "CanWalkTo", Vector3.right * 6, (LayerMask)Mask), "Thin barrier must not be mistaken for stairs");
        Debug.Log("STAIR SMASHER BARRIER PASS");
    }

    static void Reject(string label, bool ceiling)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(20, 1, 10));
        Box(new Vector3(3, ceiling ? .125f : 1f, 0), new Vector3(4, ceiling ? .25f : 2f, 4));
        if (ceiling) Box(new Vector3(2, .95f, 0), new Vector3(5, .1f, 4));
        var mover = Actor("ZombieMinion");
        var move = (Move)Delegate.CreateDelegate(typeof(Move), mover, "MoveWithWalls");
        Physics.SyncTransforms();
        Vector3 pos = Vector3.zero;
        for (int i = 0; i < 120; i++) pos = move(pos, Vector3.right * .05f, Mask);
        Check(pos.x < 1f && !mover.Overlaps(pos, Mask), label + " bypassed at " + pos);
        Debug.Log("STAIR REJECTION PASS " + label);
    }

    static void FlatCost()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(1000, 1, 10));
        var mover = Actor("ZombieMinion");
        var move = (Move)Delegate.CreateDelegate(typeof(Move), mover, "MoveWithWalls");
        Physics.SyncTransforms();
        Vector3 pos = Vector3.zero;
        for (int i = 0; i < 20; i++) pos = move(pos, Vector3.right * .01f, Mask);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) pos = move(pos, Vector3.right * .01f, Mask);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && mover.StepAttempts == 0, "Flat travel allocates or probes steps: " + allocated);
        Debug.Log("STAIR COST PASS: flat movement zero allocations, zero step attempts");
    }

    static void Box(Vector3 position, Vector3 size)
    {
        var go = new GameObject("Stair fixture"); go.layer = 6;
        go.transform.position = position; go.AddComponent<BoxCollider>().size = size;
    }

    public static void Audit()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var baker = UnityEngine.Object.FindFirstObjectByType<CrawlGraphBaker>();
        Check(baker != null && baker.graphAsset != null, "Missing main graph");
        CrawlGraph graph = CrawlGraph.Load(baker.graphAsset);
        Debug.Log($"STAIR AUDIT graph nodes={graph.NodeCount} cell={graph.CellSize}");
        foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (col.name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0
                || col.name.IndexOf("Subway", StringComparison.OrdinalIgnoreCase) >= 0)
                Debug.Log($"STAIR COLLIDER {col.name} bounds={col.bounds} layer={col.gameObject.layer}");
        var starts = new[] { new Vector3(52.684f,6.120f,-81.113f), new Vector3(96.388f,13.899f,16.81f),
            new Vector3(75.029f,4.719f,42.06f), new Vector3(96.388f,10.399f,16.81f) };
        var ends = new[] { new Vector3(52.684f,9.616f,-87.266f), new Vector3(102.431f,16.95f,16.81f),
            new Vector3(81.431f,7.95f,42.06f), new Vector3(102.431f,16.95f,16.81f) };
        var names = new[] { "Factory", "Apartment", "Police", "Apartment two floors" };
        for (int i = 0; i < starts.Length; i++) AuditPair(graph, names[i], starts[i], ends[i]);
        foreach (string entranceName in new[] { "Subway_West_Entrance", "Subway_East_Entrance" })
        {
            Bounds bounds = GameObject.Find(entranceName).GetComponent<Collider>().bounds;
            AuditPair(graph, entranceName, new Vector3(bounds.center.x - 2.4f, bounds.min.y + .29f, bounds.center.z - 1.55f),
                new Vector3(bounds.center.x - 2.4f, bounds.max.y - 3.56f, bounds.center.z + 1.55f));
        }
        // Locate the short track-to-platform flights independently of object names.
        for (float z = -4; z < 49; z += 1f)
        {
            int rises = 0; float previous = -7.6f;
            for (float x = 64; x > 59; x -= .1f)
            {
                if (!Physics.Raycast(new Vector3(x, -6.5f, z), Vector3.down, out var floor, 1.5f, Mask)) break;
                if (floor.point.y - previous > .05f && floor.point.y - previous < .3f) rises++;
                previous = floor.point.y;
            }
            if (rises >= 3) AuditPair(graph, "Subway platform z=" + z, new Vector3(64, -7.58f, z), new Vector3(59, -6.8f, z));
        }
        Debug.Log("EnemyStairChecks: AUDIT COMPLETE (read only)");
    }

    static void AuditPair(CrawlGraph graph, string label, Vector3 from, Vector3 to)
    {
        int a = graph.FindNearestGroundNode(from, 1f), b = graph.FindNearestGroundNode(to, 1f);
        foreach (bool ground in new[] { true, false })
        {
            var finder = new CrawlPathfinder(graph, 100000);
            var path = new int[8192];
            bool found = finder.FindPath(a, b, path, out int count, out bool complete, ground);
            Debug.Log($"STAIR GRAPH {label} groundOnly={ground} start={a} end={b} found={found} complete={complete} count={count}");
            if (ground && !complete)
            {
                var visited = new HashSet<int>(); var queue = new Queue<int>();
                if (a >= 0) { visited.Add(a); queue.Enqueue(a); }
                int closest = a;
                while (queue.Count > 0)
                {
                    int node = queue.Dequeue();
                    if ((graph.GetPosition(node) - to).sqrMagnitude < (graph.GetPosition(closest) - to).sqrMagnitude) closest = node;
                    for (int e = graph.EdgeStart[node]; e < graph.EdgeStart[node + 1]; e++)
                    {
                        int next = graph.EdgeTarget[e];
                        if (graph.IsGroundNode(next) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                Debug.Log($"STAIR COMPONENT {label} connected={visited.Contains(b)} visited={visited.Count} nearest={graph.GetPosition(closest)} target={to}");
            }
        }
    }

    public static void ActualMotorFlights()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var starts = new[] { new Vector3(52.684f,6.120f,-81.113f), new Vector3(96.388f,13.899f,16.81f),
            new Vector3(75.029f,4.719f,41.81f), new Vector3(64,-7.58f,22) };
        var ends = new[] { new Vector3(52.684f,9.616f,-87.266f), new Vector3(102.431f,16.95f,16.81f),
            new Vector3(81.431f,7.95f,41.81f), new Vector3(59,-6.8f,22) };
        var labels = new[] { "Factory", "Apartment", "Police", "Subway platform" };
        var ascentEnds = new Vector3[starts.Length];
        foreach (string name in new[] { "ZombieSmasher", "ZombieBomber", "ZombieMinion" })
        for (int i = 0; i < starts.Length; i++)
        for (int direction = 0; direction < 2; direction++)
        {
            var mover = Actor(name);
            var move = (Move)Delegate.CreateDelegate(typeof(Move), mover, "MoveWithWalls");
            Vector3 start = direction == 0 ? starts[i] : ascentEnds[i], goal = direction == 0 ? ends[i] : starts[i];
            Vector3 pos = start;
            if (direction == 0)
            {
                mover.GetCapsuleSize(out float radius, out float height);
                bool placed = false;
                for (float lift = 0; lift <= .301f; lift += .05f)
                {
                    Vector3 bottom = mover.SupportedCapsuleBottom(start, Mask) + Vector3.up * lift;
                    Vector3 top = bottom + Vector3.up * (height - radius * 2);
                    if (Physics.CheckCapsule(bottom, top, radius, Mask, QueryTriggerInteraction.Ignore)
                        || !Physics.CapsuleCast(bottom, top, radius, Vector3.down, out var support, .65f, Mask, QueryTriggerInteraction.Ignore)
                        || !CrawlGraph.IsWalkableGround(support.normal)) continue;
                    pos = mover.RootFromCapsuleBottom(bottom + Vector3.down * Mathf.Max(0, support.distance - GroundLocomotion.CapsuleSkin / support.normal.y));
                    placed = true; break;
                }
                Check(placed, "Actual starting clearance " + name + " " + labels[i]);
            }
            for (int tick = 0; tick < 1500; tick++)
            {
                Vector3 flat = Vector3.ProjectOnPlane(goal - pos, Vector3.up);
                if (flat.sqrMagnitude < .05f * .05f) break;
                Vector3 next = move(pos, Vector3.ClampMagnitude(flat, .05f), Mask);
                Check(!mover.Overlaps(next, Mask), "Actual motor overlap " + name + " " + labels[i] + " at " + next);
                if ((next - pos).sqrMagnitude < 1e-9f) break;
                pos = next;
            }
            if (direction == 0) ascentEnds[i] = pos;
            Debug.Log($"ACTUAL MOTOR {name} {labels[i]} direction={direction} pos={pos} goal={goal} steps={mover.StepsTaken}");
            Check(Vector3.Distance(pos, goal) < .4f, name + " actual motor failed " + labels[i] + " at " + pos);
            UnityEngine.Object.DestroyImmediate(mover.gameObject);
        }
        Debug.Log("EnemyStairChecks: ACTUAL MOTOR FLIGHTS PASSED");
    }

    // Called after an isolated test bake with the level's real colliders loaded.
    // This follows the actual graph waypoints, not a prescribed straight line.
    public static void ValidateBakedStairs(CrawlGraph graph)
    {
        Check(Application.isBatchMode, "Batch mode only");
        var routes = new List<(string label, Vector3 start, Vector3 end)> {
            // The catwalk railing blocks the exit beyond these upper steps.
            ("Factory upper steps", new Vector3(52.684f,6.120f,-81.113f), new Vector3(52.684f,9.616f,-87.266f)),
            ("Apartment", new Vector3(96.388f,13.899f,16.81f), new Vector3(102.431f,16.95f,16.81f)),
            ("Police", new Vector3(75.029f,4.719f,41.81f), new Vector3(81.431f,7.95f,41.81f)),
            ("Apartment two floors", new Vector3(96.388f,10.399f,16.81f), new Vector3(102.431f,16.95f,16.81f))
        };
        foreach (string name in new[] { "Subway_West_Entrance", "Subway_East_Entrance" })
        {
            Bounds b = GameObject.Find(name).GetComponent<Collider>().bounds;
            routes.Add((name, new Vector3(b.center.x - 2.4f, b.min.y + .29f, b.center.z - 1.55f),
                new Vector3(b.center.x - 2.4f, b.max.y - 3.56f, b.center.z + 1.55f)));
        }
        foreach (float z in new[] { -3f, 22f, 47f })
            routes.Add(("Subway platform " + z, new Vector3(64, -7.58f, z), new Vector3(59, -6.8f, z)));
        var finder = new CrawlPathfinder(graph);
        var path = new int[128]; // Same capacity as the ground motor.
        foreach (var route in routes)
        for (int reverse = 0; reverse < 2; reverse++)
        {
            Vector3 start = reverse == 0 ? route.start : route.end;
            Vector3 end = reverse == 0 ? route.end : route.start;
            int a = graph.FindNearestGroundNode(start, 1), b = graph.FindNearestGroundNode(end, 1);
            Check(a >= 0 && b >= 0, route.label + " missing endpoint");
            Check(finder.FindPath(a, b, path, out int count, out bool complete, false) && complete,
                route.label + " unrestricted/spider route regression");
            Check(finder.FindPath(a, b, path, out count, out complete, true) && complete,
                route.label + " ground route incomplete " + reverse);
            foreach (string name in new[] { "ZombieSmasher", "ZombieBomber", "ZombieMinion" })
            {
                var mover = Actor(name);
                try
                {
                    var move = (Move)Delegate.CreateDelegate(typeof(Move), mover, "MoveWithWalls");
                    // Start on the supported path endpoint, including capsule contact
                    // with the next riser, rather than inside the staircase mesh.
                    Vector3 pos = PlaceOnStair(mover, graph.GetPosition(a));
                    int next = 0;
                    for (int tick = 0; tick < 4000 && next < count; tick++)
                    {
                        Vector3 delta = graph.GetPosition(path[next]) - pos;
                        if (delta.sqrMagnitude < .3f * .3f) { next++; continue; }
                        pos = move(pos, Vector3.ClampMagnitude(delta, .04f), Mask);
                        Check(!mover.Overlaps(pos, Mask), name + " route overlap " + route.label);
                    }
                    Debug.Log($"BAKED MOTOR {route.label} {name} reverse={reverse} reached={next}/{count} pos={pos}");
                    Check(next == count, name + " failed baked waypoints " + route.label + " " + reverse);
                }
                finally { UnityEngine.Object.DestroyImmediate(mover.gameObject); }
            }
        }
        Debug.Log("EnemyStairChecks: BAKED STAIR ROUTES PASSED");
    }

    static Vector3 PlaceOnStair(GroundLocomotion mover, Vector3 point)
    {
        mover.GetCapsuleSize(out float radius, out float height);
        for (int i = 0; i <= 6; i++)
        {
            Vector3 bottom = mover.SupportedCapsuleBottom(point, Mask) + Vector3.up * (i * .05f);
            Vector3 top = bottom + Vector3.up * (height - radius * 2);
            if (!Physics.CheckCapsule(bottom, top, radius, Mask, QueryTriggerInteraction.Ignore) &&
                Physics.CapsuleCast(bottom, top, radius, Vector3.down, out var hit, .65f, Mask, QueryTriggerInteraction.Ignore) &&
                CrawlGraph.IsWalkableGround(hit.normal))
                return mover.RootFromCapsuleBottom(bottom + Vector3.down * Mathf.Max(0, hit.distance - GroundLocomotion.CapsuleSkin / hit.normal.y));
        }
        throw new Exception("No supported start pose at " + point);
    }

    // Batch entry: never overwrites the level's shipped graph or saves its scene.
    public static void BakeAndValidate()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        BakeAndValidateCurrentScene(UnityEngine.Object.FindFirstObjectByType<CrawlGraphBaker>());
    }

    public static void BakeAndValidateCurrentScene(CrawlGraphBaker source)
    {
        Check(Application.isBatchMode && source != null, "Batch mode and a scene baker required");
        const string output = "Assets/EnemyStairGraphChecks.bytes";
        Check(!System.IO.File.Exists(output), "Temporary check asset already exists; will not overwrite it");
        var go = new GameObject("Temporary stair bake check");
        var baker = go.AddComponent<CrawlGraphBaker>();
        EditorUtility.CopySerialized(source, baker);
        go.transform.position = source.transform.position;
        try
        {
            System.IO.File.WriteAllBytes(output, Array.Empty<byte>());
            AssetDatabase.ImportAsset(output);
            baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(output);
            Check(CrawlGraphBake.Bake(baker, false), "Full test bake cancelled");
            var full = CrawlGraph.Load(baker.graphAsset);
            ValidateBakedStairs(full);
            // Region sampling must repair the same connections without changing
            // cell density, format, or introducing a separate runtime graph.
            baker.rebakeRegion = new Bounds(new Vector3(61, -7, 22), new Vector3(8, 4, 8));
            Check(CrawlGraphBake.Bake(baker, true), "Region test bake cancelled");
            var region = CrawlGraph.Load(baker.graphAsset);
            Check(region.NodeCount == full.NodeCount, "Unchanged region rebake changed node count");
            for (int i = 0; i < full.NodeCount; i++)
                Check(region.NodeKey[i] == full.NodeKey[i] &&
                    (region.GetPosition(i) - full.GetPosition(i)).sqrMagnitude < 0.000001f,
                    "Unchanged region rebake moved a sample");
            ValidateBakedStairs(region);
            Debug.Log($"STAIR BAKE PASS nodes={full.NodeCount} edges={full.EdgeCount} bytes={full.MemoryBytes}; full and region routes pass");
        }
        finally
        {
            AssetDatabase.DeleteAsset(output);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    public static void RejectInvalidStairConnections()
    {
        Check(Application.isBatchMode, "Batch mode only");
        foreach (string obstacle in new[] { "valid", "tall riser", "ceiling", "gap", "thin wall" })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float rise = obstacle == "tall riser" ? .45f : .2f;
            if (obstacle == "gap")
            {
                Box(new Vector3(-2, -.5f, 0), new Vector3(4, 1, 4));
                Box(new Vector3(2.8f, (rise - 1) / 2, 0), new Vector3(4, 1 + rise, 4));
            }
            else
            {
                Box(new Vector3(0, -.5f, 0), new Vector3(8, 1, 4));
                Box(new Vector3(2.4f, rise / 2, 0), new Vector3(4, rise, 4));
            }
            if (obstacle == "ceiling") Box(new Vector3(.6f, 1f, 0), new Vector3(4, .1f, 4));
            if (obstacle == "thin wall") Box(new Vector3(.3f, 1, 0), new Vector3(.02f, 2, 4));
            Physics.SyncTransforms();
            bool result = CrawlGraphBake.StairSegment(new Vector3(-.2f, 0, 0), new Vector3(1, rise, 0), Mask);
            Check(result == (obstacle == "valid"), "Stair connection validator failed " + obstacle);
        }
        Debug.Log("EnemyStairChecks: UNSUPPORTED/BLOCKED CONNECTIONS REJECTED");
    }

    public static void ValidateSpiderBakeTransitions()
    {
        Check(Application.isBatchMode, "Batch mode only");
        const string output = "Assets/EnemySpiderBakeChecks.bytes";
        Check(!System.IO.File.Exists(output), "Temporary spider check asset already exists");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(8, 1, 8));
        Box(new Vector3(2.1f, 2, 0), new Vector3(.2f, 4, 8));
        Box(new Vector3(0, 4.1f, 0), new Vector3(8, .2f, 8));
        var go = new GameObject("Spider bake regression");
        var baker = go.AddComponent<CrawlGraphBaker>();
        baker.size = new Vector3(10, 8, 10); go.transform.position = new Vector3(0, 2, 0);
        baker.cellSize = .5f; baker.surfaceMask = Mask; baker.minIslandNodes = 1;
        try
        {
            System.IO.File.WriteAllBytes(output, Array.Empty<byte>());
            AssetDatabase.ImportAsset(output);
            baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(output);
            Check(CrawlGraphBake.Bake(baker, false), "Spider test bake cancelled");
            var graph = CrawlGraph.Load(baker.graphAsset);
            int floor = graph.FindNearestNode(Vector3.zero, 1, CrawlSurface.Floor);
            int wall = graph.FindNearestNode(new Vector3(2, 2, 0), 1, CrawlSurface.Wall);
            int ceiling = graph.FindNearestNode(new Vector3(1, 4, 0), 1, CrawlSurface.Ceiling);
            Check(floor >= 0 && wall >= 0 && ceiling >= 0, "Spider bake lost a surface");
            var finder = new CrawlPathfinder(graph); var path = new int[128];
            foreach (var pair in new[] { (floor, wall), (wall, ceiling), (ceiling, floor) })
                Check(finder.FindPath(pair.Item1, pair.Item2, path, out _, out bool complete, false) && complete,
                    "Spider floor/wall/ceiling bake transition disconnected");
            Debug.Log("EnemyStairChecks: SPIDER BAKED FLOOR/WALL/CEILING TRANSITIONS PASSED");
        }
        finally
        {
            AssetDatabase.DeleteAsset(output);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // CPU microprofile on real stair colliders and baked routes. Sixty motors,
    // thirty Smasher walking checks at their normal 0.2s cadence. This deliberately
    // excludes rendering/networking, crowd contacts and A*; it is not device FPS.
    public static void ProfileCrowdedStairs()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var graph = CrawlGraph.Load(System.IO.File.ReadAllBytes("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        var finder = new CrawlPathfinder(graph);
        Vector3[] starts = { new Vector3(96.388f,13.899f,16.81f), new Vector3(75.029f,4.719f,41.81f), new Vector3(64,-7.58f,22) };
        Vector3[] ends = { new Vector3(102.431f,16.95f,16.81f), new Vector3(81.431f,7.95f,41.81f), new Vector3(59,-6.8f,22) };
        var routes = new int[3][];
        for (int r = 0; r < 3; r++)
        {
            var buffer = new int[128];
            Check(finder.FindPath(graph.FindNearestGroundNode(starts[r], 1), graph.FindNearestGroundNode(ends[r], 1),
                buffer, out int count, out bool complete, true) && complete, "Profile route missing");
            routes[r] = new int[count]; Array.Copy(buffer, routes[r], count);
        }
        for (int pass = 0; pass < 3; pass++)
        for (int schedule = 0; schedule < 3; schedule++)
        {
            var readyMethod = typeof(ZombieSmasherJump).GetMethod("ReadyForJumpCheck", Private);
            if (schedule == 2 && readyMethod == null) continue; // Also runs against the baseline assembly.
            bool staggered = schedule == 1;
            const int actors = 60, frames = 720, warmup = 90;
            var movers = new GroundLocomotion[actors]; var moves = new Move[actors];
            var jumps = new ZombieSmasherJump[30]; var walks = new Func<Vector3, LayerMask, bool>[30];
            var ready = new Func<float, bool>[30];
            var positions = new Vector3[actors]; var indices = new int[actors]; var directions = new int[actors];
            var moveTimes = new double[frames]; var walkTimes = new double[frames]; var totalTimes = new double[frames];
            long allocated = 0; int checks = 0, walkable = 0, stepAttempts = 0;
            var startField = typeof(ZombieSmasherJump).GetField("_start", Private);
            for (int a = 0; a < actors; a++)
            {
                movers[a] = Actor(a < 30 ? "ZombieSmasher" : a < 45 ? "ZombieBomber" : "ZombieMinion");
                moves[a] = (Move)Delegate.CreateDelegate(typeof(Move), movers[a], "MoveWithWalls");
                if (a < 30)
                {
                    var enemy = movers[a].gameObject.AddComponent<Enemy>(); Call(enemy, "Awake");
                    jumps[a] = movers[a].gameObject.AddComponent<ZombieSmasherJump>(); Call(jumps[a], "Awake");
                    walks[a] = (Func<Vector3, LayerMask, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, LayerMask, bool>), jumps[a], "CanWalkTo");
                    if (readyMethod != null) ready[a] = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), jumps[a], readyMethod);
                }
                int[] route = routes[a % 3];
                indices[a] = a / 3 % (route.Length - 1); directions[a] = 1;
                positions[a] = PlaceOnStair(movers[a], graph.GetPosition(route[indices[a]]));
            }
            try
            {
                for (int frame = -warmup; frame < frames; frame++)
                {
                    double movement = 0, walking = 0;
                    for (int a = 0; a < actors; a++)
                    {
                        int[] route = routes[a % 3];
                        Vector3 delta = graph.GetPosition(route[indices[a]]) - positions[a];
                        if (delta.sqrMagnitude < .09f)
                        {
                            if (indices[a] + directions[a] < 0 || indices[a] + directions[a] >= route.Length) directions[a] *= -1;
                            indices[a] += directions[a]; delta = graph.GetPosition(route[indices[a]]) - positions[a];
                        }
                        long before = GC.GetAllocatedBytesForCurrentThread(), time = System.Diagnostics.Stopwatch.GetTimestamp();
                        positions[a] = moves[a](positions[a], Vector3.ClampMagnitude(delta, 1.8f / 90f), Mask);
                        movement += System.Diagnostics.Stopwatch.GetTimestamp() - time;
                        if (frame >= 0) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                        if (a >= 30) continue;
                        if (schedule == 2 ? !ready[a]((frame + warmup) / 90f) :
                            (frame + (staggered ? a % 18 : 0)) % 18 != 0) continue;
                        startField.SetValue(jumps[a], positions[a]); // Harness boxing is outside measured work.
                        Vector3 goal = graph.GetPosition(route[directions[a] > 0 ? route.Length - 1 : 0]);
                        before = GC.GetAllocatedBytesForCurrentThread(); time = System.Diagnostics.Stopwatch.GetTimestamp();
                        bool result = walks[a](goal, Mask);
                        walking += System.Diagnostics.Stopwatch.GetTimestamp() - time;
                        if (frame >= 0) { allocated += GC.GetAllocatedBytesForCurrentThread() - before; checks++; if (result) walkable++; }
                    }
                    if (frame < 0) continue;
                    moveTimes[frame] = movement * 1000 / System.Diagnostics.Stopwatch.Frequency;
                    walkTimes[frame] = walking * 1000 / System.Diagnostics.Stopwatch.Frequency;
                    totalTimes[frame] = moveTimes[frame] + walkTimes[frame];
                }
                foreach (var mover in movers) stepAttempts += mover.StepAttempts;
                Check(allocated == 0, "Crowded stair profile allocated " + allocated);
                Debug.Log($"CROWD PROFILE pass={pass} schedule={schedule} actors=60 smashers=30 frames=720 allocated={allocated} checks={checks} walkable={walkable} stepAttempts={stepAttempts} move[{Timing(moveTimes)}] walk[{Timing(walkTimes)}] total[{Timing(totalTimes)}]");
            }
            finally { foreach (var mover in movers) UnityEngine.Object.DestroyImmediate(mover.gameObject); }
        }
    }

    static string Timing(double[] values)
    {
        double sum = 0; foreach (double value in values) sum += value;
        Array.Sort(values);
        return $"mean={sum / values.Length:F3} p95={values[(int)(values.Length * .95)]:F3} p99={values[(int)(values.Length * .99)]:F3} max={values[values.Length - 1]:F3}ms";
    }

    public static void SmasherCheckRegressions()
    {
        Check(Application.isBatchMode, "Batch mode only");
        foreach (int fps in new[] { 30, 60, 90 })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var actors = new GameObject[30]; var ready = new Func<float, bool>[30];
            var counts = new int[30]; var last = new float[30]; int peak = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i] = Actor("ZombieSmasher").gameObject;
                var jump = actors[i].AddComponent<ZombieSmasherJump>(); Call(jump, "Awake");
                jump.ResetJump();
                ready[i] = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), jump, "ReadyForJumpCheck");
                last[i] = -1;
            }
            for (int frame = 0; frame < fps * 4; frame++)
            {
                float now = frame / (float)fps; int thisFrame = 0;
                for (int i = 0; i < actors.Length; i++)
                {
                    if (!ready[i](now)) continue;
                    Check(last[i] < 0 || now - last[i] >= .19999f, "Smasher checked faster than 0.2s");
                    Check(counts[i] != 0 || now <= .2f + 1f / fps, "Initial check delayed beyond normal cadence");
                    last[i] = now; counts[i]++; thisFrame++;
                }
                peak = Mathf.Max(peak, thisFrame);
            }
            Check(peak <= (fps == 30 ? 10 : 6), "Smasher checks bunched: " + peak);
            foreach (int count in counts) Check(count >= 16, "A Smasher was starved");
            var probe = actors[0].GetComponent<ZombieSmasherJump>(); probe.ResetJump();
            Set(probe, "_nextCheck", 10f);
            Check(!ready[0](9.9f), "Phasing bypassed cooldown");
            for (int i = 0; i <= fps; i++) ready[0](10 + i / (float)fps);
            probe.ResetJump(); Check(!ready[0](20), "Reset retained an armed check");
            foreach (var actor in actors) UnityEngine.Object.DestroyImmediate(actor);
            Debug.Log($"SMASHER PHASING PASS fps={fps} peakChecks={peak}/30 cooldown/reset/cadence preserved");
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(20, 1, 8));
        for (int i = 0; i < 8; i++) Box(new Vector3(1 + i * .32f + .16f, (i + 1) * .1f, 0), new Vector3(.32f, (i + 1) * .2f, 4));
        Box(new Vector3(6, .8f, 0), new Vector3(4.88f, 1.6f, 4));
        Box(new Vector3(3, 4f, 0), new Vector3(12, .1f, 8));
        var mover = Actor("ZombieSmasher");
        var smasher = mover.gameObject.AddComponent<ZombieSmasherJump>(); Call(smasher, "Awake");
        Call(mover.GetComponent<Enemy>(), "Awake");
        Set(smasher, "_start", Vector3.zero);
        var walk = (Func<Vector3, LayerMask, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, LayerMask, bool>), smasher, "CanWalkTo");
        Physics.SyncTransforms(); var goal = new Vector3(4.2f, 1.6f, 0);
        Check(walk(goal, Mask), "Fresh stair route should be walkable");
        var blocker = new GameObject("Dynamic obstruction"); blocker.transform.position = new Vector3(2, 1.5f, 0);
        blocker.AddComponent<BoxCollider>().size = new Vector3(.1f, 3, 4); Physics.SyncTransforms();
        Check(walk(goal, Mask), "Excluded obstacle affected planning");
        Check(!walk(goal, Mask | 1), "Same-frame mask change reused stale clearance");
        blocker.layer = 6; Physics.SyncTransforms();
        Check(!walk(goal, Mask), "Same-frame obstacle reused stale clearance");
        UnityEngine.Object.DestroyImmediate(blocker); Physics.SyncTransforms();
        Check(walk(goal, Mask), "Removed obstacle retained stale rejection");
        mover.transform.localScale *= 2; Physics.SyncTransforms();
        Check(!walk(goal, Mask), "Changed body size reused stale capsule dimensions");
        UnityEngine.Object.DestroyImmediate(mover.gameObject);
        Debug.Log("SMASHER CLEARANCE PASS fresh obstacle/mask/scale observations between calls");
    }

    public static void MovementSnapshotRegressions()
    {
        Check(Application.isBatchMode, "Batch mode only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(20, 1, 8));
        var mover = Actor("ZombieSmasher"); Physics.SyncTransforms();
        Vector3 position = mover.Displace(Vector3.zero, Vector3.right * .1f, Mask);
        mover.GetCapsuleSize(out float originalRadius, out _);
        mover.transform.localScale *= 1.5f;
        Box(new Vector3(2, 1.5f, 0), new Vector3(.02f, 3, 8)); Physics.SyncTransforms();
        mover.GetCapsuleSize(out float grownRadius, out _);
        Check(Mathf.Abs(grownRadius - originalRadius * 1.5f) < .00001f, "Movement retained previous dimensions");
        position = mover.Displace(position, Vector3.right * 4, Mask);
        Check(position.x < 2 - grownRadius && !mover.Overlaps(position, Mask), "Movement missed new wall/changed size");
        float stopped = position.x;
        mover.transform.localScale *= .5f; Physics.SyncTransforms();
        position = mover.Displace(position, Vector3.right * 2, Mask);
        Check(position.x > stopped + .25f && !mover.Overlaps(position, Mask), "Movement failed to refresh shrunken capsule");
        UnityEngine.Object.DestroyImmediate(mover.gameObject);
        Debug.Log("MOVEMENT SNAPSHOT PASS geometry refreshed between calls; new obstacle still blocks");
    }
}

