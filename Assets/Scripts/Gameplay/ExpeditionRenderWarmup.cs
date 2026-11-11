using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepares Expedition rendering states behind the transition veil, so the first flashlight,
/// the first helicopter TV feed and blocks that come into view later do not stall on Quest
/// shader/pipeline creation.
///
/// Every distinct render state (shader keywords and blend/cull state, mesh vertex layout,
/// lightmapped or not, instanced or not) is collected from the loaded Expedition, the whole
/// block catalog (including blocks not picked this run) and live flashlight/helmet emission
/// sets. Each state is drawn as a tiny proxy in front of the viewer camera, in batches, twice:
/// once with additional lights hidden from the warm cameras (the normal night state) and once
/// with a temporary unshadowed spot light (the flashlight's URP additional-light variant).
/// A copy of the helicopter feed camera draws the same proxies into a matching render texture.
/// Warmed states are remembered for the process, so later expeditions only prepare new ones.
///
/// Nothing gameplay-related is moved, enabled or networked. Skinned meshes and particles are
/// not covered. Runs once per Expedition scene load, bounded by a timeout.
/// </summary>
public sealed class ExpeditionRenderWarmup : MonoBehaviour
{
    const float RouteWaitSeconds = 20f, WarmSeconds = 15f;
    const int BatchSize = 32, PhaseFrameLimit = 6, GridSide = 8;
    const float ProxySize = .02f, ProxySpread = .03f, ProxyDepth = .2f;
    const int FeedResolution = 256;
    const float FeedFieldOfView = 70f, FeedFarClip = 25f;
    static readonly string[] StateProperties =
        { "_Cull", "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_ZWrite", "_ZTest", "_AlphaToMask" };

    static readonly HashSet<string> warmed = new HashSet<string>();
    static ExpeditionRenderWarmup active;

    /// <summary>Editor checks may turn the warmup off before entering Play Mode.</summary>
    public static bool Enabled = true;
    public static bool Running => active != null;
    public static int WarmedStates => warmed.Count;
    public static int LastNewStates { get; private set; }
    public static float LastSeconds { get; private set; } = -1;
    public static bool LastTimedOut { get; private set; }

    struct State
    {
        public string key;
        public Material material;
        public Mesh mesh;
        public bool lightmapped, instanced, receiveShadows;
        public int layer;
        public LightProbeUsage probes;
        public ReflectionProbeUsage reflections;
    }

