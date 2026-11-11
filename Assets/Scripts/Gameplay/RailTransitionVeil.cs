using System.Collections;
using TMPro;
using Unity.Profiling;
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
    // Kept short: the hold and most of the fade-out add directly to the player's wait,
    // and the fade-in delays private-run scene loads (public transfers hide it behind networking).
    const float FadeInSeconds = .15f, FadeOutSeconds = .25f, ReadyHoldSeconds = .175f;
    // Quest 3 profiling: after the world is ready, the first frames that draw the newly visible
    // blocks were GPU-bound for ~1.4 s (52–157 ms frames). Release only once frame times have
    // settled, with a cap so a slow device or editor can never keep the player in the dark.
    const int SettledFramesRequired = 6;
    const float MaxSettleWaitSeconds = 4f;
    const float PanelDistance = 2.2f, PanelWidth = 1.6f, PanelHeight = .8f;
    const int PanelPixelsWide = 1024, PanelPixelsHigh = 512;
    static readonly Vector3 HiddenRenderOrigin = new Vector3(0, -10000, 0);

    static RailTransitionVeil instance;
    public static float Alpha => instance != null ? instance.alpha : 0;
    public static bool Covered => instance != null && instance.alpha >= .999f;
    public static bool Showing => instance != null && instance.latched;

    bool everReady, latched, compositor;
    float alpha, readySince = -1;
    int settledFrames;
    public static float LastReleaseWaitSeconds { get; private set; } = -1;
    // Empty per-frame markers so profiler captures show whether a slow frame was under full cover.
    static readonly ProfilerMarker CoveredMarker = new ProfilerMarker("RailTransitionVeil.Covered");
    static readonly ProfilerMarker FadingMarker = new ProfilerMarker("RailTransitionVeil.Fading");
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

    // Owners that keep the veil up after the world is ready (ExpeditionRenderWarmup).
    static readonly System.Collections.Generic.List<Object> holds = new System.Collections.Generic.List<Object>();
    public static bool Held
    {
        get
        {
            for (int i = holds.Count - 1; i >= 0; i--) if (holds[i] == null) holds.RemoveAt(i);
            return holds.Count > 0;
        }
    }

    /// <summary>Keeps the veil up while an owner prepares the scene. A hold placed before the first
    /// world is ready (late join from Login) covers immediately instead of fading in.</summary>
    public static void SetHold(Object owner, bool hold)
    {
        if (!hold) { holds.Remove(owner); return; }
        if (owner == null) return;
        if (!holds.Contains(owner)) holds.Add(owner);
        if (instance == null || instance.latched) return;
        var session = PhotonSession.Instance;
        if (session == null) return;
        instance.Engage(session);
        instance.alpha = 1;
        instance.Apply();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; holds.Clear(); }
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
        bool held = Held;
        // The login flow has its own status presentation; only veil in-game transitions.
        if (!latched && session != null && ((everReady && session.InWorldTransition) || held)) Engage(session);
        ready &= !held; // A hold delays the ready hold/settle wait until it is released.
#if UNITY_EDITOR
        if (EditorForce && !latched && session != null) Engage(session);
        if (EditorForce) readySince = -1;
#endif
        if (latched)
        {
#if UNITY_EDITOR
            if (EditorForce) ready = false;
#endif
            if (ready)
            {
                if (readySince < 0) { readySince = now; settledFrames = 0; }
                settledFrames = Time.unscaledDeltaTime <= SettledFrameSeconds() ? settledFrames + 1 : 0;
            }
            else { readySince = -1; settledFrames = 0; }
            float waited = readySince >= 0 ? now - readySince : 0;
            if (readySince >= 0 && waited >= ReadyHoldSeconds &&
                (settledFrames >= SettledFramesRequired || waited >= MaxSettleWaitSeconds))
            {
                latched = false;
                LastReleaseWaitSeconds = waited;
                DuckzLog.Log("[TransitionVeil] Released " + waited.ToString("F2") + " s after world ready" +
                    (settledFrames >= SettledFramesRequired ? "." : " (settle cap reached)."));
            }
            if (session != null) UpdateText(session);
        }
        float step = Time.unscaledDeltaTime / (latched ? FadeInSeconds : FadeOutSeconds);
        // Cap the per-frame step so a long stall does not skip the fade in either direction.
        alpha = Mathf.MoveTowards(alpha, latched ? 1 : 0, Mathf.Min(step, .2f));
        if (alpha >= 1f) { CoveredMarker.Begin(); CoveredMarker.End(); }
        else if (alpha > 0f) { FadingMarker.Begin(); FadingMarker.End(); }
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

    // Headset: a frame is settled at no more than two display intervals (27.8 ms at 72 Hz).
    // Desktop/editor frames vary with the monitor and editor overhead, so use a fixed 33 ms.
    float SettledFrameSeconds()
    {
        if (!compositor) return 1f / 30f;
        float hz = OVRPlugin.systemDisplayFrequency;
        if (float.IsNaN(hz) || hz < 30f) hz = 72f;
        return 2f / hz;
    }

    static bool HeadsetPresent()
    {
        if (DesktopPlay.UseDesktop || !XRSettings.isDeviceActive) return false;
        return OVRManager.OVRManagerinitialized && OVRManager.isHmdPresent;
    }

    void UpdateText(PhotonSession session)
    {
        bool held = Held;
        string nextTitle = session.RecoveringConnection ? "RECONNECTING"
            : session.HeadingToExpedition || held ? "ALL ABOARD"
            : GameScenes.IsLoaded(GameScenes.Expedition) ? "HEADING HOME" : "LOADING";
        string nextDetail = held && session.WorldReady && !session.RecoveringConnection ? "Getting the train ready..."
            : session.HeadingToExpedition && !session.RecoveringConnection
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
