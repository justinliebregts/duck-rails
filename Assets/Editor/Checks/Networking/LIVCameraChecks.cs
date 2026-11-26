using System;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class LIVCameraChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    public static int PassedChecks { get; private set; }
    sealed class Actor : Photon.Realtime.Player { public Actor(int id) : base("Camera check", id, false) { } }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("LIV: " + label); }
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden | BindingFlags.Public).Invoke(o, args);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Hidden).GetValue(o);

    [MenuItem("Tools/DuckZ/LIV Camera/Validate Remote Camera")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || PhotonNetwork.IsConnected)
            throw new InvalidOperationException("Run stopped and disconnected.");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/RemoteLIVCamera.prefab");
            Check(prefab != null, "resource prefab present");
            var prop = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<RemoteLIVCamera>();
            var renderer = prop.GetComponent<MeshRenderer>();
            var mesh = prop.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.triangles.Length / 3 == 80 && mesh.subMeshCount == 1, "80 triangles / one submesh");
            Check(prop.GetComponentsInChildren<Renderer>(true).Length == 1 && renderer.sharedMaterials.Length == 1,
                "one renderer / one shared material");
            Check(prop.GetComponentsInChildren<Component>(true).All(c => c is Transform || c is MeshFilter
                || c is MeshRenderer || c is RemoteLIVCamera), "visual only: no LIV, UI, physics, lights or cameras");
            Check(renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows, "no shadow work");
            Check(mesh.bounds.size.x > .52f && mesh.bounds.size.x < .54f && mesh.bounds.size.y > .30f
                && mesh.bounds.size.y < .32f, "size matches owner tablet");
            // Overall dimensions alone missed a displaced/thin handle. Compare each grip bar
            // against the real authored tablet in the same root-local coordinate system.
            var realTablet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interface/CameraTablet.prefab");
            var realMesh = realTablet.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "tablet");
            var realVertices = realMesh.sharedMesh.vertices.Select(v => realTablet.transform.InverseTransformPoint(realMesh.transform.TransformPoint(v))).ToArray();
            foreach (int side in new[] { -1, 1 })
            {
                var actual = GripBounds(realVertices, side);
                var proxy = GripBounds(mesh.vertices, side);
                Check(Vector3.Distance(actual.min, proxy.min) < .001f && Vector3.Distance(actual.max, proxy.max) < .001f,
                    "handle grip bar bounds match real tablet within 1mm, side " + side);
            }
            var cameraGO = new GameObject("Camera check");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO, scene);
            var camera = cameraGO.AddComponent<Camera>(); camera.enabled = false;
            foreach (float distance in new[] { 0f, 107.9f, 108f, 108.1f, 200f })
            {
                camera.transform.position = new Vector3(distance, 0, 0);
                prop.ApplyView(camera);
                Check(renderer.forceRenderingOff == (distance > 108), "radial cutoff " + distance);
            }
            camera.transform.position = new Vector3(80, 0, 80);
            prop.ApplyView(camera);
            Check(renderer.forceRenderingOff, "diagonal distance uses hard radial ceiling");
            // A camera near a left-behind tablet must see it even if the owner is far away.
            prop.transform.position = new Vector3(1000, 0, 1000);
            camera.transform.position = prop.transform.position + Vector3.forward;
            prop.ApplyView(camera);
            Check(!renderer.forceRenderingOff, "visibility measured from tablet, not owner");
            var nestedGO = new GameObject("Recording check");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nestedGO, scene);
            var nested = nestedGO.AddComponent<Camera>(); nested.enabled = false;
            var before = typeof(RemoteLIVCamera).GetMethod("BeforeCamera", BindingFlags.Static | BindingFlags.NonPublic);
            var after = typeof(RemoteLIVCamera).GetMethod("AfterCamera", BindingFlags.Static | BindingFlags.NonPublic);
            // Edit-mode components don't receive ordinary runtime OnEnable: register explicitly.
            Call(prop, "OnDisable"); Call(prop, "OnEnable");
            before.Invoke(null, new object[] { default(ScriptableRenderContext), camera });
            Check(!renderer.forceRenderingOff, "parent eye visibility");
            before.Invoke(null, new object[] { default(ScriptableRenderContext), nested });
            Check(renderer.forceRenderingOff, "nested recording view has independent distance");
            after.Invoke(null, new object[] { default(ScriptableRenderContext), nested });
            Check(!renderer.forceRenderingOff, "restore parent after nested rendering");
            after.Invoke(null, new object[] { default(ScriptableRenderContext), camera });
            Call(prop, "OnDisable");

            var state = new PhotonLIVCamera.CameraState { mode = 2, position = new Vector3(12, 1, -3),
                rotation = Quaternion.Euler(10, 40, 0), scale = Vector3.one, handPosition = new Vector3(.1f, .02f, 0),
                handRotation = Quaternion.Euler(0, 30, 0) };
