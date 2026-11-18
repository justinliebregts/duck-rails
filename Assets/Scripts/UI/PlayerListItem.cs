using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class PlayerListItem : MonoBehaviour
{
    const string SelfMutedKey = "SelfMuted";

    Photon.Realtime.Player cachedPlayer;
    public TextMeshPro playerName;
    bool selfMuted;
    public PauseMenuButton muteButton;
    public TextMeshPro muteText;
    float baseNameFontSize = -1f;

    // PlayersMenu shrinks rows so a full lobby fits on one page; the mute button keeps its size.
    public void SetRowScale(float scale, float rowHeight) {
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = rowHeight;
        if (playerName == null)
            return;
        if (baseNameFontSize <= 0f)
            baseNameFontSize = playerName.fontSize;
        playerName.fontSize = baseNameFontSize * scale;
    }

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

    public void ToggleMute() {
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

    void ApplyMuteText(bool muted) {
        if (muteText == null) {
            return;
        }
        muteText.color = muted ? Color.red : Color.white;
    }
}
