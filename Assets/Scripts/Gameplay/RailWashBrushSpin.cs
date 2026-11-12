using UnityEngine;

/// <summary>Cosmetic wash rollers; collision stays on the stationary equipment frame.</summary>
public sealed class RailWashBrushSpin : MonoBehaviour
{
    public float degreesPerSecond = 38f;
    void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
}
