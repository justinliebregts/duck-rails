using System;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Isolated batch regression using the ActualCity.prefab collider fixture exported from Main.</summary>
public static class DuckSubwayHomeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(DuckCompanion duck, string name, params object[] args)
        => typeof(DuckCompanion).GetMethod(name, Private).Invoke(duck, args);
    static void Set(DuckCompanion duck, string name, object value)
        => typeof(DuckCompanion).GetField(name, Private).SetValue(duck, value);
    static object Get(DuckCompanion duck, string name)
        => typeof(DuckCompanion).GetField(name, Private).GetValue(duck);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Debug.Log("PASS: " + message);
    }

    public static void Run()
    {
        Check(Application.isBatchMode, "isolated batch editor");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ActualCity.prefab");
        Check(prefab != null, "actual Main collider fixture available");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(prefab);
        Physics.SyncTransforms();
        var go = new GameObject("Subway home probe");
        go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>();
        Call(duck, "Awake");
        Set(duck, "environment", (LayerMask)(1 << 6));
        Set(duck, "homePosition", new Vector3(50.332f, -5.848f, 7.005f));
        Set(duck, "homeFloorY", -6.8f);
        Set(duck, "homeFloorResolved", true);
        var motor = go.GetComponent<CharacterController>();
        foreach (string entrance in new[] { "Subway_West_Entrance", "Subway_East_Entrance" })
        {
            var route = (Vector3[])Call(duck, "BuildSubwayStairs", entrance);
            Check(route.Length > 0, entrance + " has a validated stair route");
            motor.enabled = false;
            go.transform.position = route[0];
            motor.enabled = true;
            int corner = 1;
            // Exercise real capsule collisions and gravity along the validated
            // route, without relying on zero deltaTime in a batch editor.
            for (int tick = 0; tick < 10000 && corner < route.Length; tick++)
            {
                while (corner < route.Length && Vector3.Distance(go.transform.position, route[corner]) < 0.35f) corner++;
                if (corner == route.Length) break;
                Vector3 delta = Vector3.ProjectOnPlane(route[corner] - go.transform.position, Vector3.up);
                motor.Move(delta.normalized * 0.05f + Vector3.down * 0.06f);
            }
            Check(corner == route.Length, entrance + " capsule descends every flight to the underground exit");
            motor.enabled = false;
            go.transform.position = route[route.Length - 1];
            Call(duck, "ResetNavigationAfterAuthorityChange");
            motor.enabled = true;
            for (int tick = 0; tick < 15000 && Vector3.Distance(go.transform.position, route[0]) > 0.4f; tick++)
            {
                if (!(bool)Call(duck, "WalkSubwayStairs", route[0], 1f / 60f))
                {
                    Check((bool)Call(duck, "ClearBodySweep", go.transform.position, route[0]),
                        "stair handoff has a clear onward path");
                    motor.Move(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(route[0] - go.transform.position,
                        Vector3.up), 0.07f) + Vector3.down * 0.04f);
                }
            }
            Check(Vector3.Distance(go.transform.position, route[0]) < 0.4f,
                entrance + " production walker ascends all flights: " + go.transform.position);
            motor.enabled = false;
            go.transform.position = route[route.Length / 2];
            Call(duck, "ResetNavigationAfterAuthorityChange");
            Check((bool)Call(duck, "WalkSubwayStairs", route[0], 1f / 60f),
                entrance + " uphill walker rejoins after authority change");
            Call(duck, "ResetNavigationAfterAuthorityChange");
            object[] destination = { Vector3.zero };
            Check((bool)Call(duck, "TryHomeStairDestination", destination)
                && (int)Get(duck, "returnStairCorner") > 2, entrance + " handoff rejoins the current stair level");
        }
        go.transform.position = new Vector3(50.332f, 0.9f, 7.005f);
        Call(duck, "ResetNavigationAfterAuthorityChange");
        object[] target = { Vector3.zero };
        Check((bool)Call(duck, "TryHomeStairDestination", target)
            && (int)Get(duck, "returnEntrance") == 0, "street directly above home routes to west entrance");
        Check(Vector3.Distance((Vector3)target[0], go.transform.position) > 10,
            "return route can move much farther away before descending");
        Set(duck, "returnStairProgressAt", Time.time - 9f);
        Check(!(bool)Call(duck, "TryHomeStairDestination", target), "stalled entrance releases to existing recovery");
        Check((bool)Call(duck, "TryHomeStairDestination", target)
            && (int)Get(duck, "returnEntrance") == 1, "blocked west entrance selects east alternative");
        go.transform.position = new Vector3(73, 0.9f, 62);
        Call(duck, "ResetNavigationAfterAuthorityChange");
        Check((bool)Call(duck, "TryHomeStairDestination", target)
            && (int)Get(duck, "returnEntrance") == 1, "near east entrance selects east stairs");
        go.transform.position = new Vector3(56.5f, -6.8f, -13);
        Call(duck, "ResetNavigationAfterAuthorityChange");
        Check(!(bool)Call(duck, "TryHomeStairDestination", target), "underground exit resumes ordinary home navigation");
        Debug.Log("ALL SUBWAY HOME CHECKS PASSED");
    }
}
