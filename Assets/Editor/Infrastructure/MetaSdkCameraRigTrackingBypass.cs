using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Meta XR's OVRCameraRig writes hand-anchor poses even when the controller is not tracked.
/// That script lives under Library/PackageCache and is restored whenever packages refresh.
/// This re-applies a tracking check on editor load and before DuckZ builds.
/// </summary>
[InitializeOnLoad]
public static class MetaSdkCameraRigTrackingBypass
{
    const string Marker = "DUCKZ_BYPASS_HAND_ANCHOR_TRACKING";
    const string AlreadyPatchedSnippet = "GetControllerPositionTracked(leftActiveController)";

    static readonly Regex OriginalAssignments = new Regex(
        @"leftHandAnchor\.localPosition\s*=\s*OVRInput\.GetLocalControllerPosition\(leftActiveController\)\s*;\s*" +
        @"rightHandAnchor\.localPosition\s*=\s*OVRInput\.GetLocalControllerPosition\(rightActiveController\)\s*;\s*" +
        @"leftHandAnchor\.localRotation\s*=\s*OVRInput\.GetLocalControllerRotation\(leftActiveController\)\s*;\s*" +
        @"rightHandAnchor\.localRotation\s*=\s*OVRInput\.GetLocalControllerRotation\(rightActiveController\)\s*;",
        RegexOptions.Multiline);

    const string ReplacementBlock =
        "// " + Marker + "\n" +
        "                if(OVRInput.GetControllerPositionTracked(leftActiveController)) {\n" +
        "                    leftHandAnchor.localPosition = OVRInput.GetLocalControllerPosition(leftActiveController);\n" +
        "                    leftHandAnchor.localRotation = OVRInput.GetLocalControllerRotation(leftActiveController);\n" +
        "                }\n" +
        "                if(OVRInput.GetControllerPositionTracked(rightActiveController)) {\n" +
        "                    rightHandAnchor.localPosition = OVRInput.GetLocalControllerPosition(rightActiveController);\n" +
        "                    rightHandAnchor.localRotation = OVRInput.GetLocalControllerRotation(rightActiveController);\n" +
        "                }";

    static MetaSdkCameraRigTrackingBypass()
    {
        EditorApplication.delayCall += () => EnsurePatched(logWhenUnchanged: false);
    }

    /// <summary>
    /// Returns true if the PackageCache file was modified (Unity will recompile).
    /// </summary>
    public static bool EnsurePatched(bool logWhenUnchanged = true)
    {
        string path = FindOvrCameraRigPath();
        if (string.IsNullOrEmpty(path))
        {
            if (logWhenUnchanged)
                Debug.LogWarning("MetaSdkCameraRigTrackingBypass: OVRCameraRig.cs not found in PackageCache.");
            return false;
        }

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"MetaSdkCameraRigTrackingBypass: Failed to read {path}: {e.Message}");
            return false;
        }

        if (content.Contains(Marker) || content.Contains(AlreadyPatchedSnippet))
        {
            if (logWhenUnchanged)
                Debug.Log("MetaSdkCameraRigTrackingBypass: Hand anchor tracking check already applied.");
            return false;
        }

        if (!OriginalAssignments.IsMatch(content))
        {
            if (logWhenUnchanged)
                Debug.LogWarning(
                    "MetaSdkCameraRigTrackingBypass: Expected hand-anchor assignments not found. " +
                    "Meta XR SDK may have changed — update the bypass.");
            return false;
        }

        string patched = OriginalAssignments.Replace(content, ReplacementBlock, 1);
        if (patched == content)
        {
            Debug.LogWarning(
                "MetaSdkCameraRigTrackingBypass: Found hand-anchor assignments but failed to patch them.");
            return false;
        }

        try
        {
            File.WriteAllText(path, patched);
        }
        catch (Exception e)
        {
            Debug.LogError($"MetaSdkCameraRigTrackingBypass: Failed to write {path}: {e.Message}");
            return false;
        }

        Debug.Log($"MetaSdkCameraRigTrackingBypass: Patched hand anchor tracking check in:\n{path}");
        AssetDatabase.Refresh();
        return true;
    }

    static string FindOvrCameraRigPath()
    {
        string packageCache = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));
        if (!Directory.Exists(packageCache))
            return null;

        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(packageCache, "com.meta.xr.sdk.core@*");
        }
        catch
        {
            return null;
        }

        foreach (string dir in dirs)
        {
            string candidate = Path.Combine(dir, "Scripts", "OVRCameraRig.cs");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
