using System.Collections.Generic;
using EasyTextEffects;
using EasyTextEffects.Effects;
using TMPro;
using UnityEngine;

// Floating "+N" popup: Raider Legends' gem reward text with a gold gradient.
// Rises and drifts to one side; the per-letter color and scale pulse come from an Easy Text Effects asset.
public class GoldRewardText : MonoBehaviour
{
    // Raider Legends draws this on a world-space canvas scaled 0.005, so offsets and font size are in canvas units.
    const float CanvasScale = 0.005f;
    const float FontSize = 36f;
    // TextMeshPro (3D) renders a font size 10x larger than the UGUI text it was tuned on.
    const float TmpWorldScale = 10f * CanvasScale;
    const float Duration = 1.6666666f;
    const float DriftX = 20.86919f;
    // Fraction of Raider Legends' rise height (about 0.25 m at 1).
    const float RiseScale = 0.5f;
    // Beyond this distance the text grows so it stays readable.
    const float ReferenceDistance = 10f;

    static readonly AnimationCurve RiseY = new AnimationCurve(
        new Keyframe(0f, 0f, 64.57412f, 64.57412f),
        new Keyframe(Duration, 50.469193f, 31.616583f, 31.616583f));

    static readonly Stack<GoldRewardText> pool = new Stack<GoldRewardText>();

    TextMeshPro label;
    TextEffect textEffect;
    Vector3 origin;
    float startTime;
    float drift;
    float worldPerUnit;

    public static void Show(string text, Vector3 position, TMP_FontAsset font, Material material, TextEffectInstance effect)
    {
        GoldRewardText popup = Get();
        popup.Play(text, position, font, material, effect);
    }

    static GoldRewardText Get()
    {
        while (pool.Count > 0)
        {
            GoldRewardText pooled = pool.Pop();
            if (pooled != null)
                return pooled;
        }

        var go = new GameObject("GoldRewardText");
        GoldRewardText popup = go.AddComponent<GoldRewardText>();
        popup.label = go.AddComponent<TextMeshPro>();
        popup.label.fontSize = FontSize;
        popup.label.alignment = TextAlignmentOptions.Center;
        popup.label.rectTransform.sizeDelta = new Vector2(30f, 5f);
        popup.label.textWrappingMode = TextWrappingModes.NoWrap;
        popup.label.overflowMode = TextOverflowModes.Overflow;
        popup.label.raycastTarget = false;
        popup.textEffect = go.AddComponent<TextEffect>();
        popup.textEffect.text = popup.label;
        popup.textEffect.globalEffects = new List<GlobalTextEffectEntry> { new GlobalTextEffectEntry() };
        return popup;
    }

    void Play(string text, Vector3 position, TMP_FontAsset font, Material material, TextEffectInstance effect)
    {
        if (font != null)
            label.font = font;
        if (material != null)
            label.fontSharedMaterial = material;
        label.text = text;
        textEffect.globalEffects[0].effect = effect;

        origin = position;
        startTime = Time.time;
        drift = Mathf.Lerp(-DriftX, DriftX, Random.value);

        float distanceScale = 1f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 fromCamera = position - cam.transform.position;
            distanceScale = Mathf.Clamp(fromCamera.magnitude / ReferenceDistance, 1f, 100f);
            if (fromCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(fromCamera, Vector3.up);
        }
        worldPerUnit = CanvasScale * distanceScale;
        transform.localScale = Vector3.one * (TmpWorldScale * distanceScale);
        transform.position = origin;

        gameObject.SetActive(true);
        // Restarts the effect timing and picks up the new text length.
        textEffect.Refresh();
    }

    void LateUpdate()
    {
        float t = Time.time - startTime;
        if (t >= Duration)
        {
            gameObject.SetActive(false);
            pool.Push(this);
            return;
        }

        Vector3 offset = transform.right * (drift * t / Duration) + transform.up * (RiseY.Evaluate(t) * RiseScale);
        transform.position = origin + offset * worldPerUnit;
    }
}
