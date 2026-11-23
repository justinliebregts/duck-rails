using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Adds the siren controller and sequenced warning cap and post chase to bridge-finale-v1. Idempotent.</summary>
public static class RailBridgeSirenAuthoring
{
    const string Prefab = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    const float ClusterGap = 3f;

    // The posts are static (batched and baked), so a runtime emission change on them is unreliable. A non-static emissive shell
    // that is hidden between pulses lights the whole post instead.
    static Renderer Shell(MeshRenderer post, Material glow)
    {
        var go = new GameObject("Post glow");
        go.layer = post.gameObject.layer;
        go.transform.SetParent(post.transform, false);
        go.transform.localScale = Vector3.one * 1.08f;
        go.AddComponent<MeshFilter>().sharedMesh = post.GetComponent<MeshFilter>().sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = glow;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = false;
        return r;
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
            foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Post glow").ToArray()) Object.DestroyImmediate(old.gameObject);
            var posts = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Warning post")).ToArray();
            var plain = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Danger.mat");
            foreach (var p in posts) p.sharedMaterial = plain;
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
                if (post != null) currentPosts.Add(Shell(post, cap.GetComponent<MeshRenderer>().sharedMaterial));
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
