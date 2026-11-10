using Game.Animation;
using UnityEngine;

namespace Game.AI
{
    public sealed partial class ZombieHookAttack
    {
        readonly Vector3[] blockSamples = new Vector3[Grabbable.MaxLocomotionSamples];
        Collider[] overlaps = new Collider[4];
        RaycastHit[] sweepHits = new RaycastHit[8];
        const int GrabbableMask = 1 << 8;

        // The holder disables normal physics collisions; a remote copy can still
        // have a solid collider. Resolve held items through the same grip samples on either peer.
        static bool HeldItemCollider(Collider collider) => collider.attachedRigidbody is Rigidbody body
            && body.TryGetComponent<PhotonGrabbable>(out var item) && item.IsHeldByAnyPlayer;

        bool OverlapObstacle(Vector3 point, int mask, out Collider obstacle)
        {
            while (true)
            {
                int n = Physics.OverlapSphereNonAlloc(point, tipRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (HeldItemCollider(overlaps[i])) continue;
                    obstacle = overlaps[i];
                    return true;
                }
                if (n < overlaps.Length) break;
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            obstacle = null;
            return false;
        }

        bool SweepObstacle(Vector3 from, Vector3 direction, float length, int mask, out RaycastHit hit)
        {
            if (!Physics.SphereCast(from, tipRadius, direction, out hit, length, mask, QueryTriggerInteraction.Ignore)) return false;
            if (!HeldItemCollider(hit.collider)) return true;
            // Only a held collider in front requires another query. A wall or a
            // loose object behind it must still win at its actual distance.
            int n;
            while ((n = Physics.SphereCastNonAlloc(from, tipRadius, direction, sweepHits, length, mask,
                QueryTriggerInteraction.Ignore)) == sweepHits.Length)
                System.Array.Resize(ref sweepHits, sweepHits.Length * 2);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var candidate = sweepHits[i];
                if (candidate.distance >= best || HeldItemCollider(candidate.collider)) continue;
                best = candidate.distance;
                hit = candidate;
            }
            return best < float.PositiveInfinity;
        }

        bool Trace(Vector3 from, Vector3 to, out Contact contact, out Vector3 point, out PhotonPlayer victim, out bool offline)
        {
            contact = Contact.Silent;
            point = to;
            victim = null;
            offline = false;
            var manager = EnemyManager.Instance;
            if (manager == null) return false;
            int solids = manager.VisionMask | manager.SurfaceMask | GrabbableMask | 1;
            // SphereCast omits a collider which moved over the tip since the last step.
            if (OverlapObstacle(from, solids, out var overlapping))
            {
                contact = SurfaceContact(overlapping, out victim);
                point = from;
                return true;
            }
            // Cover can cross the already-extended chain between two flight steps.
            Vector3 tether = from - wrist.position;
            if (tether.sqrMagnitude > tipRadius * tipRadius && SweepObstacle(wrist.position,
                tether.normalized, tether.magnitude, solids, out var across))
            {
                contact = Contact.Solid;
                point = across.point;
                return true;
            }
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.00001f) return false;
            Vector3 direction = delta / length;
            float best = length;
            if (SweepObstacle(from, direction, length, solids, out var solid))
            {
                best = solid.distance;
                contact = SurfaceContact(solid.collider, out victim);
            }
            // Baked weapon samples include compound shapes such as the frying pan.
            bool ownerReports = Networked && Authority && phase == Phase.Flight;
            if (HeldItemEntry(from, direction, ref best, ownerReports))
            {
                contact = Contact.Solid;
                victim = null;
            }
            foreach (var player in PhotonPlayer.Active)
            {
                if (player == null || player.IsDead || !player.TryGetHeadPosition(out Vector3 head)
                    || (head - from).sqrMagnitude > (best + 2.5f) * (best + 2.5f)) continue;
                if (ownerReports && !player.IsCompanion)
                {
                    if (player.photonView.Owner != Photon.Pun.PhotonNetwork.LocalPlayer)
                    {
                        AskDefender(player);
                        continue;
                    }
                    if (!player.CanReceiveHookReleasedAt(since)) continue;
                }
                if (!FleshEntry(from, direction, best, player, null, out float hit)) continue;
                if (contact != Contact.Silent && hit >= best - 0.001f) continue;
                best = hit;
                contact = Contact.Flesh;
                victim = player;
            }
            foreach (var other in Enemy.Active)
            {
                if (other == enemy || other == null || other.IsDormant ||
                    (other.Root.position - from).sqrMagnitude > (best + 3f) * (best + 3f)) continue;
                if (!FleshEntry(from, direction, best, null, other, out float hit)) continue;
                if (contact != Contact.Silent && hit >= best - 0.001f) continue;
                best = hit;
                contact = Contact.Flesh;
                victim = null;
            }
            if (!Photon.Pun.PhotonNetwork.InRoom)
            {
                for (int s = 0; s < EnemyManager.MaxTargets; s++)
                {
                    Object owner = manager.TargetOwner(s);
                    if (owner == null || owner is PhotonPlayer || !manager.TryGetTarget(s, out Vector3 head)) continue;
                    var rig = owner as LocalBodyDriver;
                    float hit;
                    if (rig != null)
                    {
                        if ((head - from).sqrMagnitude > (best + 2.5f) * (best + 2.5f)
                            || !FleshEntry(from, direction, best, null, null, out hit, rig)) continue;
                    }
                    else if (!SphereEntry(from, direction, head, tipRadius + 0.15f, best, out hit)) continue;
                    if (contact != Contact.Silent && hit >= best - 0.001f) continue;
                    best = hit;
                    contact = Contact.Flesh;
                    victim = null;
                    offline = rig != null;
                }
            }
            point = from + direction * best;
            return contact != Contact.Silent;
        }

