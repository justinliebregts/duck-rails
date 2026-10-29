using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using Game.AI;
using Photon.Pun;

public sealed partial class DuckCompanion
{
    enum NavigationPlan { None, Route, Home, Fence, Obstacle, Roof, Interior, Climb, FenceTakeoff, Traversal, Ascent, Obstruction, RoofExit, Recovery, Truck, Sideways }
    enum NavigationPhase { Candidates, Floor, Route, Traversal }
    IEnumerator<int> navigationPlan;
    NavigationPlan navigationKind, completedNavigationKind;
    NavigationPhase navigationPhase;
    Vector3 navigationGoal, navigationStart, completedNavigationGoal, navigationDirection;
    Vector3 navigationTargetHead;
    bool navigationHasTarget;
    Bounds navigationBounds;
    CompanionMode navigationMode;
    int navigationActor;
    bool navigationPaused, planSucceeded, completedNavigationSuccess;
    float navigationStarted;
    long navigationDeadline;
    Vector3? navigationProbeOrigin;
    float? navigationProbeHeight;
    bool navigationSidewaysProbe;
    CrawlPathfinder companionPathfinder;
    readonly Dictionary<Vector2Int, Vector3> floorPositions = new Dictionary<Vector2Int, Vector3>(8192);
    readonly Dictionary<Vector2Int, Vector2Int> floorParents = new Dictionary<Vector2Int, Vector2Int>(8192);
    readonly HashSet<Vector2Int> floorVisited = new HashSet<Vector2Int>(16384);
    readonly Queue<Vector2Int> floorQueue = new Queue<Vector2Int>(8192);
    readonly List<Vector3> planningRoute = new List<Vector3>(8192);
    readonly List<Vector2Int> planningCandidates = new List<Vector2Int>(24);
    readonly List<float> planningScores = new List<float>(24);
    readonly Vector3[] navMeshCorners = new Vector3[4096];
    readonly RaycastHit[] planningTraversalHits = new RaycastHit[32];
    Vector3[] plannedRoute = Array.Empty<Vector3>(), completedNavigationRoute = Array.Empty<Vector3>();
    bool floorBuilt, floorSucceeded;
    Vector3[] floorRoute = Array.Empty<Vector3>();
    public bool NavigationPending => navigationPlan != null;
    public int NavigationSlices { get; private set; }
    public int NavigationSteps { get; private set; }
    public int NavigationFloorNodes { get; private set; }
    public int NavigationCancellations { get; private set; }
    public double NavigationLastSliceMs { get; private set; }
    public float NavigationLastLatency { get; private set; }
    static readonly ProfilerMarker NavigationMarker = new ProfilerMarker("DuckNavigation.Plan");
    static readonly ProfilerMarker FloorPlanMarker = new ProfilerMarker("DuckNavigation.FloorSearch");
    static readonly ProfilerMarker RoutePlanMarker = new ProfilerMarker("DuckNavigation.Route");
    static readonly ProfilerMarker TraversalPlanMarker = new ProfilerMarker("DuckNavigation.Traversal");
    static readonly ProfilerMarker CandidatePlanMarker = new ProfilerMarker("DuckNavigation.Candidates");
    static readonly ProfilerMarker DuckUpdateMarker = new ProfilerMarker("DuckNavigation.Update");

    void PrepareNavigation()
    {
        var manager = EnemyManager.Instance;
        if (manager != null && manager.Graph != null && (companionPathfinder == null || companionPathfinder.Graph != manager.Graph))
            companionPathfinder = manager.CreateCompanionPathfinder();
    }

    bool RequestNavigation(NavigationPlan kind, Vector3 goal, Bounds bounds = default, Vector3 direction = default)
    {
        if (completedNavigationKind == kind && (goal - completedNavigationGoal).sqrMagnitude < 0.01f)
        {
            completedNavigationKind = NavigationPlan.None;
            return completedNavigationSuccess;
        }
        if (navigationPlan != null || IsDead) return false;
        BeginNavigation(kind, goal, bounds, direction);
#if UNITY_EDITOR
        // Geometry fixtures outside Play mode retain their synchronous interface.
        if (!Application.isPlaying)
        {
            while (navigationPlan != null) AdvanceNavigation(long.MaxValue);
            completedNavigationKind = NavigationPlan.None;
            return completedNavigationSuccess;
        }
#endif
        return false;
    }

