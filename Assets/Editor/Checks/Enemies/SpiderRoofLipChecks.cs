using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run with Unity -batchmode -executeMethod SpiderRoofLipChecks.Run -quit.</summary>
public static class SpiderRoofLipChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<UnityEngine.Object> Spawned = new List<UnityEngine.Object>();

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Spider roof lip check failed: " + message);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string report = Evaluate(Vector3.zero);
        if (report != "ok") throw new Exception(report);
        Debug.Log("SpiderRoofLipChecks: ALL PASSED");
    }

    /// <summary>Runs the lip cases around a building centered on origin. Caller keeps the current scene.</summary>
    public static string Evaluate(Vector3 origin)
    {
        try
        {
            Mount(origin, new Vector3(2f, 3.2f, 0f), new Vector3(1.2f, 4f, 0f), "inset roof");
            Mount(origin, new Vector3(2f, 2.2f, -1.4f), new Vector3(0.8f, 4f, 1.2f), "offset roof");
            DescendWall(origin);
            StepOffLip(origin, 0.08f, true);
            StepOffLip(origin, 2f, false);
            return "ok";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            Cleanup();
        }
    }

    static void Mount(Vector3 origin, Vector3 start, Vector3 roof, string label)
    {
        ClimbLocomotion climb = Building(origin);
        Set(climb, "_surfaceUp", Vector3.right);
        Set(climb, "_edgeNormal", Vector3.right);
        Set(climb, "_edgeDirection", Vector3.forward);
        Vector3 pos = origin + start;
        LayerMask mask = 1 << 6;
        for (int i = 0; i < 80; i++)
            pos = (Vector3)Call(climb, "MoveOnSurface", pos, origin + roof, Vector3.up, 0.1f, mask);

        Vector3 normal = (Vector3)Get(climb, "_surfaceUp");
        Vector3 local = pos - origin;
        string where = label + " normal " + normal.ToString("F2") + " pos " + local.ToString("F2");
        Check(normal.y >= CrawlGraph.FloorMinY, where);
        Check(pos.y - origin.y > 3.9f && pos.y - origin.y < 4.15f, where);
        Check(local.x < 1.85f && local.x > -1.9f && Mathf.Abs(local.z) < 1.9f, where);
    }

    static void DescendWall(Vector3 origin)
    {
        ClimbLocomotion climb = Building(origin);
        Set(climb, "_surfaceUp", Vector3.up);
        Vector3 pos = origin + new Vector3(1.5f, 4f, 0f);
        Vector3 wall = origin + new Vector3(2f, 3f, 0f);
        LayerMask mask = 1 << 6;
        for (int i = 0; i < 50; i++)
            pos = (Vector3)Call(climb, "MoveOnSurface", pos, wall, Vector3.right, 0.1f, mask);

        Vector3 normal = (Vector3)Get(climb, "_surfaceUp");
        Vector3 local = pos - origin;
        string where = "roof to wall normal " + normal.ToString("F2") + " pos " + local.ToString("F2");
        Check(normal.x > 0.8f, where);
        Check(local.x > 1.85f && local.x < 2.15f, where);
        Check(local.y < 3.7f && local.y > 2.2f, where);
    }

    static void StepOffLip(Vector3 origin, float drop, bool expectDown)
    {
        Cleanup();
        const float lowerTop = 1f;
        float upperTop = lowerTop + drop;
        GameObject lower = new GameObject("LowerFloor");
        lower.layer = 6;
        lower.transform.position = origin + new Vector3(0.5f, lowerTop - 0.5f, 0f);
        BoxCollider lowerBox = lower.AddComponent<BoxCollider>();
        lowerBox.size = new Vector3(8f, 1f, 8f);
        GameObject upper = new GameObject("UpperSlab");
        upper.layer = 6;
        upper.transform.position = origin + new Vector3(-1f, upperTop - 0.025f, 0f);
        BoxCollider upperBox = upper.AddComponent<BoxCollider>();
        upperBox.size = new Vector3(2f, 0.05f, 2f);
        GameObject actor = new GameObject("Spider");
        ClimbLocomotion climb = actor.AddComponent<ClimbLocomotion>();
        Set(climb, "collisionRadius", 0.21f);
        Set(climb, "snapProbe", 0.35f);
        Set(climb, "maxStepHeight", 0.4f);
        Set(climb, "_surfaceUp", Vector3.up);
        Spawned.Add(lower);
        Spawned.Add(upper);
        Spawned.Add(actor);
        Physics.SyncTransforms();

        Vector3 pos = origin + new Vector3(-0.12f, upperTop, 0f);
        Vector3 goal = origin + new Vector3(0.8f, lowerTop, 0f);
        LayerMask mask = 1 << 6;
        string label = expectDown ? "tiny drop" : "deep drop";
        if (expectDown)
        {
            for (int i = 0; i < 40; i++)
                pos = (Vector3)Call(climb, "MoveOnSurface", pos, goal, Vector3.up, 0.1f, mask);
            Vector3 normal = (Vector3)Get(climb, "_surfaceUp");
            Vector3 local = pos - origin;
            string where = label + " normal " + normal.ToString("F2") + " pos " + local.ToString("F2");
            Check(normal.y >= CrawlGraph.FloorMinY, where);
            Check(Mathf.Abs(local.y - lowerTop) < 0.08f, where);
            Check(local.x > 0.05f, where);
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            pos = (Vector3)Call(climb, "MoveOnSurface", pos, goal, Vector3.up, 0.05f, mask);
            Vector3 local = pos - origin;
            Check(local.y > upperTop - 0.4f, label + " left the slab at " + local.ToString("F2"));
        }
    }

    static ClimbLocomotion Building(Vector3 origin)
    {
        Cleanup();
        GameObject block = new GameObject("Building");
        block.layer = 6;
        block.transform.position = origin + new Vector3(0f, 2f, 0f);
        BoxCollider box = block.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, 4f, 4f);
        GameObject actor = new GameObject("Spider");
        ClimbLocomotion climb = actor.AddComponent<ClimbLocomotion>();
        Set(climb, "collisionRadius", 0.21f);
        Set(climb, "snapProbe", 0.35f);
        Spawned.Add(block);
        Spawned.Add(actor);
        Physics.SyncTransforms();
        return climb;
    }

    static void Cleanup()
    {
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            if (Spawned[i] != null) UnityEngine.Object.DestroyImmediate(Spawned[i]);
        }
        Spawned.Clear();
    }
}
