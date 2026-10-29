using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Cosmetic eye look. Tracks a nearby talking player when the head is already facing them,
/// otherwise small idle glances. Eyes also scale with this duck's own voice, a loud neighbor,
/// and Surprise() pulses. Not written into the pose stream.
/// </summary>
[DefaultExecutionOrder(100)]
public class PhotonEyeLook : MonoBehaviour
{
    [SerializeField] Transform leftEye;
    [SerializeField] Transform rightEye;
    [SerializeField] Vector3 lookAxisEuler;
    [SerializeField] float interestRadius = 4f;
    [SerializeField] float headCone = 55f;
    [SerializeField] float maxYaw = 22f;
    [SerializeField] float maxPitch = 12f;
    [SerializeField] float talkLoudness = 0.02f;
    [SerializeField] float holdTime = 0.8f;
    [SerializeField] float glanceInterval = 1.5f;
    [SerializeField] float smooth = 12f;
    [SerializeField] float voiceScaleThreshold = 0.06f;
    [SerializeField] float voiceFullLoudness = 0.12f;
    [SerializeField] float voiceScale = 1.3f;
    [SerializeField] float heardScale = 1.2f;
    [SerializeField] float surpriseScale = 1.45f;
    [SerializeField] float scaleCap = 1.6f;
    [SerializeField] float surpriseRadius = 4f;
    [SerializeField] float surpriseDuration = 1f;
    [SerializeField] float scaleSmooth = 48f;

    struct Pulse
    {
        public Vector3 Position;
        public float Time;
        public float Strength;
    }

    Quaternion _leftRest;
    Quaternion _rightRest;
    Vector3 _leftScale = Vector3.one;
    Vector3 _rightScale = Vector3.one;
    PhotonView _view;
    PhotonPlayer _player;
    PhotonVoiceMouth _mouth;
    PhotonPlayer _held;
    float _holdUntil;
    int _idleSlot = int.MinValue;
    float _idleYaw;
    float _idlePitch;
    float _ownAmount;
    float _heardAmount;
    bool _local;

    static PhotonPlayer[] _players = Array.Empty<PhotonPlayer>();
    static float _nextPlayerScan;
    static readonly List<Pulse> _pulses = new List<Pulse>(4);

    /// <summary>
    /// A loud sound at this position. Nearby ducks' eyes pop, then settle back over about a second.
    /// Call it on each client that plays the sound. Strength 1 is a full pop.
    /// </summary>
    public static void Surprise(Vector3 position, float strength = 1f)
    {
        if (strength <= 0f)
            return;

        _pulses.Add(new Pulse
        {
            Position = position,
            Time = Time.time,
            Strength = strength,
        });
    }

    /// <summary>Drive a different pair of eyes, e.g. after a skin change. Their current pose becomes the rest pose.</summary>
    public void Bind(Transform left, Transform right)
    {
        leftEye = left;
        rightEye = right;
        CaptureRest();
    }

    void CaptureRest()
    {
        if (leftEye != null)
        {
            _leftRest = leftEye.localRotation;
            _leftScale = leftEye.localScale;
        }
        if (rightEye != null)
        {
            _rightRest = rightEye.localRotation;
            _rightScale = rightEye.localScale;
        }
    }

    void Awake()
    {
        CaptureRest();

        _view = GetComponent<PhotonView>();
        _player = GetComponent<PhotonPlayer>();
        _mouth = GetComponent<PhotonVoiceMouth>();
    }

    void Start()
    {
        _local = _view != null && _view.IsMine && GetComponent<DuckCompanion>() == null;
    }

    void LateUpdate()
    {
        if (_local || (leftEye == null && rightEye == null))
            return;

        Vector3 eyePos = transform.position;
        Vector3 forward = transform.forward;
        bool hasGaze = false;
        if (_player != null)
            hasGaze = _player.TryGetGaze(out eyePos, out forward);

        if (hasGaze)
        {
            var companion = _player.Companion;
            var buddy = companion != null && !companion.IsDead ? PhotonPlayer.ForActor(companion.TargetActor) : null;
            // The response actor is replicated with the command pause. Every
            // peer tracks the same speaker, even when Duck refuses another
            // player's request while already following someone else.
            if (companion != null && companion.State == DuckCompanion.Activity.Waiting
                && companion.CommandResponseSpeaker != null)
                buddy = companion.CommandResponseSpeaker;
            else if (companion != null && companion.ConversationSpeaker != null)
                buddy = companion.ConversationSpeaker;
            Vector3 lookPoint = buddy != null && !buddy.IsDead && buddy.TryGetHeadPosition(out Vector3 buddyHead)
                ? buddyHead
                : TryInterest(eyePos, forward, out Vector3 interest)
                ? interest
                : IdlePoint(eyePos, forward);

            Aim(leftEye, _leftRest, lookPoint);
            Aim(rightEye, _rightRest, lookPoint);
        }

        ApplyScale(eyePos);
    }

