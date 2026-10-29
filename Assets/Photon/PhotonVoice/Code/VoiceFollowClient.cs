// ----------------------------------------------------------------------------
// <copyright file="VoiceFollowClient.cs" company="Exit Games GmbH">
// Photon Voice - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// This class can be used to automatically join/leave Voice rooms when
// another network clinet (Leader) joins or leaves its rooms.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

using ExitGames.Client.Photon;
using UnityEngine;
using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections;

namespace Photon.Voice
{
    /// <summary>
    /// This class can be used to automatically sync client states between Leader and Voice clients.
    /// </summary>
    abstract public class VoiceFollowClient : VoiceConnection
    {
        abstract protected bool LeaderInRoom { get; }
        abstract protected bool LeaderOfflineMode { get; }
        abstract protected string GetVoiceRoomName();
        abstract protected bool ConnectVoice();

        #region Public Fields

        /// <summary>Auto connect voice client and join a voice room when Leader client is joined to a Leader room </summary>
        public bool AutoConnectAndJoin = true;

        #endregion

        #region Private Fields

        /// <summary>Used as deliberate disconnect / prevents automatic (re)connect when moving to state disconnected.</summary>
        /// <remarks>
        /// After a manualDisconnect, the VoiceFollowClient will go online when Leader next joins a room.
        /// To prevent that, set AutoConnectAndJoin to false.
        /// </remarks>
        private bool manualDisconnect;
        private bool errAuthOrJoin;
        private bool retryAfterDisconnect;
        private bool quickRejoinAttempted;
        private bool started;
        private string joinedVoiceRoom;
        private Coroutine recovery;
        private float recoveryDelay = 1f;

        private bool CanRecover => this.isActiveAndEnabled && !AppQuits &&
            this.AutoConnectAndJoin && !this.manualDisconnect && !this.errAuthOrJoin &&
            this.LeaderInRoom && !this.LeaderOfflineMode;

        #endregion
        #region Public Methods

        /// <summary>
        /// Connect voice client to Photon servers and join a Voice room
        /// </summary>
        /// <returns>If true, connection command send from client</returns>
        public bool ConnectAndJoinRoom()
        {
            if (!LeaderInRoom)
            {
                this.Logger.Log(LogLevel.Error, "Cannot connect and join if Leader is not joined.");
                return false;
            }
            this.CancelRecovery(true);
            this.manualDisconnect = false;
            this.errAuthOrJoin = false;
            if (this.ConnectVoice())
            {
                this.manualDisconnect = false;
                return true;
            }
            this.Logger.Log(LogLevel.Error, "Connecting to server failed.");
            this.ScheduleRecovery();
            return false;
        }

        /// <summary>
        /// Disconnect voice client from all Photon servers
        /// </summary>
        public void Disconnect()
        {
            this.CancelRecovery(true);
            this.manualDisconnect = true;
            this.retryAfterDisconnect = false;
            if (!this.Client.IsConnected)
            {
                return;
            }
            this.Client.Disconnect();
        }

        #endregion

        #region Private Methods

        protected virtual void Start()
        {
            this.started = true;
            this.manualDisconnect = false;

            this.FollowLeader(); // in case this is enabled or activated late
        }

        protected override void OnDestroy()
        {
            this.CancelRecovery(true);
            base.OnDestroy();
        }

        private void OnEnable()
        {
            if (this.started) this.FollowLeader();
        }

        protected override void OnDisable()
        {
            this.CancelRecovery(true);
            base.OnDisable();
        }

        private void CancelRecovery(bool resetDelay)
        {
            if (this.recovery != null) this.StopCoroutine(this.recovery);
            this.recovery = null;
            if (resetDelay)
            {
                this.recoveryDelay = 1f;
                this.quickRejoinAttempted = false;
            }
        }

        private void ScheduleRecovery()
        {
            if (this.CanRecover && this.recovery == null)
                this.recovery = this.StartCoroutine(this.RecoverVoice());
        }

        private IEnumerator RecoverVoice()
        {
            // One timer only during an outage. Unscaled time also works in pause menus.
            float delay = this.recoveryDelay;
            this.recoveryDelay = Mathf.Min(delay * 2f, 8f);
            yield return new WaitForSecondsRealtime(delay);
            this.RetryVoice();
        }

