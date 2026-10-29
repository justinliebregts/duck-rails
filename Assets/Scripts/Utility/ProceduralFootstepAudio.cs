using Game.Animation;
using Game.Audio;
using UnityEngine;

/// <summary>Gaits already animate on each client; foot contacts need no extra network messages.</summary>
[RequireComponent(typeof(FootstepEmitter))]
public sealed class ProceduralFootstepAudio : MonoBehaviour
{
    FootstepEmitter emitter;
    SpiderGait spider;
    BipedGait biped;
    GorillaGait gorilla;
    void Awake()
    {
        emitter = GetComponent<FootstepEmitter>();
        spider = GetComponent<SpiderGait>();
        biped = GetComponent<BipedGait>();
        gorilla = GetComponent<GorillaGait>();
    }
    void OnEnable()
    {
        if (spider != null) spider.FootPlanted += OnPlant;
        if (biped != null) biped.FootPlanted += OnPlant;
        if (gorilla != null) gorilla.FootPlanted += OnPlant;
    }
    void OnDisable()
    {
        if (spider != null) spider.FootPlanted -= OnPlant;
        if (biped != null) biped.FootPlanted -= OnPlant;
        if (gorilla != null) gorilla.FootPlanted -= OnPlant;
    }
    void OnPlant(Vector3 point, Vector3 normal)
    {
        if (isActiveAndEnabled) emitter.PlayAt(point, normal);
    }
}
