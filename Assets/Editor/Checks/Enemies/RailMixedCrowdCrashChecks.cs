using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// Arm on the surviving peer in a private test expedition before terminating its master.
public static class RailMixedCrowdCrashChecks
{
    static readonly Dictionary<int, Vector3> positions = new();
    static readonly HashSet<string> moved = new();
    static int[] ids;
    static int originalMaster;
    static double handoff;
    static string room;

    [MenuItem("Duck Rails/Tests/Enemies/Observe mixed crowd master crash")]
    public static void Arm()
    {
        if (!EditorApplication.isPlaying || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2)
            throw new InvalidOperationException("Arm on the surviving non-master in a two-client test expedition.");
        var actors = Enemy.Active.Where(e => e && !e.IsDormant && e.GetComponent<PhotonView>()?.IsRoomView == true).ToArray();
        if (!RailEnemySpawns.Names.All(type => actors.Any(e => Kind(e) == type)))
            throw new InvalidOperationException("Load all five active Rails enemy types before arming.");
        positions.Clear();
        moved.Clear();
        foreach (var actor in actors) positions.Add(actor.GetComponent<PhotonView>().ViewID, actor.Root.position);
        ids = positions.Keys.ToArray();
        originalMaster = PhotonNetwork.MasterClient.ActorNumber;
        room = PhotonNetwork.CurrentRoom.Name;
        handoff = 0;
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        Debug.Log($"Watching {positions.Count} mixed enemies. Terminate the other test client and retain its crash timestamp.");
    }

    [MenuItem("Duck Rails/Tests/Enemies/Spawn 22 mixed crowd actors")]
    public static void SpawnStressCrowd()
    {
        if (!EditorApplication.isPlaying || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
            RailExpedition.Instance == null)
            throw new InvalidOperationException("Use the master of a private test expedition.");
        var points = RailExpedition.Instance.enemyPoints;
        if (points == null || points.Length < 5)
            throw new InvalidOperationException("Wait for authored encounter points to load.");
        RailEnemySpawns.Register();
        for (int i = 0; i < 22; i++)
        {
            int index = i % points.Length;
            var point = points[index];
            var prefab = RailEnemySpawns.Prefab(i % RailEnemySpawns.Names.Length);
            PhotonNetwork.InstantiateRoomObject(prefab.name, point.position, point.rotation, 0,
                new object[] { "rails", PhotonSession.RunId, "crowd-check-" + i });
        }
    }
    [MenuItem("Duck Rails/Tests/Enemies/Launch three Smashers at selected blocker")]
    public static void LaunchSharedLanding()
    {
        var lower = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<Enemy>() : null;
        if (!EditorApplication.isPlaying || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
            !lower || lower.IsDormant || !lower.HasAuthority || !RailEnemySpawns.Names.Contains(Kind(lower)))
            throw new InvalidOperationException("Select an active Rails enemy on the master of the test expedition.");
        Vector3 target = lower.Root.position;
        for (int i = 0; i < 3; i++)
        {
            Vector3 outward = Quaternion.Euler(0, i * 120f, 0) * Vector3.forward;
            Vector3 from = target + outward * 2f + Vector3.up * 3f;
            Quaternion rotation = Quaternion.LookRotation(-outward);
            var actor = PhotonNetwork.InstantiateRoomObject("RailSmasher", from, rotation);
            var enemy = actor.GetComponent<Enemy>();
            enemy.TickAuthority(EnemyManager.Instance, .001f, false);
            var jump = actor.GetComponent<ZombieSmasherJump>();
            jump.ResetJump();
            const float apex = .65f;
            float peak = Mathf.Sqrt(apex) / (Mathf.Sqrt(apex) + Mathf.Sqrt(apex + from.y - target.y));
            void Set(string field, object value) => typeof(ZombieSmasherJump)
                .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(jump, value);
            Set("_start", from); Set("_end", target); Set("_position", from); Set("_rotation", rotation);
            Set("_landingPoint", target); Set("_landingNormal", Vector3.up);
            Set("_apex", apex); Set("_peakTime", peak); Set("_duration", .9f); Set("_elapsed", 0f);
            Set("_plannedJump", true); Set("_falling", false); Set("_fallStartY", from.y); Set("_airTime", 0f);
            typeof(ZombieSmasherJump).GetProperty("IsJumping").SetValue(jump, true);
            actor.GetComponent<GroundLocomotion>().ClearPath();
        }
        Debug.Log("Launched three room-owned Smashers toward " + Kind(lower));
    }
    static string Kind(Enemy e) => e.gameObject.name.Replace("(Clone)", "").Trim();

    static void Sample()
    {
        if (!EditorApplication.isPlaying || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.Name != room)
        {
            EditorApplication.update -= Sample;
            Debug.LogError("Mixed crowd check interrupted before handoff completed.");
            return;
        }
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.MasterClient.ActorNumber == originalMaster) return;
        bool first = handoff == 0;
        if (first) handoff = EditorApplication.timeSinceStartup;
        foreach (int id in ids)
        {
            var enemy = PhotonView.Find(id)?.GetComponent<Enemy>();
            if (!enemy || enemy.IsDormant || !enemy.HasAuthority)
            {
                EditorApplication.update -= Sample;
                Debug.LogError($"Mixed crowd handoff lost active authoritative enemy {id}.");
                return;
            }
            if (first) positions[id] = enemy.Root.position;
            else if (Vector3.Distance(positions[id], enemy.Root.position) > .5f) moved.Add(Kind(enemy));
        }
        if (EditorApplication.timeSinceStartup - handoff < 10) return;
        EditorApplication.update -= Sample;
        string[] missing = RailEnemySpawns.Names.Where(type => !moved.Contains(type)).ToArray();
        if (missing.Length > 0) Debug.LogError("No post-crash movement observed for " + string.Join(", ", missing));
        else Debug.Log($"Mixed crowd handoff PASS. {positions.Count} view IDs retained and all five types moved.");
    }
}
