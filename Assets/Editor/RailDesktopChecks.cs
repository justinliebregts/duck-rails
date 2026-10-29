#if UNITY_EDITOR
using System;
using System.Reflection;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class RailDesktopChecks
{
    [MenuItem("Duck Rails/Run Desktop Regression Checks")]
    public static void Run()
    {
        var second = new MppmStandaloneCredentials.User { tag = "Player2", token = "test-only-token" };
        var third = new MppmStandaloneCredentials.User { tag = "Player3", token = "another-test-token" };
        var users = new[] { second, third };
        Require(MppmStandaloneCredentials.TrySelectUser(new[] { "LobbyTest", "Player2" }, users, out var selected, out _) && selected == second,
            "Account tag must select Player2 even alongside an unrelated tag");
        Require(!MppmStandaloneCredentials.TrySelectUser(new[] { "player2" }, users, out _, out _), "Account tags must be case sensitive");
        Require(!MppmStandaloneCredentials.TrySelectUser(Array.Empty<string>(), users, out _, out _), "Untagged editors must not silently use another account");
        Require(!MppmStandaloneCredentials.TrySelectUser(new[] { "Player2", "Player3" }, users, out _, out _), "Ambiguous account tags must fail");
        Require(MppmStandaloneCredentials.PrefsKeyForAssets("X:/Work/duck-rails/Assets") ==
            MppmStandaloneCredentials.PrefsKeyForAssets("X:/Work/duck-rails/Library/VP/Player2/Assets"), "Virtual and main editors must share the Rails account store");

        var carriage = new GameObject("Footstep check carriage");
        var player = new GameObject("Footstep check player");
        try
        {
            var steps = player.AddComponent<DistanceFootsteps>();
            var travelled = typeof(DistanceFootsteps).GetField("travelled", BindingFlags.Instance | BindingFlags.NonPublic);
            // Edit Mode does not invoke this component's Awake.
            typeof(DistanceFootsteps).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(steps, null);
            Vector3 seat = new Vector3(1, 0, 2);
            steps.Tick(carriage.transform.TransformPoint(seat), true, Vector3.up, carriage.transform);
            for (int i = 0; i < 20; i++)
            {
                carriage.transform.position += new Vector3(0, 0, .4f);
                carriage.transform.Rotate(0, 3, 0);
                steps.Tick(carriage.transform.TransformPoint(seat), true, Vector3.up, carriage.transform);
            }
            Require((float)travelled.GetValue(steps) < .001f, "Riding a translating/rotating carriage must not count as walking");
            seat.x += .4f;
            steps.Tick(carriage.transform.TransformPoint(seat), true, Vector3.up, carriage.transform);
            Require(Mathf.Abs((float)travelled.GetValue(steps) - .4f) < .001f, "Walking on the carriage must still count");
            seat.x += .5f;
            steps.Tick(carriage.transform.TransformPoint(seat), true, Vector3.up, carriage.transform);
            Require(Mathf.Abs((float)travelled.GetValue(steps) - .1f) < .001f, "Walking must cross the normal step threshold");
            steps.Tick(new Vector3(100, 0, 0), true, Vector3.up);
            Require((float)travelled.GetValue(steps) == 0f, "Leaving the carriage must reset the distance frame");
            steps.Tick(new Vector3(100.4f, 0, 0), true, Vector3.up);
            Require(Mathf.Abs((float)travelled.GetValue(steps) - .4f) < .001f, "Ground walking must remain unchanged");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(carriage);
        }
        Debug.Log("Duck Rails desktop regression checks passed (10 checks).");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
