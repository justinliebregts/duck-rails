using UnityEngine;

/// <summary>
/// Invisible lateral block walls plus local last-ground recovery. Falling never downs a player:
/// leaving the playable area returns the local player to their last solid ground.
/// </summary>
[DefaultExecutionOrder(12500)] // After locomotion and RailCargo.
public sealed class RailPlayerBounds : MonoBehaviour
{
    public const string WallName = "Player boundary";
    public const float WallThickness = 1f, WallBelow = 60f, WallAbove = 120f;
    // Kill floor sits this far below the block's lowest authored surface (never above -FloorMargin).
    public const float FloorMargin = 10f;
    const float LateralSlack = 1.5f, OutsideDelay = .25f, SampleInterval = .2f, PushInterval = 1f, RepeatWindow = 6f;
    const float GroundTolerance = .2f, MinGroundNormalY = .7f, EdgeMargin = .5f;
    const int SampleCount = 6;

    struct Sample { public Vector3 point; public bool train; }
    readonly Sample[] samples = new Sample[SampleCount];
    int count, newest = -1;
    float nextSample, lastPush = -100f, outsideFor, lastRecovery = -100f;
    int repeat, groundMask;
    string run;
    public int Recoveries { get; private set; }

    // ---------- Walls ----------
    public static void BuildWalls(RailBlock block)
    {
        if (block == null || block.playerHalfWidth <= 0f || block.length <= 0f || block.transform.Find(WallName) != null) return;
        var root = new GameObject(WallName).transform;
        root.SetParent(block.transform, false);
        int layer = LayerMask.NameToLayer("Environment");
        for (int side = -1; side <= 1; side += 2)
        {
            var wall = new GameObject(side < 0 ? "Left" : "Right");
            wall.layer = layer >= 0 ? layer : 0;
            wall.transform.SetParent(root, false);
            var box = wall.AddComponent<BoxCollider>();
            box.center = new Vector3(side * (block.playerHalfWidth + WallThickness * .5f), (WallAbove - WallBelow) * .5f, block.length * .5f);
            box.size = new Vector3(WallThickness, WallAbove + WallBelow, block.length);
        }
    }

    // ---------- Area queries (allocation-free) ----------
    public static float FloorY(RailBlock block)
        => block == null ? -FloorMargin : block.transform.position.y + Mathf.Min(block.enemyBounds.min.y, 0f) - FloorMargin;

    public static bool BelowFloor(Vector3 position)
    {
        if (position.y >= -FloorMargin) return false; // Every block's floor is at or below this.
        var route = RailRoute.Instance;
        if (route == null || !route.Ready) return false;
        return position.y < FloorY(route.TryPlayerArea(position, out var block) ? block : null);
    }

    public static bool Outside(Vector3 position)
    {
        var route = RailRoute.Instance;
        if (route == null || !route.Ready) return false;
        if (!route.TryPlayerArea(position, out var block)) return position.y < FloorY(null);
        if (position.y < FloorY(block)) return true;
        return block.playerHalfWidth > 0f && Mathf.Abs(position.x - block.transform.position.x) > block.playerHalfWidth + LateralSlack;
    }

    // ---------- Local fall recovery ----------
    void LateUpdate()
    {
        var route = RailRoute.Instance;
        var expedition = RailExpedition.Instance;
        // The victory ride keeps its own return-aboard rule; it never uses last-ground recovery.
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || route == null || !route.Ready || GR.Player.instance == null
            || (expedition != null && expedition.State.finished))
        { outsideFor = 0f; return; }
        if (run != PhotonSession.RunId) { run = PhotonSession.RunId; count = 0; newest = -1; repeat = 0; }

        var desktop = GR.Player.instance.GetComponent<DesktopPlayer>();
        var vr = desktop == null ? NewPlayer.instance : null;
        Vector3 center; float reach; bool grounded;
        if (desktop != null) { center = desktop.PassengerAnchor; reach = center.y - desktop.PassengerFoot.y; grounded = desktop.IsGrounded; }
        else if (vr != null && vr.bodyCollider != null) { var b = vr.bodyCollider.bounds; center = b.center; reach = b.extents.y; grounded = true; }
        else return;

        if (Outside(center))
        {
            outsideFor += Time.deltaTime;
            if (outsideFor >= OutsideDelay) Recover(desktop, vr);
            return;
        }
        outsideFor = 0f;
        if (grounded && Time.time >= nextSample) { nextSample = Time.time + SampleInterval; TrySample(center, reach); }
    }

    void TrySample(Vector3 center, float reach)
    {
        if (groundMask == 0) groundMask = LayerMask.GetMask("Default", "Environment");
        int mask = groundMask;
        if (!Physics.Raycast(center, Vector3.down, out var hit, reach + GroundTolerance, mask, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y < MinGroundNormalY) return;
        var body = hit.collider.attachedRigidbody;
        if (body != null && !body.isKinematic) return; // Loose props are not safe ground.
        if (hit.collider.GetComponentInParent<Grabbable>() != null || hit.collider.GetComponentInParent<Game.AI.Enemy>() != null) return;
        var route = RailRoute.Instance;
        if (!route.TryPlayerArea(hit.point, out var block) || hit.point.y < FloorY(block)
            || Mathf.Abs(hit.point.x - block.transform.position.x) > block.playerHalfWidth - EdgeMargin) return;

        var train = RailTrain.Instance;
        var sample = new Sample();
        if (train != null && train.Contains(hit.point + Vector3.up * .05f))
        { sample.train = true; sample.point = train.transform.InverseTransformPoint(hit.point); }
        else if (hit.collider.GetComponentInParent<Helicopter>() != null) return; // Other moving platforms are not stored.
        else sample.point = hit.point;

        if (newest < 0 || Time.time - lastPush >= PushInterval)
        {
            newest = (newest + 1) % SampleCount; count = Mathf.Min(count + 1, SampleCount); lastPush = Time.time;
        }
        samples[newest] = sample;
    }

    void Recover(DesktopPlayer desktop, NewPlayer vr)
    {
        outsideFor = 0f;
        // Falling again shortly after a recovery steps further back through the history.
        repeat = Time.time - lastRecovery < RepeatWindow ? repeat + 1 : 0;
        lastRecovery = Time.time;
        var train = RailTrain.Instance;
        Vector3 target; string source;
        if (count > 0)
        {
            int back = Mathf.Min(repeat, count - 1);
            int index = (newest - back + SampleCount) % SampleCount;
            var s = samples[index];
            if (s.train && train == null) { count = 0; newest = -1; Recover(desktop, vr); return; }
            target = s.train ? train.transform.TransformPoint(s.point) : s.point;
            source = s.train ? "train" : "ground";
            // Discard the newer samples that led here.
            newest = index; count -= back;
        }
        else if (train != null)
        {
            target = train.AboardPoint(Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber); source = "train fallback";
        }
        else return;
        target += Vector3.up * .05f;
        if (desktop != null) desktop.Teleport(target, desktop.transform.rotation);
        else vr?.Warp(target);
        nextSample = Time.time + .5f; // Let the warp settle before sampling again.
        Recoveries++;
        DuckzLog.Log($"[Rails] Fall recovery to last {source} at {target} (step back {repeat})", this);
    }
}
