using System;
using System.Reflection;
using Game.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MeleeGraceChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    sealed class Controller : Photon.Realtime.Player
    {
        public Controller(bool local) : base("Controller check", local ? 1 : 2, local) { }
    }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("PASS: " + message);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in batch mode.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var actor = new GameObject("Enemy"); actor.SetActive(false);
        var enemy = actor.AddComponent<Enemy>();
        var otherActor = new GameObject("Other enemy");
        var otherEnemy = otherActor.AddComponent<Enemy>();
        var trigger = actor.AddComponent<KillTrigger>();
        // Keep Enemy.Awake and movement out of this contact test.
        actor.SetActive(true);
        Set(enemy, "root", null);
        var playerObject = new GameObject("Local player"); playerObject.SetActive(false);
        var player = playerObject.AddComponent<GR.Player>();
        GR.Player.instance = player;
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody>().isKinematic = true;
        var contact = playerObject.AddComponent<BoxCollider>();
        // Register the collider/body before exercising real contact classification.
        playerObject.SetActive(true);
        Physics.SyncTransforms();

        enemy.SubmitSwing(Vector3.right * enemy.MinHitSpeed, 1);
        Check(!enemy.ProtectsLocalPlayer, "threshold-only swing grants no protection");
        enemy.ReceiveSwing(Vector3.right * 9, 1);
        Check(!enemy.ProtectsLocalPlayer, "received network hit grants no local protection");
        enemy.SubmitSwing(Vector3.right * 9, 1);
        Check(!enemy.ProtectsLocalPlayer, "cooldown-rejected swing grants no protection");
        Set(enemy, "_nextHitTime", float.NegativeInfinity);
        enemy.SubmitSwing(Vector3.right * 9, 1);
        Check(enemy.ProtectsLocalPlayer && !otherEnemy.ProtectsLocalPlayer, "valid swing protects only its target");
        Check(!trigger.TryContact(contact) && !player.IsDead, "local contact during grace does not kill");
        Set(enemy, "_localMeleeGraceUntil", Time.time - 1);
        Check(!enemy.ProtectsLocalPlayer, "grace has a firm expiry");
        int retries = 0;
        trigger.ContactAllowed = collider => { retries++; return false; };
        Call(trigger, "OnTriggerStay", contact);
        Check(retries == 0, "enemy contact waits until weapon detection has finished");
        Call(trigger, "LateUpdate");
        Check(retries == 1, "deferred overlap is retried without another entry");
        Call(trigger, "OnTriggerExit", contact);
        Call(trigger, "OnTriggerStay", contact);
        Call(trigger, "LateUpdate");
        Check(retries == 1, "exited contact is no longer retried");
        Set(enemy, "_nextHitTime", float.NegativeInfinity);
        Set(enemy, "_localMeleeGraceUntil", 0f);
        enemy.SubmitThrownHit(Vector3.right * enemy.MinHitSpeed);
        Check(!enemy.ProtectsLocalPlayer, "threshold-only throw grants no protection");
        enemy.ReceiveRemoteHitEffect(Vector3.right * 9, default);
        enemy.SubmitThrownHit(Vector3.right * 9);
        Check(enemy.ProtectsLocalPlayer, "throw protects hitter and remote feedback does not consume local cooldown");
        Set(enemy, "_localMeleeGraceUntil", 0f);
        enemy.SubmitThrownHit(Vector3.right * 9);
        Check(!enemy.ProtectsLocalPlayer, "cooldown-rejected throw cannot renew protection");
        Vector3 direction;
        float distance = enemy.LocalPushDistance(Vector3.right * 9, out direction);
        Check(Mathf.Approximately(distance, 3f) && direction == Vector3.right, "local full-strength reaction matches authority shove distance");
        enemy.LocalPushDistance(Vector3.up * 9, out direction);
        Check(direction == Vector3.zero, "vertical hit cannot invent a horizontal shove");
        var sync = actor.AddComponent<EnemyNetSync>();
        // This fixture runs in Edit Mode; MonoBehaviour.Awake is not automatic.
        Call(sync, "Awake");
        Set(sync, "_pendingHit", 2);
        Set(sync, "_predictionUntil", Time.time + 2f);
        Set(sync, "_protectUntil", Time.time + 2f);
        Set(sync, "_hasSample", true);
        Set(sync, "_sampleTime", 100);
        var tickPrediction = typeof(EnemyNetSync).GetMethod("TickLocalPrediction", Private);
        bool Hold() => (bool)tickPrediction.Invoke(sync, new object[] { actor.transform, 0f });
        Check(Hold(), "unconfirmed hit holds off stale movement");
        var view = actor.GetComponent<Photon.Pun.PhotonView>();
        var confirmation = new Photon.Pun.PhotonMessageInfo(view.Controller, 200, view);
        Call(sync, "RpcHitConfirmed", 1, Vector3.zero, Quaternion.identity, confirmation);
        Check(!(bool)typeof(EnemyNetSync).GetField("_hitConfirmed", Private).GetValue(sync), "previous hit acknowledgment cannot confirm repeated hit");
        Call(sync, "RpcHitConfirmed", 2, Vector3.right * 10, Quaternion.identity, confirmation);
        Check(!Hold() && sync.ProtectsLocalHitter, "confirmation snapshot releases stationary zombie without waiting for another pose");
        Set(enemy, "root", actor.transform);
        Set(sync, "_protectUntil", Time.time - 1f);
        Call(sync, "ApplySample", actor.transform, 0.01f);
        Check(actor.transform.position.x > 0f && actor.transform.position.x <= 0.08001f,
            "large handoff correction respects 8 m/s cap");
        Check(sync.ProtectsLocalHitter, "handoff protection survives timer expiry while position catches up");
        for (int i = 0; i < 180; i++) Call(sync, "ApplySample", actor.transform, 1f / 60f);
        Check(Vector3.Distance(actor.transform.position, Vector3.right * 10) < 0.01f,
            "handoff converges to master instead of accumulating drift");
        Set(sync, "_protectUntil", Time.time - 1f);
        Check(!sync.ProtectsLocalHitter, "settled handoff eventually restores contact damage");
        foreach (int fps in new[] { 30, 60, 90 })
        {
            actor.transform.position = Vector3.zero;
            Set(sync, "_reconciling", true);
            float target = 10f;
            bool caught = false;
            float maxSpeed = 0f;
            for (int frame = 0; frame < fps * 10; frame++)
            {
                target += 6.5f / fps;
                Set(sync, "_netPos", Vector3.right * target);
                Vector3 before = actor.transform.position;
                Call(sync, "ApplySample", actor.transform, 1f / fps);
                maxSpeed = Mathf.Max(maxSpeed, Vector3.Distance(before, actor.transform.position) * fps);
                if (!(bool)typeof(EnemyNetSync).GetField("_reconciling", Private).GetValue(sync))
                { caught = true; break; }
            }
            Check(caught && maxSpeed <= 8.001f, "bounded correction catches 6.5 m/s target at " + fps + " fps");
        }
        Set(sync, "_pendingHit", 4);
        Set(sync, "_reconciling", false);
        Set(sync, "_netPos", actor.transform.position + Vector3.right * 20);
        Vector3 timeoutStart = actor.transform.position;
        Call(sync, "ApplySample", actor.transform, 0.02f);
        Check(Vector3.Distance(timeoutStart, actor.transform.position) <= 0.16001f,
            "timed-out pending hit correction also respects the cap");
        Set(enemy, "root", null);
        Set(sync, "_pendingHit", 3);
        Set(sync, "_hitConfirmed", false);
        Set(sync, "_predictionUntil", Time.time - 1f);
        Check(!Hold(), "missing confirmation times out instead of freezing forever");
        Check(sync.ProtectsLocalHitter, "timeout retains protection against stale contact");
        Set(sync, "_sampleTime", 300);
        Set(sync, "_netPos", Vector3.right * 20);
        Call(sync, "RpcHitConfirmed", 3, Vector3.zero, Quaternion.identity, confirmation);
        Check(!Hold(), "late confirmation is still accepted after timeout");
        Check((Vector3)typeof(EnemyNetSync).GetField("_netPos", Private).GetValue(sync) == Vector3.right * 20,
            "late confirmation cannot overwrite a newer movement sample");
        sync.ResetRemotePose(Vector3.zero, Quaternion.identity);
        Check(!sync.ProtectsLocalHitter, "respawn/reset clears pending protection");
        Set(sync, "_pendingHit", 9);
        Set(sync, "_reconciling", true);
        Set(sync, "_pushRemaining", 3f);
        sync.OnControllerChange(new Controller(false), null);
        Check((int)typeof(EnemyNetSync).GetField("_pendingHit", Private).GetValue(sync) == 9,
            "remote master change does not revoke client hit protection");
        sync.OnControllerChange(new Controller(true), null);
        Check((int)typeof(EnemyNetSync).GetField("_pendingHit", Private).GetValue(sync) == 0
            && !(bool)typeof(EnemyNetSync).GetField("_reconciling", Private).GetValue(sync)
            && (float)typeof(EnemyNetSync).GetField("_pushRemaining", Private).GetValue(sync) == 0f,
            "local controller callback clears prediction without serialization or Apply");
        Check(sync.ProtectsLocalHitter, "authority handoff preserves short contact grace");
        Set(sync, "_protectUntil", Time.time - 1f);
        Check(!sync.ProtectsLocalHitter, "sole master protection expires without another network packet");
        Set(enemy, "_nextHitTime", float.NegativeInfinity);
        enemy.SubmitSwing(Vector3.right * 9, 1);
        Call(enemy, "OnDisable");
        Check(!enemy.ProtectsLocalPlayer, "disable clears protection");
        GR.Player.instance = null;
        UnityEngine.Object.DestroyImmediate(playerObject);
        UnityEngine.Object.DestroyImmediate(actor);
        UnityEngine.Object.DestroyImmediate(otherActor);
        Debug.Log("MeleeGraceChecks passed.");
    }
}
