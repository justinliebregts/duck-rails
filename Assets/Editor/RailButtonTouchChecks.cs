using System;
using System.Reflection;
using Game.Animation;
using UnityEditor;
using UnityEngine;

public static class RailButtonTouchChecks
{
    [MenuItem("Duck Rails/Legacy/Run VR Button Touch Checks")]
    public static void Run()
    {
        var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"));
        var key = new GameObject("Button contact fixture");
        try
        {
            var focus = typeof(PhotonMasterHandoff).GetMethod("HasInputFocus", BindingFlags.Static | BindingFlags.NonPublic);
            bool Available(bool keyboard, bool app, bool xr, bool paused, bool editorXR) =>
                (bool)focus.Invoke(null, new object[] { keyboard, app, xr, paused, editorXR });
            Check(Available(false, false, true, false, true), "Link interaction survives editor window focus loss");
            Check(Available(false, true, true, false, true), "Focused Link interaction remains available");
            Check(!Available(false, false, false, false, true), "Link dashboard focus loss blocks interaction");
            Check(!Available(false, true, false, false, true), "Editor focus cannot override headset focus loss");
            Check(!Available(false, false, true, true, true), "Paused Link blocks interaction");
            Check(!Available(false, false, true, false, false), "No running editor XR display retains app focus requirement");
            Check(Available(true, false, false, false, false), "Keyboard simulation retains existing focus policy");
            Check(!Available(true, true, true, true, false), "Pause still blocks keyboard simulation");
            key.transform.position = new Vector3(1000, 1000, 1000);
            key.transform.rotation = Quaternion.Euler(90, 25, 0);
            var volume = key.AddComponent<BoxCollider>(); volume.size = new Vector3(.23f, .2f, .07f); volume.isTrigger = true;
            var player = rig.GetComponent<GR.Player>();
            var body = rig.GetComponent<LocalBodyDriver>().Body;
            foreach (var hand in new[] { player.leftPlayerHand, player.rightPlayerHand })
            {
                var skin = hand.IsControllerLTouch ? body.LeftHand : body.RightHand;
                var fingers = skin.GetComponentsInChildren<SphereCollider>(true);
                Check(fingers.Length > 0, "DuckPlayer has fingertip colliders");
                var finger = fingers[0];
                finger.transform.position = key.transform.position - finger.transform.TransformVector(finger.center);
                Check(hand.TouchesButton(volume), "Actual DuckPlayer fingertip touches rotated button");
                Check(Vector3.Distance(hand.HoldPoint.position, key.transform.position) > 1, "Touch works independently of item hold point");
                finger.enabled = false;
                Check(!hand.TouchesButton(volume), "Disabled finger ignored");
                finger.enabled = true; finger.transform.position += Vector3.up;
                Check(!hand.TouchesButton(volume), "Withdrawal releases contact");
            }
            CheckPressLock();
            Debug.Log("25 VR button checks passed: Link focus, DuckPlayer fingertips and cross-panel withdrawal lock.");
        }
        finally { UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(key); }
    }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }

    static void CheckPressLock()
    {
        var button = new GameObject("Press lock fixture");
        var fingerObject = new GameObject("Press lock finger");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var reset = typeof(VRButtonPressLock).GetMethod("Reset", flags);
        var frame = typeof(VRButtonPressLock).GetField("pressedFrame", flags);
        try {
            reset.Invoke(null, null);
            var box = button.AddComponent<BoxCollider>(); box.size = new Vector3(.2f, .1f, .02f);
            var finger = fingerObject.AddComponent<SphereCollider>(); finger.radius = .01f;
            Check(VRButtonPressLock.TryPress(box, finger), "First physical press accepted");
            Check(!VRButtonPressLock.TryPress(box, finger), "Same-frame next panel is blocked");
            frame.SetValue(null, -1);
            Vector3 front = (Vector3)typeof(VRButtonPressLock).GetField("front", flags).GetValue(null);
            fingerObject.transform.position = -front * .3f; Physics.SyncTransforms();
            Check(VRButtonPressLock.Blocked, "Deep poke behind button stays locked");
            button.SetActive(false);
            Check(VRButtonPressLock.Blocked, "Hiding source panel preserves lock");
            fingerObject.transform.position = front * .025f; Physics.SyncTransforms();
            Check(VRButtonPressLock.Blocked, "Returning through face and small jitter stay locked");
            fingerObject.transform.position = front * .08f; Physics.SyncTransforms();
            Check(!VRButtonPressLock.Blocked, "Clear front withdrawal rearms");
            button.SetActive(true);
            fingerObject.transform.position = Vector3.zero; Physics.SyncTransforms();
            VRButtonPressLock.TryPress(box, finger); frame.SetValue(null, -1);
            fingerObject.transform.position = Vector3.right * .2f; Physics.SyncTransforms();
            Check(!VRButtonPressLock.Blocked, "Clear sideways withdrawal rearms");
        } finally {
            reset.Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(button);
            UnityEngine.Object.DestroyImmediate(fingerObject);
        }
    }
}
