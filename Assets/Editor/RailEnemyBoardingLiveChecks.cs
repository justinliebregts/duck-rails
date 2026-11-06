#if UNITY_EDITOR
using System;
using System.IO;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Fast, isolated actual Play Mode loop. No authentication, reports or route loading.</summary>
[InitializeOnLoad]
public static class RailEnemyBoardingLiveChecks
{
    const string Armed = "Rails.Boarding.Offline";
    static bool Spider => SessionState.GetBool(Armed + ".spider", false);
    static string LogPath => "docs/validation/enemy-boarding/" + (SessionState.GetBool(Armed + ".surface", false) ? "spider-surface-" : Spider ? "spider-" : "") + (SessionState.GetBool(Armed + ".sides", false) ? "side-play.txt" : (SessionState.GetBool(Armed + ".motel", false) ? "motel-play.txt" : "offline-play.txt"));
    [Serializable] class Setup { public SceneSetup[] scenes; }
    static Enemy enemy;
    static RailTrain train;
    static Transform target;
    static EnemyManager manager;
    static double entered;
    static int stage;
    static Vector3 startLocal, spawn;
    static bool started, negativeChecksDone;
    static GameObject exitBarrier;
    static bool sawSideCrossing; static int sideOpening;
    static RailEnemyBoardingLiveChecks()
    { EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick; }
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Isolated Play Loop")]
    public static void Run() => Start(false);
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Motel Dismount Loop")]
    public static void RunMotel() => Start(true);
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Side Openings Loop")]
    public static void RunSides() => Start(true, true);
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Spider Rear Loop")]
    public static void RunSpiderRear() => Start(true, false, true);
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Spider Surface Carry Loop")]
    public static void RunSpiderSurface() => Start(true, false, true, true);
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Spider Side Loop")]
    public static void RunSpiderSides() => Start(true, true, true);
    static void Start(bool motel, bool sides = false, bool spider = false, bool surface = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit current Play Mode first");
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            if (scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)
                throw new Exception("Save scene edits before isolated boarding test");
        SessionState.SetBool(Armed + ".surface", surface);
        SessionState.SetBool(Armed + ".spider", spider);
        SessionState.SetBool(Armed + ".motel", motel);
        SessionState.SetBool(Armed + ".sides", sides);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.WriteAllText(LogPath, "");
        SessionState.SetString(Armed + ".setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        SessionState.SetBool(Armed, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // EditorPlaySignIn explicitly skips scenes containing a LoginFlow, even
        // inactive. A disabled sentinel keeps this fixture offline without changing preferences.
        var loginSentinel = new GameObject("Offline fixture login sentinel");
        loginSentinel.SetActive(false); loginSentinel.AddComponent<LoginFlow>();
        EditorApplication.isPlaying = true;
    }
    static void Log(string text)
    { File.AppendAllText(LogPath, DateTime.UtcNow.ToString("O") + " " + text + "\n"); Debug.Log("BOARDING PLAY: " + text); }
    static void Changed(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Armed, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            try { SetupWorld(); }
            catch (Exception e) { Finish("FAIL setup " + e); }
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Armed, false); started = false;
            var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Armed + ".setup", "{}"));
            if (setup?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        }
    }
    static void SetupWorld()
    {
        Application.runInBackground = true; negativeChecksDone = false;
        var block = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetBool(Armed + ".motel", false) ? "Assets/DuckRails/Blocks/roadside-motel-v1.prefab" : "Assets/DuckRails/Blocks/maintenance-depot-v1.prefab")).GetComponent<RailBlock>();
        train = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailEnemyBoardingAuthoring.TrainPath)).GetComponent<RailTrain>();
        train.transform.SetPositionAndRotation(new Vector3(0, 1, 80), Quaternion.identity);
        target = new GameObject("Boarding pursuit target").transform;
        target.SetParent(train.transform, false); target.localPosition = new Vector3(0, 1, -4.2f);
        var managerRoot = new GameObject("Boarding test manager"); managerRoot.SetActive(false);
        manager = managerRoot.AddComponent<EnemyManager>();
        using (var so = new SerializedObject(manager))
        {
            so.FindProperty("graphAsset").objectReferenceValue = block.graph;
            so.FindProperty("surfaceMask").intValue = 1 << 6;
            so.FindProperty("visionMask").intValue = 1 << 6;
            so.FindProperty("maxEnemies").intValue = 0;
            so.FindProperty("fallbackTarget").objectReferenceValue = target;
            so.FindProperty("perceptionInterval").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        managerRoot.SetActive(true);
        var graph = manager.Graph;
        int node = graph.FindNearestGroundNode(new Vector3(7, 0, 70), 5);
        if (node < 0) throw new Exception("No approach node in real depot bake");
        enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Spider ? "Assets/Resources/Rails/RailSpider.prefab" : "Assets/Resources/Rails/RailZombie.prefab")).GetComponent<Enemy>();
        enemy.Teleport(graph.GetPosition(node) + Vector3.up * .02f, Quaternion.LookRotation(target.position - graph.GetPosition(node)));
        Physics.SyncTransforms();
        spawn = enemy.Root.position;
        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = "Boarding obstruction fixture"; barrier.layer = 6;
        barrier.transform.position = spawn + Vector3.forward;
        barrier.transform.localScale = new Vector3(2, 3, .5f);
        Physics.SyncTransforms();
        var clear = typeof(RailEnemyBoarding).GetMethod("ClearApproach", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        bool blocked = !(bool)clear.Invoke(enemy.Boarding, new object[] { spawn, spawn + Vector3.forward * 2, (LayerMask)(1 << 6) });
        Object.DestroyImmediate(barrier); Physics.SyncTransforms();
        if (!blocked) throw new Exception("Blocked approach accepted");
        Log("PASS physical obstacle rejects approach");
        SetSpeed(3);
        stage = -2; entered = EditorApplication.timeSinceStartup; started = true;
        if (SessionState.GetBool(Armed + ".sides", false))
        {
            SetSpeed(0);
            // Isolate route selection from sight occlusion/FOV while the transfer
            // turns away from the target. The passive-stalk case resets these below.
            using (var so = new SerializedObject(manager)) { so.FindProperty("visionMask").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            using (var so = new SerializedObject(enemy))
            {
                so.FindProperty("visionRange").floatValue = 30;
                so.FindProperty("nearSenseRadius").floatValue = 30;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            enemy.Teleport(train.transform.TransformPoint(new Vector3(0, .03f, 0)), train.transform.rotation);
            enemy.Boarding.SetRemoteFrame(true, false);
            stage = 100; SideTarget(false, -1);
        }
        if (SessionState.GetBool(Armed + ".surface", false)) { SetSpeed(0); stage = 200; }
        Log("Actual " + block.id + " + actual train + actual " + (Spider ? "spider" : "minion") + "; start=" + enemy.Root.position + " target=" + target.position);
        var eye = new SerializedObject(enemy).FindProperty("eye").objectReferenceValue as Transform;
        if (eye != null && Physics.Raycast(eye.position, (target.position-eye.position).normalized, out var obstruction,
            Vector3.Distance(eye.position,target.position), 1<<6, QueryTriggerInteraction.Ignore))
            Log("Initial sight obstruction=" + obstruction.collider.name + " point=" + obstruction.point);
    }
    static void CheckSpiderSurfaceCarry()
    {
        manager.enabled = false;
        var graph = train.GetComponent<RailTrainNavigation>().Graph;
        var climb = enemy.GetComponent<ClimbLocomotion>();
        int selected = -1;
        for (int i = 0; i < graph.NodeCount; i++)
        {
            Vector3 normal = graph.GetNormal(i), point = graph.GetPosition(i) + normal * .02f;
            if (Mathf.Abs(normal.y) > .1f || point.y < train.transform.position.y + .5f) continue;
            if (climb.IsRecoveryPoseValid(point, normal, manager.SurfaceMask, out _)) { selected = i; break; }
        }
        if (selected < 0) throw new Exception("No physically supported train wall node");
        Vector3 initialNormal = graph.GetNormal(selected);
        enemy.Teleport(graph.GetPosition(selected) + initialNormal * .02f, Quaternion.LookRotation(Vector3.up, initialNormal));
        enemy.Boarding.SetRemoteFrame(true, false);
        enemy.Gait.Tick(.02f);
        Vector3 local = train.transform.InverseTransformPoint(enemy.Root.position);
        Vector3 localNormal = train.transform.InverseTransformDirection(climb.SurfaceNormal);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var gait = enemy.GetComponent<Game.Animation.SpiderGait>();
        var legs = (Game.Animation.SpiderGait.LegSlot[])gait.GetType().GetField("legs", flags).GetValue(gait);
        var feet = Array.ConvertAll(legs, leg => train.transform.InverseTransformPoint(leg.planted));
        var origin = train.transform.position;
        for (int i = 0; i < 32; i++)
        {
            train.transform.SetPositionAndRotation(origin + new Vector3(i * .1f, Mathf.Sin(i) * .2f, i * .2f),
                Quaternion.Euler(Mathf.Sin(i) * 8, i * 3, 0));
            Physics.SyncTransforms();
            enemy.Boarding.BeforeTick(manager, .02f);
            if (!enemy.Boarding.IsAboard || Vector3.Distance(train.transform.InverseTransformPoint(enemy.Root.position), local) > .002f)
                throw new Exception("Spider lost wall support or drifted at pose " + i);
            if (Vector3.Dot(climb.SurfaceNormal, train.transform.TransformDirection(localNormal)) < .999f)
                throw new Exception("Spider wall normal did not rotate with train");
            for (int leg = 0; leg < legs.Length; leg++)
                if (Vector3.Distance(train.transform.InverseTransformPoint(legs[leg].planted), feet[leg]) > .002f)
                    throw new Exception("Spider planted foot drifted with train");
            if (enemy.GetComponent<SpiderWeb>().WebActive) throw new Exception("Moving train retained a world-anchored web");
        }
        Log("PASS supported wall node=" + selected + " local=" + local + " normal=" + localNormal);
    }
    static void BlockOpenings(bool all)
    {
        exitBarrier = new GameObject("Blocked opening fixtures");
        var nav = train.GetComponent<RailTrainNavigation>();
        for (int opening = all ? 0 : 1; opening < (all ? 3 : 2); opening++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.layer = 6;
            cube.transform.SetParent(exitBarrier.transform);
            cube.transform.SetPositionAndRotation(train.transform.TransformPoint(nav.Approach(opening)), train.transform.rotation);
            cube.transform.localScale = opening == 0 ? new Vector3(2, 4, .7f) : new Vector3(.7f, 4, 2);
        }
        Physics.SyncTransforms();
    }
    static void SideTarget(bool aboard, int side)
    {
        target.SetParent(aboard ? train.transform : null, true);
        if (aboard) target.localPosition = new Vector3(side * .8f, 1, 0);
        else
        {
            target.position = train.transform.TransformPoint(new Vector3(side * 7, 0, 0));
            target.position = new Vector3(target.position.x, 1, target.position.z);
        }
        enemy.SetRootPose(enemy.Root.position, Quaternion.LookRotation(target.position - enemy.Root.position));
        entered = EditorApplication.timeSinceStartup; sawSideCrossing = false; sideOpening = -1;
    }
    static void TickSides(double age)
    {
        if (age > 35) throw new Exception("Side timeout stage=" + stage + " " + enemy.Boarding.DiagnosticState
            + " local=" + train.transform.InverseTransformPoint(enemy.Root.position) + " state=" + enemy.CurrentState);
        if (enemy.Boarding.Crossing)
        {
            sawSideCrossing = true; sideOpening = enemy.Boarding.SelectedOpening;
            // When crossing a side edge, stay inside the authored rail opening.
            var local = train.transform.InverseTransformPoint(enemy.Root.position);
            if (sideOpening > 0 && Mathf.Abs(local.x) > 1.8f && Mathf.Abs(local.x) < 2.2f && Mathf.Abs(local.z) > .7f)
                throw new Exception("Transfer crossed a solid side rail at " + local);
        }
        if (stage == 106)
        {
            if (age < 3) return;
            if (!enemy.Boarding.IsAboard || enemy.Boarding.Crossing || enemy.Boarding.HasBoardingPlan)
                throw new Exception("All blocked openings accepted a transfer");
            Log("PASS all three openings blocked: remains aboard without a transfer plan");
            Object.DestroyImmediate(exitBarrier); Physics.SyncTransforms();
            stage = 107; SideTarget(false, -1); return;
        }
        bool boarding = stage == 101 || stage == 103 || stage == 105 || stage == 108;
        bool complete = boarding ? enemy.Boarding.IsAboard : !enemy.Boarding.IsAboard && !enemy.Boarding.Crossing
            && Vector3.Distance(enemy.Root.position, target.position - Vector3.up) < 1.3f;
        if (!complete || !sawSideCrossing) return;
        int expected = stage <= 101 || stage == 107 || stage == 108 ? 1 : stage <= 103 ? 2 : -1;
        if (expected >= 0 && sideOpening != expected) throw new Exception("Wrong opening stage=" + stage + " got=" + sideOpening);
        if (stage == 104 && sideOpening == 1) throw new Exception("Used obstructed left opening");
        Log("PASS stage=" + stage + " natural " + (boarding ? "boarding" : "exit + continued ground pursuit")
            + " opening=" + RailTrainNavigation.OpeningName(sideOpening));
        if (stage == 107 && Spider)
        {
            int node = manager.WorldGraph.FindNearestGroundNode(train.transform.position + new Vector3(-4.5f, -1, .5f), 2);
            if (node < 0) throw new Exception("No stalking approach node");
            enemy.Teleport(manager.WorldGraph.GetPosition(node) + Vector3.up * .02f, train.transform.rotation);
            using (var so = new SerializedObject(enemy))
            {
                so.FindProperty("visionRange").floatValue = 0;
                so.FindProperty("nearSenseRadius").floatValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            stage = 108; SideTarget(true, -1); return;
        }
        if (stage == 108)
        {
            if (enemy.CurrentState != Enemy.State.Wander) throw new Exception("Stalking case acquired combat target");
            Finish("PASS spider passive stalking boards through left gap; side and obstruction loop complete"); return;
        }
        if (stage == 107) { Finish("PASS both side openings in both directions, blocked-nearest fallback, all-blocked hold and retry after clearing"); return; }
        stage++;
        if (stage == 104) BlockOpenings(false);
        if (stage == 105) { Object.DestroyImmediate(exitBarrier); Physics.SyncTransforms(); }
        if (stage == 106) BlockOpenings(true);
        SideTarget(stage == 101 || stage == 103 || stage == 105, stage == 102 || stage == 103 ? 1 : -1);
    }
    static void SetSpeed(float speed) => typeof(RailTrain).GetProperty("Speed").SetValue(train, speed);
    static void RestartPursuit()
    {
        enemy.Teleport(spawn, Quaternion.LookRotation(target.position - spawn));
        entered = EditorApplication.timeSinceStartup;
    }
    static void Finish(string message)
    { Log(message); started = false; EditorApplication.isPlaying = false; }
    static void Tick()
    {
        if (!started || !EditorApplication.isPlaying) return;
        if (enemy == null) { Finish("FAIL fixture enemy unexpectedly destroyed"); return; }
        try
        {
            double age = EditorApplication.timeSinceStartup - entered;
            if (stage == 200) { CheckSpiderSurfaceCarry(); Finish("PASS spider wall support, contact normals and planted feet carried through 32 poses"); return; }
            if (stage >= 100) { TickSides(age); return; }
            if (age > 35) throw new Exception("Timeout stage=" + stage + " position=" + enemy.Root.position + " state=" + enemy.CurrentState + " plan=" + enemy.Boarding.HasBoardingPlan + " crossing=" + enemy.Boarding.Crossing);
            if (stage == -2 && age > 4)
            {
                if (enemy.Boarding.IsAboard || enemy.Boarding.Crossing || enemy.Boarding.HasBoardingPlan)
                    throw new Exception("Fast train accepted boarding");
                Log("PASS fast train rejects boarding");
                SetSpeed(0); RestartPursuit(); stage = -1;
            }
            else if (stage == -1 && enemy.Boarding.Crossing)
            {
                SetSpeed(3); enemy.Boarding.TickTraversal(manager, .02f);
                if (enemy.Boarding.Crossing || enemy.Boarding.IsAboard)
                    throw new Exception("Departure did not abort transfer");
                Log("PASS departure interrupts active transfer");
                SetSpeed(0); RestartPursuit(); stage = 0;
            }
            else if (stage == 0 && enemy.Boarding.IsAboard)
            {
                Log("PASS natural chase, approach and boarding at " + enemy.Root.position);
                stage = 1; entered = EditorApplication.timeSinceStartup;
                startLocal = train.transform.InverseTransformPoint(enemy.Root.position);
                // Pause ordinary AI only for the exact carry invariant; the same
                // component method used by the manager is exercised every pose.
                manager.enabled = false;
            }
            else if (stage == 1)
            {
                float t = (float)age;
                train.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(t) * .75f, 1 + Mathf.Sin(t) * .2f, 80 + t * t * .8f), Quaternion.Euler(Mathf.Sin(t) * 8, t * 2, 0));
                Physics.SyncTransforms(); enemy.Boarding.Carry();
                float error = Vector3.Distance(train.transform.InverseTransformPoint(enemy.Root.position), startLocal);
                if (Spider && Vector3.Dot(enemy.GetComponent<ClimbLocomotion>().SurfaceNormal, train.transform.up) < .999f)
                    throw new Exception("Spider contact normal did not follow train pitch/yaw");
                if (error > .002f) throw new Exception("Carry drift=" + error);
                if (age > 5)
                {
                    Log("PASS accelerating translation, yaw and slope carry; local error=" + error);
                    stage = 2; entered = EditorApplication.timeSinceStartup;
                    target.localPosition = new Vector3(0, 1, 0);
                    manager.enabled = true;
                }
            }
            else if (stage == 2 && age > 4)
            {
                if (!enemy.Boarding.IsAboard) throw new Exception("Chase lost train support at local=" + train.transform.InverseTransformPoint(enemy.Root.position) + " state=" + enemy.CurrentState);
                if ((train.transform.InverseTransformPoint(enemy.Root.position) - startLocal).sqrMagnitude < .3f)
                    throw new Exception("Minion did not move along train graph toward target");
                Log("PASS ordinary brain/pathfinding chases aboard, position=" + train.transform.InverseTransformPoint(enemy.Root.position));
                target.SetParent(null, true);
                target.position = train.transform.TransformPoint(new Vector3(0, 0, -9));
                target.position = new Vector3(target.position.x, 1, target.position.z);
                stage = 4; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 4 && !enemy.Boarding.IsAboard && !enemy.Boarding.Crossing
                && Vector3.Distance(enemy.Root.position, target.position - Vector3.up) < 1.3f)
            {
                if (manager.WorldGraph.FindNearestGroundNode(enemy.Root.position, 1.5f) < 0)
                    throw new Exception("Dismount did not return to the block graph");
                Log("PASS voluntary dismount and continued pursuit on block graph");
                target.SetParent(train.transform, false); target.localPosition = new Vector3(0, 1, -4.2f);
                // The target moved instantly in this fixture; face its new
                // position instead of depending on a random wander turn.
                enemy.SetRootPose(enemy.Root.position, Quaternion.LookRotation(target.position - enemy.Root.position));
                stage = 5; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 5 && enemy.Boarding.IsAboard && !negativeChecksDone && SessionState.GetBool(Armed + ".motel", false))
            {
                target.SetParent(null, true); target.position = train.transform.TransformPoint(new Vector3(0, 0, -9));
                target.position = new Vector3(target.position.x, 1, target.position.z);
                BlockOpenings(true);
                // Keep target knowledge while testing physical exit clearance.
                using (var so = new SerializedObject(manager)) { so.FindProperty("visionMask").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
                Physics.SyncTransforms(); stage = 6; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 6 && age > 3)
            {
                if (!enemy.Boarding.IsAboard) throw new Exception("Blocked exit allowed departure");
                Log("PASS blocked exit keeps minion aboard");
                Object.DestroyImmediate(exitBarrier); Physics.SyncTransforms(); SetSpeed(3);
                stage = 7; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 7 && age > 3)
            {
                if (!enemy.Boarding.IsAboard) throw new Exception("Fast train allowed voluntary dismount");
                Log("PASS fast train rejects voluntary dismount");
                SetSpeed(0); negativeChecksDone = true; stage = 4; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 5 && enemy.Boarding.IsAboard)
            {
                Log("PASS reboards after chasing off train");
                // Rear is open; keep the real hit system and real deck collider.
                enemy.Teleport(train.transform.TransformPoint(new Vector3(0, .03f, -4.4f)), train.transform.rotation);
                enemy.Boarding.SetRemoteFrame(true, false);
                enemy.ReceiveTrustedHit(-train.transform.forward * 10f, enemy.Root.position);
                stage = 3; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 3 && age > 3)
            {
                if (enemy.Boarding.IsAboard) throw new Exception("Rear melee shove did not detach enemy");
                Log("PASS real melee shove detaches and falls, world=" + enemy.Root.position);
                Finish("PASS isolated Play Mode boarding/riding/chasing/dismount/reboarding/knock-off loop");
            }
        }
        catch (Exception e) { Finish("FAIL " + e.Message); }
    }
}
#endif
