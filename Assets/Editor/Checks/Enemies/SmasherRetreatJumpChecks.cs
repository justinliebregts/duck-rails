using System;
using System.Reflection;
using Game.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SmasherRetreatJumpChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object value) => o.GetType().GetField(n, P).SetValue(o, value);
    static object Get(object o, string n) => o.GetType().GetField(n, P).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, P).Invoke(o, args);
    static void Check(bool b, string message) { if (!b) throw new Exception(message); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        foreach (bool kill in new[] { false, true }) BusEscape(kill);
        RejectedEscape(false); RejectedEscape(true);
        FlareJump();
        FlareEscapeFromRoof();
        Debug.Log("SmasherRetreatJumpChecks: ALL PASSED");
    }

    static void Setup(out Enemy enemy, out ZombieSmasherJump jump, out EnemyManager manager)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(60, 1, 60));
        var actor = new GameObject("Smasher");
        actor.AddComponent<GroundLocomotion>(); enemy = actor.AddComponent<Enemy>();
        jump = actor.AddComponent<ZombieSmasherJump>();
        Call(jump, "Awake"); Call(enemy, "Awake"); enemy.ResetBrain();
        Set(enemy, "_hadAuthority", true);
        var mo = new GameObject("Manager"); mo.SetActive(false);
        manager = mo.AddComponent<EnemyManager>(); Set(manager, "surfaceMask", (LayerMask)(1 << 6));
    }

    static void BusEscape(bool kill)
    {
        Setup(out var enemy, out var jump, out var manager);
        Box(new Vector3(0, 1.25f, 0), new Vector3(3, 2.5f, 10));
        enemy.Teleport(new Vector3(0, 2.5f, 0), Quaternion.identity);
        Set(jump, "_nextCheck", Time.time + 100f);
        Set(enemy, "_searchKillLocation", kill);
        Set(enemy, "_killLocation", enemy.Root.position - Vector3.right);
        UnityEngine.Random.InitState(41);
        Physics.SyncTransforms();
        if (kill)
        {
            Set(enemy, "_killRetreatPending", true);
            enemy.TickAuthority(manager, .02f, false);
        }
        else Call(enemy, "BeginRetreat", manager, Vector3.right);
        Check(!jump.IsJumping && enemy.CurrentState == Enemy.State.Retreat, "Retreat starts by walking: kill=" + kill);
        Set(jump, "_nextCheck", 0f);
        // Simulate successful walking on the long bus roof. Progress must prevent fallback.
        for (int i = 0; i < 12; i++)
        {
            enemy.Root.position += Vector3.forward * .3f;
            Physics.SyncTransforms();
            Call(enemy, "TickRetreat", manager, .1f);
        }
        Check(!jump.IsJumping && (float)Get(enemy, "_retreatStalledTime") < .2f, "Walking progress prevents fallback");
        for (int i = 0; i < 5; i++) Call(enemy, "TickRetreat", manager, .1f);
        Check(!jump.IsJumping, "Brief walking pauses do not trigger a jump");
        Set(jump, "_nextCheck", Time.time + 100f);
        for (int i = 0; i < 12; i++) Call(enemy, "TickRetreat", manager, .1f);
        Check(!jump.IsJumping, "Stalled retreat respects recent jump cooldown");
        Check((float)Get(jump, "_nextRetreatCheck") == 0f, "Cooldown avoids landing scans");
        Set(jump, "_nextCheck", 0f);
        Call(enemy, "TickRetreat", manager, .02f);
        Check(jump.IsJumping, "Stalled retreat escapes once cooldown expires");
        Check(!enemy.WantsPath, "Successful escape does not queue a graph search");
        float remaining = (float)Get(enemy, "_retreatRemaining");
        int landings = 0; jump.Landed += () => landings++;
        for (int i = 0; i < 200 && jump.IsJumping; i++) enemy.TickAuthority(manager, .02f, false);
        Check(!jump.IsJumping && landings == 1 && enemy.Root.position.y < .1f, "Lands off bus once");
        Check((bool)Get(enemy, "_retreatActive") && (float)Get(enemy, "_retreatRemaining") == remaining,
            "Retreat and its remaining duration survive the jump");
        Check(!jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.right), "Landing cooldown respected");
    }

    static void RejectedEscape(bool blocked)
    {
        Setup(out var enemy, out var jump, out var manager);
        if (blocked)
        {
            Box(new Vector3(0, 1.25f, 0), new Vector3(3, 2.5f, 10));
            Box(new Vector3(0, 3.8f, 0), new Vector3(30, .3f, 30));
            enemy.Teleport(Vector3.up * 2.5f, Quaternion.identity);
        }
        enemy.SetRemoteState(Enemy.State.Retreat); Physics.SyncTransforms();
        Check(!jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.right),
            blocked ? "Ceiling blocks unsafe escape" : "Flat ground does not cause an unnecessary jump");
        float deadline = (float)Get(jump, "_nextRetreatCheck");
        Check(deadline > Time.time, "Failed scan sets retry deadline");
        for (int i = 0; i < 100; i++)
            Check(!jump.TryStartRetreatJump(enemy, manager.SurfaceMask, Vector3.right), "Repeated ticks do not rescan");
        Check((float)Get(jump, "_nextRetreatCheck") == deadline, "Retry deadline is not renewed each frame");
    }

    static void FlareJump()
    {
        Setup(out var enemy, out var jump, out var manager);
        Box(new Vector3(11, 1.5f, 0), new Vector3(4, 3, 4));
        Set(enemy, "_lastSeenPos", new Vector3(11, 4.7f, 0));
        enemy.SetRemoteState(Enemy.State.Chase);
        Physics.SyncTransforms();
        var flare = new GameObject("Moving flare").AddComponent<DeathFlare>();
        try
        {
            Call(flare, "OnEnable");
            Call(flare, "Initialize", 0f);
            flare.transform.position = new Vector3(11, -2.5f, 0);
            Check(!enemy.IsFlareProtected(enemy.Root.position), "Takeoff is outside flare range");
            Check(!enemy.IsFlareProtected(enemy.LastSeenPos), "Target is visible above the flare sphere");
            Check(!(bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "Protected roof blocks chase jump");

            flare.transform.position += Vector3.up * 30f;
            jump.TickJump(enemy, manager, .02f);
            Set(jump, "_nextCheck", Time.time);
            Check(jump.TickJump(enemy, manager, .25f) && jump.IsJumping, "High flare permits a clear jump");
            var airborne = enemy.Root.position;
            flare.transform.position = airborne + Vector3.right * 2f;
            int landings = 0;
            jump.Landed += () => landings++;
            enemy.TickAuthority(manager, .02f, false);
            Check(enemy.CurrentState == Enemy.State.Retreat && jump.IsFalling, "Flare intercepts active chase arc");
            for (int i = 0; i < 200 && jump.IsJumping; i++) enemy.TickAuthority(manager, .02f, false);
            Check(!jump.IsJumping && landings == 1 && enemy.Root.position.y < .1f,
                "Smasher falls to the floor and lands once during flare retreat");
            Check(Mathf.Abs(enemy.Root.position.x - airborne.x) < .01f, "Fall stops travel toward flare");
            enemy.TickAuthority(manager, .02f, false);
            Check(enemy.CurrentState == Enemy.State.Retreat, "Grounded smasher continues flare retreat");
        }
        finally
        {
            Call(flare, "OnDisable");
            UnityEngine.Object.DestroyImmediate(flare.gameObject);
        }
    }

    static void FlareEscapeFromRoof()
    {
        Setup(out var enemy, out var jump, out var manager);
        Box(new Vector3(0, 1.25f, 0), new Vector3(3, 2.5f, 10));
        enemy.Teleport(Vector3.up * 2.5f, Quaternion.identity);
        Physics.SyncTransforms();
        var flare = new GameObject("Flare on roof").AddComponent<DeathFlare>();
        try
        {
            Call(flare, "OnEnable");
            Call(flare, "Initialize", 0f);
            flare.transform.position = enemy.Root.position + Vector3.left * 2f;
            for (int i = 0; i < 100 && !jump.IsJumping; i++) enemy.TickAuthority(manager, .02f, false);
            Check(jump.IsJumping, "Stranded smasher can jump away from roof flare");
            for (int i = 0; i < 200 && jump.IsJumping; i++)
            {
                var previous = enemy.Root.position;
                enemy.TickAuthority(manager, .02f, false);
                Check(enemy.AllowsFlareMovement(previous, enemy.Root.position), "Escape arc stays clear of flare");
            }
            Check(!jump.IsJumping && enemy.Root.position.y < .1f, "Flare escape lands off the roof");
        }
        finally
        {
            Call(flare, "OnDisable");
            UnityEngine.Object.DestroyImmediate(flare.gameObject);
        }
    }

    static void Box(Vector3 position, Vector3 size)
    {
        var go = new GameObject("Surface"); go.layer = 6; go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
    }
}