    Transform _captureLeft;
    Transform _captureRight;
    Vector3 _captureLeftScale = Vector3.one;
    Vector3 _captureRightScale = Vector3.one;

    /// <summary>One eye-scale pose for the visible capture face. Look-at stays off.</summary>
    public void PoseCaptureScale(Transform left, Transform right)
    {
        if (left != _captureLeft || right != _captureRight)
        {
            _captureLeft = left;
            _captureRight = right;
            _captureLeftScale = left != null ? left.localScale : Vector3.one;
            _captureRightScale = right != null ? right.localScale : Vector3.one;
        }

        Transform previousLeft = leftEye;
        Transform previousRight = rightEye;
        Vector3 previousLeftScale = _leftScale;
        Vector3 previousRightScale = _rightScale;
        leftEye = left;
        rightEye = right;
        _leftScale = _captureLeftScale;
        _rightScale = _captureRightScale;
        Vector3 eyePos = left != null ? left.position : transform.position;
        ApplyScale(eyePos, true);
        leftEye = previousLeft;
        rightEye = previousRight;
        _leftScale = previousLeftScale;
        _rightScale = previousRightScale;
    }

    bool TryInterest(Vector3 eyePos, Vector3 forward, out Vector3 lookPoint)
    {
        lookPoint = default;
        RefreshPlayers();

        float radiusSq = interestRadius * interestRadius;
        float cosCone = Mathf.Cos(headCone * Mathf.Deg2Rad);
        PhotonPlayer best = null;
        Vector3 bestPoint = default;
        float bestScore = 0f;
        bool heldValid = false;
        Vector3 heldPoint = default;

        for (int i = 0; i < _players.Length; i++)
        {
            PhotonPlayer other = _players[i];
            if (other == null || other == _player)
                continue;
            if (!other.TryGetGaze(out Vector3 theirEyes, out _))
                continue;

            Vector3 to = theirEyes - eyePos;
            float distSq = to.sqrMagnitude;
            if (distSq < 1e-4f || distSq > radiusSq)
                continue;

            float dist = Mathf.Sqrt(distSq);
            if (Vector3.Dot(forward, to / dist) < cosCone)
                continue;

            var mouth = other.GetComponent<PhotonVoiceMouth>();
            float loud = mouth != null ? mouth.Loudness : 0f;
            if (loud < talkLoudness)
                continue;

            if (other == _held)
            {
                heldValid = true;
                heldPoint = theirEyes;
            }

            float score = loud / dist;
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
                bestPoint = theirEyes;
            }
        }

        if (_held != null && heldValid && Time.time < _holdUntil)
        {
            lookPoint = heldPoint;
            return true;
        }

        if (best == null)
        {
            _held = null;
            return false;
        }

        if (best != _held)
        {
            _held = best;
            _holdUntil = Time.time + holdTime;
        }

