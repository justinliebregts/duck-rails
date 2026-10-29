using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HelicopterRotorChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "HelicopterRotorChecks.Pending";
    static HelicopterRotorChecks() => EditorApplication.update += Tick;

    // Run without -quit: a real play-mode frame supplies Time.deltaTime to Update.
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || Time.deltaTime <= 0f) return;
        SessionState.SetBool(Pending, false);
        try { Verify(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab");
        var staging = new GameObject("Inactive rotor fixture");
        staging.SetActive(false);
        var go = Object.Instantiate(prefab, staging.transform);
        try
        {
            var helicopter = go.GetComponent<Helicopter>();
            var pivot = (Transform)typeof(Helicopter).GetField("tailRotor", Private).GetValue(helicopter);
            Check(pivot != null, "Prefab must wire the tail rotor pivot");
            Check(pivot.GetComponentsInChildren<Collider>(true).Length == 0, "Tail rotation must never move collision geometry");
            Check(pivot.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Tail visual needs no extra update or network component");
            var visual = pivot.GetComponentInChildren<MeshFilter>();
            MeshFilter original = null;
            foreach (var mesh in go.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.name == "UH-60_TailRotor_Mesh.004") original = mesh;
            Check(original != null && visual != null, "Both original collision object and rotating visual must exist");
            var collider = original.GetComponent<MeshCollider>();
            // Keep unrelated prefab behaviours asleep. Inspect the support cache directly because
            // OwnsSupport deliberately rejects inactive hierarchies; live support has its own suite.
            typeof(Helicopter).GetMethod("CacheSupportColliders", Private).Invoke(helicopter, null);
            var support = (HashSet<Collider>)typeof(Helicopter).GetField("supportColliders", Private).GetValue(helicopter);
            Check(collider != null && collider.enabled && support.Contains(collider), "Existing tail support must remain enabled");
            Check(!original.GetComponent<MeshRenderer>().enabled, "Original tail renderer must not double-render");
            Check(visual.GetComponent<MeshRenderer>().enabled, "Rotating visual must render");
            Check(visual.sharedMesh == original.sharedMesh, "Reuse the imported mesh");
            Check(visual.GetComponent<MeshRenderer>().sharedMaterial == original.GetComponent<MeshRenderer>().sharedMaterial,
                "Reuse the existing shared material");
            Check(visual.transform.localToWorldMatrix == original.transform.localToWorldMatrix,
                "Adding the hub pivot must preserve the mesh's initial world placement");
            // The cylindrical hub in the source model is centered here, with its axle along X.
            var hub = new Vector3(.3205905f, 3.793097f, -7.58629f);
            Check(Vector3.Distance(pivot.position, original.transform.TransformPoint(hub)) < .00001f,
                "Pivot must be centered on the modeled hub");
            Check(Vector3.Dot(pivot.right, original.transform.right) > .99999f, "Spin axis must follow the hub axle");

            var colliders = go.GetComponentsInChildren<Collider>(true);
            var poses = new Matrix4x4[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) poses[i] = colliders[i].transform.localToWorldMatrix;
            var update = (Action)Delegate.CreateDelegate(typeof(Action), helicopter, typeof(Helicopter).GetMethod("Update", Private));
            Check(Time.deltaTime > 0f, "Rotor regression requires a positive frame delta");
            // Avoid a full revolution per test tick, regardless of the batch editor's frame delta.
            helicopter.propellerRotateSpeed = 17f / Time.deltaTime;
            var position = go.transform.position;
            var rotation = go.transform.rotation;
            foreach (bool running in new[] { true, false })
            {
                if (running) helicopter.StartPropeller(); else helicopter.StopPropeller();
                var mainBefore = helicopter.propeller.localRotation;
                var tailBefore = pivot.localRotation;
                update();
                Check(Mathf.Abs(Quaternion.Angle(mainBefore, helicopter.propeller.localRotation) - 17f) < .01f,
                    "Main rotor must keep spinning regardless of start/stop state");
                Check(Mathf.Abs(Quaternion.Angle(tailBefore, pivot.localRotation) - 17f) < .01f,
                    "Tail rotor must keep spinning regardless of start/stop state");
                Check(Vector3.Distance(pivot.position, visual.transform.TransformPoint(hub)) < .00001f,
                    "Hub must stay centered while blades rotate");
            }
            for (int i = 0; i < 100; i++) update();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) update();
            Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Rotor updates must have zero recurring managed allocations");
            Check(go.transform.position == position && go.transform.rotation == rotation, "Visual rotation must not move the cabin");
            for (int i = 0; i < colliders.Length; i++)
                if (!colliders[i].transform.IsChildOf(helicopter.propeller))
                    Check(colliders[i].transform.localToWorldMatrix == poses[i], "Visual rotation moved a cabin/support collider: " + colliders[i].name);
            Check(support.Contains(collider), "Tail support identity must survive rotation");
            Debug.Log("HelicopterRotorChecks: ALL PASSED (hub/axis, mesh/material reuse, always spinning, unchanged support colliders, zero recurring allocations)");
        }
        finally { Object.DestroyImmediate(staging); }
    }
}
