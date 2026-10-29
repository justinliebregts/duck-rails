using System.Collections.Generic;
using UnityEngine;

public sealed partial class DuckCompanion
{
    Vector3[] interiorStairRoute = System.Array.Empty<Vector3>();
    int interiorStairCorner;
    Vector3 interiorStairGoal;
    float nextInteriorStairSearch, interiorStairBlocked;
    struct StairNode { public Vector3 point; public int parent; public float cost; }
    readonly List<StairNode> stairNodes = new List<StairNode>(32769);
    readonly Dictionary<Vector3Int, int> stairVisited = new Dictionary<Vector3Int, int>(32769);
    readonly StairFrontier stairOpen = new StairFrontier();
    readonly List<Vector3> stairResult = new List<Vector3>(4096);
    int stairTrace = -1;
    static readonly Unity.Profiling.ProfilerMarker StairPlanMarker = new Unity.Profiling.ProfilerMarker("DuckNavigation.Stairs");

    // Each discovered node is inserted once; priority/index ordering matches the
    // old SortedSet without allocating a tree node for every insertion.
    sealed class StairFrontier
    {
        readonly (float priority, int index)[] entries = new (float, int)[32769];
        public int Count { get; private set; }
        public (float priority, int index) Min => entries[0];
        public void Clear() => Count = 0;
        static int Compare((float priority, int index) a, (float priority, int index) b)
        { int c = a.priority.CompareTo(b.priority); return c != 0 ? c : a.index.CompareTo(b.index); }
        public void Add((float priority, int index) value)
        {
            int at = Count++;
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (Compare(entries[parent], value) <= 0) break;
                entries[at] = entries[parent]; at = parent;
            }
            entries[at] = value;
        }
        public void Remove((float priority, int index) unused)
        {
            var last = entries[--Count];
            int at = 0;
            while (at * 2 + 1 < Count)
            {
                int child = at * 2 + 1;
                if (child + 1 < Count && Compare(entries[child + 1], entries[child]) < 0) child++;
                if (Compare(last, entries[child]) <= 0) break;
                entries[at] = entries[child]; at = child;
            }
            entries[at] = last;
        }
    }
    bool stairSearchRunning;
    Vector3 stairSearchOrigin, stairSearchGoal;
    int stairSearchExamined;

    bool StairStep(Vector3 from, Vector3 horizontalTarget, out Vector3 point)
    {
        point = default;
        float step = motor.stepOffset + 0.02f;
        Vector3 ray = new Vector3(horizontalTarget.x, from.y + step + 0.04f, horizontalTarget.z);
        if (!Physics.Raycast(ray, Vector3.down, out RaycastHit hit, step * 2 + 0.12f,
            environment, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f) return false;
        point = hit.point + Vector3.up * 0.04f;
        if (Mathf.Abs(point.y - from.y) > step) return false;
        // Same tread-straddling clearance as subway walking. Check the upward
        // part too, so a low ceiling cannot be bypassed by the raised sweep.
        Vector3 a = from + Vector3.up * 0.18f, b = point + Vector3.up * 0.18f;
        Vector3 highA = new Vector3(a.x, Mathf.Max(a.y, b.y), a.z);
        Vector3 highB = new Vector3(b.x, Mathf.Max(a.y, b.y), b.z);
        return ClearBody(b) && ClearBodySweep(a, highA)
            && ClearBodySweep(highA, highB) && ClearBodySweep(highB, b);
    }

    bool NearbyStairTread()
    {
        Vector3 origin = transform.position;
        for (int direction = 0; direction < 8; direction++)
        {
            Vector3 axis = Quaternion.Euler(0, direction * 45, 0) * Vector3.forward;
            Vector3 previous = origin;
            int rises = 0;
            for (int sample = 1; sample <= 4; sample++)
            {
                if (!StairStep(previous, origin + axis * (sample * 0.25f), out Vector3 point)) break;
                if (Mathf.Abs(point.y - previous.y) > 0.06f) rises++;
                previous = point;
                if (rises >= 2) return true;
            }
        }
        return false;
    }

    bool BuildInteriorStairRoute(Vector3 destination)
    {
        BeginInteriorStairSearch(destination);
        while (stairSearchRunning) AdvanceInteriorStairSearch(128);
        return interiorStairRoute.Length > 0;
    }

    void BeginInteriorStairSearch(Vector3 destination)
    {
        stairNodes.Clear(); stairVisited.Clear(); stairOpen.Clear();
        interiorStairRoute = System.Array.Empty<Vector3>();
        interiorStairCorner = 0;
        Vector3 origin = transform.position;
        stairSearchOrigin = origin; stairSearchGoal = destination;
        stairSearchExamined = 0; stairSearchRunning = true;
        stairTrace = -1; stairResult.Clear();
        stairNodes.Add(new StairNode { point = origin, parent = -1 });
        stairVisited[new Vector3Int(0, Mathf.RoundToInt(origin.y * 20), 0)] = 0;
        stairOpen.Add((0, 0));
    }

    void AdvanceInteriorStairSearch(int budget)
    {
        using var sampleMarker = StairPlanMarker.Auto();
        Vector3 origin = stairSearchOrigin, destination = stairSearchGoal;
        if (stairTrace >= 0)
        {
            for (int i = 0; i < budget && stairTrace > 0; i++)
            { stairResult.Add(stairNodes[stairTrace].point); stairTrace = stairNodes[stairTrace].parent; }
            if (stairTrace > 0) return;
            stairResult.Reverse(); interiorStairRoute = stairResult.ToArray();
            stairTrace = -1; stairSearchRunning = false;
            interiorStairCorner = 0; interiorStairGoal = destination; interiorStairBlocked = 0;
            return;
        }
        int reached = -1;
        const float spacing = 0.2f;
        // Local bounded search, only near confirmed treads, and only on route
        // acquisition. Height is part of the key so stacked flights stay separate.
        for (int slice = 0; stairOpen.Count > 0 && stairSearchExamined < 4096 && slice < budget; slice++, stairSearchExamined++)
        {
            var entry = stairOpen.Min; stairOpen.Remove(entry);
            int index = entry.index;
            StairNode node = stairNodes[index];
            if (index > 0 && Mathf.Abs(node.point.y - destination.y) < 0.12f
                && Mathf.Abs(node.point.y - origin.y) > 0.4f)
            { reached = index; break; }
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                Vector3 sample = node.point + new Vector3(x * spacing, 0, z * spacing);
                if (Vector3.ProjectOnPlane(sample - origin, Vector3.up).sqrMagnitude > 144f
                    || !StairStep(node.point, sample, out Vector3 point)) continue;
                var key = new Vector3Int(Mathf.RoundToInt((point.x - origin.x) / spacing),
                    Mathf.RoundToInt(point.y * 20), Mathf.RoundToInt((point.z - origin.z) / spacing));
                if (stairVisited.ContainsKey(key)) continue;
                float cost = node.cost + Vector3.Distance(node.point, point);
                int next = stairNodes.Count;
                stairNodes.Add(new StairNode { point = point, parent = index, cost = cost });
                stairVisited.Add(key, next);
                float estimate = Vector3.Distance(point, destination) + Mathf.Abs(point.y - destination.y) * 3;
                stairOpen.Add((cost + estimate, next));
            }
        }
        if (reached < 0)
        {
            if (stairOpen.Count == 0 || stairSearchExamined >= 4096) stairSearchRunning = false;
            return;
        }
        stairTrace = reached;
    }

    bool WalkInteriorStairs(Vector3 destination, float dt)
    {
        if (interiorStairCorner < interiorStairRoute.Length
            && Mathf.Abs(destination.y - interiorStairGoal.y) > 0.5f)
            interiorStairRoute = System.Array.Empty<Vector3>();
        if (interiorStairCorner >= interiorStairRoute.Length)
        {
            if (!stairSearchRunning)
            {
                if (Time.time < nextInteriorStairSearch || Mathf.Abs(destination.y - transform.position.y) < 0.4f) return false;
                var source = Game.AI.EnemyManager.Instance;
                if (!Game.AI.NavigationWorkBudget.TryBegin(source != null ? source.PathBudgetMilliseconds : 1f,
                    source != null && Game.AI.Enemy.Active.Count > 0, out long started, out _, true)) return false;
                try
                {
                    nextInteriorStairSearch = Time.time + 2f;
                    if (!NearbyStairTread()) return false;
                    BeginInteriorStairSearch(destination);
                }
                finally { Game.AI.NavigationWorkBudget.End(started, true); }
            }
            if (Mathf.Abs(destination.y - stairSearchGoal.y) > 0.5f
                || (transform.position - stairSearchOrigin).sqrMagnitude > 0.25f)
            {
                stairSearchRunning = false;
                nextInteriorStairSearch = 0;
                return false;
            }
            var manager = Game.AI.EnemyManager.Instance;
            if (Game.AI.NavigationWorkBudget.TryBegin(manager != null ? manager.PathBudgetMilliseconds : 1f,
                manager != null && Game.AI.Enemy.Active.Count > 0, out long began, out long deadline, true))
            {
                try
                {
                    while (stairSearchRunning && System.Diagnostics.Stopwatch.GetTimestamp() < deadline)
                        AdvanceInteriorStairSearch(1);
                }
                finally { Game.AI.NavigationWorkBudget.End(began, true); }
            }
            if (stairSearchRunning)
            {
                MoveStairMotor(transform.position, dt);
                LastEvent = "checking connected interior stair flights";
                return true;
            }
            if (interiorStairRoute.Length == 0) return false;
            corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
            localDetourUntil = dropUntil = 0;
        }
        while (interiorStairCorner < interiorStairRoute.Length
            && Vector3.Distance(transform.position, interiorStairRoute[interiorStairCorner]) < 0.2f)
            interiorStairCorner++;
        if (interiorStairCorner >= interiorStairRoute.Length) { nextPath = 0; return false; }
        Vector3 point = interiorStairRoute[interiorStairCorner];
        // The capsule straddles the previous tread until its centre passes the
        // edge. Validate the stored adjacent floors, not that temporary body
        // height, or descending incorrectly looks like an oversized riser.
        Vector3 previousTread = interiorStairCorner > 0 ? interiorStairRoute[interiorStairCorner - 1] : transform.position;
        if (Mathf.Abs(point.y - transform.position.y) > 0.6f
            || !StairStep(previousTread, point, out Vector3 liveFloor)
            || Mathf.Abs(liveFloor.y - point.y) > 0.1f)
        {
            LastEvent = "stair segment no longer clear at " + point;
            interiorStairRoute = System.Array.Empty<Vector3>();
            nextInteriorStairSearch = Time.time + 5f;
            return false;
        }
        bool progressed = MoveStairMotor(point, dt, Mathf.Min(speed, 2.8f));
        interiorStairBlocked = progressed ? 0 : interiorStairBlocked + dt;
        if (interiorStairBlocked > 3f)
        {
            LastEvent = "stair motor blocked; using alternate movement";
            interiorStairRoute = System.Array.Empty<Vector3>();
            nextInteriorStairSearch = Time.time + 5f;
            return false;
        }
        LastEvent = destination.y > transform.position.y ? "walking up interior stairs" : "walking down interior stairs";
        return true;
    }

    bool MoveStairMotor(Vector3 waypoint, float dt, float walkSpeed = -1f)
    {
        if (!motor.enabled) motor.enabled = true;
        movementRequested = true;
        Vector3 before = transform.position;
        Vector3 toward = Vector3.ProjectOnPlane(waypoint - before, Vector3.up);
        bool supported = TryGroundSupport(before, out _);
        vertical = supported ? -2f : vertical - gravity * dt;
        motor.Move(Vector3.ClampMagnitude(toward, (walkSpeed < 0 ? speed : walkSpeed) * dt) + Vector3.up * vertical * dt);
        bool progressed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).sqrMagnitude > 0.00001f;
        if (progressed) stuck = 0;
        return progressed;
    }
}
