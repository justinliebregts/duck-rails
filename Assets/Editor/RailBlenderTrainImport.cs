#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Installs Blender-authored geometry without replacing the gameplay assembly.</summary>
public static class RailBlenderTrainImport
{
    public const string Folder = "Assets/DuckRails/Train/Blender";
    public const string VisualRoot = "Blender train shell";
    [Serializable] public class Part
    {
        public string name;
        public Vector3[] vertices;
        public Vector2[] uv;
        public int[] triangles, triangleMaterials;
        public string[] materials;
    }
    [Serializable] public class Model { public Part[] visuals, parts; public Part controlTrim; }

    public static bool Installed => AssetDatabase.LoadAssetAtPath<GameObject>(RailYardMuleArt.Train)?.transform.Find(VisualRoot) != null;
    public static void ProtectBlenderModel()
    {
        if (Installed) throw new InvalidOperationException("The train uses reviewed Blender geometry. Use Import Reviewed Blender Train; legacy builders would overwrite its geometry or controls.");
    }

    static Mesh SaveMesh(Part part, string name, Matrix4x4 matrix, bool materials = true)
    {
        string path = Folder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.vertices = part.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        mesh.uv = part.uv;
        if (materials)
        {
            mesh.subMeshCount = part.materials.Length;
            for (int slot = 0; slot < part.materials.Length; slot++)
            {
                var ids = new List<int>();
                for (int i = 0; i < part.triangleMaterials.Length; i++)
                    if (part.triangleMaterials[i] == slot) ids.AddRange(part.triangles.Skip(i * 3).Take(3));
                mesh.SetTriangles(ids, slot);
            }
        }
        else
        {
            // Convex cooking must not include unused vertices from the opposite cowl piece.
            var used = part.triangles.Distinct().ToArray();
            var remap = used.Select((index, slot) => new { index, slot }).ToDictionary(x => x.index, x => x.slot);
            mesh.vertices = used.Select(i => matrix.MultiplyPoint3x4(part.vertices[i])).ToArray();
            mesh.uv = used.Select(i => part.uv[i]).ToArray();
            mesh.triangles = part.triangles.Select(i => remap[i]).ToArray();
        }
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Material MaterialFor(string path)
    {
        if (path == "BLACK_LIP")
        {
            string target = Folder + "/Furnace lip black.mat";
            var black = AssetDatabase.LoadAssetAtPath<Material>(target);
            if (!black) { black = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(black, target); }
            // Blender shader values are linear; Unity material colors are authored in sRGB.
            black.SetColor("_BaseColor", new Color(.008f, .009f, .01f).gamma);
            black.SetFloat("_Smoothness", .1f); black.SetFloat("_SpecularHighlights", 0);
            black.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); EditorUtility.SetDirty(black);
            return black;
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) throw new InvalidOperationException("Missing train material " + path);
        return mat;
    }
    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (!t) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        t.gameObject.layer = 6;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        return t;
    }
    static void Convex(Transform parent, Part p, Matrix4x4 matrix, string name)
    {
        var t = Child(parent, name);
        var c = t.GetComponent<MeshCollider>(); if (!c) c = t.gameObject.AddComponent<MeshCollider>();
        c.sharedMesh = SaveMesh(p, "Collision " + name, matrix, false);
        c.convex = true;
    }
    static void Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var c = Child(parent, name).GetComponent<BoxCollider>();
        if (!c) c = parent.Find(name).gameObject.AddComponent<BoxCollider>();
        c.center = center; c.size = size;
    }

    [MenuItem("Duck Rails/Art/Import Reviewed Blender Train")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || Application.dataPath != "X:/Work/duck-rails/Assets")
            throw new InvalidOperationException("Duck Rails Edit Mode required");
        var model = JsonUtility.FromJson<Model>(File.ReadAllText(Folder + "/unity-integration.json"));
        if (model.visuals.Length != 3) throw new InvalidOperationException("Expected three reviewed mesh sections");
        var root = PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try
        {
            var assembly = root.GetComponent<RailTrainAssembly>();
            // Capture actual gameplay components before editing, including all serialized bindings.
            var preserved = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c is RailTrain || c is RailTrainAssembly || c is RailControl || c is RailHoldControl || c is RailCockpitFuelBar || c is RailFurnaceModule)
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            var shell = Child(root.transform, VisualRoot);
            var anchor = Child(root.transform, "Train light probe anchor"); anchor.localPosition = new Vector3(0, .5f, 0);
            foreach (var p in model.visuals)
            {
                var t = Child(shell, p.name);
                var filter = t.GetComponent<MeshFilter>(); if (!filter) filter = t.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = SaveMesh(p, p.name, Matrix4x4.identity);
                var renderer = t.GetComponent<MeshRenderer>(); if (!renderer) renderer = t.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = p.materials.Select(MaterialFor).ToArray();
                renderer.probeAnchor = anchor; renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            // Retain old objects and their identities: only static artwork is replaced.
            foreach (var r in root.transform.Find("Yard Mule shell").GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in assembly.furnace.transform.Find("Front firebox art").GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var art = assembly.controls.Find("Integrated dash art");
            art.Find("Integrated housing").GetComponent<MeshRenderer>().enabled = false;
            var trim = art.Find("Dash trim");
            trim.GetComponent<MeshFilter>().sharedMesh = SaveMesh(model.controlTrim, "Retained gauge trim", trim.worldToLocalMatrix * root.transform.localToWorldMatrix);
            // The black/yellow housing is in the shell; this keeps gauge surround/recesses only.
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled) r.probeAnchor = anchor;

            var furnaceCollision = Child(assembly.furnace.transform, "Blender furnace collision");
            foreach (var old in assembly.furnace.transform.Find("Front firebox art").GetComponentsInChildren<Collider>(true)) old.enabled = false;
            var source = model.parts.ToDictionary(p => p.name);
            string[] furnaceSolids = { "Chamber floor", "Chamber ceiling", "Closed rear", "Chamber side -1", "Chamber side 1", "Mouth sill", "Mouth lintel", "Mouth jamb -1", "Mouth jamb 1", "Low exhaust duct", "Stack X -1", "Stack X 1", "Stack Z -1", "Stack Z 1", "Stack collar" };
            foreach (var name in furnaceSolids)
                Convex(furnaceCollision, source[name], assembly.furnace.transform.worldToLocalMatrix * root.transform.localToWorldMatrix, name);
            var collision = Child(root.transform, "Blender supplemental collision");
            Convex(collision, source["Dash to furnace cowl"], Matrix4x4.identity, "Dash to furnace cowl");
            // Separate left/right pieces avoid a convex hull bridging the cockpit approach.
            var transition = source["Cowl side transitions"];
            foreach (int side in new[] { -1, 1 })
            {
                var indices = new List<int>();
                for (int i = 0; i < transition.triangles.Length; i += 3)
                    if (transition.vertices[transition.triangles[i]].x * side > 0) indices.AddRange(transition.triangles.Skip(i).Take(3));
                Convex(collision, new Part { vertices = transition.vertices, uv = transition.uv, triangles = indices.ToArray() }, Matrix4x4.identity, "Cowl side " + side);
            }
            // Lower enclosure is stepped below the cavity, never a convex cap across its opening.
            Box(collision, "Lower nose center", new Vector3(0, -.55f, 5.42f), new Vector3(2.16f, .19f, 1.04f));
            foreach (int side in new[] { -1, 1 })
                Box(collision, "Lower nose side " + side, new Vector3(side * 1.56f, -.4675f, 5.47f), new Vector3(.96f, .355f, .94f));

            foreach (var pair in preserved)
                if (EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Gameplay binding changed: " + pair.Key.GetType().Name);
            // Fire follows the lowered ember bed. The insertion anchor, accepted samples and fuel protocol stay intact.
            assembly.furnace.fire.transform.localPosition = new Vector3(0, -.29f, .28f);
            PrefabUtility.SaveAsPrefabAsset(root, RailYardMuleArt.Train);
            Debug.Log("Blender train installed: three static sections; " + preserved.Count + " gameplay components/bindings unchanged; controls, fuel display, horn, radio and furnace retained.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Duck Rails/Train/Validate Blender Integration")]
    public static void Validate()
    {
        var lines = new List<string>();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new InvalidOperationException(message); lines.Add("PASS " + message); };
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(RailYardMuleArt.Train);
        var a = root.GetComponent<RailTrainAssembly>();
        var shell = root.transform.Find(VisualRoot);
        check(shell && shell.GetComponentsInChildren<MeshRenderer>().Length == 3, "Exactly three Blender static mesh sections");
        var model = JsonUtility.FromJson<Model>(File.ReadAllText(Folder + "/unity-integration.json"));
        foreach (var p in model.visuals)
        {
            var t = shell.Find(p.name); var mesh = t.GetComponent<MeshFilter>().sharedMesh;
            check(t.localScale == Vector3.one && t.localRotation == Quaternion.identity && t.localPosition == Vector3.zero, p.name + " uses metre-scale vertices without FBX axis/scale transforms");
            check(mesh.vertices.SequenceEqual(p.vertices) && mesh.uv.SequenceEqual(p.uv), p.name + " matches reviewed Blender positions and UVs exactly");
            check(mesh.triangles.Length == p.triangles.Length, p.name + " retains triangle count");
            var renderer = t.GetComponent<MeshRenderer>();
            check(renderer.sharedMaterials.SequenceEqual(p.materials.Select(MaterialFor)), p.name + " maps original Unity materials and black furnace lip");
            check(renderer.lightProbeUsage == LightProbeUsage.BlendProbes && renderer.probeAnchor == root.transform.Find("Train light probe anchor"), p.name + " uses the shared moving probe anchor");
        }
        check(root.transform.Find("Yard Mule shell").GetComponentsInChildren<MeshRenderer>(true).All(r => !r.enabled), "Legacy shell artwork disabled without removing collision/light identities");
        check(a.furnace.transform.Find("Front firebox art").GetComponentsInChildren<MeshRenderer>(true).All(r => !r.enabled), "Legacy furnace artwork disabled");
        check(a.furnace.transform.Find("Front firebox art").GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Legacy furnace collision disabled");
        check(a.furnace.transform.Find("Blender furnace collision").GetComponentsInChildren<MeshCollider>().All(c => c.enabled && c.convex && c.sharedMesh), "Replacement furnace solids use valid convex mesh collision");
        check(a.radio.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled), "Separate radio/table renderers retained");
        check(a.controls.GetComponent<RailCockpitFuelBar>().segments.GetComponent<MeshRenderer>().enabled, "Live fuel segments retained separately");
        check(a.controls.Find("Integrated dash art/Dash trim").GetComponent<MeshFilter>().sharedMesh.name == "Retained gauge trim", "Gauge surround retained; paired dash light strips omitted");
        check(a.fuelGauge.font.name == "CutePixel SDF", "Fuel gauge retains CutePixel SDF");
        Directory.CreateDirectory("docs/validation/blender-train");
        File.WriteAllLines("docs/validation/blender-train/integration-checks.txt", lines);
        Debug.Log("Blender integration: " + lines.Count + " checks passed");
    }
}
#endif
