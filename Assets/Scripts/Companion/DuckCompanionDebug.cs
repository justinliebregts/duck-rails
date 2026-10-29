using Photon.Pun;
using UnityEngine;

/// <summary>Editor-only readout and typed fallback, toggled with F8.</summary>
public sealed class DuckCompanionDebug : MonoBehaviour
{
#if UNITY_EDITOR
    const string VisiblePrefKey = "Duck.CompanionDebug.Visible";

    public static bool IsTyping { get; private set; }
    public static bool IsHovered { get; private set; }
    bool visible;
    string input = "";

    void Awake()
    {
        visible = UnityEditor.EditorPrefs.GetBool(VisiblePrefKey, false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
            SetVisible(!visible);
    }

    void OnGUI()
    {
        if (!visible)
        {
            IsHovered = false;
            IsTyping = false;
            return;
        }

        Rect area = new Rect(Mathf.Max(8, Screen.width - 388), 8, 380, 468);
        IsHovered = area.Contains(Event.current.mousePosition);
        DuckCompanion duck = FindAnyObjectByType<DuckCompanion>();
        if (duck == null) IsTyping = false;
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Duck companion (F8)");
        if (GUILayout.Button("−", GUILayout.Width(24f)))
            SetVisible(false);
        GUILayout.EndHorizontal();
        if (duck == null)
            GUILayout.Label(GameManager.instance != null && GameManager.instance.disableDuckCompanion && PhotonNetwork.IsMasterClient
                ? "Duck is disabled on GameManager." : PhotonNetwork.InRoom ? "Waiting for Duck to spawn..." : "Join a room to meet Duck.");
        else
        {
            GUILayout.Label("Movement: " + duck.State + " | " + duck.Mode + " | Following actor " + duck.TargetActor);
            GUILayout.Label("Last movement event: " + duck.LastEvent);
            GUILayout.Label("Home wandering: " + duck.HomeWanderStatus);
            GUILayout.Label("Attack: " + duck.KillerStatus);
            GUILayout.Label("Conversation: " + duck.Dialogue.Status);
            var recognizer = DuckOfflineRecognition.Instance;
            GUILayout.Label(recognizer != null ? recognizer.Status : "Microphone starting...");
            if (recognizer != null)
            {
                GUILayout.Label(recognizer.ListeningStatus, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.Label("Input: " + recognizer.InputFormat + " | Frames: " + recognizer.ReceivedFrames);
                GUILayout.Label("Duck listening gain: " + recognizer.RecognitionGain.ToString("0.0") + "x");
                recognizer.RecognitionGain = GUILayout.HorizontalSlider(recognizer.RecognitionGain, 1f, 8f);
            }
            GUILayout.Label("Mic: " + (recognizer != null ? recognizer.MicLevel.ToString("0.0000") : "0")
                + (PhotonVoiceChat.IsLocallyMuted ? " (muted)" : ""));
            GUILayout.Label("Heard: " + (recognizer != null ? recognizer.LastHeard : duck.Dialogue.LastHeard));
            GUILayout.Label("Duck: " + duck.Dialogue.LastReply, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUI.SetNextControlName("DuckTypedInput");
            input = GUILayout.TextField(input, 220);
            IsTyping = GUI.GetNameOfFocusedControl() == "DuckTypedInput";
            if (GUILayout.Button("Talk to Duck") && !string.IsNullOrWhiteSpace(input))
            {
                duck.Dialogue.SubmitLocal("Duck, " + input);
                input = "";
                GUI.FocusControl(null);
                IsTyping = false;
            }
            GUILayout.Label($"Buddy chat: {DuckCompanionDialogue.BuddyConversationRange:0}m. Say Duck or follow me: {DuckCompanionDialogue.ConversationRange:0}m. Replies stay open for 60s.");
        }
        GUILayout.EndArea();
    }

    void SetVisible(bool value)
    {
        visible = value;
        UnityEditor.EditorPrefs.SetBool(VisiblePrefKey, value);
        if (!visible) { IsTyping = false; IsHovered = false; }
    }

    void OnDestroy() { IsTyping = false; IsHovered = false; }
#else
    // Desktop controls reference these flags in player builds too. No GUI or
    // per-frame callbacks are compiled outside the Editor.
    public static bool IsTyping => false;
    public static bool IsHovered => false;
#endif
}
