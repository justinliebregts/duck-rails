using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Amber edge strip around the compound's public departure pressure plate. It is hidden
/// until the departure countdown runs (RailPressurePlate.countdownGlow), so players can see
/// which area boards the train together without reading the board.
/// </summary>
public static class RailDepartureGlowBuilder
{
    public const string GlowName = "Departure countdown glow";
    const string MeshPath = "Assets/DuckRails/Compound/SubwayTerminal/Meshes/Departure countdown glow.asset";
    const string MaterialPath = "Assets/DuckRails/Compound/SubwayTerminal/Materials/Departure countdown glow.mat";
    const float Inset = .03f, Width = .10f, Bottom = .02f, Top = .035f;

    [MenuItem("Duck Rails/Compound/Install Departure Countdown Glow")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        Install(UnityEngine.Object.FindAnyObjectByType<RailPressurePlate>(FindObjectsInactive.Include));
    }

    public static void Install(RailPressurePlate pressurePlate)
    {
        if (pressurePlate == null || pressurePlate.contactSurface == null) throw new InvalidOperationException("Missing pressure plate or its contact surface.");
        var world = UnityEngine.Object.FindAnyObjectByType<RailWorld>(FindObjectsInactive.Include);
        var parent = world != null ? world.compound.transform : pressurePlate.transform;
        for (var old = parent.Find(GlowName); old != null; old = parent.Find(GlowName))
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        var bounds = pressurePlate.contactSurface.bounds;
        var go = new GameObject(GlowName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        // Rest on the floor the visible slab sits on (the collider extends below it).
        var slab = pressurePlate.plate != null ? pressurePlate.plate.GetComponent<Renderer>() : null;
        float floor = slab != null ? slab.bounds.min.y : bounds.center.y;
        go.transform.position = new Vector3(bounds.center.x, floor, bounds.center.z);
        go.transform.rotation = Quaternion.identity;
        go.GetComponent<MeshFilter>().sharedMesh = RingMesh(bounds.extents.x + Inset, bounds.extents.z + Inset);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GlowMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;

        Undo.RecordObject(pressurePlate, "Assign departure glow");
        pressurePlate.countdownGlow = renderer;
        PrefabUtility.RecordPrefabInstancePropertyModifications(pressurePlate);
        EditorUtility.SetDirty(pressurePlate);
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    /// <summary>Flat rectangular frame, open in the middle, sitting just above the floor frame.</summary>
    static Mesh RingMesh(float innerX, float innerZ)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Box(Vector3 min, Vector3 max)
        {
            Vector3 P(int x, int y, int z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.AddRange(new[] { a, b, c, d });
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Quad(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0)); // top
            Quad(P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0)); // -z
            Quad(P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), P(0, 0, 1)); // +z
            Quad(P(0, 0, 1), P(0, 1, 1), P(0, 1, 0), P(0, 0, 0)); // -x
            Quad(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1)); // +x
        }
        float ox = innerX + Width, oz = innerZ + Width;
        Box(new Vector3(-ox, Bottom, -oz), new Vector3(ox, Top, -innerZ));
        Box(new Vector3(-ox, Bottom, innerZ), new Vector3(ox, Top, oz));
        Box(new Vector3(-ox, Bottom, -innerZ), new Vector3(-innerX, Top, innerZ));
        Box(new Vector3(innerX, Bottom, -innerZ), new Vector3(ox, Top, innerZ));

        var mesh = new Mesh { name = "Departure countdown glow" };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, MeshPath); return mesh; }
        existing.Clear();
        existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.triangles = mesh.triangles;
        existing.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }

    static Material GlowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Departure countdown glow" };
        material.SetColor("_BaseColor", new Color(1f, .62f, .18f));
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
