using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fixed security camera on the helicopter, drawn onto the square screen this component sits on.
/// Aim the Camera mount. It is a normal camera, so the picture does not follow the player's head.
/// The screen stays off until the local head is within range, checked twice a second,
/// and even then the camera only redraws every other frame.
/// The local face is restored only while this camera draws, same as the LCK selfie preview.
/// Other players and held items are ordinary renderers, so they come along in that draw.
/// </summary>
[DefaultExecutionOrder(-100)]
public class HelicopterMirror : MonoBehaviour
{
    public static bool AnyActive => _activeCount > 0;

    static int _activeCount;
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [Tooltip("Edge length of the screen texture. 256 is the Quest 2 budget.")]
    [SerializeField] int resolution = 256;
    [Tooltip("Redraw every Nth frame while the head is near the screen. 2 keeps a Quest 2 feed cheap.")]
    [SerializeField] int renderInterval = 2;
    [Tooltip("Metres. How far the security camera can see.")]
    [SerializeField] float farClip = 25f;
    [Tooltip("Head distance that turns the screen on. Checked twice a second.")]
    [SerializeField] float maxViewDistance = 10f;
    [Tooltip("Vertical field of view of the security camera.")]
    [SerializeField] float fieldOfView = 70f;
    [Tooltip("Where the security camera sits and which way it looks. Move this, not the screen.")]
    [SerializeField] Transform cameraMount;

    Camera _camera;
    MeshRenderer _screen;
    RenderTexture _texture;
    Material _screenMaterial;
    bool _screenShown;
    bool _haveFrame;
    bool _rendering;
    bool _near;
    float _nextProbe;

    const float ProbeInterval = 0.5f;

    public static bool IsMirrorCamera(Camera camera)
    {
        return camera != null && camera.TryGetComponent<HelicopterMirrorCamera>(out _);
    }

    void Awake()
    {
        _screen = GetComponent<MeshRenderer>();
        if (_screen != null)
        {
            _screen.enabled = false;
            _screen.shadowCastingMode = ShadowCastingMode.Off;
            _screen.receiveShadows = false;
        }

        CreateCamera();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        SetRendering(false);
        SetScreenShown(false);
        _haveFrame = false;
        _near = false;
        _nextProbe = 0f;
    }

    void OnDestroy()
    {
        if (_camera != null)
            _camera.targetTexture = null;
        if (_texture != null)
            _texture.Release();
        if (_screenMaterial != null)
            Destroy(_screenMaterial);
    }

    void LateUpdate()
    {
        if (Time.time >= _nextProbe)
        {
            _nextProbe = Time.time + ProbeInterval;
            Transform head = Viewer();
            _near = head != null && InRange(head);
        }

        if (!_near)
        {
            _haveFrame = false;
            SetScreenShown(false);
            SetRendering(false);
            return;
        }

        // First frame draws immediately. After that, every other frame is enough at 72Hz.
        bool render = !_haveFrame || (Time.frameCount % Mathf.Max(1, renderInterval)) == 0;
        if (render)
            _haveFrame = true;

        SetScreenShown(true);
        SetRendering(render);
    }

    static Transform Viewer()
    {
        var drivers = Game.Animation.LocalBodyDriver.Active;
        for (int i = 0; i < drivers.Count; i++)
        {
            Game.Animation.LocalBodyDriver driver = drivers[i];
            if (driver != null && driver.HeadAnchor != null)
                return driver.HeadAnchor;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera)
            return;

        ApplyMount();
        // The screen mesh is the picture itself. Hide it so the feed cannot see its own quad.
        SetSurfaceEnabled(false);
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera)
            return;

