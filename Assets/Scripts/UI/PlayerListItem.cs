using UnityEngine;
using TMPro;
using Photon.Pun;

public class PlayerListItem : MonoBehaviour
{
    const string SelfMutedKey = "SelfMuted";

    Photon.Realtime.Player cachedPlayer;
    PhotonPlayer companion;
    public TextMeshPro playerName;
    bool selfMuted;
    public PauseMenuButton muteButton;
    public TextMeshPro muteText;

    public static bool ReadSelfMuted() {
        return PlayerPrefsX.GetBool(SelfMutedKey, false);
    }

    public static void ApplySavedSelfMute() {
        PhotonVoiceChat.SetSelfMuted(ReadSelfMuted());
    }

    // A voice ban keeps the mic off. Self-mute is the only state this writes.
    public static void SetLocalSelfMuted(bool muted) {
        if (PhotonVoiceChat.PunishmentMuted)
            return;

        PlayerPrefsX.SetBool(SelfMutedKey, muted);
        UserPlayerPrefs.Save();
        PhotonVoiceChat.SetSelfMuted(muted);
        ShowLocalMuteLocked();
    }

    public void Init(Photon.Realtime.Player p) {
        cachedPlayer = p;
        companion = null;
        bool muted = false;
        if (PhotonNetwork.LocalPlayer == p) {
            playerName.color = Color.yellow;
            selfMuted = PhotonVoiceChat.PunishmentMuted || ReadSelfMuted();
            muted = selfMuted;
            PhotonVoiceChat.SetSelfMuted(selfMuted);
            muteButton.ShowPressed(selfMuted);
        } else if (GameManager.instance != null) {
            PhotonPlayer photonPlayer = GameManager.instance.GetPhotonPlayer(p.ActorNumber);
            muted = photonPlayer != null && photonPlayer._voiceSource != null && photonPlayer._voiceSource.mute;
        }
        ApplyMuteText(muted);
        playerName.text = p.NickName;
    }

    public void InitCompanion(PhotonPlayer duck) {
        cachedPlayer = null;
        companion = duck;
        playerName.color = Color.white;
        playerName.text = "Duck";
        AudioSource voice = duck != null ? duck.VoiceSource : null;
        ApplyCompanionMute(voice != null && voice.mute);
    }

    public void ToggleMute() {
        if (companion != null) {
            AudioSource voice = companion.VoiceSource;
            DuckCompanion duck = companion.Companion;
            if (voice != null && duck != null && duck.SelfMute != null) {
                duck.SelfMute.SetMuted(!voice.mute);
                ApplyMuteText(voice.mute);
            }
            return;
        }

        if (cachedPlayer == null)
            return;

        if (PhotonNetwork.LocalPlayer != cachedPlayer) {
            PhotonPlayer p = GameManager.instance.GetPhotonPlayer(cachedPlayer.ActorNumber);
            if (p != null && p._voiceSource != null) {
                p._voiceSource.mute = !p._voiceSource.mute;
                ApplyMuteText(p._voiceSource.mute);
            }
            return;
        }

        if (PhotonVoiceChat.PunishmentMuted) {
            selfMuted = true;
            muteButton.ShowPressed(true);
            ApplyMuteText(true);
            return;
        }

        SetLocalSelfMuted(!selfMuted);
    }

    public static void ShowLocalMuteLocked() {
        foreach (PlayerListItem item in FindObjectsByType<PlayerListItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            item.ShowPunishmentLock();
    }

    void ShowPunishmentLock() {
        if (cachedPlayer == null || PhotonNetwork.LocalPlayer != cachedPlayer || muteButton == null)
            return;

        selfMuted = PhotonVoiceChat.PunishmentMuted || ReadSelfMuted();
        muteButton.ShowPressed(selfMuted);
        ApplyMuteText(selfMuted);
    }

    void OnEnable() {
        DuckCompanion.UnmutedItself += OnDuckUnmutedItself;
        if (companion != null) {
            AudioSource voice = companion.VoiceSource;
            ApplyCompanionMute(voice != null && voice.mute);
        }
    }

    void OnDisable() {
        DuckCompanion.UnmutedItself -= OnDuckUnmutedItself;
    }

    void OnDuckUnmutedItself(DuckCompanion duck) {
        if (companion == null || duck == null || companion.Companion != duck) {
            return;
        }
        ApplyCompanionMute(false);
    }

    void ApplyCompanionMute(bool muted) {
        ApplyMuteText(muted);
        if (muteButton != null) {
            muteButton.ShowPressed(muted);
        }
    }

    void ApplyMuteText(bool muted) {
        if (muteText == null) {
            return;
        }
        muteText.color = muted ? Color.red : Color.white;
    }
}
