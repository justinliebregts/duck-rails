using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Short-lived control connection: keeps a crew in contact while its gameplay
/// coordinator leaves the expedition to reserve a compound. No avatars or voice
/// are created here. The authenticated identity is unchanged; rooms are distinct.
/// </summary>
public sealed class RailCrewReturn : IConnectionCallbacks, IMatchmakingCallbacks, IDisposable
{
    const string Destination = "destination", Searcher = "searcher", SearchUntil = "searchUntil", Arrived = "arrived", Complete = "complete";
    readonly PhotonSession session;
    readonly LoadBalancingClient client = new LoadBalancingClient();
    readonly AppSettings settings;
    readonly string token;
    readonly double expires;
    double retryAt, lastWrite, searchStarted;
    string attemptedDestination, arrivedDestination, retainedTarget = "";
    bool searching, stopped;
    public readonly string[] Users;
    public string FreshDestination => "fresh:" + token;
    public bool Finished { get; private set; }
    public bool Active => !stopped;
    public string Diagnostic => "control=" + client.State + " active=" + Active + " destination=" + Target;
    public string Target => client.InRoom && client.CurrentRoom.CustomProperties[Destination] is string value ? value : "";
    public bool InDestination => PhotonNetwork.InRoom && PhotonSession.Kind == RailSessionRules.CompoundKind &&
        (Target == FreshDestination ? PhotonSession.Get(RailSessionRules.Allocation, "") == token :
            Target.Length > 0 && PhotonNetwork.CurrentRoom.Name == Target);
    bool IsSearcher => client.InRoom && client.CurrentRoom.CustomProperties[Searcher] is string user && user == client.UserId;
    double Now => Time.realtimeSinceStartupAsDouble;

    public RailCrewReturn(PhotonSession session, AppSettings settings, string token, string[] users)
    {
        this.session = session; this.token = token; Users = users;
        this.settings = settings.CopyTo(new AppSettings());
        // PUN appends its protocol version; a raw Realtime connection must match it.
        this.settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion;
        expires = Now + 90;
        client.AddCallbackTarget(this);
        Connect();
    }

    void Connect()
    {
        client.AuthValues = PhotonNetwork.AuthValues.CopyTo(new AuthenticationValues());
        var account = PlayerSession.Instance;
        if (account != null && account.TryGetAccessToken(out string accessToken))
        {
            client.AuthValues.AuthType = CustomAuthenticationType.Custom;
            client.AuthValues.AuthGetParameters = null;
            client.AuthValues.AddAuthParameter("usr", account.PlayerId);
            client.AuthValues.AddAuthParameter("pla", account.Platform);
            client.AuthValues.AddAuthParameter("tkn", accessToken);
        }
        client.ConnectUsingSettings(settings);
        retryAt = Now + 2;
    }

