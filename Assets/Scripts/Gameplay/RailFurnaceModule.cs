using TMPro;
using UnityEngine;
using EasyTextEffects.Effects;

/// <summary>The firebox, coal acceptance volume and effects travel as one authoring module.</summary>
public sealed class RailFurnaceModule : MonoBehaviour
{
    public Transform insertion;
    public Vector3 acceptanceHalfExtents = new Vector3(.50f, .43f, .50f);
    public ParticleSystem fire, smoke;
    [Header("Polygon FX (idle flames + per-coal burst)")]
    public GameObject idleFirePrefab, coalBurstPrefab;
    public float fxScale = .25f;
    [Range(0, 1)] public float outOfFuelEmission = .12f;
    GameObject idleFire;
    ParticleSystem[] idleSystems;
    float[] idleBaseRates;
    float idleLevel = 1;
    [Header("Fuel popup (same look as the gold crate's +N)")]
    public TMP_FontAsset fuelTextFont;
    public Material fuelTextMaterial;
    public TextEffectInstance fuelTextEffect;
    RailTrain train;

    /// <summary>Floats a "+N" fuel number up out of the firebox opening.</summary>
    public void ShowFuelGain(float fuel)
    {
        string text = Mathf.Abs(fuel - Mathf.Round(fuel)) < .05f ? Mathf.Round(fuel).ToString("0") : fuel.ToString("0.#");
        Vector3 at = insertion != null ? insertion.position : transform.position;
        GoldRewardText.Show("+" + text, at, fuelTextFont, fuelTextMaterial, fuelTextEffect);
        PlayCoalBurst(at);
    }

    /// <summary>One-shot flame burst; runs on every client from the committed-deposit popup path.</summary>
    void PlayCoalBurst(Vector3 at)
    {
        if (coalBurstPrefab == null) return;
        var rot = transform.rotation;
        var burst = Instantiate(coalBurstPrefab, at, rot);
        burst.transform.localScale = Vector3.one * fxScale;
        foreach (var ps in burst.GetComponentsInChildren<ParticleSystem>()) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; ps.Play(); }
        Destroy(burst, 4f);
    }

    void BuildIdleFire()
    {
        if (idleFirePrefab == null || idleFire != null) return;
        var anchor = fire != null ? fire.transform : (insertion != null ? insertion : transform);
        idleFire = Instantiate(idleFirePrefab, anchor.position, transform.rotation, transform);
        idleFire.transform.localScale = Vector3.one * fxScale;
        idleSystems = idleFire.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in idleSystems) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
        idleBaseRates = new float[idleSystems.Length];
        for (int i = 0; i < idleSystems.Length; i++) idleBaseRates[i] = idleSystems[i].emission.rateOverTimeMultiplier;
        if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void UpdateIdleFire(bool aboard)
    {
        float target = !aboard ? 0 : PhotonSession.CurrentFuel > 0 ? 1 : outOfFuelEmission;
        idleLevel = Mathf.MoveTowards(idleLevel, target, Time.deltaTime * .5f);
        for (int i = 0; i < idleSystems.Length; i++)
        {
            var emission = idleSystems[i].emission;
            emission.rateOverTimeMultiplier = idleBaseRates[i] * idleLevel;
            if (idleLevel > 0 && !idleSystems[i].isPlaying) idleSystems[i].Play();
        }
    }

    public bool Contains(Vector3 worldPoint)
    {
        if (insertion == null) return false;
        Vector3 p = insertion.InverseTransformPoint(worldPoint);
        return Mathf.Abs(p.x) <= acceptanceHalfExtents.x &&
            Mathf.Abs(p.y) <= acceptanceHalfExtents.y && Mathf.Abs(p.z) <= acceptanceHalfExtents.z;
    }

    // Network samples are rotation-relative world metres, matching RailExpedition's existing protocol.
    public bool AcceptsSample(Vector3 offset) => insertion != null &&
        Contains(insertion.position + insertion.rotation * offset);

    void OnEnable() { train = GetComponentInParent<RailTrain>(); }
    void Update()
    {
        bool aboard = PhotonSession.IsRailsRoom && PhotonSession.OnTrain;
        BuildIdleFire();
        if (idleFire != null) UpdateIdleFire(aboard);
        else SetEmission(fire, aboard && PhotonSession.CurrentFuel > 0);
        SetEmission(smoke, aboard && train != null && Mathf.Abs(train.Speed) > .05f);
    }
    public static void SetEmission(ParticleSystem effect, bool enabled)
    {
        if (effect == null) return;
        var emission = effect.emission; emission.enabled = enabled;
        if (enabled && !effect.isPlaying) effect.Play();
    }
    void OnDisable()
    {
        if (idleFire != null) { Destroy(idleFire); idleFire = null; }
        if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void OnDrawGizmosSelected()
    {
        if (insertion == null) return;
        Gizmos.matrix = insertion.localToWorldMatrix; Gizmos.color = new Color(1, .5f, 0, .8f);
        Gizmos.DrawWireCube(Vector3.zero, acceptanceHalfExtents * 2);
    }
}
