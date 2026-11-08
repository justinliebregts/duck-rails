using System;
using UnityEditor;
using UnityEngine;

public static class RailBridgeHoldChecks
{
    [MenuItem("Duck Rails/Legacy/Run Bridge Hold Checks")]
    public static void Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Debug.Log("BRIDGE HOLD PASS: " + message); }
        bool Near(double a, double b) => Math.Abs(a - b) < .0001;
        var state = new RailRunState { run = "bridge-test" };
        Check(state.BridgeProgress(1000) == 0, "Idle bridge never lowers");
        state.UpdateBridgeHold(100, 101);
        Check(Near(state.BridgeElapsed(100.4), .4), "Continuous hold lowers bridge");
        state.UpdateBridgeHold(100.4, 0);
        Check(Near(state.BridgeElapsed(200), .4) && !state.BridgeMoving(200), "Release saves progress and pauses indefinitely");
        state.UpdateBridgeHold(200, 201);
        state.UpdateBridgeHold(200.25, 201.25);
        Check(Near(state.BridgeElapsed(200.5), .9), "Resume and renewed hold retain prior work without counting idle time");
        var restored = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));
        Check(Near(restored.BridgeElapsed(200.5), .9), "Late join reconstructs active hold and prior work");
        Check(Near(restored.BridgeElapsed(400), 1.65), "Missing heartbeat or authority cannot lower beyond lease expiry");
        restored.UpdateBridgeHold(400, 0);
        restored = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(restored));
        Check(Near(restored.BridgeElapsed(500), 1.65), "Paused state survives master reconstruction");
        restored.UpdateBridgeHold(500, 501);
        Check(Near(restored.BridgeElapsed(500.5), 2.15), "Replacement operator resumes without losing work or adding downtime");
        restored.UpdateBridgeHold(500.5, 501.5);
        restored.UpdateBridgeHold(500.5, 501.5);
        Check(Near(restored.BridgeElapsed(501), 2.65), "Duplicate or overlapping holds do not multiply lowering speed");
        restored.UpdateBridgeHold(501, double.NaN);
        Check(!restored.BridgeMoving(501) && Near(restored.BridgeElapsed(600), 2.65), "Malformed hold stops safely");
        restored.UpdateBridgeHold(600, 900);
        Check(!restored.BridgeMoving(600), "Unbounded hold cannot restart lowering");
        restored.bridgeElapsed = RailRunState.BridgeSeconds - .25;
        restored.UpdateBridgeHold(700, 701);
        restored.UpdateBridgeHold(700.5, 701.5);
        Check(restored.BridgeProgress(700.5) == 1 && !restored.BridgeMoving(700.5), "Fully lowered bridge locks in place");
        restored.UpdateBridgeHold(800, 0);
        Check(restored.BridgeProgress(900) == 1, "Completed bridge stays down after release");
        state.Finish(200.5, "Crew down", Array.Empty<string>());
        double final = state.BridgeElapsed(200.5);
        state.UpdateBridgeHold(300, 301);
        Check(Near(state.BridgeElapsed(400), final), "End of run stops bridge movement and rejects later holds");
        Check(new RailRunState { run = "next" }.BridgeProgress(900) == 0, "Replay starts raised with no carried progress");
        var root = new GameObject("Bridge hold authoring fixture");
        try
        {
            var control = root.AddComponent<RailControl>(); control.action = RailControl.Action.Bridge;
            RailHoldControlBuilder.Install(control); RailHoldControlBuilder.Install(control);
            var hold = root.GetComponent<RailHoldControl>();
            Check(root.GetComponents<RailHoldControl>().Length == 1 && hold.bridge && !hold.radio && hold.button != null,
                "Authoring installs one bridge hold with a real pressable button");
            Check(hold.button.GetComponentInChildren<TMPro.TMP_Text>().text == "HOLD\nLOWER", "Physical control explains continuous holding");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("BRIDGE HOLD: " + count + " checks passed");
    }
}
