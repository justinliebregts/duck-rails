using Photon.Pun;
using UnityEngine;

/// <summary>Ordinary shared grabbable/bag item; only deliberate loose insertion burns it.</summary>
public sealed class RailCoal : MonoBehaviour
{
    PhotonView view;
    PhotonGrabbable item;
    double nextRequest;
    void Awake() { view = GetComponent<PhotonView>(); item = GetComponent<PhotonGrabbable>(); }
    void Update()
    {
        var run = RailExpedition.Instance;
        if (run == null || view == null || !view.IsMine || item.IsHeldByAnyPlayer || item.IsStashed || PhotonSession.Phase != RailPhase.Running) return;
        if (PhotonNetwork.Time < nextRequest || Vector3.Distance(transform.position, run.furnace.position) > .75f) return;
        nextRequest = PhotonNetwork.Time + .5;
        run.RequestCoal(view, item);
    }

    // The controller's loose snapshot travels reliably with the request. Remote
    // held/transform streams may still describe the hand when this event arrives.
    public static bool ValidInsertion(int sender, int controller, string spawn, string currentSpawn,
        int version, int currentVersion, Vector3 offset, double age)
        => sender > 0 && sender == controller && !string.IsNullOrEmpty(spawn) && spawn == currentSpawn &&
           version >= currentVersion && offset.sqrMagnitude <= .75f * .75f && age >= -.25 && age <= 2;
}
