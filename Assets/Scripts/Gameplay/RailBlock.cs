using System;
using UnityEngine;

/// <summary>Authored at rail entry (0,0,0), +Z forward, unit scale. Graph coordinates are local.</summary>
public sealed class RailBlock : MonoBehaviour
{
    public string id, displayName;
    public string[] tags;
    public float length = 160;
    public Transform entry, exit, stop;
    public TextAsset graph;
    public Bounds enemyBounds = new Bounds(new Vector3(23, 4, 80), new Vector3(28, 12, 70));
    public RailSpawnSet[] variations;
    public Vector3[] track;
    public float TrackLength
    {
        get { float total = 0; for (int i = 1; i < track.Length; i++) total += Vector3.Distance(track[i - 1], track[i]); return total; }
    }
    public void Sample(float distance, out Vector3 position, out Quaternion rotation)
    {
        for (int i = 1; i < track.Length; i++)
        {
            Vector3 delta = track[i] - track[i - 1]; float segment = delta.magnitude;
            if (distance <= segment || i == track.Length - 1)
            {
                position = transform.TransformPoint(track[i - 1] + delta * (distance / segment));
                // Interpolate adjacent tangent directions to avoid a yaw snap at each sampled bend.
                Vector3 a = i > 1 ? (track[i] - track[i - 2]).normalized : Vector3.forward;
                Vector3 b = i + 1 < track.Length ? (track[i + 1] - track[i - 1]).normalized : Vector3.forward;
                rotation = transform.rotation * Quaternion.LookRotation(Vector3.Slerp(a, b, Mathf.Clamp01(distance / segment)));
                return;
            }
            distance -= segment;
        }
        position = transform.position; rotation = transform.rotation;
    }
}

[Serializable]
public sealed class RailSpawnSet
{
    public Transform[] coal, enemies;
}

[Serializable]
public sealed class RailRouteState
{
    public string run;
    public string[] blocks;
    public int[] variations;
    public Vector3[] placements;
}
