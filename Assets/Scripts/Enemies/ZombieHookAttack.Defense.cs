using Photon.Pun;
using UnityEngine;

namespace Game.AI
{
    public sealed partial class ZombieHookAttack
    {
        Contact localContact;
        Vector3 localContactPoint;
        bool observedEnd, defenseRequested, clearedLocalHit;

        struct Defense
        {
            public PhotonPlayer player;
            public bool received;
            public Contact contact;
            public Vector3 point;
            public float distance;
            public float firstReach, lastReach;
            public ulong reachable, blockable;
            public double lastHeld, holdProofDeadline;
        }

        readonly Defense[] defenses = new Defense[PhotonSession.MaxPlayers];
        int defenseCount;
        bool waiting;
        Contact authorityContact;
        PhotonPlayer authorityVictim;
        bool offlineVictim;
        const float DefenseTimeout = 2f;
        // Room-scale arm/item reach plus room for a late remote pose (about
        // 0.2 seconds at desktop sprint speed). Waiting for a reply adds no reach.
        const float DefenderReach = 3f + 1.6f;
        const double HoldReplicationGrace = 0.25;
        const double HoldDeliveryGrace = 0.4;
        const float ContactTie = 0.001f;
        Vector3 shotDirection;
        float shotLength, sweptDistance;
        double defenseDeadline;

        void ResetDefense()
        {
            localContact = Contact.Silent;
            observedEnd = defenseRequested = clearedLocalHit = false;
            System.Array.Clear(defenses, 0, defenseCount);
            defenseCount = 0;
            waiting = false;
            authorityContact = Contact.Silent;
            authorityVictim = null;
            offlineVictim = false;
            sweptDistance = 0f;
        }

        // Sample the newly displayed segment once, after the local grip has moved.
        // A hand raised while the authority is waiting cannot change an earlier contact.
        void ObserveDefense(Vector3 tip, bool atEnd)
        {
            var local = PhotonPlayer.Local;
            if (local == null) return;
            if (!local.CanReceiveHookReleasedAt(since))
            {
                localContact = Contact.Silent;
                observedEnd = true;
                clearedLocalHit = false;
            }
            if (!observedEnd)
            {
                Vector3 delta = tip - previousTip;
                float length = delta.magnitude;
                if (length > 0.00001f)
                {
                    Vector3 direction = delta / length;
                    float best = length;
                    bool item = HeldItemEntry(previousTip, direction, ref best, true);
                    if (local.TryGetHeadPosition(out var head) &&
                        (head - previousTip).sqrMagnitude <= (best + 2.5f) * (best + 2.5f) &&
                        FleshEntry(previousTip, direction, best, local, null, out float hit) &&
                        (!item || hit < best - 0.001f))
                    {
                        localContact = Contact.Flesh;
                        best = hit;
                    }
                    else if (item) localContact = Contact.Solid;
                    if (localContact != Contact.Silent) localContactPoint = previousTip + direction * best;
                }
                previousTip = tip;
                observedEnd = atEnd || localContact != Contact.Silent;
            }
            if (!defenseRequested || !observedEnd) return;
            defenseRequested = false;
            clearedLocalHit = localContact == Contact.Flesh;
            photonView.RPC(nameof(ReportHookDefense), photonView.Controller, revision, since, local.photonView.ViewID,
                (byte)localContact, localContactPoint);
        }

        void AskNearbyDefenders(PhotonPlayer target)
        {
            Vector3 shot = destination - origin;
            shotLength = shot.magnitude;
            if (shotLength <= 0f) return;
            shotDirection = shot / shotLength;
            // Include a hand-held item plus a sprint across the lane during flight.
            float reach = DefenderReach + 8f * flightTime;
            foreach (var player in PhotonPlayer.Active)
            {
                if (player == null || player.IsLocalHuman || player.IsCompanion) continue;
                if (player != target)
                {
                    if (!player.TryGetHeadPosition(out var head)) continue;
                    float distance = Mathf.Clamp(Vector3.Dot(head - origin, shotDirection), 0f, shotLength);
                    if ((origin + distance * shotDirection - head).sqrMagnitude > reach * reach) continue;
                }
                AskDefender(player);
            }
        }