    void BeginNavigation(NavigationPlan kind, Vector3 goal, Bounds bounds, Vector3 direction)
    {
        navigationKind = kind; navigationGoal = goal; navigationBounds = bounds; navigationDirection = direction;
        navigationStart = transform.position; navigationMode = Mode; navigationActor = TargetActor; navigationPaused = followingPaused;
        navigationProbeOrigin = traversalProbeOrigin; navigationProbeHeight = traversalProbeMinimumHeight;
        navigationSidewaysProbe = sidewaysProbe;
        var target = TargetActor > 0 ? PhotonPlayer.ForActor(TargetActor) : null;
        navigationHasTarget = target != null && target.TryGetHeadPosition(out navigationTargetHead);
        navigationStarted = Time.time;
        NavigationSlices = NavigationSteps = NavigationFloorNodes = 0;
        completedNavigationKind = NavigationPlan.None;
        planSucceeded = floorBuilt = false;
        plannedRoute = floorRoute = Array.Empty<Vector3>();
        navigationPlan = RunNavigation().GetEnumerator();
        NavigationWorkBudget.RegisterDuck();
    }

    void CancelNavigation()
    {
        if (navigationPlan != null)
        {
            navigationPlan.Dispose(); navigationPlan = null;
            NavigationWorkBudget.UnregisterDuck(); NavigationCancellations++;
        }
        companionPathfinder?.Cancel();
        completedNavigationKind = NavigationPlan.None;
        navigationProbeOrigin = null; navigationProbeHeight = null;
    }

    void TickNavigation()
    {
        if (navigationPlan == null) return;
        var target = navigationActor > 0 ? PhotonPlayer.ForActor(navigationActor) : null;
        bool targetChanged = navigationHasTarget && (target == null || target.IsDead
            || !target.TryGetHeadPosition(out Vector3 currentHead) || (currentHead - navigationTargetHead).sqrMagnitude > 9f);
        if (!HasAuthority || IsDead || navigationActor != TargetActor || navigationMode != Mode
            || targetChanged || navigationPaused != followingPaused || State == Activity.Climbing || State == Activity.Jumping
            || (transform.position - navigationStart).sqrMagnitude > 9f || Time.time - navigationStarted > 8f)
        { CancelNavigation(); return; }
        var manager = EnemyManager.Instance;
        if (!NavigationWorkBudget.TryBegin(manager != null ? manager.PathBudgetMilliseconds : 1f,
            manager != null && Enemy.Active.Count > 0, out long started, out long deadline, true)) return;
        try { AdvanceNavigation(deadline); }
        finally
        {
            NavigationWorkBudget.End(started, true);
            NavigationLastSliceMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        }
    }

    void AdvanceNavigation(long deadline)
    {
        using var sample = NavigationMarker.Auto();
        navigationDeadline = deadline;
        NavigationSlices++;
        while (navigationPlan != null && System.Diagnostics.Stopwatch.GetTimestamp() < deadline)
        {
            Vector3? priorOrigin = traversalProbeOrigin;
            float? priorHeight = traversalProbeMinimumHeight;
            bool priorSideways = sidewaysProbe;
            bool pending;
            try
            {
                traversalProbeOrigin = navigationProbeOrigin; traversalProbeMinimumHeight = navigationProbeHeight;
                sidewaysProbe = navigationSidewaysProbe;
                var marker = navigationPhase == NavigationPhase.Floor ? FloorPlanMarker : navigationPhase == NavigationPhase.Route
                    ? RoutePlanMarker : navigationPhase == NavigationPhase.Traversal ? TraversalPlanMarker : CandidatePlanMarker;
                using (marker.Auto()) pending = navigationPlan.MoveNext();
                navigationProbeOrigin = traversalProbeOrigin; navigationProbeHeight = traversalProbeMinimumHeight;
                navigationSidewaysProbe = sidewaysProbe;
                NavigationSteps++;
            }
            finally { traversalProbeOrigin = priorOrigin; traversalProbeMinimumHeight = priorHeight; sidewaysProbe = priorSideways; }
            if (pending) continue;
            navigationPlan.Dispose(); navigationPlan = null; NavigationWorkBudget.UnregisterDuck();
            completedNavigationKind = navigationKind; completedNavigationGoal = navigationGoal;
            completedNavigationSuccess = planSucceeded; completedNavigationRoute = plannedRoute;
            NavigationLastLatency = Time.time - navigationStarted;
            if (planSucceeded && navigationKind == NavigationPlan.Route && plannedRoute.Length > 0
                && ClearBodySweep(transform.position, plannedRoute[Mathf.Min(1, plannedRoute.Length - 1)]))
            { corners = plannedRoute; corner = Mathf.Min(1, corners.Length - 1); }
            if (planSucceeded && navigationKind == NavigationPlan.Home)
            { homeRoute = plannedRoute; homeDestination = navigationGoal; homeCorner = 0; homeWalking = true; }
        }
    }

