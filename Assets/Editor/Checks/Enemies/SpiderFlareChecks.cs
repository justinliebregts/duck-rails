using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEngine;

public static class SpiderFlareChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    [MenuItem("Tools/DuckZ/Validation/Effects/Spider Flare Fear")]
    public static void Run()
    {
        var actor = new GameObject("Spider flare check");
        var flareObject = new GameObject("Test flare");
        var managerObject = new GameObject("Inactive manager");
        managerObject.SetActive(false);
        try
        {
            actor.AddComponent<ClimbLocomotion>();
            var enemy = actor.AddComponent<Enemy>();
            Call(enemy, "Awake");
            Set(enemy, "flareFearRadius", 6f);
            var manager = managerObject.AddComponent<EnemyManager>();
            var flare = flareObject.AddComponent<DeathFlare>();
            Call(flare, "Initialize", -2f);
            Check(!enemy.IsFlareProtected(Vector3.zero), "Unlaunched flare must not protect");
            Call(flare, "Initialize", 0f);
            flareObject.transform.position = new Vector3(0, 24, 0);
            Check(!enemy.IsFlareProtected(Vector3.zero), "High flare must not protect ground");
            flareObject.transform.position = new Vector3(0, 2, 0);
            Check(enemy.IsFlareProtected(Vector3.zero), "Low active flare must protect");
            Check(!enemy.AllowsFlareMovement(new Vector3(-8, 2, 0), new Vector3(8, 2, 0)), "Movement cannot cross a flare sphere");
            Check(enemy.AllowsFlareMovement(Vector3.zero, Vector3.back), "Escape movement is allowed");
            var remembered = new Vector3(12, 0, 0);
            Set(enemy, "_lastSeenPos", remembered);
            Check((bool)Call(enemy, "TickFlareFear", manager, 0.1f), "Nearby flare must interrupt AI");
            Check(enemy.CurrentState == Enemy.State.Retreat && !enemy.ProtectsLocalPlayer, "Fear retreats without contact protection");
            Vector3 destination = (Vector3)Get(enemy, "_flareDestination");
            float retreatDistance = Vector3.Distance(destination, flareObject.transform.position);
            Check(retreatDistance >= 9.999f && retreatDistance <= 14.001f, "Destination must be 10–14 metres from flare");
            actor.transform.position = new Vector3(8, 0, 0);
            Check((bool)Call(enemy, "TickFlareFear", manager, 0.1f), "Retreat must continue beyond the 6-metre boundary");
            Check((Vector3)Get(enemy, "_flareDestination") == destination, "Retreat destination must stay stable");
            actor.transform.position = destination;
            Check(!(bool)Call(enemy, "TickFlareFear", manager, 0.1f), "Arrival must end fear while flare burns");
            Check(enemy.CurrentState == Enemy.State.Search && enemy.LastSeenPos == remembered, "Fear must preserve search destination");
            Call(enemy, "Think", manager, 0.1f);
            Check(enemy.CurrentState == Enemy.State.Search, "Search must work without a living target");
            actor.transform.position = Vector3.zero;
            Call(enemy, "TickFlareFear", manager, 0.1f);
            Set(enemy, "_lastSeenPos", flareObject.transform.position);
            actor.transform.position = (Vector3)Get(enemy, "_flareDestination");
            Call(enemy, "TickFlareFear", manager, 0.1f);
            Check(enemy.LastSeenPos == actor.transform.position, "Search must not return to a protected remembered position");
            actor.transform.position = Vector3.zero;
            Call(enemy, "TickFlareFear", manager, 0.1f);
            Call(flare, "Initialize", DeathFlare.Lifetime + 1f);
            Check(!(bool)Call(enemy, "TickFlareFear", manager, 0.1f), "Expired flare must immediately end fear");
            Check(enemy.CurrentState == Enemy.State.Search, "Expiry must transition to search");
            Call(flare, "Initialize", 0f);
            flareObject.SetActive(false);
            Check(!enemy.IsFlareProtected(flareObject.transform.position), "Disabled flare must stop protecting");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab");
            Check(new SerializedObject(prefab.GetComponent<Enemy>()).FindProperty("flareFearRadius").floatValue == 6f, "Spider prefab radius");
            EnemyRetreatChecks.Run();
            Debug.Log("SpiderFlareChecks PASSED");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(flareObject);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }
}
