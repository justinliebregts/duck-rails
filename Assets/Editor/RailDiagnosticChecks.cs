#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.Animation;
using UnityEditor;
using UnityEngine;

public static class RailDiagnosticChecks
{
    public static string CheckLivePanel()
    {
        if (!EditorApplication.isPlaying || GR.Player.instance == null || RailDiagnosticPanel.Instance == null) throw new Exception("Requires a live local player");
        var panel = RailDiagnosticPanel.Instance;
        if (!panel.IsOpen) panel.Toggle();
        var button = UnityEngine.Object.FindObjectsByType<PressableButton>(FindObjectsSortMode.None).Single(b => b.diagnostic && b.name == "REPORT BUG");
        int before = RailDiagnostics.Instance.SessionReports;
        button.Release(); button.Press(); button.Press();
        if (RailDiagnostics.Instance.SessionReports != before + 1) throw new Exception("Report button did not save exactly once");
        var report = RailDiagnostics.ReadReports().First(r => r.category == "manual" && r.status == "open");
        if (report.snapshot == null || report.history.Length == 0 || report.snapshot.objects == null || report.preset == null) throw new Exception("Report missing evidence");
        // This is a tool validation artifact, not an unresolved game bug. Do not send it to production.
        report.category = "tool-validation"; report.status = "resolved"; report.resolution = "Live Report Bug button saved exactly one complete report"; report.uploaded = true;
        RailDiagnostics.Write(report);
        button.Release();
        return "PASS report button saved " + report.id + " history=" + report.history.Length + " objects=" + report.snapshot.objects.Length;
    }
    [MenuItem("Duck Rails/Run Diagnostic Checks")]
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks++; }
        Check(!RailDiagnostics.Sanitize("Authorization=secret-value").Contains("secret-value"), "Authorization redaction");
        Check(!RailDiagnostics.Sanitize("Bearer secret-value").Contains("secret-value"), "Bearer redaction");
        Check(!RailDiagnostics.Sanitize("account 1234567890123456").Contains("1234567890123456"), "Account redaction");
        Check(!RailDiagnostics.Sanitize(@"C:\Users\Someone\secret.cs").Contains("Someone"), "Local path redaction");
        Check(RailDiagnostics.Sanitize(new string('x', 1000)).Length == 600, "Bounded events");
        var gate = new RailDiagnosticDebounce();
        Check(!gate.Observe("ownership", 0, 5), "Ownership handoff receives grace");
        Check(!gate.Observe("ownership", 4, 5), "Short transient is not reported");
        gate.Keep(Array.Empty<string>());
        Check(!gate.Observe("ownership", 6, 5), "Recovery resets continuous-failure clock");
        Check(gate.Observe("ownership", 11, 5), "Persistent violation reported");
        Check(!gate.Observe("ownership", 30, 5), "Persistent error deduplicated");
        gate.Reset(); Check(gate.Observe("ownership", 31, 0), "New session can report recurrence");
        var queueReport = new RailDiagnosticReport { localOwner = "account-a" };
        Check(RailDiagnostics.CanUpload(queueReport, "account-a"), "Originating account can upload its report");
        Check(!RailDiagnostics.CanUpload(queueReport, "account-b"), "Other client cannot upload the report");
        Check(!RailDiagnostics.CanUpload(queueReport, ""), "Unsigned client cannot claim queued report");
        Check(RailDiagnostics.UploadPayload(queueReport).localOwner == "" && queueReport.localOwner == "account-a", "Local routing identity removed from upload without mutating queue");
        queueReport.uploaded = true; Check(!RailDiagnostics.CanUpload(queueReport, "account-a"), "Completed uploads are skipped");
        var report = new RailDiagnosticReport { id = Guid.NewGuid().ToString("N"), category = "fixture", created = DateTime.UtcNow.ToString("O"), summary = "Diagnostic storage check" };
        try
        {
            RailDiagnostics.Write(report);
            Check(RailDiagnostics.ReadReports().Any(r => r.id == report.id && r.status == "open"), "Report persists as open");
            report.status = "resolved"; report.resolution = "Fixture verified"; RailDiagnostics.Write(report);
            Check(RailDiagnostics.ReadReports().Single(r => r.id == report.id).resolution == "Fixture verified", "Resolution persists");
            report.id = "../escape";
            bool rejected = false; try { RailDiagnostics.Write(report); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Path traversal rejected");
        }
        finally
        {
            foreach (var r in RailDiagnostics.ReadReports().Where(r => r.category == "fixture" && r.created == report.created)) File.Delete(Path.Combine(RailDiagnostics.Folder, r.id + ".json"));
        }
        var previousPlayer = GR.Player.instance;
        var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"));
        var root = new GameObject("Arm button fixture");
        try
        {
            var hand = rig.GetComponent<GR.Player>().rightPlayerHand;
            GR.Player.instance = rig.GetComponent<GR.Player>();
            var left = rig.GetComponent<LocalBodyDriver>().Body.LeftHand;
            root.transform.SetParent(left, false); root.transform.localPosition = new Vector3(0, .13f, -.12f);
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(.18f, .048f, .025f); collider.isTrigger = true;
            var button = root.AddComponent<PressableButton>(); button.diagnostic = true;
            var fingers = rig.GetComponent<LocalBodyDriver>().Body.RightHand.GetComponentsInChildren<SphereCollider>(true);
            var finger = fingers[0];
            foreach (var other in fingers.Skip(1)) other.enabled = false;
            finger.transform.position = root.transform.position - finger.transform.TransformVector(finger.center);
            Check(hand.TouchesButton(collider), "DuckPlayer right finger touches left-arm report button");
            root.transform.Rotate(20, 40, 60);
            finger.transform.position = root.transform.position - finger.transform.TransformVector(finger.center);
            Check(hand.TouchesButton(collider), "Arm rotation keeps fingertip contact");
            int presses = 0; button.onPressed.AddListener(() => presses++);
            button.SendMessage("Awake"); // Edit-mode fixtures do not receive MonoBehaviour lifecycle callbacks.
            button.SendMessage("LateUpdate"); button.SendMessage("LateUpdate");
            Check(presses == 1 && button.IsPressed, "Actual DuckPlayer touch invokes one button action");
            Check(!button.Press() && presses == 1, "Held press produces one action");
            button.Release(); Check(button.Press() && presses == 2, "Release permits next action");
            root.SetActive(false); Check(!button.Press(), "Hidden panel cannot press");
            finger.transform.position += Vector3.up;
            Check(!hand.TouchesButton(collider), "Withdrawal ends contact");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(rig); GR.Player.instance = previousPlayer; }
        Debug.Log("DIAGNOSTICS: " + checks + " checks passed");
    }
}
#endif
