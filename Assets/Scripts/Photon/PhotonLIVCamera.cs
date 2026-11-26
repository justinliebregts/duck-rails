using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// One cosmetic tablet per human avatar. Reliable transitions/final poses, unreliable
/// motion, and an explicit snapshot request for late joiners. No room item ownership.
/// </summary>
[DefaultExecutionOrder(150), DisallowMultipleComponent]
public sealed class PhotonLIVCamera : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Duckz uses 112; Rails already uses 101-119 (112 is PhotonMasterHandoff.PickupRefreshCode).
    public const byte MotionEvent = 120;
    const float SendInterval = 1f / 15f;
    const float SettleTime = .2f;
    static readonly RaiseEventOptions Others = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
    PhotonPlayer player;
    RemoteLIVCamera visual;
    CameraState sent, received;
    int sequence, receivedSequence;
    bool hasSent, hasReceived, settling;
    float nextSend, lastMotion, requestAt;
    string spawnToken;
    readonly byte[] motionPacket = new byte[CameraState.PacketBytes];
    readonly object[] motionEnvelope = new object[3];
    int envelopeViewId;
    string envelopeToken;
    bool poseSettled;
    // Smoothed unheld pose in the received state's own space (world, or train-local).
    Vector3 shownPosition;
    Quaternion shownRotation = Quaternion.identity;

    public struct CameraState
    {
        public const int PacketBytes = 73;
        // Closed, world, left hand, right hand, train-local. World pose remains a fallback until hands arrive.
        // Rails: an unheld tablet aboard the moving train is sent relative to the train, so
        // remote peers rebuild it in their own synced train frame without trailing behind.
        public byte mode;
        public Vector3 position, scale, handPosition;
        public Quaternion rotation, handRotation;
        public bool Open => mode != 0;
        public bool Held => mode == 2 || mode == 3;
        public bool TrainLocal => mode == 4;

        public bool Valid => mode <= 4 && (!Open ||
            (Finite(position) && Finite(scale) && scale.x > 0 && scale.y > 0 && scale.z > 0
            && scale.x <= 10 && scale.y <= 10 && scale.z <= 10 && RotationValid(rotation)
            && Finite(handPosition) && handPosition.sqrMagnitude < 100 && RotationValid(handRotation)));

        public bool MovedFrom(CameraState previous) => mode != previous.mode ||
            (scale - previous.scale).sqrMagnitude > .000001f ||
            (Held ? (handPosition - previous.handPosition).sqrMagnitude > .000001f
                || Quaternion.Angle(handRotation, previous.handRotation) > .2f
                : (position - previous.position).sqrMagnitude > .000001f
                || Quaternion.Angle(rotation, previous.rotation) > .2f);

        public byte[] Pack(int id)
        {
            var data = new byte[PacketBytes];
            Encode(id, data);
            return data;
        }

        // Fixed-size, reusable motion buffer. Reliable transitions may allocate their own snapshot.
        public void Encode(int id, byte[] data)
        {
            int i = 0;
            WriteInt(data, ref i, id); data[i++] = mode;
            WriteVector(data, ref i, position); WriteRotation(data, ref i, rotation);
            WriteVector(data, ref i, scale); WriteVector(data, ref i, handPosition);
            WriteRotation(data, ref i, handRotation);
        }

        public static bool TryUnpack(byte[] data, out int id, out CameraState state)
        {
            id = 0; state = default;
            if (data == null || data.Length != PacketBytes) return false;
            int i = 0;
            id = ReadInt(data, ref i); state.mode = data[i++];
            state.position = ReadVector(data, ref i); state.rotation = ReadRotation(data, ref i);
            state.scale = ReadVector(data, ref i); state.handPosition = ReadVector(data, ref i);
            state.handRotation = ReadRotation(data, ref i);
            return state.Valid;
        }
        static void WriteInt(byte[] data, ref int i, int value)
        { data[i++] = (byte)value; data[i++] = (byte)(value >> 8); data[i++] = (byte)(value >> 16); data[i++] = (byte)(value >> 24); }
        static int ReadInt(byte[] data, ref int i)
        { int v = data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24; i += 4; return v; }
        static void WriteFloat(byte[] data, ref int i, float v) => WriteInt(data, ref i, System.BitConverter.SingleToInt32Bits(v));
        static float ReadFloat(byte[] data, ref int i) => System.BitConverter.Int32BitsToSingle(ReadInt(data, ref i));
        static void WriteVector(byte[] b, ref int i, Vector3 v)
        { WriteFloat(b, ref i, v.x); WriteFloat(b, ref i, v.y); WriteFloat(b, ref i, v.z); }
        static Vector3 ReadVector(byte[] b, ref int i) => new Vector3(ReadFloat(b, ref i), ReadFloat(b, ref i), ReadFloat(b, ref i));
        static void WriteRotation(byte[] b, ref int i, Quaternion q)
        { WriteFloat(b, ref i, q.x); WriteFloat(b, ref i, q.y); WriteFloat(b, ref i, q.z); WriteFloat(b, ref i, q.w); }
        static Quaternion ReadRotation(byte[] b, ref int i) => new Quaternion(ReadFloat(b, ref i), ReadFloat(b, ref i), ReadFloat(b, ref i), ReadFloat(b, ref i));
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static bool RotationValid(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y)
            && float.IsFinite(q.z) && float.IsFinite(q.w) && Mathf.Abs(Quaternion.Dot(q, q) - 1) < .01f;
    }

    void Awake() => player = GetComponent<PhotonPlayer>();

    void Start()
    {
        spawnToken = photonView.SpawnToken;
        RequestSnapshot();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        hasSent = hasReceived = settling = false;
        requestAt = nextSend = 0;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (visual != null) Destroy(visual.gameObject);
        visual = null;
        hasReceived = false;
    }

    CameraState Capture()
    {
        var tablet = TabletGrab.Local;
        if (tablet == null || !tablet.isActiveAndEnabled || !tablet.IsVisible) return default;
        Transform root = tablet.transform;
        var state = new CameraState { mode = 1, position = root.position, rotation = root.rotation,
            scale = root.lossyScale, handRotation = Quaternion.identity };
        var grab = tablet.GrabSource;
        if (grab != null && grab.Holder != null)
        {
            bool left = grab.Holder.IsControllerLTouch;
            if (player.TryGetHandPose(left, out Vector3 pos, out Quaternion rot))
            {
                state.mode = (byte)(left ? 2 : 3);
                state.handPosition = Quaternion.Inverse(rot) * (root.position - pos);
                state.handRotation = Quaternion.Inverse(rot) * root.rotation;
            }
        }
        // Rails: a placed or following tablet on the moving train is sent train-local while its
        // owner rides. One left behind at a stop (outside the train) stays a world pose.
        if (state.mode == 1 && RailTrain.TryPassengerFrame(out Transform frame)
            && RailTrain.Instance.ContainsPadded(root.position))
        {
            state.mode = 4;
            state.position = frame.InverseTransformPoint(root.position);
            state.rotation = Quaternion.Inverse(frame.rotation) * root.rotation;
        }
        return state;
    }

    void LateUpdate()
    {
        if (player == null || !PhotonNetwork.InRoom || photonView.Owner == null) return;
        if (spawnToken != photonView.SpawnToken)
        {
            spawnToken = photonView.SpawnToken;
            hasSent = hasReceived = false;
            sequence = receivedSequence = 0;
        }
        if (player.IsLocalHuman)
        {
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + SendInterval;
            var state = Capture();
            if (!state.Valid) return;
            bool transition = !hasSent || state.mode != sent.mode;
            bool moved = state.Open && state.MovedFrom(sent);
            if (transition || moved)
            {
                Publish(state, transition);
                lastMotion = Time.unscaledTime;
                settling = !transition;
            }
            else if (settling && Time.unscaledTime - lastMotion >= SettleTime)
            {
                // The last unreliable motion packet may have been lost. This final pose
                // is reliable and leaves a stationary tablet with zero ongoing traffic.
                Publish(state, true);
                settling = false;
            }
            return;
        }

        if (!hasReceived && Time.unscaledTime >= requestAt) RequestSnapshot();
        if (!hasReceived || !received.Open || photonView.Owner.IsInactive)
        {
            if (visual != null) visual.gameObject.SetActive(false);
            return;
        }
        // A train-local pose needs this peer's synced train; without one there is nowhere to show it.
        Transform frame = null;
        if (received.TrainLocal && !RailTrain.TryFrame(int.MaxValue, out frame))
        {
            if (visual != null) visual.gameObject.SetActive(false);
            return;
        }
        if (visual == null)
        {
            var prefab = Resources.Load<GameObject>("RemoteLIVCamera");
            if (prefab == null) return;
            visual = Instantiate(prefab).GetComponent<RemoteLIVCamera>();
            visual.name = "Remote LIV Camera (" + photonView.OwnerActorNr + ")";
            shownPosition = received.position;
            shownRotation = received.rotation;
            poseSettled = false;
        }
        if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);
        if (received.Held)
        {
            Vector3 position = received.position;
            Quaternion rotation = received.rotation;
            if (player.TryGetHandPose(received.mode == 2, out var handPos, out var handRot))
            {
                position = handPos + handRot * received.handPosition;
                rotation = handRot * received.handRotation;
            }
            // Held motion uses the already-interpolated remote hands without a second delay.
            visual.transform.SetPositionAndRotation(position, rotation);
            visual.transform.localScale = received.scale;
            return;
        }
        // A settled world pose needs no work; a settled train-local pose still rides the train.
        if (poseSettled && frame == null) return;
        if (!poseSettled)
        {
            float t = 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime);
            shownPosition = Vector3.Lerp(shownPosition, received.position, t);
            shownRotation = Quaternion.Slerp(shownRotation, received.rotation, t);
            if ((shownPosition - received.position).sqrMagnitude < .00000001f
                && Quaternion.Angle(shownRotation, received.rotation) < .01f)
            {
                shownPosition = received.position;
                shownRotation = received.rotation;
                poseSettled = true;
            }
        }
        if (frame != null)
            visual.transform.SetPositionAndRotation(frame.TransformPoint(shownPosition), frame.rotation * shownRotation);
        else
            visual.transform.SetPositionAndRotation(shownPosition, shownRotation);
        visual.transform.localScale = received.scale;
    }

    void Publish(CameraState state, bool reliable)
    {
        sent = state; hasSent = true;
        int id = unchecked(++sequence);
        if (reliable)
            photonView.RPC(nameof(ReceiveCameraSnapshot), RpcTarget.Others, new object[] { photonView.SpawnToken, state.Pack(id) });
        else
        {
            if (envelopeViewId != photonView.ViewID || envelopeToken != photonView.SpawnToken)
            {
                envelopeViewId = photonView.ViewID; envelopeToken = photonView.SpawnToken;
                motionEnvelope[0] = envelopeViewId; motionEnvelope[1] = envelopeToken; motionEnvelope[2] = motionPacket;
            }
            state.Encode(id, motionPacket);
            // Photon serializes OpRaiseEvent synchronously; the encoded send queue owns its bytes.
            PhotonNetwork.RaiseEvent(MotionEvent, motionEnvelope, Others, SendOptions.SendUnreliable);
        }
    }

    void RequestSnapshot()
    {
        requestAt = Time.unscaledTime + 2;
        if (PhotonNetwork.InRoom && photonView.Owner != null && !photonView.IsMine)
            photonView.RPC(nameof(RequestCameraSnapshot), photonView.Owner, photonView.SpawnToken);
    }

    [PunRPC]
    void RequestCameraSnapshot(string token, PhotonMessageInfo info)
    {
        if (!player.IsLocalHuman || info.Sender == null || info.Sender.IsLocal || token != photonView.SpawnToken) return;
        var state = Capture();
        if (!state.Valid) return;
        // Increment even for a targeted snapshot; other receivers tolerate gaps.
        photonView.RPC(nameof(ReceiveCameraSnapshot), info.Sender,
            new object[] { photonView.SpawnToken, state.Pack(unchecked(++sequence)) });
    }

    [PunRPC]
    void ReceiveCameraSnapshot(string token, byte[] packet, PhotonMessageInfo info)
    {
        if (token == photonView.SpawnToken && info.Sender != null) Accept(packet, info.Sender.ActorNumber);
    }

    public void OnEvent(EventData data)
    {
        if (data.Code != MotionEvent || !(data.CustomData is object[] envelope) || envelope.Length != 3
            || !(envelope[0] is int viewId) || viewId != photonView.ViewID
            || !Equals(envelope[1], photonView.SpawnToken) || !(envelope[2] is byte[] packet)) return;
        Accept(packet, data.Sender);
    }

    void Accept(byte[] packet, int actor)
    {
        if (!isActiveAndEnabled || player.IsLocalHuman || actor != photonView.OwnerActorNr
            || !CameraState.TryUnpack(packet, out int seq, out var state)
            || (hasReceived && !IsNewer(seq, receivedSequence))) return;
        bool snap = !hasReceived || received.mode != state.mode ||
            (received.position - state.position).sqrMagnitude > 4;
        received = state; receivedSequence = seq; hasReceived = true;
        poseSettled = false;
        // Mode changes also switch coordinate space (world/train-local), so they always snap.
        if (snap) { shownPosition = state.position; shownRotation = state.rotation; }
        if (visual != null && !state.Open) visual.gameObject.SetActive(false);
    }

    public static bool IsNewer(int sequence, int previous) => unchecked(sequence - previous) > 0;
}
