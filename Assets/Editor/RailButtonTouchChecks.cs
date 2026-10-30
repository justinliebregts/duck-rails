using System;
using Game.Animation;
using UnityEditor;
using UnityEngine;

public static class RailButtonTouchChecks
{
    [MenuItem("Duck Rails/Run VR Button Touch Checks")]
    public static void Run()
    {
        var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"));
        var key = new GameObject("Button contact fixture");
        try
        {
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
            Debug.Log("10 VR button contact checks passed using DuckPlayer prefab hands.");
        }
        finally { UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(key); }
    }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
}
