using System;
using Game.AI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class SpiderRaiseChecks
{
    const string PlayKey = "SpiderRaiseChecks.Play";
    static SpiderRaiseChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false)) PlayCases();
        };
    }

    public static void RunPlay()
    {
        Setup();
        SessionState.SetBool(PlayKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void PlayCases()
    {
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            manager = Object.FindFirstObjectByType<EnemyManager>(FindObjectsInactive.Include);
            typeof(EnemyManager).GetProperty("Instance").SetValue(null, manager);
            targetObject = new GameObject("Play elevated target");
            ((Object[])Get(manager, "_slotOwner"))[0] = targetObject;
            Target(new Vector3(0, 1.5f, .8f));
            using var r = new Rig();
            var player = new GameObject("Play elevated player") { tag = "Player", layer = LayerMask.NameToLayer("Player") };
            var rb = player.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var capsule = player.AddComponent<CapsuleCollider>(); capsule.radius = .15f; capsule.height = .5f;
            player.transform.position = new Vector3(0, 1.275f, .74f);
            var trigger = r.kill.GetComponent<KillTrigger>();
            int contacts = 0;
            trigger.ContactAllowed = collider => { if (collider == capsule) contacts++; return false; };
            void Step()
            {
                Physics.SyncTransforms(); Physics.Simulate(.02f); Call(trigger, "LateUpdate");
            }
            Set(r.gait, "raiseNearTarget", false); r.Settle(); Step();
            Check(contacts == 0, "Low spider must not contact the elevated player");
            Set(r.gait, "raiseNearTarget", true);
            for (int frame = 0; frame < 45; frame++) { r.Tick(1f / 90); Step(); }
            Check(contacts > 0, "Raised head must generate a real physics kill-trigger contact");
            Check(r.gait.RaiseHeight > .3f, "Play-mode rise height");
            r.actor.SetActive(false); Check(r.gait.RaiseHeight == 0 && r.gait.RaiseLevel == 0, "Actual OnDisable resets stance");
            r.actor.SetActive(true); r.enemy.SetRemoteState(Enemy.State.Chase); Set(r.enemy, "_target", 0); r.Settle();
            Check(r.gait.RaiseHeight > .3f, "Re-enabled spider can rise again");
            Object.DestroyImmediate(player);
            Debug.Log($"SpiderRaiseChecks PLAY PASS: {checks} assertions; real physics contact after rising, ordinary low miss, actual disable/re-enable lifecycle.");
            SessionState.SetBool(PlayKey, false); EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex); SessionState.SetBool(PlayKey, false); EditorApplication.Exit(1);
        }
    }
}
