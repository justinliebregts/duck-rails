using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Local scenery visibility only. Block gameplay, collision and lighting payloads stay loaded.</summary>
[DefaultExecutionOrder(20000)]
public sealed class RailBlockVisibility : MonoBehaviour
{
    sealed class Group
    {
        public RailBlock block;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<bool> previous = new List<bool>();
        public bool hidden;

        public void SetHidden(bool value)
        {
            if (hidden == value) return;
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                if (value) previous[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = value || previous[i];
            }
            hidden = value;
        }
    }

    readonly List<Group> groups = new List<Group>();
    RailRoute route;
    int current = -1;

    // Called after each route build, including replay and late-join reconstruction.
    public void Rebuild(RailRoute source)
    {
        Restore();
        groups.Clear();
        route = source;
        var seen = new HashSet<Renderer>();
        void AddRenderers(Group group, Transform root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Movable/networked objects retain their own visibility even if parented to scenery.
                if (renderer.GetComponentInParent<PhotonView>(true) != null ||
                    renderer.GetComponentInParent<Grabbable>(true) != null || !seen.Add(renderer)) continue;
                group.renderers.Add(renderer);
                group.previous.Add(false);
            }
        }
        void AddBlock(RailBlock block, Transform extra = null)
        {
            if (block == null) return;
            var group = new Group { block = block };
            AddRenderers(group, block.transform);
            AddRenderers(group, extra);
            groups.Add(group);
        }
        AddBlock(route.startingStation);
        for (int i = 0; i < route.Blocks.Count; i++)
            AddBlock(route.Blocks[i], i == route.Blocks.Count / 2 ? route.midpointShop : null);
        AddBlock(route.finale, route.finaleRoot);
    }

    void LateUpdate()
    {
        if (route == null || !route.isActiveAndEnabled || groups.Count == 0)
        {
            Restore();
            return;
        }
        var player = GR.Player.instance;
        if (player != null && player.isActiveAndEnabled)
            ApplyViewerPosition(player.HeadCenteredGameplayPosition(player.transform.position));
        else
        {
            // Offline previews and the interval before the local rig binds can use the main camera.
            var camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled) ApplyViewerPosition(camera.transform.position);
            else Restore();
        }
    }

    public void ApplyViewerPosition(Vector3 position)
    {
        if (!isActiveAndEnabled || groups.Count == 0) return;
        if (!float.IsFinite(position.z)) { Restore(); return; }
        // Route footprints run along world +Z. Height, lateral exploration and curved train
        // distance must not change the player's block. Clamp beyond either end for the dawn ride.
        int index = 0;
        for (int i = 1; i < groups.Count; i++)
            if (groups[i].block != null && position.z >= groups[i].block.transform.position.z) index = i;
        if (index == current) return;
        current = index;
        for (int i = 0; i < groups.Count; i++) groups[i].SetHidden(Mathf.Abs(i - current) > 1);
    }

    void Restore()
    {
        foreach (var group in groups) group.SetHidden(false);
        current = -1;
    }
    void OnDisable() => Restore();
}
