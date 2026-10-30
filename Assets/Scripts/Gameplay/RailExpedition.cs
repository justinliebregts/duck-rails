using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Authoritative Week 2 encounter, rescue, furnace and bridge lifecycle.</summary>
public sealed class RailExpedition : MonoBehaviour, IOnEventCallback
{
    public const string StateKey = "rails.expedition";
    const byte ActionEvent = 108;
    public static RailExpedition Instance { get; private set; }
    public GameObject[] supplies;
    public Transform[] supplyPoints, coalPoints, enemyPoints;
    public GameObject coalPrefab, enemyPrefab;
    public Transform furnace, bridgeButton, bridgeDeck;
    public TMP_Text gauge, bridgeLabel, results;
    public TMP_Text[] runningLabels;
    public Material explosionMaterial;
    public Game.AI.EnemyManager enemies;
    public float bridgeZ = 550;
    public int baseEnemyCount = 2;
    public DeathFlare flarePrefab;
    public RailRunState State { get; private set; } = new RailRunState();
    readonly Queue<(int actor, string action, int target, object[] coal)> requests = new Queue<(int, string, int, object[])>();
    readonly Dictionary<string, double> shownFlares = new Dictionary<string, double>();
    string lastJson, localRun, caughtRun;
    string pendingRequestJson;
    double pendingRequestAt;
    bool wasDown;
    double nextTick, nextDownRequest, fallAt;
    AudioSource reviveAudio;
    AudioClip reviveClip;
    readonly Dictionary<string, TMP_Text> downLabels = new Dictionary<string, TMP_Text>();
    GameObject explosion;
    readonly HashSet<string> carrierHits = new HashSet<string>();
    readonly HashSet<string> issuedDrops = new HashSet<string>();
    public void CarrierHit(string marker) { if (PhotonSession.Phase == RailPhase.Running && State.carriers.Any(c => c.marker == marker && c.carrying)) carrierHits.Add(marker); }
    public static bool LocalDown => Instance != null && Instance.LocalParticipant?.down == true && PhotonSession.OnTrain;
    public RailParticipant LocalParticipant => State.Player(PhotonNetwork.LocalPlayer?.UserId);
    public static bool ActorDown(int actor) => Instance != null && Instance.State.players.Any(p => p.actor == actor && p.down);

