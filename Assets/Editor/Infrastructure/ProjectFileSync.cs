using System;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

// "Cursor (internal)" doesn't write .csproj/.sln files, so `dotnet build Assembly-CSharp.csproj` has nothing to build.
// This runs the Visual Studio package's SDK-style generator whenever scripts or assembly definitions change.
// Skipped when the external editor is Visual Studio, VS Code or Rider, since they already generate them.
public class ProjectFileSync : AssetPostprocessor
{
    const string GeneratorType = "Microsoft.Unity.VisualStudio.Editor.SdkStyleProjectGeneration, Unity.VisualStudio.Editor";
    const string SessionKey = "ProjectFileSync.Synced";

    [InitializeOnLoadMethod]
    static void SyncOncePerSession()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () => Sync(false);
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsProjectFile))
            Sync(false);
    }

    static bool IsProjectFile(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);

    [MenuItem("Tools/DuckZ/Maintenance/Regenerate Project Files")]
    static void SyncFromMenu() => Sync(true);

    static void Sync(bool force)
    {
        if (!force && CodeEditor.CurrentEditor.GetType().Name != "DefaultExternalCodeEditor")
            return;

        Type type = Type.GetType(GeneratorType);
        if (type == null)
        {
            Debug.LogWarning("ProjectFileSync: com.unity.ide.visualstudio isn't installed, so project files can't be generated.");
            return;
        }

        object generator = Activator.CreateInstance(type);
        type.GetMethod("Sync")?.Invoke(generator, null);
        if (force)
            Debug.Log("ProjectFileSync: project files regenerated.");
    }
}
