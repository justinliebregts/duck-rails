using System.Collections.Generic;
using UnityEngine;

// Builds the small display model shown in a bag slot: a renderer-only copy of the item prefab, scaled to fit.
public static class BagMiniModel
{
    public static GameObject Build(GameObject source, string key, Transform parent, float slotSize)
    {
        GameObject root = new GameObject(key + " (Mini)");
        root.transform.SetParent(parent, false);

        GameObject content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);
        if (source == null)
            return root;

        BagItem overrides = source.GetComponent<BagItem>();
        if (overrides != null && overrides.miniPrefab != null)
            Object.Instantiate(overrides.miniPrefab, content.transform, false);
        else
            CloneMeshes(source.transform, content.transform);

        content.transform.localRotation = overrides != null ? Quaternion.Euler(overrides.miniEuler) : Quaternion.identity;
        Fit(content.transform, slotSize * (overrides != null ? overrides.miniScale : 1f));
        return root;
    }

    // Copies MeshFilter/MeshRenderer and SkinnedMeshRenderer (as a static mesh) in item-local space. No colliders, scripts or PhotonViews.
    static void CloneMeshes(Transform source, Transform target)
    {
        Matrix4x4 toLocal = source.worldToLocalMatrix;
        var renderers = new List<Renderer>();
        source.GetComponentsInChildren(true, renderers);

        foreach (Renderer r in renderers)
        {
            Mesh mesh = null;
            if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter))
                mesh = filter.sharedMesh;
            else if (r is SkinnedMeshRenderer skinned)
                mesh = skinned.sharedMesh;
            if (mesh == null)
                continue;

            GameObject part = new GameObject(r.name);
            part.transform.SetParent(target, false);
            Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
            part.transform.localPosition = m.GetColumn(3);
            part.transform.localRotation = m.rotation;
            part.transform.localScale = m.lossyScale;

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            mr.sharedMaterials = r.sharedMaterials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = true;
        }
    }

    // Scales and recenters so the largest bounds axis equals size, centered on the root.
    static void Fit(Transform content, float size)
    {
        Renderer[] renderers = content.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || size <= 0f)
            return;

        Transform root = content.parent;
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer r in renderers)
        {
            // Transform the local box directly: a world-axis-aligned box expands
            // as the bag rotates, making later insertions fit to a different size.
            Bounds b = r.localBounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 point = toRoot.MultiplyPoint3x4(corner);
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(point);
            }
        }

        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largest <= 1e-5f)
            return;

        float scale = size / largest;
        content.localScale *= scale;
        content.localPosition -= bounds.center * scale;
    }
}
