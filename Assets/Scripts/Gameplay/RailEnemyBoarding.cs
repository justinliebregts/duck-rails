using Game.AI;
using UnityEngine;

/// <summary>
/// Opt-in minion transfer: authored block <-> checked rear/side openings <-> train bake.
/// This component owns only the transfer and reference frame; the existing brain,
/// ground mover, melee reactions and budgeted pathfinder continue to run aboard.
/// </summary>
[DisallowMultipleComponent]
public sealed class RailEnemyBoarding : MonoBehaviour
{
    public const float MaxBoardingSpeed = .6f;
    public const float ApproachReach = 8f;
    enum Transfer { None, Approach, Lift, Across, ExitAcross, ExitDrop, ExitWalk }
    Enemy enemy;
    GroundLocomotion ground;
    RailTrainNavigation navigation;
    Transform frame;
    Matrix4x4 previousWorldToLocal;
    Quaternion previousRotation;
    bool attached, displaced, exitPlan;
    Transfer transfer;
    Vector3 anchor, approach, landing;
    float nextPlan, transferAge, retryAt;
    int anchorNode = -1, selectedOpening = -1;
    public int SelectedOpening => selectedOpening;
    public bool IsAboard => attached && frame != null;
    public bool HasBoardingPlan => anchorNode >= 0 || transfer != Transfer.None;
    public bool Crossing => transfer != Transfer.None;
    public bool OwnsBoundary => IsAboard || Crossing || displaced;
    public CrawlGraph NavigationGraph => IsAboard ? navigation?.Graph : null;
    public Transform Frame => IsAboard ? frame : null;
    public string DiagnosticState => "aboard=" + IsAboard + " transfer=" + transfer + " exitPlan=" + exitPlan
        + " opening=" + RailTrainNavigation.OpeningName(selectedOpening) + " anchor=" + anchorNode + " displaced=" + displaced
        + " speed=" + (RailTrain.Instance != null ? RailTrain.Instance.Speed.ToString("F2") : "-");
    void Trace(string action) => RailDiagnostics.Instance?.Event("Minion transfer " + action + " " + DiagnosticState);

    void Awake() { enemy = GetComponent<Enemy>(); ground = GetComponent<GroundLocomotion>(); }
    void OnDisable() => ResetFrame();
    public void ResetFrame()
    { attached = displaced = exitPlan = false; selectedOpening = -1; transfer = Transfer.None; anchorNode = -1; frame = null; navigation = null; }

    public void SetRemoteFrame(bool aboard, bool outsideBlock)
    {
        transfer = Transfer.None; anchorNode = -1; exitPlan = false;
        displaced = outsideBlock && !aboard;
        if (aboard == IsAboard) return;
        if (aboard) Attach(false);
        else { attached = false; frame = null; transfer = Transfer.None; anchorNode = -1; }
    }

    bool Attach(bool interrupt)
    {
        var train = RailTrain.Instance;
        navigation = train != null ? train.GetComponent<RailTrainNavigation>() : null;
        if (navigation == null || navigation.Graph == null) return false;
        frame = train.transform;
        attached = true; displaced = exitPlan = false; transfer = Transfer.None; anchorNode = -1;
        previousWorldToLocal = frame.worldToLocalMatrix; previousRotation = frame.rotation;
        if (interrupt) { ground.Interrupt(); Trace("boarded"); }
        return true;
    }

    // Called on both authority and proxies before locomotion/interpolation. Carrying
    // the rendered pose first prevents train motion being smoothed a second time.
    public void Carry()
    {
        if (!IsAboard || enemy == null || enemy.Root == null) return;
        var motion = frame.localToWorldMatrix * previousWorldToLocal;
        var rotation = frame.rotation * Quaternion.Inverse(previousRotation);
        enemy.CarryNavigationFrame(motion, rotation);
        previousWorldToLocal = frame.worldToLocalMatrix; previousRotation = frame.rotation;
    }

