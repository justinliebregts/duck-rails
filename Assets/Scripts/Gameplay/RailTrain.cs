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
    bool initialized;
    void OnEnable() { Instance = this; initialized = false; }
    void OnDisable() { if (Instance == this) Instance = null; }
    void Update()
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain) return;
        var command = PhotonSession.Get(RailSessionRules.Motion, new object[] { PhotonNetwork.Time, 0f, 0f, 0f });
        RailSessionRules.EvaluateMotion(command, PhotonNetwork.Time, out float distance, out float speed);
        Speed = speed;
        Vector3 next = origin + Vector3.forward * distance;
        Delta = initialized ? next - transform.position : Vector3.zero;
        foreach (var cargo in RailCargo.Active) cargo.Carry(Delta);
        cabin.MoveCabin(next, Quaternion.identity, initialized);
        initialized = true;
    }
    public bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) < 2.45f && Mathf.Abs(local.z) < 4.9f && local.y > -.1f && local.y < 2.8f;
    }
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
