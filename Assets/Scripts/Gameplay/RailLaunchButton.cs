using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// Private-staging launch control on the departure gate. Shown only in a private
/// room's staging phase; only the room owner (Photon Master Client) can press it.
/// PhotonSession.StartPrivateRun remains the authority for the transition.
/// </summary>
public sealed class RailLaunchButton : MonoBehaviour
{
    public PressableButton button;
    public TMP_Text prompt;

    void Awake()
    {
        if (button != null) button.onPressed.AddListener(Launch);
        SetShown(false);
    }

    void OnDestroy()
    {
        if (button != null) button.onPressed.RemoveListener(Launch);
    }

    void Update()
    {
        var session = PhotonSession.Instance;
        bool staging = session != null && PhotonSession.IsRailsRoom && PhotonSession.IsPrivate &&
            PhotonSession.Phase == RailPhase.Staging && !session.HasRoomRequest;
        SetShown(staging);
        if (!staging) return;
        string owner = PhotonNetwork.MasterClient?.NickName;
        if (string.IsNullOrEmpty(owner)) owner = "Owner";
        string text = owner + ", push to start!";
        if (prompt != null && prompt.text != text) prompt.text = text;
        if (button != null) button.interactable = PhotonNetwork.IsMasterClient && session.WorldReady;
    }

    void SetShown(bool shown)
    {
        if (button != null && button.gameObject.activeSelf != shown) button.gameObject.SetActive(shown);
        if (prompt != null && prompt.gameObject.activeSelf != shown) prompt.gameObject.SetActive(shown);
    }

    void Launch()
    {
        if (PhotonNetwork.IsMasterClient) PhotonSession.Instance?.StartPrivateRun();
    }
}
