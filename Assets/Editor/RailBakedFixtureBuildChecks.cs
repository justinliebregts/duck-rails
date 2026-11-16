using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Fail before producing a player with stale authored-light membership.
public sealed class RailBakedFixtureBuildChecks : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Run();

    [MenuItem("Duck Rails/Lighting/Check Player Baked Fixture Metadata")]
    public static void Run()
    {
        int owners = 0, fixtures = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var owner in prefab.GetComponentsInChildren<RailBakedBlockLighting>(true))
            {
                if (string.IsNullOrEmpty(owner.lightingScene)) continue;
                var expected = owner.GetComponentsInChildren<Light>(true).Where(light =>
                    light.lightmapBakeType == LightmapBakeType.Baked &&
                    light.GetComponentInParent<RailBakedBlockLighting>(true) == owner).ToArray();
                using var serialized = new SerializedObject(owner);
                var stored = serialized.FindProperty("bakedFixtures");
                var actual = Enumerable.Range(0, stored.arraySize)
                    .Select(i => stored.GetArrayElementAtIndex(i).objectReferenceValue as Light).ToArray();
                if (actual.Any(light => light == null) || actual.Distinct().Count() != actual.Length ||
                    !expected.ToHashSet().SetEquals(actual))
                    throw new BuildFailedException("Stale baked fixture metadata: " + path +
                        ". CaptureBakedFixtures during lighting export before building.");
                owners++; fixtures += actual.Length;
            }
        }
        Debug.Log("PASS player baked fixture metadata: " + owners + " owners, " + fixtures + " fixtures");
    }
}
