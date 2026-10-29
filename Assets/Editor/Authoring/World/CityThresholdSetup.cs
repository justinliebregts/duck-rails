using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor-only authoring and real-scene checks for the city model's entrance seams.</summary>
public static class CityThresholdSetup
{
    const string ScenePath = "Assets/Scenes/Quackville.unity";
    const string RampPath = "Assets/Models/Quackville/CityEntranceRamp.asset";
    const string GroupName = "Entrance Traversal Colliders";
    const int Mask = 1 << 6;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Buildings = { "Diner", "Corner_Store", "Police_Station", "Warehouse", "Apartment_Block", "Rear_Tenement", "West_Shop", "Rear_Utility_Block", "Central_Service_Block" };

    struct Entrance
    {
        public string name;
        public Transform building;
        public Vector3 origin, forward;
        public float width, length, rise;
        public Vector3 Point(float across, float along) => building.TransformPoint(origin
            + Vector3.Cross(Vector3.up, forward) * across + forward * along);
        public Quaternion Rotation => building.rotation * Quaternion.LookRotation(forward);
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("City entrance: " + message);
    }

    static Scene OpenScenes()
    {
        Require(Application.isBatchMode, "Run in batch mode so open editor scenes cannot be overwritten.");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Physics.SyncTransforms();
        return scene;
    }

    static List<Entrance> Entrances(out Transform city)
    {
        city = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Single(t => t.name == "Quackville_City" && t.gameObject.scene.path == ScenePath);
        var entries = new List<Entrance>();
        foreach (string name in Buildings)
        {
            var building = city.Find(name);
            Require(building != null, "Missing city building " + name);
            var bounds = building.GetComponent<MeshFilter>().sharedMesh.bounds;
            // All nine shells share a 3 m doorway. The front porch is level with
            // the interior; the rear paving is 8 cm lower. Both have a floor seam.
            entries.Add(new Entrance { name = name + " Front Bridge", building = building,
                origin = new Vector3(bounds.center.x, bounds.min.y + .14f, bounds.min.z + 1.05f),
                forward = Vector3.forward, width = 3.04f, length = .9f });
            entries.Add(new Entrance { name = name + " Rear Ramp", building = building,
                origin = new Vector3(bounds.center.x, bounds.min.y + .06f, bounds.max.z - .85f),
                forward = Vector3.back, width = 3.04f, length = 1.1f, rise = .08f });
        }

        // Two open ground-level bays between the columns on each garage side.
        // Its slab and terrain are level, with a recessed strip between them.
        Transform garage = city.Find("Parking_Garage");
        Require(garage != null, "Missing parking garage");
        foreach (int side in new[] { -1, 1 })
        {
            float x = side < 0 ? -39.025f : -14.975f;
            foreach (float z in new[] { 18.75f, 29.25f })
                entries.Add(new Entrance { name = $"Garage X{side} Z{z} Bridge", building = garage,
                    origin = new Vector3(x + side * .3f, 2f, z), forward = Vector3.left * side,
                    width = 9.69f, length = 1.45f });
            float edge = side < 0 ? 13.075f : 34.925f;
            foreach (float cx in new[] { -32.8f, -21.2f })
                entries.Add(new Entrance { name = $"Garage Z{side} X{cx} Bridge", building = garage,
                    origin = new Vector3(cx, 2f, edge + side * .3f), forward = Vector3.back * side,
                    width = 10.79f, length = 1.45f });
        }
        return entries;
    }

