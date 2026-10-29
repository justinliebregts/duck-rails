using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opt-in for human rigs whose elbow/wrist offsets are authored 10% longer.
/// Extends only arm geometry in bone space; hands, bones, IK and physics stay untouched.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerArmGeometry : MonoBehaviour
{
    public const float LengthScale = 1.1f;
    [SerializeField] SkinnedMeshRenderer body;

    static readonly Dictionary<Mesh, Mesh> Fitted = new Dictionary<Mesh, Mesh>();
    static readonly HashSet<Mesh> Generated = new HashSet<Mesh>();
    bool boundsPrepared;

    void Awake() => Prepare(body);

    // Also called by skin initialization: inactive ragdolls may be dressed before Awake.
    public void Prepare(SkinnedMeshRenderer renderer)
    {
        if (!enabled || renderer == null) return;
        renderer.sharedMesh = Fit(renderer.sharedMesh, renderer.bones);
        if (boundsPrepared) return;
        Bounds bounds = renderer.localBounds;
        bounds.Encapsulate(bounds.min * LengthScale);
        bounds.Encapsulate(renderer.localBounds.max * LengthScale);
        renderer.localBounds = bounds;
        boundsPrepared = true;
    }

    public Mesh Fit(Mesh source, Transform[] bones)
    {
        if (!enabled || source == null || Generated.Contains(source)) return source;
        if (Fitted.TryGetValue(source, out Mesh cached)) return cached;

        Matrix4x4[] poses = source.bindposes;
        if (poses.Length != bones.Length) return source;
        Matrix4x4 stretch = Matrix4x4.Scale(new Vector3(1f, LengthScale, 1f));
        int arms = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            switch (bones[i].name)
            {
                case "upper_arm.L": case "upper_arm.R":
                case "forearm.L": case "forearm.R":
                    // Pre-multiply: stretch along the bone's Y axis, not mesh/world axes.
                    poses[i] = stretch * poses[i];
                    arms++;
                    break;
            }
        }
        if (arms != 4) return source;

        Mesh fitted = Instantiate(source);
        fitted.name = source.name + " (Player arms +10%)";
        fitted.hideFlags = HideFlags.HideAndDontSave;
        fitted.bindposes = poses;
        Fitted.Add(source, fitted);
        Generated.Add(fitted);
        return fitted;
    }

#if UNITY_EDITOR
    // Cache survives play sessions with domain reload disabled. Release native meshes on reload/quit.
    [UnityEditor.InitializeOnLoadMethod]
    static void RegisterCleanup()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        UnityEditor.EditorApplication.quitting += ClearCache;
    }

    static void ClearCache()
    {
        foreach (Mesh mesh in Generated)
            if (mesh != null) DestroyImmediate(mesh);
        Generated.Clear();
        Fitted.Clear();
    }
#endif
}
