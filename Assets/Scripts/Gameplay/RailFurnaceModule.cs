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
    [Tooltip("Idle flames live in the Furnace prefab so they can be tuned in the editor; emission is scaled at runtime.")]
    public GameObject idleFire;
    public GameObject coalBurstPrefab;
    public float burstScale = .25f;
    [Range(0, 1)] public float outOfFuelEmission = .12f;
    [Tooltip("Emission change per second when the fire grows or shrinks (1 = full change in one second).")]
    public float fadeSpeed = .5f;
    [Tooltip("Fuel fraction (of capacity) at and above which the fire burns at full emission. Below it the fire ramps down linearly to Out Of Fuel Emission at 0 fuel.")]
    [Range(0, 1)] public float fullBurnFuelFraction = .2f;
    [Header("Debug")]
    [Tooltip("Force the idle fire level (0-1 of full emission) regardless of fuel or boarding.")]
    public bool debugOverride;
    [Range(0, 1)] public float debugLevel = 1;
    ParticleSystem[] idleSystems;
    float[] idleBaseRates;
    float idleLevel = 1;
    [Header("Fuel popup (same look as the gold crate's +N)")]
    public TMP_FontAsset fuelTextFont;
    public Material fuelTextMaterial;
    public TextEffectInstance fuelTextEffect;
    RailTrain train;
    Game.Audio.AudioHandle fireLoop;
    float nextFireTry;

    /// <summary>Floats a "+N" fuel number up out of the firebox opening.</summary>
    public void ShowFuelGain(float fuel)
    {
        string text = Mathf.Abs(fuel - Mathf.Round(fuel)) < .05f ? Mathf.Round(fuel).ToString("0") : fuel.ToString("0.#");
        Vector3 at = insertion != null ? insertion.position : transform.position;
        GoldRewardText.Show("+" + text, at, fuelTextFont, fuelTextMaterial, fuelTextEffect);
        PlayCoalBurst(at);
        RailAudioDirector.CoalDeposited(at);
    }

    /// <summary>One-shot flame burst; runs on every client from the committed-deposit popup path.</summary>
    void PlayCoalBurst(Vector3 at)
    {
        if (coalBurstPrefab == null) return;
        var rot = transform.rotation;
        var burst = Instantiate(coalBurstPrefab, at, rot);
        burst.transform.localScale = Vector3.one * burstScale;
        foreach (var ps in burst.GetComponentsInChildren<ParticleSystem>()) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; ps.Play(); }
        Destroy(burst, 4f);
    }

    void BuildIdleFire()
    {
        if (idleFire == null || idleSystems != null) return;
        idleSystems = idleFire.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in idleSystems) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
        idleBaseRates = new float[idleSystems.Length];
        for (int i = 0; i < idleSystems.Length; i++) idleBaseRates[i] = idleSystems[i].emission.rateOverTimeMultiplier;
        if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void UpdateIdleFire(bool aboard)
    {
        float fuel = PhotonSession.CurrentFuel;
        float ramp = fullBurnFuelFraction <= 0 ? 1 : Mathf.Clamp01(fuel / (RailResourceBalance.Capacity * fullBurnFuelFraction));
        float lit = Mathf.Lerp(outOfFuelEmission, 1, ramp);
        float target = debugOverride ? debugLevel : !aboard ? 0 : lit;
        idleLevel = Mathf.MoveTowards(idleLevel, target, Time.deltaTime * fadeSpeed);
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
        UpdateFireLoop(aboard && PhotonSession.CurrentFuel > 0);
    }
    // The fire bed follows the same condition as the flames and fades in and out with them.
    void UpdateFireLoop(bool burning)
    {
        if (!burning) { if (fireLoop.IsPlaying) fireLoop.Stop(.6f); return; }
        if (fireLoop.IsPlaying || Time.unscaledTime < nextFireTry) return;
        nextFireTry = Time.unscaledTime + .5f;
        var bank = RailAudio.Bank;
        var manager = Game.Audio.AudioManager.instance;
        if (bank != null && bank.furnaceFireLoop != null && manager != null)
            fireLoop = manager.PlayAmbience(bank.furnaceFireLoop, insertion != null ? insertion : transform, this, true, .6f);
    }
    public static void SetEmission(ParticleSystem effect, bool enabled)
    {
        if (effect == null) return;
        var emission = effect.emission; emission.enabled = enabled;
        if (enabled && !effect.isPlaying) effect.Play();
    }
    void OnDisable()
    {
        fireLoop.Stop();
        if (idleSystems != null) foreach (var ps in idleSystems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        idleSystems = null;
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
