using Photon.Pun;
using UnityEngine;

/// <summary>Bridge warning with a cooldown retained across interrupted lowering.</summary>
public sealed class RailBridgeAlarm : MonoBehaviour
{
    public AudioSource alarm;
    public const double AlarmInterval = 4.0;
    double nextAlarmAt = double.NegativeInfinity;
    // Wall time, retained through release/re-grab and disable; never catch up missed beeps.
    public static bool TakeAlarm(bool moving, double now, ref double nextAt)
    {
        if (!moving || now < nextAt) return false;
        nextAt = now + AlarmInterval;
        return true;
    }

    void LateUpdate()
    {
        var run = RailExpedition.Instance;
        bool active = PhotonSession.OnTrain && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady && run != null;
        bool moving = active && PhotonSession.Phase == RailPhase.Running && run.State.BridgeMoving(PhotonNetwork.Time) && run.State.BridgeProgress(PhotonNetwork.Time) < 1;
        if (alarm == null || alarm.clip == null) return;
        var manager = Game.Audio.AudioManager.instance;
        if (manager != null && manager.Settings != null) alarm.outputAudioMixerGroup = manager.Settings.effects;
        if (TakeAlarm(moving, Time.realtimeSinceStartupAsDouble, ref nextAlarmAt)) alarm.Play();
        // Let the baked tail finish on release. Disabling stops sound, not the cooldown.
    }
    void OnDisable() { if (alarm != null) alarm.Stop(); }
}
