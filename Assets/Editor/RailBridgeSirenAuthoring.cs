using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Adds the siren controller and spinning beacon arms to bridge-finale-v1. Idempotent.</summary>
public static class RailBridgeSirenAuthoring
{
    const string Prefab = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";

    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var button = all.First(t => t.name == "Lower bridge button");
            var caps = new[] { "Emissive red warning cap (1)", "Emissive red warning cap (2)" }
                .Select(n => all.First(t => t.name == n)).ToArray();
            foreach (var old in all.Where(t => t.name == "Bridge siren").ToArray()) Object.DestroyImmediate(old.gameObject);

            var host = new GameObject("Bridge siren");
            host.transform.SetParent(button, false);
            host.layer = caps[0].gameObject.layer;
            var siren = host.AddComponent<RailBridgeSiren>();
            siren.soundPoint = button;

            var mesh = caps[0].GetComponent<MeshFilter>().sharedMesh;
            var material = caps[0].GetComponent<MeshRenderer>().sharedMaterial;
            siren.spinners = caps.Select(cap =>
            {
                var spin = new GameObject("Beacon spinner").transform;
                spin.SetParent(cap.parent, false);
                spin.position = cap.position + Vector3.up * .2f;
                spin.gameObject.layer = cap.gameObject.layer;
                foreach (float yaw in new[] { 0f, 90f })
                {
                    var arm = new GameObject("Beacon beam");
                    arm.layer = cap.gameObject.layer;
                    arm.transform.SetParent(spin, false);
                    arm.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    arm.transform.localScale = new Vector3(.08f, .05f, 3.4f);
                    arm.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = arm.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                spin.gameObject.SetActive(false);
                return spin;
            }).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
