#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Photon.Pun;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

/// <summary>One-shot, explicitly armed native-device benchmark. No normal-play hook beyond startup.</summary>
public sealed class RailQuestPerformance : MonoBehaviour
{
    public static float FarClipOverride { get; private set; }
    public static string MotelWingVariant { get; private set; }
    public static string AppliedMotelWingVariant { get; set; }
    public static string MotelFullMapVariant { get; private set; }
    public static string AppliedMotelFullMapVariant { get; set; }
    [Serializable] public sealed class Request
    {
        public int version = 1, seed = 12345;
        public string id, roomCode;
        public int warmupSeconds = 20, compoundSeconds = 20, viewSeconds = 8, timeoutSeconds = 1200;
        public string[] blocks;
        public float farClipMeters; // Zero retains authored cameras; only explicitly armed captures override.
        public string motelWingVariant; // Empty = normal build; explicit control/atlas requires comparison build.
        public string motelFullMapVariant;
        public void Validate()
        {
            if ((!string.IsNullOrEmpty(motelFullMapVariant) && motelFullMapVariant != "control" && motelFullMapVariant != "atlas") ||
                (!string.IsNullOrEmpty(motelFullMapVariant) && !string.IsNullOrEmpty(motelWingVariant)))
                throw new ArgumentException("Invalid or conflicting Motel full-map comparison variant.");
            if (!string.IsNullOrEmpty(motelWingVariant) && motelWingVariant != "control" && motelWingVariant != "atlas")
                throw new ArgumentException("Invalid Motel wing comparison variant.");
            if (!float.IsFinite(farClipMeters) || (farClipMeters != 0 && (farClipMeters < 10 || farClipMeters > 2000)) ||
                version != 1 || !Guid.TryParseExact(id, "N", out _) || roomCode == null ||
                roomCode.Length != 5 || roomCode.Any(c => c < '0' || c > '9') ||
                warmupSeconds < 5 || warmupSeconds > 120 || compoundSeconds < 5 || compoundSeconds > 120 ||
                viewSeconds < 2 || viewSeconds > 60 || timeoutSeconds < 300 || timeoutSeconds > 1800 ||
                (blocks != null && blocks.Length != 0 && (blocks.Length != 4 || blocks.Distinct().Count() != 4)))
                throw new ArgumentException("Invalid benchmark request (requires four distinct blocks or normal seeded route).");
        }
    }
    [Serializable] sealed class Metadata
    {
        public Request request;
        public string utc, build, buildGuid, unity, model, gpu, graphicsApi, quality, xrBackend, route;
        public string scope = "Solo development-build rail-view survey; real XR/AI/physics. Enemy invincibility, full fuel before each leg, scripted throttle and stationary yaw warps. No interior/crew-load acceptance. No clocks/resolution overrides. Buffered capture; final file writing excluded.";
        public int bufferCapacity, bufferBytes, eyeWidth, eyeHeight, msaa;
        public bool development;
        public float renderScale;
        public int overriddenCameraCount;
        public float appliedFarClipMeters;
        public string appliedMotelWingVariant;
        public string appliedMotelFullMapVariant;
    }
    [Serializable] sealed class Status
    {
        public string id, state, segment, message;
        public double elapsed;
        public int frames, validFrames;
        public bool appFocus, paused, xrRunning, vrFocus, inputFocus, userPresent, positionTracked, orientationTracked, headTracked;
        public string sessionPhase, sessionStatus;
        public float trainDistance, trainSpeed, trainFuel;
        public int driveGripActor;
    }
    struct Sample
    {
        public double elapsed;
        public float intervalMs, cpuMs, gpuMs, hz, distance, speed, headX, headY, headZ, yaw, pitch;
        public long mainNs, renderNs, drawCalls, triangles, gcBytes, memory;
        public int segment, valid, dropped, cpuLevel, gpuLevel, foveation;
        public float batteryTemperature, ovrCpuRaw, ovrGpuRaw;
    }
    enum Stage { Login, Warmup, Compound, Private, Starting, Settle, Refuel, Drive, Brake, View }
    const int Capacity = 150000;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    Request request;
    Metadata metadata;
    Sample[] samples;
    int count, validCount, segment, stopIndex, viewIndex;
    readonly List<string> segments = new List<string>();
    readonly List<float> stops = new List<float>();
    readonly List<string> stopNames = new List<string>();
    readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    XRDisplaySubsystem display;
    ProfilerRecorder main, render, draws, triangles, gc, memory;
    Func<string, RailRouteState> oldRoute;
    Func<bool> oldInvincibility;
    Stage stage;
    double began, stageAt, previous, nextStatus, invalidSince = -1;
    string folder, ownedRoom;
    bool finished, hooksInstalled, paused, departureObserved;
    double nextDepartureAttempt, nextTrainAttempt;
    float cachedTemperature;
    int cachedCpu, cachedGpu, cachedFoveation;
    NewPlayer clippedPlayer;
    readonly Dictionary<Camera, float> originalFarClips = new Dictionary<Camera, float>();

