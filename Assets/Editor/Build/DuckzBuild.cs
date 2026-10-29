using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Version is major.minor.patch; minor and patch stay under 100 so the bundle code always goes up.</summary>
public static class DuckzVersion
{
    public static bool TryParse(string version, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        string[] parts = (version ?? "").Trim().Split('.');
        return parts.Length == 3
            && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor) && int.TryParse(parts[2], out patch)
            && major >= 0 && major < 210000 && minor >= 0 && minor < 100 && patch >= 0 && patch < 100
            && major + minor + patch > 0;
    }

    public static int BundleCode(int major, int minor, int patch) => major * 10000 + minor * 100 + patch;

    /// <summary>Patches share a Photon version, so 0.1.2 and 0.1.3 can play together.</summary>
    public static string PhotonVersion(int major, int minor) => $"{major}.{minor}";
}

// Runs for every build, not just ones started from the window.
public class DuckzBuildVersioning : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string version = PlayerSettings.bundleVersion;
        if (!DuckzVersion.TryParse(version, out int major, out int minor, out int patch))
            throw new BuildFailedException($"Version '{version}' must be major.minor.patch, with minor and patch under 100.");

        PlayerSettings.Android.bundleVersionCode = DuckzVersion.BundleCode(major, minor, patch);
        var photon = PhotonNetwork.PhotonServerSettings;
        photon.AppSettings.AppVersion = DuckzVersion.PhotonVersion(major, minor);
        EditorUtility.SetDirty(photon);
        AssetDatabase.SaveAssetIfDirty(photon);
        Debug.Log($"Building {version} (bundle code {PlayerSettings.Android.bundleVersionCode}, Photon version {photon.AppSettings.AppVersion}).");
    }
}

public class DuckzBuildWindow : EditorWindow
{
    const string OutputFolderPref = "DuckzBuild.OutputFolder";
    const string DevelopmentPref = "DuckzBuild.Development";
    // EditorPrefs live in this PC's registry, never in the project.
    const string KeystorePasswordPref = "DuckzBuild.KeystorePassword";
    const string Keystore = "user.keystore";
    // The store listing was first uploaded with Unity's debug key, so every update must keep using it.
    const string KeyAlias = "androiddebugkey";

    string version;
    string outputFolder;
    bool development;
    string keystorePassword;

    [MenuItem("Tools/DuckZ/Build/Open Build Window")]
    static void Open() => GetWindow<DuckzBuildWindow>("DuckZ Build");

    void OnEnable()
    {
        version = PlayerSettings.bundleVersion;
        outputFolder = EditorPrefs.GetString(OutputFolderPref, @"X:\Work\builds\duckz");
        development = EditorPrefs.GetBool(DevelopmentPref, false);
        keystorePassword = EditorPrefs.GetString(KeystorePasswordPref, "");
    }

    static bool IsAndroid => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;

    void OnGUI()
    {
        EditorGUILayout.LabelField("Platform", EditorUserBuildSettings.activeBuildTarget.ToString());
        version = EditorGUILayout.TextField("Version", version);
        bool valid = DuckzVersion.TryParse(version, out int major, out int minor, out int patch);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Bundle Code", valid ? DuckzVersion.BundleCode(major, minor, patch).ToString() : "-");
            EditorGUILayout.TextField("Photon Version", valid ? DuckzVersion.PhotonVersion(major, minor) : "-");
        }
        if (!valid)
            EditorGUILayout.HelpBox("Use major.minor.patch, with minor and patch under 100 (e.g. 0.1.3).", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string picked = EditorUtility.OpenFolderPanel("Build Output Folder", outputFolder, "");
                if (!string.IsNullOrEmpty(picked)) outputFolder = picked;
            }
        }
        development = EditorGUILayout.Toggle("Development Build", development);
        if (IsAndroid)
            keystorePassword = EditorGUILayout.PasswordField("Keystore Password", keystorePassword);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(OutputFolderPref, outputFolder);
            EditorPrefs.SetBool(DevelopmentPref, development);
            EditorPrefs.SetString(KeystorePasswordPref, keystorePassword);
        }

        bool signable = !IsAndroid || (File.Exists(Keystore) && !string.IsNullOrEmpty(keystorePassword));
        if (IsAndroid && !File.Exists(Keystore))
            EditorGUILayout.HelpBox($"{Keystore} is missing from the project folder.", MessageType.Error);
        else if (!signable)
            EditorGUILayout.HelpBox("Enter the keystore password (remembered on this PC only).", MessageType.Warning);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!valid || !signable || string.IsNullOrWhiteSpace(outputFolder)))
        {
            if (GUILayout.Button("Build", GUILayout.Height(32)))
                EditorApplication.delayCall += () => Build(major, minor, patch);
        }
    }

    void Build(int major, int minor, int patch)
    {
        // PackageCache copies of OVRCameraRig revert when packages refresh, so re-apply before building.
        // A fresh patch forces a recompile, which cannot finish in the middle of this build.
        if (MetaSdkCameraRigTrackingBypass.EnsurePatched())
        {
            EditorUtility.DisplayDialog(
                "Meta SDK Patch Applied",
                "Patched Meta XR PackageCache scripts that were restored.\n\n" +
                "Unity is recompiling scripts — click Build again once that finishes.",
                "OK");
            return;
        }

        PlayerSettings.bundleVersion = version.Trim();
        if (IsAndroid)
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(Keystore);
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasPass = keystorePassword;
        }
        AssetDatabase.SaveAssets();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("DuckZ Build: no enabled scenes in Build Settings.");
            return;
        }

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string extension = target == BuildTarget.Android ? ".apk" : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
        string fileName = $"duckz_{PlayerSettings.bundleVersion}_{DuckzVersion.BundleCode(major, minor, patch)}{(development ? "_dev" : "")}{extension}";
        Directory.CreateDirectory(outputFolder);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputFolder, fileName),
            target = target,
            options = development ? BuildOptions.Development : BuildOptions.None,
        });

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"DuckZ Build: {fileName} ({report.summary.totalSize / (1024 * 1024)} MB) in {report.summary.totalTime:mm\\:ss}.");
            EditorUtility.RevealInFinder(report.summary.outputPath);
        }
        else
        {
            Debug.LogError($"DuckZ Build: {report.summary.result} with {report.summary.totalErrors} error(s).");
        }
    }
}