#pragma warning disable CS0612
            var wire = (byte[])Protocol.Deserialize(Protocol.Serialize(state.Pack(42)));
#pragma warning restore CS0612
            Check(PhotonLIVCamera.CameraState.TryUnpack(wire, out int sequence, out var decoded)
                && sequence == 42 && decoded.mode == 2 && decoded.position == state.position, "Photon wire roundtrip");
            var moved = state; moved.position += Vector3.right * 50;
            Check(!moved.MovedFrom(state), "held world motion reuses existing hand stream");
            moved.handPosition += Vector3.right * .01f;
            Check(moved.MovedFrom(state), "changed grip is synchronized");
            moved = state; moved.mode = 1;
            Check(moved.MovedFrom(state), "release is a state transition");
            var stationary = moved;
            Check(!stationary.MovedFrom(moved), "stationary tablet needs no motion traffic");
            moved.position += Vector3.right * .01f;
            Check(moved.MovedFrom(stationary), "follow movement is synchronized");
            var invalid = state; invalid.position.x = float.NaN;
            Check(!invalid.Valid, "reject nonfinite position");
            invalid = state; invalid.scale.x = -1;
            Check(!invalid.Valid, "reject invalid scale");
            Check(!PhotonLIVCamera.CameraState.TryUnpack(new byte[2], out _, out _), "reject malformed packet");
            // Rails: unheld tablets aboard the train travel train-local (mode 4).
            var aboard = state; aboard.mode = 4;
            Check(aboard.Valid && aboard.Open && !aboard.Held && aboard.TrainLocal, "train-local mode is an open unheld pose");
            Check(!aboard.MovedFrom(aboard), "tablet stationary on the moving train needs no motion traffic");
            var aboardMoved = aboard; aboardMoved.position += Vector3.forward * .01f;
            Check(aboardMoved.MovedFrom(aboard), "train-local movement is synchronized");
            Check(aboard.MovedFrom(moved), "boarding (world to train-local) is a state transition");
            invalid = state; invalid.mode = 5;
            Check(!invalid.Valid, "reject unknown mode");
#pragma warning disable CS0612
            wire = (byte[])Protocol.Deserialize(Protocol.Serialize(aboard.Pack(43)));