        private void RetryVoice()
        {
            this.recovery = null;
            if (!this.CanRecover) return;

            // Try quick rejoin once per outage, and only for the same gameplay room.
            // A rejected/expired rejoin must fall back to a fresh connection.
            if (this.ClientState == ClientState.Disconnected && !this.quickRejoinAttempted &&
                this.joinedVoiceRoom == this.GetVoiceRoomName() &&
                (this.Client.DisconnectedCause == DisconnectCause.ClientTimeout ||
                 this.Client.DisconnectedCause == DisconnectCause.ServerTimeout))
            {
                this.quickRejoinAttempted = true;
                if (this.Client.ReconnectAndRejoin()) return;
            }
            this.FollowLeader();
        }

        private static bool RecoverableError(int code)
        {
            switch (code)
            {
                case ErrorCode.InternalServerError:
                case ErrorCode.ServerFull:
                case ErrorCode.MaxCcuReached:
                case ErrorCode.AuthenticationTicketExpired:
                case ErrorCode.GameDoesNotExist:
                case ErrorCode.GameIdAlreadyExists:
                case ErrorCode.JoinFailedWithRejoinerNotFound:
                case ErrorCode.JoinFailedFoundInactiveJoiner:
                case ErrorCode.JoinFailedFoundActiveJoiner:
                case ErrorCode.JoinFailedPeerAlreadyJoined:
                    return true;
                default:
                    return false;
            }
        }

        private static bool RecoverableDisconnect(DisconnectCause cause)
        {
            switch (cause)
            {
                case DisconnectCause.ExceptionOnConnect:
                case DisconnectCause.DnsExceptionOnConnect:
                case DisconnectCause.Exception:
                case DisconnectCause.SendException:
                case DisconnectCause.ReceiveException:
                case DisconnectCause.ClientTimeout:
                case DisconnectCause.ServerTimeout:
                case DisconnectCause.DisconnectByServerReasonUnknown:
                case DisconnectCause.AuthenticationTicketExpired:
                case DisconnectCause.MaxCcuReached:
                    return true;
                default:
                    return false;
            }
        }

        protected override void OnOperationResponseReceived(OperationResponse operationResponse)
        {
            // the base method only logs some error cases. this class re-implements that, so we deliberately skip calling the base method
            //base.OnOperationResponseReceived(operationResponse);

            if (operationResponse.ReturnCode != ErrorCode.Ok)
            {
                switch (operationResponse.OperationCode)
                {
                    case OperationCode.Authenticate:
                    case OperationCode.AuthenticateOnce:
                    case OperationCode.JoinGame:
                        this.Logger.Log(LogLevel.Error, "Voice operation {0} failed: {1}. {2}", operationResponse.OperationCode, operationResponse.ReturnCode, operationResponse.DebugMessage);
                        this.CancelRecovery(false);
                        this.errAuthOrJoin = !RecoverableError(operationResponse.ReturnCode);
                        this.quickRejoinAttempted = true;
                        this.retryAfterDisconnect = !this.errAuthOrJoin;
                        // Bypass the public deliberate-disconnect path. Recovery begins only
                        // after the transport is disconnected, never while an operation is busy.
                        if (this.ClientState == ClientState.Disconnected)
                        {
                            this.retryAfterDisconnect = false;
                            this.ScheduleRecovery();
                        }
                        else this.Client.Disconnect();
                        break;

                    default:
                        this.Logger.Log(LogLevel.Error, "Operation {0} response error code {1} message {2}", operationResponse.OperationCode, operationResponse.ReturnCode, operationResponse.DebugMessage);
                        break;
                }
            }
        }

        protected void LeaderStateChanged(ClientState toState)
        {
            this.Logger.Log(LogLevel.Info, "OnLeaderStateChanged to {0}", toState);
            if (toState == ClientState.Joined)
            {
                //clear the error state so Voice can try to connect once
                this.errAuthOrJoin = false;
                this.manualDisconnect = false;
                this.CancelRecovery(true);
            }
            if (!this.LeaderInRoom || this.LeaderOfflineMode) this.CancelRecovery(true);
            this.FollowLeader(toState);
        }

        protected override void OnVoiceStateChanged(ClientState fromState, ClientState toState)
        {
            base.OnVoiceStateChanged(fromState, toState);
            if (toState == ClientState.Joined)
            {
                this.joinedVoiceRoom = this.Client.CurrentRoom.Name;
                this.CancelRecovery(true);
            }
            if (toState == ClientState.Disconnected)
            {
                bool retry = this.retryAfterDisconnect || RecoverableDisconnect(this.Client.DisconnectedCause);
                this.retryAfterDisconnect = false;
                if (retry) this.ScheduleRecovery();
                return;
            }

            this.Logger.Log(LogLevel.Debug, "OnVoiceStateChanged  from {0} to {1}", fromState, toState);
            this.FollowLeader(toState);
        }

