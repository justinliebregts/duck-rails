using UnityEngine;

/// <summary>
/// Holds a light steady, then buzzes and blinks like a failing fluorescent tube.
/// The intensity set on the Light is full brightness.
/// </summary>
[RequireComponent(typeof(Light))]
public class FluorescentFlicker : MonoBehaviour
{
    [SerializeField] Vector2 quietTime = new Vector2(2f, 9f);
    [SerializeField] Vector2Int blinks = new Vector2Int(2, 6);
    [SerializeField, Range(0f, 1f)] float outageChance = 0.25f;
    [SerializeField, Min(0f)] float outageTime = 0.4f;

    Light _light;
    float _baseIntensity;
    float _timer;
    int _blinksLeft;
    bool _lit;
    bool _outage;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        BeginQuiet();
    }

    void OnDisable()
    {
        if (_light != null)
            _light.intensity = _baseIntensity;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;

        if (_blinksLeft > 0)
        {
            StepBlink();
            return;
        }

        if (_outage)
        {
            _outage = false;
            BeginQuiet();
            return;
        }

        BeginBurst();
    }

    void BeginQuiet()
    {
        _blinksLeft = 0;
        _outage = false;
        _lit = true;
        _light.intensity = _baseIntensity;
        _timer = Random.Range(quietTime.x, Mathf.Max(quietTime.x, quietTime.y));
    }

    void BeginBurst()
    {
        _blinksLeft = Random.Range(Mathf.Max(1, blinks.x), Mathf.Max(blinks.x, blinks.y) + 1);
        StepBlink();
    }

    void StepBlink()
    {
        _lit = !_lit;
        _blinksLeft--;
        if (_lit)
        {
            _light.intensity = _baseIntensity * Random.Range(0.7f, 1f);
            _timer = Random.Range(0.02f, 0.08f);
        }
        else
        {
            _light.intensity = _baseIntensity * Random.Range(0f, 0.06f);
            _timer = Random.Range(0.03f, 0.14f);
        }

        if (_blinksLeft > 0)
            return;

        if (Random.value < outageChance)
        {
            _outage = true;
            _light.intensity = 0f;
            _timer = outageTime * Random.Range(0.6f, 1.5f);
            return;
        }

        BeginQuiet();
    }
}
