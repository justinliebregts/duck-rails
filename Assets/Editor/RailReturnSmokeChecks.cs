#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

/// <summary>Focused authenticated one-client return checks; never substitutes for group acceptance.</summary>
public static class RailReturnSmokeChecks
{
    static int stage, round;
    static double entered;
    static string expedition;
    static readonly List<string> passed = new List<string>();
    [MenuItem("Duck Rails/Run Public Return Smoke Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || PhotonSession.Kind != RailSessionRules.CompoundKind ||
            !PhotonSession.Instance.WorldReady || PhotonNetwork.CurrentRoom.PlayerCount != 1)
            throw new InvalidOperationException("Start in an authenticated public compound with only the local test player.");
        stage = round = 0; passed.Clear(); entered = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        MoveToPlatform(); Save("running");
    }
    static void MoveToPlatform() => NewPlayer.instance.Warp(new Vector3(0, .4f, 4), Quaternion.identity);
    static void Save(string status) => File.WriteAllText("Temp/rails-return-smoke.txt", status + "\n" + string.Join("\n", passed));
    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Play Mode stopped");
            var session = PhotonSession.Instance;
            if (EditorApplication.timeSinceStartup - entered > 70)
                throw new Exception("Return timed out: " + session.Status + " " + session.CrewReturnDiagnostic);
            if (stage == 0 && PhotonSession.Phase == RailPhase.Running && session.WorldReady)
            {
                expedition = PhotonNetwork.CurrentRoom.Name;
                session.FinishRun(round == 0);
                stage = 1; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 1 && PhotonSession.Kind == RailSessionRules.CompoundKind && session.WorldReady)
            {
                if (PhotonNetwork.CurrentRoom.Name == expedition || PhotonNetwork.CurrentRoom.MaxPlayers != 12)
                    throw new Exception("Return did not reach a twelve-player compound");
                passed.Add((round == 0 ? "Success" : "Failure") + " returned to compound " + PhotonNetwork.CurrentRoom.Name);
                // Allow the control-room completion barrier to be acknowledged.
                stage = 2; entered = EditorApplication.timeSinceStartup; Save("running");
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - entered > 2)
            {
                if (!session.CrewReturnDiagnostic.Contains("active=False"))
                    throw new Exception("Control connection did not finish: " + session.CrewReturnDiagnostic);
                passed.Add("Crew handoff completed and its control connection closed");
                if (++round == 2)
                {
                    Save("PASS (one authenticated desktop client)");
                    Debug.Log("RAILS RETURN SMOKE PASSED: " + string.Join("; ", passed));
                    EditorApplication.update -= Tick;
                }
                else { stage = 0; entered = EditorApplication.timeSinceStartup; MoveToPlatform(); }
            }
        }
        catch (Exception error)
        {
            Save("FAIL: " + error.Message);
            Debug.LogError("RAILS RETURN SMOKE FAILED: " + error.Message);
            EditorApplication.update -= Tick;
        }
    }
}
#endif
