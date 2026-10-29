#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in two-editor check using actual authenticated PUN clients and network ownership.</summary>
[InitializeOnLoad]
public static class RailMultiClientCleanupChecks
{
    const int Rounds = 4;
    const string Ack = "test.cleanup.ack", Command = "test.cleanup.command";
    static string Key => MppmStandaloneCredentials.PrefsKeyForAssets(Application.dataPath) + ".CleanupCheck";
    static string RoomFile
    {
        get
        {
            string assets = Application.dataPath.Replace('\\', '/');
            int virtualProject = assets.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            string root = virtualProject >= 0 ? assets.Substring(0, virtualProject) : Path.GetDirectoryName(assets);
            return Path.Combine(root, "Temp", "rails-cleanup-rendezvous.txt");
        }
    }
    static string runToken, room, previousRun, freshRun, takenRun, returnedRun;
    static int stage, completed;
    static double entered, nextTick;
    static bool finished, finishSent;
    static readonly List<string> evidence = new List<string>();
    static bool Host => CurrentPlayer.IsMainEditor;

    static RailMultiClientCleanupChecks() => EditorApplication.update += Tick;

    [MenuItem("Duck Rails/Arm Two Client Cleanup Test")]
    public static void Arm()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Host)
            throw new InvalidOperationException("Arm in the main editor before launching a main + Player2 scenario.");
        if (RailEditorQuickStart.Enabled)
            throw new InvalidOperationException("Disable Two Ducks Quick Start for this private replay check.");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _))
            throw new InvalidOperationException("Save a distinct authenticated Player2 account first.");
        // EditorPrefs reads can be cached per process. Only the pre-launch arm
        // token uses them; publish the eventual room through a shared temp file.
        Directory.CreateDirectory(Path.GetDirectoryName(RoomFile));
        File.WriteAllText(RoomFile, "");
        EditorPrefs.SetString(Key + ".Until", DateTime.UtcNow.AddMinutes(10).ToString("O"));
        EditorPrefs.SetString(Key, Guid.NewGuid().ToString("N"));
        Debug.Log("RAILS TWO CLIENT CLEANUP: armed for ten minutes; launch main + Player2 from the scenario toolbar.");
    }

    [MenuItem("Duck Rails/Disarm Two Client Cleanup Test")]
    public static void Disarm() => EditorPrefs.DeleteKey(Key);

    static void Require(bool ok, string error) { if (!ok) throw new Exception(error); }
    static void Advance(int value) { stage = value; entered = EditorApplication.timeSinceStartup; Save("RUNNING stage " + stage); }
    static void Save(string status)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/rails-multiclient-cleanup-" + (Host ? "main" : "player2") + ".txt",
            status + "\n" + string.Join("\n", evidence));
    }
    static void Record(string message) { evidence.Add(message); Save("RUNNING stage " + stage); }
    static PhotonView[] Cargo()
    {
        var views = new List<PhotonView>();
        foreach (var view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID != 0 && view.GetComponent<RailCargo>() != null) views.Add(view);
        return views.ToArray();
    }
    static void SetAck(string value)
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties[Ack] as string != value)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [Ack] = value });
    }
    static bool AllAck(string value) => PhotonNetwork.PlayerList.Length == 2 &&
        PhotonNetwork.PlayerList.All(p => p.CustomProperties[Ack] as string == value);

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        string token = EditorPrefs.GetString(Key, "");
        if (token.Length == 0 || !EditorApplication.isPlaying || (!Host && !CurrentPlayer.Tags.Contains("Player2"))) return;
        if (!DateTime.TryParse(EditorPrefs.GetString(Key + ".Until", ""), out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (runToken != token)
        {
            runToken = token; room = previousRun = freshRun = takenRun = returnedRun = null;
            completed = 0; finished = finishSent = false; evidence.Clear(); Advance(0);
            Record("Started actual " + (Host ? "main" : "Player2") + " editor client");
        }
        if (finished) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - entered < 180, "Timed out at stage " + stage + ": " + PhotonSession.Instance?.Status);
            var session = PhotonSession.Instance;
            if (session == null || !PhotonSession.IsRailsRoom || session.HasRoomRequest) return;
            if (stage == 0)
            {
                if (!session.WorldReady) return;
                if (Host)
                {
                    room = UnityEngine.Random.Range(70000, 80000).ToString();
                    session.RequestCreatePrivate(room); Advance(1);
                }
                else
                {
                    room = File.Exists(RoomFile) ? File.ReadAllText(RoomFile).Trim() : "";
                    if (room.Length == 0) return;
                    session.RequestJoinCode(room); Advance(2);
                }
                return;
            }
            if (stage == 1)
            {
                if (!session.WorldReady || !PhotonSession.IsPrivate || PhotonNetwork.CurrentRoom.Name != room) return;
                File.WriteAllText(RoomFile, room); Advance(2); return;
            }
            if (PhotonNetwork.CurrentRoom.Name != room || !PhotonSession.IsPrivate) return;
            if (stage == 2)
            {
                if (PhotonNetwork.PlayerList.Length != 2 || !session.WorldReady) return;
                Require(PhotonNetwork.PlayerList.All(p => !string.IsNullOrEmpty(p.UserId)) &&
                    PhotonNetwork.PlayerList.Select(p => p.UserId).Distinct().Count() == 2, "Two distinct authenticated identities are required");
                Require(PhotonNetwork.IsMasterClient == Host, "Unexpected initial master");
                Record("Two distinct authenticated players in private room " + room + "; local actor " + PhotonNetwork.LocalPlayer.ActorNumber);
                Advance(3);
            }
            Require(PhotonNetwork.PlayerList.Length == 2, "A client left during cleanup verification");
            if (PhotonSession.Phase == RailPhase.Staging && session.WorldReady)
            {
                Require(!RailWorld.HasLiveCargo && Cargo().Length == 0, "Staging became ready with stale registered cargo");
                if (previousRun != null && returnedRun != previousRun)
                {
                    returnedRun = previousRun; completed++;
                    Record("Round " + completed + ": zero cargo before staging readiness; same private code");
                    entered = EditorApplication.timeSinceStartup;
                }
                SetAck("clean-" + completed);
                if (completed == Rounds)
                {
                    if (!AllAck("clean-" + completed)) return;
                    finished = true;
                    Save("PASS: four rounds, success/failure, non-master owned active/inactive cargo; two authenticated editor clients");
                    Debug.Log("RAILS TWO CLIENT CLEANUP PASSED (" + (Host ? "main" : "Player2") + ")");
                    return;
                }
                if (Host && AllAck("clean-" + completed)) session.StartPrivateRun();
                return;
            }
            if (PhotonSession.Phase != RailPhase.Running || !session.WorldReady) return;
            var cargo = Cargo();
            string run = PhotonSession.RunId;
            if (freshRun != run)
            {
                int[] manifest = PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>());
                Require(run != previousRun, "Replay reused old run ID");
                Require(cargo.Length == 2 && manifest.OrderBy(id => id).SequenceEqual(cargo.Select(v => v.ViewID).OrderBy(id => id)), "Expected exactly two fresh supplies matching the manifest");
                Require(cargo.All(v => v.InstantiationData is object[] data && data.Length >= 3 && data[data.Length - 2] as string == run), "Old-run supplies survived replay");
                Require(BagObject.Active.Any(b => b.Keys().Contains("RailBat")), "Fresh bag missing its supply");
                freshRun = previousRun = run; finishSent = false;
                Record("Round " + (completed + 1) + ": two fresh supplies, loaded bag, matching run and manifest");
                SetAck("fresh-" + run);
            }
            if (Host && AllAck("fresh-" + run) && PhotonSession.Get(Command, "") != run)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Command] = run });
            if (!Host && PhotonSession.Get(Command, "") == run && takenRun != run)
            {
                foreach (var view in cargo)
                {
                    var bag = view.GetComponent<BagObject>();
                    if (bag != null) bag.RequestControl();
                    else view.TransferOwnership(PhotonNetwork.LocalPlayer);
                }
                takenRun = run;
            }
            int peer = PhotonNetwork.PlayerList.Single(p => p.ActorNumber != PhotonNetwork.MasterClient.ActorNumber).ActorNumber;
            if (cargo.Length != 2 || !cargo.All(v => v.OwnerActorNr == peer)) return;
            if (!Host && PhotonNetwork.LocalPlayer.CustomProperties[Ack] as string != "owned-" + run)
            {
                Require(cargo.All(v => v.IsMine), "Non-master does not control its owned supplies");
                cargo.First(v => (v.GetComponent<BagObject>() != null) == (completed % 2 == 0)).gameObject.SetActive(false);
                Record("Round " + (completed + 1) + ": peer owns both supplies; one inactive on owning client");
                SetAck("owned-" + run);
            }
            if (Host && !finishSent && PhotonNetwork.PlayerList.Single(p => !p.IsLocal).CustomProperties[Ack] as string == "owned-" + run)
            {
                Require(cargo.All(v => !v.IsMine), "Master unexpectedly controls peer supplies");
                cargo.First(v => (v.GetComponent<BagObject>() != null) == (completed % 2 == 0)).gameObject.SetActive(false);
                Record("Round " + (completed + 1) + ": master confirms both supplies owned remotely; finish " + (completed % 2 == 0 ? "success" : "failure"));
                finishSent = true; session.FinishRun(completed % 2 == 0);
            }
        }
        catch (Exception error)
        {
            finished = true; Save("FAIL: " + error.Message);
            Debug.LogError("RAILS TWO CLIENT CLEANUP FAILED: " + error.Message);
        }
    }
}
#endif