    public void Tick()
    {
        if (stopped) return;
        client.Service();
        if (Now >= expires)
        {
            // Do not independently matchmake after losing the control channel.
            // A destination may already have been committed by another member.
            Finished = arrivedDestination != null && InDestination;
            session.CrewReturnStopped(Finished);
            Dispose(); return;
        }
        if (!client.InRoom)
        {
            if (Now >= retryAt && (client.State == ClientState.Disconnected || client.State == ClientState.PeerCreated)) Connect();
            return;
        }
        string target = Target;
        if (target.Length > 0) retainedTarget = target;
        if (client.CurrentRoom.CustomProperties[Complete] is bool complete && complete)
        { Finished = true; Dispose(); return; }
        if (target.Length == 0)
        {
            var props = client.CurrentRoom.CustomProperties;
            string owner = props[Searcher] as string ?? "";
            bool ownerPresent = client.CurrentRoom.Players.Values.Any(p => !p.IsInactive && p.UserId == owner);
            double until = props[SearchUntil] is double value ? value : 0;
            if (client.LocalPlayer.IsMasterClient && Now - lastWrite > 1)
            {
                if (!RailSessionRules.SearchExistingCompound(Users.Length) ||
                    (owner.Length > 0 && (!ownerPresent || client.LoadBalancingPeer.ServerTimeInMilliSeconds / 1000d >= until)))
                    UseFreshCompound();
                else if (owner.Length == 0)
                {
                    lastWrite = Now;
                    client.CurrentRoom.SetCustomProperties(new Hashtable { [Searcher] = client.UserId,
                        [SearchUntil] = client.LoadBalancingPeer.ServerTimeInMilliSeconds / 1000d + RailSessionRules.ReturnSearchSeconds },
                        new Hashtable { [Destination] = "", [Searcher] = "" });
                }
            }
            if (IsSearcher && !searching && !session.HasRoomRequest)
            {
                searching = true; searchStarted = Now;
                session.BeginCrewSearch(Users);
            }
            if (searching && Now - searchStarted >= RailSessionRules.ReturnSearchSeconds) UseFreshCompound();
            // Retry publication until the server echoes it. CAS prevents a stale
            // search result from overwriting the common fresh-room fallback.
            if (searching && IsSearcher && PhotonSession.Kind == RailSessionRules.CompoundKind &&
                PhotonNetwork.InRoom && !session.HasRoomRequest && Now - lastWrite > .5)
                PublishExisting();
            return;
        }
        bool fresh = target == FreshDestination;
        if (InDestination && !session.HasRoomRequest)
        {
            arrivedDestination = target;
            if (!(client.LocalPlayer.CustomProperties[Arrived] is string arrived) || arrived != target)
                client.LocalPlayer.SetCustomProperties(new Hashtable { [Arrived] = target });
            // Keep every client available for a coordinated fallback until all
            // original members arrive, or the bounded handoff lifetime expires.
            if (Now - lastWrite > .5 && Users.All(user => client.CurrentRoom.Players.Values.Any(p => p.UserId == user &&
                p.CustomProperties[Arrived] is string at && at == target)))
            {
                lastWrite = Now;
                client.CurrentRoom.SetCustomProperties(new Hashtable { [Complete] = true },
                    new Hashtable { [Destination] = target, [Complete] = false });
            }
            return;
        }
        if (!session.HasRoomRequest && (attemptedDestination != target || Now >= retryAt))
        {
            attemptedDestination = target; retryAt = Now + 2;
            session.BeginCrewDestination(fresh, fresh ? token : target, Users);
        }
    }

    public void JoinedCompound()
    {
        if (Target.Length == 0 && searching && IsSearcher) PublishExisting();
    }
    void PublishExisting()
    {
        lastWrite = Now;
        client.CurrentRoom.SetCustomProperties(new Hashtable { [Destination] = PhotonNetwork.CurrentRoom.Name },
            new Hashtable { [Destination] = "", [Searcher] = client.UserId });
    }
    public void JoinFailed()
    {
        retryAt = Now + 2;
        UseFreshCompound();
    }
    void UseFreshCompound()
    {
        if (!client.InRoom || Target == FreshDestination || Now - lastWrite < .5) return;
        lastWrite = Now;
        client.CurrentRoom.SetCustomProperties(new Hashtable { [Destination] = FreshDestination },
            new Hashtable { [Destination] = Target, [Complete] = false });
    }
    public void Dispose()
    {
        if (stopped) return;
        stopped = true;
        client.RemoveCallbackTarget(this);
        client.Disconnect();
        // Flush the disconnect even though this instance is no longer serviced.
        client.LoadBalancingPeer.SendOutgoingCommands();
        client.LoadBalancingPeer.StopThread();
    }
    public void OnConnectedToMaster()
    {
        client.OpJoinOrCreateRoom(new EnterRoomParams
        {
            RoomName = "rails-return-" + token,
            RoomOptions = new RoomOptions { IsVisible = false, MaxPlayers = RailSessionRules.CrewCapacity,
                PublishUserId = true, PlayerTtl = 0, EmptyRoomTtl = 0,
                CustomRoomProperties = new Hashtable { [Destination] = retainedTarget, [Searcher] = "", [SearchUntil] = 0d, [Complete] = false } },
            ExpectedUsers = Users
        });
    }
    public void OnJoinedRoom() { }
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) { retryAt = Now + 2; }
    public void OnRegionListReceived(RegionHandler handler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    public void OnCustomAuthenticationFailed(string message) { retryAt = Now + 5; }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short code, string message) { client.Disconnect(); }
    public void OnJoinRoomFailed(short code, string message) { client.Disconnect(); }
    public void OnJoinRandomFailed(short code, string message) { client.Disconnect(); }
    public void OnLeftRoom() { }
    public void OnFriendListUpdate(List<FriendInfo> friends) { }
}
