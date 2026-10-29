using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Batch: -executeMethod PauseMenuColliderChecks.Run (without -quit).
// Uses an empty scene and isolated controls, so no room requests or preference writes occur.
[InitializeOnLoad]
public static class PauseMenuColliderChecks
{
    const string Key = "PauseMenuColliderChecks.Run";
    const string PrefabPath = "Assets/Prefabs/PauseMenu/PauseMenu.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator checks;
    static double started;
    static readonly List<string> physicsWarnings = new List<string>();

    static PauseMenuColliderChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup;
            physicsWarnings.Clear();
            Application.logMessageReceived += CaptureWarning;
            checks = PlayChecks();
            EditorApplication.update += Step;
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only; uses an empty scene.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CheckGeometry(root);
            CheckPages(root);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Camera.main excludes the prefab preview scene used by the existing audio/pose check.
        var camera = new GameObject("Menu check main camera", typeof(Camera));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3, 2, 4);
        try { PauseMenuAudioChecks.Run(); }
        finally { UnityEngine.Object.DestroyImmediate(camera); }
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void CaptureWarning(string message, string stack, LogType type)
    {
        if (message.Contains("BoxCollider does not support negative")) physicsWarnings.Add(message);
    }

    static void Step()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 120, "Physics checks timed out.");
            if (checks.MoveNext()) return;
            Require(physicsWarnings.Count == 0, "Negative collider warning: " + string.Join("\n", physicsWarnings));
            Debug.Log("PauseMenuColliderChecks PASS: 38 control hitboxes, positive effective scale, original bounds, tab switching, 34 physical button enter/exit and desktop targets, four slider contacts/mapping, cloned mute row, zero negative-scale warnings.");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Step;
        Application.logMessageReceived -= CaptureWarning;
        EditorApplication.Exit(code);
    }

    static void CheckGeometry(GameObject root)
    {
        var boxes = root.GetComponentsInChildren<BoxCollider>(true);
        Require(boxes.Length == 39, "38 controls plus original grab collider.");
        Require(root.GetComponentsInChildren<PauseMenuButton>(true).Length == 34, "34 buttons including mute template.");
        Require(root.GetComponentsInChildren<PauseMenuSlider>(true).Length == 4, "Four sliders.");
        foreach (var box in boxes)
        {
            Vector3 scale = box.transform.lossyScale;
            Require(scale.x > 0 && scale.y > 0 && scale.z > 0, "Positive collider scale: " + box.transform.parent.name);
            if (box.name == "Grab") continue;
            Require(box.name == "Hitbox", "Dedicated hitbox child.");
            var owner = box.transform.parent;
            Require(owner.GetComponent<PauseMenuButton>() != null || owner.GetComponent<PauseMenuSlider>() != null, "Collider owner.");
            Require(box.transform.localPosition == Vector3.zero && box.transform.localRotation == Quaternion.identity,
                "Hitbox origin unchanged.");
            // Reflecting X changes corner order, not the occupied box. Verify all eight original corners.
            Vector3 originalCenter = Vector3.Scale(box.center, new Vector3(-1, 1, 1));
            var actual = Corners(box.transform, box.center, box.size);
            foreach (var point in Corners(owner, originalCenter, box.size))
                Require(actual.Any(candidate => Vector3.Distance(point, candidate) < 0.000001f), "Original box geometry: " + owner.name);
        }
        var row = root.GetComponent<PauseMenu>().playersMenu.playerListItem;
        var clone = UnityEngine.Object.Instantiate(row, row.transform.parent);
        try { Require(clone.muteButton.GetComponentInChildren<BoxCollider>(true).transform.lossyScale.x > 0, "Mute clone retains nested reflection override."); }
        finally { UnityEngine.Object.DestroyImmediate(clone.gameObject); }
    }

    static Vector3[] Corners(Transform t, Vector3 center, Vector3 size)
    {
        var points = new Vector3[8];
        for (int i = 0; i < 8; i++)
            points[i] = t.TransformPoint(center + Vector3.Scale(size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
        return points;
    }

    static void CheckPages(GameObject root)
    {
        var menu = root.GetComponent<PauseMenu>();
        var tabs = new[] { menu.playersButton, menu.roomsButton, menu.settingsButton, menu.cameraButton };
        var pages = new[] { menu.playersMenu.gameObject, menu.roomMenu.gameObject, menu.settingsMenu.gameObject, menu.cameraPage };
        for (int i = 0; i < tabs.Length; i++)
        {
            menu.SetActiveButton(tabs[i].GetComponent<PauseMenuButton>());
            for (int j = 0; j < pages.Length; j++) Require(pages[j].activeSelf == (i == j), "Tab activates only its page.");
        }
    }

    static IEnumerator PlayChecks()
    {
        // PauseMenuButton and PauseMenuSlider create their normal kinematic Rigidbody in Awake.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var hand = new GameObject("Menu check finger");
        hand.layer = LayerMask.NameToLayer("Player");
        var finger = hand.AddComponent<SphereCollider>();
        finger.radius = .001f;
        finger.isTrigger = true;
        hand.transform.position = Vector3.one * 100;
        Physics.simulationMode = SimulationMode.Script;
        foreach (var source in prefab.GetComponentsInChildren<PauseMenuButton>(true))
        {
            var host = new GameObject("Isolated menu control");
            host.SetActive(false);
            host.transform.localScale = source.transform.parent.lossyScale;
            host.transform.rotation = source.transform.parent.rotation;
            var button = UnityEngine.Object.Instantiate(source, host.transform);
            button.transform.localPosition = Vector3.zero;
            button.gameObject.SetActive(true);
            // Retain the real collider, animation, layer filtering and trigger handlers; isolate side effects.
            button.pressedIn = new UnityEvent();
            button.pressedOut = new UnityEvent();
            button.selectOnEnable = false;
            button.preserveLatch = false;
            int presses = 0;
            button.pressedIn.AddListener(() => presses++);
            host.SetActive(true);
            var box = button.GetComponentInChildren<BoxCollider>();
            Require(box.attachedRigidbody == button.GetComponent<Rigidbody>(), "Parent Rigidbody owns child collider.");
            Require((BoxCollider)Field(button, "box") == box, "Button resolves child collider.");
            Require(box.includeLayers == (1 << hand.layer), "Finger layer override retained.");
            Physics.SyncTransforms();
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 forward = box.transform.forward;
            Require(box.Raycast(new Ray(center + forward, -forward), out var hit, 2), "Desktop ray hits button.");
            Require(hit.collider.GetComponentInParent<PauseMenuButton>() == button, "Desktop target resolves original button.");
            hand.transform.position = center;
            Physics.SyncTransforms(); Physics.Simulate(.02f);
            yield return null;
            Require(presses == 1, "Physical finger press: " + source.name);
            Physics.Simulate(.02f);
            Require(presses == 1, "Held contact does not repeat.");
            Call(button, "SnapPose");
            Require(Mathf.Abs(button.transform.localPosition.z + button.pressDepth) < .000001f, "Original press depth and direction.");
            hand.transform.position = Vector3.one * 100;
            Physics.SyncTransforms(); Physics.Simulate(.02f);
            yield return null;
            Require(((List<Collider>)Field(button, "contacts")).Count == 0, "Exit forwarded to parent.");
            Require((bool)Field(button, "latched") == !button.momentary, "Momentary release / toggle latch preserved.");
            button.ShowPressed(false);
            button.Press();
            Require(presses == 2, "Desktop Press invokes event.");
            UnityEngine.Object.DestroyImmediate(host);
        }
        foreach (var source in prefab.GetComponentsInChildren<PauseMenuSlider>(true))
        {
            var host = new GameObject("Isolated slider");
            host.SetActive(false);
            host.transform.localScale = source.transform.parent.lossyScale;
            host.transform.rotation = source.transform.parent.rotation;
            var slider = UnityEngine.Object.Instantiate(source, host.transform);
            slider.transform.localPosition = Vector3.zero;
            slider.gameObject.SetActive(true);
            // Disable OnEnable preference loading; invoke Awake only, retaining the actual collider setup.
            slider.enabled = false;
            host.SetActive(true);
            var box = slider.GetComponentInChildren<BoxCollider>();
            Require((BoxCollider)Field(slider, "box") == box, "Slider resolves child collider.");
            Require(box.attachedRigidbody == slider.GetComponent<Rigidbody>(), "Slider rigidbody ownership.");
            for (int value = slider.minValue; value <= slider.maxValue; value++)
            {
                float x = (float)Call(slider, "ValueToX", value);
                Require((int)Call(slider, "XToValue", x) == value, "Slider value round trip.");
                Call(slider, "PlaceKnob", x);
                Require(Mathf.Abs(slider.knob.localPosition.x - x) < .000001f, "Knob keeps original coordinates.");
            }
            hand.transform.position = box.transform.TransformPoint(box.center);
            Physics.SyncTransforms();
            Require(Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                finger, hand.transform.position, hand.transform.rotation, out _, out _), "Slider contact overlap.");
            var contacts = (HashSet<Collider>)Field(slider, "cancelledContacts");
            contacts.Add(finger);
            Call(slider, "ForgetSeparatedContacts");
            Require(contacts.Count == 1, "Overlapping slider contact retained.");
            hand.transform.position = Vector3.one * 100;
            Physics.SyncTransforms();
            Call(slider, "ForgetSeparatedContacts");
            Require(contacts.Count == 0, "Separated slider contact forgotten.");
            UnityEngine.Object.DestroyImmediate(host);
        }
        UnityEngine.Object.DestroyImmediate(hand);
    }

    static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
