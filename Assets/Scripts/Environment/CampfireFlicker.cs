using UnityEngine;

/// <summary>
/// Wobbles a light like a campfire. The intensity set on the Light is the baseline.
/// </summary>
[RequireComponent(typeof(Light))]
public class CampfireFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float amount = 0.4f;
    [SerializeField, Min(0.1f)] float speed = 6f;

    Light _light;
    float _baseIntensity;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.Range(0f, 100f);
    }

    void OnDisable()
    {
        if (_light != null)
            _light.intensity = _baseIntensity;
    }

    void Update()
    {
        float slow = Mathf.PerlinNoise(_seed, Time.time * speed);
        float fast = Mathf.PerlinNoise(_seed + 40f, Time.time * speed * 3.1f);
        float flare = fast > 0.72f ? (fast - 0.72f) * 2f : 0f;
        float dip = slow < 0.22f ? (0.22f - slow) * 1.5f : 0f;
        float scale = 1f - amount * 0.45f + amount * slow + flare - dip;
        _light.intensity = _baseIntensity * Mathf.Max(0.05f, scale);
    }
}
