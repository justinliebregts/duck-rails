using UnityEngine;

/// <summary>
/// Ported from Duckz DistantMountains (01f8e3e4). The ring sits 2.6-3.7 km out, far past the
/// camera's far plane, so its real bounds would be frustum culled. Its shader pins depth just
/// inside the far plane; this bounds override (which always contains the camera) keeps it drawn.
/// Duck Rails centres the ring on the assembled route so the train travels through its middle.
/// Created and coloured at runtime by <see cref="RailAtmosphere"/>; it has no gameplay state.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
public sealed class RailDistantMountains : MonoBehaviour
{
    const float BoundsSize = 20000f;
    // Route centre used when no route exists (editor preview of an unbuilt scene).
    public const float DefaultCentreZ = 600f;
    [Tooltip("Height of the ring origin. The ridge bases sit 250 m below it.")]
    public float height;

    void OnEnable()
    {
        GetComponent<MeshRenderer>().localBounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
    }

    /// <summary>Centres the ring between the starting station and the end of the finale.</summary>
    public void FollowRoute()
    {
        var route = RailRoute.Instance;
        if (route == null && !Application.isPlaying) route = FindFirstObjectByType<RailRoute>();
        var target = new Vector3(0, height, DefaultCentreZ);
        if (route != null && route.startingStation != null && route.finaleRoot != null && route.finale != null)
            target.z = (route.startingStation.transform.position.z + route.finaleRoot.position.z + route.finale.length) * .5f;
        if (transform.position != target) transform.position = target;
    }
}
