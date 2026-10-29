using Photon.Pun;
using UnityEngine;

/// <summary>
/// Hold any use button on the holding hand to send your voice to every other walkie.
/// Releasing all use buttons or the radio stops transmission. This radio stays quiet
/// for your own voice and still plays everyone else.
/// </summary>
[RequireComponent(typeof(PhotonGrabbable))]
[RequireComponent(typeof(PhotonView))]
public sealed class WalkieTalkie : MonoBehaviour, IPunObservable
{
    public const string PrefabName = "ItemWalkieTalkie";
    // Desktop stand-in for the index trigger. Hold it while the radio is in a hand.
    const KeyCode DesktopTalkKey = KeyCode.T;

    PhotonGrabbable grabbable;
    PhotonView photonView;
    ItemSounds sounds;
    WalkieSpeaker speaker;
    bool transmitting;

    void Awake()
    {
        grabbable = GetComponent<PhotonGrabbable>();
        photonView = GetComponent<PhotonView>();
        sounds = GetComponent<ItemSounds>();
    }

    void Start()
    {
        speaker = gameObject.AddComponent<WalkieSpeaker>();
        speaker.Configure(photonView != null ? photonView.ViewID : 0, false);
    }

    void Update()
    {
        if (speaker != null && photonView != null)
            speaker.Configure(photonView.ViewID, false);

        if (grabbable == null || photonView == null || !photonView.IsMine)
            return;

        PlayerHand hand = grabbable.Holder;
        bool want = !PhotonVoiceChat.IsLocallyMuted
            && hand != null
            && (hand.IsUseHeld || Input.GetKey(DesktopTalkKey));
        if (want == transmitting)
            return;

        SetTransmitting(want);
    }

    void OnDisable()
    {
        if (!transmitting || photonView == null || !photonView.IsMine)
            return;

        SetTransmitting(false);
    }

    void SetTransmitting(bool want)
    {
        if (want == transmitting)
            return;

        transmitting = want;
        if (sounds != null)
            sounds.Play(want ? ItemSound.TurnOn : ItemSound.TurnOff);

        int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        int viewId = photonView != null ? photonView.ViewID : 0;
        if (actor != 0 && viewId != 0)
            WalkieRadio.SetTransmitting(actor, viewId, want);
        if (photonView != null && PhotonNetwork.InRoom)
            photonView.RPC(nameof(RpcTalking), RpcTarget.Others, want);
    }

    void OnDestroy()
    {
        if (photonView != null)
            WalkieRadio.ClearView(photonView.ViewID);
    }

    [PunRPC]
    void RpcTalking(bool talking, PhotonMessageInfo info)
    {
        if (photonView != null && photonView.IsMine)
            return;
        ApplyRemote(talking, info.Sender != null ? info.Sender.ActorNumber : 0);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transmitting);
            return;
        }

        if (photonView != null && photonView.IsMine)
        {
            stream.ReceiveNext();
            return;
        }

        ApplyRemote((bool)stream.ReceiveNext(), info.Sender != null ? info.Sender.ActorNumber : 0);
    }

    void ApplyRemote(bool talking, int actor)
    {
        transmitting = talking;
        if (photonView != null)
            WalkieRadio.SetTransmitting(actor, photonView.ViewID, talking);
    }
}

/// <summary>
/// Plays the walkie mix from this object. A bag slot is receive-only: other players, never your own voice.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class WalkieSpeaker : MonoBehaviour
{
    const int HeadCount = 16;
    const float HighpassHz = 300f;
    const float LowpassHz = 3000f;
    const float OutputGain = 3f;

    AudioSource source;
    AudioClip clip;
    int viewId;
    bool receiveOnly;
    volatile bool configured;
    readonly WalkieRadio.ReadHead[] heads = new WalkieRadio.ReadHead[HeadCount];
    float[] scratch = System.Array.Empty<float>();
    Biquad highpass;
    Biquad lowpass;

    public void Configure(int sourceViewId, bool othersOnly)
    {
        viewId = sourceViewId;
        receiveOnly = othersOnly;
        if (configured)
            return;
        highpass = Biquad.Highpass(HighpassHz, WalkieRadio.SampleRate);
        lowpass = Biquad.Lowpass(LowpassHz, WalkieRadio.SampleRate);
        configured = true;
    }

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.spatialize = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.5f;
        source.maxDistance = 8f;
        source.playOnAwake = false;
        source.loop = true;
        source.priority = 64;
        source.bypassReverbZones = false;

        clip = AudioClip.Create("Walkie", WalkieRadio.SampleRate, 1, WalkieRadio.SampleRate, true, OnPcm);
        source.clip = clip;
        source.Play();
    }

    void OnDestroy()
    {
        if (source != null)
            source.Stop();
        if (clip != null)
            Destroy(clip);
    }

    void OnPcm(float[] data)
    {
        if (!configured)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = 0f;
            return;
        }

        if (scratch.Length < data.Length)
            scratch = new float[data.Length];

        int exclude = 0;
        if (receiveOnly)
            exclude = WalkieRadio.LocalActor;
        else if (viewId != 0 && WalkieRadio.TryTransmitter(viewId, out int actor))
            exclude = actor;

        WalkieRadio.Mix(data, exclude, heads, scratch);

        for (int i = 0; i < data.Length; i++)
        {
            float sample = lowpass.Process(highpass.Process(data[i])) * 1.7f;
            float abs = sample < 0f ? -sample : sample;
            data[i] = sample / (1f + abs) * OutputGain;
        }
    }

    struct Biquad
    {
        float b0, b1, b2, a1, a2, z1, z2;

        public static Biquad Highpass(float freq, float sampleRate) => Create(freq, sampleRate, true);
        public static Biquad Lowpass(float freq, float sampleRate) => Create(freq, sampleRate, false);

        static Biquad Create(float freq, float sampleRate, bool high)
        {
            float w0 = 2f * Mathf.PI * freq / sampleRate;
            float cos = Mathf.Cos(w0);
            float sin = Mathf.Sin(w0);
            float alpha = sin / 1.41421356f;
            float b0n = high ? (1f + cos) * 0.5f : (1f - cos) * 0.5f;
            float b1n = high ? -(1f + cos) : 1f - cos;
            float b2n = b0n;
            float a0 = 1f + alpha;
            return new Biquad
            {
                b0 = b0n / a0,
                b1 = b1n / a0,
                b2 = b2n / a0,
                a1 = (-2f * cos) / a0,
                a2 = (1f - alpha) / a0
            };
        }

        public float Process(float x)
        {
            float y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }
    }
}
