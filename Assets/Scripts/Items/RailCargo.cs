using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Train-relative loose cargo poses. No parenting or physical holder is required.</summary>
[DefaultExecutionOrder(10500)]
public sealed class RailCargo : MonoBehaviourPun, IPunObservable
{
    public static readonly HashSet<RailCargo> Active = new HashSet<RailCargo>();
    const string CompoundPurchaseTag = "rails-compound-purchase-v1";
    // Retained spawn data lets peers and late joiners distinguish shop deliveries
    // from expedition leftovers. A private departure changes the run in-place.
    public static object[] WithCompoundPurchase(object[] data, string buyer)
    {
        if (!PhotonSession.IsRailsRoom || PhotonSession.OnTrain || !PhotonNetwork.InRoom) return data;
        var tagged = new object[data.Length + 1];
        System.Array.Copy(data, tagged, data.Length);
        tagged[data.Length] = new object[] { CompoundPurchaseTag, PhotonNetwork.CurrentRoom.Name, PhotonSession.RunId, buyer };
        return tagged;
    }
    public static bool IsCompoundPurchase(PhotonView view) => PurchaseLifetime(view) != null;
    static object[] PurchaseLifetime(PhotonView view)
    {
        var data = view != null ? view.InstantiationData : null;
        return data != null && data.Length > 0 && data[data.Length - 1] is object[] lifetime &&
            lifetime.Length == 4 && lifetime[0] as string == CompoundPurchaseTag ? lifetime : null;
    }
    public static bool KeepCompoundPurchase(PhotonView view)
    {
        var lifetime = PurchaseLifetime(view);
        if (lifetime == null || PhotonSession.OnTrain || !PhotonNetwork.InRoom ||
            lifetime[1] as string != PhotonNetwork.CurrentRoom.Name || lifetime[2] as string != PhotonSession.RunId) return false;
        // Retain an inactive reconnect slot, but clean up after a buyer actually
        // leaves the room (including a public departure to another room).
        foreach (var player in PhotonNetwork.PlayerList)
            if (player.UserId == lifetime[3] as string) return true;
        return false;
    }
    public static Vector3 PassengerCarryOffset { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCarryOffset() => PassengerCarryOffset = Vector3.zero;
    public static void RecordPassengerCarry(Vector3 delta) => PassengerCarryOffset += delta;
    Rigidbody body;
    PhotonGrabbable item;
    BagObject bag;
    HeadEquipment headEquipment;
    PhotonTransformView transformSync;
    bool attached, received, previousLoose, preparedRelease, removed;
    Vector3 localPose;
    Quaternion localRotation;
    string run;
    public bool StateReady => photonView.IsMine || received;
    bool Predicting => item != null && item.PickupPending;
    bool Simulates => photonView.IsMine || Predicting;
    bool Loose => item != null ? !item.IsHeldByAnyPlayer && !item.IsStashed : bag != null && bag.State == BagState.Dropped;
    void Awake()
    {
        // Authored loot is mostly stationary. Publish moving/interaction state
        // at the normal stream rate, then PUN's reliable final rest snapshot.
        // PUN resets its comparison cache when a player joins.
        if (photonView.Synchronization == ViewSynchronization.Unreliable)
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
        body = GetComponent<Rigidbody>(); item = GetComponent<PhotonGrabbable>();
        bag = GetComponent<BagObject>(); transformSync = GetComponent<PhotonTransformView>();
        headEquipment = GetComponent<HeadEquipment>();
        previousLoose = Loose;
    }
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);
    // Match the throw sample's reference frame before the hand detaches. Prediction
    // permits local motion only; Photon publication remains with the actual owner.
    public bool PrepareRelease()
    {
        var train = RailTrain.Instance;
        preparedRelease = train != null && train.Contains(transform.position);
        return preparedRelease;
    }
    public void CompleteRelease()
    {
        if (!preparedRelease) return;
        preparedRelease = false;
        attached = true;
        previousLoose = true; // Velocity was already made relative by the release sampler.
        var train = RailTrain.Instance;
        if (train == null) { attached = false; return; }
        localPose = train.transform.InverseTransformPoint(transform.position);
        localRotation = Quaternion.Inverse(train.transform.rotation) * transform.rotation;
        run = PhotonSession.RunId;
    }
    public void CancelPrediction()
    {
        attached = received = preparedRelease = false;
        previousLoose = Loose;
        if (transformSync != null) transformSync.enabled = Loose;
    }
    public void Carry(Vector3 delta)
    {
        if (!Simulates || !attached || !Loose || body == null) return;
        body.position += delta;
        transform.position = body.position;
    }
    public void Carry(Vector3 previous, Quaternion oldRotation, Vector3 next, Quaternion rotation)
    {
        if (!Simulates || !attached || !Loose || body == null) return;
        Quaternion turn = rotation * Quaternion.Inverse(oldRotation);
        body.position = next + turn * (body.position - previous);
        body.rotation = turn * body.rotation;
        if (!body.isKinematic) { body.linearVelocity = turn * body.linearVelocity; body.angularVelocity = turn * body.angularVelocity; }
        transform.SetPositionAndRotation(body.position, body.rotation);
    }
    void LateUpdate()
    {
        // Loose cargo below the block floor is unrecoverable; its authority removes it from the room.
        if (!removed && photonView.IsMine && Loose && !Predicting && PhotonNetwork.InRoom && PhotonSession.IsRailsRoom
            && (headEquipment == null || !headEquipment.IsEquipped) && RailPlayerBounds.BelowFloor(transform.position))
        {
            removed = true;
            DuckzLog.Log($"[Rails] Removing {name} (view {photonView.ViewID}) below the block floor at {transform.position}", this);
            PhotonNetwork.Destroy(gameObject);
            return;
        }
        var train = RailTrain.Instance;
        if (Simulates)
        {
            bool wasAttached = attached;
            attached = train != null && Loose && train.Contains(transform.position);
            if (attached && !previousLoose && body != null && !body.isKinematic)
                body.linearVelocity -= train.Velocity;
            if (attached)
            {
                localPose = train.transform.InverseTransformPoint(transform.position);
                localRotation = Quaternion.Inverse(train.transform.rotation) * transform.rotation;
                run = PhotonSession.RunId;
            }
            // Cargo velocity is relative aboard the carriage. Leaving the volume restores train velocity.
            if (wasAttached && !attached && Loose && body != null && !body.isKinematic && train != null)
                body.linearVelocity += train.Velocity;
            previousLoose = Loose;
        }
        else if (received && attached && Loose && train != null && run == PhotonSession.RunId)
        {
            transform.SetPositionAndRotation(train.transform.TransformPoint(localPose), train.transform.rotation * localRotation);
        }
        // Grabbable still owns held/stowed state and authority. Its stock world pose is consumed but suppressed aboard.
        if (transformSync != null && Loose) transformSync.enabled = !attached && !Predicting;
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
        if (Predicting || state == null || state.Length != 4) return;
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
            if (Simulates || info.Sender != photonView.Controller || (item != null && !item.AcceptRecoveryPacket)) return;
            attached = nextAttached; run = nextRun; localPose = nextPose; localRotation = nextRotation; received = true;
        }
    }
}