    void ApplyCameraOverride()
    {
        if (request.farClipMeters == 0 || !NewPlayer.instance || clippedPlayer == NewPlayer.instance) return;
        clippedPlayer = NewPlayer.instance;
        foreach (var camera in clippedPlayer.GetComponentsInChildren<Camera>(true))
        {
            if (camera.stereoTargetEye == StereoTargetEyeMask.None) continue;
            originalFarClips[camera] = camera.farClipPlane;
            camera.farClipPlane = request.farClipMeters;
        }
        metadata.overriddenCameraCount = originalFarClips.Count;
        metadata.appliedFarClipMeters = request.farClipMeters;
        WriteMetadata();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        FarClipOverride = 0;
        MotelWingVariant = null;
        AppliedMotelWingVariant = null;
        MotelFullMapVariant = null;
        AppliedMotelFullMapVariant = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        string path = Path.Combine(Application.persistentDataPath, "quest-performance-request.json");
        if (!File.Exists(path)) return;
        // Consume before starting: a crash or relaunch cannot re-arm the test.
        string text = File.ReadAllText(path);
        File.Delete(path);
        var go = new GameObject(nameof(RailQuestPerformance));
        DontDestroyOnLoad(go);
        var runner = go.AddComponent<RailQuestPerformance>();
        runner.Begin(text);
#endif
    }
    void Begin(string json)
    {
        try
        {
            request = JsonUtility.FromJson<Request>(json);
            request.Validate();
            FarClipOverride = request.farClipMeters;
            MotelWingVariant = request.motelWingVariant;
            AppliedMotelWingVariant = null;
            MotelFullMapVariant = request.motelFullMapVariant;
            AppliedMotelFullMapVariant = null;
            folder = Path.Combine(Application.persistentDataPath, "QuestPerformance", request.id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "request.json"), json);
            samples = new Sample[Capacity];
            main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
            began = previous = Time.realtimeSinceStartupAsDouble;
            metadata = new Metadata { request = request, utc = DateTime.UtcNow.ToString("o"),
                build = Application.version, buildGuid = Application.buildGUID, unity = Application.unityVersion,
                model = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                development = Debug.isDebugBuild, bufferCapacity = Capacity,
                bufferBytes = System.Runtime.InteropServices.Marshal.SizeOf<Sample>() * Capacity,
                eyeWidth = XRSettings.eyeTextureWidth, eyeHeight = XRSettings.eyeTextureHeight,
                renderScale = XRSettings.eyeTextureResolutionScale, msaa = QualitySettings.antiAliasing };
            WriteMetadata();
            Change(Stage.Login, "login");
        }
        catch (Exception e) { End(false, "Request/startup: " + e.Message); }
    }
    void Update()
    {
        if (finished || request == null || samples == null) return;
        try
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (stage == Stage.View && !string.IsNullOrEmpty(request.motelWingVariant) && AppliedMotelWingVariant != request.motelWingVariant)
                throw new InvalidOperationException("Requested Motel wing variant was not applied; install the comparison build.");
            if (stage == Stage.View && !string.IsNullOrEmpty(request.motelFullMapVariant) && AppliedMotelFullMapVariant != request.motelFullMapVariant)
                throw new InvalidOperationException("Requested Motel full-map variant was not applied; install the full-map comparison build.");
            ApplyCameraOverride();
            Capture(now);
            if (now - began > request.timeoutSeconds) { End(false, "Overall deadline exceeded"); return; }
            if (File.Exists(Path.Combine(Application.persistentDataPath, "quest-performance-cancel-" + request.id)))
            { End(false, "Cancelled by PC"); return; }
            if (now >= nextStatus)
            {
                nextStatus = now + 5;
                cachedTemperature = OVRPlugin.batteryTemperature;
                cachedCpu = OVRPlugin.cpuLevel; cachedGpu = OVRPlugin.gpuLevel;
                cachedFoveation = (int)OVRPlugin.foveatedRenderingLevel;
                WriteStatus("running", "");
            }
            if (stage != Stage.Login && stage != Stage.Private && stage != Stage.Starting)
            {
                if (!Focused()) { if (invalidSince < 0) invalidSince = now; }
                else invalidSince = -1;
                if (invalidSince >= 0 && now - invalidSince > 30) { End(false, "XR focus/tracking absent for 30 seconds"); return; }
            }
            if (hooksInstalled && (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.Name != ownedRoom ||
                !PhotonSession.IsPrivate || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount != 1))
            { End(false, "Private solo room or authority changed"); return; }
            var session = PhotonSession.Instance;
            double age = now - stageAt;
            switch (stage)
            {
                case Stage.Login:
                    if (age > 180) { End(false, "Login/world/focus timeout"); break; }
                    if (session != null && session.WorldReady && !session.HasRoomRequest &&
                        PhotonSession.Phase == RailPhase.Compound && Focused()) Change(Stage.Warmup, "compound-warmup");
                    break;
                case Stage.Warmup:
                    if (age >= request.warmupSeconds) Change(Stage.Compound, "compound");
                    break;
                case Stage.Compound:
                    if (age >= request.compoundSeconds)
                    {
                        session.RequestCreatePrivate(request.roomCode);
                        Change(Stage.Private, "private-room");
                    }
                    break;
                case Stage.Private:
                    if (age > 120) { End(false, "Private room creation timeout: " + session?.LastError); break; }
                    if (session != null && session.WorldReady && !session.HasRoomRequest && PhotonSession.IsPrivate &&
                        PhotonSession.Phase == RailPhase.Staging && PhotonNetwork.IsMasterClient)
                    {
                        if (PhotonNetwork.CurrentRoom.PlayerCount != 1) { End(false, "Expected a new solo room"); break; }
                        ownedRoom = PhotonNetwork.CurrentRoom.Name;
                        // Prevent late join from turning a solo benchmark into uncontrolled crew load.
                        PhotonNetwork.CurrentRoom.IsOpen = false;
                        oldRoute = RailTestTools.OrderedRoute;
                        oldInvincibility = RailTestTools.EnemyInvincibilityRequested;
                        RailTestTools.OrderedRoute = ChooseRoute;
                        RailTestTools.EnemyInvincibilityRequested = () => true;
                        hooksInstalled = true;
                        session.StartPrivateRun();
                        nextDepartureAttempt = now + 1;
                        Change(Stage.Starting, "departure");
                    }
                    break;
                case Stage.Starting:
                    if (age > 150) { End(false, "Departure/world readiness timeout"); break; }
                    // StartPrivateRun uses a shared CAS revision and can be declined while
                    // initial room setup is committing. Retry admission only; never relaunch
                    // a run that actually departed and then returned/failed.
                    if (PhotonSession.Phase == RailPhase.Departing || PhotonSession.Phase == RailPhase.Running)
                        departureObserved = true;
                    if (PhotonSession.Phase == RailPhase.Staging)
                    {
                        if (departureObserved || age > 10) { End(false, "Departure rejected or returned to staging"); break; }
                        if (now >= nextDepartureAttempt && session.WorldReady && !session.HasRoomRequest)
                        { nextDepartureAttempt = now + 1; session.StartPrivateRun(); }
                    }
                    if (PhotonSession.Phase == RailPhase.Running && session.WorldReady &&
                        RailRoute.Instance != null && RailRoute.Instance.Ready && NewPlayer.instance != null)
                    {
                        metadata.route = JsonUtility.ToJson(RailRoute.Instance.Selection);
                        metadata.eyeWidth = XRSettings.eyeTextureWidth; metadata.eyeHeight = XRSettings.eyeTextureHeight;
                        metadata.xrBackend = display?.SubsystemDescriptor.id;
                        WriteMetadata();
                        float distance = RailRoute.StationDepartureLength;
                        foreach (var block in RailRoute.Instance.TravelBlocks)
                        {
                            stops.Add(distance + block.TrackLength * .5f); stopNames.Add(block.id);
                            distance += block.TrackLength;
                        }
                        stops.Add(RailRoute.Instance.FinaleStart + 20); stopNames.Add("finale-approach");
                        RailTestTools.Board();
                        Change(Stage.Settle, "station-warmup");
                    }
                    break;
                case Stage.Settle:
                    if (age >= request.warmupSeconds) StartDrive();
                    break;
                case Stage.Refuel:
                    RequireRunning();
                    if (age > 15) { End(false, "Benchmark refuel was not acknowledged"); break; }
                    if (PhotonSession.CurrentFuel >= 99f && RailTrain.Instance.Speed < .02f)
                    {
                        session.SetTrainMoving(true); nextTrainAttempt = now + 1;
                        Change(Stage.Drive, "travel-to/" + stopNames[stopIndex]);
                    }
                    else if (now >= nextTrainAttempt)
                    { RailTestTools.SetFuel(100); nextTrainAttempt = now + 1; }
                    break;
                case Stage.Drive:
                    RequireRunning();
                    if (age > 180) { End(false, "Train failed to reach next stop"); break; }
                    if (PhotonSession.CurrentFuel <= 0) { End(false, "Benchmark leg exhausted its fuel budget"); break; }
                    if (RailTrain.Instance.Speed < .02f && now >= nextTrainAttempt)
                    { session.SetTrainMoving(true); nextTrainAttempt = now + 1; }
                    // Braking distance at 4 m/s and 2 m/s² is 4 m.
                    if (RailTrain.Instance.Distance >= stops[stopIndex] - 4)
                    { session.SetTrainMoving(false); nextTrainAttempt = now + 1; Change(Stage.Brake, stopNames[stopIndex] + "/braking"); }
                    break;
                case Stage.Brake:
                    RequireRunning();
                    if (age > 15) { End(false, "Train did not stop"); break; }
                    if (RailTrain.Instance.Speed >= .02f && now >= nextTrainAttempt)
                    { session.SetTrainMoving(false); nextTrainAttempt = now + 1; }
                    if (age > 2 && RailTrain.Instance.Speed < .02f)
                    { viewIndex = 0; SetView(); }
                    break;
                case Stage.View:
                    RequireRunning();
                    // First two seconds are separately labelled settling frames in the CSV.
                    if (age >= request.viewSeconds + 2)
                    {
                        if (++viewIndex < 4) SetView();
                        else if (++stopIndex < stops.Count) StartDrive();
                        else End(true, "Completed solo rail-view survey");
                    }
                    break;
            }
        }
        catch (Exception e) { End(false, e.GetType().Name + ": " + e.Message); }
    }
    RailRouteState ChooseRoute(string run)
    {
        var catalog = RailRoute.Instance.catalog;
        return request.blocks == null || request.blocks.Length == 0
            ? RailRoute.ChooseNormal(run, catalog, request.seed)
            : RailRoute.ChooseOrdered(run, request.blocks.Select(id => catalog.Single(b => b.id == id)).ToArray(), request.seed);
    }
    void RequireRunning()
    {
        if (PhotonSession.Phase != RailPhase.Running || RailTrain.Instance == null ||
            NewPlayer.instance == null || RailExpedition.LocalDown)
            throw new InvalidOperationException("Expedition/player no longer running");
    }
    void StartDrive()
    {
        RailTestTools.Board();
        NewPlayer.instance.Warp(RailTrain.Instance.AboardPoint(PhotonNetwork.LocalPlayer.ActorNumber), RailTrain.Instance.transform.rotation);
        // Explicit survey fixture: no manual coal collection. Wait for the room's
        // authoritative refill before GO; SetFuel also stops/rebases train motion.
        RailTestTools.SetFuel(100);
        nextTrainAttempt = Time.realtimeSinceStartupAsDouble + 1;
        Change(Stage.Refuel, stopNames[stopIndex] + "/refuel");
    }
    void SetView()
    {
        NewPlayer.instance.Warp(RailTrain.Instance.AboardPoint(PhotonNetwork.LocalPlayer.ActorNumber),
            RailTrain.Instance.transform.rotation * Quaternion.Euler(0, viewIndex * 90, 0));
        Change(Stage.View, stopNames[stopIndex] + "/yaw-" + viewIndex * 90);
    }
    void Change(Stage value, string label)
    {
        stage = value; stageAt = Time.realtimeSinceStartupAsDouble;
        segment = segments.Count; segments.Add(label);
        WriteStatus("running", "");
    }
    bool Focused() => !paused && Application.isFocused && display != null && display.running &&
        OVRPlugin.hasVrFocus && OVRPlugin.hasInputFocus && OVRPlugin.userPresent &&
        OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter) && OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter);
    void Capture(double now)
    {
        if (count == Capacity) throw new InvalidOperationException("Capture buffer full");
        if (display == null || !display.running)
        {
            SubsystemManager.GetSubsystems(displays);
            display = displays.FirstOrDefault(d => d.running);
        }
        float cpu = float.NaN, gpu = float.NaN, hz = float.NaN;
        int dropped = -1;
        if (display != null && display.running)
        {
            // OpenXR META counters are milliseconds (see installed provider's MetaPerformanceMetricsTest).
            if (UnityEngine.XR.Provider.XRStats.TryGetStat(display, "perfmetrics.appcputime", out float c) && c > 0) cpu = c;
            if (UnityEngine.XR.Provider.XRStats.TryGetStat(display, "perfmetrics.appgputime", out float g) && g > 0) gpu = g;
            if (display.TryGetDisplayRefreshRate(out float h) && h > 0) hz = h;
            if (display.TryGetDroppedFrameCount(out int d)) dropped = d;
        }
        bool valid = Focused();
        var head = NewPlayer.instance != null ? NewPlayer.instance.headCollider.transform : null;
        var position = head != null ? head.position : Vector3.zero;
        samples[count++] = new Sample { elapsed = now - began, intervalMs = (float)((now - previous) * 1000),
            cpuMs = cpu, gpuMs = gpu, hz = hz, dropped = dropped,
            segment = segment, valid = valid ? (stage == Stage.View && now - stageAt < 2 ? 2 : 1) : 0,
            distance = RailTrain.Instance != null ? RailTrain.Instance.Distance : 0,
            speed = RailTrain.Instance != null ? RailTrain.Instance.Speed : 0,
            headX = position.x, headY = position.y, headZ = position.z,
            yaw = head != null ? head.eulerAngles.y : 0, pitch = head != null ? head.eulerAngles.x : 0,
            mainNs = Value(main), renderNs = Value(render), drawCalls = Value(draws), triangles = Value(triangles),
            gcBytes = Value(gc), memory = Value(memory), cpuLevel = cachedCpu, gpuLevel = cachedGpu,
            foveation = cachedFoveation, batteryTemperature = cachedTemperature,
            ovrCpuRaw = OVRPlugin.GetPerfMetricsFloat(OVRPlugin.PerfMetrics.App_CpuTime_Float) ?? float.NaN,
            ovrGpuRaw = OVRPlugin.GetPerfMetricsFloat(OVRPlugin.PerfMetrics.App_GpuTime_Float) ?? float.NaN };
        if (valid) validCount++;
        previous = now;
    }
    static long Value(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;
    void WriteMetadata()
    {
        metadata.appliedMotelWingVariant = AppliedMotelWingVariant;
        metadata.appliedMotelFullMapVariant = AppliedMotelFullMapVariant;
        File.WriteAllText(Path.Combine(folder, "metadata.json"), JsonUtility.ToJson(metadata, true));
    }
    void WriteStatus(string state, string message)
    {
        if (folder == null) return;
        var status = new Status { id = request.id, state = state, segment = segments.Count > segment ? segments[segment] : "startup",
            elapsed = Time.realtimeSinceStartupAsDouble - began, frames = count, validFrames = validCount, message = message,
            appFocus = Application.isFocused, paused = paused, xrRunning = display != null && display.running,
            vrFocus = OVRPlugin.hasVrFocus, inputFocus = OVRPlugin.hasInputFocus, userPresent = OVRPlugin.userPresent,
            positionTracked = OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter),
            orientationTracked = OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter),
            headTracked = InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked,
            sessionPhase = PhotonSession.Phase.ToString(), sessionStatus = PhotonSession.Instance?.Status,
            trainDistance = RailTrain.Instance != null ? RailTrain.Instance.Distance : 0,
            trainSpeed = RailTrain.Instance != null ? RailTrain.Instance.Speed : 0,
            trainFuel = PhotonSession.Instance != null ? PhotonSession.CurrentFuel : 0,
            driveGripActor = PhotonSession.DriveGripActor };
        // Rename ensures the PC never observes a half-written JSON file.
        string path = Path.Combine(folder, "status.json");
        File.WriteAllText(path + ".tmp", JsonUtility.ToJson(status, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
    }
    void End(bool success, string message)
    {
        if (finished) return;
        finished = true;
        try
        {
            if (hooksInstalled)
            {
                RailTestTools.OrderedRoute = oldRoute;
                RailTestTools.EnemyInvincibilityRequested = oldInvincibility;
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == ownedRoom && PhotonNetwork.IsMasterClient)
                    PhotonSession.Instance?.SetTrainMoving(false);
                // End only this fresh benchmark room; do not generate success/reward state.
                PhotonNetwork.Disconnect();
                hooksInstalled = false;
            }
            if (folder != null)
            {
                WriteMetadata();
                WriteStatus("saving", message);
                using (var writer = new StreamWriter(Path.Combine(folder, "frames.csv"), false, new UTF8Encoding(false), 65536))
                {
                    writer.WriteLine("elapsed_s,segment,valid,interval_ms,app_cpu_ms,app_gpu_ms,refresh_hz,dropped_total,main_thread_ns,render_thread_ns,draw_calls,triangles,gc_bytes,unity_memory_bytes,train_distance,train_speed,head_x,head_y,head_z,head_yaw,head_pitch,cpu_level,gpu_level,foveation,battery_temperature,ovr_cpu_raw,ovr_gpu_raw");
                    for (int i = 0; i < count; i++)
                    {
                        var s = samples[i];
                        writer.WriteLine(string.Join(",", s.elapsed.ToString("F6", Invariant), segments[s.segment], s.valid,
                            F(s.intervalMs), F(s.cpuMs), F(s.gpuMs), F(s.hz), s.dropped, s.mainNs, s.renderNs,
                            s.drawCalls, s.triangles, s.gcBytes, s.memory, F(s.distance), F(s.speed),
                            F(s.headX), F(s.headY), F(s.headZ), F(s.yaw), F(s.pitch), s.cpuLevel, s.gpuLevel, s.foveation, F(s.batteryTemperature), F(s.ovrCpuRaw), F(s.ovrGpuRaw)));
                    }
                }
                WriteStatus(success && validCount > 0 ? "complete" : "failed", message);
            }
        }
        finally
        {
            FarClipOverride = 0;
            MotelWingVariant = null;
            AppliedMotelWingVariant = null;
            MotelFullMapVariant = null;
            AppliedMotelFullMapVariant = null;
            foreach (var saved in originalFarClips) if (saved.Key) saved.Key.farClipPlane = saved.Value;
            originalFarClips.Clear();
            main.Dispose(); render.Dispose(); draws.Dispose(); triangles.Dispose(); gc.Dispose(); memory.Dispose();
            samples = null;
            enabled = false;
        }
    }
    static string F(float value) => float.IsNaN(value) || float.IsInfinity(value) ? "" : value.ToString("F4", Invariant);
    void OnApplicationPause(bool value) { paused = value; }
    void OnApplicationQuit() { if (!finished) End(false, "Application quit before completion"); }
    void OnDestroy() { if (!finished) End(false, "Runner destroyed before completion"); }
}
#endif