    // Shared closed convex mesh: buried toe, shallow incline and level landing.
    static Mesh RampMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RampPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "City Entrance Ramp" };
            AssetDatabase.CreateAsset(mesh, RampPath);
        }
        var section = new[] { new Vector2(0, -.08f), new Vector2(1.1f, -.08f),
            new Vector2(1.1f, .081f), new Vector2(.75f, .081f), new Vector2(0, -.005f) };
        var vertices = new List<Vector3>();
        foreach (float x in new[] { -.5f, .5f })
            foreach (var p in section) vertices.Add(new Vector3(x, p.y, p.x));
        var triangles = new List<int>();
        for (int i = 1; i < 4; i++)
            triangles.AddRange(new[] { 0, i + 1, i, 5, 5 + i, 6 + i });
        for (int i = 0; i < 5; i++)
        {
            int next = (i + 1) % 5;
            triangles.AddRange(new[] { i, next, 5 + next, i, 5 + next, 5 + i });
        }
        Vector3 center = new Vector3(0, -.01f, .55f);
        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - center) < 0)
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    public static void ApplyAndBake()
    {
        Scene level = OpenScenes();
        var entries = Entrances(out var city);
        Require(city.Find(GroupName) == null && entries.All(e => e.building.Find(GroupName) == null),
            "Entrance colliders already exist; use Validate instead of adding duplicates.");
        Mesh ramp = RampMesh();
        foreach (var e in entries)
        {
            var go = new GameObject(e.name) { layer = 6, isStatic = true };
            go.transform.SetParent(BuildingGroup(e.building), false);
            go.transform.SetPositionAndRotation(e.Point(0, 0), e.Rotation);
            if (e.rise > 0)
            {
                go.transform.localScale = new Vector3(e.width, 1, 1);
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = ramp; collider.convex = true;
            }
            else
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(e.width, .12f, e.length);
                collider.center = new Vector3(0, -.059f, e.length * .5f);
            }
        }
        Physics.SyncTransforms();
        var report = new StringBuilder();
        ValidateGeometry(entries, report);
        ValidateMovement(entries, report);
        Require(EditorSceneManager.SaveScene(level), "Could not save entrance colliders.");
        AssetDatabase.SaveAssets();
        var baker = UnityEngine.Object.FindFirstObjectByType<CrawlGraphBaker>();
        Require(baker != null && baker.surfaceMask.value == Mask, "Missing/mismatched crawl graph baker.");
        Require(CrawlGraphBake.Bake(baker, false), "Crawl graph bake did not finish.");
        ValidateGraph(entries, CrawlGraph.Load(baker.graphAsset), report);
        Require(EditorSceneManager.SaveScene(baker.gameObject.scene), "Could not save bake report.");
        AssetDatabase.SaveAssets();
        report.AppendLine(baker.lastBakeReport);
        File.WriteAllText("city-threshold-checks.log", report.ToString());
        Debug.Log("CITY ENTRANCES PASSED\n" + report);
    }

    public static void Validate()
    {
        OpenScenes();
        var entries = Entrances(out var city);
        var report = new StringBuilder();
        ValidateGeometry(entries, report);
        ValidateMovement(entries, report);
        var baker = UnityEngine.Object.FindFirstObjectByType<CrawlGraphBaker>();
        ValidateGraph(entries, CrawlGraph.Load(baker.graphAsset), report);
        File.WriteAllText("city-threshold-checks.log", report.ToString());
        Debug.Log("CITY ENTRANCES PASSED\n" + report);
    }

    static RaycastHit FloorHit(Vector3 p)
    {
        Require(Physics.Raycast(p + Vector3.up * .35f, Vector3.down, out var hit, .8f, Mask,
            QueryTriggerInteraction.Ignore), "Missing support at " + p);
        Require(hit.normal.y >= CrawlGraph.FloorMinY, $"Unexpected steep support at {p}: {hit.collider.name}, hit {hit.point}, normal {hit.normal}");
        return hit;
    }

    static Vector3 Floor(Vector3 p) => FloorHit(p).point;

    static float[] Lanes(Entrance e) => new[] { -e.width * .5f + .36f, 0, e.width * .5f - .36f };

    static Transform BuildingGroup(Transform building)
    {
        Transform group = building.Find(GroupName);
        if (group != null) return group;
        group = new GameObject(GroupName).transform;
        group.SetParent(building, false);
        return group;
    }

    static void ValidateGeometry(List<Entrance> entries, StringBuilder report)
    {
        var groups = entries.Select(e => e.building).Distinct().Select(b => b.Find(GroupName)).ToArray();
        Require(groups.All(g => g != null) && groups.Sum(g => g.childCount) == entries.Count, "Missing/duplicate entrance colliders.");
        foreach (var group in groups)
        {
            Require(group.GetComponentsInChildren<Renderer>().Length == 0, "Traversal colliders must be invisible.");
            Require(group.GetComponentsInChildren<Rigidbody>().Length == 0 && group.GetComponentsInChildren<MonoBehaviour>().Length == 0,
                "Traversal colliders must have no runtime simulation or scripts.");
        }
        foreach (var e in entries)
        {
            var collider = e.building.Find(GroupName).Find(e.name).GetComponent<Collider>();
            Require(collider.enabled && !collider.isTrigger && collider.gameObject.isStatic
                && collider.gameObject.layer == 6, "Invalid static collision setup: " + e.name);
            Require(Game.Audio.AudioSurface.TypeOf(collider) == Game.Audio.AudioSurface.TypeOf(e.building.GetComponent<Collider>()),
                "Footstep surface changed: " + e.name);
            foreach (float lane in Lanes(e))
            {
                var prior = Floor(e.Point(lane, -.15f));
                for (float along = -.125f; along <= e.length + .15f; along += .025f)
                {
                    var p = Floor(e.Point(lane, along));
                    Require(Mathf.Abs(p.y - prior.y) < .025f, $"Unbridged lip: {e.name} lane {lane} along {along} ({prior.y} -> {p.y})");
                    prior = p;
                }
            }
        }
        report.AppendLine($"Geometry: {entries.Count} entrances; continuous support at center and both edges; static, invisible, no runtime scripts.");
    }

    static void ValidateMovement(List<Entrance> entries, StringBuilder report)
    {
        var actor = new GameObject("Temporary threshold movement check");
        var mover = actor.AddComponent<ClimbLocomotion>();
        EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab")
            .GetComponent<ClimbLocomotion>(), mover);
        var move = typeof(ClimbLocomotion).GetMethod("MoveOnSurface", Private);
        var up = typeof(ClimbLocomotion).GetField("_surfaceUp", Private);
        int count = 0;
        try
        {
            foreach (var e in entries)
            foreach (float lane in Lanes(e))
            foreach (bool reverse in new[] { false, true })
            foreach (float dt in new[] { 1f / 90, 1f / 30 })
            foreach (float speed in new[] { 1.05f, 4.6f })
            {
                var a = Floor(e.Point(lane, -.15f));
                var b = Floor(e.Point(lane, e.length + .15f));
                if (reverse) (a, b) = (b, a);
                mover.Reset(); up.SetValue(mover, FloorHit(a).normal);
                Vector3 targetNormal = FloorHit(b).normal;
                Vector3 pos = a;
                int frames = Mathf.CeilToInt((Vector3.Distance(a, b) / speed + .35f) / dt);
                for (int frame = 0; frame < frames && Vector3.Distance(pos, b) > .07f; frame++)
                    pos = (Vector3)move.Invoke(mover, new object[] { pos, b, targetNormal, speed * dt, (LayerMask)Mask });
                Require(Vector3.Distance(pos, b) < .1f,
                    $"Movement stalled: {e.name}, lane {lane}, reverse {reverse}, dt {dt}, speed {speed}: {pos} -> {b}");
                count++;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
        report.AppendLine($"Spider movement: {count} real-collider crossings passed (both directions, three lanes, stalking/chase speeds, 30/90 Hz). Production movement code and prefab unchanged.");
    }

    static void ValidateGraph(List<Entrance> entries, CrawlGraph graph, StringBuilder report)
    {
        var finder = new CrawlPathfinder(graph);
        int[] path = new int[128];
        int count = 0;
        foreach (var e in entries)
        foreach (float lane in Lanes(e))
        {
            Vector3 a = Floor(e.Point(lane, -.25f)), b = Floor(e.Point(lane, e.length + .25f));
            int from = graph.FindNearestNode(a, .8f, CrawlSurface.Floor);
            int to = graph.FindNearestNode(b, .8f, CrawlSurface.Floor);
            Require(from >= 0 && to >= 0, "Missing graph endpoints: " + e.name);
            foreach (bool reverse in new[] { false, true })
            {
                Require(finder.FindPath(reverse ? to : from, reverse ? from : to, path, out int size, out bool complete) && complete,
                    "Disconnected entrance graph: " + e.name);
                float length = 0;
                for (int i = 1; i < size; i++) length += Vector3.Distance(graph.GetPosition(path[i - 1]), graph.GetPosition(path[i]));
                Require(length < Vector3.Distance(a, b) + 2f, "Entrance path detours around building: " + e.name);
                count++;
            }
        }
        report.AppendLine($"Crawl graph: {count} local routes passed (both directions, center and both edges; no building detours).");
    }
}
