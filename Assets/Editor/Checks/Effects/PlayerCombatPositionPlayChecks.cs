using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Actual bomber damage and enemy contact paths, offline, with authored death assets.
[InitializeOnLoad]
public static class PlayerCombatPositionPlayChecks
{
    const string Key = "PlayerCombatPositionPlayChecks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks, blasts, contacts, revision;
    static DeathFlare flare;
    static GameObject blood;
    static Ragdoll ragdoll;
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static object Get(object o, string field) => o.GetType().GetField(field, Private).GetValue(o);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < .001f, message + ": " + a + " vs " + b);
    static PlayerCombatPositionPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) RunCases();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void RunCases()
    {
        try
        {
            checks = blasts = contacts = revision = 0;
            Check(!Photon.Pun.PhotonNetwork.InRoom, "Must not connect to a live room");
            Physics.simulationMode = SimulationMode.Script;
            var services = new GameObject("Inert combat services"); services.SetActive(false);
            typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, services.AddComponent<DuckzAnalytics>());
            HelicopterManager.instance = null;
            flare = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab");
            blood = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("ad9e74f5bd397b946b45d0be59033f15"));
            ragdoll = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
            Check(flare != null && blood != null && ragdoll != null, "Missing authored death fixtures");
            foreach (bool interpolated in new[] { false, true })
            foreach (Vector3 offset in new[] { Vector3.zero, new Vector3(4, 0, -3), new Vector3(-4, 0, 3) })
            foreach (float height in new[] { .8f, 1.6f })
            using (var rig = new MovementTestRig())
            {
                NewPlayer.instance = rig.p;
                rig.p.rb.interpolation = interpolated ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                rig.p.headCollider.transform.localPosition = offset + Vector3.up * height;
                rig.p.Warp(new Vector3(100, height == .8f ? 0 : 8, 100));
                rig.p.Turn(height == .8f ? 45 : 1.5f);
                rig.p.ApplyPassengerDisplacement(new Vector3(.1f, .2f, -.3f));
                var player = Setup(rig.root);
                rig.p.headCollider.enabled = true;
                NewPlayer.SyncTransformsPreservingMovementPose();
                Vector3 expected = rig.p.headCollider.transform.position;
                expected.y = rig.root.transform.position.y;
                Exercise(player, rig.p, rig.p.headCollider, expected);
                var head = rig.p.headCollider; rig.p.headCollider = null;
                Vector3 fallback = new Vector3(7, 13, -9);
                Near(player.HeadCenteredGameplayPosition(fallback), fallback, "Missing head changed fallback");
                rig.p.headCollider = head;
                Object.DestroyImmediate(player.ragdoll.gameObject);
            }
            // A foreign live B singleton must never offset desktop gameplay.
            using (var foreign = new MovementTestRig())
            {
                NewPlayer.instance = foreign.p; foreign.p.headCollider.transform.localPosition = new Vector3(4, 1.6f, -3);
                foreach (float y in new[] { 0f, 8f })
                {
                    var root = new GameObject("Desktop combat rig"); root.SetActive(false);
                    var player = Setup(root);
                    var desktop = root.AddComponent<DesktopPlayer>();
                    var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true;
                    var contact = root.AddComponent<SphereCollider>(); // Authored desktop contact collider.
                    root.SetActive(true); desktop.enabled = false;
                    root.transform.position = new Vector3(200, y, 200);
                    NewPlayer.SyncTransformsPreservingMovementPose();
                    Exercise(player, null, contact, root.transform.position);
                    Object.DestroyImmediate(player.ragdoll.gameObject); Object.DestroyImmediate(root);
                }
            }
            Debug.Log($"PlayerCombatPositionPlayChecks PASS: {checks} assertions, {blasts} blast cases, {contacts} enemy contact cases; VR with/without interpolation and desktop, room offsets, vertical/radius boundaries, turn/carry, duplicate/old events, guards, report receiver, missing/foreign head and zero lookup allocations.");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static GR.Player Setup(GameObject root)
    {
        root.tag = "Player";
        var p = root.AddComponent<GR.Player>(); GR.Player.instance = p;
        p.playerHUD = root.AddComponent<PlayerHUD>();
        var overlay = new GameObject("Death overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(root.transform); p.playerHUD.deathEffect = overlay.GetComponent<Image>();
        p.deathFlarePrefab = flare; p.bloodDecalSpawner = blood;
        p.ragdoll = Object.Instantiate(ragdoll); p.ragdoll.Park(); p.ragdoll.Prepare(root.transform);
        return p;
    }
    static void Revive(GR.Player p)
    {
        p.StopAllCoroutines(); p.IsDead = false; p.playerHUD.DisableDeathEffect(); p.ragdoll.Park();
    }
    static void Exercise(GR.Player p, NewPlayer movement, Collider collider, Vector3 expected)
    {
        GR.Player.instance = p;
        Check(collider != null && collider.attachedRigidbody != null, "Combat fixture needs an attached player Rigidbody");
        Vector3 beforeRoot = p.transform.position;
        Vector3 beforePhysics = collider.attachedRigidbody.position;
        var source = new GameObject("Combat enemy"); source.SetActive(false);
        var enemy = source.AddComponent<Enemy>();
        Set(enemy, "playerDeathForce", 7f); Set(enemy, "playerDeathLift", 2f);
        Set(enemy, "playerDeathVariation", 0f); Set(enemy, "retreatAfterKill", true);
        var bomber = source.AddComponent<ZombieBomberExplode>();
        source.SetActive(true);
        // Add after bomber OnEnable so contact tests do not trigger a second explosion.
        var trigger = source.AddComponent<KillTrigger>(); int listeners = 0;
        trigger.PlayerKilled += () => listeners++;
        var explode = (Action<int, Vector3, Quaternion, float>)Delegate.CreateDelegate(
            typeof(Action<int, Vector3, Quaternion, float>), bomber,
            typeof(ZombieBomberExplode).GetMethod("ApplyExplosion", Private));
        Vector3 fallback = beforeRoot + new Vector3(0, 3, 0);
        Vector3 expectedFallback = expected; expectedFallback.y = fallback.y;
        Near(p.HeadCenteredGameplayPosition(fallback), expectedFallback, "Gameplay lookup changed caller height");
        for (int i = 0; i < 20; i++) p.HeadCenteredGameplayPosition(fallback);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) p.HeadCenteredGameplayPosition(fallback);
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Gameplay lookup allocated");

        void Blast(Vector3 origin, float radius, bool lethal, string label)
        {
            Revive(p); int id = ++revision;
            explode(id, origin, Quaternion.identity, radius); p.StopAllCoroutines(); blasts++;
            Check(p.IsDead == lethal, label + " wrong kill decision; expected " + lethal);
            if (lethal) Near((Vector3)p.DeathImpactState, enemy.PlayerDeathLaunch(expected, origin), label + " wrong launch");
            Near(p.transform.position, beforeRoot, label + " moved root");
            Near(collider.attachedRigidbody.position, beforePhysics, label + " moved Rigidbody");
            Revive(p);
            explode(id, origin, Quaternion.identity, 100f);
            explode(id - 1, origin, Quaternion.identity, 100f);
            Check(!p.IsDead, label + " duplicate/old explosion killed respawned player");
        }
        Blast(expected + Vector3.right * .999f, 1, true, "Inside radius");
        Blast(expected + Vector3.right, 1, true, "Exact boundary");
        Blast(expected + Vector3.right * 1.001f, 1, false, "Outside radius");
        Blast(expected + Vector3.up * 1.001f, 1, false, "Vertical separation");
        Blast(expected, 0, true, "Zero radius exact position");
        Blast(expected, -1, true, "Negative radius clamped");
        Blast(expected + Vector3.forward * .01f, 0, false, "Zero radius miss");
        Blast(beforeRoot, 1, (expected - beforeRoot).sqrMagnitude <= 1, "Old root location");
        Vector3 headGround = movement != null ? movement.headCollider.transform.position : expected;
        headGround.y = expected.y;
        Blast(headGround, 1, (expected - headGround).sqrMagnitude <= 1, "Actual horizontal head location");

        p.enabled = false; explode(++revision, expected, Quaternion.identity, 10); Check(!p.IsDead, "Disabled player killed"); p.enabled = true;
        GR.Player.instance = null; explode(++revision, expected, Quaternion.identity, 10); GR.Player.instance = p;
        Check(!p.IsDead, "Missing player changed death state");
        p.IsDead = true; object impactBefore = p.DeathImpactState;
        explode(++revision, expected, Quaternion.identity, 10);
        Check(ReferenceEquals(impactBefore, p.DeathImpactState), "Already dead player relaunched"); Revive(p);

        source.transform.position = expected + new Vector3(2, -1, 1);
        enemy.IsDormant = true;
        Check(!trigger.TryContact(collider) && !p.IsDead && listeners == 0, "Dormant enemy killed player"); enemy.IsDormant = false;
        Set(enemy, "_localMeleeGraceUntil", Time.time + 10);
        Check(!trigger.TryContact(collider) && !p.IsDead, "Melee grace bypassed"); Set(enemy, "_localMeleeGraceUntil", -1f);
        Check(trigger.TryContact(collider) && p.IsDead && listeners == 1, "Enemy contact was not accepted once");
        p.StopAllCoroutines(); contacts++;
        Near((Vector3)p.DeathImpactState, enemy.PlayerDeathLaunch(expected, source.transform.position), "Contact launch used root");
        Vector3 report = expected; report.y = beforePhysics.y;
        if (movement == null) report = beforePhysics;
        Near((Vector3)Get(enemy, "_killLocation"), report, "Enemy received wrong kill report position");
        Check(enemy.CurrentState == Enemy.State.Retreat, "Kill report failed to reach enemy receiver");
        Check(!trigger.TryContact(collider) && listeners == 1, "Duplicate contact accepted");
        Near(p.transform.position, beforeRoot, "Contact changed root");
        Near(collider.attachedRigidbody.position, beforePhysics, "Contact changed physics pose");
        Revive(p); Object.DestroyImmediate(source);
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.Exit(code); }
}
