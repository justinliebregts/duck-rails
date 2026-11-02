using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Calls actual SetDead (effects, raycast, pooling and ragdoll), then actual respawn.
// Offline only; synchronous cases finish before the timed respawn coroutine advances.
[InitializeOnLoad]
public static class DeathEffectPlacementPlayChecks
{
    const string Key = "DeathEffectPlacementPlayChecks";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks, cases;
    static GameManager manager;
    static DeathFlare flarePrefab;
    static GameObject bloodPrefab;
    static Ragdoll ragdollPrefab;
    static DeathEffectPlacementPlayChecks()
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
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < .001f, message + ": " + a + " vs " + b);
    static Func<Vector3> Origin(GR.Player p) => (Func<Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3>), p,
        typeof(GR.Player).GetMethod("DeathEffectOrigin", Private));
    static void RunCases()
    {
        try
        {
            checks = cases = 0; Check(!Photon.Pun.PhotonNetwork.InRoom, "Must not send to a live room");
            Physics.simulationMode = SimulationMode.Script;
            var services = new GameObject("Inert placement services"); services.SetActive(false);
            typeof(DuckzAnalytics).GetProperty("Instance").SetValue(null, services.AddComponent<DuckzAnalytics>());
            manager = services.AddComponent<GameManager>(); GameManager.instance = manager;
            manager.respawnPoint = new GameObject("Respawn").transform;
            manager.respawnPoint.SetPositionAndRotation(new Vector3(700, 2, 700), Quaternion.Euler(0, 110, 0));
            HelicopterManager.instance = null;
            flarePrefab = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/DeathFlare.prefab");
            bloodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("ad9e74f5bd397b946b45d0be59033f15"));
            ragdollPrefab = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
            Check(flarePrefab != null && bloodPrefab != null && ragdollPrefab != null, "Authored effect fixtures missing");
            foreach (bool interpolated in new[] { false, true })
            foreach (Vector3 offset in new[] { Vector3.zero, new Vector3(2, 0, -1), new Vector3(-3, 0, 2) })
            foreach (bool ground in new[] { false, true })
            using (var rig = new MovementTestRig())
            {
                NewPlayer.instance = rig.p;
                rig.p.rb.interpolation = interpolated ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                rig.p.headCollider.transform.localPosition = offset + Vector3.up * (ground ? .8f : 1.6f);
                rig.p.Warp(new Vector3(200 + cases * 20, ground ? 0 : 8, 200));
                if (interpolated) { rig.p.Turn(45); rig.p.ApplyPassengerDisplacement(new Vector3(.1f, 0, .2f)); }
                var player = Setup(rig.root);
                Vector3 expected = rig.p.headCollider.transform.position;
                expected.y = rig.root.transform.position.y;
                Execute(player, rig.p, expected, ground, false);

                var origin = Origin(player);
                for (int i = 0; i < 20; i++) origin();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) origin();
                Check(GC.GetAllocatedBytesForCurrentThread() == before, "Death-origin lookup allocated");
                var head = rig.p.headCollider; rig.p.headCollider = null;
                Near(origin(), rig.root.transform.position, "Missing headset must fall back to root"); rig.p.headCollider = head;
            }
            // Keep a different B rig alive to prove desktop cannot use its singleton head.
            using (var foreign = new MovementTestRig())
            {
                NewPlayer.instance = foreign.p; foreign.p.headCollider.transform.localPosition = new Vector3(4, 1.6f, -3);
                foreach (bool ground in new[] { false, true })
                {
                    var root = new GameObject("Desktop placement rig"); root.SetActive(false);
                    var player = Setup(root);
                    var desktop = root.AddComponent<DesktopPlayer>();
                    root.SetActive(true); desktop.enabled = false; player.enabled = false;
                    root.transform.SetPositionAndRotation(new Vector3(200 + cases * 20, ground ? 0 : 8, 200), Quaternion.Euler(0, 70, 0));
                    Near(Origin(player)(), root.transform.position, "Desktop used foreign B headset");
                    Execute(player, null, root.transform.position, ground, true);
                    Object.DestroyImmediate(root);
                }
            }
            Debug.Log($"DeathEffectPlacementPlayChecks PASS: {cases} actual death/respawn cases, {checks} assertions; VR with/without interpolation and desktop, offsets, crouch/airborne, turn/carry, ground/no ground, duplicate death, missing/foreign headset, zero lookup allocations.");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static GR.Player Setup(GameObject root)
    {
        var p = root.AddComponent<GR.Player>(); p.enabled = false; GR.Player.instance = p;
        p.playerHUD = root.AddComponent<PlayerHUD>();
        var image = new GameObject("Death overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        image.transform.SetParent(root.transform); p.playerHUD.deathEffect = image.GetComponent<Image>();
        p.deathFlarePrefab = flarePrefab; p.bloodDecalSpawner = bloodPrefab;
        p.ragdoll = Object.Instantiate(ragdollPrefab); p.ragdoll.Park(); p.ragdoll.Prepare(root.transform);
        return p;
    }
    static void Execute(GR.Player p, NewPlayer movement, Vector3 expected, bool ground, bool desktop)
    {
        GameObject floor = null;
        if (ground)
        {
            floor = new GameObject("Death ray floor"); floor.layer = LayerMask.NameToLayer("Environment");
            floor.AddComponent<BoxCollider>().size = new Vector3(2, .2f, 2);
            floor.transform.position = expected + Vector3.down * .1f;
            NewPlayer.SyncTransformsPreservingMovementPose();
            Check(Physics.Raycast(expected + Vector3.up * .2f, Vector3.down, out var floorHit, 5f,
                LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore),
                "Ground fixture ray missed: layer=" + floor.layer + " mask=" + LayerMask.GetMask("Environment") + " bounds=" + floor.GetComponent<Collider>().bounds);
        }
        manager.usingDesktopPlayer = desktop;
        GR.Player.instance = p;
        Vector3 rootBefore = p.transform.position;
        Vector3 physicsBefore = movement != null ? movement.rb.position : rootBefore;
        int flaresBefore = Object.FindObjectsByType<DeathFlare>(FindObjectsSortMode.None).Length;
        Near(Origin(p)(), expected, "Wrong death origin");
        p.SetDead("hazard"); p.StopAllCoroutines();
        var flares = Object.FindObjectsByType<DeathFlare>(FindObjectsSortMode.None);
        var blood = Object.FindObjectsByType<BloodDecalSpawner>(FindObjectsSortMode.None);
        Check(flares.Length == flaresBefore + 1 && flares.Any(f => Vector3.Distance(f.transform.position, expected + Vector3.up * .35f) < .001f), "Actual flare spawned away from player");
        Vector3 bloodExpected = expected + (ground ? Vector3.up * .01f : Vector3.zero);
        // The bounded blood pool can recycle its oldest live burst; placement, not
        // increasing object count, is the contract under test.
        Check(blood.Any(b => Vector3.Distance(b.transform.position, bloodExpected) < .001f),
            "Actual blood spawn/raycast incorrect in case " + cases + "; expected=" + bloodExpected.ToString("F4")
            + "; active=" + string.Join(",", blood.Select(b => b.transform.position.ToString("F4"))));
        Near(p.transform.position, rootBefore, "Death placement moved root");
        if (movement != null) Near(movement.rb.position, physicsBefore, "Death placement moved physics pose");
        p.SetDead("hazard");
        Check(Object.FindObjectsByType<DeathFlare>(FindObjectsSortMode.None).Length == flares.Length &&
            Object.FindObjectsByType<BloodDecalSpawner>(FindObjectsSortMode.None).Length == blood.Length, "Duplicate death spawned duplicate effects");
        manager.RespawnPlayer();
        Check(!p.IsDead && !p.playerHUD.deathEffect.enabled && !p.ragdoll.gameObject.activeSelf, "Respawn failed");
        Object.DestroyImmediate(p.ragdoll.gameObject); if (floor != null) Object.DestroyImmediate(floor);
        cases++;
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.Exit(code); }
}
