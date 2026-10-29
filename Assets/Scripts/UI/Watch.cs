using System;
using Game.Audio;
using UnityEngine;
using TMPro;

// Wrist readout of the coin balance. The balance itself lives on DuckzWallet.
// While deposit coins are flying in, the readout holds back the new balance and counts up as each one lands.
public class Watch : MonoBehaviour
{
    const float PunchScale = 0.25f;
    const float PunchDecay = 8f;

    public TextMeshPro moneyText;
    [Tooltip("Plays when a landing coin moves the readout to an even number. Only you hear it.")]
    [SerializeField] SoundEvent coinCountUpSound;
    [SerializeField] TextMeshPro muteText;

    public static Watch Local { get; private set; }

    int displayed;
    int coinsInFlight;
    float punch;
    Vector3 textScale = Vector3.one;
    Vector2 moneyRestPos;
    Vector2 moneyRestSize;

    public Transform CoinTarget => moneyText != null ? moneyText.transform : transform;

    void Awake()
    {
        if (moneyText != null)
        {
            textScale = moneyText.transform.localScale;
            moneyRestPos = moneyText.rectTransform.anchoredPosition;
            moneyRestSize = moneyText.rectTransform.sizeDelta;
        }
    }

    /// <summary>Local punishment mute. Null clears the wrist readout.</summary>
    public void SetPunishmentCountdown(TimeSpan? remaining)
    {
        bool show = remaining.HasValue && remaining.Value > TimeSpan.Zero;
        SetStatus(show ? "Muted\n" + FormatCountdown(remaining.Value) : null);
    }

    /// <summary>Shows a line under the coin balance. Null or empty hides it.</summary>
    public void SetStatus(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (muteText != null)
        {
            if (muteText.gameObject.activeSelf != show)
                muteText.gameObject.SetActive(show);
            if (show)
                muteText.text = text;
        }

        if (moneyText == null)
            return;

        moneyText.rectTransform.anchoredPosition = show ? moneyRestPos + new Vector2(0f, 0.025f) : moneyRestPos;
        moneyText.rectTransform.sizeDelta = show ? new Vector2(moneyRestSize.x, 0.06f) : moneyRestSize;
    }

    static string FormatCountdown(TimeSpan remaining)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int secs = seconds % 60;
        if (hours > 0)
            return hours + ":" + minutes.ToString("00") + ":" + secs.ToString("00");
        return minutes + ":" + secs.ToString("00");
    }

    void OnEnable()
    {
        Local = this;
        coinsInFlight = 0;
        DuckzWallet.Ensure().OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (Local == this)
            Local = null;
        if (DuckzWallet.Instance != null)
            DuckzWallet.Instance.OnChanged -= Refresh;
        if (moneyText != null)
            moneyText.transform.localScale = textScale;
    }

    /// <summary>Coins are about to fly in; the readout waits for them instead of jumping to the new balance.</summary>
    public void AddCoinsInFlight(int count)
    {
        coinsInFlight += Mathf.Max(0, count);
    }

    /// <summary>One flying coin arrived. Moves the readout its share of the way to the balance.</summary>
    public void CoinLanded()
    {
        if (coinsInFlight <= 0)
            return;
        int before = displayed;
        if (DuckzWallet.Instance != null)
            displayed += Mathf.RoundToInt((DuckzWallet.Instance.Coins - displayed) / (float)coinsInFlight);
        coinsInFlight--;
        punch = 1f;
        Show();

        if (displayed != before && displayed % 2 == 0 && coinCountUpSound != null && AudioManager.instance != null)
            AudioManager.instance.Play(coinCountUpSound, CoinTarget.position);
    }

    void Refresh()
    {
        if (coinsInFlight == 0 && DuckzWallet.Instance != null)
            displayed = DuckzWallet.Instance.Coins;
        Show();
    }

    void Show()
    {
        if (moneyText != null)
            moneyText.text = "$" + displayed;
    }

    void LateUpdate()
    {
        if (punch <= 0f || moneyText == null)
            return;
        punch = Mathf.Max(0f, punch - Time.deltaTime * PunchDecay);
        moneyText.transform.localScale = textScale * (1f + PunchScale * punch);
    }
}
