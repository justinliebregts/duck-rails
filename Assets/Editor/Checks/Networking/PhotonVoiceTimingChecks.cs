using System;
using System.Collections;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// RunTiming enters actual Play Mode in a disposable, Photon-only project. Do not
// include Assembly-CSharp/game bootstrap code: no gameplay or cloud connections.
public static partial class PhotonVoiceRecoveryChecks
{
    const string TimingPending = "Duckz.VoiceTiming.Pending";
    const string TimingRunning = "Duckz.VoiceTiming.Running";

    [InitializeOnLoadMethod]
    static void RegisterTimingRunner()
    {
        EditorApplication.playModeStateChanged -= TimingPlayModeChanged;
        EditorApplication.playModeStateChanged += TimingPlayModeChanged;
    }

    public static void RunTiming()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/VoiceTimingChecks/Assets"))
            throw new InvalidOperationException("Use the disposable Photon-only VoiceTimingChecks project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(TimingPending, true);
        SessionState.SetBool(TimingRunning, false);
        EditorApplication.EnterPlaymode(); // Run without -quit; completion exits explicitly.
    }

    static void TimingPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(TimingPending, false)) return;
        SessionState.SetBool(TimingPending, false);
        SessionState.SetBool(TimingRunning, true);
        checks = 0;
        var runner = new GameObject("Voice timing regression").AddComponent<VoiceTimingRunner>();
        runner.Begin(TimingScenarios());
    }

    internal static void TimingFinished(Exception error)
    {
        Time.timeScale = 1f;
        SessionState.SetBool(TimingRunning, false);
        if (error == null) Debug.Log($"PhotonVoiceTimingChecks: ALL PASSED ({checks} assertions, real Play Mode timers)");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    static VoiceTimingTestClient TimingClient(string name)
    {
        var client = Init<VoiceTimingTestClient>(Root(name));
        client.Client.LoadBalancingPeer.SocketImplementationConfig[ConnectionProtocol.Udp] = typeof(VoiceTimingSocket);
        client.Client.AuthMode = AuthModeOption.Auth;
        client.Client.AppId = "voice-test-only";
        client.Client.MasterServerAddress = "127.0.0.1:5055";
        typeof(LoadBalancingClient).GetProperty("GameServerAddress").SetValue(client.Client, "127.0.0.1:5056");
        Set(client.Client, "tokenCache", "test-token");
        Set(client.Client, "enterRoomParamsCache", new EnterRoomParams { RoomName = "room-a_voice_" });
        return client;
    }

    static void PeerConnected(VoiceTimingTestClient client, ServerConnection server)
    {
        // Supply the server's connection completion without any socket IO.
        var peer = Get<object>(client.Client.LoadBalancingPeer, "peerBase");
        var state = Field(peer, "peerConnectionState");
        state.SetValue(peer, Enum.Parse(state.FieldType, "Connected"));
        Set(peer, "ApplicationIsInitialized", true);
        typeof(LoadBalancingClient).GetProperty("Server").SetValue(client.Client, server);
    }

    static void AuthenticatedMaster(VoiceTimingTestClient client)
    {
        PeerConnected(client, ServerConnection.MasterServer);
        client.Client.OnOperationResponse(new OperationResponse
        {
            OperationCode = OperationCode.Authenticate,
            Parameters = new ParameterDictionary()
        });
    }

    static IEnumerator TimingScenarios()
    {
        // 1. Quick rejoin accepted by the actual SDK, rejected asynchronously by the
        // simulated server, then disconnect completion and fresh-connection fallback.
        var client = TimingClient("Asynchronous rejoin rejection");
        yield return null; // Let Unity call the production Start before the scenario.
        client.LeaderJoined = true;
        client.Enter("room-a_voice_");
        client.Lose(DisconnectCause.ClientTimeout);
        yield return new WaitForSecondsRealtime(1.25f);
        Check(client.ClientState == ClientState.ConnectingToGameServer && client.FreshConnections == 0,
            "SDK accepts quick rejoin; no premature fresh connection");
        Check(Get<bool>(client, "quickRejoinAttempted") && !Pending(client), "Quick rejoin is in flight with no duplicate timer");
        PeerConnected(client, ServerConnection.GameServer);
        client.Client.State = ClientState.Joining;
        client.Client.OnOperationResponse(new OperationResponse
            { OperationCode = OperationCode.JoinGame, ReturnCode = ErrorCode.JoinFailedWithRejoinerNotFound, Parameters = new ParameterDictionary() });
        Check(client.FreshConnections == 0, "Async rejection waits for disconnect completion and backoff");
        yield return null;
        client.Client.LoadBalancingPeer.DispatchIncomingCommands();
        Check(client.ClientState == ClientState.Disconnected && Pending(client), "Real disconnect completion schedules fresh recovery");
        yield return new WaitForSecondsRealtime(2.25f);
        Check(client.FreshConnections == 1 && client.ClientState == ClientState.ConnectingToMasterServer,
            "Async quick-rejoin failure falls back through the master server exactly once");
        AuthenticatedMaster(client);
        Check(client.JoinRequests == 1 && client.LastJoin == "room-a_voice_" && client.ClientState == ClientState.Joining,
            "Fresh connection requests current voice room and SDK accepts the join");
        client.Enter("room-a_voice_");
        Check(!Pending(client) && Get<float>(client, "recoveryDelay") == 1f, "Recovered join resets retry state");
        Object.Destroy(client.gameObject);
        yield return null;

        // 2. A successful response for the old room arrives after gameplay moved.
        foreach (bool switchRoom in new[] { false, true })
        {
            client = TimingClient(switchRoom ? "Room switch in flight" : "Room leave in flight");
            yield return null;
            client.LeaderJoined = true;
            client.Enter("room-a_voice_");
            client.Lose(DisconnectCause.ClientTimeout);
            yield return new WaitForSecondsRealtime(1.25f);
            Check(client.ClientState == ClientState.ConnectingToGameServer, "Old room rejoin is already connecting");
            client.LeaderJoined = false;
            client.LeaderChanged(ClientState.Leaving);
            if (switchRoom)
            {
                client.RoomName = "room-b_voice_";
                client.LeaderJoined = true;
                client.LeaderChanged(ClientState.Joined);
            }
            PeerConnected(client, ServerConnection.GameServer);
            client.Enter("room-a_voice_"); // Late success delivered through real StateChanged subscribers.
            Check(client.ClientState == ClientState.Leaving, "Late old-room success immediately leaves the obsolete voice room");
            Check(!Pending(client), "Late success creates no extra recovery timer");
            client.Client.OnOperationResponse(new OperationResponse { OperationCode = OperationCode.Leave, Parameters = new ParameterDictionary() });
            yield return null;
            client.Client.LoadBalancingPeer.DispatchIncomingCommands();
            AuthenticatedMaster(client);
            Check(switchRoom ? client.LastJoin == "room-b_voice_" && client.JoinRequests == 1 && client.ClientState == ClientState.Joining
                : client.JoinRequests == 0 && client.ClientState == ClientState.ConnectedToMasterServer,
                switchRoom ? "After old room leaves, only the new room is requested" : "After gameplay leave, voice does not join another room");
            if (switchRoom) client.Enter("room-b_voice_");
            Object.Destroy(client.gameObject);
            yield return null;
        }

        // 3. Native Unity coroutine timing, timeScale=0, actual component disable /
        // GameObject deactivation, and their corresponding enable callbacks.
        client = TimingClient("Unscaled recovery timer");
        yield return null;
        client.LeaderJoined = true;
        client.Enter("room-a_voice_");
        Time.timeScale = 0f;
        client.OnApplicationPause(true);
        client.Lose(DisconnectCause.DnsExceptionOnConnect);
        yield return new WaitForSecondsRealtime(0.2f);
        Check(client.FreshConnections == 0 && Pending(client), "Recovery respects initial delay with timeScale zero");
        yield return new WaitForSecondsRealtime(1.05f);
        Check(client.FreshConnections == 1 && !Pending(client), "Real unscaled timer advances while game time is paused");
        client.OnApplicationPause(false);
        Time.timeScale = 1f;
        Object.Destroy(client.gameObject);
        yield return null;

        foreach (bool deactivateObject in new[] { false, true })
        {
            client = TimingClient(deactivateObject ? "GameObject disable" : "Component disable");
            yield return null;
            client.LeaderJoined = true;
            client.Enter("room-a_voice_");
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            yield return new WaitForSecondsRealtime(0.2f);
            if (deactivateObject) client.gameObject.SetActive(false); else client.enabled = false;
            Check(!Pending(client), "Actual disable callback cancels coroutine");
            yield return new WaitForSecondsRealtime(1.1f);
            Check(client.FreshConnections == 0, "Disabled voice performs no retry after the old deadline");
            if (deactivateObject) client.gameObject.SetActive(true); else client.enabled = true;
            yield return null;
            Check(client.FreshConnections == 1, "Actual enable callback resumes recovery once");
            yield return new WaitForSecondsRealtime(1.1f);
            Check(client.FreshConnections == 1 && !Pending(client), "Cancelled timer cannot reconnect a second time after resume");
            AuthenticatedMaster(client);
            client.Enter("room-a_voice_");
            Check(client.JoinRequests == 1, "Resumed connection reaches the expected room");
            Object.Destroy(client.gameObject);
            yield return null;
        }
    }
}