        bool HeldItemEntry(Vector3 from, Vector3 direction, ref float best, bool localOnly)
        {
            bool found = false;
            foreach (var held in PhotonGrabbable.HeldItems)
            {
                if (held == null || held.IsStashed || (localOnly && !held.IsHeld) ||
                    (held.transform.position - from).sqrMagnitude > (best + 3f) * (best + 3f)) continue;
                int count = held.CopyHookBlockSamples(blockSamples, out float radius);
                for (int i = 0; i < count; i++)
                {
                    if (!SphereEntry(from, direction, blockSamples[i], tipRadius + radius, best, out float hit)) continue;
                    best = hit;
                    found = true;
                }
            }
            return found;
        }

        static Contact SurfaceContact(Collider collider, out PhotonPlayer victim)
        {
            victim = collider.GetComponentInParent<PhotonPlayer>();
            if (victim != null && victim.IsDead) { victim = null; return Contact.Flesh; }
            return victim != null || collider.GetComponentInParent<Enemy>() != null ||
                collider.GetComponentInParent<Ragdoll>() != null ? Contact.Flesh : Contact.Solid;
        }

        bool FleshEntry(Vector3 from, Vector3 direction, float distance, PhotonPlayer avatar, Enemy other,
            out float hit, LocalBodyDriver rig = null)
        {
            bool Overlap(float d)
            {
                Vector3 end = from + direction * d;
                if (avatar != null) return avatar.OverlapsHook(from, end, tipRadius);
                if (other != null) return other.OverlapsSwing(from, end, tipRadius);
                return PhotonPlayer.OverlapsOfflineHook(rig, from, end, tipRadius);
            }
            hit = distance;
            if (!Overlap(distance)) return false;
            float low = 0f;
            // Refine only actual hits. Prefix overlap gives the earliest body/limb contact.
            for (int i = 0; i < 9; i++)
            {
                float mid = (low + hit) * 0.5f;
                if (Overlap(mid)) hit = mid;
                else low = mid;
            }
            return true;
        }

        static bool SphereEntry(Vector3 from, Vector3 dir, Vector3 center, float radius, float limit, out float hit)
        {
            Vector3 offset = center - from;
            float along = Vector3.Dot(offset, dir);
            float discriminant = radius * radius - (offset.sqrMagnitude - along * along);
            hit = 0f;
            if (discriminant < 0f) return false;
            float half = Mathf.Sqrt(discriminant);
            if (along + half < 0f) return false;
            hit = Mathf.Max(0f, along - half);
            return hit <= limit;
        }
    }
}