    IEnumerable<int> RunNavigation()
    {
        switch (navigationKind)
        {
            case NavigationPlan.Truck: foreach (var step in PlanTruck(navigationGoal)) yield return step; break;
            case NavigationPlan.Sideways: foreach (var step in PlanSideways(navigationDirection, navigationGoal)) yield return step; break;
            case NavigationPlan.Route: foreach (var step in PlanRoute(navigationGoal, true)) yield return step; break;
            case NavigationPlan.Home: foreach (var step in PlanHome(navigationGoal)) yield return step; break;
            case NavigationPlan.Fence: foreach (var step in PlanFence(navigationGoal)) yield return step; break;
            case NavigationPlan.Obstacle: foreach (var step in PlanObstacle(navigationGoal)) yield return step; break;
            case NavigationPlan.Roof: foreach (var step in PlanRoof(navigationGoal, navigationBounds)) yield return step; break;
            case NavigationPlan.Interior: foreach (var step in PlanInterior(navigationGoal, navigationBounds)) yield return step; break;
            case NavigationPlan.Climb: foreach (var step in PlanClimb(navigationGoal)) yield return step; break;
            case NavigationPlan.FenceTakeoff: foreach (var step in PlanFenceTakeoff(navigationGoal)) yield return step; break;
            case NavigationPlan.Traversal: foreach (var step in PlanTraversal(navigationDirection, navigationGoal)) yield return step; break;
            case NavigationPlan.Ascent:
                foreach (var step in PlanTraversal(navigationDirection, navigationGoal)) yield return step;
                if (!planSucceeded) foreach (var step in PlanClimb(navigationGoal)) yield return step;
                break;
            case NavigationPlan.RoofExit:
                foreach (var step in PlanRoof(navigationGoal, navigationBounds)) yield return step;
                if (!planSucceeded) { floorBuilt = false; foreach (var step in PlanInterior(navigationGoal, navigationBounds)) yield return step; }
                if (!planSucceeded && TryGroundSupport(transform.position, out _) && TryDropDirection(transform.forward, out dropDirection))
                { levelRoute = new[] { dropLanding }; levelCorner = 0; levelTarget = navigationGoal; levelRouteUntil = Time.time + 5; }
                break;
            case NavigationPlan.Obstruction:
                if (navigationGoal.y > transform.position.y + motor.stepOffset)
                {
                    foreach (var step in PlanTraversal(navigationDirection, navigationGoal)) yield return step;
                    if (!planSucceeded) foreach (var step in PlanObstacle(navigationGoal)) yield return step;
                }
                else
                {
                    foreach (var step in PlanObstacle(navigationGoal)) yield return step;
                    if (!planSucceeded) foreach (var step in PlanTraversal(navigationDirection, navigationGoal)) yield return step;
                }
                if (!planSucceeded) planSucceeded = TryLocalDetour(navigationDirection);
                if (!planSucceeded && TryDropDirection(-navigationDirection, out dropDirection))
                { localDetourUntil = 0; levelRoute = Array.Empty<Vector3>(); nextPath = 0; stuck = 0; }
                break;
            case NavigationPlan.Recovery:
                while (trail.Count > 0)
                {
                    Vector3 waypoint = trail.Dequeue();
                    yield return 0;
                    if (Vector3.Distance(waypoint, transform.position) < 2f) continue;
                    foreach (var step in PlanRoute(waypoint, true)) yield return step;
                    if (!planSucceeded) continue;
                    corners = plannedRoute; corner = 1; nextPath = Time.time + 4; break;
                }
                break;
        }
    }

    IEnumerable<int> PlanRoute(Vector3 destination, bool requireComplete)
    {
        planSucceeded = false; plannedRoute = Array.Empty<Vector3>(); navigationPhase = NavigationPhase.Route;
        yield return 0;
        // This native call is indivisible; its full duration is charged to the shared budget.
        if (CompletePath(destination))
        {
            int count = path.GetCornersNonAlloc(navMeshCorners);
            if (count == navMeshCorners.Length) yield break; // Never publish a truncated path.
            plannedRoute = new Vector3[count];
            Array.Copy(navMeshCorners, plannedRoute, count); planSucceeded = true; yield break;
        }
        yield return 0;
        var manager = EnemyManager.Instance;
        if (manager == null || manager.Graph == null) yield break;
        if (companionPathfinder == null || companionPathfinder.Graph != manager.Graph)
            companionPathfinder = manager.CreateCompanionPathfinder();
        var graph = manager.Graph;
        int start = graph.FindNearestGroundNode(transform.position, 2f);
        yield return 0;
        int end = graph.FindNearestGroundNode(destination, 1.5f);
        companionPathfinder.BeginSearch(start, end, true);
        while (companionPathfinder.IsSearching)
        { companionPathfinder.Step(navigationDeadline, 32); yield return 0; }
        if (!companionPathfinder.CopyResult(groundRoute, out int length, out bool complete) || requireComplete && !complete) yield break;
        plannedRoute = new Vector3[length];
        for (int i = 0; i < length; i++)
        {
            yield return 0;
            if (!graph.IsGroundNode(groundRoute[i])) { plannedRoute = Array.Empty<Vector3>(); yield break; }
            plannedRoute[i] = graph.GetPosition(groundRoute[i]) + Vector3.up * 0.04f;
        }
        planSucceeded = true;
    }

