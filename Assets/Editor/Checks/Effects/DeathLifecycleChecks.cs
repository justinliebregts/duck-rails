using System;
using System.Reflection;
using System.Diagnostics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Photon.Pun;
using Game.Animation;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

public static class DeathLifecycleChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static object Get(object o, string n) => o.GetType().GetField(n, P).GetValue(o);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, P).Invoke(o, a);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch tests only");
        // These Edit Mode checks replace scenes. Keep an inert analytics singleton
        // in a preview scene so companion interactions never boot the live SDK.
        var analyticsScene = EditorSceneManager.NewPreviewScene();
        var analyticsObject = new GameObject("Disabled regression analytics");
        analyticsObject.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(analyticsObject, analyticsScene);
        var previousAnalytics = DuckzAnalytics.Instance;
        typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, analyticsObject.AddComponent<DuckzAnalytics>());
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FlareAndBomber();
            AvatarRagdoll();
            RagdollChecks.Run();
            DeathFlareLightChecks.Run();
            MeleeGraceChecks.Run();
            EquipmentPersistenceChecks.Run();
            DuckHomeChecks.Run();
            var duck = (DuckCompanion)typeof(DuckHomeChecks).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "Contact victim" });
            typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, true);
            var triggerGo = new GameObject("Lethal hazard"); var trigger = triggerGo.AddComponent<KillTrigger>();
            var contact = duck.gameObject.AddComponent<BoxCollider>();
            var secondContact = duck.gameObject.AddComponent<SphereCollider>();
            int accepted = 0; trigger.PlayerKilled += () => accepted++;
            Check(trigger.TryContact(contact) && !trigger.TryContact(secondContact) && accepted == 1 && duck.IsDead,
                "Multiple contacts must accept death and listeners exactly once");
            Debug.Log("KillTrigger multiple-contact death/listener guard PASS");
            Debug.Log("DeathLifecycleChecks: ALL PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(analyticsScene);
            typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, previousAnalytics);
        }
    }
    static void FlareAndBomber()
    {
        var flarePrefab = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab");
        var gib = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/GibPFX.prefab");
        Check(flarePrefab != null && gib != null, "Missing effect prefabs");
        var pool = DeathEffectPool.Get(); Call(pool, "Awake");
        DeathFlare.Prewarm(flarePrefab); pool.Prewarm(gib, 4);
        Check(pool.CreatedObjects == 12 && pool.ActiveCount == 0, "Prewarm inactive effects");
        var point = new Vector3(50, 10, 50);
        var first = DeathFlare.SpawnAfterDeath(flarePrefab, point);
        Call(first, "OnEnable"); // Editor fixtures explicitly deliver lifecycle callbacks.
        Check(!first.IsBurning && !first.GetComponentInChildren<Light>().enabled, "Launch delay has no protection/light");
        Set(first, "started", Time.unscaledTimeAsDouble - 1); Call(first, "Update");
        Check(first.IsBurning && DeathFlare.FindNear(first.transform.position, 2) == first, "Burning flare protects");
        Check(!DeathFlare.AllowsMovement(first.transform.position + Vector3.right * 2, first.transform.position, 1), "Flare blocks approach");
        first.GetComponentInChildren<Light>().intensity = .01f;
        Set(first, "landed", true); Set(first, "started", Time.unscaledTimeAsDouble - 31); Call(first, "Update");
        Check(!first.gameObject.activeSelf && pool.ActiveCount == 0, "Expiry returns effect");
        var second = DeathFlare.SpawnAfterDeath(flarePrefab, point + Vector3.right);
        Check(second == first && !(bool)Get(second, "landed") && !(bool)Get(second, "launched"), "Flare reuses/reset flight");
        Check(second.GetComponentInChildren<Light>().intensity > .01f, "Flare restores authored intensity");
        pool.Release(second.gameObject);
        var late = DeathFlare.SpawnAfterDeath(flarePrefab, point, 10);
        Check(late.IsBurning && (bool)Get(late, "launched"), "Delayed event catches up"); pool.Release(late.gameObject);
        Check(DeathFlare.SpawnAfterDeath(flarePrefab, point, 33) == null, "Expired network event ignored");
        var held = new DeathFlare[9];
        for (int i = 0; i < held.Length; i++) held[i] = DeathFlare.Spawn(flarePrefab, point + Vector3.right * i);
        Check(pool.ActiveCount == 9 && pool.OverflowCreates == 1 && held[0].IsBurning, "Overflow preserves all live protection");
        foreach (var f in held) pool.Release(f.gameObject);
        var enemyGo = new GameObject("Bomber test"); enemyGo.SetActive(false);
        var bomber = enemyGo.AddComponent<ZombieBomberExplode>(); Set(bomber, "gibPrefab", gib);
        GR.Player.instance = null;
        Call(bomber, "ApplyExplosion", 1, point, Quaternion.identity, 0f);
        int active = pool.ActiveCount;
        Call(bomber, "ApplyExplosion", 1, point, Quaternion.identity, 0f);
        Call(bomber, "ApplyExplosion", 0, point, Quaternion.identity, 0f);
        Check(active == 1 && pool.ActiveCount == active, "Bomber duplicate/old revisions rejected");
        pool.Tick(Time.time + 11);
        Check(pool.ActiveCount == 0, "Bomber expires to pool");
        Call(bomber, "ApplyExplosion", 2, point, Quaternion.identity, 0f);
        Check(pool.ActiveCount == 1 && pool.CreatedObjects == 13, "New bomber life reuses effect");
        pool.Tick(Time.time + 11);
        Object.DestroyImmediate(enemyGo);
        int created = pool.CreatedObjects;
        Action original = () => {
            var f = Object.Instantiate(flarePrefab, point, Quaternion.identity);
            foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var g = Object.Instantiate(gib, point, Quaternion.identity);
            Object.DestroyImmediate(f.gameObject); Object.DestroyImmediate(g);
        };
        Action reused = () => {
            var f = DeathFlare.SpawnAfterDeath(flarePrefab, point);
            var g = pool.Rent(gib, point, Quaternion.identity); pool.Activate(g, 10);
            pool.Release(f.gameObject); pool.Release(g);
        };
        Bench("flare+gib create/reset/release (excludes audio/network)", original, reused);
        Check(pool.CreatedObjects == created && pool.ActiveCount == 0, "No warmed effect creation");
        Debug.Log("Death effects PASS: delayed launch, age catchup, expiry, light reset, protection, overflow and bomber revisions");
    }
    static void AvatarRagdoll()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
        Check(prefab != null, "Missing ragdoll prefab");
        var source = Object.Instantiate(prefab); source.Park();
        var go = new GameObject("Remote avatar"); go.SetActive(false); go.AddComponent<PhotonView>();
        var avatar = go.AddComponent<PhotonPlayer>();
        var body = go.AddComponent<RemotePlayerBody>();
        var left = go.AddComponent<TwoBoneLimb>(); var rightGo = new GameObject("right arm"); rightGo.transform.SetParent(go.transform);
        var right = rightGo.AddComponent<TwoBoneLimb>();
        Set(left, "endBone", source.leftHandTarget); Set(right, "endBone", source.rightHandTarget);
        Set(body, "chest", source.targetHip); Set(body, "head", source.targetHip); Set(body, "leftArm", left); Set(body, "rightArm", right);
        Set(avatar, "body", body); Set(avatar, "deathRagdollPrefab", prefab);
        Call(avatar, "PrepareDeathRagdoll");
        var reused = (Ragdoll)Get(avatar, "_remoteRagdoll");
        Check(reused != null && !reused.gameObject.activeSelf, "Avatar prewarm inactive");
        var launch = new RagdollLaunch { body = Vector3.right * 3, leftHand = Vector3.up * 2 };
        for (int i = 0; i < 3; i++)
        {
            Call(avatar, "SetRemoteDeathVisuals", true, launch);
            Check(Get(avatar, "_remoteRagdoll") == reused && reused.gameObject.activeSelf, "Avatar reuses same ragdoll");
            Call(avatar, "SetRemoteDeathVisuals", false, default(RagdollLaunch));
            foreach (var rb in reused.GetComponentsInChildren<Rigidbody>()) Check(rb.isKinematic, "Parked ragdoll physics stopped");
        }
        reused.ApplyCompanionAppearance(source.transform, true);
        var eye = Array.Find(reused.GetComponentsInChildren<Renderer>(true), r => r.name == "Eye_L");
        Check(eye != null, "Corpse eye renderer missing");
        var block = new MaterialPropertyBlock(); eye.GetPropertyBlock(block);
        Check(block.GetColor("_EmissionColor") == Color.red, "Killer eye appearance");
        reused.ApplyCompanionAppearance(source.transform, false); eye.GetPropertyBlock(block);
        Check(block.GetColor("_EmissionColor") != Color.red, "Normal death clears killer eyes");
        // Profile pose/reset separately from appearance and network processing.
        Action original = () => { var r = Object.Instantiate(prefab); r.PoseToPlayer(source.transform, launch); Object.DestroyImmediate(r.gameObject); };
        Action pooled = () => { reused.PoseToPlayer(source.transform, launch); reused.Park(); };
        Bench("ragdoll instantiate+pose+release vs reuse (excludes skin/network)", original, pooled);
        // Invalid flare sender must not acquire any pooled effect.
        Set(avatar, "deathFlarePrefab", AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab"));
        var sender = (Photon.Realtime.Player)Activator.CreateInstance(typeof(Photon.Realtime.Player), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, new object[] { "forged", 99, false }, null);
        int active = DeathEffectPool.Get().ActiveCount;
        Call(avatar, "ReceiveDeathFlare", Vector3.zero, PhotonNetwork.Time, new PhotonMessageInfo(sender, 0, go.GetComponent<PhotonView>()));
        Check(DeathEffectPool.Get().ActiveCount == active, "Forged flare sender rejected");
        Call(avatar, "OnDestroy"); Object.DestroyImmediate(go); Object.DestroyImmediate(source.gameObject);
        Debug.Log("Ragdoll reuse/parking and flare sender guard PASS");
    }
    static void Bench(string label, Action baseline, Action optimized)
    {
        baseline(); optimized();
        var a = Measure(baseline); var b = Measure(optimized);
        Check(b.allocations < a.allocations, "Expected fewer allocations: " + label);
        Debug.Log($"DEATH BENCH {label}: original {a.ms:F3} ms / {a.allocations:F1} GC events; pooled {b.ms:F3} ms / {b.allocations:F1} GC events per cycle (Editor, synchronous cleanup)");
    }
    static (double ms, double allocations) Measure(Action action)
    {
        const int n = 20;
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 100000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(recorder.Valid, "GC recorder unavailable");
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < n; i++) action();
        double ms = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency / n;
        recorder.Stop(); Check(recorder.Count < recorder.Capacity, "GC recorder overflow");
        return (ms, (double)recorder.Count / n);
    }
}
