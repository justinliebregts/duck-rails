using UnityEngine;

/// <summary>Average world velocity of a few transforms over the last tenth of a second.</summary>
public class VelocitySampler : MonoBehaviour
{
    const float Window = 0.1f;
    const int Capacity = 16;

    Transform[] targets = new Transform[0];
    Vector3[,] positions = new Vector3[Capacity, 0];
    readonly float[] times = new float[Capacity];
    int newest = -1;
    int count;

    public void Track(params Transform[] tracked)
    {
        targets = tracked;
        positions = new Vector3[Capacity, tracked.Length];
        newest = -1;
        count = 0;
    }

    void LateUpdate()
    {
        newest = (newest + 1) % Capacity;
        count = Mathf.Min(count + 1, Capacity);
        times[newest] = Time.time;
        for (int i = 0; i < targets.Length; i++)
            positions[newest, i] = targets[i] != null ? targets[i].position : Vector3.zero;
    }

    /// <summary>Velocity of the transform passed at this index to Track. Zero until two frames are recorded.</summary>
    public Vector3 Velocity(int index)
    {
        if (count < 2 || index < 0 || index >= targets.Length || targets[index] == null)
            return Vector3.zero;

        int oldest = newest;
        for (int n = 1; n < count; n++)
        {
            int slot = (newest - n + Capacity) % Capacity;
            oldest = slot;
            if (times[newest] - times[slot] >= Window)
                break;
        }
        float dt = times[newest] - times[oldest];
        return dt > 1e-4f ? (positions[newest, index] - positions[oldest, index]) / dt : Vector3.zero;
    }
}
