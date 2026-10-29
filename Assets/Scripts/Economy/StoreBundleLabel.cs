using TMPro;
using UnityEngine;

// Shared text over a bundle's items on the wall: "Rambro Bundle $14.99" until it's owned, then "Rambro Bundle Owned".
// The price is Meta's localized one once it loads, the catalog's displayPrice until then.
public class StoreBundleLabel : MonoBehaviour
{
    [SerializeField] StoreUI store;
    [Tooltip("Meta SKU from StoreCatalog bundles.")]
    [SerializeField] string bundleSku = "duckz_rambro_bundle";
    [Tooltip("Defaults to the TMP_Text on this object.")]
    [SerializeField] TMP_Text label;

    string shown;
    bool warned;

    void Awake()
    {
        if (store == null)
            store = GetComponentInParent<StoreUI>();
        if (label == null)
            label = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (store != null)
            store.OnStateChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (store != null)
            store.OnStateChanged -= Refresh;
    }

    void Refresh()
    {
        if (label == null || store == null)
        {
            if (!warned)
            {
                warned = true;
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write($"[Store] Bundle label '{name}' has no {(store == null ? "StoreUI" : "text")}, so it won't update");
                }
            }
            return;
        }

        string text = store.BundleLabel(bundleSku);
        if (text == shown)
            return;
        shown = text;
        label.text = text;
        DuckzLog.Log($"[Store] Bundle label '{bundleSku}' is now '{text}'");
    }
}
