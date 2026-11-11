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

    const string BackToCompoundName = "BackToCompound";

    TMP_Text currentRoomCode;
    TMP_Text enteredRoomCodeText;
    // Expedition mode shows only the room code and Back to Compound.
    bool? expeditionMode;
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
        Bind(BackToCompoundName, BackToCompound);
        for (int i = 0; i <= 9; i++) {
            int digit = i;
            Bind("Numbers/" + digit, () => AppendDigit(digit));
        }
    }

    void OnEnable() {
        Listen(PhotonSession.Instance);
        expeditionMode = null;
        ApplyMode();
    }

    void Update() {
        ApplyMode();
    }

    static bool InExpedition => PhotonSession.IsRailsRoom && PhotonSession.OnTrain;

    void ApplyMode() {
        bool expedition = InExpedition;
        if (expeditionMode == expedition) {
            return;
        }

        expeditionMode = expedition;
        foreach (Transform child in transform) {
            bool shown = child.name == BackToCompoundName ? expedition
                : enteredRoomCodeText != null && child == enteredRoomCodeText.transform || !expedition;
            child.gameObject.SetActive(shown);
        }

        if (expedition) {
            typed = "";
            showingFailure = false;
        }
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

    // Leave the expedition alone (the crew stays aboard) and join a random public
    // compound; PhotonSession creates a fresh compound when none is open.
    void BackToCompound() {
        if (!InExpedition || !AcceptButtonPress()) {
            return;
        }

        PhotonSession session = PhotonSession.Ensure();
        Listen(session);
        session.RequestJoinRandom();
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
        session.RequestCreatePrivate(typed);
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
        if (InExpedition || Time.time < nextDigitTime) {
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
        RefreshEntered();
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
        if (session != null && session.HasCurrentRoom(out string code, out _)) {
            // Public expeditions are public too; only code-created rooms are private.
            currentRoomCode.text = RoomCodePrefix + code + (PhotonSession.IsPrivate ? " Private" : " Public");
        } else {
            currentRoomCode.text = "Not in a room";
        }
    }

    void RefreshEntered() {
        if (enteredRoomCodeText == null || showingFailure) {
            return;
        }

        if (expeditionMode == true) {
            PhotonSession session = PhotonSession.Instance;
            enteredRoomCodeText.text = session != null && session.HasCurrentRoom(out string code, out _)
                ? RoomCodePrefix + code : "Not in a room";
            return;
        }

        enteredRoomCodeText.text = RoomCodePrefix + typed.PadRight(PhotonSession.RoomCodeLength, '_');
    }
}
