using Game.Audio;
using UnityEngine;

/// <summary>Visual-only slab driven by the shared public departure countdown.</summary>
public sealed class RailPressurePlate : MonoBehaviour
{
    [Tooltip("Moving visual slab; collision stays on a stationary sibling.")]
    public Transform plate;
    public Collider contactSurface;
    public SoundEvent pressSound;
    public SoundEvent releaseSound;
    [Tooltip("How far the slab sinks from its authored resting position, in metres.")]
    public float travel = .05f;
    public float speed = .2f;

    Vector3 rest;
    bool initialized;
    public bool Pressed { get; private set; }

    void Awake() { if (plate != null) { rest = plate.localPosition; initialized = true; } }

    void Update()
    {
        if (plate == null) return;
        // Deadline is cleared when the countdown stops. Do not infer occupancy or
        // release early when the local clock reaches zero before shared state updates.
        bool counting = PhotonSession.IsRailsRoom && !PhotonSession.IsPrivate &&
            PhotonSession.Phase == RailPhase.Compound &&
            PhotonSession.Get(RailSessionRules.Deadline, 0d) > 0d;
        ApplyCountdown(counting, Time.deltaTime);
    }

    void ApplyCountdown(bool counting, float deltaTime)
    {
        if (counting != Pressed)
        {
            Pressed = counting;
            var position = contactSurface != null ? contactSurface.bounds.center : plate.position;
            RailAudio.Play(Pressed ? pressSound : releaseSound, position);
        }
        var target = rest + Vector3.down * (Pressed ? travel : 0);
        plate.localPosition = Vector3.MoveTowards(plate.localPosition, target, speed * deltaTime);
    }

    void OnDisable()
    {
        if (initialized && plate != null) plate.localPosition = rest;
        Pressed = false;
    }
}
