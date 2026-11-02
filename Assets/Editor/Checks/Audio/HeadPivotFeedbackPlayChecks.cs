using System;
using System.Reflection;
using Game.Audio;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Exercises actual held-sound sampling and analytics lookup without a live room/SDK.
[InitializeOnLoad]
public static class HeadPivotFeedbackPlayChecks
{
    const string Key = "HeadPivotFeedbackPlayChecks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    delegate bool ReadHead(out Vector3 head);
    static int checks, firstFrame;
    static ReadHead readHead;
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string label) => Check(Vector3.Distance(a, b) < .0001f, label + ": " + a + " vs " + b);
    static HeadPivotFeedbackPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            firstFrame = Time.frameCount; Time.captureDeltaTime = 1f / 72;
            EditorApplication.update += WaitForFrame;
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void WaitForFrame()
    {
        if (Time.frameCount < firstFrame + 2) return;
        EditorApplication.update -= WaitForFrame;
        try { RunCases(); Finish(0); }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    sealed class HeldSound : IDisposable
    {
        public readonly GameObject go;
        public readonly ItemSounds sound;
        public readonly Grabbable grab;
        public readonly Action tick;
        readonly SoundEvent eventAsset;
        public HeldSound(Transform parent)
        {
            go = new GameObject("Held sound fixture"); go.SetActive(false);
            go.AddComponent<Rigidbody>().isKinematic = true;
            grab = go.AddComponent<Grabbable>(); grab.enabled = false;
            sound = go.AddComponent<ItemSounds>(); sound.enabled = false;
            var handGo = new GameObject("Hand"); handGo.transform.SetParent(parent, false);
            var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
            Set(grab, "holder", hand);
            eventAsset = ScriptableObject.CreateInstance<SoundEvent>(); eventAsset.name = "RegressionWhoosh";
            Set(sound, "swing", eventAsset);
            go.SetActive(true);
            tick = (Action)Delegate.CreateDelegate(typeof(Action), sound, typeof(ItemSounds).GetMethod("LateUpdate", Private));
        }
        public void Prime(Vector3 position)
        {
            go.transform.position = position; sound.ResetSwingState();
            Set(sound, "nextSwingTime", float.NegativeInfinity); tick();
            Check(float.IsNegativeInfinity(Get<float>(sound, "nextSwingTime")), "First sample played a whoosh");
        }
        public bool Played => !float.IsNegativeInfinity(Get<float>(sound, "nextSwingTime"));
        public void Dispose() { Object.DestroyImmediate(go); Object.DestroyImmediate(eventAsset); }
    }
    static void RunCases()
    {
        checks = 0;
        Check(!PhotonNetwork.InRoom, "Offline only");
        Check(Time.deltaTime > 0 && Time.deltaTime < .1f, "Invalid test frame duration");
        Physics.simulationMode = SimulationMode.Script; HelicopterManager.instance = null;
        PhotonPlayer.Active.Clear();
        readHead = (ReadHead)Delegate.CreateDelegate(typeof(ReadHead), typeof(DuckzAnalytics).GetMethod("TryLocalHead", BindingFlags.Static | BindingFlags.NonPublic));
        foreach (bool roomWalking in new[] { false, true })
        foreach (Vector3 offset in new[] { Vector3.zero, new Vector3(1, 0, -.5f), new Vector3(-4, 0, 3) })
        using (var rig = new MovementTestRig())
        {
            NewPlayer.instance = rig.p; rig.p.headCollider.transform.localPosition = offset + Vector3.up * 1.6f;
            var player = rig.root.AddComponent<GR.Player>(); player.enabled = false;
            GR.Player.instance = player;
            using (var held = new HeldSound(rig.root.transform))
            {
                Vector3 start = rig.p.headCollider.transform.position + Vector3.forward * .4f;
                held.Prime(start);
                Vector3 walk = Vector3.right * (3f * Time.deltaTime);
                if (roomWalking) rig.p.headCollider.transform.position += walk;
                else rig.root.transform.position += walk; // Legacy recentered locomotion origin.
                held.go.transform.position += walk; held.tick();
                Check(!held.Played, "Body translation counted as hand swing");
                held.go.transform.position += Vector3.forward * (3f * Time.deltaTime); held.tick();
                Check(held.Played, "Actual hand swing did not reach whoosh playback");
                held.tick(); Check(Get<bool>(held.sound, "swingArmed"), "Stationary hand did not rearm");
                float deadline = Get<float>(held.sound, "nextSwingTime");
                held.go.transform.position += Vector3.forward * (3f * Time.deltaTime); held.tick();
                Check(Get<float>(held.sound, "nextSwingTime") == deadline, "Cooldown changed");

                foreach (float angle in new[] { 1.5f, 45f, -90f })
                {
                    held.Prime(rig.p.headCollider.transform.position + Vector3.forward * .4f);
                    Vector3 pivot = rig.p.headCollider.transform.position;
                    Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                    Vector3 newItem = pivot + rotation * (held.go.transform.position - pivot);
                    // Feed a completed visible turn. A's Rigidbody rotation is published
                    // later in the real loop; manually sampling before it is not a turn.
                    // Real controller timing is covered by HeadPivotInteractionPlayChecks.
                    rig.root.transform.RotateAround(pivot, Vector3.up, angle);
                    rig.p.rb.position = rig.root.transform.position;
                    rig.p.rb.rotation = rig.root.transform.rotation;
                    held.go.transform.position = newItem; held.tick();
                    Check(!held.Played, "Turning alone played whoosh: B=" + roomWalking + " angle=" + angle + " offset=" + offset);
                }
                held.Prime(rig.p.headCollider.transform.position + Vector3.forward * .4f);
                Vector3 oldHead = rig.p.headCollider.transform.position;
                rig.p.Warp(new Vector3(50, 8, 70));
                held.go.transform.position += rig.p.headCollider.transform.position - oldHead; held.tick();
                Check(!held.Played, "Teleport played whoosh");
                held.Prime(rig.p.headCollider.transform.position + Vector3.forward * .4f);
                rig.p.ApplyPassengerDisplacement(walk); held.go.transform.position += walk; held.tick();
                Check(!held.Played, "Passenger carry played whoosh");
                held.Prime(rig.p.headCollider.transform.position + Vector3.forward * .4f);
                rig.p.headCollider.transform.position += Vector3.down * (3 * Time.deltaTime);
                held.go.transform.position += Vector3.down * (3 * Time.deltaTime); held.tick();
                Check(held.Played, "Vertical sound behavior changed"); // Preserve original root height.

                held.Prime(rig.p.headCollider.transform.position + Vector3.forward * .4f);
                var head = rig.p.headCollider; rig.p.headCollider = null; held.tick();
                Check(!held.Played, "Headset loss played whoosh");
                rig.p.headCollider = head; held.tick(); Check(!held.Played, "Headset recovery played whoosh");
                if (roomWalking) {
                    NewPlayer.instance = null; held.tick(); Check(!held.Played, "Missing movement source played whoosh");
                    NewPlayer.instance = rig.p; held.tick(); Check(!held.Played, "Restored movement source played whoosh");
                }
                Set(held.grab, "stashed", true); held.tick();
                Check(!Get<bool>(held.sound, "hasSwingSample"), "Stash did not reset samples");
                Set(held.grab, "stashed", false); held.tick(); Check(!held.Played, "Unstash played whoosh");
                Set(held.grab, "holder", null); held.tick();
                Check(!Get<bool>(held.sound, "hasSwingSample"), "Release did not reset samples");
                held.sound.PlayThrow(Vector3.right * 3);
                Check(held.Played, "Throw whoosh behavior changed");
            }
            Vector3 expected = rig.p.headCollider.transform.position;
            Check(readHead(out Vector3 result), "Analytics lost local player"); Near(result, expected, "Wrong pre-avatar position");
            var savedHead = rig.p.headCollider; rig.p.headCollider = null;
            Check(readHead(out result), "Missing headset lost root fallback"); Near(result, rig.root.transform.position, "Wrong missing-head fallback");
            rig.p.headCollider = savedHead;
            var avatarGo = new GameObject("Local network avatar"); avatarGo.SetActive(false);
            var view = avatarGo.AddComponent<PhotonView>(); var avatar = avatarGo.AddComponent<PhotonPlayer>();
            typeof(PhotonView).GetProperty("IsMine").SetValue(view, true);
            var networkHead = new GameObject("Network head").transform; networkHead.SetParent(avatarGo.transform);
            networkHead.position = new Vector3(300, 25, 400); avatar.BindSources(networkHead, networkHead, networkHead);
            avatarGo.SetActive(true);
            Check(readHead(out result), "Avatar lookup failed"); Near(result, networkHead.position, "Tracked fallback overrode avatar priority");
            avatarGo.SetActive(false);
            Check(readHead(out result), "Disconnected avatar lost fallback"); Near(result, expected, "Wrong disconnect fallback");
            avatarGo.SetActive(true); networkHead.position += Vector3.one;
            Check(readHead(out result), "Reconnected avatar lookup failed"); Near(result, networkHead.position, "Reconnection kept stale fallback");
            Object.DestroyImmediate(avatarGo);
            // Both hot paths are warmed and measured without reflection in the loop.
            using (var held = new HeldSound(rig.root.transform))
            {
                held.Prime(rig.p.headCollider.transform.position + Vector3.forward);
                for (int i = 0; i < 20; i++) { held.tick(); readHead(out _); }
                Vector3 before = rig.root.transform.position; Vector3 physics = rig.p.rb.position;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) { held.tick(); readHead(out _); }
                Check(GC.GetAllocatedBytesForCurrentThread() == bytes, "Feedback sampling allocated");
                Near(rig.root.transform.position, before, "Feedback moved root"); Near(rig.p.rb.position, physics, "Feedback moved Rigidbody");
            }
        }
        using (var foreign = new MovementTestRig())
        {
            NewPlayer.instance = foreign.p; var desktopRoot = new GameObject("Desktop feedback"); desktopRoot.SetActive(false);
            var desktop = desktopRoot.AddComponent<DesktopPlayer>();
            var player = desktopRoot.AddComponent<GR.Player>();
            desktopRoot.SetActive(true); desktop.enabled = player.enabled = false;
            desktopRoot.transform.position = new Vector3(40, 2, 50); GR.Player.instance = player;
            Check(readHead(out var result), "Desktop analytics failed"); Near(result, desktopRoot.transform.position, "Desktop used foreign headset");
            using (var held = new HeldSound(desktopRoot.transform))
            {
                held.Prime(desktopRoot.transform.position + Vector3.up);
                Vector3 step = Vector3.right * (3 * Time.deltaTime);
                desktopRoot.transform.position += step; held.go.transform.position += step; held.tick();
                Check(!held.Played, "Desktop walking played whoosh");
                held.go.transform.position += step; held.tick(); Check(held.Played, "Desktop swing suppressed");
            }
            Object.DestroyImmediate(desktopRoot); GR.Player.instance = null;
            var cameraGo = new GameObject("Fallback camera"); cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>(); cameraGo.transform.position = new Vector3(7, 3, 9);
            Check(readHead(out result), "Missing player lost camera fallback"); Near(result, cameraGo.transform.position, "Orphan B rig overrode camera");
            Object.DestroyImmediate(cameraGo);
            Check(!readHead(out _), "No sources should return false");
        }
        Debug.Log($"HeadPivotFeedbackPlayChecks PASS: {checks} assertions; actual whoosh sampling, walking/swings/turn/carry/warp, vertical parity, source transitions, throw/rearm/cooldown, analytics avatar/fallback/reconnection, desktop, zero warmed allocations and unchanged movement poses.");
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.Exit(code); }
}
