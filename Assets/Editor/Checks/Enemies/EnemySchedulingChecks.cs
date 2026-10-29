using System;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch checks using the production brain, movement and network handlers.</summary>
public static class EnemySchedulingChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    static object Get(object o, string n) => o.GetType().GetField(n, Private).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Private).Invoke(o, args);
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Debug.Log("PASS: " + label); }
    static bool Perceive(Enemy e, EnemyManager m, int frame = 0) => (bool)Call(e, "ShouldPerceive", m, frame, 0, 8);

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var managerObject = new GameObject("Scheduling manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>(); Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        var actor = new GameObject("Scheduling enemy");
        var mover = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>(); Call(enemy, "Awake"); enemy.ResetBrain();
        Set(enemy, "_hadAuthority", true);
        var target = new GameObject("Living target");
        Call(manager, "UpdateSlot", target.transform, Vector3.right * 30);
        Perceive(enemy, manager);
        Check((float)Get(enemy, "_farPerceptionInterval") == 0.2f, "30m idle perception uses 0.2s interval");
        Set(enemy, "_nextFarPerception", Time.time + 1);
        Check(!Perceive(enemy, manager), "distant idle check waits for its own deadline");
        Set(enemy, "_nextFarPerception", 0f);
        Check(Perceive(enemy, manager), "due distant check runs");
        Call(manager, "UpdateSlot", target.transform, Vector3.right * 60); Perceive(enemy, manager);
        Check((float)Get(enemy, "_farPerceptionInterval") == 0.5f, "60m idle perception uses 0.5s interval");
        foreach (Enemy.State state in new[] { Enemy.State.Chase, Enemy.State.Search, Enemy.State.Retreat })
        {
            enemy.SetRemoteState(state); Check(Perceive(enemy, manager), state + " keeps normal perception cadence");
        }
        enemy.SetRemoteState(Enemy.State.Wander); actor.transform.rotation = Quaternion.Euler(90, 0, 0);
        Check(Perceive(enemy, manager), "wall-climbing orientation bypasses distant throttle");
        actor.transform.rotation = Quaternion.identity;
        Call(manager, "UpdateSlot", target.transform, Vector3.right * 24);
        Check(Perceive(enemy, manager) && !Perceive(enemy, manager, 1), "nearby checks retain existing frame staggering");
        // A second nearby target must override a distant followed target.
        Call(manager, "UpdateSlot", target.transform, Vector3.right * 60);
        var buddy = new GameObject("Duck or other player"); Call(manager, "UpdateSlot", buddy.transform, Vector3.forward * 10);
        Check(Perceive(enemy, manager), "nearest living target controls scheduling");

        var probe = actor.AddComponent<EnemySchedulingProbe>(); Set(enemy, "_loco", probe);
        Set(enemy, "_target", 0); Set(enemy, "_pathRequestTarget", 0);
        Set(enemy, "_hasPathRequestGoal", true); Set(enemy, "_pathRequestGoal", Vector3.zero);
        Set(enemy, "_lastSeenPos", Vector3.zero); Set(enemy, "_stablePathRefresh", Time.time + 10);
        Check(!(bool)Call(enemy, "ShouldRefreshChasePath"), "stationary target retains usable path");
        probe.done = true; Check((bool)Call(enemy, "ShouldRefreshChasePath"), "finished or invalidated path refreshes"); probe.done = false;
        Set(enemy, "_lastSeenPos", Vector3.right * 0.6f); Check((bool)Call(enemy, "ShouldRefreshChasePath"), "moved target refreshes");
        Set(enemy, "_lastSeenPos", Vector3.zero); Set(enemy, "_target", 1); Check((bool)Call(enemy, "ShouldRefreshChasePath"), "target switch refreshes");
        Set(enemy, "_target", 0); Set(enemy, "_stablePathRefresh", 0f); Check((bool)Call(enemy, "ShouldRefreshChasePath"), "periodic refresh remains active");
        probe.pending = true; Check(!(bool)Call(enemy, "ShouldRefreshChasePath"), "pending path is not repeatedly overwritten"); probe.pending = false;
        enemy.ResetBrain(); Set(enemy, "_hadAuthority", true);
        for (int i = 0; i < 20; i++) enemy.TickAuthority(manager, 0.02f, false);
        Check(probe.moves == 20, "movement ticks every frame even without perception");

        Set(enemy, "_loco", mover); enemy.ResetBrain(); Set(enemy, "_hadAuthority", true);
        var floor = new GameObject("Floor"); floor.layer = 6; floor.transform.position = Vector3.down * 0.5f;
        floor.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); Physics.SyncTransforms();
        enemy.ReceiveSwing(Vector3.right * 5, 1f, actor.transform.position);
        Check((bool)Get(enemy, "_shoveActive") || enemy.IsStunned || enemy.IsKnocked, "local swing reacts immediately");
        Vector3 beforeHitMove = actor.transform.position;
        for (int i = 0; i < 10; i++) enemy.TickAuthority(manager, 0.02f, false);
        Check(Vector3.Distance(beforeHitMove, actor.transform.position) > 0.01f, "local hit motion continues without perception updates");
        enemy.ResetBrain(); Set(enemy, "_hadAuthority", true);
        var net = actor.AddComponent<EnemyNetSync>(); Call(net, "Awake"); Call(enemy, "Awake");
        Set(enemy, "_nextHitTime", 0f); Call(net, "RpcThrownHit", Vector3.right * 5, actor.transform.position);
        Check((bool)Get(enemy, "_shoveActive") || enemy.IsStunned || enemy.IsKnocked, "network thrown-hit handler reacts immediately");
        enemy.ResetBrain(); Set(enemy, "_hadAuthority", true);
        Set(enemy, "_nextHitTime", 0f); Call(net, "RpcSwing", Vector3.right * 5, 1f, actor.transform.position);
        Check((bool)Get(enemy, "_shoveActive") || enemy.IsStunned || enemy.IsKnocked, "network swing handler reacts immediately");
        enemy.ResetBrain();
        var packet = new EnemyPosePacket(); packet.Set(Vector3.right * 10, Quaternion.identity, (byte)Enemy.State.Chase, true, 20, 5);
        net.OnPhotonSerializeView(new PhotonStream(false, new object[] { packet }), default);
        enemy.ApplyRemote(0.1f);
        Check(Vector3.Distance(actor.transform.position, Vector3.right * 10) < 0.01f, "remote pose still applies while local AI is inactive");
        Set(enemy, "_nextFarPerception", Time.time + 100); Set(enemy, "_farPerceptionInterval", 0.5f); Set(enemy, "_hasPathRequestGoal", true);
        Check(Perceive(enemy, manager), "new authority receives an immediate perception opportunity");
        enemy.TickAuthority(manager, 0.02f, false);
        Check((float)Get(enemy, "_nextFarPerception") == 0f && !(bool)Get(enemy, "_hasPathRequestGoal"), "remote-to-authority transition clears old scheduling and path cache");
        Debug.Log("EnemySchedulingChecks: ALL PASSED");
    }
}

public sealed class EnemySchedulingProbe : MonoBehaviour, IEnemyLocomotion
{
    public bool done, pending; public int moves;
    public bool PathDone => done; public bool WantsPath => pending;
    public void Reset() { } public void ClearPath() { } public void BeginWanderPause() { }
    public bool TickWanderPause(float dt) => false;
    public void RequestWander(EnemyManager m) { }
    public void RequestPathToWorld(EnemyManager m, Vector3 p) { }
    public void ForcePathToWorld(EnemyManager m, Vector3 p) { }
    public void RequestRetreat(EnemyManager m, Vector3 d, float distance) { }
    public void RunPathRequest(EnemyManager m) { }
    public void TickMove(Enemy enemy, EnemyManager manager, float dt) { moves++; }
    public void Interrupt() { }
}