    void Awake()
    {
        Instance = this;
        foreach (var prefab in supplies) PhotonPreparePool.Register(prefab);
        PhotonPreparePool.Register(coalPrefab); PhotonPreparePool.Register(enemyPrefab);
        reviveClip = RailControl.Tone("Revive", 330, 1);
        reviveAudio = gameObject.AddComponent<AudioSource>(); reviveAudio.clip = reviveClip; reviveAudio.loop = true; reviveAudio.volume = .12f;
    }
    void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); requests.Clear(); if (reviveAudio != null) reviveAudio.Stop(); }
    void OnDestroy() { if (Instance == this) Instance = null; if (reviveClip != null) Destroy(reviveClip); }
    public static bool CanTarget(PhotonPlayer player)
    {
        if (!PhotonSession.IsRailsRoom || Instance == null) return true;
        if (PhotonSession.Phase != RailPhase.Running || ActorDown(player.photonView.OwnerActorNr)) return false;
        return player.TryGetHeadPosition(out var head) && RailTrain.Instance != null && !RailTrain.Instance.Contains(head) &&
            (RailRoute.Instance != null ? RailRoute.Instance.Ready && RailRoute.Instance.IsEncounter(head) : head.x > 4 && head.z >= 140 && head.z <= 550);
    }
    public static void RequestDown()
    {
        var instance = Instance;
        if (instance == null || PhotonNetwork.Time < instance.nextDownRequest || LocalDown) return;
        instance.nextDownRequest = PhotonNetwork.Time + .3;
        instance.Request("down");
    }
    public void Request(string action, int target = 0)
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || PhotonNetwork.LocalPlayer == null) return;
        PhotonNetwork.RaiseEvent(ActionEvent, new object[] { PhotonSession.RunId, action, target },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void OnEvent(EventData e)
    {
        if (e.Code != ActionEvent || !PhotonNetwork.IsMasterClient || PhotonSession.Phase != RailPhase.Running) return;
        if (!(e.CustomData is object[] d) || (d.Length != 3 && d.Length != 4) || d[0] as string != PhotonSession.RunId || !(d[1] is string action) || !(d[2] is int target)) return;
        if (requests.Count < 64) requests.Enqueue((e.Sender, action, target, d.Length == 4 ? d[3] as object[] : null));
    }
    public void RequestCoal(PhotonView view, PhotonGrabbable item)
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || view == null || !view.IsMine ||
            item == null || item.IsHeldByAnyPlayer || item.IsStashed || furnace == null) return;
        var sample = new object[] { view.SpawnToken, item.HoldVersion,
            Quaternion.Inverse(furnace.rotation) * (view.transform.position - furnace.position), PhotonNetwork.Time };
        PhotonNetwork.RaiseEvent(ActionEvent, new object[] { PhotonSession.RunId, "coal", view.ViewID, sample },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public static bool Position(int actor, out Vector3 point)
    {
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber && GR.Player.instance != null)
        { point = GR.Player.instance.transform.position + Vector3.up * .8f; return true; }
        var p = PhotonPlayer.ForActor(actor);
        if (p != null && p.TryGetHeadPosition(out point)) return true;
        point = default; return false;
    }
    static bool HandNear(int actor, Vector3 point, float radius)
    {
        var p = PhotonPlayer.ForActor(actor);
        if (p == null) return false;
        for (int i = 0; i < 2; i++) if (p.TryGetHandPose(i == 0, out var hand, out _) && Vector3.Distance(hand, point) <= radius) return true;
        return false;
    }
    static bool RescuePosition(int actor, out Vector3 point)
    {
        var avatar = PhotonPlayer.ForActor(actor);
        if (avatar != null && avatar.TryGetHeadPosition(out point)) return true;
        return Position(actor, out point);
    }
    public static bool TryGetDesktopRescuePoint(Ray aim, float reach, int obstructionMask, out Vector3 point)
    {
        point = default;
        if (Instance == null || !PhotonSession.OnTrain || LocalDown) return false;
        float nearest = reach;
        bool found = false;
        foreach (var participant in Instance.State.players)
        {
            if (!participant.down || !IsConnected(participant.actor) ||
                participant.actor == PhotonNetwork.LocalPlayer.ActorNumber ||
                !RescuePosition(participant.actor, out var head)) continue;
            float along = Vector3.Dot(head - aim.origin, aim.direction);
            if (along <= 0 || Vector3.Distance(aim.origin, head) > reach || along > nearest ||
                Vector3.Distance(aim.GetPoint(along), head) > .65f ||
                Physics.Linecast(aim.origin, head, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
            nearest = along; point = head; found = true;
        }
        return found;
    }
    void ReadState()
    {
        string json = PhotonSession.Get(StateKey, "");
        if (json == lastJson && State.run == PhotonSession.RunId) return;
        lastJson = json;
        if (pendingRequestJson != null && json == pendingRequestJson)
        { if (requests.Count > 0) requests.Dequeue(); pendingRequestJson = null; }
        State = string.IsNullOrEmpty(json) ? new RailRunState() : JsonUtility.FromJson<RailRunState>(json);
        if (State.run != PhotonSession.RunId) State = new RailRunState { run = PhotonSession.RunId };
    }
    void Update()
    {
        if (!PhotonSession.IsRailsRoom) return;
        ReadState();
        if (!PhotonSession.OnTrain)
        {
            if (PhotonNetwork.IsMasterClient) foreach (var e in Game.AI.Enemy.Active.ToArray())
                if (e != null && e.TryGetComponent<PhotonView>(out var view) && view.IsMine) PhotonNetwork.Destroy(e.gameObject);
            requests.Clear(); reviveAudio.Stop(); wasDown = false;
            foreach (var label in downLabels.Values) if (label != null) label.gameObject.SetActive(false);
            return;
        }
        if (RailRoute.Instance != null && !RailRoute.Instance.Ready) return;
        if (localRun != PhotonSession.RunId)
        {
            localRun = PhotonSession.RunId; caughtRun = null; shownFlares.Clear(); wasDown = false; requests.Clear(); pendingRequestJson = null;
            RenderSettings.ambientLight = new Color(.36f, .42f, .5f); RenderSettings.fogColor = new Color(.07f, .11f, .17f);
            if (explosion != null) Destroy(explosion);
        }
        Present();
        ConsumeCommittedCoal();
        if (PhotonNetwork.IsMasterClient) SpawnCarrierDrops();
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.Time >= nextTick)
        { nextTick = PhotonNetwork.Time + .1; TickAuthority(); }
    }
    void TickAuthority()
    {
        bool ending = PhotonSession.Phase == RailPhase.Ending && State.finished;
        if (PhotonSession.Phase != RailPhase.Running && !ending) return;
        if (!ending) SpawnEnemies();
        if (pendingRequestJson != null && PhotonNetwork.Time - pendingRequestAt < 1.2) return;
        pendingRequestJson = null;
        var next = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(State));
        double now = PhotonNetwork.Time;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            var p = next.Player(player.UserId);
            if (p == null) { p = new RailParticipant { id = player.UserId, name = player.NickName }; next.players.Add(p); }
            p.actor = player.ActorNumber; p.name = player.NickName;
        }
        var changes = new Hashtable();
        if (!ending && RailRoute.Instance != null) UpdateCarriers(next, now);
        // Process one request per CAS. Keep it queued until the server's snapshot acknowledges it.
        if (!ending && requests.Count > 0)
        {
            var request = requests.Peek();
            var p = next.players.Find(x => x.actor == request.actor);
            if (p != null && Position(request.actor, out var position))
                ApplyRequest(next, p, position, request.action, request.target, now, changes, request.coal);
        }
        foreach (var p in next.players.Where(p => p.down))
        {
            var rescuer = next.players.FirstOrDefault(r => !r.down && IsConnected(r.actor) && r.id != p.id &&
                RescuePosition(p.actor, out var pos) && HandNear(r.actor, pos, .95f));
            if (rescuer == null) { p.rescuer = ""; p.reviveAt = 0; }
            else if (p.rescuer != rescuer.id || p.reviveAt <= 0) { p.rescuer = rescuer.id; p.reviveAt = now; }
            else if (now - p.reviveAt >= RailRunState.ReviveSeconds)
            { p.down = false; p.protectedUntil = now + RailRunState.ProtectionSeconds; p.reviveAt = 0; p.rescuer = ""; if (!ending) rescuer.revives++; }
        }
        if (ending)
        {
            // Teammates may still help one another during the ride. Locked finish
            // credit and gameplay statistics remain unchanged.
            requests.Clear();
            string rescued = JsonUtility.ToJson(next);
            if (rescued != lastJson) PhotonSession.Instance.Commit(new Hashtable { [StateKey] = rescued });
            return;
        }
        bool allDown = next.players.Any(p => IsConnected(p.actor)) && next.players.Where(p => IsConnected(p.actor)).All(p => p.down);
        if (!allDown) next.allDownAt = 0;
        else if (next.allDownAt <= 0) next.allDownAt = now;
        var train = RailTrain.Instance;
        if (train != null && train.transform.position.z + 5 >= bridgeZ)
        {
            if (next.BridgeProgress(now) < 1) next.Finish(now, "Bridge collision", Array.Empty<string>());
            else next.Finish(now, "Bridge crossed", next.players.Where(p => IsConnected(p.actor) && Position(p.actor, out var point) && train.Contains(point)).Select(p => p.id));
        }
        if (!next.finished && next.allDownAt > 0 && now - next.allDownAt >= RailRunState.AllDownGrace)
            next.Finish(now, "Crew down", Array.Empty<string>());
        if (next.finished)
        {
            var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
            float distance = train != null ? train.Distance : 0;
            changes[RailSessionRules.Motion] = new object[] { now, distance, next.success ? 4f : 0f, next.success ? 4f : 0f };
            // Ending has a fuel budget far beyond its fixed 32-second ride.
            changes[RailSessionRules.Fuel] = next.success ? 100f : PhotonSession.CurrentFuel;
            changes[RailSessionRules.Phase] = (int)RailPhase.Ending;
            changes[RailSessionRules.Deadline] = now + 32;
            changes["rails.success"] = next.success;
        }
        string json = JsonUtility.ToJson(next);
        // Invalid/repeated requests make no state change, so no property callback
        // can acknowledge them. Discard them without blocking subsequent actions.
        if (json == lastJson && changes.Count == 0)
        { if (requests.Count > 0) requests.Dequeue(); return; }
        changes[StateKey] = json;
        if (PhotonSession.Instance.Commit(changes) && requests.Count > 0)
        { pendingRequestJson = json; pendingRequestAt = now; }
    }
    static bool IsConnected(int actor) => PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.IsInactive == false;
    void ApplyRequest(RailRunState state, RailParticipant p, Vector3 position, string action, int target, double now, Hashtable changes, object[] coalSample = null)
    {
#if UNITY_EDITOR
        if (action == "editor-fuel")
        {
            var currentMotion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
            RailSessionRules.EvaluateTrain(currentMotion, PhotonSession.Get(RailSessionRules.Fuel, 10f), now,
                out var distance, out var velocity, out var remainingFuel);
            changes[RailSessionRules.Motion] = new object[] { now, distance, velocity, (float)currentMotion[3] };
            changes[RailSessionRules.Fuel] = remainingFuel + 1f;
            return;
        }
        // Only the requesting participant is changed; production masters omit this action.
        if (action == "editor-revive")
        {
            if (!p.down) return;
            p.down = false; p.reviveAt = 0; p.rescuer = "";
            p.protectedUntil = now + RailRunState.ProtectionSeconds;
            state.allDownAt = 0;
            return;
        }
#endif
        if (action == "down")
        {
            if (p.down || now < p.protectedUntil || RailTrain.Instance.Contains(position)) return;
            p.down = true; p.downs++;
            if (p.flareAt <= 0 || now - p.flareAt >= RailRunState.FlareCooldown) { p.flareAt = now; p.flarePosition = position; }
            return;
        }
        if (p.down) return;
        if (action == "bridge" && state.bridgeAt <= 0 && Vector3.Distance(position, bridgeButton.position) < 3)
            state.bridgeAt = now;
        if (action == "horn" && RailTrain.Instance.Contains(position) && now - p.hornAt > 1)
        { p.horns++; p.hornAt = now; }
        if (action != "coal") return;
        var view = PhotonView.Find(target);
        if (view == null || state.WasConsumed(target, view.SpawnToken)) return;
        var coal = view != null ? view.GetComponent<RailCoal>() : null;
        var item = view != null ? view.GetComponent<PhotonGrabbable>() : null;
        if (coal == null || item == null || Vector3.Distance(position, furnace.position) > 5) return;
        if (coalSample != null)
        {
            if (coalSample.Length != 4 || !(coalSample[0] is string spawn) || !(coalSample[1] is int version) ||
                !(coalSample[2] is Vector3 offset) || !(coalSample[3] is double sent) ||
                !RailCoal.ValidInsertion(p.actor, view.ControllerActorNr, spawn, view.SpawnToken,
                    version, item.HoldVersion, offset, now - sent)) return;
            // Local authority has newer direct evidence; remote held state can be
            // the pre-release snapshot and must not veto its controller's release.
            if (view.IsMine && (item.IsHeldByAnyPlayer || item.IsStashed)) return;
        }
        else if (item.IsHeldByAnyPlayer || item.IsStashed || Vector3.Distance(coal.transform.position, furnace.position) > .9f) return;
        state.consumed.Add(target); state.consumedSpawns.Add(view.SpawnToken); p.coal++;
        if (view.InstantiationData is object[] data && data.Length >= 3 && data[data.Length - 1] is string marker) state.consumedMarkers.Add(marker);
        var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
        RailSessionRules.EvaluateTrain(motion, PhotonSession.Get(RailSessionRules.Fuel, 10f), now, out var z, out var speed, out var fuel);
        changes[RailSessionRules.Motion] = new object[] { now, z, speed, (float)motion[3] };
        changes[RailSessionRules.Fuel] = Mathf.Min(40f, fuel + 2.5f);
    }
    void ConsumeCommittedCoal()
    {
        foreach (int id in State.consumed)
        {
            var view = PhotonView.Find(id);
            if (view != null && State.WasConsumed(id, view.SpawnToken) && view.GetComponent<RailCoal>() != null && view.IsMine) PhotonNetwork.Destroy(view.gameObject);
        }
    }
    void Present()
    {
        double now = PhotonNetwork.Time;
        if (gauge != null) gauge.text = "FUEL  " + PhotonSession.CurrentFuel.ToString("0.0") + "\nRelease coal into furnace (+2.5)";
        float progress = State.BridgeProgress(now);
        bridgeDeck.localRotation = Quaternion.Euler(-80 * (1 - progress), 0, 0);
        bridgeLabel.text = progress >= 1 ? "BRIDGE READY\nBOARD THE TRAIN" : State.bridgeAt > 0 ? "DEFEND THE CREW\n" + Mathf.CeilToInt((float)(RailRunState.BridgeSeconds - (now - State.bridgeAt))) + " seconds" : "STOP BEFORE BRIDGE\nPress button to lower";
        foreach (var p in State.players)
        {
            if (p.flareAt > 0 && (!shownFlares.TryGetValue(p.id, out var shown) || shown != p.flareAt))
            { shownFlares[p.id] = p.flareAt; DeathFlare.SpawnAfterDeath(flarePrefab, p.flarePosition, (float)(now - p.flareAt)); }
            if (!downLabels.TryGetValue(p.id, out var label))
            {
                var go = new GameObject("Crew condition"); go.transform.SetParent(transform); label = go.AddComponent<TextMeshPro>();
                label.fontSize = 2; label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(3, 1); downLabels[p.id] = label;
            }
            label.gameObject.SetActive(p.down && Position(p.actor, out _));
            if (p.down && Position(p.actor, out var pos))
            {
                label.transform.position = pos + Vector3.up;
                if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
                label.text = p.reviveAt > 0 ? "REVIVING " + Mathf.Clamp01((float)((now - p.reviveAt) / 3)).ToString("P0") : "DOWN\nHold a hand nearby";
                label.color = new Color(1, .75f, .2f);
            }
        }
        var local = LocalParticipant;
        if (local != null && local.down && !wasDown)
        {
            GR.Player.instance?.leftPlayerHand?.Release(); GR.Player.instance?.rightPlayerHand?.Release();
            // Bags hold a locomotion shape outside PlayerHand.Held.
            (PlayerHand.GripHandler as BagInventory)?.DropHandsForUnavailablePlayer();
        }
        wasDown = local?.down == true;
        var reviving = State.players.FirstOrDefault(p => p.down && p.reviveAt > 0 && (p.id == local?.id || p.rescuer == local?.id));
        if (reviving != null) { reviveAudio.pitch = 1 + Mathf.Clamp01((float)((now - reviving.reviveAt) / 3)); if (!reviveAudio.isPlaying) reviveAudio.Play(); }
        else reviveAudio.Stop();
        results.gameObject.SetActive(State.finished);
        if (State.finished)
        {
            if (State.reason == "Bridge collision" && explosion == null)
            {
                explosion = new GameObject("Train explosion"); explosion.transform.position = RailTrain.Instance.transform.position;
                var ps = explosion.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.duration = 1; main.startLifetime = 2; main.startSpeed = 8; main.startSize = 2; main.startColor = new Color(1, .3f, .04f);
                if (explosionMaterial != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = explosionMaterial;
                var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 90) });
                ps.Play();
            }
            results.text = (State.success ? "<color=#8AFFBA>SUCCESS</color>" : "<color=#FF8866>FAIL</color>") + "\n" + State.reason + "\n\n" +
                string.Join("\n", State.players.Select(p => Safe(p.name) + " — " + (p.escaped ? "Escaped" : "Didn't make it") + "\n<size=65%>Coal " + p.coal + "   Revives " + p.revives + "   Downs " + p.downs + "   Honks " + p.horns + "  •  " + Award(p) + "</size>"));
            if (caughtRun != State.run)
            { caughtRun = State.run; if (GR.Player.instance != null && !RailTrain.Instance.Contains(GR.Player.instance.transform.position + Vector3.up * .5f)) TeleportAboard(); }
            if (GR.Player.instance != null && !RailTrain.Instance.Contains(GR.Player.instance.transform.position + Vector3.up))
            { if (fallAt <= 0) fallAt = now; if (now - fallAt > 2) TeleportAboard(); }
            else fallAt = 0;
            if (State.success && now - State.finishedAt > 14)
            { RenderSettings.ambientLight = new Color(.7f, .65f, .5f); RenderSettings.fogColor = new Color(.65f, .73f, .78f); }
        }
        if (Camera.main != null) Camera.main.backgroundColor = RenderSettings.fogColor;
        if (runningLabels != null) foreach (var label in runningLabels) if (label != null) label.gameObject.SetActive(!State.finished);
    }
    public void SeedCargo()
    {
        string run = PhotonSession.RunId;
        bool Has(string marker) => HasMarker(run, marker);
        for (int i = 0; i < supplies.Length; i++)
        {
            string marker = "supply-" + i;
            if (Has(marker)) continue;
            object[] initial = supplies[i].GetComponent<BagObject>() != null ? BagObject.SpawnData(1, new[] { "", "", "", "" }, 0, BagObject.Target.Dropped, 0) : Array.Empty<object>();
            var data = initial.Concat(new object[] { "rails", run, marker }).ToArray();
            PhotonNetwork.InstantiateRoomObject(supplies[i].name, supplyPoints[i].position, supplyPoints[i].rotation, 0, data);
        }
        for (int i = 0; i < coalPoints.Length; i++)
            if (!Has("coal-" + i)) PhotonNetwork.InstantiateRoomObject(coalPrefab.name, coalPoints[i].position, Quaternion.identity, 0, new object[] { "rails", run, "coal-" + i });
    }
    void SpawnEnemies()
    {
        int count = State.bridgeAt > 0 && State.BridgeProgress(PhotonNetwork.Time) < 1 ? enemyPoints.Length : baseEnemyCount;
        for (int i = 0; i < count; i++)
        {
            string marker = "enemy-" + i;
            bool exists = HasMarker(PhotonSession.RunId, marker);
            if (!exists) PhotonNetwork.InstantiateRoomObject(enemyPrefab.name, enemyPoints[i].position, enemyPoints[i].rotation, 0, new object[] { "rails", PhotonSession.RunId, marker });
        }
    }
    static bool HasMarker(string run, string marker)
    {
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 2] as string == run && d[d.Length - 1] as string == marker) return true;
        return false;
    }
    void UpdateCarriers(RailRunState next, double now)
    {
        // One carrier per stop, plus both bridge defenders. Initial equipment is retained by marker.
        foreach (var enemy in Game.AI.Enemy.Active)
        {
            var carrier = enemy.GetComponent<RailCoalCarrier>(); string marker = carrier != null ? carrier.Marker : null;
            if (marker == null || !marker.StartsWith("enemy-")) continue;
            if (!int.TryParse(marker.Substring(6), out int index)) continue;
            var state = next.carriers.Find(c => c.marker == marker);
            if (state == null && (index % 2 == 0 || index >= baseEnemyCount)) { state = new RailCarrierState { marker = marker }; next.carriers.Add(state); }
            if (state != null && state.carrying && carrierHits.Contains(marker))
            { state.carrying = false; state.drops.Add(enemy.Root.position + Vector3.up * .7f); }
        }
        // Keep hit intents until the committed state acknowledges the drop, including CAS contention.
        carrierHits.RemoveWhere(marker => State.carriers.Any(c => c.marker == marker && !c.carrying));
        var train = RailTrain.Instance;
        bool NearCrew(Vector3 point) => Vector3.Distance(point, train.transform.position) < 100 ||
            next.players.Any(p => IsConnected(p.actor) && Position(p.actor, out var pos) && Vector3.Distance(point, pos) < 65);
        bool fuelAvailable = false;
        foreach (var v in PhotonNetwork.PhotonViewCollection)
                if (v != null && v.GetComponent<RailCoal>() != null && v.transform.position.y > -.5f && !next.WasConsumed(v.ViewID, v.SpawnToken) && NearCrew(v.transform.position)) fuelAvailable = true;
        fuelAvailable |= BagObject.Active.Any(b => b != null && NearCrew(b.transform.position) && b.Keys().Any(k => k == coalPrefab.name));
        if (PhotonSession.CurrentFuel > 2 || fuelAvailable) { next.recoveryAt = 0; return; }
        if (next.recoveryAt <= 0) { next.recoveryAt = now + 20; return; }
        if (now < next.recoveryAt) return;
          // Prefer an existing nearby supply opportunity. Otherwise re-equip a
          // spent carrier outside every crew member's view, on its own baked block.
          var localEnemies = Game.AI.Enemy.Active.Where(e => e != null && e.GetComponent<RailCoalCarrier>() != null && NearCrew(e.Root.position))
              .OrderBy(e => Vector3.Distance(e.Root.position, train.transform.position)).ToArray();
          if (localEnemies.Any(e => next.carriers.Any(c => c.marker == e.GetComponent<RailCoalCarrier>().Marker && c.carrying)))
          { next.recoveryAt = now + 30; return; }
          var candidate = localEnemies.FirstOrDefault(e => CanResupply(e.Root.position));
        if (candidate == null) return;
        var key = candidate.GetComponent<RailCoalCarrier>().Marker;
        var retained = next.carriers.Find(c => c.marker == key);
          if (retained == null) { retained = new RailCarrierState { marker = key, carrying = false }; next.carriers.Add(retained); }
        if (!retained.carrying)
        {
            retained.generation++; retained.carrying = true;
        }
        next.recoveryAt = now + 30;
    }
      bool CanResupply(Vector3 position)
      {
          foreach (var player in PhotonNetwork.PlayerList)
          {
              if (player.IsInactive) continue;
              var avatar = PhotonPlayer.ForActor(player.ActorNumber);
              if (avatar == null || !avatar.TryGetGaze(out var eye, out var forward)) return false;
              Vector3 delta = position + Vector3.up - eye;
              if (delta.magnitude < 15) return false;
              if (Vector3.Dot(forward.normalized, delta.normalized) > .3f &&
                  !Physics.Linecast(eye, position + Vector3.up, 1 << 6, QueryTriggerInteraction.Ignore)) return false;
          }
          return true;
      }
    void SpawnCarrierDrops()
    {
        if (State.finished || !PhotonSession.OnTrain) return;
        string receiptKey = "rails.carrierDrops";
        var receipts = PhotonSession.Get(receiptKey, Array.Empty<string>());
        bool changed = false;
        foreach (var carrier in State.carriers) for (int i = 0; i < carrier.drops.Count; i++)
        {
            string marker = "carrier-" + carrier.marker + "-" + i;
            string receipt = State.run + ":" + marker;
            // Issuance survives stowing, consumption and master handoff. A missing
            // loose object does not mean it should be spawned again.
            if (State.consumedMarkers.Contains(marker) || receipts.Contains(receipt) || issuedDrops.Contains(receipt)) continue;
            if (!HasMarker(State.run, marker))
                PhotonNetwork.InstantiateRoomObject(coalPrefab.name, carrier.drops[i], Quaternion.identity, 0, new object[] { "rails", State.run, marker });
            issuedDrops.Add(receipt); changed = true;
        }
        // Sent after reliable instantiation, by the same authority. Keep only this
        // run's receipts so repeated private departures do not grow room metadata.
        if (changed) PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            [receiptKey] = receipts.Concat(issuedDrops).Where(r => r.StartsWith(State.run + ":", StringComparison.Ordinal)).Distinct().ToArray() });
    }
    static string Safe(string text) => (text ?? "Duck").Replace("<", "").Replace(">", "");
    static string Award(RailParticipant p) => p.revives > 0 ? "Rescue Crew" : p.coal > 0 ? "Boiler Room Employee" : p.horns > 0 ? "Public Nuisance" : p.downs > 0 ? "Somehow Survived" : "Precious Cargo";
    void TeleportAboard()
    {
        if (GR.Player.instance == null || RailTrain.Instance == null) return;
        Vector3 position = RailTrain.Instance.transform.TransformPoint(new Vector3((PhotonNetwork.LocalPlayer.ActorNumber % 3 - 1) * 1.1f, .35f, -2));
        var desktop = GR.Player.instance.GetComponent<DesktopPlayer>();
        if (desktop != null) desktop.Teleport(position, Quaternion.identity, true); else NewPlayer.instance?.Warp(position, Quaternion.identity);
        fallAt = 0;
    }
}

