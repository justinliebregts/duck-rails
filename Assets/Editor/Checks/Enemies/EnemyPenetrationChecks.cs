using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Disposable batch project only. Real mover shapes, recovery limits and warmed cost.</summary>
public static partial class EnemyPenetrationChecks
{
    const int Mask = 1 << 6;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static readonly string[] Types = { "Bomber", "Smasher", "Minion", "Spider" };
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    sealed class Body : IDisposable
    {
        public readonly GameObject actor;
        public readonly GroundLocomotion ground;
        public readonly ClimbLocomotion climb;
        public readonly Vector3 scale;
        public Body(string type)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie" + type + ".prefab");
            actor = new GameObject(type + " recovery check");
            var root = Get<Transform>(prefab.GetComponent<Enemy>(), "root");
            scale = root != null ? root.lossyScale : prefab.transform.lossyScale;
            actor.transform.localScale = scale;
            if (type == "Spider")
            {
                climb = actor.AddComponent<ClimbLocomotion>();
                EditorUtility.CopySerialized(prefab.GetComponent<ClimbLocomotion>(), climb);
            }
            else
            {
                ground = actor.AddComponent<GroundLocomotion>();
                EditorUtility.CopySerialized(prefab.GetComponent<GroundLocomotion>(), ground);
            }
        }
        public float Radius { get { if (ground != null) { ground.GetCapsuleSize(out float r, out _); return r; } return (float)Call(climb, "WorldRadius"); } }
        public Collider Helper => ground != null ? Get<CapsuleCollider>(ground, "_queryCapsule") : Get<SphereCollider>(climb, "_querySphere");
        public Vector3 Contact(Vector3 point, Vector3 normal)
        {
            if (ground != null) { ground.Reset(); return ground.RootFromCapsuleBottom(ground.CapsuleBottomOnSurface(point, normal)); }
            actor.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            climb.SyncUpFromRoot(actor.transform);
            return point;
        }
        public bool Separate(ref Vector3 point, int mask) => ground != null ? ground.Separate(ref point, mask) : climb.Separate(ref point, mask);
        public bool Overlap(Vector3 point, int mask) => ground != null ? ground.Overlaps(point, mask) : climb.Overlaps(point, mask);
        public void Dispose()
        {
            Collider helper = Helper;
            // These synthetic Edit Mode components do not receive runtime callbacks.
            Call((object)ground ?? climb, "OnDestroy");
            Object.DestroyImmediate(actor);
            Check(helper == null, "Destroying an actor must destroy its recovery helper");
        }
    }

    static Mesh FloorMesh()
    {
        var mesh = new Mesh {
            vertices = new[] { new Vector3(-10,0,-10), new Vector3(-10,0,10), new Vector3(10,0,10), new Vector3(10,0,-10) },
            triangles = new[] { 0,1,2, 0,2,3 }
        };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only.");
        checks = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Mesh mesh = FloorMesh();
        var terrain = new GameObject("Recovery mesh") { layer = 6 };
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        int cases = 0;
        try
        {
            foreach (string type in Types)
            using (var body = new Body(type))
            {
                Vector3 clear = Vector3.up * 5;
                Check(body.Separate(ref clear, Mask) && body.Helper == null, type + " clear movement must not create a helper");
                foreach (float scale in new[] { .75f, 1f, 1.5f })
                foreach (float angle in new[] { 0f, 31f, 50f })
                foreach (float fraction in new[] { .1f, .35f, .7f })
                foreach (float offset in new[] { 0f, .3f })
                {
                    body.actor.transform.localScale = body.scale * scale;
                    terrain.transform.rotation = Quaternion.Euler(angle, 0, 0);
                    terrain.transform.position = new Vector3(13, -2, 7);
                    Physics.SyncTransforms();
                    Vector3 normal = terrain.transform.up;
                    Vector3 contact = body.Contact(terrain.transform.TransformPoint(new Vector3(offset, 0, 0)), normal);
                    Vector3 start = contact - normal * (.025f + body.Radius * fraction);
                    Vector3 point = start;
                    Check(body.Overlap(point, Mask), "Fixture must penetrate: " + type);
                    Check(body.Separate(ref point, Mask) && !body.Overlap(point, Mask),
                        $"Recovery failed: {type}, scale={scale}, angle={angle}, depth/radius={fraction}, seamOffset={offset}");
                    Check(Vector3.Distance(point, contact) < .005f, "Recovery must return to the actual supporting surface");
                    Check(!body.Helper.enabled && body.Helper.isTrigger, "Recovery helper must be disabled and ignored by safety queries");
                    Vector3 again = point;
                    Check(body.Separate(ref again, Mask) && Vector3.Distance(point, again) < .00001f, "Clear recovery must be idempotent");
                    cases++;
                }
                terrain.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                body.actor.transform.localScale = body.scale;
                Physics.SyncTransforms();
                Vector3 embedded = body.Contact(Vector3.zero, Vector3.up) - Vector3.up * .08f;
                Vector3 allLayers = embedded;
                Check(body.Separate(ref allLayers, ~0) && !body.Overlap(allLayers, ~0), "Helper cannot obstruct an all-layer mask at the origin");
                WarmedCost(body, embedded);
                terrain.SetActive(false); Physics.SyncTransforms();
                Vector3 removed = embedded;
                Check(body.Separate(ref removed, Mask) && removed == embedded, "Removed floor cannot cause a stale correction");
                Failures(body);
                terrain.SetActive(true); Physics.SyncTransforms();
            }
            Debug.Log($"EnemyPenetrationChecks: ALL PASSED ({cases} slope/scale/depth/seam cases; {checks} assertions)");
        }
        finally { Object.DestroyImmediate(terrain); Object.DestroyImmediate(mesh); }
    }

    static void WarmedCost(Body body, Vector3 embedded)
    {
        for (int i = 0; i < 20; i++) { Vector3 p = embedded; body.Separate(ref p, Mask); }
        Collider helper = body.Helper;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        bool passed = true;
        for (int i = 0; i < 1000; i++)
        {
            Vector3 p = embedded;
            passed &= body.Separate(ref p, Mask);
        }
        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - began;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(passed && allocated == 0, "Repeated recovery must work with zero warmed managed allocations: " + allocated);
        Check(body.Helper == helper && !helper.enabled, "Recovery must reuse and retire the same helper");
        Debug.Log($"RECOVERY COST {body.actor.name}: 1000 repairs, {allocated} managed bytes, {elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / 1000:F3} us/repair (Editor CPU)");
    }

    static void Failures(Body body)
    {
        var blockers = new GameObject[40];
        try
        {
            // Saturating the non-alloc buffer must fail closed and retire the helper.
            for (int i = 0; i < blockers.Length; i++)
            {
                var blocker = blockers[i] = new GameObject("Crowded overlap") { layer = 6 };
                blocker.transform.position = Vector3.up * body.Radius;
                blocker.AddComponent<BoxCollider>().size = Vector3.one * .1f;
            }
            Physics.SyncTransforms();
            Vector3 point = Vector3.zero;
            Check(!body.Separate(ref point, Mask) && point == Vector3.zero && !body.Helper.enabled,
                "Full query buffer must not commit a partial correction or leave a helper enabled");
            foreach (var blocker in blockers) Object.DestroyImmediate(blocker);
            var wall = blockers[0] = new GameObject("Deep solid") { layer = 6 };
            wall.AddComponent<BoxCollider>().size = Vector3.one * 10;
            Physics.SyncTransforms();
            point = Vector3.zero;
            Check(!body.Separate(ref point, Mask) && point == Vector3.zero && !body.Helper.enabled,
                "Excessive recovery distance must not teleport through a solid or leave a helper enabled");
        }
        finally { foreach (var blocker in blockers) if (blocker != null) Object.DestroyImmediate(blocker); }
    }
}
