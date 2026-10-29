using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch regressions for slope seams, oscillation and per-enemy route avoidance.</summary>
public static class GroundRecoveryChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static object Get(object o, string field) => o.GetType().GetField(field, Private).GetValue(o);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        Oscillation(false);
        Oscillation(true);
        RouteAvoidance();
        ProlongedStall();
        ThinTerrainFalls();
        SharedFallRecovery();
        HitRecovery();
        SpiderSurfaceRecovery();
        foreach (float angle in new[] { 20f, 40f, 60f }) RampSeam(angle);
        Debug.Log("GroundRecoveryChecks: ALL PASSED");
    }

    public static void SpiderSurfaceRecovery()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
        Box(new Vector3(3, 2, 0), new Vector3(4, 4, 10));
        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        var actor = new GameObject("Spider surface recovery");
        var climb = actor.AddComponent<ClimbLocomotion>();
        var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake");
        Set(enemy, "_hadAuthority", true);
        Vector3 saved = new Vector3(1, 1.5f, 0);
        actor.transform.position = saved;
        // The rendered up is still smoothing from the floor; collision is on the wall.
        Set(climb, "_surfaceUp", Vector3.left);
        Physics.SyncTransforms();
        Call(enemy, "RememberHitSafePose", (LayerMask)(1 << 6));
        Check((bool)Get(enemy, "_hasHitSafePose"), "Wall pose is saved");
        Check((Vector3)Get(enemy, "_hitSafeUp") == Vector3.left, "Save collision normal rather than smoothed root up");
        Set(climb, "_surfaceUp", Vector3.up);
        actor.transform.position = new Vector3(2.2f, 0, 0);
        Call(enemy, "BeginMeleeShove", Vector3.right, 3f);
        enemy.TickAuthority(manager, .02f, false);
        Check(Vector3.Distance(actor.transform.position, saved) < .001f, "Restore saved wall position across a normal change");
        Check(Vector3.Dot(actor.transform.up, Vector3.left) > .999f
            && Vector3.Dot(climb.SurfaceNormal, Vector3.left) > .999f, "Restore root and locomotion orientation together");
        Check(climb.IsRecoveryPoseValid(saved, Vector3.left, 1 << 6, out _), "Restored wall pose is clear and supported");
        Check(!climb.IsRecoveryPoseValid(new Vector3(2, 1, 0), Vector3.left, 1 << 6, out string reason)
            && reason.Contains("overlaps"), "Reject an obstructed saved pose");
        Check(!climb.IsRecoveryPoseValid(new Vector3(-5, 5, 0), Vector3.up, 1 << 6, out reason)
            && reason.Contains("supports"), "Reject a saved pose whose support disappeared");
        Set(climb, "_surfaceUp", Vector3.up);
        Check(!climb.TryLocalRecovery(new Vector3(2.2f, 0, 0), 1 << 6, out _, out _), "Local search cannot cross a thick wall");
        UnityEngine.Object.DestroyImmediate(actor);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
        Box(new Vector3(.3f, 1, 0), new Vector3(.4f, 2, 5));
        actor = new GameObject("Spider local recovery");
        climb = actor.AddComponent<ClimbLocomotion>();
        Vector3 embedded = new Vector3(.08f, 0, 0);
        Physics.SyncTransforms();
        Check(climb.Overlaps(embedded, 1 << 6), "Local recovery starts overlapping");
        Check(climb.TryLocalRecovery(embedded, 1 << 6, out Vector3 local, out Vector3 up), "Find nearby supported escape");
        Check(Vector3.Distance(embedded, local) <= 1f && local.x < .1f,
            "Local escape stays nearby on the original side of the wall");
        Check(climb.IsRecoveryPoseValid(local, up, 1 << 6, out _), "Local escape is clear and supported");
        Check(climb.SurfaceNormal == Vector3.up, "Queries do not mutate locomotion orientation");
        Debug.Log("SpiderSurfaceRecovery: ALL PASSED");
    }

    public static void HitRecovery()
    {
        foreach (bool climbing in new[] { false, true })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
            Box(new Vector3(3, 2, 0), new Vector3(4, 4, 10));
            var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
            var manager = managerObject.AddComponent<EnemyManager>();
            Set(manager, "surfaceMask", (LayerMask)(1 << 6));
            var actor = new GameObject("Hit recovery enemy");
            GroundLocomotion ground = climbing ? null : actor.AddComponent<GroundLocomotion>();
            ClimbLocomotion climb = climbing ? actor.AddComponent<ClimbLocomotion>() : null;
            var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake");
            Set(enemy, "_hadAuthority", true);
            Physics.SyncTransforms();
            Call(enemy, "RememberHitSafePose", (LayerMask)(1 << 6));
            Check((bool)Get(enemy, "_hasHitSafePose"), "Capture supported safe pose");
            Vector3 embedded = new Vector3(2.2f, 0, 0), proposed = embedded;
            bool separated = climbing ? climb.Separate(ref proposed, 1 << 6) : ground.Separate(ref proposed, 1 << 6);
            Check(!separated && proposed == embedded, "Failed separation must not mutate position: " + climbing);
            actor.transform.position = embedded; Physics.SyncTransforms();
            Call(enemy, "BeginMeleeShove", Vector3.right, 3f);
            enemy.TickAuthority(manager, .02f, false);
            Check(actor.transform.position.sqrMagnitude < .001f, "Restore nearby safe pose: " + climbing);
            Check(!(bool)Get(enemy, "_dropActive") && !(bool)Get(enemy, "_shoveActive"), "Overlap recovery cannot start falling or retain shove");
            Check(enemy.CurrentState == Enemy.State.Retreat, "Local recovery enters retreat");

            // Repeated accepted hits at a wall preserve retreat and its remaining time.
            actor.transform.position = new Vector3(climbing ? .75f : .60f, 0, 0);
            Physics.SyncTransforms();
            Set(enemy, "_retreatRemaining", 2f);
            for (int i = 0; i < 30; i++)
            {
                Set(enemy, "_nextHitTime", -1f);
                if (i % 2 == 0) enemy.ReceiveSwing(Vector3.right * 9f, 1f);
                else enemy.ReceiveThrownHit(Vector3.right * 9f);
                Check(!(bool)Get(enemy, "_shoveActive"), "Blocked repeated hit must not restart shove");
                Check((float)Get(enemy, "_retreatRemaining") == 2f, "Blocked hit preserves retreat timer");
            }
            // Without a safe pose, wait for local recovery rather than falling inside the wall.
            Set(enemy, "_hasHitSafePose", false);
            actor.transform.position = embedded; Physics.SyncTransforms();
            enemy.TickAuthority(manager, .02f, false);
            Check(actor.transform.position == embedded && !(bool)Get(enemy, "_dropActive"), "Unresolved overlap does not become a fall");
            UnityEngine.Object.DestroyImmediate(actor);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
            foreach (Vector3 wallPosition in new[] { new Vector3(1, 2, 0), new Vector3(0, 2, 1) })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.layer = 6; wall.transform.position = wallPosition;
                wall.transform.localScale = wallPosition.x > 0 ? new Vector3(.1f, 4, 6) : new Vector3(6, 4, .1f);
                UnityEngine.Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
                wall.AddComponent<MeshCollider>().sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh;
            }
            managerObject = new GameObject("Manager"); managerObject.SetActive(false);
            manager = managerObject.AddComponent<EnemyManager>();
            Set(manager, "surfaceMask", (LayerMask)(1 << 6));
            actor = new GameObject("Mesh corner enemy");
            ground = climbing ? null : actor.AddComponent<GroundLocomotion>();
            climb = climbing ? actor.AddComponent<ClimbLocomotion>() : null;
            enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake");
            Set(enemy, "_hadAuthority", true);
            Physics.SyncTransforms();
            for (int frame = 0; frame < 200; frame++)
            {
                if (frame % 13 == 0)
                {
                    Set(enemy, "_nextHitTime", -1f);
                    enemy.ReceiveThrownHit(new Vector3(1, 0, 1).normalized * 9f);
                }
                enemy.TickAuthority(manager, .02f, false);
                Physics.SyncTransforms();
                Check(!(climbing ? climb.Overlaps(actor.transform.position, 1 << 6) : ground.Overlaps(actor.transform.position, 1 << 6)),
                    "Repeated hits cannot embed body in a thin mesh corner: " + climbing);
                Check(!(bool)Get(enemy, "_dropActive"), "Mesh corner retains ground support");
            }
        }
        Debug.Log("HitRecovery: ALL PASSED");
    }

    public static void SharedFallRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(10, -.5f, 0), new Vector3(4, 1, 4));
        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        var spawn = new GameObject("Spawn").transform; spawn.position = new Vector3(10, 2, 0);
        Set(manager, "spawnPoints", new[] { spawn });
        foreach (bool climbing in new[] { false, true })
        {
            var actor = new GameObject("Falling enemy");
            if (climbing) actor.AddComponent<ClimbLocomotion>();
            else actor.AddComponent<GroundLocomotion>();
            var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake");
            actor.transform.position = Vector3.up * -10;
            Physics.SyncTransforms();
            Call(enemy, "TickFallRecovery", manager, 1f);
            actor.transform.position += Vector3.down;
            for (int i = 0; i < 19; i++)
                Check(!(bool)Call(enemy, "TickFallRecovery", manager, 1f), "No respawn before 20 seconds");
            // No further movement: the knockback timeout must not cancel recovery.
            Check((bool)Call(enemy, "TickFallRecovery", manager, 1f), "Shared watchdog respawns ground/climbing enemies");
            Check(Mathf.Abs(actor.transform.position.x - 10) < .01f, "Manager selects grounded spawn");
            Check((float)Get(enemy, "_fallTime") == 0f, "Teleport resets fall timer");
            actor.transform.position = Vector3.down * 10; Physics.SyncTransforms();
            Set(enemy, "_fallTime", 19f); Set(manager, "spawnPoints", new Transform[0]);
            Check(!(bool)Call(enemy, "TickFallRecovery", manager, 1f), "Missing spawn preserves pending recovery");
            Set(manager, "spawnPoints", new[] { spawn });
            Check(!(bool)Call(enemy, "TickFallRecovery", manager, .5f), "Retry is throttled");
            Check((bool)Call(enemy, "TickFallRecovery", manager, .5f), "Retry succeeds after one second");
            Physics.SyncTransforms(); Set(enemy, "_fallTime", 19f);
            Check(!(bool)Call(enemy, "TickFallRecovery", manager, 1f) && (float)Get(enemy, "_fallTime") == 0f,
                "Actual support cancels pending fall");
            if (climbing)
            {
                foreach (Vector3 normal in new[] { Vector3.left, Vector3.down })
                {
                    var surface = Box(new Vector3(30, 0, 0) - normal * .5f, Vector3.one);
                    actor.transform.position = new Vector3(30, 0, 0);
                    Set(actor.GetComponent<ClimbLocomotion>(), "_surfaceUp", normal);
                    Physics.SyncTransforms(); Set(enemy, "_fallTime", 19f);
                    Check(!(bool)Call(enemy, "TickFallRecovery", manager, 1f) && (float)Get(enemy, "_fallTime") == 0f,
                        "Wall and ceiling attachments cancel fall recovery");
                    UnityEngine.Object.DestroyImmediate(surface);
                }
            }
            UnityEngine.Object.DestroyImmediate(actor);
        }
        var knocked = new GameObject("Knocked enemy");
        knocked.transform.position = Vector3.down * 10;
        var body = knocked.AddComponent<Rigidbody>();
        knocked.AddComponent<CapsuleCollider>();
        var knockedEnemy = knocked.AddComponent<Enemy>(); Call(knockedEnemy, "Awake");
        Set(knockedEnemy, "_hadAuthority", true);
        Call(knockedEnemy, "BeginFlight", Vector3.down);
        for (int i = 0; i < 19; i++) knockedEnemy.TickAuthority(manager, 1f, false);
        Check(knockedEnemy.IsKnocked, "No early knockback respawn");
        knockedEnemy.TickAuthority(manager, 1f, false);
        Check(!knockedEnemy.IsKnocked && Mathf.Abs(knocked.transform.position.x - 10) < .01f,
            "Watchdog runs before knockback early return and teleport clears physics flight");
        UnityEngine.Object.DestroyImmediate(knocked);
        Debug.Log("SharedFallRecovery: ALL PASSED");
    }

    static void Oscillation(bool landingEachTick)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
        var actor = new GameObject("Smasher");
        var ground = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        Call(enemy, "Awake");
        enemy.SetRemoteState(Enemy.State.Chase);
        Set(enemy, "_lastSeenPos", Vector3.forward * 10);
        var managerObject = new GameObject("Manager");
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Physics.SyncTransforms();
        ground.Reset();
        for (int i = 0; i < 110 && !ground.IsRecovering; i++)
        {
            Vector3 pos = new Vector3(i % 2 == 0 ? -.3f : .3f, 0, 0);
            actor.transform.position = pos;
            if (landingEachTick)
            {
                ground.RecordUnplannedFall(.02f);
                actor.transform.position = ground.AdoptLanding(ground.CapsuleBottomOnSurface(pos, Vector3.up), Vector3.up);
                ground.CheckAfterUnplannedLanding(enemy, manager, actor.transform.position);
            }
            else Call(ground, "UpdateStuck", enemy, manager, .02f, pos);
        }
        Check(ground.IsRecovering, "Oscillation must trigger escape, including repeated landings: " + landingEachTick);
        Vector3 escape = (Vector3)Get(ground, "_escapeTarget");
        Check(escape.z < actor.transform.position.z, "First recovery backs away from blocked chase direction");
        ground.AdoptLanding(ground.SupportedCapsuleBottom(actor.transform.position, manager.SurfaceMask), Vector3.up);
        Check(ground.IsRecovering, "Brief landing preserves active escape");

        ground.Reset();
        for (int i = 0; i < 110; i++)
        {
            actor.transform.position += Vector3.forward * .02f;
            Call(ground, "UpdateStuck", enemy, manager, .02f, actor.transform.position);
        }
        Check(!ground.IsRecovering, "Ordinary forward travel must not trigger escape");
    }

    static void RouteAvoidance()
    {
        var graph = new CrawlGraph {
            NodeCount = 4, NodePos = new[] { 0f,0f,0f, 1f,0f,0f, 0f,0f,1f, 2f,0f,0f },
            NodeNormal = new[] { CrawlGraph.PackNormal(Vector3.up), CrawlGraph.PackNormal(Vector3.up), CrawlGraph.PackNormal(Vector3.up), CrawlGraph.PackNormal(Vector3.up) },
            NodeSurface = new byte[4], NodeCluster = new int[4],
            EdgeStart = new[] { 0,2,3,4,4 }, EdgeTarget = new[] { 1,2,3,3 },
            ClusterCount = 1, ClusterComponent = new int[1], ClusterEdgeTarget = new int[0]
        };
        var finder = new CrawlPathfinder(graph);
        int[] path = new int[8];
        Check(finder.FindPath(0, 3, path, out _, out bool reached, true) && reached && path[1] == 1, "Baseline route");
        Check(finder.FindPath(0, 3, path, out _, out reached, true, new HashSet<int> { 1 }) && reached && path[1] == 2, "Blocked route takes alternative");
        Check(finder.FindPath(0, 3, path, out _, out reached, true) && reached && path[1] == 1, "Avoidance does not leak to another enemy/search");
    }

    static void ProlongedStall()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(10, -.5f, 0), new Vector3(4, 1, 4));
        var actor = new GameObject("Ground enemy");
        var ground = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        actor.AddComponent<ZombieSmasherJump>(); // Scratch brain also resets this component on teleport.
        Call(enemy, "Awake");
        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        var spawn = new GameObject("Spawn").transform; spawn.position = new Vector3(10, 2, 0);
        Set(manager, "spawnPoints", new[] { spawn });
        Physics.SyncTransforms(); ground.Reset();

        Call(ground, "BlockNode", 12);
        var expiry = (Dictionary<int, float>)Get(ground, "_blockedExpiry");
        float deadline = expiry[12];
        Call(ground, "BlockNode", 12);
        Check(expiry[12] == deadline, "Repeated failures cannot extend an active restriction");
        expiry[12] = Time.time - 1;
        Call(ground, "BlockNode", 13);
        Check(!expiry.ContainsKey(12) && expiry.ContainsKey(13), "Restrictions expire independently");

        Set(ground, "_stalled", true);
        for (int i = 0; i < 19; i++)
            Check(!(bool)Call(ground, "TickStallRecovery", enemy, manager, 1f), "No early relocation");
        Check((bool)Call(ground, "TickStallRecovery", enemy, manager, 1f), "20-second stall relocates");
        Check(Mathf.Abs(actor.transform.position.x - 10) < .01f, "Uses EnemyManager spawn point");
        Check(!(bool)Get(ground, "_stalled"), "Teleport resets watchdog");

        Set(ground, "_stalled", true); Set(ground, "_stallOrigin", actor.transform.position);
        Set(ground, "_pathCount", 1); Set(ground, "_pathIndex", 1); Set(ground, "_pauseTimer", 2f);
        Check(!(bool)Call(ground, "TickStallRecovery", enemy, manager, 30f)
            && !(bool)Get(ground, "_stalled"), "Completed wander pause does not relocate");
        enemy.SetRemoteState(Enemy.State.Chase);
        Set(enemy, "_lastSeenPos", actor.transform.position);
        Set(enemy, "_seen", true);
        Set(ground, "_stalled", true);
        Check(!(bool)Call(ground, "TickStallRecovery", enemy, manager, 30f)
            && !(bool)Get(ground, "_stalled"), "Attack-range stop does not relocate");
    }

    static void ThinTerrainFalls()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var terrain = new GameObject("One-sided terrain"); terrain.layer = 6;
        var mesh = new Mesh {
            vertices = new[] { new Vector3(-10,0,-10), new Vector3(-10,0,10), new Vector3(10,0,10), new Vector3(10,0,-10) },
            triangles = new[] { 0,1,2, 0,2,3 }
        };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        var actor = new GameObject("Smasher");
        var ground = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        var jump = actor.AddComponent<ZombieSmasherJump>();
        Call(enemy, "Awake"); Call(jump, "Awake");
        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Physics.SyncTransforms();
        foreach (float height in new[] { -.08f, 3f })
        {
            actor.transform.position = Vector3.up * height;
            ground.Reset(); jump.ResetJump(); enemy.SetRemoteState(Enemy.State.Wander);
            for (int i = 0; i < 10; i++) jump.TickJump(enemy, manager, .5f);
            Check(!jump.IsJumping && actor.transform.position.y >= -.001f
                && actor.transform.position.y < .1f, "Thin mesh catches embedded and fast-falling capsules: " + height);
        }
        // A supported pose is the fallback, not an arbitrary airborne point.
        actor.transform.position = Vector3.zero; ground.Reset(); jump.ResetJump();
        ground.ValidateSmasherPose(enemy, 1 << 6);
        actor.transform.position = Vector3.down * .2f;
        Check(ground.ValidateSmasherPose(enemy, 1 << 6) && actor.transform.position.y >= -.001f,
            "Resolve an already overlapping capsule before sweeping");
        ground.ValidateSmasherPose(enemy, 1 << 6);
        actor.transform.position = Vector3.down * 2f;
        Check(ground.ValidateSmasherPose(enemy, 1 << 6) && actor.transform.position.y >= -.001f,
            "Crossing a one-sided mesh restores the verified safe pose");
        ground.ValidateSmasherPose(enemy, 1 << 6);
        terrain.SetActive(false); Physics.SyncTransforms();
        actor.transform.position = Vector3.down * 2f;
        Check(!ground.ValidateSmasherPose(enemy, 1 << 6) && actor.transform.position.y < -1f,
            "Removed support does not restore a stale safe pose");
        terrain.SetActive(true); Physics.SyncTransforms();
        // A real edge must still fall to the lower floor, never cling to its old height.
        var roof = Box(new Vector3(0, 2.75f, 0), new Vector3(2, .5f, 2));
        Physics.SyncTransforms();
        actor.transform.position = new Vector3(3, 3, 0);
        ground.Reset(); jump.ResetJump();
        Check(jump.TickJump(enemy, manager, .02f) && jump.IsJumping, "Unsupported roof height begins falling");
        for (int i = 0; i < 100 && jump.IsJumping; i++) jump.TickJump(enemy, manager, .02f);
        Check(!jump.IsJumping && actor.transform.position.y < .1f && actor.transform.position.y >= -.001f,
            "Roof edge reaches actual ground");
        UnityEngine.Object.DestroyImmediate(terrain);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    static void RampSeam(float angle)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
        var ramp = Box(Vector3.zero, new Vector3(8, .5f, 8));
        ramp.transform.rotation = Quaternion.Euler(0, 0, angle);
        ramp.transform.position = ramp.transform.right * 4 - ramp.transform.up * .25f;
        var actor = new GameObject("Smasher");
        var ground = actor.AddComponent<GroundLocomotion>();
        actor.transform.position = Vector3.left;
        Physics.SyncTransforms();
        for (int i = 0; i < 180; i++)
        {
            Vector3 before = actor.transform.position;
            actor.transform.position = (Vector3)Call(ground, "MoveWithWalls", before, Vector3.right * .025f, (LayerMask)(1 << 6));
            Check(Vector3.Distance(before, actor.transform.position) < .15f, "No seam position spikes: " + angle + " frame=" + i + " before=" + before.ToString("F4") + " after=" + actor.transform.position.ToString("F4"));
            Check(ground.HasGroundSupport(actor.transform.position, 1 << 6), "Seam retains support: " + angle);
        }
        Check(actor.transform.position.x > .3f && actor.transform.position.y > .1f, "Cross floor/ramp seam: " + angle);
    }

    static GameObject Box(Vector3 position, Vector3 size)
    {
        var obj = new GameObject("Surface"); obj.layer = 6; obj.transform.position = position;
        obj.AddComponent<BoxCollider>().size = size; return obj;
    }
}

