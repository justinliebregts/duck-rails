using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>Read-only presentation of the same retained progress that poses the bridge.</summary>
public sealed class RailBridgeTerminal : MonoBehaviour
{
    public TMP_Text percentage;
    public Transform progressFill;
    public AudioSource mechanism;
    public float barWidth = .39f;
    [Min(0)] public float spinUpSeconds = .18f, spinDownSeconds = .22f;
    [Range(.1f, 1f)] public float stoppedPitch = .55f;
    [Range(0, 1)] public float runningVolume = .5f;
    float motorSpeed;
    public static float StepMotor(float speed, bool moving, float delta, float up, float down) =>
        Mathf.MoveTowards(speed, moving ? 1 : 0, delta / Mathf.Max(.001f, moving ? up : down));
    public static int Percent(float progress) => progress >= 1 ? 100 : Mathf.Clamp(Mathf.FloorToInt(progress * 100), 0, 99);

    public void Present(float progress, bool moving)
    {
        progress = Mathf.Clamp01(progress);
        if (percentage != null) percentage.SetText("{0}%", Percent(progress));
        if (progressFill != null)
        {
            var scale = progressFill.localScale; scale.x = barWidth * progress; progressFill.localScale = scale;
            var position = progressFill.localPosition; position.x = -.5f * barWidth * (1 - progress); progressFill.localPosition = position;
            progressFill.gameObject.SetActive(progress > 0);
        }
        if (mechanism == null || !Application.isPlaying) return;
        var manager = Game.Audio.AudioManager.instance;
        if (manager != null && manager.Settings != null) mechanism.outputAudioMixerGroup = manager.Settings.effects;
        bool runMotor = moving && progress < 1 && mechanism.clip != null;
        motorSpeed = StepMotor(motorSpeed, runMotor, Time.unscaledDeltaTime, spinUpSeconds, spinDownSeconds);
        float blend = Mathf.SmoothStep(0, 1, motorSpeed);
        mechanism.volume = runningVolume * blend;
        mechanism.pitch = Mathf.Lerp(stoppedPitch, 1, blend);
        // Keep the same playhead through a quick stop/resume; stop only after the tail fades.
        if (runMotor && !mechanism.isPlaying) mechanism.Play();
        else if (!runMotor && motorSpeed <= 0 && mechanism.isPlaying) mechanism.Stop();
    }
    void LateUpdate()
    {
        var run = RailExpedition.Instance;
        bool active = PhotonSession.OnTrain && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady && run != null;
        Present(active ? run.State.BridgeProgress(PhotonNetwork.Time) : 0,
            active && PhotonSession.Phase == RailPhase.Running && run.State.BridgeMoving(PhotonNetwork.Time));
    }
    void OnDisable()
    {
        motorSpeed = 0;
        if (mechanism != null) { mechanism.Stop(); mechanism.volume = 0; mechanism.pitch = stoppedPitch; }
    }
}
