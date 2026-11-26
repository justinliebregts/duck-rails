#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Two authenticated MPPM clients in a running expedition: real trigger input fires the Huggy flare gun,
/// peers simulate the same shot, cooldown blocks early presses, the charge survives a handover, an enemy
/// deflection is replicated, and a peer that rejoins gets the charge but no replay of the old shot.
/// </summary>
[InitializeOnLoad]
public static class HuggyFlareNetworkChecks
{
    const string Key = "test.huggy.stage", AckKey = "test.huggy.ack", GunKey = "test.huggy.gun",
        PosKey = "test.huggy.pos", ChargeKey = "test.huggy.charge", EnemyKey = "test.huggy.enemy", ReadyKey = "test.huggy.ready";
    const string RunKey = "Rails.HuggyFlare.Network";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static string Root { get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string ArmPath => Root + "/Temp/rails-huggy-arm.txt";
    static string LogPath => Root + "/docs/validation/huggy-flare/network-" + (CurrentPlayer.IsMainEditor ? "main" : "remote") + ".txt";
    static string token, room;
    static int stage = -1, sub, errors, firstKey;
    static double entered, started, nextTick, pressAt = -1, releaseAt = -1;
    static bool done, rejoining, rendezvousAttempted, routeInstalled, recorded, sawHit, aimed, placed;
    static Vector3 releasedRoot;
    static double aimedAt;
    static int spot;
    static bool shotSeen, pushBefore;
    static readonly Vector3[] Spots = { new Vector3(-1.2f, .35f, 2), new Vector3(0, .35f, 2), new Vector3(1.2f, .35f, 2), new Vector3(0, .35f, -2),
        new Vector3(-1.2f, .35f, -4), new Vector3(1.2f, .35f, -4), new Vector3(0, .35f, -6), new Vector3(0, .35f, 5) };
    const string Pos3Key = "test.huggy.pos3", FireKey = "test.huggy.fire", ReleasedKey = "test.huggy.released";
    static Func<string, RailRouteState> previousRoute;

    static ScriptableObject Scenario => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    static HuggyFlareNetworkChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (stage < 0 || done || (type != LogType.Exception && type != LogType.Error)) return;
            if ((message + trace).IndexOf("Flare", StringComparison.OrdinalIgnoreCase) < 0) return;
            errors++; Record("ERROR " + type + ": " + message.Split('\n')[0]);
        };
    }

    [MenuItem("Duck Rails/Tests/Huggy Flare Gun/Run Two Client Network Check")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Scenario == null) throw new Exception("Stop Play Mode first");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 test account first");
        var manager = Scenario; var property = manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(RunKey + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(manager)));
        SessionState.SetBool(RunKey + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(RunKey, true);
        Directory.CreateDirectory(Root + "/docs/validation/huggy-flare");
        File.WriteAllText(Root + "/docs/validation/huggy-flare/network-main.txt", "");
        File.WriteAllText(Root + "/docs/validation/huggy-flare/network-remote.txt", "");
        File.WriteAllText(ArmPath, DateTime.UtcNow.AddMinutes(12).ToString("O"));
        RailEditorQuickStart.Enabled = true;
        property.SetValue(manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(manager, null);
    }

    [MenuItem("Duck Rails/Tests/Huggy Flare Gun/Stop Network Check")]
    public static void Stop()
    {
        File.WriteAllText(ArmPath, "");
        var manager = Scenario;
        if (manager != null) manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(manager, null);
        else EditorApplication.isPlaying = false;
    }

    static void Record(string message)
    { File.AppendAllText(LogPath, DateTime.UtcNow.ToString("O") + " " + message + "\n"); Debug.Log("HUGGY NETWORK: " + message); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Stage(int value, Hashtable extra = null)
    {
        var props = extra ?? new Hashtable(); props[Key] = value;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }
    static bool Acked => PhotonNetwork.LocalPlayer.CustomProperties[AckKey] is int v && v == stage;
    static void Ack(string message = null)
    {
        if (Acked) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [AckKey] = stage });
        Record("ACK " + stage + " actor=" + PhotonNetwork.LocalPlayer.ActorNumber + " master=" + PhotonNetwork.IsMasterClient + (message != null ? " " + message : ""));
    }
    static bool AllAck => PhotonNetwork.PlayerList.Length == 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties[AckKey] is int v && v == stage);
    static object F(object target, string name) => target.GetType().GetField(name, Any)?.GetValue(target)
        ?? target.GetType().GetProperty(name, Any)?.GetValue(target);
    static IDictionary Shots(HuggyFlareGun gun) => (IDictionary)F(gun, "shots");
    static DeathFlare Flare(HuggyFlareGun gun, int key) => Shots(gun)[key] as DeathFlare;
    static int ShotKey(DeathFlare flare) => (int)F(flare, "ShotKey");
    static string Phase(DeathFlare flare) => F(flare, "shotPhase").ToString();
    static PlayerHand Hand => GR.Player.instance != null ? GR.Player.instance.rightPlayerHand : null;

    // ForceGrab keeps the item's current offset; a player grabs at the hand, so bring the gun there first.
    static bool GrabInHand(PhotonGrabbable grab)
    {
        var hand = Hand;
        if (hand == null) return false;
        grab.transform.SetPositionAndRotation(hand.transform.position, hand.transform.rotation);
        if (grab.TryGetComponent(out Rigidbody body)) { body.position = hand.transform.position; body.rotation = hand.transform.rotation; }
        return hand.ForceGrab(grab);
    }

    static void Input(bool grip, bool trigger)
    {
        var rig = SimulatedRigInput.Instance;
        if (rig == null) return;
        rig.UseInjectedInput = true;
        var frame = rig.InjectedInput; frame.grip = grip; frame.trigger = trigger; rig.InjectedInput = frame;
    }
    static void ClearInput()
    {
        var rig = SimulatedRigInput.Instance;
        if (rig != null) { rig.InjectedInput = default; rig.UseInjectedInput = false; }
    }
    // A short trigger pulse through the real simulated-rig button path (PlayerHand.WasUsePressed).
    static void Press() { pressAt = EditorApplication.timeSinceStartup; releaseAt = pressAt + 0.3; }
    static void PumpPress(bool grip)
    {
        double now = EditorApplication.timeSinceStartup;
        Input(grip, pressAt >= 0 && now >= pressAt && now < releaseAt);
        if (releaseAt >= 0 && now >= releaseAt) pressAt = releaseAt = -1;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .05;
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
        if (arm != token)
        {
            token = arm; stage = -1; errors = 0; done = rejoining = rendezvousAttempted = false; started = EditorApplication.timeSinceStartup;
            Record("Started two-client Huggy flare gun check");
        }
        if (!routeInstalled)
        {
            previousRoute = RailTestTools.OrderedRoute;
            RailTestTools.OrderedRoute = run => RailRoute.ChooseOrdered(run, new[] { "roadside-motel-v1", "maintenance-depot-v1", "lakeside-campground-v1" }
                .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/" + id + ".prefab").GetComponent<RailBlock>()).ToArray(), 12345);
            routeInstalled = true;
        }
        if (done) return;
        try { Step(); }
        catch (Exception e) { Record("FAIL " + e.Message); done = true; ClearInput(); }
    }

    static void Step()
    {
        Require(EditorApplication.timeSinceStartup - started < 660, "Overall timeout");
        var session = PhotonSession.Instance;
        if (session == null || !session.WorldReady || session.HasRoomRequest) return;
        if (rejoining && PhotonNetwork.CurrentRoom?.Name != room) { session.RequestJoinCode(room); return; }
        if (!rejoining && !rendezvousAttempted && PhotonSession.Phase == RailPhase.Compound)
        {
            rendezvousAttempted = true;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
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
            stage = current; sub = 0; recorded = sawHit = aimed = placed = shotSeen = pushBefore = false; spot = 0; aimedAt = 0; entered = EditorApplication.timeSinceStartup;
            Record("Stage=" + stage + " room=" + room + " players=" + PhotonNetwork.PlayerList.Length);
        }
        double age = EditorApplication.timeSinceStartup - entered;
        Require(age < 120, "Timeout stage=" + stage + " sub=" + sub);
        var train = RailTrain.Instance;
        int gunId = PhotonSession.Get(GunKey, 0);
        var gun = gunId > 0 ? PhotonView.Find(gunId)?.GetComponent<HuggyFlareGun>() : null;
        var grab = gun != null ? gun.GetComponent<PhotonGrabbable>() : null;
        bool shooter = PhotonSession.Get("test.huggy.shooter", 0) == PhotonNetwork.LocalPlayer.ActorNumber;
        if (stage < 5 && NewPlayer.instance != null && (age < 1.5 || !train.Contains(NewPlayer.instance.transform.position)))
            // The rig root sits below the duck, so this re-warps every tick; keep the shooter on its chosen spot.
            NewPlayer.instance.Warp(train.transform.TransformPoint(stage == 3 && shooter ? Spots[spot]
                : new Vector3(PhotonNetwork.IsMasterClient ? 1.2f : -1.2f, .35f, 2)), train.transform.rotation);

        if (stage == 0)
        {
            Ack();
            if (PhotonNetwork.IsMasterClient && AllAck && age > 4 && sub == 0)
            {
                sub = 1; session.SetTrainMoving(false);
                var go = PhotonNetwork.Instantiate("Weapon_Huggy", train.transform.TransformPoint(new Vector3(1.2f, 1f, 2.6f)), train.transform.rotation);
                Record("Spawned " + go.name + " view=" + go.GetComponent<PhotonView>().ViewID);
                Stage(1, new Hashtable { [GunKey] = go.GetComponent<PhotonView>().ViewID, ["test.huggy.shooter"] = PhotonNetwork.LocalPlayer.ActorNumber });
            }
            return;
        }
        if (gun == null) return;

        if (stage == 1) // Master fires once; early second press is blocked; peers trace the same shot.
        {
            if (shooter)
            {
                if (grab.Holder == null && sub == 0) { Input(true, false); if (GrabInHand(grab)) { sub = 1; Record("Grabbed gun; charged=" + gun.IsCharged); } return; }
                PumpPress(true);
                if (sub == 1 && gun.GetComponent<PhotonView>().IsMine && age > 3) { Require(gun.IsCharged, "Fresh gun starts charged"); Press(); sub = 2; Record("Trigger press 1"); }
                else if (sub == 2 && Shots(gun).Count == 1)
                {
                    var flare = Shots(gun).Values.Cast<DeathFlare>().First(); firstKey = ShotKey(flare);
                    var muzzle = (Transform)F(gun, "muzzle");
                    Require(flare.IsShot && flare.IsBurning, "Local shot flare burning");
                    Require(Vector3.Distance(flare.transform.position, muzzle.position) < 3f, "Flare starts at the fingertip");
                    Record($"PASS fired via trigger key={firstKey:X8} charge={gun.Charge01:F2} muzzle={muzzle.position} dir={muzzle.forward}");
                    sub = 3;
                }
                else if (sub == 3 && age > 9 && pressAt < 0) { Press(); sub = 4; Record("Trigger press 2 during cooldown, charge=" + gun.Charge01.ToString("F2")); }
                else if (sub == 4 && pressAt < 0 && age > 10)
                {
                    Require(Shots(gun).Count == 1 && !gun.IsCharged, "Second press during cooldown fired nothing");
                    Record("PASS cooldown blocked the early press"); sub = 5;
                }
            }
            else if (sub == 0 && Shots(gun).Count == 1)
            {
                var flare = Shots(gun).Values.Cast<DeathFlare>().First(); firstKey = ShotKey(flare);
                Require(flare.IsShot && flare.IsBurning, "Peer flare burning");
                Require(gun.Charge01 < 0.3f, "Peer sees the gun emptied, charge=" + gun.Charge01.ToString("F2"));
                Record($"PASS peer received shot key={firstKey:X8} charge={gun.Charge01:F2} pos={flare.transform.position}");
                sub = 5;
            }
            // Both clients sample the shot when the gun is half charged (shot age ~5 s).
            if ((shooter ? sub >= 3 : sub == 5) && !recorded && gun.Charge01 >= 0.5f)
            {
                var flare = Flare(gun, firstKey);
                Require(flare != null && flare.IsBurning, "Flare still burning at 5 s");
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [PosKey] = flare.transform.position });
                Record($"Sample at charge {gun.Charge01:F2}: pos={flare.transform.position} phase={Phase(flare)}");
                recorded = true;
            }
            if (recorded && (!shooter || sub >= 5) && PhotonNetwork.PlayerList.All(p => p.CustomProperties[PosKey] is Vector3))
            {
                var a = (Vector3)PhotonNetwork.PlayerList[0].CustomProperties[PosKey];
                var b = (Vector3)PhotonNetwork.PlayerList[1].CustomProperties[PosKey];
                Require(Vector3.Distance(a, b) < 0.75f, $"Both clients trace the same flight: {a} vs {b}");
                Ack($"same shot within {Vector3.Distance(a, b):F2} m");
            }
            if (PhotonNetwork.IsMasterClient && AllAck)
            {
                Hand?.Release(); Input(false, false);
                Stage(2, new Hashtable { ["test.huggy.shooter"] = PhotonNetwork.PlayerList.First(p => !p.IsLocal).ActorNumber });
            }
        }
        else if (stage == 2) // Handover keeps recharging, then the peer can fire when full.
        {
            if (shooter)
            {
                if (grab.Holder == null && !grab.IsHeldByAnyPlayer && age > 1.5 && sub == 0)
                {
                    Input(true, false);
                    if (GrabInHand(grab))
                    {
                        sub = 1; float charge = gun.Charge01;
                        Require(charge > 0.4f && charge < 1f, "New holder sees the gun still recharging, charge=" + charge.ToString("F2"));
                        Record("PASS handover kept charge " + charge.ToString("F2"));
                    }
                    return;
                }
                if (sub >= 1) Input(true, false);
                if (sub == 1 && gun.IsCharged) { Record("PASS recharged to full after 10 s and stays full"); sub = 2; }
            }
            else if (!shooter && sub == 0 && gun.IsCharged) { sub = 2; Record("Peer sees charge full"); }
            if (sub == 2 && (!shooter || gun.GetComponent<PhotonView>().IsMine)) Ack();
            if (PhotonNetwork.IsMasterClient && AllAck) Stage(3);
        }
        else if (stage == 3) // Non-master fires at a real enemy: deflection replicated, enemy pushed.
        {
            int enemyId = PhotonSession.Get(EnemyKey, 0);
            var enemy = enemyId > 0 ? PhotonView.Find(enemyId)?.GetComponent<Enemy>() : null;
            var muzzle = (Transform)F(gun, "muzzle");
            if (shooter)
            {
                PumpPress(true);
                // The desktop hand cannot be steered, so move the duck around the car until the finger has a
                // clear 3.5 m line of fire inside the train, then publish the aim point for the enemy.
                if (!(PhotonNetwork.LocalPlayer.CustomProperties[ReadyKey] is Vector3) && age > 2.5
                    && EditorApplication.timeSinceStartup - aimedAt > 1.2 && NewPlayer.instance != null)
                {
                    bool clear = !Physics.SphereCast(muzzle.position, 0.12f, muzzle.forward, out var block, 3.5f, (1 << 0) | (1 << 6), QueryTriggerInteraction.Ignore);
                    Vector3 aim = Vector3.zero, under = Vector3.zero; bool floored = false;
                    foreach (float d in new[] { 2.5f, 2f, 1.6f })
                    {
                        aim = muzzle.position + muzzle.forward * d;
                        if (Physics.Raycast(aim, Vector3.down, out var down, 2f, (1 << 0) | (1 << 6), QueryTriggerInteraction.Ignore)) { floored = true; under = down.point; break; }
                    }
                    // The car volume is low, so test the floor point under the aim rather than the aim itself.
                    if (clear && floored && train.Contains(under + Vector3.up * 0.2f))
                    {
                        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [ReadyKey] = aim });
                        Record($"Aimed from spot {spot}: muzzle={muzzle.position} fwd={muzzle.forward} aim={aim}");
                    }
                    else
                    {
                        Require(spot < Spots.Length - 1, "No spot in the car gives a clear line of fire");
                        spot++; aimedAt = EditorApplication.timeSinceStartup;
                        Record($"Spot {spot - 1} blocked by {(clear ? (floored ? "aim outside car" : "no floor under aim") :block.collider.name + " at " + block.distance.ToString("F2"))}; muzzle fwd={muzzle.forward}; moving");
                        NewPlayer.instance.Warp(train.transform.TransformPoint(Spots[spot]), train.transform.rotation);
                    }
                }
            }
            var shooterPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == PhotonSession.Get("test.huggy.shooter", 0));
            if (PhotonNetwork.IsMasterClient && enemyId == 0 && !placed && shooterPlayer?.CustomProperties[ReadyKey] is Vector3 target)
            {
                enemy = Enemy.Active.Where(e => e != null && e.HasAuthority && e.Boarding != null && !e.IsDormant)
                    .OrderBy(e => e.Locomotion is ClimbLocomotion ? 1 : 0).FirstOrDefault();
                if (enemy == null) { Require(age < 40, "No authoritative boarding enemy to target"); return; }
                Vector3 dir = Vector3.ProjectOnPlane(target - muzzle.position, Vector3.up).normalized;
                Require(Physics.Raycast(target, Vector3.down, out var floor, 2f, (1 << 0) | (1 << 6), QueryTriggerInteraction.Ignore),
                    "Train floor under the aim point");
                Vector3 feet = floor.point;
                using (var mover = new SerializedObject((MonoBehaviour)enemy.Locomotion))
                {
                    mover.FindProperty("walkSpeed").floatValue = 0; mover.FindProperty("chaseSpeed").floatValue = 0;
                    mover.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var trigger in enemy.GetComponentsInChildren<KillTrigger>()) trigger.enabled = false;
                // Hold the enemy aboard and dormant (as the boarding fixture does) with its torso on the line
                // of fire; it is released just before the shot so the push is applied.
                // The RailZombie hit shape is only ~0.9 m tall, so it is held up to the finger's height and drops
                // once released. Place it with the line of fire 0.35 m above the root, then fire low on the torso.
                Vector3 root = feet + Vector3.up * Mathf.Max(0f, target.y - feet.y - 0.35f);
                enemy.Teleport(root, Quaternion.LookRotation(-dir));
                enemy.Boarding.SetRemoteFrame(true, false); enemy.IsDormant = true;
                placed = true;
                Record($"Placed {enemy.name} view={enemy.GetComponent<PhotonView>().ViewID} root={root} aim={target} (aim {target.y - feet.y:F2} m above floor)");
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [EnemyKey] = enemy.GetComponent<PhotonView>().ViewID });
                return;
            }
            if (enemy == null) return;
            if (PhotonNetwork.IsMasterClient && enemy.IsDormant && shooterPlayer?.CustomProperties[FireKey] is bool)
            {
                enemy.IsDormant = false; Record("Released " + enemy.name + " for the shot");
                // IsDormant is local; the authority drops hits while its copy is dormant, so the shooter waits for this.
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [ReleasedKey] = enemyId });
            }
            if (shooter && sub == 1 && pressAt < 0 && PhotonNetwork.CurrentRoom.CustomProperties[ReleasedKey] is int released && released == enemyId)
            {
                pressAt = EditorApplication.timeSinceStartup; releaseAt = pressAt + 0.3; sub = 2;
                Record("Authority released the enemy; pressing the trigger");
            }
            if (shooter && sub == 0 && gun.IsCharged)
            {
                // Fire once this client's copy of the enemy stands in front of the finger with its torso on the line.
                Vector3 to = enemy.Root.position - muzzle.position;
                float along = Vector3.Dot(to, muzzle.forward);
                float off = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(to, muzzle.forward), Vector3.up).magnitude;
                float height = muzzle.position.y - enemy.Root.position.y;
                // The desktop hand bobs 2.3-2.7 m. Release drops the held enemy (4.9 t^2: ~0.1-0.3 m before the
                // flare arrives), so fire low on the torso, 0.25-0.45 m above the root, and press quickly; the
                // line then still crosses the ~0.9 m hit shape instead of passing over a falling enemy.
                if (along > 1.2f && along < 5f && off < 0.4f && height > 0.25f && height < 0.45f)
                {
                    Record($"Firing at enemy: along={along:F2} sideways={off:F2} muzzleAboveRoot={height:F2} enemy={enemy.Root.position} muzzle={muzzle.position}");
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [FireKey] = true });
                    sub = 1; // Press once the authority has released the enemy (below).
                }
                else Require(age < 60, $"Enemy never stood in front of the gun: along={along:F2} sideways={off:F2} muzzleAboveRoot={height:F2}");
            }
            int newest = Shots(gun).Keys.Cast<int>().Where(k => k != firstKey).DefaultIfEmpty(0).Max();
            var shot = newest != 0 ? Flare(gun, newest) : null;
            if (shot != null && !recorded && gun.Charge01 > 0.35f)
            {
                int bounces = (int)F(shot, "shotBounces");
                // The hit pushes the enemy away and the flare rebounds at bounceKeep speed (back, or up and over on a
                // graze), so judge the landing on the floor plan against where the enemy stood when the shot was
                // fired (the published aim point it was placed on). A miss would fly on for up to 25 m.
                Vector3 hitSpot = shooterPlayer?.CustomProperties[ReadyKey] is Vector3 aimPoint ? aimPoint : enemy.Root.position;
                Vector3 offset = Vector3.ProjectOnPlane(shot.transform.position - hitSpot, Vector3.up);
                Vector3 fire = Vector3.ProjectOnPlane(muzzle.forward, Vector3.up).normalized;
                float near = offset.magnitude, past = Vector3.Dot(offset, fire);
                float fromNow = Vector3.Distance(shot.transform.position, enemy.Root.position);
                Record($"Shot sample: phase={Phase(shot)} bounces={bounces} travel={(float)F(shot, "shotTravel"):F2} pos={shot.transform.position} fromAimFlat={near:F2} pastAim={past:F2} fromEnemyNow={fromNow:F2} enemyNow={enemy.Root.position}");
                Require(bounces > 0, "Flare deflected (shooter hit or replicated deflect RPC)");
                Require(near < 6f, "Flare came down near where the enemy was hit, " + near.ToString("F2") + " m");
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [Pos3Key] = shot.transform.position });
                recorded = true;
            }
            // Count the push by the authority's enemy sliding across the floor after its release. Its walk and chase
            // speeds are zeroed and the release drop is vertical, so only the hit moves it sideways. HitMovementActive
            // was unusable here: the release drop and flare-fear retreat already set it, hiding the push's start.
            if (enemy.HasAuthority && !enemy.IsDormant && shooterPlayer?.CustomProperties[FireKey] is bool && !sawHit)
            {
                if (!shotSeen) { shotSeen = true; releasedRoot = enemy.Root.position; }
                else
                {
                    float slid = Vector3.ProjectOnPlane(enemy.Root.position - releasedRoot, Vector3.up).magnitude;
                    if (slid > 0.5f) { sawHit = true; Record($"PASS authority observed the flare push on {enemy.name}: slid {slid:F2} m"); }
                }
            }
            if (recorded && (!enemy.HasAuthority || sawHit) && PhotonNetwork.PlayerList.All(p => p.CustomProperties[Pos3Key] is Vector3))
            {
                var a = (Vector3)PhotonNetwork.PlayerList[0].CustomProperties[Pos3Key];
                var b = (Vector3)PhotonNetwork.PlayerList[1].CustomProperties[Pos3Key];
                Require(Vector3.Distance(a, b) < 0.75f, $"Enemy deflection followed on both clients: {a} vs {b}");
                bool fearing = (bool)F(enemy, "_flareRetreat") || enemy.IsFlareProtected(enemy.Root.position);
                Ack($"deflected shot within {Vector3.Distance(a, b):F2} m on both; flare-fear={fearing}");
            }
            if (PhotonNetwork.IsMasterClient && AllAck) Stage(4);
        }
        else if (stage == 4) // Peer leaves and rejoins: charge restored, old shot not replayed.
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                if (sub == 0) { sub = 1; ClearInput(); Hand?.Release(); rejoining = true; Record("Leaving for re-entry with live shot flares"); session.LeaveCurrentRoom(); return; }
                if (sub == 1 && PhotonNetwork.CurrentRoom?.Name == room && PhotonNetwork.PlayerList.Length == 2 && age > 8)
                {
                    rejoining = false;
                    int burning = Shots(gun).Count;
                    Record($"Rejoined: shots replayed={burning} charge={gun.Charge01:F2}");
                    Require(burning == 0, "Old shots are not replayed for a rejoining peer");
                    if (PhotonNetwork.PlayerList.FirstOrDefault(p => p.IsMasterClient)?.CustomProperties[ChargeKey] is float master)
                    {
                        Require(Mathf.Abs(master - gun.Charge01) < 0.2f, $"Charge restored on rejoin: {gun.Charge01:F2} vs master {master:F2}");
                        Ack("charge matches master " + master.ToString("F2"));
                    }
                }
            }
            else
            {
                if (sub++ % 20 == 0) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [ChargeKey] = gun.Charge01 });
                if (PhotonNetwork.PlayerList.Length == 2 && age > 8) Ack();
                if (AllAck) Stage(5);
            }
        }
        else if (stage == 5)
        {
            ClearInput();
            Require(errors == 0, errors + " flare-related errors/exceptions logged");
            Record("PASS two-client Huggy flare gun: trigger fire, shared flight, cooldown, handover charge, enemy deflection, rejoin charge without replay");
            done = true;
        }
    }
}
#endif
