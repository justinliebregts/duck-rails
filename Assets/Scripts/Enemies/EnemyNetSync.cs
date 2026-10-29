using Photon.Pun;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Sends a fixed 21-byte pose from the owner (room objects are owned by the master client).
    /// Other clients smooth toward the latest sample; their gait animates locally from the root.
    /// Add this to the PhotonView's Observed Components.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class EnemyNetSync : MonoBehaviourPun, IPunObservable, IOnPhotonViewControllerChange
    {
        [SerializeField] float positionSmooth = 12f;
        [SerializeField] float rotationSmooth = 12f;
        [SerializeField] float snapDistance = 3f;

        Enemy _enemy;
        EnemyPosePacket _write;
        EnemyPosePacket _sent;
        Vector3 _netPos;
        Quaternion _netRot = Quaternion.identity;
        bool _hasSample;
        const float PredictionTimeout = 2f;
        const float HandoffGrace = 0.35f;
        // Above the current fastest ground chase (6.5 m/s), so a moving
        // authority can be caught without a large single-frame correction.
        const float ReconcileSpeed = 8f;
        // Keep interpolation lag below the existing 0.5m handoff threshold
        // at high frame rates, including a target moving at 6.5 m/s.
        const float ReconcileSmoothing = 16f;
        int _hitSequence, _pendingHit;
        float _predictionUntil, _protectUntil, _pushRemaining;
        Vector3 _pushDirection;
        bool _hitConfirmed;
        bool _reconciling;
        int _confirmedTime, _sampleTime;
        int _predictionController, _sampleController;

        // A transport delay is not evidence that the client's defensive hit failed.
        // Only this zombie is harmless until its confirmed movement catches up.
        public bool ProtectsLocalHitter => _pendingHit != 0 || _reconciling || Time.time < _protectUntil;

        void ClearPrediction()
        {
            _pendingHit = 0;
            _pushRemaining = 0f;
            _protectUntil = 0f;
            _hitConfirmed = false;
            _reconciling = false;
        }

        void OnEnable() => photonView.AddCallback<IOnPhotonViewControllerChange>(this);

        void OnDisable()
        {
            photonView.RemoveCallback<IOnPhotonViewControllerChange>(this);
            ClearPrediction();
        }

        public void OnControllerChange(Photon.Realtime.Player newController, Photon.Realtime.Player previousController)
        {
            // A sleeping former master may regain control without ever ticking as a remote.
            if (_enemy != null) _enemy.OnAuthorityChanged();
            _hasSample = false;
            if (newController == null || !newController.IsLocal) return;
            // PUN skips serialization when alone, and the authority never calls
            // Apply. Retire the pending remote hit at the ownership transition.
            bool protectedHit = ProtectsLocalHitter;
            ClearPrediction();
            if (protectedHit) _protectUntil = Time.time + HandoffGrace;
        }

        public bool HasAuthority => !PhotonNetwork.InRoom || photonView.IsMine;

        public void ResetRemotePose(Vector3 position, Quaternion rotation)
        {
            _netPos = position;
            _netRot = rotation;
            _hasSample = false;
            ClearPrediction();
            if (_enemy != null) _enemy.SetAirbornePose(false);
        }

        void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _write = new EnemyPosePacket();
            _sent = new EnemyPosePacket();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            Transform root = _enemy.Root;
            if (stream.IsWriting)
            {
                bool aim = _enemy.TryGetAimPoint(out Vector3 aimPoint);
                float yaw = 0f;
                float pitch = 0f;
                if (aim) EnemyPosePacket.AimAngles(root.position, aimPoint, out yaw, out pitch);
                _write.Set(root.position, root.rotation, (byte)_enemy.CurrentState, aim, yaw, pitch);
                if (_enemy.IsJumping) _write.Flags |= EnemyPosePacket.JumpFlag;
                if (_enemy.IsKnocked) _write.Flags |= EnemyPosePacket.KnockFlag;
                stream.SendNext(_write);
                EnemyPosePacket swap = _write;
                _write = _sent;
                _sent = swap;
                return;
            }

            EnemyPosePacket packet = (EnemyPosePacket)stream.ReceiveNext();
            if (info.Sender != photonView.Controller
                || (_hasSample && unchecked(info.SentServerTimestamp - _sampleTime) < 0))
            {
                EnemyPosePacket.Return(packet);
                return;
            }
            _sampleTime = info.SentServerTimestamp;
            _sampleController = photonView.ControllerActorNr;
            _netPos = packet.Position;
            _netRot = packet.Rotation;
            _enemy.SetRemoteState((Enemy.State)packet.State);
            _enemy.SetAirbornePose((packet.Flags & EnemyPosePacket.JumpFlag) != 0);
            _enemy.SetRemoteKnocked(packet.Knocked);
            if (packet.HasAim)
                _enemy.SetRemoteAim(true, packet.AimPoint(_netPos));
            else
                _enemy.SetRemoteAim(false, default);
            EnemyPosePacket.Return(packet);
            _hasSample = true;
        }

        public void SubmitPlayerKill(Vector3 position)
        {
            if (HasAuthority) _enemy.ReceivePlayerKill(position);
            else photonView.RPC(nameof(RpcPlayerKill), photonView.Controller, position);
        }

        public void SendScarKill(Vector3 spiderPosition, Vector3 victimPosition)
        {
            // The victim's client may not own this enemy. Only broadcast this
            // cosmetic event; pose ownership and combat RPCs remain unchanged.
            if (PhotonNetwork.InRoom && photonView.ViewID != 0)
                photonView.RPC(nameof(RpcScarKill), RpcTarget.Others, spiderPosition, victimPosition);
        }

        [PunRPC]
        void RpcScarKill(Vector3 spiderPosition, Vector3 victimPosition)
        {
            if (_enemy != null && _enemy.Gait is Game.Animation.SpiderGait spider)
                spider.ReceiveKillScar(spiderPosition, victimPosition);
        }

        [PunRPC]
        void RpcPlayerKill(Vector3 position)
        {
            if (HasAuthority) _enemy.ReceivePlayerKill(position);
        }

        public void SubmitSwing(Vector3 swingVelocity, float weaponScale, Vector3 hitPosition)
        {
            SubmitHit(swingVelocity, hitPosition);
        }

        [PunRPC]
        void RpcHit(Vector3 velocity, Vector3 hitPosition, int sequence, PhotonMessageInfo info)
        {
            if (HasAuthority)
            {
                if (_enemy == null || _enemy.Root == null || _enemy.IsDormant) return;
                _enemy.ReceiveTrustedHit(velocity, hitPosition);
                photonView.RPC(nameof(RpcHitConfirmed), info.Sender, sequence, _enemy.Root.position, _enemy.Root.rotation);
            }
            else _enemy.ReceiveRemoteHitEffect(velocity, hitPosition);
        }

        public void SubmitThrownHit(Vector3 throwVelocity, Vector3 hitPosition)
        {
            SubmitHit(throwVelocity, hitPosition);
        }

        void SubmitHit(Vector3 velocity, Vector3 hitPosition)
        {
            int sequence = unchecked(++_hitSequence);
            if (sequence == 0) sequence = unchecked(++_hitSequence);
            if (!HasAuthority)
            {
                _pendingHit = sequence;
                _predictionController = photonView.ControllerActorNr;
                _hitConfirmed = false;
                _reconciling = false;
                _predictionUntil = Time.time + PredictionTimeout;
                _protectUntil = _predictionUntil + HandoffGrace;
                _pushRemaining = _enemy.LocalPushDistance(velocity, out _pushDirection);
            }
            if (PhotonNetwork.InRoom)
                photonView.RPC(nameof(RpcHit), RpcTarget.Others, velocity, hitPosition, sequence);
            _enemy.ReceiveSwing(velocity, 1f, hitPosition);
        }

        [PunRPC]
        void RpcHitConfirmed(int sequence, Vector3 position, Quaternion rotation, PhotonMessageInfo info)
        {
            if (_pendingHit == 0 || info.Sender != photonView.Controller || sequence != _pendingHit) return;
            _hitConfirmed = true;
            _confirmedTime = info.SentServerTimestamp;
            // PUN can suppress unchanged poses (e.g. a shove blocked by a wall).
            // Carry one authoritative snapshot in the acknowledgment, so that
            // handoff never depends on a later movement packet being sent.
            if (!_hasSample || unchecked(_confirmedTime - _sampleTime) >= 0)
            {
                _netPos = position;
                _netRot = rotation;
                _sampleTime = _confirmedTime;
                _sampleController = photonView.ControllerActorNr;
                _hasSample = true;
            }
        }

        bool TickLocalPrediction(Transform root, float dt)
        {
            if (_pendingHit != 0 && _enemy != null && root != null)
            {
                bool expired = Time.time >= _predictionUntil;
                _pushRemaining = expired ? 0f : _enemy.MoveLocalPush(_pushDirection, _pushRemaining, dt);
                bool ready = _pushRemaining <= 0f && _hitConfirmed && _hasSample
                    && unchecked(_sampleTime - _confirmedTime) >= 0;
                // Stop holding the visual pose on timeout, but retain the ID and
                // protection so a late confirmation can still finish the handoff.
                if (!ready) return !expired;
                _pendingHit = 0;
                _pushRemaining = 0f;
                _reconciling = true;
            }
            return false;
        }

        public void Apply(Transform root, float dt)
        {
            if (HasAuthority) { ClearPrediction(); return; }
            int controller = photonView.ControllerActorNr;
            if ((_pendingHit != 0 || _reconciling) && _predictionController != controller)
            {
                // The old master cannot confirm after migration. Reconcile with
                // the new controller's first pose while retaining local safety.
                _predictionController = controller;
                _pendingHit = 0;
                _pushRemaining = 0f;
                _hitConfirmed = false;
                _reconciling = true;
                _hasSample &= _sampleController == controller;
            }
            if (TickLocalPrediction(root, dt)) return;
            ApplySample(root, dt);
        }

        void ApplySample(Transform root, float dt)
        {
            if (!_hasSample || _enemy == null || root == null) return;
            bool protectedHit = ProtectsLocalHitter;
            if (!protectedHit && (root.position - _netPos).sqrMagnitude > snapDistance * snapDistance)
            {
                _enemy.SetRootPose(_netPos, _netRot);
                return;
            }

            float positionRate = protectedHit ? Mathf.Max(positionSmooth, ReconcileSmoothing) : positionSmooth;
            float positionT = 1f - Mathf.Exp(-positionRate * dt);
            float rotationT = 1f - Mathf.Exp(-rotationSmooth * dt);
            Vector3 pos = Vector3.Lerp(root.position, _netPos, positionT);
            // ApplySample runs after local shove prediction. Also cover a timed
            // out pending hit and the final grace period; never cap the shove.
            if (protectedHit)
                pos = Vector3.MoveTowards(root.position, pos, ReconcileSpeed * Mathf.Max(0f, dt));
            Quaternion rot = Quaternion.Slerp(root.rotation, _netRot, rotationT);
            _enemy.SetRootPose(pos, rot);
            // Allow normal interpolation lag for a moving target; a large
            // accumulated prediction error must settle before contact is lethal.
            if (_reconciling && (pos - _netPos).sqrMagnitude <= 0.25f)
            {
                _reconciling = false;
                _protectUntil = Time.time + HandoffGrace;
            }
        }
    }
}
