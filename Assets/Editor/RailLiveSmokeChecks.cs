#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in live smoke test using the currently authenticated DesktopPlayer; never creates fake accounts.</summary>
public static class RailLiveSmokeChecks
{
    static int stage;
    static double started, entered;
    static string sourceRoom, privateRoom;
    static float trainZ;
    static Vector3[] cargoLocal;
    static int[] cargoIds;
    static readonly System.Collections.Generic.List<string> passed = new();
    static double Now => EditorApplication.timeSinceStartup;
    [MenuItem("Duck Rails/Run Live Desktop Smoke Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || !PhotonSession.IsRailsRoom || !PhotonSession.Instance.WorldReady || PhotonSession.Kind != RailSessionRules.CompoundKind)
            throw new InvalidOperationException("Start authenticated DesktopPlayer in a public Compound first.");
        if (PhotonNetwork.CurrentRoom.PlayerCount != 1) throw new InvalidOperationException("Smoke test requires a room with only the local test player.");
        EditorApplication.update -= Tick;
        passed.Clear(); stage = 0; started = entered = Now; sourceRoom = PhotonNetwork.CurrentRoom.Name;
        Move(new Vector3(0, .4f, 4));
        EditorApplication.update += Tick;
        Save("running");
    }
    static void Move(Vector3 position) => UnityEngine.Object.FindAnyObjectByType<DesktopPlayer>().Teleport(position, Quaternion.identity, true);
    static void Next(string message)
    {
        passed.Add(message); Debug.Log("RAILS LIVE PASS: " + message); stage++; entered = Now; Save("running");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Save(string status) => File.WriteAllText("Temp/rails-live-smoke.txt", status + "\nstage=" + stage + "\n" + string.Join("\n", passed));
    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Play Mode stopped");
            if (Now - started > 150 || Now - entered > 45) throw new Exception("Timed out at stage " + stage + ": " + PhotonSession.Instance?.Status +
                " phase=" + PhotonSession.Phase + " ready=" + PhotonSession.Instance?.WorldReady +
                " revision=" + PhotonSession.Get(RailSessionRules.Revision, 0) + " run=" + PhotonSession.RunId +
                " seed=" + PhotonSession.Get("rails.seeded", "") + " cargo=" + string.Join(",", PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>())) +
                " paused=" + EditorApplication.isPaused + " elapsed=" + (Now - entered).ToString("F1"));
            var session = PhotonSession.Instance;
            switch (stage)
            {
                case 0:
                    if (Now - entered < 2) return;
                    Require(PhotonSession.Get(RailSessionRules.Deadline, 0d) > PhotonNetwork.Time, "Platform did not start countdown");
                    Require(PhotonSession.Get(RailSessionRules.Roster, Array.Empty<int>()).Contains(PhotonNetwork.LocalPlayer.ActorNumber), "Local occupant missing from roster");
                    Move(new Vector3(0, .3f, -8)); Next("Trigger starts a twenty-second public countdown"); break;
                case 1:
                    if (Now - entered < 2) return;
                    Require(PhotonSession.Get(RailSessionRules.Deadline, 0d) == 0, "Empty platform did not cancel countdown");
                    Move(new Vector3(0, .4f, 4)); Next("Empty trigger cancels countdown"); break;
                case 2:
                    if (Now - entered < 2) return;
                    double remaining = PhotonSession.Get(RailSessionRules.Deadline, 0d) - PhotonNetwork.Time;
                    Require(remaining >= 16 && remaining <= 20, "Re-entry did not start fresh twenty seconds");
                    Next("Re-entry starts a fresh countdown"); break;
                case 3:
                    if (PhotonSession.Phase != RailPhase.Running || !session.WorldReady) return;
                    Require(PhotonNetwork.CurrentRoom.Name != sourceRoom && PhotonSession.Kind == RailSessionRules.ExpeditionKind, "Public departure did not change rooms");
                    cargoIds = PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>());
                    Require(cargoIds.Length == 2, "Expected one bat and one loaded bag");
                    Require(BagObject.Active.Any(b => b.Keys().Any(k => k == "RailBat")), "Backpack not loaded");
                    Next("Public departure joins expedition with a bat and loaded backpack"); break;
                case 4:
                    if (Now - entered < 2) return;
                    cargoLocal = cargoIds.Select(id => RailTrain.Instance.transform.InverseTransformPoint(PhotonView.Find(id).transform.position)).ToArray();
                    trainZ = RailTrain.Instance.transform.position.z;
                    session.SetTrainMoving(true); Next("Train acceleration requested"); break;
                case 5:
                    if (Now - entered < 5) return;
                    Require(RailTrain.Instance.transform.position.z > trainZ + 10, "Carriage did not move");
                    Require(RailTrain.Instance.Contains(GR.Player.instance.transform.position + Vector3.up), "Passenger left moving carriage");
                    CheckCargo(); session.SetTrainMoving(false); Next("Moving carriage carries DesktopPlayer and loose cargo"); break;
                case 6:
                    if (Now - entered < 4) return;
                    Require(Mathf.Abs(RailTrain.Instance.Speed) < .01, "Carriage did not stop");
                    CheckCargo();
                    PhotonView.Find(cargoIds.First(id => PhotonView.Find(id).GetComponent<PhotonGrabbable>() != null)).GetComponent<Rigidbody>().linearVelocity = new Vector3(10, 4, 0);
                    Next("Braking retains loose bat and loaded backpack"); break;
                case 7:
                    if (Now - entered < 2) return;
                    var thrown = PhotonView.Find(cargoIds.First(id => PhotonView.Find(id).GetComponent<PhotonGrabbable>() != null));
                    Require(!RailTrain.Instance.Contains(thrown.transform.position), "Thrown bat stayed attached to train");
                    session.FinishRun(true); Next("Deliberately thrown cargo leaves the carriage"); break;
                case 8:
                    if (PhotonSession.Kind != RailSessionRules.CompoundKind || !session.WorldReady) return;
                    privateRoom = UnityEngine.Random.Range(70000, 79999).ToString(); session.RequestCreatePrivate(privateRoom);
                    Next("Public success returns to a compatible compound"); break;
                case 9:
                    if (!PhotonSession.IsPrivate || !session.WorldReady) return;
                    Require(PhotonNetwork.IsMasterClient && PhotonSession.Phase == RailPhase.Staging, "Creator is not staging master");
                    session.StartPrivateRun(); Next("Private code creation enters manual staging as master"); break;
                case 10:
                    if (PhotonSession.Phase != RailPhase.Running || !session.WorldReady) return;
                    session.FinishRun(false); Next("Private master launches expedition"); break;
                case 11:
                    if (PhotonSession.Phase != RailPhase.Staging || !session.WorldReady) return;
                    Require(PhotonNetwork.CurrentRoom.Name == privateRoom, "Private return changed the code");
                    session.StartPrivateRun(); Next("Private failure returns to staging with the same code"); break;
                case 12:
                    if (PhotonSession.Phase != RailPhase.Running || !session.WorldReady) return;
                    Require(PhotonSession.Get(RailSessionRules.Cargo, Array.Empty<int>()).Length == 2, "Replay duplicated test supplies");
                    session.RequestJoinRandom(); Next("Private replay creates exactly one fresh supply set"); break;
                case 13:
                    if (PhotonSession.Kind != RailSessionRules.CompoundKind || !session.WorldReady) return;
                    session.RequestJoinCode(privateRoom); Next("Last private occupant leaves"); break;
                case 14:
                    if (!session.WorldReady || session.HasRoomRequest) return;
                    Require(PhotonSession.Kind == RailSessionRules.CompoundKind && session.LastError.Contains("Room not found"), "Empty private code still exists or missing-room error absent");
                    var voice = Photon.Voice.PUN.PunVoiceClient.Instance;
                    if (voice == null || voice.Client.CurrentRoom == null || voice.Client.CurrentRoom.Name != PhotonNetwork.CurrentRoom.Name + Photon.Voice.PUN.PunVoiceClient.VoiceRoomNameSuffix) return;
                    Require(voice.Client.AppVersion == PhotonNetwork.NetworkingClient.AppVersion, "Voice does not match the gameplay version partition");
                    Next("Empty private room disappears; missing-code error returns to Compound; Voice follows the final room and version");
                    EditorApplication.update -= Tick; Save("PASS — single authenticated DesktopPlayer only"); break;
            }
        }
        catch (Exception ex)
        {
            EditorApplication.update -= Tick;
            passed.Add("FAIL: " + ex.Message); Debug.LogError("RAILS LIVE FAIL: " + ex);
            Save("FAIL");
        }
    }
    static void CheckCargo()
    {
        for (int i = 0; i < cargoIds.Length; i++)
        {
            var view = PhotonView.Find(cargoIds[i]); Require(view != null, "Cargo disappeared");
            var local = RailTrain.Instance.transform.InverseTransformPoint(view.transform.position);
            Require(Vector3.Distance(local, cargoLocal[i]) < .7f, "Cargo drifted relative to carriage: " + view.name + " " + local);
        }
    }
}
#endif