        private void ConnectOrJoinVoice()
        {
            switch (this.ClientState)
            {
                case ClientState.PeerCreated:
                case ClientState.Disconnected:
                    this.Logger.Log(LogLevel.Info, "Leader joined room, now connecting Voice client");
                    if (!this.ConnectVoice())
                    {
                        this.Logger.Log(LogLevel.Error, "Connecting to server failed.");
                        this.ScheduleRecovery();
                    }
                    break;
                case ClientState.ConnectedToMasterServer:
                    this.Logger.Log(LogLevel.Info, "Leader joined room, now joining Voice room");
                    if (!this.JoinVoiceRoom(GetVoiceRoomName()))
                    {
                        this.Logger.Log(LogLevel.Error, "Joining a voice room failed.");
                        this.ScheduleRecovery();
                    }
                    break;
                default:
                    this.Logger.Log(LogLevel.Warning, "Leader joined room, Voice client is busy ({0}). Is this expected?", this.ClientState);
                    break;
            }
        }

        protected virtual bool JoinVoiceRoom(string voiceRoomName)
        {
            if (string.IsNullOrEmpty(voiceRoomName))
            {
                this.Logger.Log(LogLevel.Error, "Voice room name is null or empty.");
                return false;
            }

            var roomParams = new EnterRoomParams
            {
                RoomOptions = new RoomOptions { IsVisible = false, PlayerTtl = 2000 },
                RoomName = voiceRoomName
            };

            return this.Client.OpJoinOrCreateRoom(roomParams);
        }

        private bool followPending;

        protected override void Update()
        {
            base.Update();
            if (!this.followPending) return;
            this.followPending = false;
            // StateChanged runs inside Realtime's join handler, before nickname/property updates.
            // Defer room switches until that handler finishes to avoid issuing SetProperties while Leaving.
            this.FollowLeader();
        }

        private void FollowLeader(ClientState toState)
        {
            switch (toState)
            {
                case ClientState.Joined:
                case ClientState.Disconnected:
                case ClientState.ConnectedToMasterServer:
                    this.Logger.Log(LogLevel.Debug, $"FollowLeader for state {toState}");
                    this.followPending = true;
                    break;
            }
        }

        private void FollowLeader()
        {
            // no matter what usually happens, if there was a deliberate disconnect call, don't react to state changes
            if (!this.isActiveAndEnabled || AppQuits || this.manualDisconnect || this.retryAfterDisconnect || this.recovery != null)
            {
                return;
            }

            // setting errAuthOrJoin to true should keep the client from automatically joining the lead room (unless this client is already on the way).
            if (this.AutoConnectAndJoin && !this.errAuthOrJoin || this.Client.IsConnected)
            {
                // if Leader is NOT in an online room, voice should disconnect
                if (!LeaderInRoom || LeaderOfflineMode)
                {
                    // Leave is only valid while Joined. A room switch hits ConnectedToMasterServer
                    // again when matchmaking fails, and by then this client is already Leaving.
                    if (this.Client.InRoom)
                    {
                        // Remove the player from the room to avoid an error when reconnecting without rejoining with the same UserId
                        this.Client.OpLeaveRoom(false);
                    }
                    return;
                }
                // as Leader is in an online room (checked above), voice might have to follow (and check if in the correct room)
                if (!this.AutoConnectAndJoin || this.errAuthOrJoin) return;
                if (!this.Client.InRoom)
                {
                    // Leader is in a room but the voice client not. follow with next steps!
                    this.ConnectOrJoinVoice();
                    return;
                }

                // if Leader is in a room and voice, too, make sure the voice room has the expected room name
                string expectedRoomName = GetVoiceRoomName();
                string currentRoomName = this.Client.CurrentRoom.Name;
                if (string.IsNullOrEmpty(currentRoomName) || !currentRoomName.Equals(expectedRoomName))
                {
                    this.Logger.Log(LogLevel.Warning,
                        "Voice room mismatch: Expected:\"{0}\" Current:\"{1}\", leaving the second to join the first.",
                        expectedRoomName, currentRoomName);
                    if (!this.Client.OpLeaveRoom(false))
                    {
                        this.Logger.Log(LogLevel.Error, "Leaving the current voice room failed.");
                    }
                }

                // if both clients are in matching rooms, everything is fine.
            }
        }

        #endregion
    }
}

