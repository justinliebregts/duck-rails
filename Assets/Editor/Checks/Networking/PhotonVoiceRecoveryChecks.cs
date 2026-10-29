using System;
using System.Collections;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using Photon.Voice.Unity.UtilityScripts;
using UnityEngine;
using Object = UnityEngine.Object;

// Disposable batch project only. Runs production callbacks against synthetic transport
// states, with real Recorder/Opus streams and no connections to public Photon rooms.
public static partial class PhotonVoiceRecoveryChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    static MethodInfo Method(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, Flags, null, Type.EmptyTypes, null);
            if (method != null) return method;
        }
        throw new MissingMethodException(name);
    }
    public static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static void Call(object obj, string name) => Method(obj, name).Invoke(obj, null);
    static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception(message);
    }
    public static void State(VoiceConnection client, ClientState state)
    {
        Set(client.Client, "state", state);
    }
    static bool Pending(VoiceFollowClient client) => Get<Coroutine>(client, "recovery") != null;
    static void FireTimer(VoiceFollowClient client)
    {
        // Deliver the production timer callback deterministically, without waiting seconds.
        typeof(VoiceFollowClient).GetMethod("CancelRecovery", Flags).Invoke(client, new object[] { false });
        Call(client, "RetryVoice");
    }
    static GameObject Root(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<VoiceLogger>().LogLevel = (LogLevel)0;
        return go;
    }
    static T Init<T>(GameObject go) where T : VoiceConnection
    {
        var client = go.AddComponent<T>();
        client.ApplyDontDestroyOnLoad = false;
        if (client.Client == null) Call(client, "Awake");
        client.StopFallbackSendAckThread();
        // Keep the real codec/voice implementation, replacing only socket delivery.
        Set(client.VoiceClient, "transport", new TestTransport(client));
        client.VoiceClient.ThreadingEnabled = false;
        return client;
    }
    sealed class TestTransport : IVoiceTransport
    {
        readonly VoiceConnection owner;
        public int Frames;
        public TestTransport(VoiceConnection owner) { this.owner = owner; }
        public bool IsChannelJoined(int channelId) => owner.ClientState == ClientState.Joined;
        public void SendVoiceInfo(LocalVoice voice, int channelId, bool targetMe, int[] targets) { }
        public void SendVoiceRemove(LocalVoice voice, int channelId, bool targetMe, int[] targets) { }
        public void SendFrame(ArraySegment<byte> data, FrameFlags flags, ushort ev, byte frame, byte voice, int channel, SendFrameParams par) { Frames++; }
        public string ChannelIdStr(int id) => "test-channel";
        public string PlayerIdStr(int id) => "test-player";
        public int GetPayloadFragmentSize(SendFrameParams par) => 0;
    }
    static Recorder RecorderOn(GameObject go)
    {
        var rec = go.AddComponent<Recorder>();
        Call(rec, "Awake");
        rec.RecordingEnabled = false;
        rec.RecordWhenJoined = true;
        rec.SourceType = Recorder.InputSourceType.Factory;
        rec.InputFactory = () => new TestAudio();
        return rec;
    }
    sealed class TestAudio : IAudioReader<float>
    {
        public int SamplingRate => 24000;
        public int Channels => 1;
        public string Error => null;
        public bool Read(float[] data) => false;
        public void Dispose() { }
    }
    static void StreamReady(Recorder rec, int viewId, string message)
    {
        Call(rec, "Update");
        var voice = Get<LocalVoice>(rec, "voice");
        Check(rec.LevelMeter != null && voice != LocalVoiceAudioDummy.Dummy, message + ": real audio stream");
        Check((int)voice.Info.UserData == viewId, message + ": current avatar routing");
        var samples = new float[960];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.2f * Mathf.Sin(i * 0.1f);
        var transport = Get<TestTransport>(Get<VoiceConnection>(rec, "voiceConnection").VoiceClient, "transport");
        int sent = transport.Frames;
        ((LocalVoiceFramed<float>)voice).PushData(samples);
        if (rec.TransmitEnabled)
            Check(rec.LevelMeter.CurrentAvgAmp > 0, message + ": microphone-level processing is alive");
        Check(rec.TransmitEnabled ? transport.Frames > sent : transport.Frames == sent,
            message + ": encoded frames follow mute state");
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only");
        checks = 0;
        ConnectionHandler.AppQuits = false;
        try
        {
            ConnectionRecovery();
            RecorderOrderings();
            AvatarHandoff();
            IncomingSpeakerRecovery();
            Debug.Log($"PhotonVoiceRecoveryChecks: ALL PASSED ({checks} assertions)");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            UnityEditor.EditorApplication.Exit(1);
            throw;
        }
    }

    static void ConnectionRecovery()
    {
        var go = Root("Voice connection recovery");
        var client = Init<VoiceRecoveryTestClient>(go);
        try
        {
            client.Enter("room-a_voice_");
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Check(Pending(client) && client.Connects == 0, "DNS failure schedules instead of connecting immediately");
            float delay = Get<float>(client, "recoveryDelay");
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Check(Get<float>(client, "recoveryDelay") == delay, "Repeated callbacks coalesce into one timer");
            FireTimer(client);
            Check(client.Connects == 1, "DNS failure retries without gameplay rejoining");
            State(client, ClientState.Disconnected);
            client.Response(OperationCode.JoinGame, ErrorCode.JoinFailedWithRejoinerNotFound);
            Check(Pending(client), "Expired rejoin schedules fallback");
            FireTimer(client);
            Check(client.Connects == 2 && Get<bool>(client, "quickRejoinAttempted"), "Expired rejoin uses fresh connection");

            State(client, ClientState.ConnectedToMasterServer);
            client.VoiceChanged(ClientState.ConnectingToMasterServer, ClientState.ConnectedToMasterServer);
            Check(client.Joins == 1 && client.LastJoin == "room-a_voice_", "Fresh connection joins matching voice room");
            client.Enter("room-a_voice_");
            Check(!Pending(client) && Get<float>(client, "recoveryDelay") == 1f, "Successful recovery removes timer and resets delay");

            client.Lose(DisconnectCause.ClientTimeout);
            FireTimer(client); // No cached server/token: quick rejoin returns false.
            Check(client.Connects == 3, "Rejected quick-rejoin initiation falls back immediately on timer");
            client.ConnectAccepted = false;
            for (int i = 0; i < 8; i++)
            {
                State(client, ClientState.Disconnected);
                client.Lose(DisconnectCause.DnsExceptionOnConnect);
                FireTimer(client);
            }
            Check(Pending(client) && Get<float>(client, "recoveryDelay") == 8f, "Repeated failures retain one bounded-backoff timer");
            client.ConnectAccepted = true;

            client.LeaderJoined = false;
            client.LeaderChanged(ClientState.Leaving);
            Check(!Pending(client), "Leaving gameplay cancels pending voice recovery");
            int attempts = client.Connects;
            FireTimer(client);
            Check(client.Connects == attempts, "Stale timer cannot reconnect after gameplay leave");
            client.LeaderJoined = true;
            client.RoomName = "room-b_voice_";
            State(client, ClientState.Disconnected);
            client.LeaderChanged(ClientState.Joined);
            State(client, ClientState.ConnectedToMasterServer);
            client.VoiceChanged(ClientState.ConnectingToMasterServer, ClientState.ConnectedToMasterServer);
            Check(client.LastJoin == "room-b_voice_", "Room switch targets current gameplay room");
            client.Enter("room-b_voice_");
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            client.Disconnect();
            Check(!Pending(client), "Deliberate disconnect cancels timer even while already disconnected");
            attempts = client.Connects;
            client.Lose(DisconnectCause.ClientTimeout);
            FireTimer(client);
            Check(client.Connects == attempts, "Deliberate disconnect stays disconnected");

            client.LeaderChanged(ClientState.Joined);
            State(client, ClientState.Disconnected);
            client.Response(OperationCode.Authenticate, ErrorCode.CustomAuthenticationFailed);
            Check(!Pending(client), "Terminal authentication failure does not retry");
            attempts = client.Connects;
            FireTimer(client);
            Check(client.Connects == attempts, "Timer cannot bypass authentication failure");

            client.LeaderChanged(ClientState.Joined);
            State(client, ClientState.Disconnected);
            client.Response(OperationCode.Authenticate, ErrorCode.AuthenticationTicketExpired);
            Check(Pending(client), "Expired authentication ticket permits fresh authentication");
            FireTimer(client);
            client.AutoConnectAndJoin = false;
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Check(!Pending(client), "Auto-connect opt-out is respected");
            client.AutoConnectAndJoin = true;
            client.Offline = true;
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Check(!Pending(client), "Offline gameplay cannot trigger network recovery");
            client.Offline = false;
            ConnectionHandler.AppQuits = true;
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Check(!Pending(client), "Application quit cannot trigger recovery");
            ConnectionHandler.AppQuits = false;
            client.Lose(DisconnectCause.DnsExceptionOnConnect);
            Call(client, "OnDisable");
            Check(!Pending(client), "Disabling voice cancels recovery");

            client.Enter("room-b_voice_");
            var follow = (Action)Delegate.CreateDelegate(typeof(Action), client, Method(client, "FollowLeader"));
            follow();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) follow();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && !Pending(client), "Healthy matching-room checks allocate zero bytes and schedule no work");
            Check(typeof(VoiceFollowClient).GetMethod("Update", BindingFlags.DeclaredOnly | Flags) == null,
                "Recovery adds no Update loop");
        }
        finally { ConnectionHandler.AppQuits = false; Object.DestroyImmediate(go); }
    }

    static void RecorderOrderings()
    {
        // Two clients model an independently reconnecting peer and an unaffected peer.
        var a = Root("Reconnecting peer");
        var b = Root("Unaffected peer");
        var ca = Init<VoiceRecoveryTestClient>(a);
        var cb = Init<VoiceRecoveryTestClient>(b);
        var ra = RecorderOn(a);
        var rb = RecorderOn(b);
        try
        {
            ra.UserData = 1001; rb.UserData = 2001;
            ca.Enter("room-a_voice_");
            cb.Enter("room-a_voice_");
            Check(ca.AddRecorder(ra) && ra.RecordingEnabled, "Join-before-avatar enables late recorder");
            cb.AddRecorder(rb);
            StreamReady(ra, 1001, "Late registration");
            StreamReady(rb, 2001, "Other player");
            var otherVoice = Get<LocalVoice>(rb, "voice");
            ra.TransmitEnabled = false;
            ca.Lose(DisconnectCause.ClientTimeout);
            Check(!ra.RecordingEnabled && rb.RecordingEnabled, "Only disconnected peer stops recording");
            ca.RemoveRecorder(ra);
            ca.Enter("room-a_voice_");
            ra.UserData = 1002;
            ca.AddRecorder(ra);
            Check(ra.RecordingEnabled && !ra.TransmitEnabled, "Reconnect + replacement restores capture and preserves mute");
            StreamReady(ra, 1002, "Replacement after voice join");
            Check(ReferenceEquals(otherVoice, Get<LocalVoice>(rb, "voice")), "Other peer's recording stream is untouched");

            ca.Lose(DisconnectCause.ClientTimeout);
            ca.RemoveRecorder(ra);
            ra.UserData = 1003;
            ca.AddRecorder(ra);
            Check(!ra.RecordingEnabled, "Avatar-before-join waits while disabled");
            ca.Enter("room-a_voice_");
            StreamReady(ra, 1003, "Replacement before voice join");
            Check(!ra.TransmitEnabled, "Mute survives both orderings");
            ra.TransmitEnabled = true;
            ca.AddRecorder(ra);
            Check(Get<IList>(ca, "recorders").Count == 1, "Duplicate registration never duplicates recorder");

            ca.RemoveRecorder(ra);
            ra.RecordingEnabled = false;
            ra.RecordWhenJoined = false;
            ca.AddRecorder(ra);
            Check(!ra.RecordingEnabled, "Explicit RecordWhenJoined opt-out is preserved");
            ca.RemoveRecorder(ra);
            ra.RecordWhenJoined = true;
            var permission = a.AddComponent<MicrophonePermission>();
            Set(permission, "hasPermission", false);
            ra.SourceType = Recorder.InputSourceType.Microphone;
            ra.RecordingEnabled = false;
            ca.AddRecorder(ra);
            Check(!ra.RecordingEnabled, "Late registration respects denied microphone permission");
            ca.Lose(DisconnectCause.ClientTimeout);
            ca.Enter("room-a_voice_");
            Check(!ra.RecordingEnabled, "Join callback also respects denied permission");
            typeof(MicrophonePermission).GetProperty("HasPermission").SetValue(permission, true);
            Check(ra.RecordingEnabled, "Permission granted after join starts recorder");
            ra.SourceType = Recorder.InputSourceType.Factory;
            StreamReady(ra, 1003, "Permission granted later");
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    static void AvatarHandoff()
    {
        var root = Root("Persistent PUN voice");
        var client = Init<PunVoiceClient>(root);
        var rec = RecorderOn(root);
        client.PrimaryRecorder = rec;
        var old = new GameObject("Retiring local avatar");
        var current = new GameObject("Replacement local avatar");
        try
        {
            // Use explicit fields instead of starting gameplay/Photon matchmaking.
            client.Client.CurrentRoom = new Room("room-a_voice_", new RoomOptions());
            State(client, ClientState.Joined);
            var oldView = old.AddComponent<PhotonView>();
            var newView = current.AddComponent<PhotonView>();
            Set(oldView, "viewIdField", 1001);
            Set(newView, "viewIdField", 1001); // same numeric ID, different avatar lifetime
            var oldVoice = old.AddComponent<PhotonVoiceView>();
            var newVoice = current.AddComponent<PhotonVoiceView>();
            Set(oldVoice, "punVoiceClient", client); Set(oldVoice, "photonView", oldView);
            Set(newVoice, "punVoiceClient", client); Set(newVoice, "photonView", newView);
            Call(oldVoice, "SetupRecorder");
            Call(newVoice, "SetupRecorder");
            // Edit-mode batch objects do not receive runtime lifecycle callbacks.
            Call(oldVoice, "OnDestroy");
            Object.DestroyImmediate(old);
            Check(Get<IList>(client, "recorders").Count == 1 && Get<VoiceConnection>(rec, "voiceConnection") == client,
                "Late destruction of old avatar cannot unregister replacement's shared recorder, even with reused ID");
            StreamReady(rec, 1001, "Shared recorder handoff");
            Call(newVoice, "OnDestroy");
            Object.DestroyImmediate(current);
            Check(Get<IList>(client, "recorders").Count == 0 && Get<VoiceConnection>(rec, "voiceConnection") == null,
                "Current avatar destruction still unregisters and stops recorder");
        }
        finally
        {
            if (old != null) Object.DestroyImmediate(old);
            if (current != null) Object.DestroyImmediate(current);
            Object.DestroyImmediate(root);
        }
    }

    static void IncomingSpeakerRecovery()
    {
        var root = Root("Incoming peer voice");
        var client = Init<VoiceRecoveryTestClient>(root);
        var speakerObject = new GameObject("Remote duck speaker");
        speakerObject.transform.SetParent(root.transform);
        speakerObject.AddComponent<AudioSource>();
        var speaker = speakerObject.AddComponent<Speaker>();
        Call(speaker, "Awake");
        client.RemoteSpeaker = speaker;
        RemoteVoiceOptions first = default, second = default;
        try
        {
            client.Enter("room-a_voice_");
            var info = VoiceInfo.CreateAudioOpus(POpusCodec.Enums.SamplingRate.Sampling24000, 1,
                OpusCodec.FrameDuration.Frame40ms, 30000, 2001);
            first = new RemoteVoiceOptions(client.Logger, "first", info);
            client.VoiceClient.OnRemoteVoiceInfoAction(1, 2, 1, info, ref first);
            Check(speaker.IsLinked && client.CachedRemoteVoices.Count == 1, "Incoming voice links to existing remote duck");
            first.OnRemoteVoiceRemoveAction(); // Transport removes remote voices when leaving voice.
            client.Lose(DisconnectCause.ClientTimeout);
            Check(!speaker.IsLinked && client.CachedRemoteVoices.Count == 0, "Voice loss removes old stream and speaker link");
            client.Enter("room-a_voice_");
            second = new RemoteVoiceOptions(client.Logger, "second", info);
            client.VoiceClient.OnRemoteVoiceInfoAction(1, 2, 2, info, ref second);
            Check(speaker.IsLinked && speaker.RemoteVoice.VoiceId == 2 && client.CachedRemoteVoices.Count == 1,
                "Recovered voice binds new incoming stream to surviving remote duck");
        }
        finally
        {
            second.OnRemoteVoiceRemoveAction?.Invoke();
            first.Decoder?.Dispose();
            second.Decoder?.Dispose();
            Object.DestroyImmediate(root);
        }
    }
}

public sealed class VoiceRecoveryTestClient : VoiceFollowClient
{
    public bool LeaderJoined = true, Offline, ConnectAccepted = true;
    public string RoomName = "room-a_voice_", LastJoin;
    public int Connects, Joins;
    public Speaker RemoteSpeaker;
    protected override bool LeaderInRoom => LeaderJoined;
    protected override bool LeaderOfflineMode => Offline;
    protected override string GetVoiceRoomName() => RoomName;
    protected override bool ConnectVoice() { Connects++; return ConnectAccepted; }
    protected override bool JoinVoiceRoom(string name) { Joins++; LastJoin = name; return true; }
    protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData) => RemoteSpeaker;
    public void LeaderChanged(ClientState state) => LeaderStateChanged(state);
    public void VoiceChanged(ClientState from, ClientState to) => OnVoiceStateChanged(from, to);
    public void Enter(string room)
    {
        Client.CurrentRoom = new Room(room, new RoomOptions());
        PhotonVoiceRecoveryChecks.State(this, ClientState.Joined);
        OnVoiceStateChanged(ClientState.Joining, ClientState.Joined);
    }
    public void Lose(DisconnectCause cause)
    {
        var previous = ClientState;
        PhotonVoiceRecoveryChecks.State(this, ClientState.Disconnected);
        typeof(LoadBalancingClient).GetProperty("DisconnectedCause").SetValue(Client, cause);
        OnVoiceStateChanged(previous, ClientState.Disconnected);
    }
    public void Response(byte operation, int error) => OnOperationResponseReceived(new OperationResponse
        { OperationCode = operation, ReturnCode = (short)error });
}
