using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class LoggingPolicyBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) => Run();

    [MenuItem("Tools/DuckZ/Validation/Runtime Logging")]
    public static void Run()
    {
        var failures = LoggingPolicyRules.CheckProject(Path.GetDirectoryName(Application.dataPath));
        if (failures.Count > 0)
            throw new BuildFailedException("Runtime logging policy failed:\n" + string.Join("\n", failures));
        Debug.Log("Runtime logging policy passed.");
    }
}
