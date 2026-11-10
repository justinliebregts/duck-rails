using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Masks world transitions (compound to expedition, return, room changes) after the first world is ready.
/// On a headset, the veil uses compositor layers: an opaque black cubemap plus a message panel fixed in
/// tracking space. The compositor keeps showing these at display rate, with head tracking, while Unity
/// stalls during scene activation or route building. Desktop and the mirror view use a screen overlay.
/// It latches on <see cref="PhotonSession.InWorldTransition"/> and releases after the world is ready.
/// </summary>
[DefaultExecutionOrder(-30000)]
public sealed class RailTransitionVeil : MonoBehaviour
{
    const float FadeInSeconds = .3f, FadeOutSeconds = .5f, ReadyHoldSeconds = .35f;
    const float PanelDistance = 2.2f, PanelWidth = 1.6f, PanelHeight = .8f;
    const int PanelPixelsWide = 1024, PanelPixelsHigh = 512;
    static readonly Vector3 HiddenRenderOrigin = new Vector3(0, -10000, 0);

    static RailTransitionVeil instance;
    public static float Alpha => instance != null ? instance.alpha : 0;
    public static bool Covered => instance != null && instance.alpha >= .999f;
    public static bool Showing => instance != null && instance.latched;

    bool everReady, latched, compositor;
    float alpha, readySince = -1;
    string title, detail;
    CanvasGroup screenGroup;
    TextMeshProUGUI screenTitle, screenDetail;
    GameObject panelCanvasRoot;
    TextMeshProUGUI panelTitle, panelDetail;
    Camera panelCamera;
    RenderTexture panelTexture;
    Cubemap black;
    OVROverlay cubeLayer, panelLayer;
    Pose panelTrackingPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("Transition veil");
        DontDestroyOnLoad(go);
        go.AddComponent<RailTransitionVeil>();
    }

    /// <summary>Waits for full cover when the veil is engaging; returns at once if it is not.</summary>
    public static IEnumerator WaitUntilCovered(float timeoutSeconds)
    {
        if (instance == null) yield break;
        instance.Tick(); // Engage this frame if a transition just began.
        double until = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
        while (instance != null && instance.latched && !Covered && Time.realtimeSinceStartupAsDouble < until) yield return null;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        BuildScreenOverlay();
        Application.onBeforeRender += PlacePanel;
    }
    void OnDestroy()
    {
        Application.onBeforeRender -= PlacePanel;
        if (instance == this) instance = null;
        if (panelTexture != null) { panelTexture.Release(); Destroy(panelTexture); }
        if (black != null) Destroy(black);
    }

    void Update() => Tick();
    // RailWorld marks readiness during Update, after this component; observe it the same frame so a
    // room request issued straight after first readiness is still veiled.
    void LateUpdate()
    {
        var session = PhotonSession.Instance;
        if (session != null && session.WorldReady && !session.InWorldTransition) everReady = true;
    }

#if UNITY_EDITOR
    /// <summary>Editor checks: hold the veil on through the normal path, optionally without the black cubemap.</summary>
    public static bool EditorForce, EditorHideCube;
    /// <summary>Editor check: renders the headset message panel offscreen and returns a readable copy.</summary>
    public static Texture2D EditorRenderPanelPreview(string previewTitle, string previewDetail)
    {
        var go = new GameObject("Transition veil preview") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var veil = go.AddComponent<RailTransitionVeil>();
            veil.title = previewTitle; veil.detail = previewDetail;
            veil.BuildPanelCanvas();
            veil.RenderPanel();
            var previous = RenderTexture.active;
            RenderTexture.active = veil.panelTexture;
            var copy = new Texture2D(PanelPixelsWide, PanelPixelsHigh, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, PanelPixelsWide, PanelPixelsHigh), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            veil.panelTexture.Release();
            DestroyImmediate(veil.panelTexture);
            return copy;
        }
        finally { DestroyImmediate(go); }
    }
