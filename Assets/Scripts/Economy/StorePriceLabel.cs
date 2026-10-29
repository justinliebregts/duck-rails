using TMPro;
using UnityEngine;

// World-space price for a store item. Stays on the shelf while the item is pulled or held.
// The number comes from duckz_economy.json once GET /duckz/store has loaded.
public class StorePriceLabel : MonoBehaviour
{
    Component host;
    StoreUI store;
    string itemKey;
    TMP_Text label;

    public static void Attach(Component host, StoreUI store, string itemKey)
    {
        if (host == null || string.IsNullOrEmpty(itemKey))
            return;

        var go = new GameObject(host.name + " Price");
        Transform parent = host.transform.parent != null ? host.transform.parent : host.transform;
        go.transform.SetParent(parent, false);

        Collider col = host.GetComponent<Collider>();
        Vector3 world = col != null
            ? col.bounds.center + Vector3.up * (col.bounds.extents.y + 0.12f)
            : host.transform.position + Vector3.up * 0.4f;
        go.transform.position = world;
        go.transform.localScale = Vector3.one * 0.08f;

        StorePriceLabel view = go.AddComponent<StorePriceLabel>();
        view.host = host;
        view.store = store;
        view.itemKey = itemKey;
        view.label = go.AddComponent<TextMeshPro>();
        view.label.font = TMP_Settings.defaultFontAsset;
        view.label.fontSize = 36f;
        view.label.alignment = TextAlignmentOptions.Center;
        view.label.color = new Color(1f, 0.92f, 0.45f);
        view.label.rectTransform.sizeDelta = new Vector2(8f, 2f);
        view.label.enableWordWrapping = false;
        view.label.overflowMode = TextOverflowModes.Overflow;
        view.label.raycastTarget = false;
        view.Refresh();
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

    void LateUpdate()
    {
        if (host == null)
        {
            Destroy(gameObject);
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector3 toCamera = cam.transform.position - transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toCamera);
    }

    void Refresh()
    {
        if (label == null || store == null)
            return;

        StoreItem item = store.Find(itemKey);
        if (item == null)
        {
            label.text = "";
            return;
        }

        label.text = store.StatusLabel(item);
    }
}
