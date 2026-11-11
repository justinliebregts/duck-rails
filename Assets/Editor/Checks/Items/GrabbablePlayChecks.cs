using System;
using System.Collections;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GrabbablePlayChecks
{
    const string Key = "GrabbablePlayChecks.Run";
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator checks;
    static double started;
    static GrabbablePlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup; checks = RunChecks(); EditorApplication.update += Step;
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        SessionState.SetBool(Key, true); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Step()
    {
        try { if (EditorApplication.timeSinceStartup - started > 30) throw new Exception("Timeout");
            if (checks.MoveNext()) return; Finish(0);
        } catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Step; EditorApplication.Exit(code); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static IEnumerator RunChecks()
    {
        GR.Player.instance = null;
        var enemyGo = new GameObject("Sweep target"); var enemy = enemyGo.AddComponent<Enemy>();
        typeof(Enemy).GetField("root", P).SetValue(enemy, enemyGo.transform);
        // Verify contact recognition without invoking unrelated enemy knockback simulation.
        typeof(Enemy).GetField("_nextHitTime", P).SetValue(enemy, float.PositiveInfinity);
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>(); typeof(PhotonView).GetProperty("IsMine").SetValue(view, true);
        view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        var handGo = new GameObject("Moving hand"); var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
        item.transform.position = new Vector3(-1, .5f, 0); item.Grab(hand);
        // OnDisable intentionally cancels the hold. Keep the real lifecycle active
        // while checking idle/restarted sweeps; disabling here invalidates the grab.
        Check(item.IsHeld, "Actual grab");
        var swing = (Action)Delegate.CreateDelegate(typeof(Action), item, typeof(PhotonGrabbable).GetMethod("TickSwing", P));
        swing(); PhotonGrabbable.ResetProfiling();
        for (int i = 0; i < 10; i++) { yield return null; swing(); }
        Check(item.IsHeld && item.transform.IsChildOf(hand.HoldPoint), "Idle fixture must remain held");
        Check(PhotonGrabbable.IdleSkips > 0 && !enemy.SwingLocked, "Idle hand does not hit");
        yield return null;
        handGo.transform.position += Vector3.right * 2; Physics.SyncTransforms(); swing();
        Check(enemy.SwingLocked, "First moving frame after idle must hit");
        // Throw still has a valid sweep after sleeping/idle. No IsSleeping gate is used.
        item.Release(Vector3.right * 3, Vector3.zero);
        item.GetComponent<Rigidbody>().isKinematic = true;
        item.transform.position = new Vector3(-1, .5f, 0);
        typeof(PhotonGrabbable).GetField("_hasSamplePrev", P).SetValue(item, false);
        typeof(Enemy).GetField("_localSwingLock", P).SetValue(enemy, 0f); swing();
        yield return null; swing();
        item.transform.position += Vector3.right * 2; Physics.SyncTransforms(); swing();
        Check(enemy.SwingLocked, "Thrown item first movement still hits");
        var failures = new System.Collections.Generic.List<string>();
        foreach (var test in new Action[] { () => ThrowThroughPair(false), () => ThrowThroughPair(true),
            () => ThrowThroughPair(false, true),
            () => ExplodedBomber(0), () => ExplodedBomber(2), () => ExplodedBomber(3) })
        {
            try { test(); } catch (Exception e) { failures.Add(e.ToString()); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Debug.Log("GrabbablePlayChecks: ALL PASSED; idle and moving held/thrown items, frontmost contact in both spawn orders, dormant and respawned Bomber");
    }

    static Enemy Spawn(string type)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Zombie{type}.prefab");
        return UnityEngine.Object.Instantiate(prefab, new Vector3(40, 10, 0), Quaternion.identity).GetComponent<Enemy>();
    }

    static Vector3 ShapePoint(Enemy enemy, int index, string endpoint)
    {
        var shape = new SerializedObject(enemy).FindProperty("hitShapes").GetArrayElementAtIndex(index);
        var bone = (Transform)shape.FindPropertyRelative(endpoint).objectReferenceValue;
        return bone.TransformPoint(shape.FindPropertyRelative(endpoint + "Offset").vector3Value);
    }

    static void ThrowBat(Vector3 point, Enemy expected, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab");
        var bat = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var item = bat.GetComponent<PhotonGrabbable>();
            typeof(PhotonView).GetProperty("IsMine").SetValue(bat.GetComponent<PhotonView>(), true);
            var sounds = bat.GetComponent<ItemSounds>();
            Check(sounds.FleshImpactReady, "Bat impact sound armed");
            var samples = new Vector3[Grabbable.MaxLocomotionSamples];
            int count = item.CopySampleWorldPositions(samples);
            Check(count > 0, "Bat has sweep samples");
            bat.transform.position += point - samples[count - 1] - Vector3.forward * .8f;
            var swing = (Action)Delegate.CreateDelegate(typeof(Action), item, typeof(PhotonGrabbable).GetMethod("TickSwing", P));
            swing();
            bat.transform.position += Vector3.forward * 2.4f;
            swing();
            var actual = (Enemy)typeof(PhotonGrabbable).GetField("_thrownIgnore", P).GetValue(item);
            Check(actual == expected, label + " expected " + (expected ? expected.name : "no contact")
                + ", got " + (actual ? actual.name : "no contact"));
            Check((bat.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > 0f) == (expected != null), label + " deflection");
            Check(sounds.FleshImpactReady == (expected == null), label + " flesh sound");
            if (expected != null) Check(expected.SwingLocked, label + " accepted hit");
            Debug.Log("PASS " + label);
        }
        finally { UnityEngine.Object.DestroyImmediate(bat); }
    }

    static void ThrowThroughPair(bool rearFirst, bool frontOnCooldown = false)
    {
        Enemy first = Spawn(rearFirst ? "Minion" : "Smasher");
        Enemy second = Spawn(rearFirst ? "Smasher" : "Minion");
        try
        {
            Enemy front = rearFirst ? second : first, rear = rearFirst ? first : second;
            Vector3 forearm = Vector3.Lerp(ShapePoint(front, 3, "start"), ShapePoint(front, 3, "end"), .6f);
            rear.SetRootPose(rear.Root.position + forearm + Vector3.forward * .9f - ShapePoint(rear, 0, "start"), Quaternion.identity);
            if (frontOnCooldown)
            {
                front.LockSwing();
                ThrowBat(forearm, rear, "Front on cooldown allows the rear hit");
            }
            else
            {
                ThrowBat(forearm, front, "First forearm hit, rear spawned first=" + rearFirst);
                Check(!rear.SwingLocked, "Rear zombie did not receive a second hit");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(first.gameObject); UnityEngine.Object.DestroyImmediate(second.gameObject); }
    }

    static void ExplodedBomber(int respawnShape)
    {
        Enemy bomber = Spawn("Bomber"), rear = Spawn("Minion");
        try
        {
            var explode = bomber.GetComponent<ZombieBomberExplode>();
            typeof(ZombieBomberExplode).GetMethod("Explode", P).Invoke(explode, null);
            Check(bomber.IsDormant, "Explosion puts the Bomber into dormancy");
            Vector3 belly = ShapePoint(bomber, 0, "start");
            if (respawnShape == 0)
            {
                rear.SetRootPose(belly + Vector3.right * 8, Quaternion.identity);
                ThrowBat(belly, null, "Dormant belly causes no hit, sound or deflection");
                rear.SetRootPose(rear.Root.position + belly + Vector3.forward * .9f - ShapePoint(rear, 0, "start"), Quaternion.identity);
                ThrowBat(belly, rear, "Throw through dormant belly reaches rear zombie");
                Check(!bomber.SwingLocked, "Dormant body cannot consume a swing lock");
            }
            // The authority publishes the new pose through this same transition.
            Vector3 next = bomber.Root.position + Vector3.right * 4;
            typeof(ZombieBomberExplode).GetMethod("PublishState", P).Invoke(explode,
                new object[] { false, 0d, next, Quaternion.identity });
            Check(!bomber.IsDormant, "Respawn restores the Bomber");
            Vector3 target = ShapePoint(bomber, respawnShape, respawnShape == 0 ? "start" : "end");
            if (respawnShape != 0) target += Vector3.right * Mathf.Sign(target.x - next.x) * .05f;
            ThrowBat(target, bomber, "Respawned Bomber shape " + respawnShape);
        }
        finally { UnityEngine.Object.DestroyImmediate(bomber.gameObject); UnityEngine.Object.DestroyImmediate(rear.gameObject); }
    }
}
