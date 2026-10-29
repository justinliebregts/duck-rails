using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    public Image deathEffect;
    public RawImage nightVisionEffect;
    bool nightVisionActive;

    public void EnableDeathEffect()
    {
        deathEffect.enabled = true;
        RefreshNightVisionEffect();
    }

    public void DisableDeathEffect()
    {
        deathEffect.enabled = false;
        RefreshNightVisionEffect();
    }

    public void SetNightVisionEffect(bool enabled)
    {
        nightVisionActive = enabled;
        RefreshNightVisionEffect();
    }

    void RefreshNightVisionEffect()
    {
        if (nightVisionEffect == null)
            return;

        bool shouldBeEnabled = nightVisionActive && (deathEffect == null || !deathEffect.enabled);
        if (nightVisionEffect.enabled != shouldBeEnabled)
            nightVisionEffect.enabled = shouldBeEnabled;
    }
}
