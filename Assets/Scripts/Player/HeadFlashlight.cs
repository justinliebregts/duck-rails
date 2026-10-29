using UnityEngine;

/// <summary>
/// Enables a prefab-assigned light while its head equipment is worn.
/// Without HeadEquipment, the light keeps its serialized state for standalone use.
/// </summary>
public sealed class HeadFlashlight : MonoBehaviour
{
    [SerializeField] Light spotLight;

    HeadEquipment equipment;
    ItemSounds sounds;
    bool subscribed;
    LightEmissionState emission;
    LightRenderMode normalRenderMode;

    void Awake()
    {
        equipment = GetComponent<HeadEquipment>();
        sounds = GetComponent<ItemSounds>();
        if (spotLight != null)
            normalRenderMode = spotLight.renderMode;
        // Standalone player lights have no helmet mesh; do not alter the player's materials.
        if (equipment != null)
            emission = new LightEmissionState(this);
        if (equipment != null)
            SetLightEnabled(false);
    }

    void OnEnable()
    {
        Subscribe();
        if (equipment != null)
            SetLightEnabled(equipment.IsEquipped);
    }

    void OnDisable()
    {
        Unsubscribe();
        UpdateLightPriority();
    }

    void LateUpdate()
    {
        if (equipment != null)
            SetLightEnabled(equipment.IsEquipped);
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
        SetLightEnabled(true);
        PlaySound(ItemSound.TurnOn);
    }

    void HandleLocallyUnequipped()
    {
        SetLightEnabled(false);
        PlaySound(ItemSound.TurnOff);
    }

    void PlaySound(ItemSound sound)
    {
        if (sounds != null)
            sounds.Play(sound);
    }

    void SetLightEnabled(bool enabled)
    {
        if (spotLight != null && spotLight.enabled != enabled)
            spotLight.enabled = enabled;
        UpdateLightPriority();
        emission?.Apply(spotLight != null && spotLight.isActiveAndEnabled);
    }

    void UpdateLightPriority()
    {
        // Priority is a local rendering preference, never synchronized to other players.
        LightRenderMode desired = isActiveAndEnabled && equipment != null && equipment.IsLocallyEquipped
            ? LightRenderMode.ForcePixel : normalRenderMode;
        if (spotLight != null && spotLight.renderMode != desired)
            spotLight.renderMode = desired;
    }

    void OnDestroy()
    {
        emission?.Dispose();
    }

    void OnValidate()
    {
        if (spotLight == null)
            spotLight = GetComponent<Light>();
        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);
    }
}
