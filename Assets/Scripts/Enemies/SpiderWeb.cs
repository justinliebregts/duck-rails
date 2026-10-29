using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Game.AI
{
    /// <summary>Cosmetic only: never writes a pose or influences locomotion.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy), typeof(PhotonView))]
    public sealed class SpiderWeb : MonoBehaviourPunCallbacks, IOnPhotonViewControllerChange
    {
        [SerializeField] Transform attachment;
        [SerializeField] LineRenderer line;
        [SerializeField, Min(0.1f)] float maximumLength = 10f;
        const float TeleportDistance = 2f;
        Enemy _enemy;
        Vector3 _anchor, _previousRoot;
        bool _active, _blockedDeparture;
        int _sequence, _receivedSequence;
        bool _received;
        bool _hasTimeBoundary;
        int _acceptAfter;

        bool Authority => !PhotonNetwork.InRoom || photonView.IsMine;
        bool Interrupted => _enemy == null || _enemy.Root == null || _enemy.IsDormant
            || _enemy.IsKnocked || _enemy.IsStunned || _enemy.RagdollActive;
        public bool WebActive => _active;

        void Awake()
        {
            _enemy = GetComponent<Enemy>();
            Hide();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            photonView.AddCallback<IOnPhotonViewControllerChange>(this);
            _hasTimeBoundary = PhotonNetwork.InRoom;
            _acceptAfter = PhotonNetwork.ServerTimestamp;
        }

        public override void OnDisable()
        {
            Cancel();
            photonView.RemoveCallback<IOnPhotonViewControllerChange>(this);
            base.OnDisable();
        }

        public void OnControllerChange(Player current, Player previous)
        {
            Hide();
            _received = false;
            _blockedDeparture = true;
            _hasTimeBoundary = PhotonNetwork.InRoom;
            _acceptAfter = PhotonNetwork.ServerTimestamp;
            if (Authority) Send(null);
        }

        public override void OnLeftRoom()
        {
            Hide();
            _received = false;
            _hasTimeBoundary = false;
            _blockedDeparture = true;
        }

        public override void OnPlayerEnteredRoom(Player player)
        {
            if (Authority) Send(player);
        }

        // Called only after the mover accepts a pose. Its query results are observations.
        public void ObserveMove(bool departure, bool attached, Vector3 from, Vector3 to)
        {
            if (!Authority) return;
            if (attached)
            {
                Cancel();
                _blockedDeparture = false;
                return;
            }
            if (_active || _blockedDeparture || !departure || to.y >= from.y - 0.00001f || Interrupted) return;
            _blockedDeparture = true;
            // One start-only query verifies the fixed ceiling anchor. No ongoing probes.
            EnemyManager manager = EnemyManager.Instance;
            if (manager == null || !Physics.Raycast(from + Vector3.down * 0.05f, Vector3.up,
                out RaycastHit ceiling, 0.45f, manager.SurfaceMask, QueryTriggerInteraction.Ignore)
                || ceiling.normal.y > -0.5f || !ValidSegment(ceiling.point, to)) return;
            _anchor = ceiling.point;
            _previousRoot = to;
            _active = true;
            unchecked { ++_sequence; }
            Send(null);
        }

        public void Cancel()
        {
            bool wasActive = _active;
            Hide();
            if (!wasActive) return;
            _blockedDeparture = true;
            if (Authority)
            {
                unchecked { ++_sequence; }
                Send(null);
            }
        }

        void Hide()
        {
            _active = false;
            if (line != null && line.enabled) line.enabled = false;
        }

        void Send(Player player)
        {
            if (!PhotonNetwork.InRoom || photonView.ViewID == 0) return;
            // Reliable, unbuffered transitions; joining peers receive a targeted snapshot.
            // No additions to the recurring enemy pose packet.
            if (player != null) photonView.RPC(nameof(ReceiveWeb), player, _sequence, _active, _anchor);
            else photonView.RPC(nameof(ReceiveWeb), RpcTarget.Others, _sequence, _active, _anchor);
        }

        [PunRPC]
        void ReceiveWeb(int sequence, bool active, Vector3 anchor, PhotonMessageInfo info)
        {
            if (Authority || info.Sender != photonView.Controller
                || (_hasTimeBoundary && unchecked(info.SentServerTimestamp - _acceptAfter) < 0)) return;
            ApplySnapshot(sequence, active, anchor);
        }

        void ApplySnapshot(int sequence, bool active, Vector3 anchor)
        {
            if (_received && unchecked(sequence - _receivedSequence) <= 0) return;
            _received = true;
            _receivedSequence = sequence;
            _sequence = sequence;
            Hide();
            if (!active || Interrupted || !ValidSegment(anchor, _enemy.Root.position)) return;
            _anchor = anchor;
            _previousRoot = _enemy.Root.position;
            _active = true;
        }

        static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
            && !float.IsNaN(point.z) && !float.IsInfinity(point.z);

        bool ValidSegment(Vector3 anchor, Vector3 end) => Finite(anchor) && Finite(end)
            && (anchor - end).sqrMagnitude <= maximumLength * maximumLength
            && end.y <= anchor.y + 0.5f;

        void LateUpdate()
        {
            if (!_active) return;
            if (Interrupted || attachment == null || line == null)
            {
                Cancel();
                return;
            }
            Vector3 root = _enemy.Root.position;
            Vector3 end = attachment.position;
            if (!ValidSegment(_anchor, root) || !ValidSegment(_anchor, end)
                || (root - _previousRoot).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                Cancel();
                return;
            }
            _previousRoot = root;
            // Both points are valid before enabling, including the first visible frame.
            if (!line.enabled) line.SetPosition(0, _anchor);
            line.SetPosition(1, end);
            if (!line.enabled) line.enabled = true;
        }
    }
}
