using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-only checks against Quackville's real colliders and Smasher capsule settings.</summary>
public static class SmasherTerrainChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, P).Invoke(o, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
        bool isolated = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/ActualLevel/Quackville.unity") != null;
        EditorSceneManager.OpenScene(isolated ? "Assets/ActualLevel/Quackville.unity" : "Assets/Scenes/Main.unity");
        Physics.SyncTransforms();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(isolated
            ? "Assets/ActualLevel/ZombieSmasher.prefab" : "Assets/Prefabs/Enemies/ZombieSmasher.prefab");
        var actor = UnityEngine.Object.Instantiate(prefab);
        var ground = actor.GetComponent<GroundLocomotion>();
        var enemy = actor.GetComponent<Enemy>();
        if (enemy == null) enemy = actor.AddComponent<Enemy>();
        Call(enemy, "Awake");
        ground.GetCapsuleSize(out float radius, out float height);
        // Reported cave freezes: a completed fall must also count as supported
        // on the following retreat tick, including mesh seams near these points.
        foreach (Vector3 cave in new[] {
            new Vector3(-31.28f, -4.09f, 23.55f),
            new Vector3(-39.95f, -2.85f, 23.55f),
            new Vector3(-50.67f, -0.71f, 19.52f) })
        {
            for (int sample = -2; sample <= 2; sample++)
            {
                ground.Reset();
                Vector3 position = cave + Vector3.right * (sample * 0.05f) + Vector3.up * 0.3f;
                float speed = 0f;
                bool landed = false;
                for (int tick = 0; tick < 200 && !landed; tick++)
                {
                    landed = ground.TickFall(ref position, ref speed, 0.02f, 1 << 6, out Vector3 normal);
                    if (landed)
                        position = ground.AdoptLanding(ground.SupportedCapsuleBottom(position, 1 << 6), normal);
                }
                Check(landed, "Cave fall must land near " + cave);
                Check(ground.HasGroundSupport(position, 1 << 6),
                    "Completed cave landing must remain supported at " + position);
            }
        }
        Debug.Log($"Actual Smasher capsule: radius={radius}, height={height}, scale={actor.transform.lossyScale}");
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
            Debug.Log($"Smasher mesh {renderer.name}: relative bounds min={renderer.bounds.min - actor.transform.position}, max={renderer.bounds.max - actor.transform.position}");
        int trials = 0;
        foreach (Vector3 location in new[] {
            new Vector3(-14.321f,30,45.748f), new Vector3(52.885f,30,-51.410f),
            new Vector3(1.733f,30,-89.932f), new Vector3(65.944f,30,-92.737f), new Vector3(87.286f,30,23.760f) })
        {
            Check(Physics.Raycast(location, Vector3.down, out RaycastHit floor, 100, 1 << 6,
                QueryTriggerInteraction.Ignore), "Actual terrain must load at " + location);
            if (!CrawlGraph.IsWalkableGround(floor.normal)) continue;
            Vector3 start = ground.RootFromCapsuleBottom(ground.CapsuleBottomOnSurface(floor.point, floor.normal));
            for (int heading = 0; heading < 8; heading++)
            {
                actor.transform.position = start; ground.Reset();
                Vector3 direction = Quaternion.Euler(0, heading * 45, 0) * Vector3.forward;
                for (int i = 0; i < 250; i++)
                {
                    ground.ValidateSmasherPose(enemy, 1 << 6);
                    // This test exercises surface movement; stop at genuine edges.
                    if (!ground.HasGroundSupport(actor.transform.position, 1 << 6)) break;
                    actor.transform.position = (Vector3)Call(ground, "MoveWithWalls", actor.transform.position,
                        direction * .16f, (LayerMask)(1 << 6));
                    Vector3 bottom = ground.SupportedCapsuleBottom(actor.transform.position, 1 << 6);
                    Check(!Physics.CheckCapsule(bottom, bottom + Vector3.up * (height - 2 * radius), radius,
                        1 << 6, QueryTriggerInteraction.Ignore), "Embedded during real-terrain movement at " + actor.transform.position);
                    Check(actor.transform.position.y > -20, "Fell below the level");
                }
                trials++;
            }
        }
        Check(trials > 0, "At least one real terrain trial");
        // Exercise the actual prefab's body driver with deliberately buried
        // smoothing state. This does not rely on the root collider to move the mesh.
        var gait = actor.GetComponent<Game.Animation.GorillaGait>();
        Check(gait != null, "Actual prefab gait must load");
        Check(Physics.Raycast(new Vector3(52.885f,30,-51.410f), Vector3.down, out RaycastHit bodyFloor,
            100, 1 << 6), "Body floor");
        actor.transform.position = bodyFloor.point;
        typeof(Game.Animation.GorillaGait).GetField("_bodyPos", P).SetValue(gait, bodyFloor.point - Vector3.up);
        Call(gait, "UpdateBody", .02f);
        Transform body = (Transform)typeof(Game.Animation.GorillaGait).GetField("body", P).GetValue(gait);
        Check(body.position.y >= bodyFloor.point.y + .199f, "Body smoothing cannot leave the mesh buried");
        UnityEngine.Object.DestroyImmediate(actor);
        Debug.Log($"SmasherTerrainChecks: ALL PASSED ({trials} real-terrain routes)");
    }
}
