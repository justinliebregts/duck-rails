using UnityEngine;

/// <summary>Local presentation of the existing shared fuel value; owns no gameplay state.</summary>
public sealed class RailCockpitFuelBar : MonoBehaviour
{
    public MeshFilter segments;
    public float capacity = RailResourceBalance.Capacity;
    Mesh displayMesh;
    int[] fullIndices;
    int shown = -1;

    void LateUpdate() { SetFuel(PhotonSession.CurrentFuel); }

    public void SetFuel(float fuel)
    {
        if (segments == null || segments.sharedMesh == null) return;
        if (displayMesh == null)
        {
            displayMesh = Instantiate(segments.sharedMesh);
            displayMesh.name = "Fuel segments (instance)";
            fullIndices = displayMesh.triangles;
            segments.sharedMesh = displayMesh;
        }
        int count = Mathf.CeilToInt(Mathf.Clamp01(fuel / Mathf.Max(.01f, capacity)) * 10);
        if (shown == count) return;
        shown = count;
        displayMesh.SetTriangles(fullIndices, 0, count * 6, 0, false);
    }

    void OnDestroy()
    {
        if (displayMesh == null) return;
        if (Application.isPlaying) Destroy(displayMesh);
        else DestroyImmediate(displayMesh);
    }
}
