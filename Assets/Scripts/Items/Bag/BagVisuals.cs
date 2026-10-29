using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows a bag's look: the store catalog's bag model for its id, a prefab with children named Mesh, Slots (holding the
/// BagSlots), GrabLeft and GrabRight, spawned under this root. Slot contents are not moved here: callers refill the slots.
/// </summary>
public class BagVisuals : MonoBehaviour
{
    readonly List<BagSlot> slots = new List<BagSlot>();
    GameObject model;
    int appliedId = -1;

    public IReadOnlyList<BagSlot> Slots => slots;
    public Transform GrabLeft { get; private set; }
    public Transform GrabRight { get; private set; }
    public byte AppliedId => appliedId < 0 ? (byte)0 : (byte)appliedId;

    public static BagVisuals Attach(GameObject root)
    {
        if (!root.TryGetComponent(out BagVisuals visuals))
            visuals = root.AddComponent<BagVisuals>();
        return visuals;
    }

    /// <summary>Shows the bag with this id. Returns false if it was already showing.</summary>
    public bool Apply(byte bagId)
    {
        if (appliedId == bagId)
            return false;
        appliedId = bagId;

        if (model != null)
        {
            model.transform.SetParent(null, false);
            Destroy(model);
            model = null;
        }

        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        GameObject asset = catalog != null ? catalog.FindBagModel(bagId) : null;
        if (asset == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write($"[Bag] Bag {bagId} has no bag model in the store catalog.", this);
            }
            slots.Clear();
            GrabLeft = GrabRight = null;
            return true;
        }

        model = Instantiate(asset, transform, false);
        model.name = asset.name;
        Transform root = model.transform;
        Transform grid = root.Find("Slots");
        slots.Clear();
        (grid != null ? grid : root).GetComponentsInChildren(true, slots);
        GrabLeft = root.Find("GrabLeft");
        GrabRight = root.Find("GrabRight");

        DuckzLog.Log($"[Bag] '{name}' now shows bag {bagId} ('{asset.name}'), {slots.Count} slots, " +
                  $"grabs {(GrabLeft != null ? "L" : "-")}{(GrabRight != null ? "R" : "-")}", this);
        return true;
    }
}
