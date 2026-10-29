using UnityEngine;

// One bag slot: remembers which item prefab is in it and shows its mini display model.
public class BagSlot : MonoBehaviour
{
    const float HoverScale = 1.2f;
    const float ScaleSpeed = 14f;
    const float FillDuration = 0.18f;

    [SerializeField] Transform contentAnchor;
    [Tooltip("Optional: enabled while hovered.")]
    [SerializeField] GameObject hoverHighlight;

    string itemKey;
    GameObject mini;
    bool hovered;
    Vector3 baseScale;

    float fillTime = -1f;
    Vector3 fillFromWorld;
    float fillFromScale;

    public string ItemKey => itemKey;
    public bool IsEmpty => string.IsNullOrEmpty(itemKey);
    public bool Hovered => hovered;
    public Transform ContentAnchor => contentAnchor != null ? contentAnchor : transform;
    public Transform DisplayContent => mini != null ? mini.transform.GetChild(0) : ContentAnchor;

    void Awake()
    {
        baseScale = transform.localScale;
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
        if (GetComponentInChildren<Renderer>(true) == null)
            CreatePlate();
    }

    void CreatePlate()
    {
        GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        circle.name = "SlotCircle";
        Destroy(circle.GetComponent<Collider>());
        Transform plate = circle.transform;
        plate.SetParent(transform, false);
        plate.localRotation = Quaternion.Euler(90f, 0f, 0f);
        plate.localPosition = Vector3.zero;
        plate.localScale = new Vector3(0.065f, 0.003f, 0.065f);

        Renderer renderer = circle.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.material = new Material(shader);

        Color color = new Color(0.95f, 0.9f, 0.75f, 1f);
        if (renderer.material.HasProperty("_BaseColor"))
            renderer.material.SetColor("_BaseColor", color);
        if (renderer.material.HasProperty("_Color"))
            renderer.material.SetColor("_Color", color);
    }
    void Update()
    {
        float target = hovered ? HoverScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target, 1f - Mathf.Exp(-ScaleSpeed * Time.deltaTime));

        if (fillTime >= 0f && mini != null)
        {
            fillTime += Time.deltaTime;
            float t = Mathf.Clamp01(fillTime / FillDuration);
            float e = 1f - (1f - t) * (1f - t);
            Transform anchor = ContentAnchor;
            mini.transform.position = Vector3.Lerp(fillFromWorld, anchor.position, e);
            mini.transform.localScale = Vector3.one * Mathf.Lerp(fillFromScale, 1f, e);
            if (t >= 1f)
            {
                fillTime = -1f;
                mini.transform.localPosition = Vector3.zero;
                mini.transform.localScale = Vector3.one;
            }
        }
    }

    public void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        if (hoverHighlight != null)
            hoverHighlight.SetActive(value);
    }

    /// <summary>Puts the item in place with no animation (loading a saved bag).</summary>
    public void Fill(string key, GameObject prefab, float slotSize)
    {
        if (string.IsNullOrEmpty(key) || !IsEmpty) return;
        itemKey = key;
        mini = BagMiniModel.Build(prefab, key, ContentAnchor, slotSize);
        fillTime = -1f;
        WalkieRadio.BindSlot(this);
    }

    /// <summary>Puts the item in and shrinks the mini from where the real item was, at its real size.</summary>
    public void Fill(string key, GameObject prefab, float slotSize, Vector3 fromWorld, float realSize)
    {
        if (string.IsNullOrEmpty(key) || !IsEmpty) return;
        Fill(key, prefab, slotSize);

        float miniSize = WorldSize(mini);
        fillFromWorld = fromWorld;
        fillFromScale = miniSize > 1e-5f ? Mathf.Clamp(realSize / miniSize, 1f, 20f) : 1f;
        fillTime = 0f;
        mini.transform.position = fromWorld;
        mini.transform.localScale = Vector3.one * fillFromScale;
    }

    /// <summary>Empties the slot and returns the prefab key of what was in it.</summary>
    public string Take()
    {
        WalkieRadio.UnbindSlot(this);
        string taken = itemKey;
        itemKey = null;
        fillTime = -1f;
        if (mini != null)
            Destroy(mini);
        mini = null;
        SetHovered(false);
        return taken;
    }

    public static float WorldSize(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
    }
}