        void AskDefender(PhotonPlayer player)
        {
            for (int i = 0; i < defenseCount; i++)
                if (defenses[i].player == player) return;
            var owner = player.photonView.Owner;
            if (owner == null || owner.IsInactive || player.IsDead || defenseCount == defenses.Length ||
                PhotonMasterHandoff.IsUnavailableInRoom(owner.ActorNumber)) return;
            ref Defense defense = ref defenses[defenseCount++];
            defense.player = player;
            defense.firstReach = float.PositiveInfinity;
            defense.lastHeld = HoldsBlocker(owner.ActorNumber) ? Time.realtimeSinceStartupAsDouble : double.NegativeInfinity;
            photonView.RPC(nameof(CheckHookDefense), owner, revision, since, player.photonView.ViewID);
        }

        [PunRPC]
        void CheckHookDefense(int shot, int release, int victim, PhotonMessageInfo info)
        {
            if (!isActiveAndEnabled || Authority || phase != Phase.Flight || revision != shot || since != release ||
                info.Sender != photonView.Controller) return;
            var local = PhotonPlayer.Local;
            if (local != null && local.photonView.ViewID == victim) defenseRequested = true;
        }

        [PunRPC]
        void ReportHookDefense(int shot, int release, int victim, byte firstContact, Vector3 point, PhotonMessageInfo info)
        {
            if (!isActiveAndEnabled || !Authority || phase != Phase.Flight || revision != shot || since != release ||
                info.Sender == null || (waiting && Time.realtimeSinceStartupAsDouble >= defenseDeadline)) return;
            for (int i = 0; i < defenseCount; i++)
            {
                ref Defense reply = ref defenses[i];
                if (reply.received || reply.player == null || reply.player.photonView.ViewID != victim ||
                    info.Sender != reply.player.photonView.Owner) continue;
                // Consume an invalid authenticated reply as a miss. It cannot hold
                // somebody else's hit open or be replaced by another guess.
                reply.received = true;
                if (firstContact == (byte)Contact.Silent || firstContact > (byte)Contact.Flesh) return;
                float distance = Mathf.Clamp(Vector3.Dot(point - origin, shotDirection), 0f, shotLength);
                if (!((origin + distance * shotDirection - point).sqrMagnitude <= 0.01f * 0.01f)) return;
                reply.contact = (Contact)firstContact;
                reply.distance = distance;
                reply.point = point;
                if (reply.contact == Contact.Solid)
                {
                    float start = Mathf.Min(hookPoint.localPosition.magnitude, shotLength);
                    float arrival = flightTime * Mathf.InverseLerp(start, shotLength, distance);
                    float sentAge = unchecked(info.SentServerTimestamp - since) * 0.001f;
                    // Bound when a new grip may be claimed separately from
                    // the time needed to receive its independent item snapshot.
                    if (sentAge >= -HoldReplicationGrace && sentAge <= arrival + HoldReplicationGrace &&
                        Age <= arrival + HoldReplicationGrace + HoldDeliveryGrace)
                        reply.holdProofDeadline = Time.realtimeSinceStartupAsDouble + HoldDeliveryGrace;
                }
                return;
            }
        }

        static bool HoldsBlocker(int actor)
        {
            foreach (var item in PhotonGrabbable.HeldItems)
                if (item != null && !item.IsStashed && item.HeldActor == actor) return true;
            return false;
        }

        // One bit per portion of the throw. Visiting two separated portions
        // does not grant the space between them. At 20 m each portion is 31 cm.
        int ReachBin(float distance) => Mathf.Clamp(Mathf.FloorToInt(distance / shotLength * 64f), 0, 63);

        void ObserveReach(ref Defense reply, float end, double now)
        {
            if (end <= sweptDistance || !reply.player.TryGetHeadPosition(out var head)) return;
            Vector3 offset = head - origin;
            float along = Vector3.Dot(offset, shotDirection);
            float discriminant = DefenderReach * DefenderReach - (offset - along * shotDirection).sqrMagnitude;
            if (discriminant < 0f) return;
            float half = Mathf.Sqrt(discriminant);
            float lo = Mathf.Max(sweptDistance, along - half);
            float hi = Mathf.Min(end, along + half);
            if (lo > hi) return;
            reply.firstReach = Mathf.Min(reply.firstReach, lo);
            reply.lastReach = Mathf.Max(reply.lastReach, hi);
            ulong portion = (ulong.MaxValue << ReachBin(lo)) & (ulong.MaxValue >> (63 - ReachBin(hi)));
            reply.reachable |= portion;
            if (HoldsBlocker(reply.player.photonView.OwnerActorNr)) reply.lastHeld = now;
            if (now - reply.lastHeld <= HoldReplicationGrace) reply.blockable |= portion;
        }

