using TMPro;
using UnityEngine;

// Gold coin placed beside a store price. Visible only while that label is a whole number.
// Each coin winds up and spins on its own 10–20 second timer.
public class StorePriceCoin : MonoBehaviour
{
    [Tooltip("Price text. Defaults to the TMP_Text on the parent.")]
    [SerializeField] TMP_Text label;

    const float WindUpDuration = 0.4f;
    const float SpinDuration = 0.8f;
    const float SettleDuration = 0.6f;

    enum Phase { Waiting, WindUp, Spin, Settle }

    Renderer[] renderers;
    Quaternion restRotation;
    Phase phase;
    float waitRemaining;
    float phaseTime;
    int direction;
    float windUpAngle;
    float overshootAngle;
    bool shown;
    bool warned;

    void Awake()
    {
        if (label == null)
            label = GetComponentInParent<TMP_Text>();
        renderers = GetComponentsInChildren<Renderer>(true);
        restRotation = transform.localRotation;
        shown = false;
        SetRenderers(false);
    }

    void OnEnable()
    {
        waitRemaining = Random.Range(15f, 30f);
        phase = Phase.Waiting;
        phaseTime = 0f;
        transform.localRotation = restRotation;
    }

    void LateUpdate()
    {
        bool price = IsPrice();
        if (price != shown)
        {
            shown = price;
            SetRenderers(shown);
            if (!shown)
            {
                transform.localRotation = restRotation;
                phase = Phase.Waiting;
                phaseTime = 0f;
            }
        }

        if (!shown)
            return;

        switch (phase)
        {
            case Phase.Waiting:
                waitRemaining -= Time.deltaTime;
                if (waitRemaining <= 0f)
                    BeginSpin();
                break;
            case Phase.WindUp:
                if (Advance(WindUpDuration))
                    phase = Phase.Spin;
                else
                    Apply(-direction * windUpAngle * EaseOut(phaseTime / WindUpDuration));
                break;
            case Phase.Spin:
                if (Advance(SpinDuration))
                    phase = Phase.Settle;
                else
                {
                    float from = -direction * windUpAngle;
                    float to = direction * (360f + overshootAngle);
                    Apply(Mathf.Lerp(from, to, EaseIn(phaseTime / SpinDuration)));
                }
                break;
            case Phase.Settle:
                if (Advance(SettleDuration))
                    FinishSpin();
                else
                    Apply(Mathf.Lerp(direction * overshootAngle, 0f, EaseOut(phaseTime / SettleDuration)));
                break;
        }
    }

    void BeginSpin()
    {
        direction = Random.value < 0.5f ? -1 : 1;
        windUpAngle = Random.Range(12f, 28f);
        overshootAngle = Random.Range(8f, 18f);
        phase = Phase.WindUp;
        phaseTime = 0f;
    }

    void FinishSpin()
    {
        Apply(0f);
        phase = Phase.Waiting;
        phaseTime = 0f;
        waitRemaining = Random.Range(10f, 20f);
    }

    // Returns true on the frame a phase ends, and starts the clock for the next one.
    bool Advance(float duration)
    {
        phaseTime += Time.deltaTime;
        if (phaseTime < duration)
            return false;
        phaseTime = 0f;
        return true;
    }

    void Apply(float degrees)
    {
        transform.localRotation = restRotation * Quaternion.AngleAxis(degrees, Vector3.up);
    }

    bool IsPrice()
    {
        if (label == null)
        {
            if (!warned)
            {
                warned = true;
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write($"[Store] Price coin '{name}' has no text, so it stays hidden", this);
                }
            }
            return false;
        }

        return !string.IsNullOrWhiteSpace(label.text) && int.TryParse(label.text.Trim(), out _);
    }

    void SetRenderers(bool enabled)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u;
    }

    static float EaseIn(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }
}
