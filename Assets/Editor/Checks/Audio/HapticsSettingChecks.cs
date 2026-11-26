using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode checks for Settings > Controls > Haptics: the prefab row (CutePixel SDF text, On/Off buttons,
/// tidy spacing), the saved per-user preference, and VibrationManager ignoring every request while off.
/// Uses a throwaway preference scope and restores the previous user, value and manager afterwards.
/// Immediate vibrations are not fired here because they reach OVRInput; delayed requests prove the gate.
/// </summary>
public static class HapticsSettingChecks
{
    const string PrefabPath = "Assets/Prefabs/PauseMenu/PauseMenu.prefab";
    const string FontPath = "Assets/Fonts/CutePixel SDF.asset";
    const string TestUser = "haptics-setting-check";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Duck Rails/Tests/Haptics Setting (Edit Mode)")]
    public static void Run() => Debug.Log(RunChecks());

    /// <summary>Throws on the first failure; returns the PASS summary.</summary>
    public static string RunChecks()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Run haptics setting checks in Edit Mode.");
        var passed = new List<string>();
        CheckPrefabRow(passed);

        var userField = typeof(UserPlayerPrefs).GetField("currentUserId", PrivateStatic);
        var enabledField = typeof(VibrationManager).GetField("hapticsEnabled", PrivateStatic);
        Require(userField != null && enabledField != null, "Private state fields not found");
        object savedUser = userField.GetValue(null);
        object savedEnabled = enabledField.GetValue(null);
        var savedInstance = VibrationManager.instance;
        var host = new GameObject("Haptics check manager") { hideFlags = HideFlags.HideAndDontSave };
        AudioClip clip = AudioClip.Create("Haptics check clip", 1000, 1, 1000, false);
        try
        {
            // Set the scope directly; SetCurrentUser would fire UserReady side effects in the editor.
            userField.SetValue(null, TestUser);
            UserPlayerPrefs.DeleteKey(VibrationManager.HapticsPrefsKey);
            CheckPersistence(enabledField, passed);
            CheckManagerGate(host, clip, enabledField, passed);
            return "HapticsSettingChecks PASS (" + passed.Count + "):\n- " + string.Join("\n- ", passed);
        }
        finally
        {
            UserPlayerPrefs.DeleteKey(VibrationManager.HapticsPrefsKey);
            userField.SetValue(null, savedUser);
            enabledField.SetValue(null, savedEnabled);
            VibrationManager.instance = savedInstance;
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    static void CheckPrefabRow(List<string> passed)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Require(font != null, "Missing " + FontPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var menu = root.GetComponentInChildren<SettingsMenu>(true);
            Require(menu != null, "PauseMenu has no SettingsMenu");
            var page = menu.transform.Find("ControlsPage");
            Require(page != null, "Settings has no ControlsPage");
            var row = page.Find("Haptics");
            Require(row != null, "ControlsPage has no Haptics row");
            var label = row.Find("Text")?.GetComponent<TextMeshPro>();
            Require(label != null && label.text.StartsWith("Haptics"), "Haptics row label");
            Require(menu.hapticsText == label, "SettingsMenu.hapticsText points at the row label");
            string[] names = { "On", "Off" };
            foreach (var name in names)
            {
                var button = row.Find(name);
                Require(button != null && button.GetComponent<PauseMenuButton>() != null, "Haptics button " + name);
                Require(button.GetComponentInChildren<BoxCollider>(true)?.name == "Hitbox", "Haptics " + name + " hitbox");
                var text = button.Find("Text")?.GetComponent<TextMeshPro>();
                Require(text != null && text.text == name, "Haptics " + name + " button text");
            }
            int buttons = 0;
            foreach (Transform child in row)
                if (child.GetComponent<PauseMenuButton>() != null) buttons++;
            Require(buttons == 2, "Haptics row has exactly On and Off");
            foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
                Require(text.font == font, "CutePixel SDF on " + text.name + " '" + text.text + "'");
            passed.Add("Settings > Controls > Haptics row: label, On/Off buttons with hitboxes, all text CutePixel SDF");

            CheckLayout(root, menu, page);
            passed.Add("Controls rows stay inside the screen with no vertical overlap");

            // The UI's own handler (desktop or finger press) saves the choice.
            var userField = typeof(UserPlayerPrefs).GetField("currentUserId", PrivateStatic);
            object savedUser = userField.GetValue(null);
            object savedEnabled = typeof(VibrationManager).GetField("hapticsEnabled", PrivateStatic).GetValue(null);
            try
            {
                userField.SetValue(null, TestUser);
                var handler = typeof(SettingsMenu).GetMethod("OnHapticsPressed", Private);
                Require(handler != null, "SettingsMenu.OnHapticsPressed");
                handler.Invoke(menu, new object[] { row.Find("Off").GetComponent<PauseMenuButton>() });
                Require(SavedValue() == 0 && !VibrationManager.HapticsEnabled, "Off press saves 0 and disables");
                Require(label.text == "Haptics: Off", "Label shows Off");
                handler.Invoke(menu, new object[] { row.Find("On").GetComponent<PauseMenuButton>() });
                Require(SavedValue() == 1 && VibrationManager.HapticsEnabled, "On press saves 1 and enables");
                Require(label.text == "Haptics: On", "Label shows On");
                passed.Add("Pressing Off/On through SettingsMenu saves 0/1 and updates the label");
            }
            finally
            {
                UserPlayerPrefs.DeleteKey(VibrationManager.HapticsPrefsKey);
                userField.SetValue(null, savedUser);
                typeof(VibrationManager).GetField("hapticsEnabled", PrivateStatic).SetValue(null, savedEnabled);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CheckLayout(GameObject root, SettingsMenu menu, Transform page)
    {
        page.gameObject.SetActive(true);
        var settings = menu.transform;
        var screen = root.GetComponent<PauseMenu>().screen.GetComponent<Renderer>();
        Require(screen != null, "Pause screen renderer");
        float screenMin = float.MaxValue, screenMax = float.MinValue;
        Extend(settings, screen.bounds, ref screenMin, ref screenMax);
        var spans = new List<(string name, float min, float max)>();
        foreach (Transform row in page)
        {
            if (row.name == "SettingsText") continue;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var renderer in row.GetComponentsInChildren<MeshRenderer>(true))
            {
                var text = renderer.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.ForceMeshUpdate();
                    Bounds b = text.textBounds;
                    Extend(settings, text.transform.TransformPoint(b.min), ref min, ref max);
                    Extend(settings, text.transform.TransformPoint(b.max), ref min, ref max);
                }
                else Extend(settings, renderer.bounds, ref min, ref max);
            }
            Require(min >= screenMin && max <= screenMax, row.name + " stays inside the screen");
            spans.Add((row.name, min, max));
        }
        spans.Sort((a, b) => b.max.CompareTo(a.max));
        for (int i = 1; i < spans.Count; i++)
            Require(spans[i].max < spans[i - 1].min, spans[i].name + " overlaps " + spans[i - 1].name);
        Require(spans.Count == 5 && spans[spans.Count - 1].name == "Haptics", "Haptics is the bottom Controls row");
    }

    static void Extend(Transform space, Bounds bounds, ref float min, ref float max)
    {
        Extend(space, bounds.min, ref min, ref max);
        Extend(space, bounds.max, ref min, ref max);
    }

    static void Extend(Transform space, Vector3 world, ref float min, ref float max)
    {
        float y = space.InverseTransformPoint(world).y;
        min = Mathf.Min(min, y);
        max = Mathf.Max(max, y);
    }

    static void CheckPersistence(FieldInfo enabledField, List<string> passed)
    {
        enabledField.SetValue(null, false);
        VibrationManager.LoadSavedHapticsSetting();
        Require(VibrationManager.HapticsEnabled, "No saved value defaults to on");

        VibrationManager.SetHapticsEnabled(false);
        Require(SavedValue() == 0, "Off is written to the user-scoped key");
        enabledField.SetValue(null, true); // a fresh session starts at the default
        VibrationManager.LoadSavedHapticsSetting();
        Require(!VibrationManager.HapticsEnabled, "Off survives a reload");

        VibrationManager.SetHapticsEnabled(true);
        enabledField.SetValue(null, false);
        VibrationManager.LoadSavedHapticsSetting();
        Require(VibrationManager.HapticsEnabled && SavedValue() == 1, "On survives a reload");
        passed.Add("Default on; Off and On saved per user with UserPlayerPrefs and restored on reload");
    }

    static void CheckManagerGate(GameObject host, AudioClip clip, FieldInfo enabledField, List<string> passed)
    {
        var manager = host.AddComponent<VibrationManager>();
        var hand = host.AddComponent<PlayerHand>();
        hand.controller = OVRInput.Controller.LTouch;
        VibrationManager.instance = manager;
        var pending = (System.Collections.IList)typeof(VibrationManager).GetField("pending", Private).GetValue(manager);
        var pulseEnds = (float[])typeof(VibrationManager).GetField("pulseEndTimes", Private).GetValue(manager);

        VibrationManager.SetHapticsEnabled(true);
        QueueAll(manager, hand, clip);
        Require(pending.Count == 3, "On: delayed requests are queued");
        pulseEnds[0] = pulseEnds[1] = 10f; // pulses running on both hands

        VibrationManager.SetHapticsEnabled(false);
        Require(pending.Count == 0, "Turning off drops queued vibrations");
        Require(pulseEnds[0] < 0f && pulseEnds[1] < 0f, "Turning off ends running pulses");
        Require(!manager.CanHaptic, "CanHaptic follows the setting");

        QueueAll(manager, hand, clip);
        manager.TriggerVibrationPulse(1f, 1f, .5f, hand);
        manager.TriggerGrabTick(hand, true);
        manager.TriggerThrottleClick(hand);
        manager.TriggerButtonPress(hand);
        manager.TriggerDriftWarning(hand, 1f);
        manager.TriggerHeldItemHit(hand);
        manager.TriggerTrackJoint(1f);
        manager.TriggerDowned();
        manager.TriggerRevived();
        manager.TriggerVibration(1f, 1f, hand);
        manager.TriggerVibration(clip, hand);
        manager.TriggerVibrationAll(clip);
        Require(pending.Count == 0 && pulseEnds[0] < 0f && pulseEnds[1] < 0f, "Off: every request is ignored");

        VibrationManager.SetHapticsEnabled(true);
        QueueAll(manager, hand, clip);
        Require(pending.Count == 3 && manager.CanHaptic, "Turning back on resumes vibration requests");
        passed.Add("VibrationManager ignores every preset, pulse and clip while off, clears queued/running vibration, and resumes when on");
    }

    static void QueueAll(VibrationManager manager, PlayerHand hand, AudioClip clip)
    {
        manager.TriggerVibration(.5f, .5f, hand, 1f);
        manager.TriggerVibration(clip, hand, 1f);
        manager.TriggerVibrationAll(clip, 1f);
    }

    static int SavedValue() =>
        PlayerPrefs.GetInt(UserPlayerPrefs.ScopedKey(VibrationManager.HapticsPrefsKey), -1);

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
