using System;
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

    public const string PhotonSuffix = "_dra2";

    /// <summary>Patches share a Photon version, so 0.1.2 and 0.1.3 can play together. The suffix keeps Duck Rails out of DuckZ rooms.</summary>
    public static string PhotonVersion(int major, int minor) => $"{major}.{minor}{PhotonSuffix}";
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

/// <summary>
/// The PC spectator: a Windows player with DUCKZ_SPECTATOR defined (an invisible free camera that joins
/// live rooms; see Assets/Scripts/Spectator). Release by default; Development adds the on-screen console.
/// Build it from the same commit as the live Quest build: it only sees rooms with the same Photon version.
/// </summary>
public static class DuckzSpectatorBuild
{
    public const string Define = "DUCKZ_SPECTATOR";
    public const string ExeName = "DuckRailsSpectator.exe";

    /// <summary>
    /// True while the spectator player builds. The block atlas guards (Rail*AtlasProduction) compare
    /// fingerprints that include per-platform asset dependency hashes recorded for Android, so they
    /// cannot pass on Windows; they skip for this build only and still guard every Quest build.
    /// </summary>
    public static bool InProgress { get; private set; }

    public static string FolderName(bool development)
    {
        DuckzVersion.TryParse(PlayerSettings.bundleVersion, out int major, out int minor, out int patch);
        return $"duckrails_spectator_{PlayerSettings.bundleVersion}_{DuckzVersion.BundleCode(major, minor, patch)}{(development ? "_dev" : "")}";
    }

    /// <summary>Builds to outputFolder/duckrails_spectator_[version]_[code][_dev]/DuckRailsSpectator.exe. The active platform must be Windows.</summary>
    public static BuildReport Build(string[] scenes, string outputFolder, bool development = false)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new BuildFailedException("Switch the platform to Windows before building the spectator.");
        if (!DuckzVersion.TryParse(PlayerSettings.bundleVersion, out _, out _, out _))
            throw new BuildFailedException($"Version '{PlayerSettings.bundleVersion}' must be major.minor.patch.");

        // A Windows player needs its own folder for the _Data folder and DLLs beside the exe.
        string folder = Path.Combine(outputFolder, FolderName(development));
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);

        BuildReport report;
        InProgress = true;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = new[] { Define },
            });
        }
        finally { InProgress = false; }

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"DuckZ Build: spectator {folder} ({report.summary.totalSize / (1024 * 1024)} MB) in {report.summary.totalTime:mm\\:ss}.");
        else
            Debug.LogError($"DuckZ Build: spectator {report.summary.result} with {report.summary.totalErrors} error(s).");
        return report;
    }

    public static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

    [MenuItem("Tools/DuckZ/Build/Build PC Spectator (Windows)")]
    static void BuildFromMenu()
    {
        string output = EditorPrefs.GetString(DuckzBuildWindow.OutputFolderPref, @"X:\Work\builds\duckz");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            EditorUtility.DisplayDialog("PC Spectator", "Switch the platform to Windows (File > Build Profiles) before building the spectator.", "OK");
            return;
        }
        BuildReport report = Build(EnabledScenes(), output, EditorPrefs.GetBool(DuckzBuildWindow.DevelopmentPref, false));
        if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(report.summary.outputPath);
    }

    /// <summary>
    /// Batch entry: Unity -batchmode -projectPath . -buildTarget Win64 -executeMethod DuckzSpectatorBuild.BuildFromCommandLine
    /// -spectatorOutput "path" [-spectatorDevelopment] -quit -logFile build.log
    /// </summary>
    public static void BuildFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-spectatorOutput");
        string output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds");
        bool development = args.Contains("-spectatorDevelopment");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            Debug.LogError("DuckZ Build: could not switch to Windows for the spectator.");
            EditorApplication.Exit(1);
            return;
        }
        if (MetaSdkCameraRigTrackingBypass.EnsurePatched())
        {
            Debug.LogError("DuckZ Build: Meta SDK PackageCache patch re-applied; run the spectator build again after it recompiles.");
            EditorApplication.Exit(2);
            return;
        }
        BuildReport report = Build(EnabledScenes(), output, development);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}

public class DuckzBuildWindow : EditorWindow
{
    public const string OutputFolderPref = "DuckzBuild.OutputFolder";
    public const string DevelopmentPref = "DuckzBuild.Development";
    const string SpectatorPref = "DuckzBuild.Spectator";

    string version;
    string outputFolder;
    bool development;
    bool spectator;

    [MenuItem("Tools/DuckZ/Build/Open Build Window")]
    static void Open() => GetWindow<DuckzBuildWindow>("DuckZ Build");

    void OnEnable()
    {
        version = PlayerSettings.bundleVersion;
        outputFolder = EditorPrefs.GetString(OutputFolderPref, @"X:\Work\builds\duckz");
        development = EditorPrefs.GetBool(DevelopmentPref, false);
        spectator = EditorPrefs.GetBool(SpectatorPref, false);
        DuckzSigningPasswords.RestoreEmptySessionPasswords();
    }