    IEnumerable<int> PlanFloor(Vector3 goal, Bounds building, int budget = 1024, float spacing = 0.6f, bool homeOnly = false)
    {
        navigationPhase = NavigationPhase.Candidates;

        // Baked nodes can overlap shop furniture. For an interior exit, validate
        // a small local floor grid against the actual body, walls and counter.
        floorSucceeded = false; floorRoute = Array.Empty<Vector3>();
        var positions = floorPositions; var parents = floorParents;
        navigationPhase = NavigationPhase.Floor;
        Vector3 origin = transform.position;
        Vector2Int start = Vector2Int.zero;
        if (!floorBuilt)
        {
            // Build once per exit search and reuse for every candidate exit.
            positions.Clear(); parents.Clear(); floorVisited.Clear(); floorQueue.Clear();

            var visited = floorVisited;
            var queue = floorQueue;
            positions[start] = origin; visited.Add(start); queue.Enqueue(start);
            building.Expand(4f);
            int examined = 0;
            while (queue.Count > 0 && examined++ < budget)
            {
                yield return 0;
                NavigationFloorNodes++;
                Vector2Int current = queue.Dequeue();
                Vector3 from = positions[current];
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                {
                    yield return 0;
                    if (x == 0 && z == 0) continue;
                    Vector2Int next = current + new Vector2Int(x, z);
                    if (visited.Contains(next)) continue;
                    Vector3 sample = new Vector3(origin.x + next.x * spacing, from.y, origin.z + next.y * spacing);
                    if (sample.x < building.min.x || sample.x > building.max.x || sample.z < building.min.z || sample.z > building.max.z
                        || !Physics.Raycast(sample + Vector3.up * 0.35f, Vector3.down, out RaycastHit floor, 0.8f, environment, QueryTriggerInteraction.Ignore)
                        || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) { visited.Add(next); continue; }
                    Vector3 point = floor.point + Vector3.up * 0.04f;
                    if (homeOnly && (Mathf.Abs(point.y - homeFloorY) > 0.5f
                        || (point - HomeFloor).sqrMagnitude > HomeRadius * HomeRadius))
                    { visited.Add(next); continue; }
                    if (!ClearBody(point)) { visited.Add(next); continue; }
                    // Avoid repeating the failed downward crossing, but keep the
                    // upper floor available for walking along to another exit.
                    if (AvoidingLedge && point.y < from.y - 0.1f
                        && Mathf.Abs(from.y - avoidedLedge.y) < 0.5f
                        && Vector3.ProjectOnPlane(from - avoidedLedge, Vector3.up).sqrMagnitude < 4f) continue;
                    if (!ClearBodySweep(from, point)) continue;
                    visited.Add(next); positions[next] = point; parents[next] = current; queue.Enqueue(next);
                }
            }
        }
        floorBuilt = true;
        foreach (var entry in positions)
        {
            yield return 0;
            if ((entry.Value - goal).sqrMagnitude >= 1f || !ClearBodySweep(entry.Value, goal)) continue;
            if (homeOnly && !TryHomeFloor(goal, out _)) continue;
            var result = planningRoute; result.Clear(); result.Add(goal);
            Vector2Int current = entry.Key;
            while (current != start) { yield return 0; result.Add(positions[current]); current = parents[current]; }
            result.Reverse(); floorRoute = result.ToArray(); floorSucceeded = true; yield break;
        }
        yield break;

    }

