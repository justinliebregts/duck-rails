using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEngine;

public static class EnemyRetreatChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        foreach (string name in new[] { "Spider", "Smasher", "Minion", "Bomber" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Zombie{name}.prefab");
            var configured = prefab.GetComponent<Enemy>();
            Check((float)Get(configured, "retreatTime") == 4f && (float)Get(configured, "retreatTimeMax") == 10f, name + " retreat range");
            Check((bool)Get(configured, "retreatAfterKill") == (name == "Spider" || name == "Smasher"), name + " kill retreat setting");
        }
        var actor = new GameObject("Retreat check");
        var managerObject = new GameObject("Inactive manager");
        managerObject.SetActive(false);
        try
        {
            actor.AddComponent<GroundLocomotion>();
            var enemy = actor.AddComponent<Enemy>();
            Call(enemy, "Awake");
            var manager = managerObject.AddComponent<EnemyManager>();
            var kill = new Vector3(8, 0, 12);
            enemy.ReceivePlayerKill(kill);
            Check(!(bool)Get(enemy, "_killRetreatPending"), "Disabled enemies must ignore kill retreat");
            Set(enemy, "retreatAfterKill", true);
            enemy.ReceivePlayerKill(kill);
            Check(enemy.CurrentState == Enemy.State.Retreat, "Kill immediately changes state");
            Check(!enemy.ProtectsLocalPlayer, "Kill retreat must not grant melee grace");
            for (int i = 0; i < 100; i++)
            {
                Call(enemy, "BeginRetreat", null, Vector3.back);
                float duration = (float)Get(enemy, "_retreatRemaining");
                Check(duration >= 4 && duration <= 10, "Sample outside retreat range");
            }
            Call(enemy, "FinishRetreat", manager);
            Check(enemy.CurrentState == Enemy.State.Search && enemy.LastSeenPos == kill, "Search must use kill position");
            Call(enemy, "Think", manager, 0.1f);
            Check(enemy.CurrentState == Enemy.State.Search, "Search must survive missing dead target");
            Call(enemy, "Think", manager, 11f);
            Check(enemy.CurrentState == Enemy.State.Wander, "Search must time out");
            var nextKill = new Vector3(-5, 0, 2);
            enemy.ReceivePlayerKill(nextKill);
            Call(enemy, "BeginRetreat", null, Vector3.forward);
            Call(enemy, "FinishRetreat", manager);
            Check(enemy.LastSeenPos == nextKill, "Next kill must replace search location");
            enemy.ResetBrain();
            Check(!(bool)Get(enemy, "_searchingKillLocation") && !(bool)Get(enemy, "_killRetreatPending"), "Reset must clear kill state");
            Debug.Log("EnemyRetreatChecks PASSED");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }
}
