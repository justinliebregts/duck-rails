#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Two authenticated desktop processes, actual pose stream, melee RPC and room handoff.</summary>
[InitializeOnLoad]
public static class RailEnemyBoardingNetworkChecks
{
    const string Key = "test.boarding.stage", IdKey = "test.boarding.enemy", AckKey = "test.boarding.ack";
    const string RunKey = "Rails.Boarding.Network";
    static string Root { get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string ArmPath => Root + "/Temp/rails-boarding-arm.txt";
    static string LogPath => Root + "/docs/validation/enemy-boarding/" + (RailEnemySpawns.ForcedEnemyType == 3 ? "spider-" : "") + "network-" + (CurrentPlayer.IsMainEditor ? "main" : "remote") + ".txt";
    static string token, room;
    static int stage = -1, pendingStage = -1;
    static double entered, started, nextTick;
    static bool action, done, rejoining;
    static float initialDistance;
    static Vector3 reference;
    static int preparedEnemy;
    static bool routeInstalled, sawExit, rendezvousAttempted, sawMelee;
    static Func<string, RailRouteState> previousRoute;
    static RailRouteState FixtureRoute(string run)
    {
        return RailRoute.ChooseOrdered(run, new[] { "roadside-motel-v1", "maintenance-depot-v1", "lakeside-campground-v1" }
            .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/" + id + ".prefab").GetComponent<RailBlock>()).ToArray(), 12345);
    }
    static ScriptableObject Scenario => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    static RailEnemyBoardingNetworkChecks() => EditorApplication.update += Tick;
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Run Two Client Network Loop")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Scenario == null) throw new Exception("Stop Play Mode and select a multiplayer scenario first");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 test account first");
        var manager = Scenario;
        var property = manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(RunKey + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(manager)));
        SessionState.SetBool(RunKey + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(RunKey, true);
        Directory.CreateDirectory(Root + "/docs/validation/enemy-boarding");
        File.WriteAllText(Root + "/docs/validation/enemy-boarding/" + (RailEnemySpawns.ForcedEnemyType == 3 ? "spider-" : "") + "network-main.txt", "");
        File.WriteAllText(Root + "/docs/validation/enemy-boarding/" + (RailEnemySpawns.ForcedEnemyType == 3 ? "spider-" : "") + "network-remote.txt", "");
        foreach (string fixture in new[] { "rails-coal-arm.txt", "rails-week2-arm.txt", "rails-week3-arm.txt", "rails-stability-arm.txt", "rails-cleanup-arm.txt" })
        {
            string path = Root + "/Temp/" + fixture;
            if (File.Exists(path) && DateTime.TryParse(File.ReadAllText(path), out var expiry) && expiry.ToUniversalTime() > DateTime.UtcNow)
                throw new Exception("Another fixture is armed: " + fixture);
        }
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(12).ToString("O"));
        RailEditorQuickStart.Enabled = true;
        property.SetValue(manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(manager, null);
    }
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Stop Network Loop")]
    public static void Stop()
    {
        File.WriteAllText(ArmPath, "");
        var manager = Scenario;
        if (manager != null) manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(manager, null);
        else EditorApplication.isPlaying = false;
    }
    static void Record(string message)
    { File.AppendAllText(LogPath, DateTime.UtcNow.ToString("O") + " " + message + "\n"); Debug.Log("BOARDING NETWORK: " + message); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Stage(int value) { pendingStage = value; action = true; PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [Key] = value }); }
    static void Ack()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties[AckKey] is int value && value == stage) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [AckKey] = stage });
        Record("ACK " + stage + " actor=" + PhotonNetwork.LocalPlayer.ActorNumber + " master=" + PhotonNetwork.IsMasterClient);
    }
    static bool AllAck => PhotonNetwork.PlayerList.Length == 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[AckKey] is int value && value == stage);
    public static void UpdateDormancy(Enemy enemy)
    {
        int freezeStage = pendingStage >= 0 ? pendingStage : stage;
        if (enemy != null) enemy.IsDormant = enemy.HasAuthority && freezeStage != 4 && freezeStage < 7;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        if (!EditorApplication.isPlaying)
        {
            if (routeInstalled) { RailTestTools.OrderedRoute = previousRoute; routeInstalled = false; }
            if (CurrentPlayer.IsMainEditor && SessionState.GetBool(RunKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode
                && Scenario?.GetType().GetProperty("CurrentState")?.GetValue(Scenario)?.ToString() == "NotRunning")
            {
                RailEditorQuickStart.Enabled = SessionState.GetBool(RunKey + ".quick", true);
                Scenario.GetType().GetProperty("ActivePlayModeConfig").SetValue(Scenario,
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SessionState.GetString(RunKey + ".scenario", "")));
                SessionState.SetBool(RunKey, false); File.WriteAllText(ArmPath, "");
            }
            return;
        }
        if (!File.Exists(ArmPath)) return;
        string arm = File.ReadAllText(ArmPath);
        if (!DateTime.TryParse(arm, out var until) || DateTime.UtcNow > until.ToUniversalTime()) return;
        if (arm != token) { token = arm; stage = pendingStage = -1; preparedEnemy = 0; done = rejoining = rendezvousAttempted = false; started = EditorApplication.timeSinceStartup; Record("Started real-client network proof"); }
        if (!routeInstalled)
        {
            previousRoute = RailTestTools.OrderedRoute;
            RailTestTools.OrderedRoute = FixtureRoute;
            routeInstalled = true; sawExit = false;
        }
        if (done) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 600, "Overall network timeout");
            var session = PhotonSession.Instance;
            if (session == null || !session.WorldReady || session.HasRoomRequest) return;
            if (rejoining && PhotonNetwork.CurrentRoom?.Name != room)
            { session.RequestJoinCode(room); return; }
            // A stale MPPM main tag can bypass quick start during authentication.
            // The armed fixture explicitly retries the same authenticated allocation
            // path once on both clients; leave/rejoin phases keep their normal path.
            if (!rejoining && !rendezvousAttempted && PhotonSession.Phase == RailPhase.Compound)
            {
                rendezvousAttempted = true;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var begin = typeof(PhotonSession).GetMethod("Begin", flags);
                var request = Enum.Parse(begin.GetParameters()[0].ParameterType, "EditorQuickStart");
                begin.Invoke(session, new object[] { request, RailEditorQuickStart.RoomCode });
                typeof(PhotonSession).GetField("allocation", flags).SetValue(session, "editor-quick-start-" + RailEditorQuickStart.RoomCode);
                Record("Retrying authenticated test-room rendezvous after Compound startup");
                return;
            }
            if (PhotonSession.Phase != RailPhase.Running || RailTrain.Instance == null || !RailRoute.Instance.Ready) return;
            room = PhotonNetwork.CurrentRoom.Name;
            int current = PhotonSession.Get(Key, 0);
            if (current != stage)
            {
                stage = current; pendingStage = -1; entered = EditorApplication.timeSinceStartup; action = false; sawExit = sawMelee = false;
                initialDistance = RailTrain.Instance.Distance;
                Record("Stage=" + stage + " room=" + room);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 90, "Timeout stage=" + stage);
            var train = RailTrain.Instance;
            if (stage < 7 && NewPlayer.instance != null && (age < 2 || !train.Contains(NewPlayer.instance.transform.position)))
                NewPlayer.instance.Warp(train.transform.TransformPoint(new Vector3(1.2f, .35f, 2)), train.transform.rotation);
            int id = PhotonSession.Get(IdKey, 0);
            var enemy = id > 0 ? PhotonView.Find(id)?.GetComponent<Enemy>() : null;
            if (enemy != null && preparedEnemy != enemy.GetInstanceID())
            {
                preparedEnemy = enemy.GetInstanceID();
                if (enemy.GetComponent<RailBoardingFixtureFreeze>() == null) enemy.gameObject.AddComponent<RailBoardingFixtureFreeze>();
                // Transport/melee fixture only: prevent incidental crew wipes and
                // hold the walking pose while the non-owner waits for replication.
                // The hit/retreat/fall code and all network messages remain active.
                foreach (var trigger in enemy.GetComponentsInChildren<KillTrigger>()) trigger.enabled = false;
                using (var mover = new SerializedObject((MonoBehaviour)enemy.Locomotion))
                {
                    mover.FindProperty("walkSpeed").floatValue = 0;
                    mover.FindProperty("chaseSpeed").floatValue = 0;
                    mover.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            UpdateDormancy(enemy);
            // A connected peer may still be loading its world. Its AI can become
            // authoritative before this fixture can freeze it, so wait for both
            // ready fixture acknowledgments before setting the reference pose.
            if (stage == 0) Ack();
            if (stage == 0 && AllAck && age > 4 && PhotonNetwork.IsMasterClient && !action)
            {
                if (RailEnemySpawns.ForcedEnemyType == 3 && Enemy.Active.Any(e => e.HasAuthority))
                {
                    Require(Enemy.Active.Where(e => e.HasAuthority).All(e => e.GetComponent<ClimbLocomotion>() != null), "Spider-only expedition spawns");
                    Record("PASS spider-only encounter spawns");
                }
                enemy = Enemy.Active.FirstOrDefault(e => e.Boarding != null && e.HasAuthority);
                if (enemy == null) return;
                session.SetTrainMoving(false);
                enemy.Teleport(train.transform.TransformPoint(new Vector3(0, .03f, -3)), train.transform.rotation);
                enemy.Boarding.SetRemoteFrame(true, false); enemy.IsDormant = true;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [IdKey] = enemy.GetComponent<PhotonView>().ViewID, [Key] = 1 });
                action = true; return;
            }
            if (enemy == null) return;
            if (stage == 1 && age > 4)
            {
                if (action) return;
                var sync = enemy.GetComponent<EnemyNetSync>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Record("POSE owner=" + enemy.HasAuthority + " local=" + train.transform.InverseTransformPoint(enemy.Root.position)
                    + " frame=" + enemy.Boarding.IsAboard + " sample=" + typeof(EnemyNetSync).GetField("_hasSample", flags).GetValue(sync)
                    + " trainSample=" + typeof(EnemyNetSync).GetField("_trainSample", flags).GetValue(sync)
                    + " net=" + typeof(EnemyNetSync).GetField("_netPos", flags).GetValue(sync));
                Require(enemy.Boarding.IsAboard, "Train frame replicated to observer");
                reference = train.transform.InverseTransformPoint(enemy.Root.position);
                Require(Vector3.Distance(reference, new Vector3(0, .03f, -3)) < .15f, "Initial local pose");
                Ack();
                if (PhotonNetwork.IsMasterClient && AllAck) { session.SetTrainMoving(true); Stage(2); }
            }
            else if (stage == 2 && age > 6)
            {
                Require(train.Distance > initialDistance + 10, "Train really moved");
                Require(enemy.Boarding.IsAboard && Vector3.Distance(reference, train.transform.InverseTransformPoint(enemy.Root.position)) < .15f, "No carriage-relative lag/drift");
                Ack();
                if (PhotonNetwork.IsMasterClient && AllAck && !action)
                {
                    Stage(3); PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerList.First(p => !p.IsMasterClient));
                }
            }
            else if (stage == 3 && age > 5)
            {
                if (action) return;
                Require(enemy.Boarding.IsAboard && Vector3.Distance(reference, train.transform.InverseTransformPoint(enemy.Root.position)) < .15f, "Moving host migration preserves train attachment");
                Ack();
                if (PhotonNetwork.IsMasterClient && AllAck && !action)
                {
                    session.SetTrainMoving(false);
                    enemy.Teleport(train.transform.TransformPoint(new Vector3(0, .03f, -4.4f)), train.transform.rotation);
                    enemy.Boarding.SetRemoteFrame(true, false); Stage(4); action = true;
                }
            }
            else if (stage == 4)
            {
                if (!PhotonNetwork.IsMasterClient && age > 1.5 && !action)
                { enemy.SubmitSwing(-train.transform.forward * 10, 1, enemy.Root.position); action = true; Record("Submitted non-owner melee RPC"); }
                if (enemy.HasAuthority && enemy.HitMovementActive && !sawMelee)
                { sawMelee = true; Record("PASS authority observed non-owner melee reaction"); }
                // A climbing spider can retain the rear face after a shove.
                // Check support-loss replication independently of that valid adhesion.
                if (age > 5 && enemy.HasAuthority && enemy.Locomotion is ClimbLocomotion && sawMelee && !action)
                {
                    action = true;
                    enemy.SetRootPose(train.transform.TransformPoint(new Vector3(0, .2f, -7)), train.transform.rotation);
                    enemy.Locomotion.Interrupt();
                    enemy.Boarding.BeforeTick(EnemyManager.Instance, .02f);
                    Record("Spider support-loss fixture applied after confirmed melee reaction");
                }
                if (age > 5 && !enemy.Boarding.IsAboard)
                {
                    Ack();
                    if (PhotonNetwork.IsMasterClient && AllAck)
                    {
                        enemy.Teleport(train.transform.TransformPoint(new Vector3(0, .03f, -3)), train.transform.rotation);
                        enemy.Boarding.SetRemoteFrame(true, false); enemy.IsDormant = true; Stage(5);
                    }
                }
            }
            else if (stage == 5 && age > 3)
            {
                Require(enemy.Boarding.IsAboard && Vector3.Distance(train.transform.InverseTransformPoint(enemy.Root.position), new Vector3(0, .03f, -3)) < .15f, "Reattached reference pose stable before late join");
                if (!PhotonNetwork.IsMasterClient && !action)
                { action = true; rejoining = true; Record("Leaving for late re-entry"); session.LeaveCurrentRoom(); }
                if (PhotonNetwork.IsMasterClient && PhotonNetwork.PlayerList.Length == 1)
                { session.SetTrainMoving(true); Stage(6); }
            }
            else if (stage == 6 && age > 4 && PhotonNetwork.PlayerList.Length == 2)
            {
                Require(enemy.Boarding.IsAboard && Vector3.Distance(train.transform.InverseTransformPoint(enemy.Root.position), new Vector3(0, .03f, -3)) < .15f, "Late join receives moving train-relative enemy pose");
                Ack();
                if (PhotonNetwork.IsMasterClient && AllAck && !action)
                {
                    // Transport checks can finish before the first encounter's
                    // graph. Place the stopped train at its authored middle for
                    // the gameplay transfer; do not ask AI to exit into no graph.
                    float distance = RailRoute.StationDepartureLength + RailRoute.Instance.Blocks[0].TrackLength * .5f;
                    if (session.Commit(new Hashtable {
                        [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, distance, 0f, 0f },
                        [Key] = 7 })) action = true;
                }
            }
            else if (stage >= 7 && stage <= 9)
            {
                bool boarding = stage == 8;
                int opening = stage == 9 ? 2 : 1;
                Vector3 targetLocal = boarding ? new Vector3(-.8f, .35f, 0)
                    : new Vector3(stage == 7 ? -7 : 7, -.65f, 0);
                if (NewPlayer.instance != null)
                    NewPlayer.instance.Warp(train.transform.TransformPoint(targetLocal), train.transform.rotation);
                if (!action && age > 1)
                {
                    using (var so = new SerializedObject((MonoBehaviour)enemy.Locomotion))
                    {
                        so.FindProperty("walkSpeed").floatValue = enemy.Locomotion is ClimbLocomotion ? .7f : 1.8f;
                        so.FindProperty("chaseSpeed").floatValue = enemy.Locomotion is ClimbLocomotion ? 1.8f : 3.2f;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (enemy.HasAuthority) enemy.SetRootPose(enemy.Root.position,
                        Quaternion.LookRotation(train.transform.TransformPoint(targetLocal) - enemy.Root.position));
                    action = true; Record("Ordinary pursuit enabled: " + (boarding ? "board " : "exit ") + RailTrainNavigation.OpeningName(opening));
                }
                if (enemy.HasAuthority && enemy.Boarding.Crossing && !sawExit)
                {
                    Require(enemy.Boarding.SelectedOpening == opening, "Selected expected side opening");
                    sawExit = true; Record("Observed committed " + (boarding ? "boarding" : "exit")
                        + " through " + RailTrainNavigation.OpeningName(opening));
                }
                bool arrived = boarding ? enemy.Boarding.IsAboard : !enemy.Boarding.IsAboard && !enemy.Boarding.Crossing
                    && !RailTrainObstacle.Contains(enemy.Root.position)
                    && EnemyManager.Instance.WorldGraph.FindNearestGroundNode(enemy.Root.position, 1.5f) >= 0;
                if (age > 5 && (!enemy.HasAuthority || sawExit) && arrived)
                {
                    Require(RailDiagnostics.Snapshot().objects.Any(s => s.Contains("enemy=") && s.Contains("transfer=")), "Enemy transfer state included in diagnostic snapshot");
                    Ack(); if (PhotonNetwork.IsMasterClient && AllAck) Stage(stage + 1);
                }
            }
            else if (stage == 10)
            { Record("PASS actual two-client carry, moving host migration, non-owner hit/support-loss detachment, leave/rejoin, left exit/reboard, right exit and diagnostic snapshot"); done = true; }
        }
        catch (Exception e) { Record("FAIL " + e.Message); done = true; }
    }
}
// Transport assertions require no AI step between ownership change and the
// editor's next polling tick. Proxies remain awake for normal interpolation.
public sealed class RailBoardingFixtureFreeze : MonoBehaviour, IOnPhotonViewControllerChange
{
    PhotonView view;
    void OnEnable() { view = GetComponent<PhotonView>(); view.AddCallback<IOnPhotonViewControllerChange>(this); }
    void OnDisable() { if (view != null) view.RemoveCallback<IOnPhotonViewControllerChange>(this); }
    public void OnControllerChange(Photon.Realtime.Player current, Photon.Realtime.Player previous)
        => RailEnemyBoardingNetworkChecks.UpdateDormancy(GetComponent<Enemy>());
}
#endif