    static bool IsAndroid => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
    static bool IsWindows => EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64;

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
        if (IsWindows)
            spectator = EditorGUILayout.Toggle(new GUIContent("Spectator (PC)",
                "Builds the PC spectator (free camera, no duck). Build it from the same commit as the live Quest build."), spectator);
        else
            EditorGUILayout.HelpBox("Switch the platform to Windows to build the PC spectator.", MessageType.None);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(OutputFolderPref, outputFolder);
            EditorPrefs.SetBool(DevelopmentPref, development);
            EditorPrefs.SetBool(SpectatorPref, spectator);
        }

        if (IsAndroid)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Keystore", PlayerSettings.Android.keystoreName);
                EditorGUILayout.TextField("Key Alias", PlayerSettings.Android.keyaliasName);
            }
            EditorGUI.BeginChangeCheck();
            string keystorePass = EditorGUILayout.PasswordField("Keystore Password", PlayerSettings.Android.keystorePass);
            string keyaliasPass = EditorGUILayout.PasswordField("Key Password", PlayerSettings.Android.keyaliasPass);
            if (EditorGUI.EndChangeCheck())
            {
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
                DuckzSigningPasswords.Save(keystorePass, keyaliasPass);
            }
            EditorGUILayout.HelpBox("Signing passwords are remembered on this computer for this project and are not committed.", MessageType.Info);
        }
        string signingError = IsAndroid ? GetSigningError() : null;
        bool signable = signingError == null;
        if (!signable)
            EditorGUILayout.HelpBox(signingError, MessageType.Error);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!valid || !signable || string.IsNullOrWhiteSpace(outputFolder)))
        {
            if (GUILayout.Button("Build", GUILayout.Height(32)))
                EditorApplication.delayCall += () => Build(major, minor, patch);
        }
    }

    public static string GetSigningError()
    {
        if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
            return "Select a custom keystore in Player Settings > Android > Publishing Settings.";
        string keystore = PlayerSettings.Android.keystoreName;
        string path = Path.IsPathRooted(keystore) ? keystore : Path.Combine(Path.GetDirectoryName(Application.dataPath), keystore);
        if (!File.Exists(path))
            return $"Configured keystore is missing: {path}";
        if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
            return "Select a key alias in Player Settings > Android > Publishing Settings.";
        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            return "Enter both signing passwords.";
        return null;
    }

    void Build(int major, int minor, int patch)
    {
        string signingError = IsAndroid ? GetSigningError() : null;
        if (signingError != null)
        {
            Debug.LogError("DuckZ Build: " + signingError);
            return;
        }
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
        AssetDatabase.SaveAssets();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("DuckZ Build: no enabled scenes in Build Settings.");
            return;
        }

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        if (spectator && target == BuildTarget.StandaloneWindows64)
        {
            BuildReport spectatorReport = DuckzSpectatorBuild.Build(scenes, outputFolder, development);
            if (spectatorReport.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(spectatorReport.summary.outputPath);
            return;
        }

        string extension = target == BuildTarget.Android ? ".apk" : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
        string fileName = $"duckrails_{PlayerSettings.bundleVersion}_{DuckzVersion.BundleCode(major, minor, patch)}{(development ? "_dev" : "")}{extension}";
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

/// <summary>
/// Remembers Android signing passwords in gitignored UserSettings. Unity clears the session fields on restart.
/// The old shared Duckz EditorPrefs password is not used.
/// </summary>
static class DuckzSigningPasswords
{
    const string StoreFileName = "build-window-passwords.json";
    const string LegacyFileName = "password.txt";

    [Serializable]
    class Store
    {
        public string keystorePass;
        public string keyaliasPass;
    }

    [InitializeOnLoadMethod]
    static void RestoreOnLoad()
    {
        try
        {
            RestoreEmptySessionPasswords();
        }
        catch (Exception)
        {
            Debug.LogWarning("DuckZ Build: could not restore the signing passwords.");
        }
    }

    public static void RestoreEmptySessionPasswords()
    {
        bool needKeystore = string.IsNullOrEmpty(PlayerSettings.Android.keystorePass);
        bool needAlias = string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass);
        if (!needKeystore && !needAlias)
            return;

        string directory = SigningDirectory();
        string storePath = Path.Combine(directory, StoreFileName);
        if (File.Exists(storePath))
        {
            ApplyStore(storePath, needKeystore, needAlias);
            return;
        }

        string legacyPath = Path.Combine(directory, LegacyFileName);
        if (!File.Exists(legacyPath))
            return;
        string shared = ReadFirstPassword(legacyPath);
        if (string.IsNullOrEmpty(shared))
            return;
        if (needKeystore)
            PlayerSettings.Android.keystorePass = shared;
        if (needAlias)
            PlayerSettings.Android.keyaliasPass = shared;
    }

    public static void Save(string keystorePass, string keyaliasPass)
    {
        try
        {
            string directory = SigningDirectory();
            Directory.CreateDirectory(directory);
            var store = new Store
            {
                keystorePass = keystorePass ?? "",
                keyaliasPass = keyaliasPass ?? "",
            };
            File.WriteAllText(Path.Combine(directory, StoreFileName), JsonUtility.ToJson(store));
        }
        catch (Exception)
        {
            Debug.LogWarning("DuckZ Build: could not save the signing passwords on this computer.");
        }
    }

    static void ApplyStore(string path, bool needKeystore, bool needAlias)
    {
        Store store;
        try
        {
            store = JsonUtility.FromJson<Store>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            Debug.LogWarning("DuckZ Build: could not read the saved signing passwords.");
            return;
        }
        if (store == null)
            return;
        if (needKeystore)
            PlayerSettings.Android.keystorePass = store.keystorePass ?? "";
        if (needAlias)
            PlayerSettings.Android.keyaliasPass = store.keyaliasPass ?? "";
    }

    /// <summary>password.txt is the key backup. Use its first password and never write the file.</summary>
    static string ReadFirstPassword(string path)
    {
        try
        {
            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0)
                        return trimmed;
                }
            }
        }
        catch (Exception)
        {
            Debug.LogWarning("DuckZ Build: could not read the local signing password backup.");
        }
        return "";
    }

    static string SigningDirectory()
    {
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings", "AndroidSigning");
    }
}