#pragma warning restore CS0612
            Check(PhotonLIVCamera.CameraState.TryUnpack(wire, out sequence, out decoded) && sequence == 43
                && decoded.TrainLocal && decoded.position == aboard.position, "train-local wire roundtrip");
            // Rails event codes 101-119 are taken (112 is the pickup refresh). Keep the motion code unique.
            foreach (var field in typeof(PhotonPlayer).Assembly.GetTypes()
                .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(f => f.IsLiteral && f.FieldType == typeof(byte) && f.DeclaringType != typeof(PhotonLIVCamera)
                    && (f.Name.EndsWith("Code") || f.Name.EndsWith("Event") || f.Name.EndsWith("Query") || f.Name.EndsWith("Reply"))))
                Check((byte)field.GetRawConstantValue() != PhotonLIVCamera.MotionEvent,
                    "motion event code unique vs " + field.DeclaringType.Name + "." + field.Name);
            Check(PhotonLIVCamera.IsNewer(int.MinValue, int.MaxValue), "sequence wraps safely");
            Check(!PhotonLIVCamera.IsNewer(3, 4) && !PhotonLIVCamera.IsNewer(4, 4), "reject reordered and duplicate packets");

            var peer = new GameObject("Remote peer check");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(peer, scene);
            var view = peer.AddComponent<PhotonView>(); view.OwnerActorNr = 41;
            var player = peer.AddComponent<PhotonPlayer>();
            var sync = peer.AddComponent<PhotonLIVCamera>();
            Call(sync, "Awake");
            Call(sync, "Accept", state.Pack(5), 99);
            Check(!Get<bool>(sync, "hasReceived"), "ignore spoofed owner");
            Call(sync, "ReceiveCameraSnapshot", "previous-spawn", state.Pack(5), new PhotonMessageInfo(new Actor(41), 0, view));
            Check(!Get<bool>(sync, "hasReceived"), "ignore reliable snapshot from previous spawn");
            Call(sync, "Accept", state.Pack(5), 41);
            Check(Get<bool>(sync, "hasReceived"), "late join snapshot accepted");
            var closed = default(PhotonLIVCamera.CameraState);
            Call(sync, "Accept", closed.Pack(7), 41);
            Call(sync, "Accept", state.Pack(6), 41);
            Check(!Get<PhotonLIVCamera.CameraState>(sync, "received").Open, "old motion cannot reopen closed camera");
            state.mode = 1;
            Call(sync, "Accept", state.Pack(8), 41);
            Check(Get<PhotonLIVCamera.CameraState>(sync, "received").mode == 1, "reopen/left-behind snapshot accepted");
            state.mode = 4;
            Call(sync, "Accept", state.Pack(9), 41);
            Check(Get<PhotonLIVCamera.CameraState>(sync, "received").TrainLocal
                && Get<Vector3>(sync, "shownPosition") == state.position, "boarding snapshot snaps to train-local pose");
            CheckTabletVisibility(scene, sync, player, prop);
            Call(sync, "OnDisable");
            Check(!Get<bool>(sync, "hasReceived"), "avatar teardown clears remote state");
            PassedChecks = checks;
            Debug.Log("LIV_CAMERA_CHECKS_PASS " + checks);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void CheckTabletVisibility(UnityEngine.SceneManagement.Scene scene, PhotonLIVCamera sync, PhotonPlayer player, RemoteLIVCamera proxy)
    {
        var previousLocal = TabletGrab.Local;
        var root = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interface/CameraTablet.prefab"), scene);
        var tablet = root.GetComponent<TabletGrab>();
        foreach (var button in root.GetComponentsInChildren<Liv.Lck.UI.LckButton>(true))
        {
            typeof(Liv.Lck.UI.LckButton).GetField("_propertyBlock", Hidden).SetValue(button, null);
            typeof(Liv.Lck.UI.LckButton).GetField("_colorId", Hidden).SetValue(button, 0);
            Call(button, "OnApplicationFocus", true);
            Check(Get<MaterialPropertyBlock>(button, "_propertyBlock") != null
                && Get<int>(button, "_colorId") == Shader.PropertyToID("_Color"),
                "LIV button handles initial focus before Start: " + button.name);
        }
        var grab = root.GetComponent<Grabbable>();
        var body = root.GetComponent<Rigidbody>();
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var canvases = root.GetComponentsInChildren<Canvas>(true);
        var hidden = renderers.Select(r => r.forceRenderingOff).ToArray();
        var canvasEnabled = canvases.Select(c => c.enabled).ToArray();
        bool collisionEnabled = body.detectCollisions;
        Call(grab, "Awake"); Call(tablet, "Awake"); Call(tablet, "OnEnable");
        var captureCameras = root.GetComponentsInChildren<Liv.Lck.LckCamera>(true);
        typeof(PhotonLIVCamera).GetField("visual", Hidden).SetValue(sync, proxy);
        try
        {
            root.transform.position = new Vector3(30, 2, 20);
            var opened = (PhotonLIVCamera.CameraState)Call(sync, "Capture");
            Check(opened.Valid && opened.mode == 1 && opened.position == root.transform.position,
                "visible owner tablet publishes its world pose");
            tablet.SetVisible(false); tablet.SetVisible(false);
            Check(root.activeInHierarchy && tablet.isActiveAndEnabled && captureCameras.All(c => c.isActiveAndEnabled),
                "closing preserves all capture objects and their lifecycle");
            Check(renderers.All(r => r.forceRenderingOff) && canvases.All(c => !c.enabled), "closed tablet has no visuals");
            Check(!grab.CanBeGrabbed && !body.detectCollisions, "closed tablet cannot be grabbed or touch buttons");
            var closed = (PhotonLIVCamera.CameraState)Call(sync, "Capture");
            Check(closed.Valid && !closed.Open, "active but hidden capture rig publishes closed proxy state");
            Call(sync, "Accept", closed.Pack(20), 41);
            Check(!proxy.gameObject.activeSelf, "close snapshot hides the actual shared proxy model");
            Call(sync, "Accept", opened.Pack(19), 41);
            Check(!Get<PhotonLIVCamera.CameraState>(sync, "received").Open, "delayed open cannot undo visibility close");
            Call(sync, "Accept", closed.Pack(21), 41);
            Check(!Get<PhotonLIVCamera.CameraState>(sync, "received").Open, "late join snapshot stays closed while capture rig runs");

            root.transform.position = new Vector3(-6, 2, 8);
            tablet.SetVisible(true); tablet.SetVisible(true);
            Check(renderers.Select(r => r.forceRenderingOff).SequenceEqual(hidden)
                && canvases.Select(c => c.enabled).SequenceEqual(canvasEnabled)
                && body.detectCollisions == collisionEnabled && grab.CanBeGrabbed, "reopen restores presentation and interaction states");
            opened = (PhotonLIVCamera.CameraState)Call(sync, "Capture");
            Call(sync, "Accept", opened.Pack(22), 41);
            Check(Get<PhotonLIVCamera.CameraState>(sync, "received").position == root.transform.position
                && Get<PhotonLIVCamera.CameraState>(sync, "received").Open
                && Get<Vector3>(sync, "shownPosition") == root.transform.position, "recalled proxy receives and snaps to its new position");
            Call(sync, "Accept", closed.Pack(21), 41);
            Check(Get<PhotonLIVCamera.CameraState>(sync, "received").Open, "old close cannot hide a reopened proxy");

            typeof(PhotonPlayer).GetField("_hasPose", Hidden).SetValue(player, true);
            typeof(PhotonPlayer).GetField("_lastPose", Hidden).SetValue(player, new BodyPose {
                lHandPos = root.transform.position + Vector3.left * .2f, lHandRot = Quaternion.Euler(0, 30, 0),
                rHandPos = root.transform.position + Vector3.right * .2f, rHandRot = Quaternion.Euler(0, -30, 0)
            });
            foreach (bool left in new[] { true, false })
            {
                var handGO = new GameObject(left ? "Left camera check hand" : "Right camera check hand");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(handGO, scene);
                handGO.transform.position = root.transform.position;
                var hand = handGO.AddComponent<PlayerHand>();
                hand.controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                Call(hand, "Awake");
                Check(hand.ForceGrab(grab), "grab visible tablet with " + hand.name);
                var held = (PhotonLIVCamera.CameraState)Call(sync, "Capture");
                Check(held.Valid && held.mode == (left ? 2 : 3), "held proxy retains correct hand mode");
                player.TryGetHandPose(left, out var handPosition, out var handRotation);
                Check(Vector3.Distance(handPosition + handRotation * held.handPosition, root.transform.position) < .0001f,
                    "held proxy offset reconstructs owner position");
                tablet.SetVisible(false);
                Check(hand.Held == null && !grab.IsHeld && root.transform.parent == null, "closing releases held tablet");
                Check(!((PhotonLIVCamera.CameraState)Call(sync, "Capture")).Open, "closing held tablet hides its proxy");
                tablet.SetVisible(true);
                Check(((PhotonLIVCamera.CameraState)Call(sync, "Capture")).mode == 1, "reopened tablet has world mode, not stale hand attachment");
            }
        }
        finally
        {
            typeof(PhotonLIVCamera).GetField("visual", Hidden).SetValue(sync, null);
            Call(tablet, "OnDisable");
            if (previousLocal != null) Call(previousLocal, "OnEnable");
        }
    }

    static Bounds GripBounds(Vector3[] vertices, int side)
    {
        var selected = vertices.Where(v => v.x * side > .22f).ToArray();
        if (selected.Length == 0) throw new Exception("No handle vertices on side " + side);
        var bounds = new Bounds(selected[0], Vector3.zero);
        foreach (var vertex in selected) bounds.Encapsulate(vertex);
        return bounds;
    }
}
