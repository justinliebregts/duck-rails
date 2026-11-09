using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises login placement with simulated tracking, without starting sign-in or XR.
public static class LoginViewChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Call(LoginView view, string method, params object[] args)
        => typeof(LoginView).GetMethod(method, Private).Invoke(view, args);

    static void Set(LoginView view, string field, object value)
        => typeof(LoginView).GetField(field, Private).SetValue(view, value);

    static bool Pending(LoginView view)
        => (bool)typeof(LoginView).GetField("_awaitingAlign", Private).GetValue(view);

    static void TrackedPose(HeadsetCamera camera, Vector3 position, Quaternion rotation)
    {
        camera.transform.SetLocalPositionAndRotation(position, rotation);
        ((Action)typeof(HeadsetCamera).GetField("TrackedPoseApplied", Private).GetValue(camera))?.Invoke();
    }

    [MenuItem("Duck Rails/Tests/Login View Alignment")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run login checks outside Play mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        LoginView view = null;
        try
        {
            var root = new GameObject("Login alignment fixture");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            Transform Child(string name)
            {
                var child = new GameObject(name).transform;
                child.SetParent(root.transform, false);
                return child;
            }

            var rig = Child("Rig");
            var head = Child("Head");
            head.SetParent(rig, false);
            var camera = head.gameObject.AddComponent<HeadsetCamera>();
            var spots = new LoginSpot[3];
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = Child("Spot " + i);
                spot.SetPositionAndRotation(new Vector3(20 * i - 7, 2 * i, 15 - i), Quaternion.Euler(0, 113 * i - 90, 0));
                spots[i] = spot.gameObject.AddComponent<LoginSpot>();
            }

            view = root.AddComponent<LoginView>();
            Set(view, "rig", rig);
            Set(view, "spots", spots);
            Call(view, "Awake");
            Call(view, "OnEnable");

            // No valid pose must leave alignment pending, even beyond the old timeout.
            for (int i = 0; i < 180; i++) Call(view, "Update");
            Require(Pending(view), "Login gave up before tracking arrived.");

            int cases = 0;
            foreach (var offset in new[] { new Vector3(0, 1.6f, 0), new Vector3(2, 1.2f, -3), new Vector3(-4, 1.9f, 2) })
            foreach (float yaw in new[] { 0f, 90f, 180f, -120f })
            foreach (float pitch in new[] { -30f, 0f, 40f })
            for (int i = 0; i < spots.Length; i++)
            {
                Call(view, "Show", i);
                var rotation = Quaternion.Euler(pitch, yaw, 12);
                TrackedPose(camera, offset, rotation);
                Vector3 position = head.position;
                Vector3 target = spots[i].transform.position;
                Require(Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z)) < .001f,
                    "Room-scale offset displaced the viewer from the login spot.");
                Require(Mathf.Abs(position.y - target.y - offset.y) < .001f, "Alignment changed the tracked eye height.");
                Require(Vector3.Angle(Vector3.ProjectOnPlane(head.forward, Vector3.up), spots[i].transform.forward) < .05f,
                    "Viewer is not facing the login logo.");
                Require(Quaternion.Angle(head.localRotation, rotation) < .05f, "Alignment changed the physical head pose.");
                Require(!Pending(view), "Successful placement remained pending.");
                for (int j = 0; j < spots.Length; j++)
                    Require(spots[j].gameObject.activeSelf == (i == j), "Wrong login spot is visible.");

                Vector3 rigPosition = rig.position;
                Quaternion rigRotation = rig.rotation;
                TrackedPose(camera, offset + Vector3.right, Quaternion.Euler(pitch, yaw + 45, 12));
                Require(Vector3.Distance(rigPosition, rig.position) < .001f && Quaternion.Angle(rigRotation, rig.rotation) < .05f,
                    "Login kept forcing the viewer back toward the logo after placement.");
                cases++;
            }

            Call(view, "Show", 0);
            TrackedPose(camera, Vector3.up * 1.6f, Quaternion.Euler(90, 0, 0));
            Require(Pending(view), "A vertical gaze consumed alignment without a usable heading.");
            TrackedPose(camera, new Vector3(1, 1.6f, 2), Quaternion.Euler(0, 150, 0));
            Require(!Pending(view), "Login did not retry once the heading became usable.");

            Call(view, "Show", 1);
            Call(view, "OnDisable");
            TrackedPose(camera, Vector3.up * 1.6f, Quaternion.identity);
            Require(Pending(view), "Disabled login view still received tracking callbacks.");
            Call(view, "OnEnable");
            TrackedPose(camera, Vector3.up * 1.6f, Quaternion.identity);
            Require(!Pending(view), "Re-enabled login view did not receive tracking callbacks.");
            CheckAuthoredSpots();
            Debug.Log($"LoginViewChecks PASS: {cases} offset/heading/pitch/spot combinations; delayed pose, free movement, vertical gaze, subscription lifecycle and authored logo placement.");
        }
        finally
        {
            if (view != null) Call(view, "OnDisable");
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void CheckAuthoredSpots()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Login.unity");
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LoginView>(true)).Single();
            var spots = (LoginSpot[])typeof(LoginView).GetField("spots", Private).GetValue(view);
            Require(spots.Length == 3, "Expected the three shipped login spots.");
            foreach (var spot in spots)
            {
                var logo = spot.transform.Find("Logo");
                Require(logo != null, "Login spot has no logo.");
                Vector3 toLogo = Vector3.ProjectOnPlane(logo.position - spot.transform.position, Vector3.up);
                Require(toLogo.sqrMagnitude > 1 && Vector3.Angle(toLogo, spot.transform.forward) < .1f,
                    "Authored login spot does not face its logo: " + spot.name);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
