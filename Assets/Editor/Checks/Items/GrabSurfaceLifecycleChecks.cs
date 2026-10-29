using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated batch only: exercises real import callbacks and runtime Awake/OnEnable/OnDisable.
[InitializeOnLoad]
public static class GrabSurfaceLifecycleChecks
{
    const string Key = "GrabSurfaceLifecycleChecks.Run";
    const string Fixture = "Assets/Generated/FreeGrabLifecycleFixture.prefab";
    static double deadline;
    static int stage;
    static IEnumerator playing;

    static GrabSurfaceLifecycleChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 30;
            playing = PlayChecks();
            EditorApplication.update += StepPlay;
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project only.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Fixture) != null) throw new Exception("Unexpected pre-existing fixture");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("New item outside shop catalog");
        root.layer = 8;
        root.AddComponent<PhotonGrabbable>();
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(root.transform, false);
        model.transform.localScale = Vector3.one * .1f;
        model.transform.localPosition = Vector3.right * .35f;
        model.layer = 8;
        PrefabUtility.SaveAsPrefabAsset(root, Fixture);
        Object.DestroyImmediate(root);
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 30;
        EditorApplication.update += StepBake;
    }

    static void StepBake()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Automatic bake timed out at stage " + stage);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fixture);
            var data = prefab.GetComponent<Grabbable>().GrabSurface;
            if (data == null || !data.IsValid) return;
            data.TryClosestPoint(Matrix4x4.identity, Vector3.zero, out Vector3 point);
            if (stage == 0)
            {
                Require(Mathf.Abs(point.x - .3f) < .0001f, "New prefab automatically baked without shop entry");
                var contents = PrefabUtility.LoadPrefabContents(Fixture);
                contents.transform.GetChild(0).localPosition = Vector3.right * .55f;
                PrefabUtility.SaveAsPrefabAsset(contents, Fixture);
                PrefabUtility.UnloadPrefabContents(contents);
                stage = 1;
                return;
            }
            if (Mathf.Abs(point.x - .5f) >= .0001f) return;
            Debug.Log("GRAB LIFECYCLE: new prefab and changed child transform rebaked automatically.");
            EditorApplication.update -= StepBake;
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static IEnumerator PlayChecks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fixture);
        var item = Object.Instantiate(prefab).GetComponent<PhotonGrabbable>();
        var left = new GameObject("Left grab hand").AddComponent<PlayerHand>();
        var right = new GameObject("Right grab hand").AddComponent<PlayerHand>();
        left.controller = OVRInput.Controller.LTouch;
        right.controller = OVRInput.Controller.RTouch;
        left.enabled = right.enabled = false;
        left.transform.position = new Vector3(.5f - .059f, 0, -.06f);
        right.transform.position = new Vector3(.5f - .061f, 0, -.06f);
        yield return null;
        Physics.SyncTransforms();
        Require(left.FindGrabCandidate() == item, "Actual OnEnable registers a 5.9 cm pickup");
        Require(right.FindGrabCandidate() == null, "Actual hand rejects a 6.1 cm pickup");
        Quaternion originalRotation = item.transform.rotation;
        Require(left.TryGrab(item), "Left hand takes item immediately");
        Require(item.TryGetSurfacePoint(left.HoldPoint.position, out var point) && (point - left.HoldPoint.position).magnitude < .0011f, "Left surface contact");
        Require(Quaternion.Angle(item.transform.rotation, originalRotation) < .001f, "No forced orientation");
        right.transform.position = left.transform.position + Vector3.forward * .02f;
        Require(right.FindGrabCandidate() == item, "Held item remains discoverable with collisions disabled");
        Require(right.TryGrab(item) && !left.IsHolding && right.IsHolding, "Hand transfer succeeds");
        Require(item.TryGetSurfacePoint(right.HoldPoint.position, out point) && (point - right.HoldPoint.position).magnitude < .0011f, "Right transfer surface contact");
        right.transform.position += Vector3.up;
        yield return null;
        Require(item.TryGetSurfacePoint(right.HoldPoint.position, out point) && (point - right.HoldPoint.position).magnitude < .0011f, "Contact stays locked across runtime frames");
        right.Release();
        Require(!item.IsHeld && item.GetComponent<Rigidbody>().detectCollisions, "Release restores physics");
        item.gameObject.SetActive(false);
        Require(right.FindGrabCandidate() == null, "Disabled/pool item removed from candidates");
        item.gameObject.SetActive(true);
        Require(right.FindGrabCandidate() == item, "Pool enable restores candidates");
        Object.Destroy(item.gameObject); Object.Destroy(left.gameObject); Object.Destroy(right.gameObject);
        yield return null;
        Debug.Log("GrabSurfaceLifecycleChecks PASS: automatic new/changed bakes, actual runtime registry, both hands, transfer, persistent contact, release and pool reuse.");
    }

    static void StepPlay()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play checks timed out");
            if (playing.MoveNext()) return;
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void Require(bool value, string label) { if (!value) throw new Exception(label); }

    static void Finish(int code)
    {
        EditorApplication.update -= StepBake;
        EditorApplication.update -= StepPlay;
        SessionState.EraseBool(Key);
        string guid = AssetDatabase.AssetPathToGUID(Fixture);
        AssetDatabase.DeleteAsset(Fixture);
        if (!string.IsNullOrEmpty(guid)) AssetDatabase.DeleteAsset(GrabSurfaceBaker.OutputFolder + "/" + guid + ".asset");
        EditorApplication.Exit(code);
    }
}
