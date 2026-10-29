using Photon.Pun;
using UnityEngine;

public sealed partial class DuckCompanion
{
    bool sidewaysClimbing, sidewaysProbe, sidewaysExitClimb;
    Vector3 sidewaysEnd, sidewaysLanding, sidewaysWallDirection;
    Transform sidewaysExitSurface;

    bool TrySidewaysClimb(Vector3 direction, Vector3 destination) => RequestNavigation(NavigationPlan.Sideways, destination, default, direction);

    System.Collections.Generic.IEnumerable<int> PlanSideways(Vector3 direction, Vector3 destination)
    {

        if (traversalProbeOrigin.HasValue || sidewaysClimbing || fenceDetourCommitted
            || (PhotonNetwork.Time < failedFenceUntil
                && Vector3.ProjectOnPlane(transform.position - failedFenceCrossing, Vector3.up).sqrMagnitude < 16f))
            { planSucceeded = false; yield break; }
        Vector3 origin = transform.position;
        if (!Physics.Raycast(origin + Vector3.up * eyeHeight, direction, out RaycastHit wall,
            0.85f, environment, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > 0.35f) { planSucceeded = false; yield break; }
        if (IsFence(wall.collider)) RegisterFenceAttempt(wall, destination);
        if (fenceAttemptUntil > 0 && PhotonNetwork.Time >= fenceAttemptUntil) { planSucceeded = false; yield break; }
        Vector3 inward = -Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, inward);
        if (Vector3.Dot(tangent, destination - origin) < 0) tangent = -tangent;
        // Fixed candidate count. A shimmy is useful only if it connects to a
        // fully checked exit; it never starts an unbounded wall search.
        for (int step = 0; step < 3; step++)
        for (int side = 0; side < 2; side++)
        {
            yield return 0;
            float distance = step == 0 ? 0.75f : step == 1 ? 1.25f : 2f;
            float duration = distance / 1.2f;
            if (fenceAttemptUntil > 0 && PhotonNetwork.Time + duration >= fenceAttemptUntil) continue;
            Vector3 candidate = origin + tangent * (side == 0 ? distance : -distance);
            if (!ClearSidewaysClimb(origin, candidate, inward)) continue;
            bool exitFound = false;
            traversalProbeOrigin = candidate;
            sidewaysProbe = true;
            try
            {
                Vector3 toward = Vector3.ProjectOnPlane(destination - candidate, Vector3.up).normalized;
                for (int angle = 0; angle < 3 && !exitFound; angle++)
                {
                    foreach (var tick in PlanTraversalDirection(Quaternion.AngleAxis(angle == 0 ? 0 : angle == 1 ? 30 : -30, Vector3.up) * toward, destination)) yield return tick;
                    exitFound = planSucceeded;
                }
            }
            finally { traversalProbeOrigin = null; sidewaysProbe = false; }
            if (!exitFound) continue;
            // Reject exits that merely lead back to the original approach.
            if (Vector3.Distance(sidewaysLanding, destination) >= Vector3.Distance(origin, destination) - 0.2f) continue;
            sidewaysEnd = candidate;
            sidewaysWallDirection = inward;
            traversalStart = origin;
            traversalTime = 0;
            traversalDuration = duration;
            sidewaysClimbing = true;
            State = Activity.Climbing;
            localDetourUntil = dropUntil = 0;
            LastEvent = "climbing sideways to a checked exit";
            { planSucceeded = true; yield break; }
        }
        { planSucceeded = false; yield break; }

    }

    bool ClearSidewaysClimb(Vector3 start, Vector3 end, Vector3 inward)
    {
        int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / 0.2f);
        Vector3 previous = start;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, i / (float)Mathf.Max(1, steps));
            if (!ClearBody(point) || !ClearBodySweep(previous, point)
                || !Physics.Raycast(point + Vector3.up * eyeHeight, inward, out RaycastHit hold,
                    0.85f, environment, QueryTriggerInteraction.Ignore)
                || Vector3.Dot(hold.normal, -inward) < 0.7f) return false;
            previous = point;
        }
        return true;
    }

    void AdvanceSidewaysClimb(float dt)
    {
        movementRequested = true;
        if (fenceAttemptUntil > 0 && PhotonNetwork.Time >= fenceAttemptUntil)
        {
            StopSidewaysClimb();
            return;
        }
        traversalTime += dt;
        Vector3 next = Vector3.Lerp(traversalStart, sidewaysEnd, Mathf.Clamp01(traversalTime / traversalDuration));
        if (!ClearSidewaysClimb(transform.position, next, sidewaysWallDirection))
        {
            StopSidewaysClimb();
            return;
        }
        motor.Move(next - transform.position);
        if (Vector3.Distance(next, transform.position) > 0.15f)
        {
            StopSidewaysClimb();
            return;
        }
        if (traversalTime < traversalDuration) return;
        sidewaysClimbing = false;
        // Revalidate after moving: a clear exit at planning time is not a
        // promise that its collider/landing stayed clear.
        if (!BeginTraversal(sidewaysLanding, sidewaysExitSurface, sidewaysExitClimb))
            StopSidewaysClimb();
    }

    void StopSidewaysClimb()
    {
        sidewaysClimbing = false;
        traversalEnd = sidewaysLanding;
        AbandonFailedClimb();
    }

    void PoseSidewaysHand(Transform hand, float side, float phaseOffset)
    {
        // The weapon hand is posed by PoseKillerArm and replicated with the
        // normal player pose. Only the free hand may attach to the wall.
        if (hand == RightHand && KillerWeaponHeld) return;
        float traveled = Vector3.Distance(traversalStart, transform.position);
        float phase = Mathf.Repeat(traveled / 0.6f + phaseOffset, 1f);
        Vector3 tangent = (sidewaysEnd - traversalStart).normalized;
        Vector3 reach = transform.position + Vector3.up * (eyeHeight + 0.06f)
            + tangent * (side * 0.18f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.12f);
        if (Physics.Raycast(reach, sidewaysWallDirection, out RaycastHit hold,
            0.85f, environment, QueryTriggerInteraction.Ignore))
            hand.position = hold.point - sidewaysWallDirection * 0.04f;
        hand.rotation = Quaternion.LookRotation(sidewaysWallDirection, Vector3.up)
            * Quaternion.Euler(0, side < 0 ? -90 : 90, 0);
    }
}