        SetSurfaceEnabled(_screenShown);
    }

    bool InRange(Transform viewer)
    {
        return (viewer.position - transform.position).sqrMagnitude <= maxViewDistance * maxViewDistance;
    }

    void ApplyMount()
    {
        if (_camera == null)
            return;

        Transform mount = Mount();
        if (mount != null && mount != _camera.transform)
            _camera.transform.SetPositionAndRotation(mount.position, mount.rotation);

        _camera.fieldOfView = fieldOfView > 1f ? fieldOfView : 70f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = farClip;
        _camera.aspect = 1f;
        _camera.ResetProjectionMatrix();
    }

    Transform Mount()
    {
        if (cameraMount != null)
            return cameraMount;

        if (transform.parent != null)
        {
            Transform found = transform.parent.Find("SecurityCamera");
            if (found != null)
                return found;
        }

        return null;
    }

    void SetRendering(bool render)
    {
        if (_camera == null || _rendering == render)
            return;

        _rendering = render;
        _camera.enabled = render;
        if (render)
            _activeCount++;
        else
            _activeCount = Mathf.Max(0, _activeCount - 1);
    }

    void SetScreenShown(bool shown)
    {
        if (_screenShown == shown)
            return;

        _screenShown = shown;
        SetSurfaceEnabled(shown);
    }

    void SetSurfaceEnabled(bool enabled)
    {
        if (_screen != null)
            _screen.enabled = enabled;
    }

    void CreateCamera()
    {
        Transform mount = Mount();
        if (mount == null)
        {
            var mountGo = new GameObject("SecurityCamera");
            if (transform.parent != null)
                mountGo.transform.SetParent(transform.parent, false);
            mountGo.transform.SetPositionAndRotation(transform.position, transform.rotation);
            mount = mountGo.transform;
            cameraMount = mount;
        }

        // A scaled parent skews a camera. The mount stays put; the camera copies its pose.
        var go = new GameObject("SecurityCameraEye");
        go.transform.SetParent(mount.parent, false);
        go.transform.SetPositionAndRotation(mount.position, mount.rotation);

        _camera = go.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.clearFlags = CameraClearFlags.Skybox;
        _camera.backgroundColor = new Color(0.45f, 0.55f, 0.65f, 1f);
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = farClip;
        _camera.fieldOfView = fieldOfView > 1f ? fieldOfView : 70f;
        _camera.aspect = 1f;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.useOcclusionCulling = false;
        _camera.stereoTargetEye = StereoTargetEyeMask.None;
        // UI stays out. Environment is back in, along with ducks and held items.
        // Eye overlays sit on their own layer so this outside view does not draw them.
        _camera.cullingMask = ~(1 << 5);
        int eyeOverlay = LayerMask.NameToLayer("EyeOverlay");
        if (eyeOverlay >= 0)
            _camera.cullingMask &= ~(1 << eyeOverlay);
        _camera.depth = -50f;

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.allowXRRendering = false;

        go.AddComponent<HelicopterMirrorCamera>();

        _texture = NewTexture();
        _camera.targetTexture = _texture;

        Material source = _screen != null ? _screen.sharedMaterial : null;
        Shader shader = source != null ? source.shader : Shader.Find("Duckz/TvScreen");
        _screenMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (source != null)
            _screenMaterial.CopyPropertiesFromMaterial(source);
        _screenMaterial.SetTexture(BaseMapId, _texture);
        if (_screen != null)
            _screen.sharedMaterial = _screenMaterial;
    }

    RenderTexture NewTexture()
    {
        int edge = Mathf.Clamp(resolution, 64, 512);
        var texture = new RenderTexture(edge, edge, 16, RenderTextureFormat.Default);
        texture.name = "HelicopterScreen";
        texture.useMipMap = false;
        texture.autoGenerateMips = false;
        texture.filterMode = FilterMode.Bilinear;
        texture.Create();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, new Color(0.45f, 0.55f, 0.65f, 1f));
        RenderTexture.active = previous;
        return texture;
    }

    void OnDrawGizmos()
    {
        Transform mount = cameraMount;
        if (mount == null && transform.parent != null)
            mount = transform.parent.Find("SecurityCamera");
        if (mount == null)
            return;

        Gizmos.matrix = Matrix4x4.TRS(mount.position, mount.rotation, Vector3.one);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawFrustum(Vector3.zero, fieldOfView > 1f ? fieldOfView : 70f, Mathf.Min(farClip, 8f), 0.05f, 1f);
    }
}

/// <summary>Marks the security camera so the local face can be shown for that draw only.</summary>
public sealed class HelicopterMirrorCamera : MonoBehaviour
{
}
