#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class RailQuickStartChecks
{
    [MenuItem("Duck Rails/Run Quick Start Checks")]
    public static void Run()
    {
        Require(RailEditorQuickStart.ShouldQuickStart(true, true, Array.Empty<string>(), "RailsQuickStart"),
            "Main editor with empty runtime tags must follow the selected quick-start scenario.");
        Require(!RailEditorQuickStart.ShouldQuickStart(true, true, new[] { "RailsQuickStart" }, null),
            "Stale runtime tags must not enable quick start for solo Play.");
        Require(!RailEditorQuickStart.ShouldQuickStart(true, true, Array.Empty<string>(), "LobbyTest"),
            "Other scenarios must retain normal matchmaking.");
        Require(RailEditorQuickStart.ShouldQuickStart(true, false, new[] { "Player2" }, "RailsQuickStart"),
            "Additional Player2 editor must join the shared expedition.");
        Require(!RailEditorQuickStart.ShouldQuickStart(true, false, new[] { "LobbyTest" }, "RailsQuickStart"),
            "Other additional players must retain normal matchmaking.");
        Require(!RailEditorQuickStart.ShouldQuickStart(false, true, Array.Empty<string>(), "RailsQuickStart") &&
            !RailEditorQuickStart.ShouldQuickStart(false, false, new[] { "Player2" }, "RailsQuickStart"),
            "Disabling quick start must restore normal matchmaking for both editors.");
        Require(!RailEditorQuickStart.ShouldQuickStart(true, false, new[] { "Player2" }, null) &&
            !RailEditorQuickStart.ShouldQuickStart(true, false, new[] { "Player2" }, "") &&
            !RailEditorQuickStart.ShouldQuickStart(true, false, new[] { "Player2" }, "None"),
            "Player2's account tag must not enable quick start when the main tag is None.");
        Require(RailEditorQuickStart.RoomCode == "11111", "Quick start must use the fixed room code.");
        Debug.Log("Duck Rails quick-start checks passed (8 checks).");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
