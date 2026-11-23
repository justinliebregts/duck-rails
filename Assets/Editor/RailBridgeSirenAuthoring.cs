using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Adds the siren controller and sequenced warning-cap chase to bridge-finale-v1. Idempotent.</summary>
public static class RailBridgeSirenAuthoring
{
    const string Prefab = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    const float ClusterGap = 3f;

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
            var steps = new System.Collections.Generic.List<RailBridgeSiren.Step>();
            var current = new System.Collections.Generic.List<Renderer>();
            float last = float.NegativeInfinity;
            foreach (var cap in caps)
            {
                if (current.Count > 0 && cap.position.z - last > ClusterGap)
                { steps.Add(new RailBridgeSiren.Step { renderers = current.ToArray() }); current.Clear(); }
                current.Add(cap.GetComponent<MeshRenderer>());
                last = cap.position.z;
            }
            if (current.Count > 0) steps.Add(new RailBridgeSiren.Step { renderers = current.ToArray() });

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
