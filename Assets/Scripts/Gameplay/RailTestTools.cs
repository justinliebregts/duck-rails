using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[Serializable] public sealed class RailTestPreset
{
    public int version = 1, seed = 12345, lag, jitter, loss;
    public string route, scenario = "Manual", roles = "master + peer";
}

/// <summary>Explicit development fixtures. Mutations require a private test room and its authority.</summary>
public static class RailTestTools
{
    static RailTestPreset preset = new RailTestPreset();
    static int[] previousNetwork;
    static bool configured;
    public static string Status = "Test tools require a private room";
    public static bool Allowed => RailDiagnostics.Development && PhotonSession.IsPrivate && PhotonSession.IsRailsRoom &&
        PhotonSession.Instance != null && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
    public static bool Authority => Allowed && PhotonNetwork.IsMasterClient;
    public static bool CanMutate => Authority && PhotonSession.Phase == RailPhase.Running;
    public static RailTestPreset CurrentPreset()
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        var network = peer.NetworkSimulationSettings;
        return new RailTestPreset { seed = preset.seed, scenario = preset.scenario, roles = preset.roles,
            lag = peer.IsSimulationEnabled ? network.IncomingLag : 0,
            jitter = peer.IsSimulationEnabled ? network.IncomingJitter : 0,
            loss = peer.IsSimulationEnabled ? network.IncomingLossPercentage : 0,
            route = PhotonSession.Get(RailRoute.StateKey, preset.route ?? "") };
    }
    public static void LoadPreset(RailTestPreset value)
    {
        if (!RailDiagnostics.Development || value == null || value.version != 1 || value.lag < 0 || value.lag > 500 || value.jitter < 0 || value.jitter > 200 || value.loss < 0 || value.loss > 20)
            throw new ArgumentException("Invalid test preset");
        preset = value;
        configured = true;
        Directory.CreateDirectory(RailDiagnostics.Folder);
        File.WriteAllText(Path.Combine(RailDiagnostics.Folder, "active-preset.txt"), JsonUtility.ToJson(preset));
        Status = "Preset loaded; applies to next private run. Apply network settings separately.";
    }
    public static bool TryRoute(string run, out RailRouteState route)
    {
        route = null;
        if (!RailDiagnostics.Development || !PhotonSession.IsPrivate || !PhotonNetwork.IsMasterClient) return false;
        if (!configured)
        {
            string path = Path.Combine(RailDiagnostics.Folder, "active-preset.txt");
            if (File.Exists(path)) try { LoadPreset(JsonUtility.FromJson<RailTestPreset>(File.ReadAllText(path))); } catch (Exception) { Status = "Saved preset is invalid; using normal route"; }
        }
        if (!configured) return false;
        if (!string.IsNullOrEmpty(preset.route))
        {
            try
            {
                route = JsonUtility.FromJson<RailRouteState>(preset.route);
                if (route?.blocks?.Length != 5 || route.blocks.Distinct().Count() != 5 || route.variations?.Length != 5 || route.placements?.Length != 5)
                    throw new ArgumentException("Invalid route layout");
                float z = RailRoute.StartZ;
                for (int i = 0; i < 5; i++)
                {
                    string blockId = route.blocks[i];
                    var asset = RailRoute.Instance.catalog.FirstOrDefault(b => b.id == blockId);
                    if (asset == null || route.variations[i] < 0 || route.variations[i] >= asset.variations.Length ||
                        !float.IsFinite(route.placements[i].sqrMagnitude) || Vector3.Distance(route.placements[i], new Vector3(0, 0, z)) > .001f)
                        throw new ArgumentException("Preset is incompatible with current blocks");
                    z += asset.length;
                }
                route.run = run; return true;
            }
            catch (Exception) { Status = "Saved route is incompatible; using normal random route"; route = null; return false; }
        }
        route = RailRoute.Choose(run, RailRoute.Instance.catalog, preset.seed); return true;
    }
    public static void SavePreset()
    {
        Directory.CreateDirectory(RailDiagnostics.Folder);
        File.WriteAllText(Path.Combine(RailDiagnostics.Folder, "last-preset.txt"), JsonUtility.ToJson(CurrentPreset(), true));
        Status = "Current route and network preset saved";
    }
    public static void RepeatRoute()
    {
        if (!Allowed) { Status = "Use a private test room"; return; }
        LoadPreset(CurrentPreset()); Status = "Current route selected for the next private run";
    }
    public static void ClearPreset()
    {
        configured = false; preset = new RailTestPreset();
        string path = Path.Combine(RailDiagnostics.Folder, "active-preset.txt");
        if (File.Exists(path)) File.Delete(path);
        Status = "Normal random routes restored";
    }
    public static void SetFuel(float fuel)
    {
        if (!CanMutate) { Status = "Only the private run's master can set fuel"; return; }
        var train = RailTrain.Instance;
        Commit(new Hashtable { [RailSessionRules.Fuel] = Mathf.Clamp(fuel, 0, 100),
            [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, train.Distance, 0f, 0f } }, "Set fuel and stopped train");
    }
    public static void Bridge()
    {
        if (!CanMutate || RailRoute.Instance == null || !RailRoute.Instance.Ready) { Status = "Requires master in a ready private run"; return; }
        Commit(new Hashtable { [RailSessionRules.Fuel] = 20f,
            [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, RailRoute.Instance.FinaleStart + 45, 0f, 0f } }, "Train moved to bridge approach");
    }
    public static void Board()
    {
        if (!Allowed || RailTrain.Instance == null || GR.Player.instance == null) return;
        var position = RailTrain.Instance.transform.TransformPoint(new Vector3(0, .35f, -2));
        var desktop = GR.Player.instance.GetComponent<DesktopPlayer>();
        if (desktop != null) desktop.Teleport(position, RailTrain.Instance.transform.rotation, true);
        else if (NewPlayer.instance != null) NewPlayer.instance.Warp(position, RailTrain.Instance.transform.rotation);
        Status = "Returned to train";
    }
    public static void SpawnBag()
    {
        if (!CanMutate || RailWorld.Instance == null) { Status = "Requires master in a private run"; return; }
        var go = PhotonNetwork.InstantiateRoomObject(RailWorld.Instance.bagPrefab.name,
            RailTrain.Instance.transform.TransformPoint(new Vector3(0, 1, 0)), Quaternion.identity);
        var bag = go.GetComponent<BagObject>();
        bag.Put(bag.FirstEmptySlot(), "RailCoal", go.transform.position, .3f);
        Status = "Spawned shared bag containing coal"; RailDiagnostics.Instance?.Event(Status);
    }
    public static void DownSelf()
    {
        if (!Allowed || PhotonSession.Phase != RailPhase.Running) return;
        RailExpedition.RequestDown(); Status = "Requested normal player down";
    }
    static void Commit(Hashtable changes, string message)
    {
        Status = PhotonSession.Instance.Commit(changes) ? message : "Room update pending; try again";
        RailDiagnostics.Instance?.Event(Status);
    }
    public static void ApplyNetwork(bool adverse)
    {
        if (!Allowed) { Status = "Network presets require a private test room"; return; }
        RestoreNetwork();
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        var s = peer.NetworkSimulationSettings;
        if (peer.IsSimulationEnabled) { Status = "Another test owns network simulation"; return; }
        previousNetwork = new[] { s.IncomingLag, s.OutgoingLag, s.IncomingJitter, s.OutgoingJitter, s.IncomingLossPercentage, s.OutgoingLossPercentage };
        if (adverse) { preset.lag = 120; preset.jitter = 25; preset.loss = 3; }
        s.IncomingLag = s.OutgoingLag = preset.lag; s.IncomingJitter = s.OutgoingJitter = preset.jitter;
        s.IncomingLossPercentage = s.OutgoingLossPercentage = preset.loss; peer.IsSimulationEnabled = true;
        Status = "Network preset applied to this client"; RailDiagnostics.Instance?.Event(Status);
    }
    public static void RestoreNetwork()
    {
        if (previousNetwork == null) return;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer; peer.IsSimulationEnabled = false;
        var s = peer.NetworkSimulationSettings;
        s.IncomingLag = previousNetwork[0]; s.OutgoingLag = previousNetwork[1]; s.IncomingJitter = previousNetwork[2];
        s.OutgoingJitter = previousNetwork[3]; s.IncomingLossPercentage = previousNetwork[4]; s.OutgoingLossPercentage = previousNetwork[5];
        previousNetwork = null; preset.lag = preset.jitter = preset.loss = 0; Status = "Normal network restored";
    }
}