    IEnumerable<int> PlanHome(Vector3 goal)
    {
        navigationPhase = NavigationPhase.Candidates;

        foreach (var step in PlanFloor(goal, new Bounds(HomeFloor, Vector3.one * 16f), 512, 0.6f, true)) yield return step;
        Vector3[] route = floorRoute;
        if (floorSucceeded)
        {
            Vector3 previous = transform.position;
            bool supported = true;
            foreach (Vector3 waypoint in route)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, waypoint) / 0.25f));
                for (int i = 1; i <= steps; i++)
                {
                    yield return 0;
                    if (!TryHomeFloor(Vector3.Lerp(previous, waypoint, (float)i / steps), out _))
                    { supported = false; break; }
                }
                if (!supported) break;
                previous = waypoint;
            }
            if (supported) { plannedRoute = route; planSucceeded = true; yield break; }
        }
        route = System.Array.Empty<Vector3>();
        HomeWanderStatus = "no bounded alternate route; retrying in five seconds";
        { planSucceeded = false; yield break; }

    }

    IEnumerable<int> PlanFence(Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        foreach (var step in PlanRoute(destination, true)) yield return step;
        Vector3[] route = plannedRoute;
        bool complete = planSucceeded;
        if (complete)
        {
            Vector3 previous = transform.position;
            foreach (Vector3 point in route)
            {
                yield return 0;
                if (!ClearBody(point) || !ClearBodySweep(previous, point)) { complete = false; break; }
                previous = point;
            }
        }
        if (!complete)
        {
            // A longer bounded ground search is only used after the crossing
            // budget is exhausted. Never blacklist the combined fence collider.
            foreach (var step in PlanFloor(destination, new Bounds(transform.position, new Vector3(120, 20, 120)), 8192, 1.2f)) yield return step;
            complete = floorSucceeded; route = floorRoute;
        }
        if (!complete || route.Length == 0) { planSucceeded = false; yield break; }
        fenceDetourRoute = route;
        fenceDetourCorner = 0;
        LastEvent = "committed to a longer ground route around the fence";
        { planSucceeded = true; yield break; }

    }

    IEnumerable<int> PlanFenceTakeoff(Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        Vector3 origin = transform.position;
        Vector3 toward = Vector3.ProjectOnPlane(destination - origin, Vector3.up).normalized;
        if (toward.sqrMagnitude < 0.5f
            || !Physics.Raycast(origin + Vector3.up * eyeHeight, toward, out RaycastHit fence,
                1.6f, environment, QueryTriggerInteraction.Ignore)
            || fence.collider.name.IndexOf("fence", System.StringComparison.OrdinalIgnoreCase) < 0)
            { planSucceeded = false; yield break; }
        RegisterFenceAttempt(fence, destination);
        if (PhotonNetwork.Time < failedFenceUntil
            && Vector3.ProjectOnPlane(fence.point - failedFenceCrossing, Vector3.up).sqrMagnitude < 16f)
            { planSucceeded = false; yield break; }

        Vector3? previousProbe = traversalProbeOrigin;
        try
        {
            // A bounded search, only when obstructed and on the existing route
            // search cooldown. Ground-check the retreat as well as the jump.
            for (int angle = 0; angle < 3; angle++)
            for (int back = 0; back < 4; back++)
            {
                yield return 0;
                Vector3 retreat = Quaternion.AngleAxis(angle == 0 ? 0 : angle == 1 ? 30 : -30, Vector3.up) * -toward;
                Vector3 candidate = origin + retreat * (0.8f + back * 0.6f);
                Vector3 previous = origin;
                bool supported = true;
                int samples = Mathf.CeilToInt(Vector3.Distance(origin, candidate) / 0.25f);
                for (int step = 1; step <= samples; step++)
                {
                    yield return 0;
                    Vector3 sample = Vector3.Lerp(origin, candidate, step / (float)samples);
                    sample.y = previous.y;
                    if (!Physics.Raycast(sample + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor,
                        1f, environment, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.7f)
                    { supported = false; break; }
                    Vector3 next = floor.point + Vector3.up * 0.04f;
                    if (!ClearBody(next) || !ClearBodySweep(previous, next))
                    { supported = false; break; }
                    previous = next;
                }
                if (!supported) continue;
                Vector3 launch = previous;
                for (int beyond = 0; beyond < 4; beyond++)
                {
                    // Put the rail near the apex rather than accepting a landing
                    // that makes Duck descend while his capsule is still crossing.
                    yield return 0;
                    float balancedDistance = Vector3.Dot(fence.point - launch, toward);
                    float adjustment = beyond == 0 ? 0 : beyond == 1 ? 0.4f : beyond == 2 ? -0.4f : 0.8f;
                    Vector3 sample = fence.point + toward * Mathf.Clamp(balancedDistance + adjustment, 0.8f, 3.8f);
                    sample.y = origin.y + 1f;
                    if (!Physics.Raycast(sample, Vector3.down, out RaycastHit floor, 3f,
                        environment, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.7f) continue;
                    Vector3 landing = floor.point + Vector3.up * 0.04f;
                    if (ClearBodySweep(launch, landing)) continue; // Must cross the barrier.
                    traversalProbeOrigin = launch;
                    if (!BeginTraversal(landing, floor.transform, false)) continue;
                    obstacleRoute = new[] { launch };
                    obstacleCorner = 0;
                    obstacleTarget = destination;
                    obstacleRouteUntil = Time.time + 12f;
                    fenceLanding = landing;
                    fenceLandingSurface = floor.transform;
                    fenceTakeoffPending = true;
                    localDetourUntil = 0;
                    LastEvent = "backing away for a checked fence jump";
                    { planSucceeded = true; yield break; }
                }
            }
        }
        finally { traversalProbeOrigin = previousProbe; }
        { planSucceeded = false; yield break; }

    }

    IEnumerable<int> PlanObstacle(Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        nextObstacleSearch = Time.time + 3f;
        fenceTakeoffPending = false;
        foreach (var step in PlanFenceTakeoff(destination)) yield return step;
        if (planSucceeded) yield break;
        var positions = floorPositions;
        var parents = floorParents;
        Vector3 origin = transform.position;
        Bounds region = new Bounds(origin, new Vector3(40f, 10f, 40f));
        foreach (var step in PlanFloor(destination, region, 2048, 0.8f)) yield return step;
        // A train or overhang can block the lift while leaving a reachable
        // launch point along the platform. Follow its floor route even when
        // that first takes us farther from the player.
        if (destination.y > origin.y + motor.stepOffset + 0.05f)
        { foreach (var step in PlanObstacleTraversal(destination)) yield return step; if (planSucceeded) yield break; }
        Vector2Int bestKey = Vector2Int.zero;
        float best = float.PositiveInfinity;
        float directDistance = Vector3.Distance(origin, destination);
        foreach (var entry in positions)
        {
            yield return 0;
            float traveled = Vector3.Distance(origin, entry.Value);
            if (traveled < 2f) continue;
            float remaining = Vector3.Distance(entry.Value, destination);
            bool visible = ClearBodySweep(entry.Value, destination);
            // Prefer a corner with a clear onward route, even if reaching it
            // initially takes Duck farther from his buddy.
            if (!visible && remaining >= directDistance - 2f && !AvoidingLedge) continue;
            if (AvoidingLedge && Vector3.ProjectOnPlane(entry.Value - avoidedLedge, Vector3.up).sqrMagnitude < 6.25f) continue;
            float score = remaining + traveled * 0.35f + (visible ? 0f : 1000f);
            if (score >= best) continue;
            best = score;
            bestKey = entry.Key;
        }
        if (float.IsPositiveInfinity(best)) { planSucceeded = false; yield break; }
        var result = planningRoute; result.Clear();
        for (Vector2Int key = bestKey; key != Vector2Int.zero; key = parents[key]) { yield return 0; result.Add(positions[key]); }
        result.Reverse();
        obstacleRoute = result.ToArray();
        obstacleCorner = 0;
        obstacleTarget = destination;
        obstacleRouteUntil = Time.time + 12f;
        localDetourUntil = 0;
        LastEvent = "taking a checked route around the obstacle";
        { planSucceeded = true; yield break; }

    }

    IEnumerable<int> PlanObstacleTraversal(Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        Vector3 origin = transform.position;
        var positions = floorPositions; var parents = floorParents;
        var candidates = planningCandidates; candidates.Clear(); planningScores.Clear();
        foreach (var entry in positions)
        {
            yield return 0;
            if (entry.Key == Vector2Int.zero || (entry.Value - origin).sqrMagnitude < 0.16f) continue;
            float score = Vector3.Distance(origin, entry.Value) + Vector3.Distance(entry.Value, destination) * 0.1f;
            int at = 0; while (at < planningScores.Count && planningScores[at] <= score) at++;
            if (at >= 24) continue;
            if (candidates.Count == 24) { candidates.RemoveAt(23); planningScores.RemoveAt(23); }
            candidates.Insert(at, entry.Key); planningScores.Insert(at, score);
        }
        // Bound expensive traversal probes; the ordinary route/recovery
        // fallbacks still run if none of these launch points are usable.
        Vector3? previousOrigin = traversalProbeOrigin;
        float? previousHeight = traversalProbeMinimumHeight;
        try
        {
            for (int i = 0; i < Mathf.Min(24, candidates.Count); i++)
            {
                Vector2Int key = candidates[i];
                Vector3 launch = positions[key];
                traversalProbeOrigin = launch;
                traversalProbeMinimumHeight = launch.y + motor.stepOffset + 0.05f;
                Vector3 direction = Vector3.ProjectOnPlane(destination - launch, Vector3.up).normalized;
                foreach (var step in PlanTraversal(direction, destination)) yield return step;
                if (!planSucceeded) continue;
                var route = planningRoute; route.Clear();
                for (Vector2Int step = key; step != Vector2Int.zero; step = parents[step])
                { yield return 0; route.Add(positions[step]); }
                route.Reverse();
                obstacleRoute = route.ToArray();
                obstacleCorner = 0;
                obstacleTarget = destination;
                obstacleRouteUntil = Time.time + 12f;
                localDetourUntil = 0;
                LastEvent = "taking a checked route to a clear climbing or jumping approach";
                { planSucceeded = true; yield break; }
            }
        }
        finally
        {
            traversalProbeOrigin = previousOrigin;
            traversalProbeMinimumHeight = previousHeight;
        }
        { planSucceeded = false; yield break; }

    }

    IEnumerable<int> PlanRoof(Vector3 destination, Bounds roof)
    {
        navigationPhase = NavigationPhase.Candidates;

        // Leave the ceiling first, using ground navigation through a doorway
        // when needed, then climb from a checked exterior approach.
        // Prefer any clear exit over an earlier candidate needing a detour.
        for (int pass = 0; pass < 2; pass++)
        for (int side = 0; side < 4; side++)
        for (int sample = 0; sample < 5; sample++)
        {
            yield return 0;
            float t = (sample + 0.5f) / 5f;
            Vector3 point = side < 2
                ? new Vector3(side == 0 ? roof.min.x - 0.45f : roof.max.x + 0.45f, transform.position.y, Mathf.Lerp(roof.min.z, roof.max.z, t))
                : new Vector3(Mathf.Lerp(roof.min.x, roof.max.x, t), transform.position.y, side == 2 ? roof.min.z - 0.45f : roof.max.z + 0.45f);
            if ((point - transform.position).sqrMagnitude > 1600f
                || !Physics.Raycast(point + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor, Mathf.Infinity, environment, QueryTriggerInteraction.Ignore)
                || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) continue;
            point = floor.point + Vector3.up * 0.04f;
            if (!ClearBody(point) || !ClearBodySweep(point, new Vector3(point.x, destination.y + 0.1f, point.z))) continue;
            Vector3[] route;
            if (ClearBodySweep(transform.position, point)) route = new[] { point };
            else
            {
                if (pass == 0) continue;
                foreach (var step in PlanFloor(point, roof)) yield return step;
                if (!floorSucceeded) continue;
                route = floorRoute;
            }
            bool canClimb;
            traversalProbeOrigin = point;
            traversalProbeMinimumHeight = destination.y - 0.25f;
            try { foreach (var step in PlanTraversal(Vector3.ProjectOnPlane(destination - point, Vector3.up).normalized, destination)) yield return step; canClimb = planSucceeded; }
            finally { traversalProbeOrigin = null; traversalProbeMinimumHeight = null; }
            if (!canClimb) continue;
            levelRoute = route;
            levelCorner = 0;
            levelTarget = destination;
            levelRouteUntil = Time.time + 20f;
            localDetourUntil = 0;
            LastEvent = "finding a way outside to your roof";
            { planSucceeded = true; yield break; }
        }
        { planSucceeded = false; yield break; }

    }

    IEnumerable<int> PlanInterior(Vector3 destination, Bounds building)
    {
        navigationPhase = NavigationPhase.Candidates;

        var positions = floorPositions;

        Vector3[] route;
        // Reuse the collision-checked floor search for ordinary exits, not only
        // roof pursuits. Its path can initially lead away from the player.
        foreach (var step in PlanFloor(destination, building)) yield return step;
        bool found = floorSucceeded; route = floorRoute;
        if (!found)
        {
            Vector3 exit = default;
            float best = float.PositiveInfinity;
            foreach (var entry in positions)
            {
                yield return 0;
                Vector3 point = entry.Value;
                if ((point - transform.position).sqrMagnitude < 1f
                    || Physics.Raycast(point + Vector3.up * eyeHeight, Vector3.up, 8f,
                        environment, QueryTriggerInteraction.Ignore)) continue;
                float score = Vector3.Distance(point, destination) + Vector3.Distance(transform.position, point) * 0.25f;
                if (score >= best) continue;
                best = score; exit = point;
            }
            if (!float.IsPositiveInfinity(best))
            { foreach (var step in PlanFloor(exit, building)) yield return step; route = floorRoute; }
            if (float.IsPositiveInfinity(best) || !floorSucceeded)
            {
                // A counter or table may be disconnected from the walkable floor.
                // Find a body-clear step/drop before searching for the door again.
                if (!TryDropDirection(destination - transform.position, out _)) { planSucceeded = false; yield break; }
                route = new[] { dropLanding };
            }
        }
        if (route.Length == 0) { planSucceeded = false; yield break; }
        levelRoute = route;
        levelCorner = 0;
        levelTarget = destination;
        levelRouteUntil = Time.time + 20f;
        followingInteriorExit = true;
        localDetourUntil = dropUntil = 0;
        stuck = 0;
        LastEvent = "finding a doorway out of the building";
        { planSucceeded = true; yield break; }

    }

    IEnumerable<int> PlanClimb(Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        // Find a supported approach with an actually reachable climb, rather than
        // walking under the target floor or repeatedly circling a doorway pillar.
        Vector3 origin = transform.position;
        float rise = destination.y - origin.y;
        Vector3 toward = Vector3.ProjectOnPlane(destination - origin, Vector3.up).normalized;
        if (toward.sqrMagnitude < 0.01f) toward = transform.forward;
        for (float distance = 0.6f; distance <= 4.2f; distance += 0.6f)
        for (int i = 0; i < 8; i++)
        {
            yield return 0;
            float angle = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2) * 45f;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * toward;
            Vector3 sample = origin + direction * distance;
            if (!Physics.Raycast(sample + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor,
                0.7f, environment, QueryTriggerInteraction.Ignore) || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) continue;
            Vector3 candidate = floor.point + Vector3.up * 0.04f;
            if (!ClearBody(candidate) || !ClearBodySweep(origin, candidate)
                || !ClearBodySweep(candidate, candidate + Vector3.up * Mathf.Min(rise, 2f))) continue;
            bool reachable;
            traversalProbeOrigin = candidate;
            try { foreach (var step in PlanTraversal(Vector3.ProjectOnPlane(destination - candidate, Vector3.up).normalized, destination)) yield return step; reachable = planSucceeded; }
            finally { traversalProbeOrigin = null; }
            if (!reachable) continue;
            localDetour = candidate;
            localDetourUntil = PhotonNetwork.Time + distance / speed + 1;
            LastEvent = "finding an edge to climb";
            { planSucceeded = true; yield break; }
        }
        { planSucceeded = false; yield break; }

    }

    IEnumerable<int> PlanTraversal(Vector3 direction, Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        // The direct line to a friend can intersect a window frame or doorway
        // pillar. Search adjacent approaches without relaxing body clearance.
        // Include sideways crossings when the player is far along a platform.
        for (int i = 0; i < 7; i++)
        {
            float angle = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2) * 30f;
            foreach (var step in PlanTraversalDirection(Quaternion.AngleAxis(angle, Vector3.up) * direction, destination)) yield return step;
            if (planSucceeded) yield break;
        }
        yield return 0;
        if (!traversalProbeOrigin.HasValue)
        { foreach (var step in PlanSideways(direction, destination)) yield return step; }
        else planSucceeded = false;

    }

    IEnumerable<int> PlanTraversalDirection(Vector3 direction, Vector3 destination)
    {
        navigationPhase = NavigationPhase.Candidates;

        navigationPhase = NavigationPhase.Traversal;
        Vector3 origin = traversalProbeOrigin ?? transform.position;
        foreach (DuckTraversalLink link in DuckTraversalLink.Active)
        {
            if (link == null || link.Landing == null || Vector3.Distance(transform.position, link.transform.position) > 1.5f) continue;
            if (Vector3.Distance(link.Landing.position, destination) >= Vector3.Distance(transform.position, destination)) continue;
            if (BeginTraversal(link.Landing.position, link.Landing, link.Climb)) { planSucceeded = true; yield break; }
        }
        // Search below the current support as well as above it. The ledge guard still stops
        // ordinary walking; only a clear, reachable landing permits leaving a roof/bus.
        // Follow the player's elevation and the nearby wall's bounds, without a height cap.
        float probeLift = Mathf.Max(2.8f, destination.y - origin.y + 0.5f);
        float localProbeLift = probeLift;
        if (Physics.Raycast(origin + Vector3.up * eyeHeight, direction, out RaycastHit wall,
            1.6f, environment, QueryTriggerInteraction.Ignore))
        {
            if (!traversalProbeOrigin.HasValue && IsFence(wall.collider)) RegisterFenceAttempt(wall, destination);
            probeLift = Mathf.Max(probeLift, wall.collider.bounds.max.y - origin.y + 0.5f);
        }
        // A single downward hit sees the bus roof and hides its steps, floor and
        // window sills. Test the surfaces below it too, preferring the player's
        // floor. Body sweeps still reject solid walls, glass and small openings.
        // One MeshCollider can contain the platform, ceiling and streets above.
        // A ray starting above its bounds only returns its first surface; also
        // cast from inside the station so the ceiling cannot hide the platform.
        for (int probePass = 0; probePass < (probeLift > localProbeLift + 0.01f ? 2 : 1); probePass++)
        for (int pass = 0; pass < 2; pass++)
        for (float distance = 0.4f; distance <= 4.3f; distance += 0.35f)
        {
            yield return 0;
            Vector3 top = origin + direction * distance + Vector3.up * (probePass == 0 ? localProbeLift : probeLift);
            int count = Physics.RaycastNonAlloc(top, Vector3.down, planningTraversalHits, Mathf.Infinity, environment, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                yield return 0;
                RaycastHit hit = planningTraversalHits[i];
                if (hit.normal.y < 0.7f) continue;
                Vector3 landing = hit.point + Vector3.up * 0.04f;
                bool nearTargetFloor = landing.y <= destination.y + 0.4f;
                if (nearTargetFloor != (pass == 0)) continue;
                float height = landing.y - origin.y;
                if (height < -0.3f && destination.y >= origin.y - 0.3f) continue;
                if (destination.y > origin.y + motor.stepOffset && height < 0.15f) continue;
                // Do not spend a traversal on the same floor before the obstacle.
                if (Mathf.Abs(height) < 0.15f && distance < 0.8f) continue;
                if (distance < 1.6f && height > 0.35f && BeginTraversal(landing, hit.transform, true)) { planSucceeded = true; yield break; }
                // Do not leap onto a roof when the buddy is inside or below it.
                float normalJumpHeight = Mathf.Pow(Mathf.Min(8.66f, jumpSpeed), 2f) / (2f * gravity);
                if (height > normalJumpHeight && !nearTargetFloor) continue;
                if (BeginTraversal(landing, hit.transform, false)) { planSucceeded = true; yield break; }
            }
        }
        { planSucceeded = false; yield break; }

    }
}
