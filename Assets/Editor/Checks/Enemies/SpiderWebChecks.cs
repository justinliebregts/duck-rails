using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEngine;

public static class SpiderWebChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool success, string message) { if (!success) throw new Exception(message); }

    public static string StressSafety()
    {
        Check(!Application.isPlaying, "Edit mode required");
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab"));
        try
        {
            var web = actor.GetComponent<SpiderWeb>();
            var enemy = actor.GetComponent<Enemy>();
            Call(web, "Awake");
            var line = (LineRenderer)Get(web, "line");
            var attachment = (Transform)Get(web, "attachment");
            Vector3 home = new Vector3(12000, 1000, 12000);
            Vector3 localAttachment = attachment.localPosition;
            Action tick = (Action)Delegate.CreateDelegate(typeof(Action), web, typeof(SpiderWeb).GetMethod("LateUpdate", Private));
            int sequence = 100;
            Action start = () =>
            {
                actor.transform.position = home;
                attachment.localPosition = localAttachment;
                Call(web, "ApplySnapshot", ++sequence, true, home + Vector3.up * 3);
                tick();
                Check(web.WebActive && line.enabled, "Stress setup did not start");
            };
            Action hidden = () => Check(!web.WebActive && !line.enabled, "Unsafe line was still renderable");
            for (int cycle = 0; cycle < 1000; cycle++)
            {
                start();
                Vector3 before = actor.transform.position;
                Quaternion rotation = actor.transform.rotation;
                for (int pause = 0; pause < 3; pause++) tick();
                Check(actor.transform.position == before && actor.transform.rotation == rotation, "Visual altered root pose");
                Call(web, "ApplySnapshot", ++sequence, false, home);
                Call(web, "ApplySnapshot", sequence - 1, true, home + Vector3.up * 3);
                hidden();
                start();
                web.OnDisable();
                hidden();
                web.OnEnable();
                Call(web, "ApplySnapshot", sequence, true, home + Vector3.up * 3);
                hidden();
            }
            Check(actor.GetComponentsInChildren<LineRenderer>(true).Length == 1, "Reuse duplicated renderer");
            foreach (float jump in new[] { 2.01f, 100f, 10000f })
            {
                start();
                Vector3 renderedEnd = line.GetPosition(1);
                actor.transform.position += Vector3.right * jump;
                tick(); hidden();
                Check(line.GetPosition(1) == renderedEnd, "Unsafe endpoint was submitted before hide");
            }
            start();
            for (int step = 0; step < 100 && web.WebActive; step++)
            {
                actor.transform.position += Vector3.down * .2f;
                tick();
                if (line.enabled) Check(Vector3.Distance(line.GetPosition(0), line.GetPosition(1)) <= 10.001f, "Rendered line exceeded length cap");
            }
            hidden();
            start();
            attachment.localPosition += Vector3.right * 1000;
            tick(); hidden();
            foreach (string flag in new[] { "_remoteKnocked", "_stunned", "_ragdollActive" })
            {
                start(); Set(enemy, flag, true); tick(); hidden(); Set(enemy, flag, false);
            }
            start(); enemy.IsDormant = true; tick(); hidden(); enemy.IsDormant = false;
            start(); web.OnLeftRoom(); hidden();
            start(); web.OnControllerChange(null, null); hidden();
            actor.transform.position = home;
            foreach (Vector3 bad in new[] { home + Vector3.right * 10000, new Vector3(float.NaN, 0, 0), new Vector3(0, float.PositiveInfinity, 0) })
            {
                Call(web, "ApplySnapshot", ++sequence, true, bad);
                tick(); hidden();
            }
            Set(web, "_received", false);
            Call(web, "ApplySnapshot", int.MaxValue, true, home + Vector3.up * 3);
            tick(); Check(line.enabled, "Pre-wrap sequence");
            Call(web, "ApplySnapshot", int.MinValue, false, home);
            Call(web, "ApplySnapshot", int.MaxValue, true, home + Vector3.up * 3);
            hidden();
            return "PASS: 1000 reuse/late-message cycles; one renderer; unchanged root poses; 2.01/100/10000-unit teleports rejected before endpoint submission; gradual 10-unit cap; runaway attachment; knock/stun/ragdoll/dormancy; disable/re-enable; room exit/handoff; malformed anchors; sequence wraparound.";
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
    }

    public static string BenchmarkActiveUpdates()
    {
        Check(!Application.isPlaying, "Edit mode required");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab");
        var actors = new GameObject[20];
        var ticks = new Action[20];
        var report = new System.Text.StringBuilder("Editor CPU visual-update microbenchmark (excludes render submission/GPU): ");
        try
        {
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i] = UnityEngine.Object.Instantiate(prefab);
                actors[i].transform.position = new Vector3(12000 + i * 2, 1000, 12000);
                var web = actors[i].GetComponent<SpiderWeb>();
                Call(web, "Awake");
                Set(web, "_anchor", actors[i].transform.position + Vector3.up * 3);
                Set(web, "_previousRoot", actors[i].transform.position);
                Set(web, "_active", true);
                ticks[i] = (Action)Delegate.CreateDelegate(typeof(Action), web, typeof(SpiderWeb).GetMethod("LateUpdate", Private));
                ticks[i]();
                Check(web.WebActive, "Benchmark visual must remain active");
            }
            foreach (int count in new[] { 0, 5, 20 })
            {
                for (int frame = 0; frame < 100; frame++)
                    for (int i = 0; i < count; i++) ticks[i]();
                var watch = new System.Diagnostics.Stopwatch();
                long before = GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                for (int frame = 0; frame < 5000; frame++)
                    for (int i = 0; i < count; i++) ticks[i]();
                watch.Stop();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(bytes == 0, "Benchmark recurring allocations: " + bytes);
                report.Append(count).Append(" webs: ").Append((watch.Elapsed.TotalMilliseconds / 5000).ToString("F5"))
                    .Append(" ms/update batch, ").Append(bytes).Append(" B; ");
            }
            return report.ToString();
        }
        finally
        {
            foreach (var actor in actors) if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    // Runs in edit mode without changing the user's scene. Optional original mover
    // assembly provides a frame-by-frame comparison against the pre-web implementation.
    public static string Evaluate(Type baseline = null)
    {
        Check(!Application.isPlaying, "Edit mode required");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab");
        var actor = UnityEngine.Object.Instantiate(prefab);
        var ceiling = new GameObject("Web check ceiling");
        var floor = new GameObject("Web check floor");
        var managerObject = new GameObject("Web check manager");
        var baselineObject = new GameObject("Web check original mover");
        var singleton = typeof(EnemyManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        EnemyManager previous = EnemyManager.Instance;
        try
        {
            Vector3 origin = new Vector3(12000, 1000, 12000);
            actor.transform.position = origin + Vector3.up * 4f;
            var manager = managerObject.AddComponent<EnemyManager>();
            Set(manager, "surfaceMask", (LayerMask)(1 << 6));
            singleton.SetValue(null, manager);
            ceiling.layer = floor.layer = 6;
            ceiling.transform.position = origin + Vector3.up * 4.1f;
            ceiling.AddComponent<BoxCollider>().size = new Vector3(8, .2f, 8);
            floor.transform.position = origin + Vector3.down * .1f;
            floor.AddComponent<BoxCollider>().size = new Vector3(8, .2f, 8);
            Physics.SyncTransforms();
            var enemy = actor.GetComponent<Enemy>();
            var web = actor.GetComponent<SpiderWeb>();
            var climb = actor.GetComponent<ClimbLocomotion>();
            Call(web, "Awake");
            Call(climb, "Awake");
            var line = (LineRenderer)Get(web, "line");
            var attachment = (Transform)Get(web, "attachment");
            Check(line.positionCount == 2 && !line.enabled && line.numCapVertices == 0
                && !line.generateLightingData && line.sharedMaterial != null, "Lightweight prefab setup");
            Vector3 start = actor.transform.position;
            Vector3 next = start + Vector3.down * .05f;
            actor.transform.position = next;
            web.ObserveMove(true, false, start, next);
            Call(web, "LateUpdate");
            Check(web.WebActive && line.enabled, "Departure should show web");
            Check((line.GetPosition(0) - start).sqrMagnitude < .001f, "Ceiling anchor");
            Vector3 fixedAnchor = line.GetPosition(0);
            actor.transform.position += Vector3.down * .1f;
            Call(web, "LateUpdate");
            Check(line.GetPosition(0) == fixedAnchor && line.GetPosition(1) == attachment.position, "Fixed anchor / animated endpoint");
            Call(web, "LateUpdate");
            Check(web.WebActive, "Midair pause retains web");
            web.ObserveMove(false, true, next, origin);
            Check(!web.WebActive && !line.enabled, "Landing clears web");

            Action render = (Action)Delegate.CreateDelegate(typeof(Action), web, typeof(SpiderWeb).GetMethod("LateUpdate", Private));
            actor.transform.position = next;
            web.ObserveMove(true, false, start, next);
            render();
            for (int i = 0; i < 100; i++) render();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) render();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, "Active web allocated " + allocated + " bytes");
            actor.transform.position += Vector3.right * 3f;
            render();
            Check(!web.WebActive && !line.enabled, "Teleport clears line before rendering");
            web.ObserveMove(true, false, start, next);
            Check(!web.WebActive, "Aborted descent cannot restart in midair");
            web.ObserveMove(false, true, next, origin);
            actor.transform.position = next;
            web.ObserveMove(true, false, start, next);
            enemy.IsDormant = true;
            render();
            Check(!web.WebActive && !line.enabled, "Dormant/dead cleanup");
            enemy.IsDormant = false;
            Check(!(bool)Call(web, "ValidSegment", start, start + Vector3.down * 11f), "Length guard");
            Check(!(bool)Call(web, "ValidSegment", start, new Vector3(float.NaN, 0, 0)), "NaN guard");
            actor.transform.position = next;
            Call(web, "ApplySnapshot", 10, true, start);
            render();
            Check(web.WebActive && line.enabled, "Joining peer snapshot starts visual");
            Call(web, "ApplySnapshot", 11, false, start);
            Call(web, "ApplySnapshot", 10, true, start);
            Check(!web.WebActive && !line.enabled, "Delayed start cannot resurrect finished descent");
            Call(web, "ApplySnapshot", 12, true, start);
            web.Cancel();
            Call(web, "ApplySnapshot", 12, true, start);
            Check(!web.WebActive, "Duplicate snapshot cannot undo local safety cancellation");
            Call(web, "ApplySnapshot", 13, true, start);
            web.OnControllerChange(null, null);
            Check(!web.WebActive && !line.enabled, "Authority handoff clears stale anchor");

            // Compare the actual sweeps/attachment output with the original mover.
            Component original = baseline != null ? baselineObject.AddComponent(baseline) : null;
            Vector3 pos = start, oldPos = start;
            Set(climb, "_surfaceUp", Vector3.down);
            Set(climb, "collisionRadius", .21f);
            if (original != null) Set(original, "_surfaceUp", Vector3.down);
            actor.transform.localScale = Vector3.one;
            bool landed = false;
            bool sawWeb = false;
            actor.transform.position = start;
            web.ObserveMove(false, true, start, start);
            for (int i = 0; i < 120; i++)
            {
                Vector3 previousPosition = pos;
                pos = (Vector3)Call(climb, "MoveOnSurface", pos, origin, Vector3.up, .05f, (LayerMask)(1 << 6));
                if (original != null)
                {
                    oldPos = (Vector3)Call(original, "MoveOnSurface", oldPos, origin, Vector3.up, .05f, (LayerMask)(1 << 6));
                    Check((pos - oldPos).sqrMagnitude < 1e-10f && (climb.SurfaceNormal - (Vector3)Get(original, "_surfaceUp")).sqrMagnitude < 1e-10f,
                        "Movement changed at step " + i);
                }
                actor.transform.position = pos;
                web.ObserveMove((bool)Get(climb, "_webDeparture"), (bool)Get(climb, "_webContact"), previousPosition, pos);
                render();
                sawWeb |= line.enabled;
                landed |= (bool)Get(climb, "_webContact");
            }
            Check(landed && climb.SurfaceNormal.y > .9f && Mathf.Abs(pos.y - origin.y) < .05f, "Descent lands on floor");
            Check(sawWeb && !web.WebActive && !line.enabled, "Real descent reports departure and removes web on attachment");
            return "PASS: start, anchor, endpoint, pause, landing, teleport, restart suppression, dormancy, length/NaN guards, join snapshot, stale/duplicate rejection, authority handoff; 0 B/1000 active updates; "
                + (baseline != null ? "120 movement steps identical to original." : "descent landing verified.");
        }
        finally
        {
            singleton.SetValue(null, previous);
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(ceiling);
            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(baselineObject);
        }
    }
}
