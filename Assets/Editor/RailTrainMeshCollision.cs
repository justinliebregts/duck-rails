#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Offline collision from the reviewed low-poly train, without convex caps over openings.</summary>
public static class RailTrainMeshCollision
{
    public const string RootName = "Model shell collision";
    static readonly string[] LegacyRoots = {
        "Hull and deck", "Side rails - rear stays open", "Cockpit shell",
        "Yard Mule shell", "Blender supplemental collision"
    };
    static readonly string[] BodyParts = {
        "Deck and chassis", "Single wood deck", "Boarding rails", "Canopy supports",
        "Nose left", "Nose right", "Hazard bumper", "Dash to furnace cowl",
        "Cowl side transitions", "Enclosed lower nose", "Cockpit furnace cover"
    };

    static RailBlenderTrainImport.Model ReadModel() => JsonUtility.FromJson<RailBlenderTrainImport.Model>(
        File.ReadAllText(RailBlenderTrainImport.Folder + "/unity-integration.json"));

    static void RequireEditor()
    {
        if (EditorApplication.isPlaying || Application.dataPath != "X:/Work/duck-rails/Assets")
            throw new InvalidOperationException("Duck Rails Edit Mode required");
    }

    // Weld UV/material splits and omit zero-area faces; keep the authored silhouette exactly.
    static Mesh SaveCollision(string name, IEnumerable<RailBlenderTrainImport.Part> parts, Matrix4x4 matrix)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var lookup = new Dictionary<Vector3, int>();
        foreach (var part in parts)
            for (int t = 0; t < part.triangles.Length; t += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(part.vertices[part.triangles[t]]);
                Vector3 b = matrix.MultiplyPoint3x4(part.vertices[part.triangles[t + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(part.vertices[part.triangles[t + 2]]);
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-14f) continue;
                foreach (var point in new[] { a, b, c })
                {
                    if (!lookup.TryGetValue(point, out int index))
                    { index = vertices.Count; vertices.Add(point); lookup.Add(point, index); }
                    indices.Add(index);
                }
            }
        string path = RailBlenderTrainImport.Folder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void AddMesh(Transform parent, string name, Mesh mesh)
    {
        var child = parent.Find(name);
        if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.gameObject.layer = 6;
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        var collider = child.GetComponent<MeshCollider>();
        if (!collider) collider = child.gameObject.AddComponent<MeshCollider>();
        collider.convex = false; collider.isTrigger = false; collider.sharedMesh = mesh; collider.enabled = true;
    }

    public static void Apply(GameObject root, RailBlenderTrainImport.Model model)
    {
        RequireEditor();
        if (root.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            throw new InvalidOperationException("Train support expects transform-driven colliders without rigidbodies");
        foreach (var path in LegacyRoots)
            foreach (var collider in root.transform.Find(path).GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger) collider.enabled = false;
        var collisionRoot = root.transform.Find(RootName);
        if (!collisionRoot) { collisionRoot = new GameObject(RootName).transform; collisionRoot.SetParent(root.transform, false); }
        var source = model.parts.ToDictionary(p => p.name);
        AddMesh(collisionRoot, "Body", SaveCollision("Model body collision", BodyParts.Select(n => source[n]), Matrix4x4.identity));
        // These two reviewed meshes are already low-poly. Reuse their exact faces, not a convex hull.
        foreach (string name in new[] { "Canopy and spotlight", "Wheels" })
            AddMesh(collisionRoot, name, root.transform.Find(RailBlenderTrainImport.VisualRoot + "/" + name).GetComponent<MeshFilter>().sharedMesh);
        var housing = root.GetComponent<RailTrainAssembly>().controls.Find("Integrated dash art/Integrated housing");
        var housingCollider = housing.GetComponent<MeshCollider>();
        housingCollider.convex = false;
        housingCollider.sharedMesh = SaveCollision("Model dash collision", new[] { source["Integrated housing"] },
            housing.worldToLocalMatrix * root.transform.localToWorldMatrix);
        housingCollider.enabled = true;
    }

    [MenuItem("Duck Rails/Train/Install Model Shell Collision")]
    public static void Install()
    {
        RequireEditor();
        var root = PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try
        {
            // No visual, gameplay or interaction-trigger edits are needed for this migration.
            var bindings = root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null)
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            var triggers = root.GetComponentsInChildren<Collider>(true).Where(c => c.isTrigger)
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            Apply(root, ReadModel());
            foreach (var pair in bindings)
                if (EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Changed component " + pair.Key);
            foreach (var pair in triggers)
                if (EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Changed trigger " + pair.Key);
            PrefabUtility.SaveAsPrefabAsset(root, RailYardMuleArt.Train);
            Debug.Log("Model shell collision installed; all gameplay components and triggers preserved");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Duck Rails/Train/Validate Model Shell Collision")]
    public static void Validate()
    {
        RequireEditor();
        var lines = new List<string> { DateTime.UtcNow.ToString("O") };
        Action<bool, string> check = (ok, message) => {
            if (!ok) throw new InvalidOperationException(message);
            lines.Add("PASS " + message);
        };
        var root = PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try
        {
            foreach (var path in LegacyRoots)
                check(root.transform.Find(path).GetComponentsInChildren<Collider>(true).All(c => c.isTrigger || !c.enabled), "Old solid collision disabled: " + path);
            var shell = root.transform.Find(RootName).GetComponentsInChildren<MeshCollider>();
            check(shell.Length == 3 && shell.All(c => c.enabled && !c.convex && !c.isTrigger && c.sharedMesh && c.gameObject.layer == 6 && c.attachedRigidbody == null), "Three model-aligned non-convex shell meshes on support layer, no rigidbodies");
            foreach (string name in new[] { "Canopy and spotlight", "Wheels" })
                check(root.transform.Find(RootName + "/" + name).GetComponent<MeshCollider>().sharedMesh == root.transform.Find(RailBlenderTrainImport.VisualRoot + "/" + name).GetComponent<MeshFilter>().sharedMesh, name + " collision uses the rendered mesh");
            var model = ReadModel();
            var body = root.transform.Find(RootName + "/Body").GetComponent<MeshCollider>();
            var points = body.sharedMesh.vertices; var triangles = body.sharedMesh.triangles;
            var expected = BodyParts.Select(name => model.parts.Single(p => p.name == name)).ToArray();
            var faces = new List<Vector3>();
            foreach (var p in expected)
                for (int i = 0; i < p.triangles.Length; i += 3)
                {
                    var a = p.vertices[p.triangles[i]]; var b = p.vertices[p.triangles[i + 1]]; var c = p.vertices[p.triangles[i + 2]];
                    if (Vector3.Cross(b - a, c - a).sqrMagnitude >= 1e-14f) faces.AddRange(new[] { a, b, c });
                }
            check(triangles.Select(i => points[i]).SequenceEqual(faces), "Body retains every non-degenerate structural face with exact positions and winding");
            lines.Add("Source zero-area triangles omitted: " + (expected.Sum(p => p.triangles.Length) - faces.Count) / 3);
            var solids = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            var assembly = root.GetComponent<RailTrainAssembly>();
            check(root.GetComponent<RailTrain>().cabin && assembly.furnace && assembly.fuelGauge && assembly.results
                && assembly.supplyPoints.Length == 12 && assembly.supplyPoints.All(p => p), "Passenger, furnace, gauge, results and twelve supply bindings retained");
            check(root.GetComponentsInChildren<RailControl>(true).All(c => c.GetComponentInChildren<Collider>(true).isTrigger), "Drive, horn and radio interaction triggers retained");
            check(root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
            foreach (var pose in new[] { Vector3.zero, new Vector3(71, 3, -52) })
            {
                root.transform.SetPositionAndRotation(pose, pose == Vector3.zero ? Quaternion.identity : Quaternion.Euler(8, 67, -4));
                Physics.SyncTransforms();
                foreach (float x in new[] { -1.5f, 0, 1.5f })
                    foreach (float z in new[] { -4f, -2f, 0f, 2f })
                    {
                        var ray = new Ray(root.transform.TransformPoint(new Vector3(x, .4f, z)), -root.transform.up);
                        check(body.Raycast(ray, out var hit, .6f) && Mathf.Abs(root.transform.InverseTransformPoint(hit.point).y - model.parts.Single(p => p.name == "Single wood deck").vertices[0].y) < .002f, "Deck surface contact " + x + "," + z + " at pose " + pose);
                    }
                foreach (int side in new[] { -1, 1 })
                {
                    var ray = new Ray(root.transform.TransformPoint(new Vector3(side * 3, .3f, 0)), -side * root.transform.right);
                    check(!solids.Any(c => c.Raycast(ray, out _, 1.3f)), "Side boarding gap clear " + side + " at pose " + pose);
                }
                foreach (float x in new[] { -1f, 0, 1f })
                {
                    var ray = new Ray(root.transform.TransformPoint(new Vector3(x, .3f, -5.5f)), root.transform.forward);
                    check(!solids.Any(c => c.Raycast(ray, out _, 2)), "Rear boarding gap clear " + x + " at pose " + pose);
                }
                var sight = new Ray(root.transform.TransformPoint(new Vector3(0, .5f, 3.65f)), root.transform.forward);
                check(!solids.Any(c => c.Raycast(sight, out _, 2)), "Duck-eye forward sightline clear at pose " + pose);
                var furnace = root.GetComponent<RailTrainAssembly>().furnace;
                var mouth = new Ray(furnace.transform.TransformPoint(new Vector3(0, .07f, 1.2f)), -furnace.transform.forward);
                float nearest = float.MaxValue;
                foreach (var c in solids) if (c.Raycast(mouth, out var hit, 4)) nearest = Mathf.Min(nearest, hit.distance);
                check(nearest > 1.1f && nearest < 1.2f, "Furnace opening reaches rear wall without a shell cap at pose " + pose);
            }
            lines.Add("Enabled solid colliders: " + solids.Length + "; new shell triangles: " + shell.Sum(c => c.sharedMesh.triangles.Length / 3));
            Directory.CreateDirectory("docs/validation/train-mesh-collision");
            File.WriteAllLines("docs/validation/train-mesh-collision/editmode-checks.txt", lines);
            Debug.Log("Train mesh collision: " + lines.Count(l => l.StartsWith("PASS ")) + " checks passed");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
