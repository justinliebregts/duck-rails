using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    public partial class Enemy
    {
        // All AI runs on the main thread. Share the workspace and admit one local search per
        // rendered frame, even if a whole group enters a flare during the same physics tick.
        const int SearchNodes = 384;
        static readonly Unity.Profiling.ProfilerMarker EscapeSearchMarker = new("EnemyPath.FlareEscape");
        static readonly int[] queue = new int[SearchNodes], parents = new int[SearchNodes];
        static readonly HashSet<int> visited = new(SearchNodes);
        static readonly List<Vector3> threats = new(16);
        static readonly int[] escapePath = new int[128];
        static int lastEscapeFrame = -1;
        bool followingEscape;
        internal bool FollowingFlareEscape => _flareRetreat && followingEscape;
        int progressWaypoint;
        float waypointDistance;
        float progressTime;
        int avoidEscapeNode = -1;

        static int SelectEscape(CrawlGraph graph, int start, bool ground, float radius, int avoid, HashSet<int> blocked)
        {
            if (!graph.IsNodeAllowed(start)) return 0;
            Vector3 origin = graph.GetPosition(start);
            visited.Clear();
            visited.Add(start);
            queue[0] = start;
            parents[0] = -1;
            float best = ThreatClearance(graph.GetPosition(start)) + .01f;
            int count = 1, winner = 0;
            for (int i = 0; i < count; i++)
            {
                int node = queue[i];
                Vector3 from = graph.GetPosition(node);
                float clearance = ThreatClearance(from);
                if (clearance > best)
                {
                    best = clearance;
                    winner = i;
                    if (best >= 12f * 12f) break;
                }
                for (int e = graph.EdgeStart[node]; count < SearchNodes && e < graph.EdgeStart[node+1]; e++)
                {
                    int next = graph.EdgeTarget[e];
                    if (next == avoid || (blocked != null && blocked.Contains(next)) || !graph.IsNodeAllowed(next) ||
                        visited.Contains(next) || ground && !graph.IsGroundNode(next)) continue;
                    Vector3 to = graph.GetPosition(next);
                    if ((to - origin).sqrMagnitude > 16f * 16f) continue;
                    bool clear = true;
                    foreach (Vector3 flare in threats)
                        if (!DeathFlare.AllowsStep(from, to, flare, radius * radius, true)) { clear = false; break; }
                    if (!clear) continue;
                    visited.Add(next);
                    queue[count] = next;
                    parents[count++] = i;
                }
            }
            int length = 0;
            for (int i = winner; i > 0; i = parents[i]) length++;
            // Keep the beginning if a long winding path exceeds the follower's buffer.
            for (int i = winner, index = length; i > 0; i = parents[i])
                if (--index < escapePath.Length) escapePath[index] = queue[i];
            return Mathf.Min(length, escapePath.Length);
        }

        static float ThreatClearance(Vector3 point)
        {
            float best = float.PositiveInfinity;
            foreach (Vector3 flare in threats) best = Mathf.Min(best, (point - flare).sqrMagnitude);
            return best;
        }

        void PlanEscape(CrawlGraph graph)
        {
            using var sample = EscapeSearchMarker.Auto();
            Vector3 pos = root.position;
            DeathFlare.CollectThreats(threats, pos, 18f + Mathf.Max(12f, flareFearRadius));
            int start = _ground ? graph.FindNearestGroundNode(pos, 1.5f) : graph.FindNearestNode(pos, 1.5f);
            // Keep the mover's recent failures across replans. Remembering only the last
            // waypoint lets adjacent blocked exits alternate forever around furniture.
            int count = SelectEscape(graph, start, _ground != null, flareFearRadius, avoidEscapeNode,
                _ground ? _ground.EscapeBlockedNodes : null);
            avoidEscapeNode = -1;
            _loco.Interrupt();
            followingEscape = count > 0;
            _flareDestination = followingEscape ? graph.GetPosition(escapePath[count-1]) : pos;
            if (_ground) _ground.FollowEscape(escapePath, count);
            else _climb.FollowEscape(escapePath, count);
            progressWaypoint = -1;
            waypointDistance = float.PositiveInfinity;
            progressTime = 0;
            _flareRepath = 1f;
        }

        bool EscapeStalled(CrawlGraph graph, float dt)
        {
            if (!followingEscape) return false;
            int node = _ground ? _ground.EscapeWaypoint : _climb.EscapeWaypoint;
            if (!graph.IsNodeAllowed(node)) return false;
            float distance = Vector3.Distance(root.position, graph.GetPosition(node));
            if (node != progressWaypoint || distance < waypointDistance - .08f)
            {
                progressWaypoint = node;
                waypointDistance = distance;
                progressTime = 0;
            }
            else progressTime += dt;
            return progressTime > 1.2f;
        }
    }
}
