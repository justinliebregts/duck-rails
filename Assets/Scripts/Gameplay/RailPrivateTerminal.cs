using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>Per-client keypad input; existing PhotonSession owns all room operations.</summary>
public sealed class RailPrivateTerminal : MonoBehaviour
{
    public TMP_Text display;
    public PressableButton[] editButtons;
    public PressableButton create, join, start, leave;
    public string Code { get; private set; } = "";
    bool Ready => PhotonSession.Instance != null && PhotonSession.Instance.WorldReady &&
        !PhotonSession.Instance.HasRoomRequest && !PhotonSession.Instance.AwaitingCrewDestination &&
        !PhotonSession.OnTrain && !RailWorld.InputBlocked;
    public void Digit(string digit)
    {
        if (Ready && Code.Length < 5 && digit.Length == 1 && digit[0] >= '0' && digit[0] <= '9') Code += digit;
    }
    public void Backspace() { if (Ready && Code.Length > 0) Code = Code.Substring(0, Code.Length - 1); }
    public void Clear() { if (Ready) Code = ""; }
    public void Create() { if (Ready && Code.Length == 5) PhotonSession.Instance.RequestCreatePrivate(Code); }
    public void Join() { if (Ready && Code.Length == 5) PhotonSession.Instance.RequestJoinCode(Code); }
    public void StartRun() { if (Ready && PhotonSession.IsPrivate && PhotonNetwork.IsMasterClient) PhotonSession.Instance.StartPrivateRun(); }
    public void Leave() { if (Ready && PhotonSession.IsPrivate) PhotonSession.Instance.RequestJoinRandom(); }
    void Update()
    {
        bool ready = Ready;
        foreach (var button in editButtons) button.interactable = ready;
        create.interactable = join.interactable = ready && Code.Length == 5;
        start.interactable = ready && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging && PhotonNetwork.IsMasterClient;
        leave.interactable = ready && PhotonSession.IsPrivate;
        var session = PhotonSession.Instance;
        string room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "-----";
        string status = session == null ? "Connecting..." : !string.IsNullOrEmpty(session.LastError) ? session.LastError :
            !ready ? session.Status : PhotonSession.IsPrivate ? "Private crew: master presses START" : "Choose five digits, then CREATE or JOIN";
        display.text = "ROOM " + room + "\n" + Code.PadRight(5, '_') + "\n" + status;
    }
}
