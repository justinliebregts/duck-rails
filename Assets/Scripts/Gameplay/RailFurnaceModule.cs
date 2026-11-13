using TMPro;
using UnityEngine;
using EasyTextEffects.Effects;

/// <summary>The firebox, coal acceptance volume and effects travel as one authoring module.</summary>
public sealed class RailFurnaceModule : MonoBehaviour
{
    public Transform insertion;
    public Vector3 acceptanceHalfExtents = new Vector3(.50f, .43f, .50f);
    public ParticleSystem fire, smoke;
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
        SetEmission(fire, aboard && PhotonSession.CurrentFuel > 0);
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