        bool ConfirmHeldItem(ref Defense reply, ulong bit, double now, out bool pending)
        {
            pending = false;
            if ((reply.blockable & bit) != 0) return true;
            if (now >= reply.holdProofDeadline) return false;
            if (HoldsBlocker(reply.player.photonView.OwnerActorNr))
            {
                reply.blockable |= bit;
                return true;
            }
            pending = true;
            return false;
        }

        void AdvanceFlight(Vector3 tip, bool atEnd)
        {
            // A zero-length tip step still checks the extended tether for moving cover.
            if (waiting) tip = previousTip;
            if (Trace(previousTip, tip, out var contact, out var hit, out var victim, out bool offline))
            {
                authorityContact = contact;
                authorityVictim = victim;
                offlineVictim = offline;
                tip = hit;
                atEnd = true;
            }
            previousTip = tip;
            if (defenseCount == 0)
            {
                if (atEnd) Finish(authorityContact, tip, authorityVictim, offlineVictim);
                else if (Networked) sweptDistance = Vector3.Dot(tip - origin, shotDirection);
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (atEnd && !waiting)
            {
                waiting = true;
                defenseDeadline = now + DefenseTimeout;
            }

            Contact first = authorityContact;
            Vector3 point = tip;
            PhotonPlayer player = authorityVictim;
            bool local = offlineVictim;
            if (player != null && player.IsLocalHuman && !player.CanReceiveHookReleasedAt(since))
            {
                first = Contact.Silent;
                player = null;
            }
            float endDistance = Mathf.Clamp(Vector3.Dot(tip - origin, shotDirection), 0f, shotLength);
            float nearest = endDistance;
            float earliestUnanswered = float.PositiveInfinity;
            bool timedOut = waiting && now >= defenseDeadline;
            for (int i = 0; i < defenseCount; i++)
            {
                ref Defense reply = ref defenses[i];
                if (reply.player == null || reply.player.IsDead || reply.player.photonView.Owner == null ||
                    reply.player.photonView.Owner.IsInactive || PhotonMasterHandoff.IsUnavailableInRoom(reply.player.photonView.OwnerActorNr)) continue;
                if (reply.received && reply.contact == Contact.Silent) continue;
                ObserveReach(ref reply, endDistance, now);
                if (!reply.received)
                {
                    earliestUnanswered = Mathf.Min(earliestUnanswered, reply.firstReach);
                    continue;
                }
                float distance = reply.distance;
                if (distance > nearest + ContactTie || distance < reply.firstReach - ContactTie ||
                    distance > reply.lastReach + ContactTie) continue;
                ulong bit = 1UL << ReachBin(distance);
                if ((reply.reachable & bit) == 0) continue;
                if (reply.contact == Contact.Solid && !ConfirmHeldItem(ref reply, bit, now, out bool pending))
                {
                    if (pending) earliestUnanswered = Mathf.Min(earliestUnanswered, distance);
                    continue;
                }
                if (distance >= nearest - ContactTie && first != Contact.Silent &&
                    (first == Contact.Solid || reply.contact != Contact.Solid)) continue;
                nearest = distance;
                first = reply.contact;
                point = reply.point;
                player = first == Contact.Flesh ? reply.player : null;
                local = false;
            }
            sweptDistance = Mathf.Max(sweptDistance, endDistance);
            if (earliestUnanswered <= nearest + ContactTie)
            {
                if (!timedOut) return;
                // Never pull through an unresolved possible interception. A
                // solid impact the authority actually witnessed still happened.
                if (first == Contact.Solid) Finish(Contact.Solid, point, null, false);
                else if (authorityContact == Contact.Solid) Finish(Contact.Solid, tip, null, false);
                else Finish(Contact.Silent, tip, null, false);
                return;
            }
            if (waiting || first != Contact.Silent) Finish(first, point, player, local);
        }
    }
}
