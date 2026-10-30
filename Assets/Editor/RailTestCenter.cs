#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Multiplayer.PlayMode;

/// <summary>One entry point for existing checks, live fixtures, and persistent reports.</summary>
public sealed class RailTestCenter : EditorWindow
{
    Vector2 scroll;
    string resolution = "", message = "";
    RailDiagnosticReport[] local = Array.Empty<RailDiagnosticReport>();
    RemoteReport[] remote = Array.Empty<RemoteReport>();
    UnityWebRequest request;
    string requestKind;
    double nextInbox;
    [Serializable] public class RemoteReport { public string id, created, category, summary, build, platform, status, resolution, payload; }
    [Serializable] class Inbox { public RemoteReport[] reports; public int total; }
    [Serializable] class Envelope { public Inbox data; }
    [Serializable] class DetailEnvelope { public RemoteReport data; }
    [Serializable] class Change { public string status, resolution; }
    [MenuItem("Duck Rails/Test Center")]
    public static void Open() => GetWindow<RailTestCenter>("Duck Rails Tests");
    void OnEnable() { local = RailDiagnostics.ReadReports(); nextInbox = 0; }
    void OnDisable() { request?.Abort(); request?.Dispose(); request = null; }
    void Update()
    {
        if (EditorApplication.timeSinceStartup > nextInbox && request == null)
        { nextInbox = EditorApplication.timeSinceStartup + 60; local = RailDiagnostics.ReadReports(); FetchInbox(); }
        if (request == null || !request.isDone) return;
        try
        {
            if (request.result != UnityWebRequest.Result.Success) message = "Backend inbox unavailable (HTTP " + request.responseCode + "). Local reports remain available.";
            else if (requestKind == "list")
            {
                var inbox = JsonUtility.FromJson<Envelope>(request.downloadHandler.text)?.data;
                remote = inbox?.reports ?? Array.Empty<RemoteReport>();
                message = (inbox?.total ?? 0) + " open backend reports";
                Directory.CreateDirectory(RailDiagnostics.Folder);
                File.WriteAllText(Path.Combine(RailDiagnostics.Folder, "backend-inbox.txt"), request.downloadHandler.text);
            }
            else if (requestKind == "detail")
            {
                var data = JsonUtility.FromJson<DetailEnvelope>(request.downloadHandler.text)?.data;
                if (data != null)
                {
                    string path = Path.Combine(RailDiagnostics.Folder, data.id + ".backend.txt");
                    File.WriteAllText(path, data.payload); EditorUtility.RevealInFinder(path);
                }
            }
            else { message = "Report status updated"; nextInbox = 0; }
        }
        catch (Exception e) { message = "Inbox response error: " + e.GetType().Name; }
        finally { request.Dispose(); request = null; Repaint(); }
    }
    void FetchInbox() => Send("GET", "reports?status=open", null, "list");
    void Send(string method, string path, object body, string kind)
    {
        if (request != null) return;
        string token = Environment.GetEnvironmentVariable("DUCK_RAILS_ADMIN_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            string config = Path.GetFullPath(Path.Combine(Application.dataPath, "../.env.local"));
            if (File.Exists(config)) foreach (string line in File.ReadAllLines(config))
                if (line.TrimStart().StartsWith("ADMIN_API_TOKEN=", StringComparison.Ordinal)) token = line.Substring(line.IndexOf('=') + 1).Trim().Trim('"', '\'');
        }
        if (string.IsNullOrEmpty(token)) { message = "Backend inbox: set ADMIN_API_TOKEN in the project root .env.local, or DUCK_RAILS_ADMIN_TOKEN in the editor environment."; return; }
        request = new UnityWebRequest("https://duckrails.gunraidersapi.com/api/v1/diagnostics/admin/" + path, method);
        request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
        request.SetRequestHeader("Authorization", "Bearer " + token);
        if (body != null) { request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body))); request.SetRequestHeader("Content-Type", "application/json"); }
        requestKind = kind; request.SendWebRequest();
    }
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("Duck Rails Test Center", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Automated desktop checks. VR feel, voice intelligibility and eight-player acceptance still need real playtests.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || RailTestRunner.Running || !CurrentPlayer.IsMainEditor))
        {
            if (GUILayout.Button("Quick regression — game rules and VR contact fixtures")) RailTestRunner.Quick();
            if (GUILayout.Button("Manual sandbox — one private client, P opens test panel")) RailTestRunner.Start("sandbox");
            if (GUILayout.Button("Two clients — coal grab/release and master handoff")) RailTestRunner.Start("coal");
            if (GUILayout.Button("Three clients — public sessions and network recovery")) RailTestRunner.Start("stability");
            if (GUILayout.Button("Three clients — expedition, shared bag and replay")) RailTestRunner.Start("expedition");
        }
        GUILayout.Label(RailTestRunner.Status, EditorStyles.wordWrappedLabel);
        if (RailTestRunner.Running && GUILayout.Button("Stop test and restore setup")) RailTestRunner.Stop("Cancelled by user");
        if (GUILayout.Button("Open test results and local reports")) { Directory.CreateDirectory(RailDiagnostics.Folder); EditorUtility.RevealInFinder(RailDiagnostics.Folder); }
        GUILayout.Space(12); GUILayout.Label("Reproduction presets", EditorStyles.boldLabel);
        if (GUILayout.Button("Load preset or report for next private run"))
        {
            string path = EditorUtility.OpenFilePanel("Load reproduction preset", RailDiagnostics.Folder, "");
            if (!string.IsNullOrEmpty(path)) try
            {
                string json = File.ReadAllText(path); var report = JsonUtility.FromJson<RailDiagnosticReport>(json);
                RailTestTools.LoadPreset(report?.preset ?? JsonUtility.FromJson<RailTestPreset>(json)); message = RailTestTools.Status;
            } catch (Exception e) { message = "Cannot load preset: " + e.Message; }
        }
        if (GUILayout.Button("Save current route preset") && EditorApplication.isPlaying) RailTestTools.SavePreset();
        if (GUILayout.Button("Restore normal random routes")) RailTestTools.ClearPreset();
        GUILayout.Space(12); GUILayout.Label("Open diagnostic reports", EditorStyles.boldLabel);
        GUILayout.Label(message, EditorStyles.wordWrappedLabel);
        if (GUILayout.Button("Refresh inbox")) { local = RailDiagnostics.ReadReports(); FetchInbox(); }
        resolution = EditorGUILayout.TextField("Resolution / verification", resolution);
        foreach (var report in local.Where(r => r.status == "open"))
        {
            GUILayout.Label("LOCAL " + report.id.Substring(0, 8) + " · " + report.category + " · " + (report.uploaded ? "uploaded" : "pending upload"));
            GUILayout.Label(report.summary, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Inspect " + report.id.Substring(0, 8))) EditorUtility.RevealInFinder(Path.Combine(RailDiagnostics.Folder, report.id + ".json"));
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolution)))
                if (GUILayout.Button("Resolve local " + report.id.Substring(0, 8))) { report.status = "resolved"; report.resolution = resolution; RailDiagnostics.Write(report); }
        }
        foreach (var report in remote)
        {
            GUILayout.Label("BACKEND " + report.id + " · " + report.category, EditorStyles.boldLabel);
            GUILayout.Label(report.summary + "\n" + report.build + " · " + report.platform, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Download evidence " + report.id)) Send("GET", "reports/" + report.id, null, "detail");
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolution)))
                if (GUILayout.Button("Resolve backend " + report.id)) Send("POST", "reports/" + report.id + "/status", new Change { status = "resolved", resolution = resolution }, "resolve");
        }
        EditorGUILayout.EndScrollView();
    }
}