    public void BeforeTick(EnemyManager manager, float dt)
    {
        Carry();
        if (IsAboard && !SupportedByTrain(enemy.Root.position, manager.SurfaceMask))
        {
            attached = false; frame = null; displaced = true; retryAt = Time.time + 3f;
            GetComponent<RailEnemyBounds>()?.AdoptPosition(enemy.Root.position);
            ground.Interrupt(); enemy.FallFromTrain();
        }
        if (Crossing && enemy.HitMovementActive) Abort();
        if (displaced && !IsAboard && !Crossing && !enemy.HitMovementActive)
        {
            // Adopt only a nearby graph on the block where we actually landed.
            // The original spawn marker must not pull a transported enemy home.
            int node = manager.WorldGraph?.FindNearestGroundNode(enemy.Root.position, 1.5f) ?? -1;
            if (node >= 0)
            {
                GetComponent<RailEnemyBounds>()?.AdoptPosition(enemy.Root.position);
                displaced = false;
            }
            else if (!Physics.Raycast(enemy.Root.position + Vector3.up * .1f, Vector3.down, .4f,
                manager.SurfaceMask, QueryTriggerInteraction.Ignore)) enemy.FallFromTrain();
            else
            {
                int nearby = manager.WorldGraph?.FindNearestGroundNode(enemy.Root.position, ApproachReach) ?? -1;
                if (nearby >= 0)
                {
                    Vector3 destination = manager.WorldGraph.GetPosition(nearby);
                    if (ClearApproach(enemy.Root.position, destination, manager.SurfaceMask))
                    {
                        Vector3 delta = Vector3.ClampMagnitude(destination - enemy.Root.position, dt * 1.8f);
                        enemy.SetRootPose(ground.Displace(enemy.Root.position, delta, manager.SurfaceMask), enemy.Root.rotation);
                    }
                }
            }
        }
    }

