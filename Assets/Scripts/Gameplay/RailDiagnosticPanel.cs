using Game.Animation;
using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>Local arm-mounted development panel; shares DuckPlayer fingertip buttons with the train.</summary>
[DefaultExecutionOrder(60)]
public sealed class RailDiagnosticPanel : MonoBehaviour
{
    public static RailDiagnosticPanel Instance { get; private set; }
    public bool IsOpen => desktopOpen || (panel != null && panel.activeSelf);
    public static bool KeyboardMode => (SimulatedRigInput.Instance != null && SimulatedRigInput.Instance.IsSimulating) ||
        (GR.Player.instance != null && GR.Player.instance.GetComponent<DesktopPlayer>() != null);
    public static bool BlocksDesktopInput => Instance != null && (Instance.desktopOpen || Time.frameCount <= Instance.closedFrame);
    bool desktopOpen;
    int closedFrame = -1;
    Vector2 desktopScroll;
    string statusText = "";
    string desktopToast = "";
    GameObject panel;
    TMP_Text readout;
    Transform anchor;
    float refresh;
    bool tools, markers;
    GameObject leftMarker, rightMarker;
    string notice = "";
    Material panelMaterial;
    Material buttonMaterial;
    TMP_Text toast;
    float toastUntil;
    public static bool OwnsMenuInput => Instance != null && RailDiagnostics.Development && GR.Player.instance != null;
    void Awake() { Instance = this; }
    void OnDisable() { Close(); }
    void OnDestroy() { if (panel != null) Destroy(panel); if (toast != null) Destroy(toast.gameObject); if (panelMaterial != null) Destroy(panelMaterial); if (buttonMaterial != null) Destroy(buttonMaterial); if (Instance == this) Instance = null; RailTestTools.RestoreNetwork(); }
    void Update()
    {
        if (!RailDiagnostics.Development) return;
        var player = GR.Player.instance;
        if (player == null) { Close(); return; }
        if (IsOpen && desktopOpen != KeyboardMode) { Close(); Toggle(); }
        if (player.ReturnButtonPress_Left(ButtonName.Start).wasPressed || (Input.GetKeyDown(KeyCode.P) && !DuckCompanionDebug.IsTyping)) Toggle();
        if (desktopOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (desktopOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (!RailTestTools.Allowed) RailTestTools.RestoreNetwork();
        if (toast != null) toast.gameObject.SetActive(!KeyboardMode && Time.unscaledTime < toastUntil);
    }
    public void Notify(string text)
    {
        desktopToast = text; toastUntil = Time.unscaledTime + 6;
        if (KeyboardMode || Camera.main == null) return;
        if (toast == null)
        {
            var go = new GameObject("Diagnostic notification"); go.transform.SetParent(Camera.main.transform, false);
            go.transform.localPosition = new Vector3(0, -.2f, .8f);
            toast = go.AddComponent<TextMeshPro>(); toast.fontSize = .17f; toast.alignment = TextAlignmentOptions.Center;
            toast.rectTransform.sizeDelta = new Vector2(.65f, .1f); toast.color = new Color(1, .85f, .25f);
        }
        toast.text = text; toastUntil = Time.unscaledTime + 6; toast.gameObject.SetActive(true);
    }
    public void Toggle()
    {
        if (!RailDiagnostics.Development) return;
        if (IsOpen) { Close(); return; }
        if (KeyboardMode) {
            desktopOpen = true; refresh = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            return;
        }
        if (panel == null) Build();
        panel.SetActive(true); refresh = 0;
    }
    void LateUpdate()
    {
        if (!IsOpen || GR.Player.instance == null) return;
        var driver = GR.Player.instance.GetComponent<LocalBodyDriver>();
        var hand = driver != null ? driver.Body?.LeftHand : null;
        bool desktop = desktopOpen;
        Transform target = desktop ? null : hand != null ? hand.parent : null;
        if (target != null && anchor != target) { anchor = target; panel.transform.SetParent(anchor, false); }
        if (!desktop && hand != null && anchor != null)
        {
            if (Camera.main != null) panel.transform.position = WristPanelPosition(hand.position, Camera.main.transform.position, Camera.main.transform.up);
            panel.transform.localScale = new Vector3(1 / anchor.lossyScale.x, 1 / anchor.lossyScale.y, 1 / anchor.lossyScale.z);
            if (Camera.main != null) panel.transform.rotation = Quaternion.LookRotation(panel.transform.position - Camera.main.transform.position, Camera.main.transform.up);
        }
        if (Time.unscaledTime < refresh) return;
        refresh = Time.unscaledTime + .25f;
        statusText = "DUCK RAILS · TEST PANEL\n" +
            "Room " + (PhotonNetwork.CurrentRoom?.Name ?? "—") + " · " + PhotonSession.Phase + "\n" +
            "Frame " + (RailDiagnostics.Instance?.FrameMs ?? 0).ToString("F1") + " ms · Ping " + PhotonNetwork.GetPing() + " ms\n" +
            "Master " + (PhotonNetwork.MasterClient?.ActorNumber ?? 0) + " · Fuel " + PhotonSession.CurrentFuel.ToString("F1") + "\n" +
            Held("L", GR.Player.instance.leftPlayerHand) + "  " + Held("R", GR.Player.instance.rightPlayerHand) + "\n" +
            (RailDiagnostics.Instance?.Notice ?? "") + "\n" + (string.IsNullOrEmpty(notice) ?
                (RailTestTools.Allowed && RailTestTools.Status == "Test tools require a private room" ? "Private test room ready" : RailTestTools.Status) : notice);
        if (!desktop && readout != null) readout.text = statusText;
        if (!desktop && markers && driver != null)
        {
            Mark(ref leftMarker, driver.Body?.LeftHand); Mark(ref rightMarker, driver.Body?.RightHand);
        }
    }
    static string Held(string label, PlayerHand hand)
    {
        var view = hand != null && hand.Held != null ? hand.Held.GetComponent<PhotonView>() : null;
        return label + (view == null ? " empty" : " item " + view.ViewID + " owner " + view.OwnerActorNr);
    }
    void Mark(ref GameObject marker, Transform hand)
    {
        if (hand == null) return;
        var finger = hand.GetComponentInChildren<SphereCollider>(); if (finger == null) return;
        if (marker == null)
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(marker.GetComponent<Collider>());
            marker.name = "Diagnostic finger"; marker.transform.SetParent(panel.transform, true);
            marker.transform.localScale = Vector3.one * .018f;
        }
        marker.transform.position = finger.transform.TransformPoint(finger.center);
    }
    public void Build()
    {
        if (panel != null) Destroy(panel);
        panel = new GameObject("Left arm diagnostics"); panel.SetActive(false);
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cube); bg.name = "Panel"; bg.transform.SetParent(panel.transform, false);
        bg.transform.localScale = new Vector3(.40f, .44f, .008f); bg.transform.localPosition = new Vector3(0, 0, .016f);
        Destroy(bg.GetComponent<Collider>());
        if (panelMaterial == null)
        {
            panelMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            panelMaterial.SetColor("_BaseColor", new Color(.025f, .045f, .065f));
        }
        bg.GetComponent<Renderer>().sharedMaterial = panelMaterial;
        var text = new GameObject("Status"); text.transform.SetParent(panel.transform, false);
        readout = text.AddComponent<TextMeshPro>(); readout.fontSize = .155f; readout.alignment = TextAlignmentOptions.TopLeft;
        readout.rectTransform.sizeDelta = new Vector2(.37f, .18f); text.transform.localPosition = new Vector3(0, .12f, 0);
        Button("REPORT BUG", -.095f, -.005f, () => { RailDiagnostics.Instance?.ReportBug(); });
        Button(tools ? "STATUS" : "TEST TOOLS", .095f, -.005f, () => { tools = !tools; Rebuild(); });
        if (tools)
        {
            Button("BRIDGE", -.095f, -.065f, RailTestTools.Bridge); Button("BOARD TRAIN", .095f, -.065f, RailTestTools.Board);
            Button("FUEL 20", -.095f, -.125f, () => RailTestTools.SetFuel(20)); Button("LOADED BAG", .095f, -.125f, RailTestTools.SpawnBag);
            Button("DOWN ME", -.095f, -.185f, RailTestTools.DownSelf); Button("REPEAT ROUTE", .095f, -.185f, RailTestTools.RepeatRoute);
        }
        else
        {
            Button("FINGER MARKERS", -.095f, -.065f, () => { markers = !markers; if (leftMarker != null) leftMarker.SetActive(markers); if (rightMarker != null) rightMarker.SetActive(markers); notice = markers ? "Finger markers on" : "Finger markers off"; });
            Button("GAME MENU", .095f, -.065f, () => { var menu = FindFirstObjectByType<PauseMenu>(); menu?.OpenFromDiagnostics(); Close(); });
            Button("SIMULATE LAG", -.095f, -.125f, () => RailTestTools.ApplyNetwork(true)); Button("NORMAL NETWORK", .095f, -.125f, RailTestTools.RestoreNetwork);
            Button("SAVE PRESET", -.095f, -.185f, RailTestTools.SavePreset); Button("CLOSE", .095f, -.185f, Close);
        }
    }
    void Rebuild() { anchor = null; leftMarker = rightMarker = null; Build(); panel.SetActive(true); }
    // The 44 cm panel's lower edge clears the wrist by 10 cm in the viewing plane.
    public static Vector3 WristPanelPosition(Vector3 wrist, Vector3 eye, Vector3 viewUp) =>
        wrist + viewUp.normalized * .32f + (eye - wrist).normalized * .10f;
    public void Close()
    {
        if (desktopOpen) { closedFrame = Time.frameCount; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        desktopOpen = false;
        if (panel != null) panel.SetActive(false);
    }
    void OnGUI()
    {
        if (!RailDiagnostics.Development || !KeyboardMode) return;
        if (!desktopOpen) {
            if (Time.unscaledTime < toastUntil) GUI.Box(new Rect(20, 20, Mathf.Min(500, Screen.width - 40), 55), desktopToast);
            return;
        }
        float scale = Mathf.Min(1.5f, Mathf.Min(Screen.width / 560f, Screen.height / 650f));
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        int depth = GUI.depth; GUI.depth = -100;
        var color = GUI.color; GUI.color = new Color(.025f, .045f, .065f, 1);
        GUI.DrawTexture(new Rect((width - 520) / 2, (height - 610) / 2, 520, 610), Texture2D.whiteTexture);
        GUI.color = color;
        var label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 42 };
        GUILayout.BeginArea(new Rect((width - 520) / 2, (height - 610) / 2, 520, 610), GUI.skin.box);
        desktopScroll = GUILayout.BeginScrollView(desktopScroll);
        GUILayout.Label(statusText, label);
        GUILayout.Space(10);
        if (GUILayout.Button("Report bug", button)) RailDiagnostics.Instance?.ReportBug();
        if (GUILayout.Button(tools ? "Show status controls" : "Show test tools", button)) tools = !tools;
        if (tools) {
            GuiPair("Bridge", RailTestTools.Bridge, "Board train", RailTestTools.Board, button);
            GuiPair("Fuel 20", () => RailTestTools.SetFuel(20), "Loaded bag", RailTestTools.SpawnBag, button);
            GuiPair("Down me", RailTestTools.DownSelf, "Repeat route", RailTestTools.RepeatRoute, button);
        } else {
            GuiPair("Simulate lag", () => RailTestTools.ApplyNetwork(true), "Normal network", RailTestTools.RestoreNetwork, button);
            GuiPair("Save preset", RailTestTools.SavePreset, "Game menu", () => { FindFirstObjectByType<PauseMenu>()?.OpenFromDiagnostics(); Close(); }, button);
        }
        GUILayout.Space(8);
        GUILayout.Label("P or Esc closes this panel. Click the game afterward to resume mouse look.", label);
        if (GUILayout.Button("Close", button)) Close();
        GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix = matrix; GUI.depth = depth;
    }
    static void GuiPair(string first, System.Action a, string second, System.Action b, GUIStyle style)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(first, style)) a();
        if (GUILayout.Button(second, style)) b();
        GUILayout.EndHorizontal();
    }
    void Button(string label, float x, float y, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label); go.transform.SetParent(panel.transform, false); go.transform.localPosition = new Vector3(x, y, -.008f);
        var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(.18f, .048f, .025f); collider.isTrigger = true;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube); cap.transform.SetParent(go.transform, false);
        cap.transform.localScale = new Vector3(.175f, .044f, .012f); Destroy(cap.GetComponent<Collider>());
        if (buttonMaterial == null)
        {
            buttonMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            buttonMaterial.SetColor("_BaseColor", new Color(.78f, .88f, .95f));
        }
        cap.GetComponent<Renderer>().sharedMaterial = buttonMaterial;
        var button = go.AddComponent<PressableButton>(); button.cap = cap.transform; button.pressOffset = new Vector3(0, 0, .006f);
        button.diagnostic = true; button.onPressed.AddListener(action);
        var labelGo = new GameObject("Label"); labelGo.transform.SetParent(cap.transform, false);
        labelGo.transform.localScale = new Vector3(1/.175f, 1/.044f, 1/.012f); labelGo.transform.localPosition = new Vector3(0, 0, -.6f);
        var tmp = labelGo.AddComponent<TextMeshPro>(); tmp.fontSize = .13f; tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center; tmp.rectTransform.sizeDelta = new Vector2(.17f, .04f); tmp.text = label;
    }
}
