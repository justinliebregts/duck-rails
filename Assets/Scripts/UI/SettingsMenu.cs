using System.Collections.Generic;
using Game.Audio;
using UnityEngine;
using TMPro;
public class SettingsMenu : MonoBehaviour
{
    public TextMeshPro rotateModeText;
    public TextMeshPro snapAngleText;

    const string RotateModeKey = "RotateMode";
    const string SnapRotateAmountKey = "SnapRotateAmount";
    const string SmoothRotateAmountKey = "SmoothRotateAmount";
    const string RotateDeadzoneAmountKey = "RotateDeadzoneAmount";

    const int DefaultRotateMode = 2;
    const int DefaultSnapRotateAmount = 45;
    const int DefaultSmoothRotateAmount = 30;
    const int DefaultRotateDeadzoneAmount = 15;

    // Effects level follows the saved game volume as soon as account prefs exist, while the pause menu is still closed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootGameVolume() {
        UserPlayerPrefs.UserReady -= ApplySavedGameVolume;
        UserPlayerPrefs.UserReady += ApplySavedGameVolume;
        if (UserPlayerPrefs.IsReady)
            ApplySavedGameVolume();
    }

    static void ApplySavedGameVolume() {
        AudioManager.SetGameVolume(UserPlayerPrefs.GetInt(AudioManager.GameVolumePrefsKey, AudioManager.DefaultGameVolume));
    }

    // Restore settings for each player, independently of the pause menu lifecycle.
    public static void ApplySavedTurnSettings(NewPlayer player) {
        if (player == null)
            return;

        player.rotateMode = Mathf.Clamp(UserPlayerPrefs.GetInt(RotateModeKey, DefaultRotateMode), 0, 2);
        player.snapRotateAmount = UserPlayerPrefs.GetInt(SnapRotateAmountKey, DefaultSnapRotateAmount);
        player.SetSmoothRotateAmount(UserPlayerPrefs.GetInt(SmoothRotateAmountKey, DefaultSmoothRotateAmount));
        player.SetRotateDeadzoneAmount(UserPlayerPrefs.GetInt(RotateDeadzoneAmountKey, DefaultRotateDeadzoneAmount));
    }

    PauseMenuButton[] pageButtons = System.Array.Empty<PauseMenuButton>();
    PauseMenuButton[] rotateButtons = System.Array.Empty<PauseMenuButton>();
    PauseMenuButton[] snapButtons = System.Array.Empty<PauseMenuButton>();
    GameObject audioPage;
    GameObject controlsPage;

    int rotateMode = DefaultRotateMode;
    int snapRotateAmount = DefaultSnapRotateAmount;
    int smoothRotateAmount = DefaultSmoothRotateAmount;
    int rotateDeadzoneAmount = DefaultRotateDeadzoneAmount;

    void Awake() {
        // Parent Awake runs before each button Awake, so the row is a radio group.
        pageButtons = BindRow("Buttons", OnPagePressed);
        rotateButtons = BindRow("ControlsPage/Rotate", OnRotatePressed);
        snapButtons = BindRow("ControlsPage/SnapAngle", OnSnapAnglePressed);

        Transform audio = transform.Find("AudioPage");
        Transform controls = transform.Find("ControlsPage");
        audioPage = audio != null ? audio.gameObject : null;
        controlsPage = controls != null ? controls.gameObject : null;

        UserPlayerPrefs.UserReady += OnUserReady;
    }

    void OnEnable() {
        ShowNamed(pageButtons, "Audio");
        ReadPrefs();
        ShowPage("Audio");
        ShowSavedButtons();
    }

    void OnDestroy() {
        UserPlayerPrefs.UserReady -= OnUserReady;
    }

    void OnUserReady() {
        ReadPrefs();
        if (isActiveAndEnabled)
            ShowSavedButtons();
    }

    PauseMenuButton[] BindRow(string path, System.Action<PauseMenuButton> onPressed) {
        Transform row = transform.Find(path);
        if (row == null)
            return System.Array.Empty<PauseMenuButton>();

        var found = new List<PauseMenuButton>();
        for (int i = 0; i < row.childCount; i++) {
            PauseMenuButton button = row.GetChild(i).GetComponent<PauseMenuButton>();
            if (button == null)
                continue;

            button.momentary = false;
            button.preserveLatch = true;
            PauseMenuButton pressed = button;
            button.pressedIn.AddListener(() => onPressed(pressed));
            found.Add(button);
        }

        for (int i = 0; i < found.Count; i++)
            found[i].RebuildGroup();

        return found.ToArray();
    }

    void ReadPrefs() {
        rotateMode = Mathf.Clamp(UserPlayerPrefs.GetInt(RotateModeKey, DefaultRotateMode), 0, 2);
        snapRotateAmount = UserPlayerPrefs.GetInt(SnapRotateAmountKey, DefaultSnapRotateAmount);
        smoothRotateAmount = UserPlayerPrefs.GetInt(SmoothRotateAmountKey, DefaultSmoothRotateAmount);
        rotateDeadzoneAmount = UserPlayerPrefs.GetInt(RotateDeadzoneAmountKey, DefaultRotateDeadzoneAmount);
    }

    void ShowSavedButtons() {
        ShowNamed(rotateButtons, NameForMode(rotateMode));
        ShowNamed(snapButtons, snapRotateAmount.ToString());
        RefreshLabels();
    }

    void RefreshLabels() {
        if (rotateModeText != null)
            rotateModeText.text = "Rotate: " + NameForMode(rotateMode);
        if (snapAngleText != null)
            snapAngleText.text = "Snap Angle: " + snapRotateAmount;
    }

    static void ShowNamed(PauseMenuButton[] row, string selectedName) {
        for (int i = 0; i < row.Length; i++) {
            PauseMenuButton button = row[i];
            if (button != null)
                button.ShowPressed(button.name == selectedName);
        }
    }

    void OnPagePressed(PauseMenuButton button) {
        ShowPage(button.name);
    }

    void ShowPage(string page) {
        if (audioPage != null)
            audioPage.SetActive(page == "Audio");
        if (controlsPage != null)
            controlsPage.SetActive(page == "Controls");
        if (page == "Controls")
            ShowSavedButtons();
    }

    void OnRotatePressed(PauseMenuButton button) {
        int mode = ModeFor(button.name);
        if (mode < 0)
            return;

        rotateMode = mode;
        UserPlayerPrefs.SetInt(RotateModeKey, mode);
        UserPlayerPrefs.Save();
        ApplyToPlayer();
        ShowSavedButtons();
    }

    void OnSnapAnglePressed(PauseMenuButton button) {
        if (!int.TryParse(button.name, out int degrees))
            return;

        snapRotateAmount = degrees;
        UserPlayerPrefs.SetInt(SnapRotateAmountKey, degrees);
        UserPlayerPrefs.Save();
        ApplyToPlayer();
        ShowSavedButtons();
    }

    public void ApplySlider(PauseMenuSlider.Setting setting, int value) {
        if (setting == PauseMenuSlider.Setting.GameVolume) {
            AudioManager.SetGameVolume(value);
            return;
        }
        if (setting == PauseMenuSlider.Setting.ChatVolume) {
            PhotonVoiceChat.SetIncomingVolume(value);
            return;
        }

        NewPlayer player = NewPlayer.instance;
        if (setting == PauseMenuSlider.Setting.SmoothStrength) {
            smoothRotateAmount = value;
            if (player != null)
                player.SetSmoothRotateAmount(value);
        } else {
            rotateDeadzoneAmount = value;
            if (player != null)
                player.SetRotateDeadzoneAmount(value);
        }
    }

    void ApplyToPlayer() {
        ApplySavedTurnSettings(NewPlayer.instance);
        ReadPrefs();
    }

    static int ModeFor(string buttonName) {
        if (buttonName == "Snap")
            return 0;
        if (buttonName == "Smooth")
            return 1;
        if (buttonName == "None")
            return 2;
        return -1;
    }

    static string NameForMode(int mode) {
        if (mode == 0)
            return "Snap";
        if (mode == 1)
            return "Smooth";
        return "None";
    }
}
