using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RoomMenu : MonoBehaviour
{
    const string RoomCodePrefix = "Room Code: ";

    [Tooltip("Seconds after a digit is accepted before another number press counts.")]
    public float digitDelay = 0.3f;

    [Tooltip("Seconds after a room button is accepted before another room button press counts.")]
    public float buttonDelay = 0.3f;

    TMP_Text currentRoomCode;
    TMP_Text enteredRoomCodeText;
    PhotonSession listened;
    string typed = "";
    bool showingFailure;
    float nextDigitTime;
    float nextButtonTime;

    void Awake() {
        currentRoomCode = FindText("CurrentRoomCode");
        enteredRoomCodeText = FindText("EnteredRoomCodeText");
        Bind("JoinRandomButton", JoinRandom);
        Bind("CreatePublic", CreatePublic);
        Bind("CreatePrivate", CreatePrivate);
        Bind("EnterRoomCode", EnterCode);
        for (int i = 0; i <= 9; i++) {
            int digit = i;
            Bind("Numbers/" + digit, () => AppendDigit(digit));
        }
    }

    void OnEnable() {
        Listen(PhotonSession.Instance);
        RefreshCurrentRoom();
        RefreshEntered();
    }

    void OnDestroy() {
        Unsubscribe();
    }

    void Listen(PhotonSession session) {
        if (session == null || listened == session) {
            return;
        }

        Unsubscribe();
        session.RoomMembershipChanged += OnRoomMembershipChanged;
        session.RoomRequestStarted += OnRoomRequestStarted;
        session.RoomRequestFailed += ShowFailure;
        listened = session;
    }

    void Unsubscribe() {
        if (listened == null) {
            return;
        }

        listened.RoomMembershipChanged -= OnRoomMembershipChanged;
        listened.RoomRequestStarted -= OnRoomRequestStarted;
        listened.RoomRequestFailed -= ShowFailure;
        listened = null;
    }

    TMP_Text FindText(string objectName) {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    void Bind(string path, UnityAction action) {
        Transform child = transform.Find(path);
        if (child == null) {
            return;
        }

        PauseMenuButton button = child.GetComponent<PauseMenuButton>();
        if (button == null) {
            return;
        }

        button.momentary = true;
        button.pressedIn.AddListener(action);
    }

    void JoinRandom() {
        if (!AcceptButtonPress()) {
            return;
        }

        PhotonSession session = PhotonSession.Ensure();
        Listen(session);
        session.RequestJoinRandom();
    }

    void CreatePublic() {
        if (!AcceptButtonPress()) {
            return;
        }

        PhotonSession session = PhotonSession.Ensure();
        Listen(session);
        session.RequestCreatePublic();
    }

    void CreatePrivate() {
        if (!AcceptButtonPress()) {
            return;
        }

        PhotonSession session = PhotonSession.Ensure();
        Listen(session);
        session.RequestCreatePrivate();
    }

    void EnterCode() {
        if (typed.Length != PhotonSession.RoomCodeLength || !AcceptButtonPress()) {
            return;
        }

        PhotonSession session = PhotonSession.Ensure();
        Listen(session);
        session.RequestJoinCode(typed);
    }

    bool AcceptButtonPress() {
        if (Time.time < nextButtonTime) {
            return false;
        }

        nextButtonTime = Time.time + buttonDelay;
        return true;
    }

    void AppendDigit(int digit) {
        if (Time.time < nextDigitTime) {
            return;
        }

        nextDigitTime = Time.time + digitDelay;
        char glyph = (char)('0' + digit);
        if (showingFailure || typed.Length >= PhotonSession.RoomCodeLength) {
            typed = glyph.ToString();
        } else {
            typed += glyph;
        }

        showingFailure = false;
        RefreshEntered();
    }

    public void ClearEnteredCode() {
        typed = "";
        showingFailure = false;
        RefreshEntered();
    }

    void OnRoomRequestStarted() {
        showingFailure = false;
        RefreshEntered();
    }

    void OnRoomMembershipChanged() {
        RefreshCurrentRoom();
    }

    void ShowFailure(string message) {
        showingFailure = true;
        if (enteredRoomCodeText != null) {
            enteredRoomCodeText.text = message;
        }
    }

    void RefreshCurrentRoom() {
        if (currentRoomCode == null) {
            return;
        }

        PhotonSession session = PhotonSession.Instance;
        if (session != null && session.HasCurrentRoom(out string code, out bool isPublic)) {
            currentRoomCode.text = RoomCodePrefix + code + (isPublic ? " Public" : " Private");
        } else {
            currentRoomCode.text = "Not in a room";
        }
    }

    void RefreshEntered() {
        if (enteredRoomCodeText == null || showingFailure) {
            return;
        }

        enteredRoomCodeText.text = RoomCodePrefix + typed.PadRight(PhotonSession.RoomCodeLength, '_');
    }
}
