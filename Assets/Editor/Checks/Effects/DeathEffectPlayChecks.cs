using System;
using System.Collections;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DeathEffectPlayChecks
{
    const string Key = "DeathEffectPlayChecks.Run";
    static IEnumerator checks;
    static double started;
    static DeathEffectPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup; checks = CheckEffects(); EditorApplication.update += Step;
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Step()
    {
        try {
            if (EditorApplication.timeSinceStartup - started > 30) throw new Exception("Play checks timeout");
            if (checks.MoveNext()) return;
            Finish(0);
        } catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Step; EditorApplication.Exit(code); }
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static IEnumerator CheckEffects()
    {
        CheckBomberLaunch();
        var prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab");
        var gib = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/GibPFX.prefab");
        DeathFlare.Prewarm(prefab); var pool = DeathEffectPool.Get(); pool.Prewarm(gib, 4);
        var position = Vector3.up * 10;
        var flare = DeathFlare.SpawnAfterDeath(prefab, position);
        Require(flare.IsBurning, "Immediate protection on downing");
        while (!flare.IsBurning) yield return null;
        Require(DeathFlare.FindNear(flare.transform.position, 2) == flare, "Real OnEnable registers burning flare");
        Require((bool)typeof(DeathFlare).GetField("launched", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flare), "Real Update launches flare");
        pool.Release(flare.gameObject);
        Require(DeathFlare.FindNear(flare.transform.position, 2) == null, "Real OnDisable removes flare protection");
        var again = DeathFlare.SpawnAfterDeath(prefab, position);
        Require(again == flare && again.IsBurning, "Second lease launches immediately");
        pool.Release(again.gameObject);
        var effect = pool.Rent(gib, position, Quaternion.identity); pool.Activate(effect, .1f);
        while (effect.activeSelf) yield return null;
        Require(effect != null && pool.ActiveCount == 0, "Real pool timer parks gib");
        var second = pool.Rent(gib, position, Quaternion.identity); pool.Activate(second, 10);
        Require(second == effect && effect.GetComponent<ParticleSystem>().isPlaying, "Gib plays on reuse");
        pool.Release(second);
        int created = pool.CreatedObjects;
        using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 10000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            Require(gc.Valid, "GC recorder available");
            for (int i = 0; i < 20; i++) {
                var f = DeathFlare.SpawnAfterDeath(prefab, position);
                var g = pool.Rent(gib, position, Quaternion.identity); pool.Activate(g, 10);
                pool.Release(f.gameObject); pool.Release(g);
            }
            gc.Stop();
            Require(gc.Count < gc.Capacity && gc.Count == 0, "Warmed live callbacks must not allocate");
        }
        Require(pool.CreatedObjects == created, "No warmed runtime instantiation");
        var live = DeathFlare.Spawn(prefab, position);
        Require(live.IsBurning, "Live flare before teardown");
        UnityEngine.Object.Destroy(pool.gameObject);
        yield return null;
        while (pool != null) yield return null;
        Require(DeathFlare.FindNear(position, 100) == null, "Teardown removes live protection");
        Require(DeathEffectPool.Get().CreatedObjects == 0, "Scene-owned pool can rebuild cleanly");
        Debug.Log("DeathEffectPlayChecks: ALL PASSED; immediate launch, protection registry, particle restart, expiry; 20 warmed flare+gib cycles = 0 GC allocation events, 0 new effects");
    }

    static void CheckBomberLaunch()
    {
        // Fresh empty batch scene: do not touch a signed-in user's equipment saves.
        Require(!UserPlayerPrefs.IsReady, "Bomber fixture requires no signed-in user");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var prefab = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
        var corpse = UnityEngine.Object.Instantiate(prefab); corpse.Park();
        var playerGo = new GameObject("Bomber victim");
        var player = playerGo.AddComponent<GR.Player>();
        player.ragdoll = corpse;
        player.playerHUD = playerGo.AddComponent<PlayerHUD>();
        var overlay = new GameObject("Death overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        overlay.transform.SetParent(playerGo.transform);
        player.playerHUD.deathEffect = overlay.GetComponent<UnityEngine.UI.Image>();
        playerGo.tag = "Player";
        playerGo.AddComponent<Rigidbody>().isKinematic = true;
        var contact = playerGo.AddComponent<BoxCollider>();
        playerGo.transform.position = Vector3.right * 2;
        var enemyGo = new GameObject("Bomber launch fixture");
        var enemy = enemyGo.AddComponent<Game.AI.Enemy>();
        typeof(Game.AI.Enemy).GetField("playerDeathForce", flags).SetValue(enemy, 8f);
        typeof(Game.AI.Enemy).GetField("playerDeathLift", flags).SetValue(enemy, 4f);
        typeof(Game.AI.Enemy).GetField("playerDeathSpin", flags).SetValue(enemy, 3f);
        var trigger = enemyGo.AddComponent<KillTrigger>();
        var bomber = enemyGo.AddComponent<ZombieBomberExplode>();
        Physics.SyncTransforms();
        Require(trigger.TryContact(contact) && player.IsDead && enemy.IsDormant, "Bomber contact kills and explodes");
        Vector3 impact = (Vector3)player.DeathImpactState;
        Vector3 rotation = (Vector3)player.DeathRotationState;
        Require(rotation.magnitude > 2.6f && rotation.magnitude < 3.4f, "Bomber contact victim gets rotational kick");
        Require(Vector3.Distance(corpse.targetHip.GetComponent<Rigidbody>().angularVelocity, rotation) < .001f, "Contact kick reaches torso");
        Require(impact.x > 7 && impact.y > 3.5f && impact.z == 0, "Initial bomber victim launches outward");
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
            Require(Vector3.Distance(rb.linearVelocity, impact) < .001f, "Contact/explosion applies one launch to every body");
        Require(!trigger.TryContact(contact), "Further contact cannot repeat death");
        player.StopAllCoroutines(); player.IsDead = false; corpse.Park();
        var explode = typeof(ZombieBomberExplode).GetMethod("ApplyExplosion", flags);
        explode.Invoke(bomber, new object[] { 2, Vector3.zero, Quaternion.identity, 1f });
        Require(!player.IsDead, "Outside-radius victim survives");
        playerGo.transform.position = Vector3.left * 2;
        explode.Invoke(bomber, new object[] { 3, Vector3.zero, Quaternion.identity, 6f });
        Require(player.IsDead && ((Vector3)player.DeathImpactState).x < -7, "Splash victim launches away from actual explosion origin");
        Require(((Vector3)player.DeathRotationState).magnitude > 2.6f, "Bomber splash victim gets rotational kick");
        object cached = player.DeathImpactState;
        explode.Invoke(bomber, new object[] { 3, Vector3.zero, Quaternion.identity, 6f });
        player.SetDead("duplicate", Vector3.one * 100);
        Require(ReferenceEquals(cached, player.DeathImpactState), "Duplicate explosion/death cannot replace cached launch");
        player.StopAllCoroutines(); player.IsDead = false; corpse.Park();
        player.SetDead("hazard");
        Require((Vector3)player.DeathImpactState == Vector3.zero, "Next non-zombie death clears old impact");
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
            Require(rb.linearVelocity == Vector3.zero, "Reused corpse has no stale impact");
        player.StopAllCoroutines();
        UnityEngine.Object.DestroyImmediate(enemyGo);
        UnityEngine.Object.DestroyImmediate(playerGo); GR.Player.instance = null;
        UnityEngine.Object.DestroyImmediate(corpse.gameObject);
        Debug.Log("Bomber launch play checks passed: initial contact, splash direction/radius, one impulse, duplicate rejection and next-life reset.");
    }
}
