using System;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class DuckInteriorStairChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(DuckCompanion d, string n, params object[] a) => typeof(DuckCompanion).GetMethod(n, Flags).Invoke(d, a);
    static void Set(DuckCompanion d, string n, object v) => typeof(DuckCompanion).GetField(n, Flags).SetValue(d, v);
    static object Get(DuckCompanion d, string n) => typeof(DuckCompanion).GetField(n, Flags).GetValue(d);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("PASS: " + message); }
    public static void Run()
    {
        Check(Application.isBatchMode, "isolated actual-level fixture");
        EditorSceneManager.OpenScene("Assets/FenceLevel.unity");
        var go = new GameObject("Interior stair walker");
        go.AddComponent<PhotonView>();
        var d = go.AddComponent<DuckCompanion>(); Call(d, "Awake");
        Set(d, "environment", (LayerMask)(1 << 6));
        var starts = new[] { new Vector3(52.684f,6.120f,-81.113f), new Vector3(96.388f,13.899f,16.81f),
            new Vector3(75.029f,4.719f,42.06f), new Vector3(96.388f,10.399f,16.81f) };
        var ends = new[] { new Vector3(52.684f,9.616f,-87.266f), new Vector3(102.431f,16.95f,16.81f),
            new Vector3(81.431f,7.95f,42.06f), new Vector3(102.431f,16.95f,16.81f) };
        var names = new[] { "Factory catwalk", "Apartment interior", "Police station interior", "Apartment two floors" };
        for (int caseIndex = 0; caseIndex < starts.Length; caseIndex++)
        for (int direction = 0; direction < 2; direction++)
        {
            Vector3 start = direction == 0 ? starts[caseIndex] : ends[caseIndex];
            Vector3 goal = direction == 0 ? ends[caseIndex] : starts[caseIndex];
            Check(Physics.Raycast(start + Vector3.up * .3f, Vector3.down, out var startFloor, .7f, 1 << 6),
                "actual starting floor exists");
            Check(Physics.Raycast(goal + Vector3.up * .3f, Vector3.down, out var goalFloor, .7f, 1 << 6),
                "actual destination floor exists");
            start = startFloor.point + Vector3.up * .04f;
            goal = goalFloor.point + Vector3.up * .04f;
            Call(d, "ResetNavigationAfterAuthorityChange");
            go.transform.position = start; go.GetComponent<CharacterController>().enabled = true;
            Physics.SyncTransforms();
            typeof(DuckCompanion).GetProperty("State").SetValue(d, DuckCompanion.Activity.Following);
            Check((bool)Call(d, "NearbyStairTread"), names[caseIndex] + " detects actual treads " + direction);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool found = (bool)Call(d, "BuildInteriorStairRoute", goal);
            if (!found)
            {
                int count = 0; Vector3 highest = start, lowest = start;
                foreach (object node in (System.Collections.IEnumerable)Get(d, "stairNodes"))
                {
                    Vector3 p = (Vector3)node.GetType().GetField("point").GetValue(node);
                    if (p.y > highest.y) highest = p; if (p.y < lowest.y) lowest = p; count++;
                }
                Debug.Log($"STAIR FAILED nodes={count} highest={highest} lowest={lowest} goal={goal}");
                for (int j = 1; j <= 4; j++)
                {
                    Vector3 sample = highest + Vector3.back * (j * .15f);
                    if (Physics.Raycast(sample + Vector3.up * .31f, Vector3.down, out var h, .66f, 1 << 6))
                    {
                        object[] probe = {highest, sample, Vector3.zero};
                        Debug.Log($"STEP DIAG sample={sample} floor={h.point} collider={h.collider.name} clear={(bool)Call(d, "ClearBody",h.point+Vector3.up*.22f)} step={Call(d,"StairStep",probe)}");
                    }
                }
            }
            Check(found, names[caseIndex] + " builds connected route " + direction);
            Debug.Log("STAIR SEARCH " + names[caseIndex] + " milliseconds=" + watch.ElapsedMilliseconds);
            var route = (Vector3[])Get(d, "interiorStairRoute");
            Vector3 end = route[route.Length - 1];
            if (caseIndex == 3) Call(d, "ResetNavigationAfterAuthorityChange");
            for (int frame = 0; frame < 3000; frame++)
            {
                // This synchronous harness simulates many frames in one editor
                // update; explicitly advance the shared budget's frame boundary.
                typeof(Game.AI.NavigationWorkBudget).GetField("frame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, -1);
                int examinedBefore = (int)Get(d, "stairSearchExamined");
                if (!(bool)Call(d, "WalkInteriorStairs", goal, .025f)) break;
                if ((int)Get(d, "stairSearchExamined") - examinedBefore > 128)
                    throw new Exception("Runtime stair search exceeded its per-tick budget");
                if (caseIndex == 0 && direction == 0 && frame == 30)
                    Call(d, "ResetNavigationAfterAuthorityChange");
                if (d.State == DuckCompanion.Activity.Jumping || d.State == DuckCompanion.Activity.Climbing)
                    throw new Exception("Stair walking became traversal");
            }
            Debug.Log("STAIR MOTOR " + names[caseIndex] + " direction=" + direction + " actual=" + go.transform.position + " end=" + end + " event=" + d.LastEvent);
            Check(Vector3.Distance(go.transform.position, end) < .4f, names[caseIndex] + " motor completes " + (direction == 0 ? "ascent" : "descent"));
            if (caseIndex == 0 && direction == 0)
            {
                go.transform.position = start; Physics.SyncTransforms();
                Call(d, "ResetNavigationAfterAuthorityChange");
                Check((bool)Call(d, "BuildInteriorStairRoute", goal), "rebuild before obstruction test");
                var blockedRoute = (Vector3[])Get(d, "interiorStairRoute");
                var obstacle = new GameObject("Blocked tread"); obstacle.layer = 6;
                obstacle.transform.position = blockedRoute[0] + Vector3.up * .6f;
                obstacle.AddComponent<BoxCollider>().size = new Vector3(.5f,1.2f,.5f);
                Physics.SyncTransforms();
                Check(!(bool)Call(d, "WalkInteriorStairs", goal, .025f), "blocked stair yields to existing alternate movement");
                UnityEngine.Object.DestroyImmediate(obstacle); Physics.SyncTransforms();
            }
        }
        Debug.Log("ALL INTERIOR STAIR CHECKS PASSED");
    }
}
