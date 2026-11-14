using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RailQuestPerformanceBuild
{
    public const string Output = "output/quest-performance/duckrails-quest-profile.apk";
    [MenuItem("Duck Rails/Tests/Quest Performance/Build Device Runner")]
    public static void Build()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets")
            throw new InvalidOperationException("Wrong Unity project");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Requires idle Android editor");
        if (MetaSdkCameraRigTrackingBypass.EnsurePatched())
            throw new InvalidOperationException("Meta patch applied; wait for compile and build again");
        DuckzSigningPasswords.RestoreEmptySessionPasswords();
        string error = DuckzBuildWindow.GetSigningError();
        if (error != null) throw new InvalidOperationException(error);
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        string status = Path.Combine(Path.GetDirectoryName(Output), "build-status.txt");
        File.WriteAllText(status, "building " + DateTime.UtcNow.ToString("o"));
        bool oldDevelopment = EditorUserBuildSettings.development;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Output, target = BuildTarget.Android,
                // Comparison catalogue substitutions depend on explicit editor scope,
                // which Unity's incremental scene cache does not track as a dependency.
                options = BuildOptions.Development | BuildOptions.CleanBuildCache });
            File.WriteAllText(status, report.summary.result + " errors=" + report.summary.totalErrors +
                " duration=" + report.summary.totalTime + " bytes=" + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Quest build failed; inspect Console");
        }
        catch (Exception e) { File.WriteAllText(status, "failed " + e.Message); throw; }
        finally
        {
            EditorUserBuildSettings.development = oldDevelopment;
            EditorUserBuildSettings.buildAppBundle = oldBundle;
        }
    }
    [MenuItem("Duck Rails/Tests/Quest Performance/Check Request Guard")]
    public static void Check()
    {
        var valid = new RailQuestPerformance.Request { id = Guid.NewGuid().ToString("N"), roomCode = "58302" };
        valid.Validate();
        valid.blocks = new [] { "a", "b", "c", "d" }; valid.Validate();
        valid.farClipMeters = 50; valid.Validate();
        int rejected = 0;
        void Reject(Action mutation) { var r = new RailQuestPerformance.Request { id = Guid.NewGuid().ToString("N"), roomCode = "58302" }; current = r; mutation(); try { r.Validate(); } catch (ArgumentException) { rejected++; } }
        Reject(() => current.id = "../escape");
        Reject(() => current.roomCode = "abcde");
        Reject(() => current.timeoutSeconds = 0);
        Reject(() => current.viewSeconds = 0);
        Reject(() => current.blocks = new [] { "a", "a", "c", "d" });
        Reject(() => current.blocks = new [] { "a" });
        Reject(() => current.farClipMeters = float.NaN);
        Reject(() => current.farClipMeters = -1);
        Reject(() => current.farClipMeters = 5);
        Reject(() => current.farClipMeters = 2001);
        if (rejected != 10) throw new Exception("Request guard failed");
        var failures = LoggingPolicyRules.CheckSource("RailQuestPerformance.cs", File.ReadAllText("Assets/Scripts/Gameplay/RailQuestPerformance.cs"));
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Debug.Log("Quest performance: valid requests, ten invalid requests and runtime logging policy pass. No gameplay run.");
    }
    static RailQuestPerformance.Request current;
}