    readonly List<State> pending = new List<State>();
    readonly HashSet<string> queued = new HashSet<string>();
    readonly Dictionary<Mesh, string> layouts = new Dictionary<Mesh, string>();
    readonly Dictionary<Material, string> signatures = new Dictionary<Material, string>();
    readonly List<GameObject> proxies = new List<GameObject>();
    readonly List<Light> suppressed = new List<Light>();
    readonly StringBuilder builder = new StringBuilder(256);
    Light[] sceneLights;
    Camera viewer, feed;
    RenderTexture feedTexture;
    Transform root;
    Light warmLight;
    MaterialPropertyBlock instancedBlock;
    bool hasLightmaps, suppressing, subscribed, cleaned;
    int viewerRenders, feedRenders, fresh, batches;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active = null;
        warmed.Clear();
        LastNewStates = 0; LastSeconds = -1; LastTimedOut = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Enabled || scene.name != GameScenes.Expedition || active != null) return;
        var go = new GameObject("Expedition render warmup");
        SceneManager.MoveGameObjectToScene(go, scene);
        active = go.AddComponent<ExpeditionRenderWarmup>();
        // sceneLoaded runs before the scene's first render, so a late joiner never sees it uncovered.
        RailTransitionVeil.SetHold(active, true);
    }

    static double Now => Time.realtimeSinceStartupAsDouble;

    IEnumerator Start()
    {
        double begin = Now;
        double routeDeadline = begin + RouteWaitSeconds;
        while (!RouteReady() && Now < routeDeadline) yield return null;
        double deadline = Now + WarmSeconds;
        CreateRig();
        // First pass: everything already placed plus the whole catalog. Second pass, once the world
        // is ready: cargo and items that arrived with the room snapshot.
        for (int sweep = 0; sweep < 2; sweep++)
        {
            if (sweep == 1)
                while (!WorldReady() && Now < deadline) yield return null;
            Collect(sweep == 0);
            while (pending.Count > 0 && Now < deadline) yield return WarmBatch();
        }
        LastTimedOut = pending.Count > 0 || !WorldReady();
        LastNewStates = fresh;
        LastSeconds = (float)(Now - begin);
        DuckzLog.Log("[ExpeditionWarmup] Prepared " + fresh + " render states in " + batches + " batches, " +
            LastSeconds.ToString("F2") + " s" + (LastTimedOut ? " (stopped at the time limit)" : "") +
            "; " + warmed.Count + " warmed this session.");
        Cleanup();
        Destroy(gameObject);
    }

    static bool RouteReady() => RailRoute.Instance != null && RailRoute.Instance.Ready;
    static bool WorldReady() => PhotonSession.Instance != null && PhotonSession.Instance.WorldReady;

    // ---- Collection ---------------------------------------------------------------------------

    void Collect(bool includeCatalog)
    {
        hasLightmaps = LightmapSettings.lightmaps.Length > 0;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (root != null && renderer.transform.IsChildOf(root)) continue;
            AddRenderer(renderer, renderer.sharedMaterials, IsLightmapped(renderer));
        }

        var emission = new List<KeyValuePair<Renderer, Material[]>>();
        LightEmissionState.AppendStates(emission);
        foreach (var pair in emission)
            AddRenderer(pair.Key, pair.Value, IsLightmapped(pair.Key));

        var route = RailRoute.Instance;
        if (!includeCatalog || route == null || route.catalog == null) return;
        var baked = new HashSet<Renderer>();
        foreach (var block in route.catalog)
        {
            if (block == null) continue;
            // Catalog prefabs are assets: their lightmap bindings are applied only once placed.
            baked.Clear();
            foreach (var lighting in block.GetComponentsInChildren<RailBakedBlockLighting>(true))
                foreach (var binding in lighting.bindings)
                    if (binding.renderer != null) baked.Add(binding.renderer);
            foreach (var renderer in block.GetComponentsInChildren<MeshRenderer>(true))
                AddRenderer(renderer, renderer.sharedMaterials, baked.Contains(renderer));
        }
    }

    static bool IsLightmapped(Renderer renderer) => renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 0xFFFE;

    void AddRenderer(Renderer renderer, Material[] materials, bool lightmapped)
    {
        if (renderer == null || materials == null || !(renderer is MeshRenderer)) return;
        var filter = renderer.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null || mesh.vertexCount == 0 || mesh.subMeshCount == 0) return;
        lightmapped &= hasLightmaps;
        string layout = Layout(mesh);
        foreach (var material in materials)
        {
            if (material == null || material.shader == null) continue;
            AddState(renderer, material, mesh, layout, lightmapped, false);
            // Renderers that drop out of the SRP Batcher (property blocks) draw identical meshes instanced.
            if (material.enableInstancing) AddState(renderer, material, mesh, layout, lightmapped, true);
        }
    }

    void AddState(Renderer renderer, Material material, Mesh mesh, string layout, bool lightmapped, bool instanced)
    {
        string key = Signature(material) + "|" + layout + (lightmapped ? "|L" : "|-") + (instanced ? "I" : "-");
        if (warmed.Contains(key) || !queued.Add(key)) return;
        pending.Add(new State
        {
            key = key, material = material, mesh = mesh, lightmapped = lightmapped, instanced = instanced,
            receiveShadows = renderer.receiveShadows, layer = renderer.gameObject.layer,
            probes = renderer.lightProbeUsage, reflections = renderer.reflectionProbeUsage,
        });
    }

    // Shader + keywords + fixed-function state: what selects the shader variant and pipeline.
    string Signature(Material material)
    {
        if (signatures.TryGetValue(material, out string signature)) return signature;
        builder.Clear().Append(material.shader.name).Append('#');
        string[] keywords = material.shaderKeywords;
        System.Array.Sort(keywords, System.StringComparer.Ordinal);
        foreach (string keyword in keywords) builder.Append(keyword).Append(' ');
        builder.Append('#').Append(material.renderQueue);
        foreach (string property in StateProperties)
            if (material.HasFloat(property)) builder.Append(property).Append('=').Append(material.GetFloat(property));
        signature = builder.ToString();
        signatures.Add(material, signature);
        return signature;
    }

    // Vertex streams and topology: the pipeline's input layout.
    string Layout(Mesh mesh)
    {
        if (layouts.TryGetValue(mesh, out string layout)) return layout;
        builder.Clear();
        foreach (var attribute in mesh.GetVertexAttributes())
            builder.Append((int)attribute.attribute).Append(':').Append((int)attribute.format)
                .Append('x').Append(attribute.dimension).Append('s').Append(attribute.stream).Append(',');
        builder.Append('t').Append((int)mesh.GetTopology(0));
        layout = builder.ToString();
        layouts.Add(mesh, layout);
        return layout;
    }

    // ---- Drawing ------------------------------------------------------------------------------

    void CreateRig()
    {
        root = new GameObject("Warm proxies").transform;
        root.gameObject.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);

        warmLight = new GameObject("Warm flashlight").AddComponent<Light>();
        warmLight.transform.SetParent(root, false);
        warmLight.type = LightType.Spot;
        warmLight.spotAngle = 150f;
        warmLight.range = 1f;
        warmLight.intensity = .01f;
        warmLight.shadows = LightShadows.None;
        warmLight.renderMode = LightRenderMode.ForcePixel;
        warmLight.cullingMask = ~0;
        warmLight.enabled = false;

        // Same setup as HelicopterMirror's security camera, without its face-reveal marker.
        var feedGo = new GameObject("Warm helicopter feed");
        feedGo.transform.SetParent(root, false);
        feed = HelicopterMirror.AddFeedCamera(feedGo, FeedFieldOfView, FeedFarClip);
        feedTexture = HelicopterMirror.NewFeedTexture(FeedResolution);
        feed.targetTexture = feedTexture;

        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        Application.onBeforeRender += FollowViewer;
        subscribed = true;
    }

    IEnumerator WarmBatch()
    {
        if (!FindViewer()) { yield return null; yield break; }
        FollowViewer();
        int count = Mathf.Min(BatchSize, pending.Count);
        float depth = Mathf.Max(viewer.nearClipPlane, feed.nearClipPlane) + ProxyDepth;
        int slot = 0;
        for (int i = 0; i < count; i++)
            for (int copy = pending[i].instanced ? 2 : 1; copy > 0; copy--)
                proxies.Add(CreateProxy(pending[i], slot++, depth));

        bool viewerSeen = true;
        feed.enabled = true;
        for (int phase = 0; phase < 2; phase++)
        {
            // Phase 0: no additional lights (night baseline). Phase 1: one per-pixel spot light.
            suppressing = phase == 0;
            warmLight.enabled = phase == 1;
            if (suppressing) sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            viewerRenders = feedRenders = 0;
            for (int frame = 0; frame < PhaseFrameLimit && (viewerRenders == 0 || feedRenders == 0); frame++)
                yield return null;
            viewerSeen &= viewerRenders > 0;
        }
        suppressing = false;
        sceneLights = null;
        warmLight.enabled = false;
        feed.enabled = false;
        ClearProxies();

        if (viewerSeen)
        {
            for (int i = 0; i < count; i++) warmed.Add(pending[i].key);
            fresh += count;
        }
        pending.RemoveRange(0, count);
        batches++;
    }

    GameObject CreateProxy(State state, int slot, float depth)
    {
        var go = new GameObject("Warm proxy") { hideFlags = HideFlags.DontSave };
        go.layer = (viewer.cullingMask & (1 << state.layer)) != 0 ? state.layer : 0;
        var t = go.transform;
        t.SetParent(root, false);
        Bounds bounds = state.mesh.bounds;
        float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.z, 1e-4f)));
        float scale = ProxySize * .5f / extent;
        var cell = new Vector2(slot % GridSide, slot / GridSide) / (GridSide - 1) - new Vector2(.5f, .5f);
        t.localScale = Vector3.one * scale;
        t.localPosition = new Vector3(cell.x * 2f * ProxySpread, cell.y * 2f * ProxySpread, depth) - bounds.center * scale;

        go.AddComponent<MeshFilter>().sharedMesh = state.mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var materials = new Material[state.mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = state.material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = state.receiveShadows;
        renderer.reflectionProbeUsage = state.reflections;
        renderer.lightProbeUsage = state.lightmapped ? LightProbeUsage.Off : state.probes;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        if (state.lightmapped)
        {
            // Any loaded lightmap selects the lightmapped variant; the picture is never shown.
            renderer.lightmapIndex = 0;
            renderer.lightmapScaleOffset = new Vector4(1, 1, 0, 0);
        }
        if (state.instanced)
        {
            if (instancedBlock == null)
            {
                instancedBlock = new MaterialPropertyBlock();
                instancedBlock.SetFloat("_ExpeditionWarmup", 0f);
            }
            renderer.SetPropertyBlock(instancedBlock);
        }
        return go;
    }

    void ClearProxies()
    {
        foreach (var proxy in proxies) if (proxy != null) Destroy(proxy);
        proxies.Clear();
    }

    bool FindViewer()
    {
        if (viewer != null && viewer.isActiveAndEnabled) return true;
        viewer = null;
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled && main.targetTexture == null) { viewer = main; return true; }
        foreach (var camera in Camera.allCameras)
            if (camera.targetTexture == null && camera.cameraType == CameraType.Game && !HelicopterMirror.IsMirrorCamera(camera)
                && (viewer == null || camera.depth > viewer.depth))
                viewer = camera;
        return viewer != null;
    }

    // Proxies stay unparented from the rig, so a rig replacement can never take them with it.
    void FollowViewer()
    {
        if (root != null && viewer != null)
            root.SetPositionAndRotation(viewer.transform.position, viewer.transform.rotation);
    }

    void LateUpdate() => FollowViewer();

    // URP calls this before culling, so hidden lights neither light nor select the variant.
    void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!suppressing || sceneLights == null || (camera != viewer && camera != feed)) return;
        foreach (var light in sceneLights)
            if (light != null && light != warmLight && light.enabled && light.type != LightType.Directional && !BakedOnly(light))
            {
                light.enabled = false;
                suppressed.Add(light);
            }
    }

    void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == viewer) viewerRenders++;
        else if (camera == feed) feedRenders++;
        RestoreLights();
    }

    static bool BakedOnly(Light light) =>
        light.bakingOutput.isBaked && light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked;

    void RestoreLights()
    {
        foreach (var light in suppressed) if (light != null) light.enabled = true;
        suppressed.Clear();
    }

    void Cleanup()
    {
        if (cleaned) return;
        cleaned = true;
        suppressing = false;
        RestoreLights();
        if (subscribed)
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Application.onBeforeRender -= FollowViewer;
            subscribed = false;
        }
        ClearProxies();
        if (feed != null) feed.targetTexture = null;
        if (feedTexture != null) { feedTexture.Release(); Destroy(feedTexture); }
        if (root != null) Destroy(root.gameObject);
        RailTransitionVeil.SetHold(this, false);
    }

    void OnDestroy()
    {
        Cleanup();
        if (active == this) active = null;
    }
}
