using Game.Animation;
using Game.Audio;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.AI
{
    // Sample the tip after the gait and the held-item swing/remote grip updates.
    [DefaultExecutionOrder(11000), DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy), typeof(PhotonView))]
    public sealed partial class ZombieHookAttack : MonoBehaviourPunCallbacks, IOnPhotonViewControllerChange, IPunInstantiateMagicCallback
    {
        enum Phase : byte { Idle, Aim, Flight, Reel }
        enum Contact : byte { Silent, Solid, Flesh }

        [SerializeField] TwoBoneLimb arm;
        [SerializeField] Transform hookHead, hookPoint;
        [SerializeField] MeshFilter chain;
        [SerializeField] SoundEvent throwSound, solidSound, fleshSound;
        [SerializeField, Min(0.1f)] float range = 20f, flightTime = 0.6f;
        [SerializeField, Min(0f)] float windup = 0.3f, cooldown = 4f;
        [SerializeField, Min(0.05f)] float reelTime = 0.22f;
        [SerializeField, Min(0.01f)] float tipRadius = 0.045f;
        [SerializeField, Min(0f)] float fullPullSpeed = 16.8f;
        const float FullPullDistance = 10f;

        Enemy enemy;
        Transform wrist;
        Vector3 hookAxis;
        Quaternion modelToForward, phaseRotation;
        byte roll;
        Mesh chainMesh;
        MeshRenderer chainRenderer;
        MaterialPropertyBlock chainProperties;
        static readonly int ExposedLength = Shader.PropertyToID("_ExposedLength");
        SubMeshDescriptor fullChain;
        const float LinkLength = 0.36f;
        const int IndicesPerLink = 36 * 3;
        int linkCount;
        float linkPitch;
        int visibleLinks, revision, since, acceptAfter;
        Phase phase;
        Vector3 origin, destination, previousTip;
        Object aimedOwner;
        float readyAt, probeAt, seekUntil;
        int flank;
        bool hasBoundary;
        bool Authority => !PhotonNetwork.InRoom || photonView.IsMine;
        bool Networked => PhotonNetwork.InRoom && photonView.ViewID != 0;
        static int Now => PhotonNetwork.InRoom ? PhotonNetwork.ServerTimestamp : unchecked((int)(Time.timeAsDouble * 1000));
        float Age => Mathf.Max(0f, unchecked(Now - since) * 0.001f);
        internal bool SeekingPosition => Time.time < seekUntil;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            wrist = hookHead.parent;
            hookAxis = hookPoint.localPosition.normalized;
            modelToForward = Quaternion.FromToRotation(hookAxis, Vector3.forward);
            chainRenderer = chain.GetComponent<MeshRenderer>();
            SetChainVisible(false);
            chainRenderer.enabled = true;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            photonView.AddCallback<IOnPhotonViewControllerChange>(this);
            PhotonMasterHandoff.LocalAvailabilityChanged += OnLocalAvailabilityChanged;
            hasBoundary = Networked;
            acceptAfter = Now;
            readyAt = probeAt = seekUntil = 0f;
        }

        public override void OnDisable()
        {
            PhotonMasterHandoff.LocalAvailabilityChanged -= OnLocalAvailabilityChanged;
            photonView.RemoveCallback<IOnPhotonViewControllerChange>(this);
            Stow();
            base.OnDisable();
        }

        void OnDestroy()
        {
            if (chainMesh != null) Destroy(chainMesh);
        }

        internal bool TickChase(EnemyManager manager, int slot, float dt)
        {
            if (Networked && !PhotonMasterHandoff.LocalAvailable) return false;
            if (phase == Phase.Flight) return true;
            if (phase == Phase.Reel) return false;
            if (enemy.CurrentState != Enemy.State.Chase || slot < 0)
            {
                AbandonAim();
                return false;
            }
            Object owner = manager.TargetOwner(slot);
            if (owner == null || owner is PhotonPlayer duck && (duck.IsCompanion ||
                Networked && PhotonMasterHandoff.IsUnavailableInRoom(duck.photonView.OwnerActorNr)))
            {
                AbandonAim();
                return false;
            }
            manager.TryGetTarget(slot, out Vector3 aim);
            Vector3 shoulder = arm.RootPos;
            if (phase == Phase.Aim)
            {
                if (owner != aimedOwner || !enemy.Seen || Expired(phase, since)) { AbandonAim(); return false; }
                Vector3 flat = Vector3.ProjectOnPlane(aim - enemy.Root.position, Vector3.up);
                if (flat.sqrMagnitude > 0.001f)
                    enemy.SetRootPose(enemy.Root.position, Quaternion.RotateTowards(enemy.Root.rotation,
                        Quaternion.LookRotation(flat), 360f * dt));
                if (Age < windup) return true;
                if (!CanLineUp(shoulder, aim, manager) || !CanThrow(wrist.position, aim, manager))
                {
                    AbandonAim();
                    TryFlank(manager, aim);
                    return false;
                }
                previousTip = wrist.position;
                Change(Phase.Flight, previousTip, aim);
                if (Networked) AskNearbyDefenders(owner as PhotonPlayer);
                return true;
            }
            if (Time.time < probeAt || !enemy.HasRecentSighting) return false;
            probeAt = Time.time + 0.25f;
            if (!enemy.Seen) aim = enemy.LastSeenPos;
            float aimingRange = range + AimingReach;
            if ((aim - shoulder).sqrMagnitude > aimingRange * aimingRange) return false;
            if (!enemy.Seen || !CanLineUp(shoulder, aim, manager))
            {
                if (!SeekingPosition) TryFlank(manager, aim);
                return false;
            }
            seekUntil = 0f;
            if (Time.time < readyAt) return false;
            aimedOwner = owner;
            enemy.Locomotion.Interrupt();
            roll = (byte)Random.Range(0, 256);
            Change(Phase.Aim, wrist.position, aim);
            return true;
        }

        float AimingReach => arm.TotalReach * 0.93f;

        // The gait plants the hand low. Test the raised arm's firing lane before committing
        // to wind-up, then enforce the actual wrist-to-head range and clearance at release.
        bool CanLineUp(Vector3 shoulder, Vector3 head, EnemyManager manager) =>
            ClearLane(shoulder, head, manager, range + AimingReach);

        bool CanThrow(Vector3 from, Vector3 to, EnemyManager manager) => ClearLane(from, to, manager, range);

        bool ClearLane(Vector3 from, Vector3 to, EnemyManager manager, float maxDistance)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            int cover = manager.VisionMask | GrabbableMask | 1;
            return length > tipRadius && length <= maxDistance && !OverlapObstacle(from, cover, out _)
                && !SweepObstacle(from, d / length, length, cover, out _);
        }

        void TryFlank(EnemyManager manager, Vector3 aim)
        {
            var graph = manager.Graph;
            if (graph == null) return;
            Vector3 root = enemy.Root.position;
            Vector3 side = Vector3.Cross(Vector3.up, aim - root).normalized;
            Vector3 shoulderOffset = (Vector3)arm.RootPos - root;
            bool elevated = aim.y > root.y + shoulderOffset.y + 0.75f;
            Vector3 away = Vector3.Cross(Vector3.up, side);
            int start = graph.FindNearestGroundNode(root, 1.5f);
            if (start < 0 || side.sqrMagnitude < 0.5f) return;
            // At most two local node lookups per attempt. Movement uses the normal budgeted path queue.
            for (int i = 0; i < 2; i++)
            {
                int candidate = flank % (elevated ? 15 : 6);
                flank = candidate + 1;
                Vector3 direction = (candidate & 1) == 0 ? side : -side;
                float offset = 1.8f + candidate / 2 % 3;
                if (elevated)
                {
                    // At the foot of a bus, moving closer keeps the roof between hand and head.
                    // Try stepping back, back diagonally, and sideways at increasing distances.
                    int angle = candidate % 5;
                    if (angle == 0) direction = away;
                    else if (angle < 3) direction = (away + direction).normalized;
                    offset = 2.5f + candidate / 5 * 1.5f;
                }
                int node = graph.FindNearestGroundNode(root + direction * offset, 1.25f);
                if (node < 0 || node == start || graph.GetComponent(node) != graph.GetComponent(start)) continue;
                Vector3 goal = graph.GetPosition(node);
                if (!CanLineUp(goal + shoulderOffset, aim, manager)) continue;
                seekUntil = Time.time + 1.2f + offset * 0.2f;
                enemy.Locomotion.ForcePathToWorld(manager, goal);
                return;
            }
            probeAt = Time.time + 0.65f;
        }

        void AbandonAim()
        {
            seekUntil = 0f;
            if (phase != Phase.Aim) return;
            // No shot was released. Let normal chase retry without consuming the throw cooldown.
            probeAt = Time.time + 0.25f;
            Change(Phase.Idle, wrist.position, wrist.position);
        }

        internal void Interrupt()
        {
            seekUntil = 0f;
            if (!Authority || (phase != Phase.Aim && phase != Phase.Flight)) return;
            Finish(Contact.Silent, phase == Phase.Flight ? previousTip : hookPoint.position, null, false);
        }

        void LateUpdate()
        {
            if (phase == Phase.Idle) return;
            if (Expired(phase, since))
            {
                if (Authority && phase != Phase.Reel) CancelShot();
                else Stow();
                return;
            }
            if (enemy.IsDormant || !enemy.isActiveAndEnabled) { if (Authority) Interrupt(); Stow(); return; }
            if (Authority && (enemy.IsKnocked || enemy.IsStunned || enemy.CurrentState == Enemy.State.Retreat)) Interrupt();
            if (phase == Phase.Aim || phase == Phase.Flight)
            {
                Vector3 shoulder = arm.RootPos;
                Vector3 delta = destination - shoulder;
                float distance = delta.magnitude;
                if (distance > 0.001f)
                {
                    Vector3 direction = delta / distance;
                    Quaternion facing = Quaternion.LookRotation(direction, enemy.Root.up);
                    float reach = Mathf.Min(AimingReach, distance * 0.9f);
                    arm.Solve(shoulder + direction * reach, facing,
                        shoulder + enemy.Root.right - enemy.Root.up * 0.25f);
                    wrist.rotation = AlignHook(wrist.rotation, direction);
                }
            }
            if (phase == Phase.Aim)
            {
                float t = windup > 0f ? Mathf.SmoothStep(0f, 1f, Age / windup) : 1f;
                Quaternion aimed = AlignHook(phaseRotation, wrist.rotation * hookAxis);
                hookHead.rotation = Quaternion.Slerp(wrist.rotation, aimed, t);
                return;
            }
            if (phase == Phase.Flight)
            {
                float t = Mathf.Clamp01(Age / flightTime);
                Vector3 point = FlightTip(t);
                if (Authority)
                {
                    AdvanceFlight(point, t >= 1f);
                    point = previousTip;
                }
                else
                {
                    ObserveDefense(point, t >= 1f);
                    if (localContact != Contact.Silent) point = localContactPoint;
                }
                if (phase == Phase.Flight) Draw(point, phaseRotation);
            }
            if (phase == Phase.Reel)
            {
                float t = Mathf.Clamp01(Age / reelTime);
                if (t >= 1f) { Stow(); return; }
                Vector3 rest = wrist.TransformPoint(hookPoint.localPosition);
                Vector3 direction = Vector3.Slerp((origin - wrist.position).normalized,
                    wrist.rotation * hookAxis, t);
                if (direction.sqrMagnitude < 0.001f) direction = wrist.rotation * hookAxis;
                Quaternion rotation = Quaternion.Slerp(AlignHook(phaseRotation, direction),
                    AlignHook(wrist.rotation, direction), Mathf.SmoothStep(0f, 1f, t));
                Draw(Vector3.Lerp(origin, rest, t), rotation);
            }
        }

        Vector3 FlightTip(float t)
        {
            // The leading edge starts one hook-length out. Sweep from the wrist on release.
            var start = Vector3.MoveTowards(origin, destination, hookPoint.localPosition.magnitude);
            return Vector3.Lerp(start, destination, t);
        }

        Quaternion AlignHook(Quaternion rotation, Vector3 direction) =>
            Quaternion.FromToRotation(rotation * hookAxis, direction) * rotation;

        void Draw(Vector3 point, Quaternion rotation)
        {
            hookHead.rotation = rotation;
            hookHead.position += point - hookPoint.position;
            Vector3 line = hookHead.position - wrist.position;
            float length = line.magnitude;
            if (length < 0.04f) { SetChainVisible(false); return; }
            if (chainMesh == null)
            {
                chainMesh = chain.mesh;
                fullChain = chainMesh.GetSubMesh(0);
                linkCount = fullChain.indexCount / IndicesPerLink;
                linkPitch = (chainMesh.bounds.size.z - LinkLength) / (linkCount - 1);
                chainProperties = new MaterialPropertyBlock();
            }
            int count = Mathf.Clamp(1 + Mathf.FloorToInt(length / linkPitch), 1, linkCount);
            if (count != visibleLinks)
            {
                var part = fullChain;
                part.indexCount = IndicesPerLink * count;
                var bounds = fullChain.bounds;
                float span = LinkLength + (count - 1) * linkPitch;
                bounds.SetMinMax(new Vector3(bounds.min.x, bounds.min.y, 0f),
                    new Vector3(bounds.max.x, bounds.max.y, span));
                part.bounds = bounds;
                chainMesh.SetSubMesh(0, part, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                chainMesh.bounds = bounds;
                visibleLinks = count;
            }
            chain.transform.SetPositionAndRotation(hookHead.position, Quaternion.LookRotation(-line, enemy.Root.up));
            chainProperties.SetFloat(ExposedLength, length);
            chainRenderer.SetPropertyBlock(chainProperties);
            SetChainVisible(true);
        }

        void SetChainVisible(bool visible)
        {
            if (chain.gameObject.activeSelf != visible) chain.gameObject.SetActive(visible);
        }

        void Stow()
        {
            ResetDefense();
            phase = Phase.Idle;
            aimedOwner = null;
            if (hookHead != null) hookHead.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (chainRenderer != null) SetChainVisible(false);
        }

        // Bounds the remote visual even if the controller stops running before
        // sending its result. Use wall-clock Photon time, including time spent asleep.
        bool Expired(Phase state, int stamp)
        {
            float age = unchecked(Now - stamp) * 0.001f;
            if (state == Phase.Flight) return age > flightTime + DefenseTimeout + 0.5f;
            if (state == Phase.Aim) return age > windup + DefenseTimeout;
            return state == Phase.Reel && age > reelTime + DefenseTimeout;
        }

        void CancelShot()
        {
            readyAt = Time.time + cooldown;
            seekUntil = 0f;
            Change(Phase.Idle, wrist.position, wrist.position);
        }

        void OnLocalAvailabilityChanged()
        {
            if (!Networked) return;
            hasBoundary = true;
            acceptAfter = Now;
            if (Authority && phase != Phase.Idle) CancelShot();
            else Stow();
        }

        void Finish(Contact contact, Vector3 point, PhotonPlayer victim, bool offlineLocal)
        {
            readyAt = Time.time + cooldown;
            Vector3 pull = Vector3.zero;
            if (victim != null && !victim.IsCompanion || offlineLocal)
            {
                Vector3 center = victim != null && victim.TryGetHeadPosition(out Vector3 head) ? head : point;
                Vector3 toward = (Vector3)arm.RootPos - center;
                pull = toward.normalized * (fullPullSpeed * Mathf.Clamp01(toward.magnitude / FullPullDistance));
            }
            // A wind-up can be interrupted partway through its twist. Start every
            // peer's return from the orientation actually reached on the authority.
            Quaternion reelStart = phase == Phase.Flight ? phaseRotation : hookHead.rotation;
            Change(Phase.Reel, point, reelStart.eulerAngles, contact, victim != null ? victim.photonView.ViewID : 0, pull);
            if (offlineLocal) PhotonPlayer.ApplyLocalHookPull(pull);
        }

        void Change(Phase next, Vector3 from, Vector3 targetOrReelAngles, Contact contact = Contact.Silent, int victim = 0, Vector3 pull = default)
        {
            revision = unchecked(revision + 1);
            Apply(next, Now, from, targetOrReelAngles, contact, victim, pull, false);
            if (Networked) SendState(null, contact, victim, pull);
        }

        void SendState(Player peer, Contact contact, int victim, Vector3 pull)
        {
            // Reel already has the impact point in origin, so its target slot can carry the starting rotation.
            Vector3 targetOrReelAngles = phase == Phase.Reel ? phaseRotation.eulerAngles : destination;
            // Only a resolved hit has a victim. Reuse that wire slot during wind-up
            // and flight so the RPC keeps its existing signature and packet size.
            bool aimingOrFlying = phase == Phase.Aim || phase == Phase.Flight;
            int rollOrVictim = aimingOrFlying ? roll : victim;
            if (peer == null)
                photonView.RPC(nameof(ReceiveHookState), RpcTarget.Others, revision, (byte)phase, since,
                    origin, targetOrReelAngles, (byte)contact, rollOrVictim, pull, false);
            else photonView.RPC(nameof(ReceiveHookState), peer, revision, (byte)phase, since,
                origin, targetOrReelAngles, (byte)Contact.Silent, rollOrVictim, Vector3.zero, true);
        }

        [PunRPC]
        void ReceiveHookState(int version, byte state, int timestamp, Vector3 from, Vector3 targetOrReelAngles,
            byte contact, int rollOrVictim, Vector3 pull, bool snapshot, PhotonMessageInfo info)
        {
            if (!isActiveAndEnabled || Authority || info.Sender != photonView.Controller ||
                (hasBoundary && (unchecked(info.SentServerTimestamp - acceptAfter) < 0 || unchecked(timestamp - acceptAfter) < 0)) ||
                unchecked(version - revision) <= 0) return;
            var next = (Phase)state;
            // A result is meaningful only for the uninterrupted release that
            // preceded it. Queued RPCs and join snapshots must never launch a waking player.
            bool liveResult = (phase == Phase.Flight || (phase == Phase.Aim && contact == (byte)Contact.Silent)) &&
                unchecked(version - revision) == 1 && !Expired(phase, since);
            revision = version;
            if (!PhotonMasterHandoff.LocalAvailable || Expired(next, timestamp))
            {
                Stow();
                return;
            }
            if (next == Phase.Aim || next == Phase.Flight) roll = (byte)rollOrVictim;
            int victim = next == Phase.Reel ? rollOrVictim : 0;
            // Older senders repeated the impact point in the unused reel target slot.
            if (next == Phase.Reel && targetOrReelAngles.Equals(from)) targetOrReelAngles = hookHead.eulerAngles;
            if (next == Phase.Reel && !snapshot)
            {
                var local = PhotonPlayer.Local;
                if (!liveResult)
                {
                    Stow();
                    return;
                }
                // A committed impact still belongs at its original world position.
                // A warp or respawn invalidates only the impulse on this avatar.
                if (victim != 0 && local != null && local.photonView.ViewID == victim &&
                    (!clearedLocalHit || !local.CanReceiveHookReleasedAt(since))) victim = 0;
            }
            Apply(next, timestamp, from, targetOrReelAngles, (Contact)contact, victim, pull, snapshot);
        }

        void Apply(Phase next, int timestamp, Vector3 from, Vector3 targetOrReelAngles, Contact contact, int victim, Vector3 pull, bool snapshot)
        {
            ResetDefense();
            phase = next;
            since = timestamp;
            origin = from;
            if (next == Phase.Idle) { Stow(); return; }
            if (next == Phase.Reel) phaseRotation = Quaternion.Euler(targetOrReelAngles);
            else
            {
                destination = targetOrReelAngles;
                Vector3 axis = (destination - origin).normalized;
                if (axis.sqrMagnitude < 0.001f) axis = Vector3.forward;
                Vector3 up = Mathf.Abs(axis.y) > 0.9999f ? Vector3.forward : Vector3.up;
                phaseRotation = Quaternion.LookRotation(axis, up)
                    * Quaternion.AngleAxis(roll * (360f / 256f), Vector3.forward) * modelToForward;
                if (next == Phase.Flight)
                {
                    previousTip = snapshot ? FlightTip(Age / flightTime) : origin;
                    observedEnd = snapshot && Age >= flightTime;
                }
            }
            if (snapshot) return;
            SoundEvent sound = next == Phase.Flight ? throwSound : contact == Contact.Flesh ? fleshSound :
                contact == Contact.Solid ? solidSound : null;
            if (sound != null && AudioManager.instance != null) AudioManager.instance.Play(sound, from);
            if (victim != 0 && contact == Contact.Flesh)
            {
                var local = PhotonPlayer.Local;
                if (local != null && local.photonView.ViewID == victim && !local.IsDead)
                    PhotonPlayer.ApplyLocalHookPull(pull);
            }
        }

        public override void OnPlayerEnteredRoom(Player peer)
        {
            if (Authority && Networked && phase != Phase.Idle) SendState(peer, Contact.Silent, 0, Vector3.zero);
        }

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            // A pooled spawn starts a new sequence. Re-enabling this same live
            // view must continue the sequence its peers have already accepted.
            revision = 0;
            // An active-shot snapshot can have been sent before this peer finished
            // loading the room object. Use the server's spawn time, not local enable time.
            hasBoundary = true;
            acceptAfter = info.SentServerTimestamp;
        }

        public override void OnLeftRoom() => Stow();
        public override void OnDisconnected(DisconnectCause cause) => Stow();

        public void OnControllerChange(Player current, Player previous)
        {
            Stow();
            revision = 0;
            acceptAfter = Now;
            hasBoundary = true;
            seekUntil = 0f;
            readyAt = Time.time + cooldown;
        }
    }
}