#endif

    int tickedFrame = -1;
    void Tick()
    {
        if (tickedFrame == Time.frameCount) return;
        tickedFrame = Time.frameCount;
        var session = PhotonSession.Instance;
        float now = Time.unscaledTime;
        bool ready = session != null && session.WorldReady && !session.InWorldTransition;
        if (ready) everReady = true;
        // The login flow has its own status presentation; only veil in-game transitions.
        if (!latched && everReady && session != null && session.InWorldTransition) Engage(session);
#if UNITY_EDITOR
        if (EditorForce && !latched && session != null) Engage(session);
        if (EditorForce) readySince = -1;
#endif
        if (latched)
        {
#if UNITY_EDITOR
            if (EditorForce) ready = false;
#endif
            if (ready) { if (readySince < 0) readySince = now; }
            else readySince = -1;
            if (readySince >= 0 && now - readySince >= ReadyHoldSeconds) latched = false;
            if (session != null) UpdateText(session);
        }
        float step = Time.unscaledDeltaTime / (latched ? FadeInSeconds : FadeOutSeconds);
        // Cap the per-frame step so a long stall does not skip the fade in either direction.
        alpha = Mathf.MoveTowards(alpha, latched ? 1 : 0, Mathf.Min(step, .2f));
        Apply();
    }

    void Engage(PhotonSession session)
    {
        latched = true; readySince = -1;
        compositor = HeadsetPresent();
        if (compositor)
        {
            EnsureCompositorLayers();
            AnchorPanelInFrontOfHead();
        }
        UpdateText(session);
    }

    static bool HeadsetPresent()
    {
        if (DesktopPlay.UseDesktop || !XRSettings.isDeviceActive) return false;
        return OVRManager.OVRManagerinitialized && OVRManager.isHmdPresent;
    }

    void UpdateText(PhotonSession session)
    {
        string nextTitle = session.RecoveringConnection ? "RECONNECTING"
            : session.HeadingToExpedition ? "ALL ABOARD"
            : GameScenes.IsLoaded(GameScenes.Expedition) ? "HEADING HOME" : "LOADING";
        string nextDetail = session.HeadingToExpedition && !session.RecoveringConnection
            ? "The train is departing...\n" + session.Status : session.Status;
        if (nextTitle == title && nextDetail == detail) return;
        title = nextTitle; detail = nextDetail;
        screenTitle.text = title; screenDetail.text = detail;
        if (panelCanvasRoot != null) RenderPanel();
    }

    void Apply()
    {
        screenGroup.alpha = alpha;
        screenGroup.gameObject.SetActive(alpha > 0);
        bool layers = compositor && alpha > 0;
        if (cubeLayer != null)
        {
            bool cube = layers;
#if UNITY_EDITOR
            cube &= !EditorHideCube;
#endif
            if (cubeLayer.enabled != cube) cubeLayer.enabled = cube;
            cubeLayer.colorScale = new Vector4(1, 1, 1, alpha);
        }
        if (panelLayer != null)
        {
            if (panelLayer.enabled != layers) panelLayer.enabled = layers;
            panelLayer.colorScale = new Vector4(1, 1, 1, alpha);
        }
    }

    // ---- Desktop / mirror overlay -------------------------------------------------------------

    void BuildScreenOverlay()
    {
        var root = new GameObject("Screen veil", typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        screenGroup = root.GetComponent<CanvasGroup>();
        screenGroup.blocksRaycasts = false; screenGroup.interactable = false;
        var background = Stretch(new GameObject("Black", typeof(Image)), root.transform);
        background.GetComponent<Image>().color = Color.black;
        screenTitle = Label(root.transform, "Title", 72, new Vector2(0, 40));
        screenDetail = Label(root.transform, "Detail", 30, new Vector2(0, -70));
        root.SetActive(false);
    }

    static RectTransform Stretch(GameObject go, Transform parent)
    {
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static TextMeshProUGUI Label(Transform parent, string name, float size, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(PanelPixelsWide - 80, size * 2.6f);
        rect.anchoredPosition = offset;
        // Runtime TMP text takes TMP Settings' default font (CutePixel SDF).
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
        return text;
    }

    // ---- Headset compositor layers ------------------------------------------------------------

    void EnsureCompositorLayers()
    {
        if (cubeLayer != null) return;
        black = new Cubemap(8, TextureFormat.RGBA32, false);
        var pixels = new Color[8 * 8];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black;
        for (int face = 0; face < 6; face++) black.SetPixels(pixels, (CubemapFace)face);
        black.Apply(false, true);
        // Verified over Quest Link (Meta XR SDK 205, OpenXR): the higher compositionDepth draws
        // BEHIND here, contrary to the SDK comment. Cube 0 / panel 1 hid the panel completely.
        cubeLayer = CreateLayer("Veil cubemap", OVROverlay.OverlayShape.Cubemap, black, 1);

        BuildPanelCanvas();
        panelLayer = CreateLayer("Veil panel", OVROverlay.OverlayShape.Quad, panelTexture, 0);
        panelLayer.transform.localScale = new Vector3(PanelWidth, PanelHeight, 1);
        RenderPanel();
    }

    OVROverlay CreateLayer(string name, OVROverlay.OverlayShape shape, Texture texture, int depth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.SetActive(false); // Configure before OVROverlay.Awake/OnEnable.
        var layer = go.AddComponent<OVROverlay>();
        layer.currentOverlayType = OVROverlay.OverlayType.Overlay;
        layer.currentOverlayShape = shape;
        layer.textures = new Texture[] { texture };
        layer.isDynamic = true; // The panel re-renders when its text changes; the cubemap is tiny.
        layer.compositionDepth = depth;
        layer.noDepthBufferTesting = true;
        layer.overridePerLayerColorScaleAndOffset = true;
        layer.colorScale = new Vector4(1, 1, 1, 0);
        layer.enabled = false;
        go.SetActive(true);
        return layer;
    }

    void BuildPanelCanvas()
    {
        panelTexture = new RenderTexture(PanelPixelsWide, PanelPixelsHigh, 24, RenderTextureFormat.ARGB32) { name = "Transition veil panel" };
        panelTexture.Create();
        panelCanvasRoot = new GameObject("Veil panel render");
        panelCanvasRoot.transform.SetParent(transform, false);
        panelCanvasRoot.transform.position = HiddenRenderOrigin;
        int layer = LayerMask.NameToLayer("UI"); if (layer < 0) layer = 0;

        var camGo = new GameObject("Veil panel camera");
        camGo.transform.SetParent(panelCanvasRoot.transform, false);
        camGo.transform.localPosition = new Vector3(0, 0, -1);
        panelCamera = camGo.AddComponent<Camera>();
        panelCamera.enabled = false; // Rendered on demand only.
        panelCamera.orthographic = true; panelCamera.orthographicSize = PanelPixelsHigh / 2f;
        panelCamera.nearClipPlane = .01f; panelCamera.farClipPlane = 2;
        panelCamera.clearFlags = CameraClearFlags.SolidColor; panelCamera.backgroundColor = Color.black;
        panelCamera.cullingMask = 1 << layer;
        panelCamera.targetTexture = panelTexture;

        var canvasGo = new GameObject("Canvas", typeof(Canvas));
        canvasGo.layer = layer;
        canvasGo.transform.SetParent(panelCanvasRoot.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = panelCamera;
        var rect = (RectTransform)canvasGo.transform;
        rect.sizeDelta = new Vector2(PanelPixelsWide, PanelPixelsHigh);
        rect.localPosition = Vector3.zero;
        panelTitle = Label(canvasGo.transform, "Title", 128, new Vector2(0, 80));
        panelDetail = Label(canvasGo.transform, "Detail", 56, new Vector2(0, -110));
        foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        panelCanvasRoot.SetActive(false);
    }

    void RenderPanel()
    {
        if (panelCamera == null) return;
        panelTitle.text = title ?? ""; panelDetail.text = detail ?? "";
        // Active only for this render so no gameplay camera ever sees it.
        panelCanvasRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        panelCamera.Render();
        panelCanvasRoot.SetActive(false);
    }

    static bool TryHeadTrackingPose(out Pose pose)
    {
        pose = Pose.identity;
        if (!OVRNodeStateProperties.GetNodeStatePropertyVector3(XRNode.Head, NodeStatePropertyType.Position,
                OVRPlugin.Node.Head, OVRPlugin.Step.Render, out var position)) return false;
        if (!OVRNodeStateProperties.GetNodeStatePropertyQuaternion(XRNode.Head, NodeStatePropertyType.Orientation,
                OVRPlugin.Node.Head, OVRPlugin.Step.Render, out var rotation)) return false;
        pose = new Pose(position, rotation);
        return true;
    }

    void AnchorPanelInFrontOfHead()
    {
        if (!TryHeadTrackingPose(out var head)) { panelTrackingPose = new Pose(new Vector3(0, 1.5f, PanelDistance), Quaternion.identity); return; }
        var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        var yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
        panelTrackingPose = new Pose(head.position + yaw * Vector3.forward * PanelDistance, yaw);
    }

    // Keeps the panel fixed in tracking space even when the rig warps or the scene swaps cameras:
    // OVROverlay converts world pose to tracking space via the current camera and head pose.
    void PlacePanel()
    {
        if (panelLayer == null || !panelLayer.enabled) return;
        var camera = OVRManager.FindMainCamera();
        if (camera == null || !TryHeadTrackingPose(out var head)) return;
        var trackingToWorldRotation = camera.transform.rotation * Quaternion.Inverse(head.rotation);
        var trackingToWorldPosition = camera.transform.position - trackingToWorldRotation * head.position;
        var t = panelLayer.transform;
        t.SetPositionAndRotation(trackingToWorldPosition + trackingToWorldRotation * panelTrackingPose.position,
            trackingToWorldRotation * panelTrackingPose.rotation);
        t.localScale = Vector3.Scale(new Vector3(PanelWidth, PanelHeight, 1), camera.transform.lossyScale);
    }
}
