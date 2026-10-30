using System;
using UnityEditor;
using UnityEngine;

public static class RailHoldControlChecks
{
    [MenuItem("Duck Rails/Run Hold Button Checks")]
    public static void Run()
    {
        Check(RailHoldControl.IsLive(new object[] { "run", 10.8d }, "run", 10), "Live hold");
        Check(!RailHoldControl.IsLive(new object[] { "run", 10d }, "run", 10), "Expired hold");
        Check(!RailHoldControl.IsLive(new object[] { "old", 10.8d }, "run", 10), "Previous run rejected");
        Check(!RailHoldControl.IsLive(new object[] { "run", 0d }, "run", 10), "Release stops hold");
        Check(!RailHoldControl.IsLive(new object[] { "run", double.NaN }, "run", 10), "Malformed timestamp rejected");
        Check(!RailHoldControl.IsLive(new object[] { "run", 100d }, "run", 10), "Unbounded lease rejected");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/PressableButton.prefab");
        var go = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var button = go.GetComponent<PressableButton>(); int presses = 0, releases = 0;
            button.onPressed.AddListener(() => presses++); button.onReleased.AddListener(() => releases++);
            Check(button.Press() && !button.Press() && presses == 1, "Holding does not repeat press");
            button.Release(); button.Release();
            Check(!button.IsPressed && releases == 1, "Release is immediate and idempotent");
            button.interactable = false; Check(!button.Press(), "Disabled button cannot start hold");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Debug.Log("9 hold-button checks passed. Headset and multi-client checks remain pending.");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
