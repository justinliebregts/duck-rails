using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Opens the duck lips from the Photon speaker loudness. Closed at rest, wider when louder.
/// </summary>
public class PhotonVoiceMouth : MonoBehaviour
{
    [SerializeField] Transform topLip;
    [SerializeField] Transform bottomLip;
    [SerializeField] float maxAngle = 65f;
    [SerializeField] float fullOpenLoudness = 0.06f;
    [SerializeField] float quietScaleX = 1f;
    [SerializeField] float loudScaleX = 0.6f;

    PhotonVoiceLevel _level;
    Quaternion _topClosed;
    Quaternion _bottomClosed;
    Vector3 _topScale;
    Vector3 _bottomScale;
    float _angle;
    bool _local;
    static Recorder _localRecorder;

    public float Loudness
    {
        get
        {
            var level = Level();
            if (level != null && level.TryGetComponent(out AudioSource source) && source.isPlaying)
                return level.Loudness;

            if (_local)
                return LocalMicLoudness();
            return 0f;
        }
    }

    /// <summary>Drive a different pair of lips, e.g. after a skin change. Their current pose becomes the closed pose.</summary>
    public void Bind(Transform top, Transform bottom)
    {
        topLip = top;
        bottomLip = bottom;
        CaptureClosed();
    }

    void Awake() => CaptureClosed();

    void CaptureClosed()
    {
        if (topLip != null)
        {
            _topClosed = topLip.localRotation;
            _topScale = topLip.localScale;
        }
        if (bottomLip != null)
        {
            _bottomClosed = bottomLip.localRotation;
            _bottomScale = bottomLip.localScale;
        }
    }

    void Start()
    {
        var view = GetComponent<PhotonView>();
        _local = view != null && view.IsMine && GetComponent<DuckCompanion>() == null;
    }

    Transform _poseTop;
    Transform _poseBottom;
    Quaternion _poseTopClosed;
    Quaternion _poseBottomClosed;
    Vector3 _poseTopScale;
    Vector3 _poseBottomScale;
    float _sampledLoudness;

    /// <summary>Mic level sampled this frame for the visible capture face.</summary>
    public float SampledLoudness => _sampledLoudness;

    /// <summary>Read the local mic once this frame. PoseLocal uses the stored value.</summary>
    public void SampleLocal()
    {
        _sampledLoudness = LocalMicLoudness();
    }

    /// <summary>One lip pose from the sampled local mic, written onto the visible capture face.</summary>
    public void PoseLocal(Transform top, Transform bottom)
    {
        if (top != _poseTop || bottom != _poseBottom)
        {
            _poseTop = top;
            _poseBottom = bottom;
            if (top != null)
            {
                _poseTopClosed = top.localRotation;
                _poseTopScale = top.localScale;
            }
            if (bottom != null)
            {
                _poseBottomClosed = bottom.localRotation;
                _poseBottomScale = bottom.localScale;
            }
        }

        ApplyLips(_sampledLoudness, top, bottom, _poseTopClosed, _poseBottomClosed, _poseTopScale, _poseBottomScale);
    }

    void LateUpdate()
    {
        if (_local || (topLip == null && bottomLip == null))
            return;

        float loudness = 0f;
        PhotonVoiceLevel level = Level();
        if (level != null && fullOpenLoudness > 0f && level.TryGetComponent(out AudioSource source) && source.isPlaying)
            loudness = level.Loudness;

        ApplyLips(loudness, topLip, bottomLip, _topClosed, _bottomClosed, _topScale, _bottomScale);
    }

    void ApplyLips(float loudness, Transform top, Transform bottom, Quaternion topClosed, Quaternion bottomClosed, Vector3 topScale, Vector3 bottomScale)
    {
        float target = 0f;
        if (fullOpenLoudness > 0f)
            target = Mathf.Clamp01(loudness / fullOpenLoudness) * maxAngle;

        _angle = Mathf.Lerp(_angle, target, 1f - Mathf.Exp(-70f * Time.deltaTime));
        float opened = maxAngle > 0.001f ? Mathf.Clamp01(_angle / maxAngle) : 0f;
        float scaleX = Mathf.Lerp(quietScaleX, loudScaleX, opened);
        if (bottom != null)
        {
            bottom.localRotation = bottomClosed * Quaternion.Euler(_angle, 0f, 0f);
            bottom.localScale = new Vector3(scaleX, bottomScale.y, bottomScale.z);
        }
        if (top != null)
        {
            top.localRotation = topClosed * Quaternion.Euler(-_angle, 0f, 0f);
            top.localScale = new Vector3(scaleX, topScale.y, topScale.z);
        }
    }

    PhotonVoiceLevel Level()
    {
        if (_level != null)
            return _level;

        if (GetComponent<DuckCompanion>() != null)
        {
            _level = GetComponentInChildren<PhotonVoiceLevel>();
            return _level;
        }

        var voice = GetComponent<PhotonVoiceView>();
        Speaker speaker = voice != null ? voice.SpeakerInUse : null;
        if (speaker == null)
            return null;

        _level = speaker.GetComponent<PhotonVoiceLevel>();
        if (_level == null)
            _level = speaker.gameObject.AddComponent<PhotonVoiceLevel>();
        return _level;
    }

    float LocalMicLoudness()
    {
        if (_localRecorder != null && _localRecorder.LevelMeter == null)
            _localRecorder = null;

        if (_localRecorder == null)
        {
            PunVoiceClient client = FindAnyObjectByType<PunVoiceClient>();
            Recorder primary = client != null ? client.PrimaryRecorder : null;
            if (primary != null && primary.LevelMeter != null)
                _localRecorder = primary;
        }

        Photon.Voice.AudioUtil.ILevelMeter meter = _localRecorder != null ? _localRecorder.LevelMeter : null;
        if (meter == null)
            return 0f;

        // Same shape as PhotonVoiceLevel: sqrt of average absolute amplitude.
        return Mathf.Sqrt(Mathf.Max(0f, meter.CurrentAvgAmp));
    }
}

/// <summary>
/// Loudness of one speaker. Lives on the voice audio object so it still hears spatialized speech.
/// </summary>
public class PhotonVoiceLevel : MonoBehaviour
{
    public float Loudness { get; private set; }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float sum = 0f;
        for (int i = 0; i < data.Length; i++)
            sum += Mathf.Abs(data[i]);
        Loudness = data.Length > 0 ? Mathf.Sqrt(sum / data.Length) : 0f;
    }
}
