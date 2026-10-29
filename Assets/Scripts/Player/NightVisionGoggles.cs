using UnityEngine;

[RequireComponent(typeof(HeadEquipment))]
public sealed class NightVisionGoggles : MonoBehaviour
{
    public MeshRenderer mesh;
    [SerializeField] Light spotLight;

    const float EquippedFogDensity = 0.025f;
    const float NormalFogDensity = 0.12f;

    static NightVisionGoggles localEquipped;

    HeadEquipment equipment;
    ItemSounds sounds;
    PlayerHUD activeHUD;
    bool effectActive;
    bool subscribed;

    /// <summary>
    /// Draws the equipped goggles for the camera about to render, then hides them again.
    /// Eyes and the first-person tablet leave this false, so the mesh stays off there.
    /// </summary>
    public static void SetShownInExternalView(bool shown)
    {
        if (localEquipped == null || localEquipped.mesh == null || !localEquipped.effectActive)
            return;

        if (localEquipped.mesh.enabled == shown)
            return;

        localEquipped.mesh.enabled = shown;
    }

    void Awake()
    {
        equipment = GetComponent<HeadEquipment>();
        sounds = GetComponent<ItemSounds>();
        if (spotLight != null && spotLight.enabled)
            spotLight.enabled = false;
    }

    void OnEnable()
    {
        Subscribe();
        if (equipment != null && equipment.IsLocallyEquipped)
            Activate();
    }

    void LateUpdate()
    {
        bool shouldBeActive = equipment != null && equipment.IsLocallyEquipped;
        if (shouldBeActive != effectActive)
        {
            if (shouldBeActive)
                Activate();
            else
                Deactivate();
        }

        if (effectActive && localEquipped == this)
            ApplySettings();
    }

    void Subscribe()
    {
        if (equipment == null || subscribed)
            return;

        equipment.LocallyEquipped += HandleLocallyEquipped;
        equipment.LocallyUnequipped += HandleLocallyUnequipped;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (equipment == null || !subscribed)
            return;

        equipment.LocallyEquipped -= HandleLocallyEquipped;
        equipment.LocallyUnequipped -= HandleLocallyUnequipped;
        subscribed = false;
    }

    void HandleLocallyEquipped()
    {
        Activate();
        PlaySound(ItemSound.TurnOn);
    }

    void HandleLocallyUnequipped()
    {
        Deactivate();
        PlaySound(ItemSound.TurnOff);
    }

    void PlaySound(ItemSound sound)
    {
        if (sounds != null)
            sounds.Play(sound);
    }

    void Activate()
    {
        // Remote equipment state must never enable this local visibility aid.
        if (equipment == null || !equipment.IsLocallyEquipped)
            return;

        effectActive = true;
        if (spotLight != null && !spotLight.enabled)
            spotLight.enabled = true;
        ApplySettings();
        if (RenderSettings.fogDensity != EquippedFogDensity)
            RenderSettings.fogDensity = EquippedFogDensity;
        if (mesh != null && mesh.enabled)
            mesh.enabled = false;
        localEquipped = this;
    }

    void Deactivate()
    {
        if (spotLight != null && spotLight.enabled)
            spotLight.enabled = false;
        if (effectActive && localEquipped == this)
        {
            if (activeHUD != null)
                activeHUD.SetNightVisionEffect(false);
            if (RenderSettings.fogDensity != NormalFogDensity)
                RenderSettings.fogDensity = NormalFogDensity;
        }
        activeHUD = null;
        effectActive = false;
        if (localEquipped == this)
            localEquipped = null;
        if (mesh != null && !mesh.enabled)
            mesh.enabled = true;
    }

    public void ApplySettings()
    {
        if (!effectActive)
            return;

        PlayerHUD hud = GR.Player.instance != null ? GR.Player.instance.playerHUD : null;
        if (activeHUD != hud)
        {
            if (activeHUD != null)
                activeHUD.SetNightVisionEffect(false);
            activeHUD = hud;
        }
        if (activeHUD != null)
            activeHUD.SetNightVisionEffect(true);
    }

    void OnDisable()
    {
        Unsubscribe();
        Deactivate();
    }

    void OnDestroy()
    {
        Deactivate();
    }
}