        lookPoint = bestPoint;
        return true;
    }

    Vector3 IdlePoint(Vector3 eyePos, Vector3 forward)
    {
        int actor = _view != null ? _view.OwnerActorNr : 0;
        double shifted = PhotonNetwork.Time + actor * 0.37;
        float interval = glanceInterval > 0.05f ? glanceInterval : 0.05f;
        int slot = (int)Math.Floor(shifted / interval);
        if (slot != _idleSlot)
        {
            _idleSlot = slot;
            var rng = new System.Random(unchecked(actor * 10007 + slot));
            _idleYaw = Mathf.Lerp(-maxYaw, maxYaw, (float)rng.NextDouble());
            _idlePitch = Mathf.Lerp(-maxPitch, maxPitch, (float)rng.NextDouble());
        }

        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            up = Vector3.right;
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.forward;

        Vector3 dir = Quaternion.LookRotation(forward, up) * Quaternion.Euler(_idlePitch, _idleYaw, 0f) * Vector3.forward;
        return eyePos + dir * 2f;
    }

    void Aim(Transform eye, Quaternion rest, Vector3 lookPoint)
    {
        if (eye == null)
            return;

        Vector3 dir = lookPoint - eye.position;
        if (dir.sqrMagnitude < 1e-8f)
            return;

        Transform parent = eye.parent;
        Vector3 up = parent != null ? parent.up : Vector3.up;
        Vector3 dirN = dir.normalized;
        if (Mathf.Abs(Vector3.Dot(up, dirN)) > 0.99f)
            up = parent != null ? parent.right : Vector3.right;

        Quaternion worldLook = Quaternion.LookRotation(dirN, up) * Quaternion.Euler(lookAxisEuler);
        Quaternion parentRot = parent != null ? parent.rotation : Quaternion.identity;
        Quaternion delta = Quaternion.Inverse(rest) * (Quaternion.Inverse(parentRot) * worldLook);
        Vector3 euler = delta.eulerAngles;
        bool companion = _player != null && _player.IsCompanion;
        float yawLimit = companion ? 12f : maxYaw;
        float pitchLimit = companion ? 8f : maxPitch;
        Quaternion clamped = rest * Quaternion.Euler(
            ClampDegrees(euler.x, -pitchLimit, pitchLimit),
            ClampDegrees(euler.y, -yawLimit, yawLimit),
            0f);

        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        eye.localRotation = Quaternion.Slerp(eye.localRotation, clamped, t);
    }

    static float ClampDegrees(float degrees, float min, float max)
    {
        return Mathf.Clamp(Mathf.DeltaAngle(0f, degrees), min, max);
    }

    void ApplyScale(Vector3 eyePos, bool useSampledMic = false)
    {
        float ownLoudness = 0f;
        if (_mouth != null)
            ownLoudness = useSampledMic ? _mouth.SampledLoudness : _mouth.Loudness;
        float own = LoudAmount(ownLoudness);
        float heard = HeardAmount(eyePos);
        float k = 1f - Mathf.Exp(-scaleSmooth * Time.deltaTime);
        _ownAmount = Mathf.Lerp(_ownAmount, own, k);
        _heardAmount = Mathf.Lerp(_heardAmount, heard, k);

        float mul = 1f
            + _ownAmount * Mathf.Max(0f, voiceScale - 1f)
            + _heardAmount * Mathf.Max(0f, heardScale - 1f)
            + SurpriseAmount(eyePos) * Mathf.Max(0f, surpriseScale - 1f);
        mul = Mathf.Min(mul, Mathf.Max(1f, scaleCap));

        if (leftEye != null)
            leftEye.localScale = _leftScale * mul;
        if (rightEye != null)
            rightEye.localScale = _rightScale * mul;
    }

    float LoudAmount(float loudness)
    {
        float span = voiceFullLoudness - voiceScaleThreshold;
        if (span < 1e-4f)
            span = 1e-4f;
        return Mathf.Clamp01((loudness - voiceScaleThreshold) / span);
    }

    float HeardAmount(Vector3 eyePos)
    {
        RefreshPlayers();
        float radiusSq = interestRadius * interestRadius;
        float best = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            PhotonPlayer other = _players[i];
            if (other == null || other == _player)
                continue;
            if (!other.TryGetGaze(out Vector3 theirEyes, out _))
                continue;

            float distSq = (theirEyes - eyePos).sqrMagnitude;
            if (distSq > radiusSq)
                continue;

            var mouth = other.GetComponent<PhotonVoiceMouth>();
            if (mouth == null)
                continue;

            float dist = Mathf.Sqrt(distSq);
            float falloff = 1f - dist / interestRadius;
            float amount = LoudAmount(mouth.Loudness) * falloff;
            if (amount > best)
                best = amount;
        }

        return best;
    }

    float SurpriseAmount(Vector3 eyePos)
    {
        float duration = surpriseDuration > 0.05f ? surpriseDuration : 0.05f;
        float radius = surpriseRadius > 0.01f ? surpriseRadius : 0.01f;
        float best = 0f;
        float now = Time.time;

        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            Pulse pulse = _pulses[i];
            float age = now - pulse.Time;
            if (age > duration)
            {
                _pulses.RemoveAt(i);
                continue;
            }

            float dist = Vector3.Distance(eyePos, pulse.Position);
            if (dist > radius)
                continue;

            float env = PulseEnvelope(age, duration);
            float amount = env * (1f - dist / radius) * pulse.Strength;
            if (amount > best)
                best = amount;
        }

        return best;
    }

    static float PulseEnvelope(float age, float duration)
    {
        const float attack = 0.08f;
        if (age <= attack)
            return Mathf.Clamp01(age / attack);

        float down = 1f - Mathf.Clamp01((age - attack) / (duration - attack));
        return down * down;
    }

    static void RefreshPlayers()
    {
        if (Time.unscaledTime < _nextPlayerScan)
            return;

        _nextPlayerScan = Time.unscaledTime + 0.25f;
        _players = FindObjectsByType<PhotonPlayer>(FindObjectsSortMode.None);
    }
}
