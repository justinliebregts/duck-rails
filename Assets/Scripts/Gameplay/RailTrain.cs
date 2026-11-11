using Photon.Pun;
using UnityEngine;

/// <summary>Uses Duckz's actual cabin/passenger carry, driven by retained train commands.</summary>
[DefaultExecutionOrder(-16350)]
public sealed class RailTrain : MonoBehaviour
{
    public static RailTrain Instance { get; private set; }
    public Helicopter cabin;
    public Vector3 origin = new Vector3(0, 1, 70);
    public Vector3 Delta { get; private set; }
    public float Speed { get; private set; }
    public float Distance { get; private set; }
    public Vector3 Velocity => transform.forward * Speed;
    bool initialized;
    void OnEnable() { Instance = this; initialized = false; }
    void OnDisable() { if (Instance == this) Instance = null; }
    void Update()
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain) return;
        var command = PhotonSession.Get(RailSessionRules.Motion, new object[] { PhotonNetwork.Time, 0f, 0f, 0f });
        RailSessionRules.EvaluateTrain(command, PhotonSession.Get(RailSessionRules.Fuel, 100f),
            PhotonNetwork.Time, out float distance, out float speed, out _);
        if (RailRoute.Instance != null && !RailRoute.Instance.Ready) return;
        Speed = speed; Distance = distance;
        Vector3 next = origin + Vector3.forward * distance;
        Quaternion rotation = Quaternion.identity;
        if (RailRoute.Instance != null) RailRoute.Instance.Sample(distance, out next, out rotation);
        Delta = initialized ? next - transform.position : Vector3.zero;
        if (initialized) foreach (var cargo in RailCargo.Active) cargo.Carry(transform.position, transform.rotation, next, rotation);
        cabin.MoveCabin(next, rotation, initialized);
        initialized = true;
    }
    // Local-space half extents / vertical range for the two aboard tests.
    const float TightX = 2.45f, TightZ = 4.9f, TightMinY = -.1f, TightMaxY = 2.8f;
    [Header("Padded 'still with the train' box (train local space; yellow gizmo when selected)")]
    [Tooltip("Local-space min corner (x = left, y = below deck, z = behind).")]
    public Vector3 paddedMin = new Vector3(-4.5f, -3f, -9f);
    [Tooltip("Local-space max corner (x = right, y = above deck, z = in front).")]
    public Vector3 paddedMax = new Vector3(4.5f, 6f, 9f);
    public bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) < TightX && Mathf.Abs(local.z) < TightZ && local.y > TightMinY && local.y < TightMaxY;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green; // Contains(): horns, boarding, cargo
        Gizmos.DrawWireCube(new Vector3(0, (TightMinY + TightMaxY) / 2, 0), new Vector3(TightX * 2, TightMaxY - TightMinY, TightZ * 2));
        Gizmos.color = Color.yellow; // ContainsPadded(): escaped / rescue teleport
        Gizmos.DrawWireCube((paddedMin + paddedMax) / 2, paddedMax - paddedMin);
    }
    /// <summary>Forgiving "still with the train" test: covers hanging off the sides by hand, standing on the
    /// nose or roof. Used for who escapes at the finish and for the end-of-run rescue teleport, so both agree.
    /// Horns, enemy boarding and cargo keep the tight Contains.</summary>
    public bool ContainsPadded(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return local.x > paddedMin.x && local.x < paddedMax.x && local.y > paddedMin.y && local.y < paddedMax.y &&
               local.z > paddedMin.z && local.z < paddedMax.z;
    }
    public Vector3 AboardPoint(int actor) => transform.TransformPoint(new Vector3((actor % 3 - 1) * 1.1f, .35f, -2));
    public static bool TryLocalFrame(out int arrival, out int pad, out Transform frame)
    {
        arrival = int.MaxValue; pad = 0;
        return TryPassengerFrame(out frame);
    }
    public static bool TryFrame(int arrival, out Transform frame)
    {
        frame = arrival == int.MaxValue && Instance != null ? Instance.transform : null;
        return frame != null;
    }
    public static bool TryPassengerFrame(out Transform frame)
    {
        frame = Instance != null && Instance.cabin.HasLocalPassenger ? Instance.transform : null;
        return frame != null;
    }
}
