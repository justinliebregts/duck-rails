using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    public Image deathEffect;
    public RawImage nightVisionEffect;
    bool nightVisionActive;

    void LateUpdate()
    {
        var player = GR.Player.instance;
        if (player == null || player.playerHUD != this || deathEffect == null) return;

        // Reuse Duckz's overlay without entering its death/respawn lifecycle.
        // Deriving this from current state also handles revival, rejoin and room return.
        bool show = player.IsDead || (PhotonSession.IsRailsRoom && RailExpedition.LocalDown);
        if (deathEffect.enabled == show) return;
        if (show) EnableDeathEffect();
        else DisableDeathEffect();
    }

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
