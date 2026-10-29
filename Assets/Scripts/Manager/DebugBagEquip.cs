using UnityEngine;

public enum DebugWornBag { None, Basic, Rambro, Ninja, Gorilla }

/// <summary>
/// Editor test: which bag the local player wears. None leaves the back empty.
/// Change it on the GameManager in the hierarchy while playing.
/// </summary>
public class DebugBagEquip : MonoBehaviour
{
    public static DebugBagEquip Current { get; private set; }

    [SerializeField] DebugWornBag bag = DebugWornBag.None;

    public DebugWornBag Bag => bag;

    public string CatalogKey => bag switch
    {
        DebugWornBag.Basic => "bag_basic",
        DebugWornBag.Rambro => "bag_rambro",
        DebugWornBag.Ninja => "bag_ninja",
        DebugWornBag.Gorilla => "bag_gorilla",
        _ => null
    };

    void OnEnable() => Current = this;

    void OnDisable()
    {
        if (Current == this)
            Current = null;
    }
}