[InitializeOnLoad]
public static class RailTestRunner
{
    [Serializable] sealed class Run { public string suite, scenario, started, folder, previousPreset; public bool quick, stopRequested, hadPreset; public string state; }
    static string Root => Path.GetDirectoryName(Application.dataPath);
    static string StatePath => Path.Combine(Root, "Temp", "rails-test-center.json");
    static Run run;
    static double nextTick;
    public static string Status { get; private set; } = "Ready";
    public static bool Running => run != null;
    static RailTestRunner()
    {
        if (!CurrentPlayer.IsMainEditor) return;
        try { if (File.Exists(StatePath)) run = JsonUtility.FromJson<Run>(File.ReadAllText(StatePath)); } catch (Exception) { }
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode && run != null) Restore(); };
    }
    public static void Quick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var rows = new List<string>();
        foreach (var check in new Action[] { RailSessionChecks.Run, RailDesktopChecks.Run, RailQuickStartChecks.Run,
            RailWeek2Checks.Run, RailWeek3Checks.Run, RailDriveChecks.Run, RailButtonTouchChecks.Run, RailHoldControlChecks.Run,
            RailCoalLiveChecks.CheckAvatarLookup, RailDiagnosticChecks.Run })
        {
            try { check(); rows.Add("PASS " + check.Method.DeclaringType.Name); }
            catch (Exception e) { rows.Add("FAIL " + check.Method.DeclaringType.Name + ": " + e.Message); }
        }
        Directory.CreateDirectory(RailDiagnostics.Folder);
        File.WriteAllLines(Path.Combine(RailDiagnostics.Folder, "quick-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), rows);
        Status = string.Join("\n", rows); Debug.Log(Status);
    }
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string ScenarioState => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";
    static UnityEngine.Object Selected()
    {
        if (Manager == null) throw new InvalidOperationException("Open the Unity multiplayer scenario toolbar first");
        using (var so = new SerializedObject(Manager)) return so.FindProperty("m_Config")?.objectReferenceValue;
    }
    static void Select(string path)
    {
        if (Manager == null) throw new InvalidOperationException("Multiplayer scenario manager unavailable");
        // Unity 6000.3 exposes these members on an internal type. Use its setter so
        // the scenario lifecycle receives ConfigAssetChanged, not a raw field edit.
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        if (property == null) throw new InvalidOperationException("Unsupported Unity scenario selection API");
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
    }
    static void PlayCommand(string command)
    {
        var method = Manager?.GetType().GetMethod(command, Type.EmptyTypes);
        if (method == null) throw new InvalidOperationException("Unsupported Unity multiplayer " + command + " API");
        method.Invoke(Manager, null);
    }
    public static void Start(string suite)
    {
        if (Running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !CurrentPlayer.IsMainEditor) return;
        try
        {
            if (suite != "sandbox" && (!MppmStandaloneCredentials.TryGet("Player2", out _) || suite != "coal" && !MppmStandaloneCredentials.TryGet("Player3", out _)))
                throw new InvalidOperationException("Configure distinct Player2/Player3 accounts in the existing account window first");
            string selected = AssetDatabase.GetAssetPath(Selected());
            run = new Run { suite = suite, scenario = selected, quick = RailEditorQuickStart.Enabled,
                started = DateTime.UtcNow.ToString("O"), folder = Path.Combine(RailDiagnostics.Folder, "test-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")), state = "launching" };
            string presetPath = Path.Combine(RailDiagnostics.Folder, "active-preset.txt");
            run.hadPreset = File.Exists(presetPath); run.previousPreset = run.hadPreset ? File.ReadAllText(presetPath) : null;
            Directory.CreateDirectory(run.folder); Directory.CreateDirectory(Path.GetDirectoryName(StatePath)); Save();
            if (suite != "sandbox") RailTestTools.ClearPreset();
            if (suite == "expedition")
            {
                var catalog = Directory.GetFiles("Assets/DuckRails/Blocks", "*.prefab").Where(p => !p.Contains("bridge-finale"))
                    .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/')).GetComponent<RailBlock>()).ToArray();
                int seed = 12345;
                while (!RailRoute.Choose("fixture", catalog, seed).blocks.Any(id => catalog.First(b => b.id == id).track.Any(p => Mathf.Abs(p.x) > 1))) seed++;
                RailTestTools.LoadPreset(new RailTestPreset { seed = seed, scenario = "Three-client expedition", roles = "main, Player2, Player3" });
            }
            Disarm();
            Select(suite == "sandbox" ? "Assets/Settings/PlayMode/5 Diagnostic Solo.asset" : suite == "coal" ? "Assets/Settings/PlayMode/4 Diagnostic Two Client.asset" : "Assets/Settings/PlayMode/3 Week 2 Test.asset");
            RailEditorQuickStart.Enabled = suite == "coal";
            foreach (string path in Sources(suite)) if (File.Exists(path)) File.Copy(path, Path.Combine(run.folder, "previous-" + Path.GetFileName(path)), true);
            foreach (string path in Sources(suite)) if (File.Exists(path)) File.WriteAllText(path, "");
            if (suite == "coal") RailCoalLiveChecks.Arm();
            else if (suite == "stability") RailStabilityLiveChecks.Arm();
            else if (suite == "expedition") RailWeek3LiveChecks.Arm();
            Status = "Launching " + suite;
            PlayCommand("Start");
        }
        catch (Exception e) { Status = "Could not start: " + e.Message; if (run != null) Stop(Status); }
    }
    static string[] Sources(string suite) => suite == "sandbox" ? Array.Empty<string>() : suite == "coal" ? new[] { Root + "/Temp/rails-coal-main.txt", Root + "/Temp/rails-coal-remote.txt" } :
        suite == "stability" ? new[] { Root + "/Temp/rails-stability/main.txt", Root + "/Temp/rails-stability/player2.txt", Root + "/Temp/rails-stability/player3.txt" } :
        new[] { Root + "/Temp/rails-week3-main.txt", Root + "/Temp/rails-week3-player2.txt", Root + "/Temp/rails-week3-player3.txt" };
    static void Save() => File.WriteAllText(StatePath, JsonUtility.ToJson(run));
    static void Tick()
    {
        if (run == null || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 1;
        if (run.state == "stopping") { FinishStop(); return; }
        try
        {
            if (run.suite == "sandbox")
            {
                Status = "Manual private sandbox — P opens the test panel. Stop here when finished.";
                var session = PhotonSession.Instance;
                if (run.state != "sandbox-ready" && session != null && session.WorldReady && !session.HasRoomRequest)
                {
                    if (!PhotonSession.IsPrivate) session.RequestCreatePrivate(UnityEngine.Random.Range(10000, 90000).ToString());
                    else if (PhotonSession.Phase == RailPhase.Staging) session.StartPrivateRun();
                    else if (PhotonSession.Phase == RailPhase.Running) { run.state = "sandbox-ready"; Save(); }
                }
                return;
            }
            var sources = Sources(run.suite); var texts = sources.Select(p => File.Exists(p) ? File.ReadAllText(p) : "").ToArray();
            for (int i = 0; i < sources.Length; i++) File.WriteAllText(Path.Combine(run.folder, Path.GetFileName(sources[i])), texts[i]);
            string success = run.suite == "coal" ? "PASS stopped, moving" : run.suite == "stability" ? "PASS: four public" : "PASS: route, curve";
            Status = run.suite + "\n" + string.Join("\n", texts.Select((t,i) => Path.GetFileName(sources[i]) + ": " + (t.Split('\n').LastOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "Waiting for client")));
            if (texts.Any(t => t.Contains("FAIL ") || t.Contains("FAIL:")))
            {
                string failure = texts.SelectMany(t => t.Split('\n')).First(t => t.Contains("FAIL ") || t.Contains("FAIL:"));
                Stop("FAIL — " + failure); return;
            }
            if (texts.All(t => t.Contains(success))) { Stop("PASS — all " + sources.Length + " clients completed " + run.suite); return; }
            if (DateTime.UtcNow - DateTime.Parse(run.started).ToUniversalTime() > TimeSpan.FromMinutes(run.suite == "stability" ? 40 : 15)) Stop("FAIL — test timeout");
        }
        catch (IOException) { Status = "Waiting for test evidence file"; }
    }
    public static void Stop(string result)
    {
        if (run == null) return;
        Status = result; File.WriteAllText(Path.Combine(run.folder, "result.txt"), result + "\n" + run.started + "\n" + Application.unityVersion);
        if (result.StartsWith("FAIL", StringComparison.Ordinal)) RailDiagnostics.Instance?.Capture("automated-test", result + " (" + run.suite + ")");
        run.state = "stopping"; Save(); Disarm(); RailTestTools.RestoreNetwork();
        FinishStop();
    }
    static void FinishStop()
    {
        if (ScenarioState == "Running")
        {
            if (!run.stopRequested) { run.stopRequested = true; Save(); PlayCommand("Stop"); }
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
    }
    static void Disarm()
    {
        Directory.CreateDirectory(Root + "/Temp");
        RailCoalLiveChecks.Disarm(); RailWeek3LiveChecks.Disarm(); RailStabilityLiveChecks.Disarm(); RailMultiClientCleanupChecks.Disarm(); RailWeek2LiveChecks.Disarm();
    }
    static void Restore()
    {
        if (run == null) return;
        if (ScenarioState != "NotRunning" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (run.state != "stopping") File.WriteAllText(Path.Combine(run.folder, "result.txt"), "CANCELLED — Play Mode ended before all clients passed");
        Disarm(); RailEditorQuickStart.Enabled = run.quick;
        if (run.suite != "sandbox")
        {
            RailTestTools.ClearPreset();
            if (run.hadPreset) File.WriteAllText(Path.Combine(RailDiagnostics.Folder, "active-preset.txt"), run.previousPreset);
        }
        Select(run.scenario); run = null; if (File.Exists(StatePath)) File.Delete(StatePath);
    }
}
#endif
