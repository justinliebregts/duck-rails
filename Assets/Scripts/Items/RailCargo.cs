using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Train-relative loose cargo poses. No parenting or physical holder is required.</summary>
[DefaultExecutionOrder(10500)]
public sealed class RailCargo : MonoBehaviourPun, IPunObservable
{
    public static readonly HashSet<RailCargo> Active = new HashSet<RailCargo>();
    Rigidbody body;
    PhotonGrabbable item;
    BagObject bag;
    PhotonTransformView transformSync;
    bool attached, received, previousLoose;
    Vector3 localPose;
    Quaternion localRotation;
    string run;
    public bool StateReady => photonView.IsMine || received;
    bool Loose => item != null ? !item.IsHeldByAnyPlayer && !item.IsStashed : bag != null && bag.State == BagState.Dropped;
    void Awake()
    {
        body = GetComponent<Rigidbody>(); item = GetComponent<PhotonGrabbable>();
        bag = GetComponent<BagObject>(); transformSync = GetComponent<PhotonTransformView>();
        previousLoose = Loose;
    }
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);
    public void Carry(Vector3 delta)
    {
        if (!photonView.IsMine || !attached || !Loose || body == null) return;
        body.position += delta;
        transform.position = body.position;
    }
    void LateUpdate()
    {
        var train = RailTrain.Instance;
        if (photonView.IsMine)
        {
            bool wasAttached = attached;
            attached = train != null && Loose && train.Contains(transform.position);
            if (attached && !previousLoose && body != null && !body.isKinematic)
                body.linearVelocity -= Vector3.forward * train.Speed;
            if (attached)
            {
                localPose = train.transform.InverseTransformPoint(transform.position);
                localRotation = Quaternion.Inverse(train.transform.rotation) * transform.rotation;
                run = PhotonSession.RunId;
            }
            // Cargo velocity is relative aboard the carriage. Leaving the volume restores train velocity.
            if (wasAttached && !attached && Loose && body != null && !body.isKinematic && train != null)
                body.linearVelocity += Vector3.forward * train.Speed;
            previousLoose = Loose;
        }
        else if (received && attached && Loose && train != null && run == PhotonSession.RunId)
        {
            transform.SetPositionAndRotation(train.transform.TransformPoint(localPose), train.transform.rotation * localRotation);
        }
        // Grabbable still owns held/stowed state and authority. Its stock world pose is consumed but suppressed aboard.
        if (transformSync != null && Loose) transformSync.enabled = !attached;
    }
    public object[] CaptureFrame()
    {
        var train = RailTrain.Instance;
        bool aboard = train != null && Loose && train.Contains(transform.position);
        return new object[] { aboard, PhotonSession.RunId,
            aboard ? train.transform.InverseTransformPoint(transform.position) : Vector3.zero,
            aboard ? Quaternion.Inverse(train.transform.rotation) * transform.rotation : Quaternion.identity };
    }
    public void RestoreFrame(object[] state)
    {
        if (state == null || state.Length != 4) return;
        attached = (bool)state[0]; run = (string)state[1]; localPose = (Vector3)state[2]; localRotation = (Quaternion)state[3];
        received = true; previousLoose = Loose;
        var train = RailTrain.Instance;
        if (attached && train != null && run == PhotonSession.RunId && Loose)
        {
            transform.SetPositionAndRotation(train.transform.TransformPoint(localPose), train.transform.rotation * localRotation);
            if (body != null) { body.position = transform.position; body.rotation = transform.rotation; }
        }
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(attached); stream.SendNext(run ?? ""); stream.SendNext(localPose); stream.SendNext(localRotation);
        }
        else
        {
            bool nextAttached = (bool)stream.ReceiveNext(); string nextRun = (string)stream.ReceiveNext();
            Vector3 nextPose = (Vector3)stream.ReceiveNext(); Quaternion nextRotation = (Quaternion)stream.ReceiveNext();
            if (photonView.IsMine || info.Sender != photonView.Controller || (item != null && !item.AcceptRecoveryPacket)) return;
            attached = nextAttached; run = nextRun; localPose = nextPose; localRotation = nextRotation; received = true;
        }
    }
}
