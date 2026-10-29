using UnityEngine;

/// <summary>
/// Box volume for presence analytics. Place one (or more) per region; overlapping volumes
/// resolve by higher Priority, then smaller volume.
/// </summary>
[AddComponentMenu("DuckZ/Analytics/Analytics Area")]
public sealed class AnalyticsArea : MonoBehaviour
{
    static readonly System.Collections.Generic.List<AnalyticsArea> active = new(16);

    [SerializeField] AnalyticsAreaId area = AnalyticsAreaId.Town;
    [Tooltip("Box size in this object's local space.")]
    [SerializeField] Vector3 size = new Vector3(40f, 20f, 40f);
    [Tooltip("Higher wins when the player is inside several areas.")]
    [SerializeField] int priority;
    [Tooltip("Draw the volume in the Scene view when not selected. Selected objects always show a gizmo.")]
    [SerializeField] bool showGizmo;

    public AnalyticsAreaId Area => area;
    public int Priority => priority;
    public float Volume
    {
        get
        {
            Vector3 s = size;
            return Mathf.Abs(s.x * s.y * s.z);
        }
    }

    public static System.Collections.Generic.IReadOnlyList<AnalyticsArea> Active => active;

    public string AreaKey => area.ToString().ToLowerInvariant();

    void OnEnable()
    {
        if (!active.Contains(this))
            active.Add(this);
    }

    void OnDisable() => active.Remove(this);

    public bool ContainsWorldPoint(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        Vector3 half = size * 0.5f;
        return local.x >= -half.x && local.x <= half.x
            && local.y >= -half.y && local.y <= half.y
            && local.z >= -half.z && local.z <= half.z;
    }

    /// <summary>Best area containing world, or null if none.</summary>
    public static AnalyticsArea FindAt(Vector3 world)
    {
        AnalyticsArea best = null;
        for (int i = 0; i < active.Count; i++)
        {
            AnalyticsArea area = active[i];
            if (area == null || !area.isActiveAndEnabled || !area.ContainsWorldPoint(world))
                continue;
            if (best == null
                || area.priority > best.priority
                || (area.priority == best.priority && area.Volume < best.Volume))
                best = area;
        }
        return best;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmo)
            return;
        DrawGizmo(new Color(0.2f, 0.85f, 0.45f, 0.85f), fill: true);
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmo(new Color(1f, 0.85f, 0.2f, 0.95f), fill: false);
    }

    void DrawGizmo(Color wire, bool fill)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = wire;
        Gizmos.DrawWireCube(Vector3.zero, size);
        if (!fill)
            return;
        Gizmos.color = new Color(wire.r, wire.g, wire.b, 0.08f);
        Gizmos.DrawCube(Vector3.zero, size);
    }
#endif
}
