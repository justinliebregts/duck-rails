using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Adds the siren controller and sequenced warning cap and post chase to bridge-finale-v1. Idempotent.</summary>
public static class RailBridgeSirenAuthoring
{
    const string Prefab = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    const float ClusterGap = 3f;

    // Looks identical to the plain post material at rest (emission black); the siren drives emission at runtime.
    static Material GlowMaterial()
    {
        const string path = "Assets/DuckRails/Materials/Warning post glow.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Danger.mat"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var button = all.First(t => t.name == "Lower bridge button");
            foreach (var old in all.Where(t => t.name == "Bridge siren" || t.name == "Beacon spinner").ToArray())
                if (old != null) Object.DestroyImmediate(old.gameObject);

            // Caps along the track plus the pair at the control box, nearest the train first. Caps within ClusterGap
            // metres of each other along the track flash together (the last four sit at nearly the same spacing).
            var caps = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Emissive red warning cap") && t.GetComponent<MeshRenderer>() != null)
                .OrderBy(t => t.position.z).ToArray();
            var glowMat = GlowMaterial();
            var posts = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Warning post")).ToArray();
            var steps = new System.Collections.Generic.List<RailBridgeSiren.Step>();
            var current = new System.Collections.Generic.List<Renderer>();
            var currentPosts = new System.Collections.Generic.List<Renderer>();
            float last = float.NegativeInfinity;
            foreach (var cap in caps)
            {
                if (current.Count > 0 && cap.position.z - last > ClusterGap)
                { steps.Add(new RailBridgeSiren.Step { renderers = current.ToArray(), posts = currentPosts.ToArray() }); current.Clear(); currentPosts.Clear(); }
                current.Add(cap.GetComponent<MeshRenderer>());
                var post = posts.Where(p => Mathf.Abs(p.transform.position.x - cap.position.x) < .6f && Mathf.Abs(p.transform.position.z - cap.position.z) < .6f && p.transform.position.y < cap.position.y)
                    .OrderBy(p => cap.position.y - p.transform.position.y).FirstOrDefault();
                if (post != null) { post.sharedMaterial = glowMat; currentPosts.Add(post); }
                last = cap.position.z;
            }
            if (current.Count > 0) steps.Add(new RailBridgeSiren.Step { renderers = current.ToArray(), posts = currentPosts.ToArray() });

            var host = new GameObject("Bridge siren");
            host.transform.SetParent(button, false);
            host.layer = button.gameObject.layer;
            var siren = host.AddComponent<RailBridgeSiren>();
            siren.soundPoint = button;
            siren.steps = steps.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
