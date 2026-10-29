using Game.Audio;
using UnityEngine;

/// <summary>
/// Plays other players' hand slaps entirely on this client: watches their rendered hands
/// and plays a surface sound when a hand comes into contact with the world. No network traffic.
/// </summary>
[RequireComponent(typeof(PhotonPlayer))]
[DefaultExecutionOrder(100)] // after PhotonPlayer.LateUpdate has applied the interpolated pose
public sealed class RemoteHandHitAudio : MonoBehaviour
{
    [SerializeField] SurfaceSoundSet sounds;
    [Tooltip("Layers a hand can hit. Exclude player/avatar layers.")]
    [SerializeField] LayerMask surfaceMask = (1 << 0) | (1 << 6);
    [Tooltip("Distance from the hand bone that counts as touching.")]
    [SerializeField, Min(0.01f)] float contactRadius = 0.1f;
    [Tooltip("A hand must be off the surface this long before another hit can play.")]
    [SerializeField, Min(0f)] float rearmTime = 0.1f;

    [Header("Volume from hand speed")]
    [SerializeField] float minSpeed = 0.3f;
    [SerializeField] float fullVolumeSpeed = 4f;
    [SerializeField, Range(0f, 1f)] float minVolume = 0.2f;

    struct HandState
    {
        public bool initialized, touching;
        public Vector3 lastPosition;
        public float speed, offTime;
    }

    readonly Collider[] overlaps = new Collider[8];
    PhotonPlayer player;
    HandState left, right;

    void Awake() => player = GetComponent<PhotonPlayer>();
    void OnDisable() => left = right = default;

    void LateUpdate()
    {
        // Companion plants already drive the shared FootstepEmitter on every peer.
        if (player.IsCompanion || player.photonView.IsMine || player.IsDead || sounds == null) return;
        Check(player.LeftHand, ref left);
        Check(player.RightHand, ref right);
    }

    void Check(Transform hand, ref HandState state)
    {
        if (hand == null) return;
        Vector3 position = hand.position;
        float dt = Time.deltaTime;
        if (!state.initialized)
        {
            state = new HandState { initialized = true, lastPosition = position, offTime = rearmTime };
            return;
        }
        // Smoothed so one jittery interpolation frame doesn't decide the volume.
        if (dt > 0f) state.speed = Mathf.Lerp(state.speed, (position - state.lastPosition).magnitude / dt, 0.5f);
        state.lastPosition = position;

        int count = Physics.OverlapSphereNonAlloc(position, contactRadius, overlaps, surfaceMask, QueryTriggerInteraction.Ignore);
        if (count == 0)
        {
            state.touching = false;
            state.offTime += dt;
            return;
        }
        bool wasTouching = state.touching;
        state.touching = true;
        if (wasTouching || state.offTime < rearmTime) { state.offTime = 0f; return; }
        state.offTime = 0f;

        SurfaceType surface = null;
        for (int i = 0; i < count && surface == null; i++) surface = AudioSurface.TypeOf(overlaps[i]);
        var sound = sounds.Resolve(surface);
        if (sound == null || AudioManager.instance == null) return;
        float t = Mathf.InverseLerp(minSpeed, fullVolumeSpeed, state.speed);
        AudioManager.instance.Play(sound, position, Mathf.Lerp(minVolume, 1f, t));
    }
}
