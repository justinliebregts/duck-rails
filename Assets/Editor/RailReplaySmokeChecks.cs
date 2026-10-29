#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

/// <summary>Immediate private replays, alternating success/failure and active/inactive cargo.</summary>
public static class RailReplaySmokeChecks
{
    const int Rounds = 8;
    static int stage, round;
    static double entered;
    static string room, previousRun;
    static readonly List<string> passed = new List<string>();

    [MenuItem("Duck Rails/Run Private Replay Smoke Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || !PhotonSession.IsRailsRoom || !PhotonSession.Instance.WorldReady ||
            PhotonSession.Kind != RailSessionRules.CompoundKind || PhotonNetwork.CurrentRoom.PlayerCount != 1)
            throw new InvalidOperationException("Start in an authenticated public compound with only the local test player.");
        stage = round = 0; previousRun = null; passed.Clear(); entered = EditorApplication.timeSinceStartup;
        room = UnityEngine.Random.Range(70000, 80000).ToString();
        PhotonSession.Instance.RequestCreatePrivate(room);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Save("running");
    }
    static void Require(bool pass, string error) { if (!pass) throw new Exception(error); }
    static void Save(string status) => File.WriteAllText("Temp/rails-replay-smoke.txt", status + "\n" + string.Join("\n", passed));
    static void Tick()
    {
        try
        {
            Require(EditorApplication.isPlaying, "Play Mode stopped");
            var session = PhotonSession.Instance;
            Require(EditorApplication.timeSinceStartup - entered < 45, "Replay timed out at stage " + stage + ": " + session.Status);
            if (stage == 0 && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging && session.WorldReady)
            {
                Require(PhotonNetwork.CurrentRoom.Name == room && !RailWorld.HasLiveCargo, "Staging is ready with stale cargo or a changed room code");
                session.StartPrivateRun(); stage = 1; entered = EditorApplication.timeSinceStartup;
            }
            else if (stage == 1 && PhotonSession.Phase == RailPhase.Running && session.WorldReady)
            {
                var cargo = UnityEngine.Object.FindObjectsByType<RailCargo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(c => c.photonView.ViewID != 0 && PhotonView.Find(c.photonView.ViewID) == c.photonView).ToArray();
                int[] manifest = PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>());
                Require(PhotonSession.RunId != previousRun, "Replay reused the old run ID");
                Require(cargo.Length == 2 && manifest.OrderBy(id => id).SequenceEqual(cargo.Select(c => c.photonView.ViewID).OrderBy(id => id)),
                    "Expected exactly one fresh bat and loaded bag, including inactive cargo");
                foreach (var item in cargo)
                    Require(item.photonView.InstantiationData is object[] data && data.Length >= 3 &&
                        data[data.Length - 2] as string == PhotonSession.RunId, "An old run's object survived replay");
                Require(BagObject.Active.Any(b => b.Keys().Contains("RailBat")), "Fresh bag is missing its supply");
                previousRun = PhotonSession.RunId;
                passed.Add("Round " + (round + 1) + ": exactly two fresh network objects and a loaded bag");
                // Inactive network objects must still be removed before staging
                // is ready. Alternate bag/bat so both cleanup paths are covered.
                if (round % 2 == 0)
                {
                    bool bag = round % 4 == 0;
                    cargo.First(c => (c.GetComponent<BagObject>() != null) == bag).gameObject.SetActive(false);
                }
                session.FinishRun(round % 2 == 0); stage = 2; entered = EditorApplication.timeSinceStartup;
                Save("running");
            }
            else if (stage == 2 && PhotonSession.Phase == RailPhase.Staging && session.WorldReady)
            {
                Require(PhotonNetwork.CurrentRoom.Name == room && !RailWorld.HasLiveCargo, "Return retained old network objects or changed code");
                passed.Add("Round " + (round + 1) + ": cleanup complete before staging readiness");
                if (++round == Rounds)
                {
                    EditorApplication.update -= Tick;
                    Save("PASS: eight private rounds, seven immediate replays; one authenticated desktop client");
                    Debug.Log("RAILS PRIVATE REPLAY CHECKS PASSED: eight rounds including inactive cargo cleanup");
                    return;
                }
                // No extra wait: exercise the earliest possible replay request.
                session.StartPrivateRun(); stage = 1; entered = EditorApplication.timeSinceStartup;
            }
        }
        catch (Exception error)
        {
            EditorApplication.update -= Tick; Save("FAIL: " + error.Message);
            Debug.LogError("RAILS PRIVATE REPLAY CHECKS FAILED: " + error.Message);
        }
    }
}
#endif