    public static bool SupportedByTrain(Vector3 position, LayerMask mask)
    {
        var train = RailTrain.Instance;
        return train != null && Physics.Raycast(position + Vector3.up * .15f, Vector3.down,
            out var hit, .4f, mask, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(train.transform) && CrawlGraph.IsWalkableGround(hit.normal);
    }

    public Vector3 NavigationGoal(EnemyManager manager, Vector3 target)
    {
        if (Crossing) return target;
        bool exiting = IsAboard;
        var train = RailTrain.Instance;
        if (train == null || train.Speed > MaxBoardingSpeed || train.Contains(target) == exiting
            || Time.time < retryAt || enemy.HitMovementActive)
        { anchorNode = -1; exitPlan = false; return target; }
        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + .5f;
            anchorNode = -1; exitPlan = false;
            navigation = train.GetComponent<RailTrainNavigation>();
            var graph = manager.WorldGraph;
            if (navigation?.Graph == null || graph == null) return target;
            int current = graph.FindNearestGroundNode(exiting ? target : enemy.Root.position, exiting ? 3f : 1.5f);
            if (current < 0) return target;
            int previousOpening = selectedOpening;
            selectedOpening = -1;
            float bestRoute = float.PositiveInfinity;
            for (int opening = 0; opening < RailTrainNavigation.OpeningCount; opening++)
            {
                Vector3 near = train.transform.TransformPoint(navigation.Approach(opening));
                if ((enemy.Root.position - near).sqrMagnitude > 25f * 25f) continue;
                if (!Physics.Raycast(near + Vector3.up, Vector3.down, out var floor, 3f,
                    manager.SurfaceMask, QueryTriggerInteraction.Ignore)
                    || floor.collider.transform.IsChildOf(train.transform) || !CrawlGraph.IsWalkableGround(floor.normal)) continue;
                Vector3 candidateApproach = floor.point + Vector3.up * .02f;
                int deck = navigation.Graph.FindNearestGroundNode(train.transform.TransformPoint(navigation.Landing(opening)), .8f);
                if (deck < 0) continue;
                Vector3 deckWorld = navigation.Graph.GetPosition(deck) + Vector3.up * .02f;
                float rise = deckWorld.y - candidateApproach.y;
                if (rise < -.2f || rise > 1.8f) continue;
                Vector3 raised = new Vector3(candidateApproach.x, deckWorld.y + .08f, candidateApproach.z);
                // Reject railings, cargo and scenery before selecting an opening;
                // otherwise a blocked nearest gap would starve the clear alternatives.
                if (!ClearSweep(deckWorld, raised, manager.SurfaceMask)
                    || !ClearSweep(raised, candidateApproach, manager.SurfaceMask)) continue;

                int candidateNode = -1;
                Vector3 candidateAnchor = default;
                float best = ApproachReach * ApproachReach;
                graph.CellOf(candidateApproach, out int cx, out int cy, out int cz);
                int cells = Mathf.CeilToInt(ApproachReach / graph.CellSize);
                int vertical = Mathf.CeilToInt(.35f / graph.CellSize) + 1;
                for (int z = cz - cells; z <= cz + cells; z++)
                for (int x = cx - cells; x <= cx + cells; x++)
                for (int y = cy - vertical; y <= cy + vertical; y++)
                {
                    int n = graph.FindNodeAtCell(x, y, z);
                    if (!graph.IsGroundNode(n) || graph.GetComponent(n) != graph.GetComponent(current)) continue;
                    Vector3 point = graph.GetPosition(n);
                    float distance = (point - candidateApproach).sqrMagnitude;
                    if (distance >= best || Mathf.Abs(point.y - candidateApproach.y) > .35f) continue;
                    if (RailTrainObstacle.Contains(point, graph.CellSize)
                        || !ClearApproach(point, candidateApproach, manager.SurfaceMask)) continue;
                    best = distance; candidateNode = n; candidateAnchor = point;
                }
                if (candidateNode < 0) continue;
                // Estimate the complete approach + transfer + pursuit route.
                // A small preference for the current opening avoids path oscillation.
                float route = Vector3.Distance(enemy.Root.position, exiting ? deckWorld : candidateAnchor)
                    + Vector3.Distance(candidateAnchor, candidateApproach) + Vector3.Distance(candidateApproach, deckWorld)
                    + Vector3.Distance(exiting ? candidateAnchor : deckWorld, target)
                    - (opening == previousOpening ? .3f : 0);
                if (route >= bestRoute) continue;
                bestRoute = route; selectedOpening = opening; anchorNode = candidateNode;
                anchor = candidateAnchor; approach = candidateApproach;
                landing = train.transform.InverseTransformPoint(deckWorld);
            }
        }
        exitPlan = exiting && anchorNode >= 0;
        return anchorNode >= 0 ? (exiting ? train.transform.TransformPoint(landing) : anchor) : target;
    }
    bool ClearApproach(Vector3 from, Vector3 to, LayerMask mask)
    {
        if (!ClearSweep(from, to, mask)) return false;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .3f));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
            if (!Physics.Raycast(point + Vector3.up * .25f, Vector3.down, out var hit, .6f, mask,
                QueryTriggerInteraction.Ignore) || !CrawlGraph.IsWalkableGround(hit.normal)
                || Mathf.Abs(hit.point.y - point.y) > .2f) return false;
        }
        return true;
    }

    bool ClearSweep(Vector3 from, Vector3 to, LayerMask mask)
    {
        ground.GetCapsuleSize(out float radius, out float height);
        Vector3 a = from + Vector3.up * (radius + .03f);
        Vector3 b = a + Vector3.up * (height - radius * 2f);
        Vector3 delta = to - from;
        return !Physics.CheckCapsule(a, b, radius, mask, QueryTriggerInteraction.Ignore)
            && (delta.sqrMagnitude < .000001f || !Physics.CapsuleCast(a, b, radius, delta.normalized,
                delta.magnitude, mask, QueryTriggerInteraction.Ignore));
    }

    public bool TickTraversal(EnemyManager manager, float dt)
    {
        if (ground == null || enemy.HitMovementActive) return false;
        var train = RailTrain.Instance;
        if (train == null || train.Speed > MaxBoardingSpeed)
        { if (Crossing) Abort(); else anchorNode = -1; return false; }
        if ((int)transfer >= (int)Transfer.ExitAcross) return TickExit(manager, dt);
        if (IsAboard)
        {
            if (!exitPlan || anchorNode < 0 || (enemy.CurrentState != Enemy.State.Chase && enemy.CurrentState != Enemy.State.Search)
                || (enemy.Root.position - train.transform.TransformPoint(landing)).sqrMagnitude > .65f * .65f) return false;
            Vector3 exitRaised = new Vector3(approach.x, train.transform.TransformPoint(landing).y + .08f, approach.z);
            if (!ClearSweep(enemy.Root.position, exitRaised, manager.SurfaceMask)
                || !ClearSweep(exitRaised, approach, manager.SurfaceMask)
                || !ClearApproach(approach, anchor, manager.SurfaceMask))
            { anchorNode = -1; exitPlan = false; retryAt = Time.time + 1; Trace("exit blocked"); return false; }
            // Commit in world space so departure cannot carry an airborne enemy.
            attached = false; frame = null; displaced = true;
            landing = exitRaised; transfer = Transfer.ExitAcross; transferAge = 0;
            exitPlan = false; ground.Interrupt(); Trace("exit started");
            return TickExit(manager, dt);
        }
        if (!Crossing)
        {
            if (anchorNode < 0 || enemy.CurrentState != Enemy.State.Chase
                || (enemy.Root.position - anchor).sqrMagnitude > .65f * .65f) return false;
            if (!ClearApproach(enemy.Root.position, approach, manager.SurfaceMask))
            { anchorNode = -1; retryAt = Time.time + 1; return false; }
            transfer = Transfer.Approach; transferAge = 0; ground.Interrupt();
        }
        transferAge += dt;
        if (transferAge > 10f) { Abort(); return false; }
        Vector3 destination = train.transform.TransformPoint(landing);
        Vector3 raised = new Vector3(approach.x, destination.y + .08f, approach.z);
        if (Vector3.Distance(raised, destination) > 2.5f) { Abort(); return false; }
        Vector3 goal = transfer == Transfer.Approach ? approach : transfer == Transfer.Lift ? raised : destination;
        Vector3 old = enemy.Root.position;
        Vector3 next = Vector3.MoveTowards(old, goal, dt * 2.2f);
        if (!ClearSweep(old, next, manager.SurfaceMask)) { Abort(); return false; }
        Vector3 forward = Vector3.ProjectOnPlane(destination - old, Vector3.up);
        enemy.SetRootPose(next, forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : enemy.Root.rotation);
        if ((next - goal).sqrMagnitude > .0025f) return true;
        if (transfer == Transfer.Approach) transfer = Transfer.Lift;
        else if (transfer == Transfer.Lift) transfer = Transfer.Across;
        else if (SupportedByTrain(next, manager.SurfaceMask)) Attach(true);
        else Abort();
        return true;
    }

    bool TickExit(EnemyManager manager, float dt)
    {
        transferAge += dt;
        if (transferAge > 10f) { Abort(); return false; }
        Vector3 goal = transfer == Transfer.ExitAcross ? landing : transfer == Transfer.ExitDrop ? approach : anchor;
        Vector3 old = enemy.Root.position;
        Vector3 next = Vector3.MoveTowards(old, goal, dt * 2.2f);
        if (!ClearSweep(old, next, manager.SurfaceMask)
            || (transfer == Transfer.ExitWalk && !ClearApproach(old, next, manager.SurfaceMask)))
        { Abort(); return false; }
        Vector3 forward = Vector3.ProjectOnPlane(goal - old, Vector3.up);
        enemy.SetRootPose(next, forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : enemy.Root.rotation);
        if ((next - goal).sqrMagnitude > .0025f) return true;
        if (transfer == Transfer.ExitAcross) transfer = Transfer.ExitDrop;
        else if (transfer == Transfer.ExitDrop) transfer = Transfer.ExitWalk;
        else
        {
            transfer = Transfer.None; anchorNode = -1; displaced = false; retryAt = Time.time + 1;
            ground.Interrupt();
            GetComponent<RailEnemyBounds>()?.AdoptPosition(next);
            Trace("exit completed");
        }
        return true;
    }

    void Abort()
    {
        bool falling = transfer == Transfer.Lift || transfer == Transfer.Across || transfer == Transfer.ExitAcross || transfer == Transfer.ExitDrop;
        Trace("aborted"); exitPlan = false;
        transfer = Transfer.None; anchorNode = -1; displaced = true; retryAt = Time.time + 2;
        ground?.Interrupt();
        if (falling && !enemy.HitMovementActive) enemy.FallFromTrain();
    }
}
