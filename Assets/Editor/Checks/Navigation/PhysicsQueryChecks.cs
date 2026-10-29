using System;
using System.Reflection;
using Game.AI;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PhysicsQueryChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project only");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = 6; floor.transform.position = Vector3.down * .5f;
            floor.transform.localScale = new Vector3(100, 1, 100);
            Physics.SyncTransforms();
            var cache = new TargetGroundSnapshot();
            Vector3 origin = Vector3.up * 2;
            const int mask = 1 << 6;
            Check(Physics.Raycast(origin, Vector3.down, out var expected, 4, mask), "Baseline floor");
            AIQueryProfile.Reset();
            for (int i = 0; i < 8; i++)
            {
                Check(cache.Query(origin, 4, mask, out var hit, true), "Cached hit");
                Check(hit.collider == expected.collider && hit.point.Equals(expected.point) && hit.normal.Equals(expected.normal), "Hit parity");
            }
            Check(AIQueryProfile.TargetRequests == 8 && AIQueryProfile.TargetRaycasts == 1 && AIQueryProfile.TargetHits == 7, "8 queries must reduce to 1");
            Debug.Log("PhysicsQueryChecks repeated target: 8 requests -> 1 raycast, 7 reused (87.5% fewer)");
            cache.Query(origin + Vector3.right * .00001f, 4, mask, out _, true);
            cache.Query(origin, 3, mask, out _, true);
            Check(!cache.Query(origin, 4, 0, out _, true), "Mask separation");
            Check(AIQueryProfile.TargetRaycasts == 4, "Exact origin, distance and mask keys");
            cache.Query(origin, 4, mask, out _, false);
            cache.Query(origin, 4, mask, out _, false);
            Check(AIQueryProfile.TargetRaycasts == 6, "Safety bypass always fresh");
            typeof(TargetGroundSnapshot).GetField("frame", Private).SetValue(cache, -1);
            cache.Query(origin, 4, mask, out _, true);
            Check(AIQueryProfile.TargetRaycasts == 7, "Next frame invalidates");
            floor.transform.position += Vector3.up * .25f; Physics.SyncTransforms();
            Check(cache.Query(origin, 4, mask, out var moved, true) && Mathf.Abs(moved.point.y - .25f) < .001f, "Moving support invalidates");
            floor.GetComponent<Collider>().enabled = false; Physics.SyncTransforms();
            Check(!cache.Query(origin, 4, mask, out _, true), "Disabled support invalidates");
            cache.Clear(); floor.GetComponent<Collider>().enabled = true; Physics.SyncTransforms();
            cache.Query(origin, 4, mask, out _, true);
            Object.DestroyImmediate(floor); Physics.SyncTransforms();
            Check(!cache.Query(origin, 4, mask, out _, true), "Destroyed support invalidates");
            // A newly inserted blocker must affect fresh safety queries even if an observation was cached.
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.layer = 6;
            Physics.SyncTransforms();
            Check(cache.Query(origin, 4, mask, out _, false), "Fresh query sees new collider after cached miss");
            cache.Clear();
            Check(cache.Query(origin, 4, mask, out _, true), "Explicit reset sees new collider");
            for (int i = 0; i < 40; i++) cache.Query(new Vector3(i, 2, 0), 4, mask, out _, true);
            Check((int)typeof(TargetGroundSnapshot).GetField("count", Private).GetValue(cache) <= 16, "Bounded capacity");
            // Production manager keeps safety calls fresh and shares only downward planning observations.
            var managerGo = new GameObject("query manager"); managerGo.SetActive(false);
            var manager = managerGo.AddComponent<EnemyManager>();
            var graph = (CrawlGraph)typeof(ResumablePathChecks).GetMethod("Line", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 100, 100 });
            typeof(EnemyManager).GetField("<Graph>k__BackingField", Private).SetValue(manager, graph);
            typeof(EnemyManager).GetField("surfaceMask", Private).SetValue(manager, (LayerMask)mask);
            AIQueryProfile.Reset();
            int fresh = manager.ResolveNode(origin);
            int planned = manager.ResolveNode(origin, true);
            Check(fresh == planned && planned == manager.ResolveNode(origin, true), "Manager node parity");
            int ground = manager.ResolveGroundNode(origin);
            Check(ground == manager.ResolveGroundNode(origin, true), "Ground-only node parity");
            Check(AIQueryProfile.TargetRaycasts == 3 && AIQueryProfile.TargetHits == 2, "Manager fresh/cached routing");
            var duckGo = new GameObject("query Duck"); duckGo.SetActive(false);
            var duck = duckGo.AddComponent<DuckCompanion>();
            typeof(DuckCompanion).GetField("environment", Private).SetValue(duck, (LayerMask)mask);
            var findGround = (Func<Vector3, bool, Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3, bool, Vector3>),
                duck, typeof(DuckCompanion).GetMethod("FindGround", Private));
            AIQueryProfile.Reset();
            Vector3 duckFresh = findGround(origin, false);
            Check(findGround(origin, true).Equals(duckFresh) && findGround(origin, true).Equals(duckFresh), "Duck target parity");
            Check(AIQueryProfile.TargetRaycasts == 2 && AIQueryProfile.TargetHits == 1, "Duck target reuse");
            typeof(DuckCompanion).GetMethod("ResetNavigationAfterAuthorityChange", Private).Invoke(duck, null);
            findGround(origin, true);
            Check(AIQueryProfile.TargetRaycasts == 3, "Duck authority change invalidates observation");
            findGround(origin, false);
            Check(AIQueryProfile.TargetRaycasts == 4, "Duck default observation bypass remains fresh");
            Object.DestroyImmediate(duckGo);
            Debug.Log("PhysicsQueryChecks Duck target parity and authority invalidation PASS");
            // Cache contains observations, not nodes: ground/climb graph lookups remain independent.
            cache.Clear();
            Action old = () => { for (int i = 0; i < 1000; i++) cache.Query(origin, 4, mask, out _, false); };
            Action optimized = () => { for (int i = 0; i < 1000; i++) cache.Query(origin, 4, mask, out _, true); };
            old(); optimized();
            long before = Allocations(old), after = Allocations(optimized);
            Check(before == 0 && after == 0, "Steady-state queries must remain allocation-free");
            Debug.Log($"PhysicsQueryChecks GC allocation EVENTS / 1000 requests: fresh {before}; reused {after}. No allocation saving claimed.");
            Object.DestroyImmediate(managerGo);
            Debug.Log("PhysicsQueryChecks: focused checks ALL PASSED; starting navigation/network regression suite");
            DuckNavigationChecks.Run();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static long Allocations(Action action)
    {
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 10000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        if (!recorder.Valid) throw new Exception("Allocation recorder unavailable");
        action(); recorder.Stop();
        if (recorder.Count >= recorder.Capacity) throw new Exception("Allocation recorder overflow");
        return recorder.Count;
    }
}
