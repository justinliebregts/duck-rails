using Game.Animation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

// Cosmetic, local-only observations. Never changes collider, movement or damage state.
[DefaultExecutionOrder(10000)]
public sealed class SpiderScarPool : MonoBehaviour
{
    public const int Capacity = 16, RequestCapacity = 64, RaycastsPerFrame = 2;
    [Min(0.1f)] public float lifetime = 10f;
    [Min(1f)] public float drawDistance = 20f;
    [Tooltip("Fraction of the rendered scar size used for corner fit checks.")]
    [Range(0.1f, 1f)] public float fitFootprintScale = 0.6f;
    public int PlacementRaycasts { get; private set; }
    public int ActiveCount { get; private set; }
    public int DistanceRejected { get; private set; }
    public int SurfaceMisses { get; private set; }
    public int GeometryRejected { get; private set; }
    public int ScarsPlaced { get; private set; }
    public int FitRejected { get; private set; }
    public int FitRaycasts { get; private set; }
    static SpiderScarPool instance;
    static readonly ProfilerMarker Marker = new ProfilerMarker("SpiderScars.Tick");
    readonly Slot[] slots = new Slot[Capacity];
    readonly Request[] requests = new Request[RequestCapacity];
    readonly KillRequest[] killRequests = new KillRequest[Capacity];
    int killHead, killCount;
    int cursor;
    int pendingObservations;
    Camera view;
    readonly System.Random random = new System.Random();
    ParticleSystem.MinMaxCurve prefabSize, prefabRotation;
    readonly Fit[] fits = new Fit[Capacity];
    readonly Rejection[] rejections = new Rejection[Capacity];
    int fitHead, fitCount, rejectionCursor;
    struct Fit
    {
        public Collider collider;
        public SpiderGait owner;
        public Vector3 point, normal, head, right, up;
        public Matrix4x4 matrix;
        public float scale, particleSize, rotation, expiry;
        public int corner;
    }
    struct Rejection
    {
        public SpiderGait owner;
        public Collider collider;
        public Vector3 head;
        public Matrix4x4 matrix;
        public float expiry;
    }
    struct KillRequest
    {
        public Transform owner;
        public Vector3 root, victim;
        public int mask;
        public float expiry;
    }
    struct Request
    {
        public SpiderGait owner;
        public Collider surface;
        public Vector3 root, head;
        public float radius;
        public int frame;
        public int mask;
        public bool pending;
    }
    sealed class Slot
    {
        public Transform transform, surface;
        public ParticleSystemRenderer renderer;
        public ParticleSystem particles;
        public Vector3 localPoint, localNormal;
        public float expiry;
        public Matrix4x4 surfaceMatrix;
        public bool visible;
        public float particleSize, rotation;
    }
    public static SpiderScarPool Get()
    {
        if (instance == null) instance = new GameObject("Spider scar pool").AddComponent<SpiderScarPool>();
        return instance;
    }
    void Awake()
    {
        instance = this;
        var prefab = Resources.Load<GameObject>("Effects/SpiderScarDecalPFX");
        if (prefab == null) { enabled = false; return; }
        var main = prefab.GetComponent<ParticleSystem>().main;
        prefabSize = main.startSize;
        prefabRotation = main.startRotation;
        for (int i = 0; i < Capacity; i++)
        {
            var go = Instantiate(prefab, transform);
            go.name = "Spider scar " + i;
            var particles = go.GetComponent<ParticleSystem>();
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Runtime emits explicitly. The prefab burst is useful for editor
            // preview, but must not replace our validated particle during Simulate.
            var emission = particles.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = false;
            slots[i] = new Slot { transform = go.transform, renderer = renderer, particles = particles };
        }
    }
    // The particle is static: pause its simulation and let the pool own expiry/refresh.
    static void ShowParticle(Slot slot)
    {
        slot.particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        slot.particles.Play(false);
        // Sample rotation once for the fit and apply it to the emitter plane.
        // Explicit particle values prevent the emitter from randomizing again.
        slot.particles.Emit(new ParticleSystem.EmitParams { startSize = slot.particleSize, rotation = 0f }, 1);
        // Emit + Pause alone leaves the billboard render data uninitialized.
        // Advance once, then keep the stationary particle frozen thereafter.
        slot.particles.Simulate(0.001f, false, false, false);
        slot.particles.Pause(false);
    }
    bool NearViewer(Vector3 point, float distanceSq)
    {
        // Both DesktopPlayer and the VR rig bind their actual local head here.
        var drivers = LocalBodyDriver.Active;
        for (int i = 0; i < drivers.Count; i++)
            if (drivers[i] != null && drivers[i].isActiveAndEnabled && drivers[i].HeadAnchor != null
                && (drivers[i].HeadAnchor.position - point).sqrMagnitude <= distanceSq) return true;
        if (drivers.Count == 0 && view != null && (view.transform.position - point).sqrMagnitude <= distanceSq) return true;
#if UNITY_EDITOR
        var scene = UnityEditor.SceneView.lastActiveSceneView;
        if (scene != null && scene.hasFocus && scene.camera != null
            && (scene.camera.transform.position - point).sqrMagnitude <= distanceSq) return true;
#endif
        return false;
    }
    static void SetVisible(Slot slot, bool visible)
    {
        if (slot.visible == visible) return;
        slot.visible = visible;
        slot.renderer.enabled = visible;
    }
    void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
    void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null) return;
        float distanceSq = drawDistance * drawDistance;
        Vector3 eye = camera.transform.position;
        float now = Time.time;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            SetVisible(slot, slot.surface != null && now < slot.expiry
                && slot.surface.gameObject.activeInHierarchy
                && (slot.transform.position - eye).sqrMagnitude <= distanceSq);
        }
    }
    [ContextMenu("Log scar diagnostics")]
    void LogDiagnostics() => DuckzLog.Log($"Spider scars: active={ActiveCount}, placed/refreshed={ScarsPlaced}, "
        + $"raycasts this frame={PlacementRaycasts}, distance rejects={DistanceRejected}, "
        + $"surface misses={SurfaceMisses}, geometry rejects={GeometryRejected}, "
        + $"fit rays={FitRaycasts}, fit rejects={FitRejected}, pending fits={fitCount}", this);
    public void Observe(SpiderGait owner, Collider surface, Vector3 root, Vector3 head, float radius, int mask = 0)
    {
        if (!enabled || radius <= 0f) return;
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        // Reject distant spiders before scanning or occupying the request buffer.
        if (!NearViewer(head, drawDistance * drawDistance)) { DistanceRejected++; return; }
        for (int i = 0; i < fitCount; i++)
            if (fits[(fitHead + i) % Capacity].owner == owner) return;
        for (int i = 0; i < rejections.Length; i++)
        {
            ref var rejected = ref rejections[i];
            if (rejected.owner == owner && Time.time < rejected.expiry && rejected.collider != null
                && (head - rejected.head).sqrMagnitude < 0.01f
                && rejected.collider.transform.localToWorldMatrix == rejected.matrix) return;
        }
        int free = -1;
        for (int i = 0; i < requests.Length; i++)
        {
            if (requests[i].owner == owner) { free = i; break; }
            if (free < 0 && (requests[i].owner == null || requests[i].frame < Time.frameCount - 1)) free = i;
        }
        if (free >= 0)
        {
            if (!requests[free].pending) pendingObservations++;
            requests[free] = new Request {
                owner = owner, surface = surface, root = root, head = head, radius = radius,
                frame = Time.frameCount, mask = mask, pending = true
            };
        }
    }
    public void ObserveKill(SpiderGait owner, Vector3 root, Vector3 victim, int mask)
    {
        if (!enabled || owner == null || mask == 0) return;
        // Unlike pose observations, a kill is a one-shot snapshot. Retain it across
        // frames: KillTrigger runs after this pool and the victim may already move.
        if (killCount == killRequests.Length)
        {
            killHead = (killHead + 1) % killRequests.Length;
            killCount--;
        }
        killRequests[(killHead + killCount++) % killRequests.Length] = new KillRequest {
            owner = owner.transform, root = root, victim = victim, mask = mask, expiry = Time.time + 0.5f
        };
    }
    void LateUpdate()
    {
        using var sample = Marker.Auto();
        PlacementRaycasts = 0;
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        float distanceSq = drawDistance * drawDistance;
        float now = Time.time;
        ActiveCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.surface == null || now >= slot.expiry || !slot.surface.gameObject.activeInHierarchy)
            { SetVisible(slot, false); slot.surface = null; continue; }
            // Walls are usually stationary. Only update the particle transform
            // when its receiving surface actually moves, rotates or scales.
            Matrix4x4 matrix = slot.surface.localToWorldMatrix;
            if (matrix != slot.surfaceMatrix)
            {
                Vector3 point = slot.surface.TransformPoint(slot.localPoint);
                Vector3 normal = slot.surface.TransformDirection(slot.localNormal).normalized;
                slot.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal) * Quaternion.AngleAxis(slot.rotation, Vector3.forward));
                slot.surfaceMatrix = matrix;
            }
            SetVisible(slot, NearViewer(slot.transform.position, distanceSq));
            ActiveCount++;
        }
        ServiceFits(now, distanceSq);
        while (killCount > 0 && PlacementRaycasts < RaycastsPerFrame)
        {
            KillRequest request = killRequests[killHead];
            killRequests[killHead] = default;
            killHead = (killHead + 1) % killRequests.Length;
            killCount--;
            if (now > request.expiry) continue;
            if (!NearViewer(request.victim, distanceSq)) { DistanceRejected++; continue; }
            Vector3 delta = request.root - request.victim;
            float distance = delta.magnitude;
            if (distance < 0.001f) continue;
            PlacementRaycasts++;
            // Cast only between the captured victim and spider positions, from
            // the victim side, so open-air kills cannot mark a wall behind them.
            if (!Physics.Raycast(request.victim, delta / distance, out RaycastHit hit, distance,
                request.mask, QueryTriggerInteraction.Ignore)) { SurfaceMisses++; continue; }
            if (request.owner != null && hit.transform.IsChildOf(request.owner)) continue;
            if (Vector3.Dot(request.victim - hit.point, hit.normal) <= 0f
                || Vector3.Dot(request.root - hit.point, hit.normal) >= -0.02f) { GeometryRejected++; continue; }
            BeginFit(hit, 1f, null, request.victim, now);
        }
        for (int scanned = 0; pendingObservations > 0 && scanned < RequestCapacity && PlacementRaycasts < RaycastsPerFrame; scanned++)
        {
            int index = cursor; cursor = (cursor + 1) % RequestCapacity;
            Request request = requests[index];
            if (!request.pending) continue;
            requests[index] = default;
            pendingObservations--;
            // Only use observations made this frame after the final head correction.
            if (request.owner == null || !request.owner.isActiveAndEnabled || request.frame != Time.frameCount
                || (request.surface == null && request.mask == 0)
                || (request.surface != null && (!request.surface.enabled || !request.surface.gameObject.activeInHierarchy))) continue;
            if (!NearViewer(request.head, distanceSq)) { DistanceRejected++; continue; }
            Vector3 delta = request.head - request.root;
            if (delta.sqrMagnitude < 0.0001f) continue;
            Vector3 direction = delta.normalized;
            float reach = request.radius + 0.05f;
            PlacementRaycasts++;
            // Clearance-disabled spiders have no candidate collider. One bounded
            // world ray finds the exposed face between the head and supported root.
            var ray = new Ray(request.head + direction * reach, -direction);
            RaycastHit hit;
            bool found = request.surface != null
                ? request.surface.Raycast(ray, out hit, reach * 2f)
                : Physics.Raycast(ray, out hit, delta.magnitude + reach, request.mask, QueryTriggerInteraction.Ignore);
            if (!found || hit.transform.IsChildOf(request.owner.transform)) { SurfaceMisses++; continue; }
            float headSide = Vector3.Dot(request.head - hit.point, hit.normal);
            if (headSide < -request.radius * 0.5f || (request.surface != null && headSide > request.radius)
                || Vector3.Dot(request.root - hit.point, hit.normal) >= -0.02f) { GeometryRejected++; continue; }
            BeginFit(hit, Mathf.Clamp(request.radius * 2.4f, 0.2f, 1.2f), request.owner, request.head, now);
        }
    }
    bool RefreshNearby(Transform surface, Vector3 point, Vector3 normal, float now)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.surface == surface && slot.expiry > now
                && (surface.TransformPoint(slot.localPoint) - point).sqrMagnitude < 0.09f
                && Vector3.Dot(surface.TransformDirection(slot.localNormal), normal) > 0.8f)
            { slot.expiry = now + lifetime; return true; }
        }
        return false;
    }
    void BeginFit(RaycastHit hit, float scale, SpiderGait owner, Vector3 head, float now)
    {
        if (RefreshNearby(hit.collider.transform, hit.point, hit.normal, now))
        { ScarsPlaced++; if (owner != null) owner.ScarPlaced(head); return; }
        for (int i = 0; i < fitCount; i++)
        {
            ref var pending = ref fits[(fitHead + i) % Capacity];
            if (pending.collider == hit.collider && (pending.point - hit.point).sqrMagnitude < 0.09f) return;
        }
        if (fitCount == Capacity) return;
        float particleSize = Mathf.Max(0f, prefabSize.Evaluate(0f, (float)random.NextDouble()));
        float rotation = prefabRotation.Evaluate(0f, (float)random.NextDouble()) * Mathf.Rad2Deg;
        Quaternion orientation = Quaternion.LookRotation(hit.normal) * Quaternion.AngleAxis(rotation, Vector3.forward);
        float half = particleSize * scale * 0.5f * Mathf.Clamp(fitFootprintScale, 0.1f, 1f);
        fits[(fitHead + fitCount++) % Capacity] = new Fit {
            collider = hit.collider, owner = owner, head = head, point = hit.point, normal = hit.normal,
            matrix = hit.collider.transform.localToWorldMatrix, scale = scale, particleSize = particleSize,
            rotation = rotation, right = orientation * Vector3.right * half,
            up = orientation * Vector3.up * half, expiry = now + 1f
        };
    }
    void ServiceFits(float now, float distanceSq)
    {
        while (fitCount > 0 && PlacementRaycasts < RaycastsPerFrame)
        {
            ref var fit = ref fits[fitHead];
            bool valid = fit.collider != null && fit.collider.enabled && fit.collider.gameObject.activeInHierarchy
                && now <= fit.expiry && NearViewer(fit.point, distanceSq)
                && fit.collider.transform.localToWorldMatrix == fit.matrix;
            if (valid)
            {
                // Opposite corners first: reject a narrow surface as soon as possible.
                Vector3 offset = fit.corner == 0 ? fit.right + fit.up : fit.corner == 1 ? -fit.right - fit.up
                    : fit.corner == 2 ? -fit.right + fit.up : fit.right - fit.up;
                Vector3 corner = fit.point + offset;
                PlacementRaycasts++; FitRaycasts++;
                valid = fit.collider.Raycast(new Ray(corner + fit.normal * 0.03f, -fit.normal), out RaycastHit hit, 0.06f)
                    && Mathf.Abs(Vector3.Dot(hit.point - corner, fit.normal)) <= 0.015f
                    && Vector3.Dot(hit.normal, fit.normal) >= 0.98f;
                if (valid && ++fit.corner < 4) continue;
            }
            if (valid)
            {
                Place(fit.collider.transform, fit.point, fit.normal, fit.scale, now, fit.particleSize, fit.rotation);
                if (fit.owner != null) fit.owner.ScarPlaced(fit.head);
            }
            else
            {
                FitRejected++;
                rejections[rejectionCursor] = new Rejection {
                    owner = fit.owner, collider = fit.collider, head = fit.head, matrix = fit.matrix, expiry = now + 1f
                };
                rejectionCursor = (rejectionCursor + 1) % Capacity;
            }
            fit = default;
            fitHead = (fitHead + 1) % Capacity;
            fitCount--;
        }
    }
    void Place(Transform surface, Vector3 point, Vector3 normal, float size, float now, float particleSize, float rotation)
    {
        ScarsPlaced++;
        int chosen = -1;
        float oldest = float.PositiveInfinity;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.surface == surface && slot.expiry > now
                && (slot.surface.TransformPoint(slot.localPoint) - point).sqrMagnitude < 0.09f
                && Vector3.Dot(surface.TransformDirection(slot.localNormal), normal) > 0.8f)
            { slot.expiry = now + lifetime; return; }
            if (slot.expiry < oldest) { oldest = slot.expiry; chosen = i; }
        }
        var selected = slots[chosen];
        selected.surface = surface;
        selected.particleSize = particleSize;
        selected.rotation = rotation;
        selected.surfaceMatrix = surface.localToWorldMatrix;
        selected.localPoint = surface.InverseTransformPoint(point + normal * 0.005f);
        selected.localNormal = surface.InverseTransformDirection(normal);
        selected.expiry = now + lifetime;
        selected.transform.SetPositionAndRotation(point + normal * 0.005f, Quaternion.LookRotation(normal) * Quaternion.AngleAxis(rotation, Vector3.forward));
        selected.transform.localScale = Vector3.one * size;
        ShowParticle(selected);
        SetVisible(selected, true);
    }
    void OnDestroy()
    {
        if (instance == this) instance = null;

    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) SetVisible(slots[i], false);
        System.Array.Clear(requests, 0, requests.Length);
        System.Array.Clear(killRequests, 0, killRequests.Length);
        killHead = killCount = 0;
        pendingObservations = 0;
        System.Array.Clear(fits, 0, fits.Length);
        System.Array.Clear(rejections, 0, rejections.Length);
        fitHead = fitCount = rejectionCursor = 0;
    }
}


