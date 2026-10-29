using System;
using System.Reflection;
using Game.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch physics regressions for upward, level and downward traversal.</summary>
public static class ZombieSmasherTraversalChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        Traverse(0, 3, true, "ground to roof");
        Traverse(3, 0, true, "roof to ground");
        Traverse(3, 3, true, "equal rooftop gap");
        Traverse(3, 1.5f, true, "roof to vehicle");
        Traverse(1.5f, 3, true, "vehicle to roof");
        Traverse(3, 4.5f, true, "higher rooftop");
        Traverse(3, 0, true, "broad roof descent", 8f);
        Traverse(0, 0, false, "continuous ground");
        Traverse(0, 6, false, "height limit");
        Traverse(12, 0, false, "drop limit");
        Traverse(3, 3, false, "blocked gap", 3f, true);
        Debug.Log("ZombieSmasherTraversalChecks: ALL PASSED");
    }

    static void Traverse(float fromHeight, float toHeight, bool expected, string label,
        float takeoffWidth = 3f, bool blocked = false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
        if (fromHeight > 0) Box(new Vector3(0, fromHeight / 2, 0), new Vector3(takeoffWidth, fromHeight, 4));
        if (toHeight > 0) Box(new Vector3(6, toHeight / 2, 0), new Vector3(3, toHeight, 4));
        if (blocked) Box(new Vector3(3, 6, 0), new Vector3(0.5f, 12, 20));
        var actor = new GameObject("Smasher");
        actor.transform.position = new Vector3(0, fromHeight, 0);
        actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        var jump = actor.AddComponent<ZombieSmasherJump>();
        typeof(Enemy).GetMethod("Awake", Private).Invoke(enemy, null);
        typeof(ZombieSmasherJump).GetMethod("Awake", Private).Invoke(jump, null);
        enemy.SetRemoteState(Enemy.State.Chase);
        Set(enemy, "_lastSeenPos", new Vector3(6, toHeight + 1.7f, 0));
        var managerObject = new GameObject("Manager");
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Physics.SyncTransforms();
        int landings = 0;
        jump.Landed += () => landings++;
        Check(!jump.TickJump(enemy, manager, 0.02f), label + ": initial planning must wait for its phase");
        // This synchronous fixture does not advance Unity's clock. Expire the
        // armed deadline; cadence/latency are tested with simulated frame times.
        Set(jump, "_nextCheck", Time.time);
        bool started = jump.TickJump(enemy, manager, 0.02f);
        Check(started == expected, label + ": unexpected jump decision");
        if (!expected) return;
        for (int i = 0; i < 200 && jump.IsJumping; i++)
        {
            jump.TickJump(enemy, manager, 0.02f);
            Check(actor.transform.position.y <= fromHeight + 5.01f, label + ": apex exceeds limit");
        }
        Check(!jump.IsJumping && landings == 1, label + ": must land exactly once");
        Check(Mathf.Abs(actor.transform.position.y - toHeight) < 0.1f && actor.transform.position.x > 4.4f,
            label + ": did not reach destination surface");
        Debug.Log("Traversal passed: " + label);
    }

    static void Box(Vector3 position, Vector3 size)
    {
        var box = new GameObject("Support");
        box.layer = 6;
        box.transform.position = position;
        box.AddComponent<BoxCollider>().size = size;
    }
}
