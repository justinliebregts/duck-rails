using System;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Isolated batch physics regression. Requires ActualCity.prefab exported from Main
/// with its world transform and original mesh colliders. Never run in an unsaved scene.
/// </summary>
public static class DuckSubwayTraversalChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(DuckCompanion duck, string method, params object[] args)
        => typeof(DuckCompanion).GetMethod(method, Private).Invoke(duck, args);
    static void Set(DuckCompanion duck, string field, object value)
        => typeof(DuckCompanion).GetField(field, Private).SetValue(duck, value);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run()
    {
        Check(Application.isBatchMode, "Run this fixture in an isolated batch editor.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ActualCity.prefab");
        Check(prefab != null, "Export the actual Main city collider fixture first.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(prefab);
        Physics.SyncTransforms();
        var go = new GameObject("Subway traversal probe");
        go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>();
        Call(duck, "Awake");
        Set(duck, "environment", (LayerMask)(1 << 6));
        var start = new Vector3(51.3f, -7.58f, 4.5f);
        var landing = new Vector3(52.4f, -6.8f, 4.5f);
        duck.transform.position = start;
        Set(duck, "traversalProbeOrigin", (Vector3?)start);
        Check(!(bool)Call(duck, "BeginTraversal", landing, null, true)
            && !(bool)Call(duck, "BeginTraversal", landing, null, false),
            "Train corner must block the direct climb/jump rather than allowing clipping.");
        Set(duck, "traversalProbeOrigin", null);
        foreach (var goal in new[] { new Vector3(54, -6.8f, 8), new Vector3(54, -6.8f, 30) })
        {
            Set(duck, "nextObstacleSearch", 0f);
            Check((bool)Call(duck, "TryObstacleRoute", goal), "No alternate launch route.");
            var route = (Vector3[])typeof(DuckCompanion).GetField("obstacleRoute", Private).GetValue(duck);
            Check(route.Length > 0, "Empty detour.");
            Check(Vector3.Distance(route[0], goal) > Vector3.Distance(start, goal),
                "Expected a detour that initially moves away from the player.");
            Vector3 previous = start;
            foreach (var point in route)
            {
                Check((bool)Call(duck, "ClearBodySweep", previous, point), "Blocked detour segment.");
                previous = point;
            }
            Set(duck, "traversalProbeOrigin", (Vector3?)previous);
            Set(duck, "traversalProbeMinimumHeight", (float?)(previous.y + 0.35f));
            Check((bool)Call(duck, "TryTraversal", Vector3.ProjectOnPlane(goal - previous, Vector3.up).normalized, goal),
                "Detour endpoint cannot climb or jump onto the platform.");
            Set(duck, "traversalProbeOrigin", null);
            Set(duck, "traversalProbeMinimumHeight", null);
            Debug.Log($"SUBWAY PASS: goal={goal}, launch={previous}, corners={route.Length}");
        }
        Debug.Log("ALL SUBWAY TRAVERSAL CHECKS PASSED");
    }
}
