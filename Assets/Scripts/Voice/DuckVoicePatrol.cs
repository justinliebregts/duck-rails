using System;
using UnityEngine;
using VoicePatrol.Scripts.Integrations.PhotonVoice;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

// Photon Voice tap with cloud ban storage. Recording stays on so the mic pipeline
// stays up. A pause-menu mute stops transmit, the walkie, and Voice Patrol.
public class DuckVoicePatrol : VoicePatrolPhoton
{
    bool privateRoom;

    public override string ProviderName => "Photon-Duck";

    public void SetPrivateRoom(bool value)
    {
        privateRoom = value;
    }

    protected override void Update()
    {
        base.Update();

        // SetForceMuted applies on this frame. The repeating mute check can lag a second behind a pause-menu toggle.
        if (!VoicePatrolWrapper.IsSdkRunning())
            return;

        bool pauseMonitoring = PhotonVoiceChat.IsLocallyMuted;
        if (IsForceMuted != pauseMonitoring)
            SetForceMuted(pauseMonitoring);
    }

    protected override MuteReason EvaluateMuteReason()
    {
        if (IsPunishmentMuted())
            return MuteReason.PunishmentMute;

        if (privateRoom || IsCurrentRoomPrivate())
            return MuteReason.PrivateRoom;

        if (PhotonVoiceChat.IsLocallyMuted)
            return MuteReason.NotTransmitting;

        return MuteReason.NotMuted;
    }

    protected override void ForceProviderMute()
    {
        PhotonVoiceChat.SetPunishmentMuted(true);
    }

    protected override void RestoreProviderMute()
    {
        // The punishment lock is still held until OnMuteExpired. SetSelfMuted keeps
        // transmit off until VoicePatrolManager clears that lock.
        PhotonVoiceChat.SetSelfMuted(PlayerListItem.ReadSelfMuted());
    }

    protected override void SaveViolationData()
    {
        VoicePatrolManager manager = VoicePatrolManager.Instance;
        if (manager == null)
            manager = GetComponent<VoicePatrolManager>();

        PlayerSession session = PlayerSession.Instance;
        string platform = session != null ? session.Platform : "(no session)";
        string playerId = session != null ? session.PlayerId : "(no session)";

        if (!IsDataLoaded || manager == null)
        {
            if (DuckzLog.AllowWarning(out var logNotice1))
                logNotice1.Write("Ban not saved. dataLoaded=" + IsDataLoaded + " manager=" + (manager != null), this);
            return;
        }

        Log("Saving ban. platform=" + platform + " player=" + playerId + " violations=" + ViolationCount);
        manager.SaveBan(new VoiceBanBlob
        {
            violationCount = ViolationCount,
            muteEndTimeTicks = MuteEndTime.Ticks,
            muteReason = MuteReasonText ?? ""
        });
    }

    protected override void LoadViolationData()
    {
        if (VoicePatrolManager.Instance == null)
        {
            IsDataLoaded = true;
            return;
        }

        VoicePatrolManager.Instance.LoadBan(ApplyLoadedBan);
    }

    void ApplyLoadedBan(VoiceBanBlob blob)
    {
        if (blob != null)
        {
            ViolationCount = blob.violationCount;
            MuteReasonText = blob.muteReason ?? "";
            if (blob.muteEndTimeTicks > 0)
                MuteEndTime = new DateTime(blob.muteEndTimeTicks, DateTimeKind.Utc);
        }

        IsDataLoaded = true;

        if (IsPunishmentMuted())
        {
            ForcePunishmentMuteState();
            VoicePatrolManager.Instance.RemindExistingMute();
        }
    }
}