// All network IO stays inside this in-memory socket; even accepted SDK connections
// cannot open a real socket or send packets to the loopback addresses above.
public sealed class VoiceTimingSocket : IPhotonSocket
{
    public VoiceTimingSocket(PeerBase peer) : base(peer) { PollReceive = false; }
    public override bool Connect() { State = PhotonSocketState.Connected; return true; }
    public override bool Disconnect() { State = PhotonSocketState.Disconnected; return true; }
    public override PhotonSocketError Send(byte[] data, int length) => PhotonSocketError.Success;
    public override PhotonSocketError Receive(out byte[] data) { data = null; return PhotonSocketError.NoData; }
}

public sealed class VoiceTimingTestClient : VoiceFollowClient
{
    public bool LeaderJoined;
    public string RoomName = "room-a_voice_", LastJoin;
    public int FreshConnections, JoinRequests;
    protected override bool LeaderInRoom => LeaderJoined;
    protected override bool LeaderOfflineMode => false;
    protected override string GetVoiceRoomName() => RoomName;
    protected override bool ConnectVoice() { FreshConnections++; return Client.ConnectToMasterServer(); }
    protected override bool JoinVoiceRoom(string name)
    {
        JoinRequests++;
        LastJoin = name;
        return base.JoinVoiceRoom(name);
    }
    public void LeaderChanged(ClientState state) => LeaderStateChanged(state);
    public void Enter(string room)
    {
        Client.CurrentRoom = new Room(room, new RoomOptions());
        Client.State = ClientState.Joined;
    }
    public void Lose(DisconnectCause cause)
    {
        typeof(LoadBalancingClient).GetProperty("DisconnectedCause").SetValue(Client, cause);
        Client.State = ClientState.Disconnected;
    }
}

public sealed class VoiceTimingRunner : MonoBehaviour
{
    public void Begin(IEnumerator scenarios) => StartCoroutine(Guard(scenarios));
    IEnumerator Guard(IEnumerator scenarios)
    {
        while (true)
        {
            object next;
            try
            {
                if (!scenarios.MoveNext()) break;
                next = scenarios.Current;
            }
            catch (Exception error) { PhotonVoiceRecoveryChecks.TimingFinished(error); yield break; }
            yield return next;
        }
        PhotonVoiceRecoveryChecks.TimingFinished(null);
    }
}
