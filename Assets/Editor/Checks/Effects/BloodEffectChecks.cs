using System;
using System.Collections;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BloodEffectChecks
{
    const string Key = "BloodEffectChecks.Running";
    static IEnumerator checks;
    static double started;
    static BloodEffectChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup;
            checks = Run();
            EditorApplication.update += Step;
        };
    }
    public static void Validate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch editor only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Step()
    {
        try {
            if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException();
            if (checks.MoveNext()) return;
            Finish(0);
        } catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Step;
        EditorApplication.Exit(code);
    }
    static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    static ParticleSystem Burst(BloodEffectPool pool)
    {
        foreach (Transform child in pool.transform)
            if (child.gameObject.activeSelf && child.GetComponent<BloodDecalSpawner>() != null)
                return child.GetComponent<ParticleSystem>();
        throw new Exception("Missing active burst");
    }
    static Transform Decal(BloodEffectPool pool)
    {
        foreach (Transform child in pool.transform)
            if (child.gameObject.activeSelf && child.GetComponent<ParticleSystem>() != null && child.GetComponent<BloodDecalSpawner>() == null)
                return child;
        throw new Exception("Missing active decal");
    }
    public static IEnumerator Run()
    {
        HitBloodChecks.Run();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/BloodChunkPFX.prefab");
        var decal = prefab.GetComponent<BloodDecalSpawner>().decalPrefab;
        var pool = BloodEffectPool.Get();
        pool.enabled = false;
        pool.Prewarm(prefab);
        Check(pool.CreatedObjects == 72, "Prewarm count");
        pool.Prewarm(prefab);
        Check(pool.CreatedObjects == 72 && pool.ActiveBursts == 0, "Prewarm is idempotent and inactive");

        // Compare actual prefab particle positions and velocities against the seeded original spawn path.
        var original = UnityEngine.Object.Instantiate(prefab, Vector3.up * 20, Quaternion.identity);
        var oldPs = original.GetComponent<ParticleSystem>();
        oldPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        oldPs.useAutoRandomSeed = false; oldPs.randomSeed = 123;
        oldPs.Play(true);
        pool.Spawn(prefab, Vector3.up * 20, 123, 0);
        var newPs = Burst(pool);
        oldPs.Simulate(0.1f, true, true); newPs.Simulate(0.1f, true, true);
        var a = new ParticleSystem.Particle[100]; var b = new ParticleSystem.Particle[100];
        int count = oldPs.GetParticles(a);
        Check(count >= 5 && count <= 10 && newPs.GetParticles(b) == count, "Burst count parity");
        for (int i = 0; i < count; i++)
            Check(Vector3.Distance(a[i].position, b[i].position) < 0.0001f && Vector3.Distance(a[i].velocity, b[i].velocity) < 0.0001f, "Seeded motion parity");
        UnityEngine.Object.DestroyImmediate(original);
        pool.Tick(10);
        Check(pool.ActiveBursts == 0, "Burst expiry");
        pool.Spawn(prefab, Vector3.up * 20, 456, 20);
        Check(Burst(pool).randomSeed == 456 && Burst(pool).particleCount == 0, "Reuse clears old particles and seed");
        pool.Tick(30);

        var surface = new GameObject("Moving surface").transform;
        pool.Enqueue(decal, surface, Vector3.right, Quaternion.identity, 100);
        surface.position = Vector3.forward * 2;
        pool.Tick(40);
        var stain = Decal(pool);
        Check(Vector3.Distance(stain.position, new Vector3(1, 0, 2)) < 0.0001f, "Queued impact follows moving surface");
        surface.rotation = Quaternion.Euler(0, 90, 0);
        surface.localScale = Vector3.one * 2;
        pool.Tick(41);
        Check(Vector3.Distance(stain.position, surface.TransformPoint(Vector3.right)) < 0.0001f && stain.localScale == Vector3.one * 2, "Active decal follows rotation and scale");
        Check(stain.GetComponentsInChildren<ParticleSystem>().Length == 2, "Both original visual systems retained");
        surface.gameObject.SetActive(false);
        pool.Tick(41.1f);
        Check(!stain.gameObject.activeSelf, "Disabled surfaces hide their stains");
        surface.gameObject.SetActive(true);
        pool.Tick(41.2f);
        Check(stain.gameObject.activeSelf, "Reenabled surfaces restore their stains");
        UnityEngine.Object.DestroyImmediate(surface.gameObject);
        pool.Tick(42);
        Check(pool.ActiveDecals == 0 && pool.CreatedObjects == 72, "Destroyed surfaces return decals without destroying pool");

        surface = new GameObject("Budget surface").transform;
        var doomed = new GameObject("Removed pending surface");
        pool.Enqueue(decal, doomed.transform, Vector3.zero, Quaternion.identity, 200);
        UnityEngine.Object.DestroyImmediate(doomed);
        pool.Tick(43);
        Check(pool.ActiveDecals == 0 && pool.PendingImpacts == 0, "Destroyed queued surfaces are discarded");
        for (int i = 0; i < 100; i++) pool.Enqueue(decal, surface, Vector3.zero, Quaternion.identity, 200);
        Check(pool.PendingImpacts == 64 && pool.DroppedImpacts == 36, "Bounded impact queue");
        pool.Tick(50);
        Check(pool.ActiveDecals == 4 && pool.PendingImpacts == 60, "Four activations per tick");
        for (int i = 0; i < 15; i++) pool.Tick(51 + i);
        Check(pool.ActiveDecals == 64 && pool.PendingImpacts == 0, "Decal capacity");
        pool.Enqueue(decal, surface, Vector3.one, Quaternion.identity, 210);
        pool.Tick(70);
        Check(pool.RecycledDecals == 1 && pool.ActiveDecals == 64 && pool.CreatedObjects == 72, "Oldest decal recycling");
        pool.Tick(200);
        Check(pool.ActiveDecals == 1, "Original death-relative expiry survives reuse");
        pool.Tick(210);
        Check(pool.ActiveDecals == 0, "Recycled decal expiry");
        for (int i = 0; i < 12; i++) pool.Spawn(prefab, Vector3.up * 20, i + 1, 300 + i);
        Check(pool.ActiveBursts == 8 && pool.CreatedObjects == 72, "Burst cap without allocations");
        pool.Tick(330);
        pool.Enqueue(decal, surface, Vector3.zero, Quaternion.identity, 331);
        pool.Tick(332);
        Check(pool.ActiveDecals == 0, "Expired queued impacts are discarded");
        Debug.Log("Blood behavior PASS: prefab seed/motion parity, reset, expiry, queue, limits, moving/destroyed surfaces");

        // Warm managed/native accessors before measuring the identical ten-impact workload.
        PooledDeath(pool, prefab, decal, surface);
        long pooled = Measure(() => PooledDeath(pool, prefab, decal, surface));
        // Remove the new component from the benchmark template: its preallocated
        // collision buffer would otherwise inflate the original path's allocations.
        var baselineTemplate = UnityEngine.Object.Instantiate(prefab);
        baselineTemplate.SetActive(false);
        UnityEngine.Object.DestroyImmediate(baselineTemplate.GetComponent<BloodDecalSpawner>());
        long baseline = Measure(() => OriginalDeath(baselineTemplate, decal, surface));
        UnityEngine.Object.DestroyImmediate(baselineTemplate);
        Debug.Log($"Blood allocation EVENTS (one death + ten impacts): original {baseline}; pooled {pooled}");
        Check(baseline > 0 && pooled < baseline / 10, "Expected >90% allocation reduction after warmup");
        Check(pool.CreatedObjects == 72, "No effect creation after warmup");

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.layer = 6; floor.transform.position = new Vector3(0, -0.5f, 0); floor.transform.localScale = new Vector3(100, 1, 100);
        Physics.SyncTransforms();

        // Bounded worst-capacity simulation: eight bursts, with the original particle
        // counts and collision settings. Time only simulation + pool work, not setup.
        var stressSystems = new ParticleSystem[8];
        pool.Tick(2000);
        for (int i = 0; i < 8; i++) pool.Spawn(prefab, Vector3.up, 9000 + i, 3000);
        int systemIndex = 0;
        foreach (Transform child in pool.transform)
            if (child.gameObject.activeSelf && child.GetComponent<BloodDecalSpawner>() != null)
                stressSystems[systemIndex++] = child.GetComponent<ParticleSystem>();
        Check(systemIndex == 8, "Stress fixture reaches burst capacity");
        foreach (var system in stressSystems) system.Simulate(1f / 90, true, false, false);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long simulationStart = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int frame = 0; frame < 360; frame++)
        {
            foreach (var system in stressSystems) system.Simulate(1f / 90, true, false, false);
            pool.Tick(3000 + frame / 90f);
        }
        long simulationElapsed = System.Diagnostics.Stopwatch.GetTimestamp() - simulationStart;
        long simulationAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Debug.Log($"Blood capacity PERF: eight bursts, 360 steps; {simulationElapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / 360:F3} us/step; managedBytes={simulationAllocated}; created={pool.CreatedObjects}");
        Check(pool.CreatedObjects == 72, "Stress does not grow pool");
        pool.Tick(4000);

        // Exercise real particle collisions and callback delivery on the actual prefab.
        int previousCollisions = pool.CollisionEvents;
        pool.Spawn(prefab, Vector3.up * 0.1f, 9981, Time.time);
        Check(Burst(pool).main.cullingMode == ParticleSystemCullingMode.AlwaysSimulate,
            "Authored death chunks must simulate offscreen for collision decals");
        float deadline = Time.time + 6;
        while (Time.time < deadline && pool.CollisionEvents == previousCollisions) { pool.Tick(Time.time); yield return null; }
        Check(pool.CollisionEvents > previousCollisions, "Actual particle collision callback");
        pool.Tick(Time.time);
        Check(pool.ActiveDecals > 0, "Actual impact activates pooled decal");
        Debug.Log("Blood real particle collision PASS: " + pool.CollisionEvents + " collision events");
        UnityEngine.Object.DestroyImmediate(pool.gameObject);
        Check(BloodEffectPool.Get().CreatedObjects == 0, "Scene-owned pool can rebuild after teardown");
        Debug.Log("BloodEffectChecks: ALL PASSED");
    }
    static void PooledDeath(BloodEffectPool pool, GameObject prefab, GameObject decal, Transform surface)
    {
        pool.Spawn(prefab, Vector3.up, 123, 1000);
        for (int i = 0; i < 10; i++) pool.Enqueue(decal, surface, Vector3.zero, Quaternion.identity, 1020);
        pool.Tick(1000); pool.Tick(1000.02f); pool.Tick(1000.04f); pool.Tick(1020);
    }
    static void OriginalDeath(GameObject prefab, GameObject decal, Transform surface)
    {
        var root = UnityEngine.Object.Instantiate(prefab, Vector3.up, Quaternion.identity);
        root.SetActive(true);
        // Original BloodDecalSpawner allocated these two initially empty lists.
        var collisions = new System.Collections.Generic.List<ParticleCollisionEvent>();
        var ps = root.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false; ps.randomSeed = 123; ps.Play(true);
        var effects = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < 10; i++) effects.Add(UnityEngine.Object.Instantiate(decal, Vector3.zero, Quaternion.identity, surface));
        // Immediate destruction is used only in this synchronous benchmark to isolate repetitions.
        foreach (var effect in effects) UnityEngine.Object.DestroyImmediate(effect);
        UnityEngine.Object.DestroyImmediate(root);
        GC.KeepAlive(collisions);
    }
    static long Measure(Action action)
    {
        action();
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 100000,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(recorder.Valid, "GC.Alloc unavailable");
        for (int i = 0; i < 5; i++) action();
        recorder.Stop();
        Check(recorder.Count < recorder.Capacity, "Recorder overflow");
        // GC.Alloc.Value is time, not bytes. Count allocation events.
        return recorder.Count / 5;
    }
}
