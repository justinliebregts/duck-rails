using UnityEngine;

/// <summary>
/// Turns a light fully on and off at a steady interval. The intensity set on the Light is the on brightness.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightBlink : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float interval = 0.5f;

    Light _light;
    float _baseIntensity;
    float _timer;
    bool _on = true;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
    }

    void OnDisable()
    {
        if (_light != null)
            _light.intensity = _baseIntensity;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < interval)
            return;

        _timer -= interval;
        _on = !_on;
        _light.intensity = _on ? _baseIntensity : 0f;
    }
}
