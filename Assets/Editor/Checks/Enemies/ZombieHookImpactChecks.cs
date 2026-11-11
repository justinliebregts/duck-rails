using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Game.AI;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Object = UnityEngine.Object;

// Launch in isolated batch Play Mode. For the live check start two copies with the
// same unique -hookFxRoom value. Each copy uses the production PUN RPC dispatcher.
public static class ZombieHookImpactChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "DuckZ.HookImpactChecks";
    const string Prefabs = "Assets/Prefabs/";
    static readonly Type Type = typeof(ZombieHookAttack);
    static readonly Type Phase = Type.GetNestedType("Phase", BindingFlags.NonPublic);
    static readonly Type Contact = Type.GetNestedType("Contact", BindingFlags.NonPublic);
    enum Shot { Mesh, ControllerPan, Duck, Miss, DefenderPan, DefenderBody }
    static Fixture fixture;
    static IEnumerator frames;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Fixture.CheckSparkPreview();
        SessionState.SetBool(Pending, true);
        Register();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= WhenPlaying;
        EditorApplication.update += WhenPlaying;
    }

    static void WhenPlaying()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= WhenPlaying;
        SessionState.SetBool(Pending, false);
        try
        {
            ZombieHookChecks.Run();
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-hookFxRoom");
            if (index >= 0) Connect(args[index + 1]);
            fixture = new Fixture();
            fixture.CheckWarmup();
            fixture.CheckContacts();
            if (index >= 0)
            {
                fixture.CheckRoom();
                frames = fixture.CheckFramePlayback();
                EditorApplication.update += NextFrame;
                return;
            }
            Finish(true);
        }
        catch (Exception e) { Debug.LogException(e); Finish(false); }
    }

    static void NextFrame()
    {
        try { if (!frames.MoveNext()) Finish(true); }
        catch (Exception e) { Debug.LogException(e); Finish(false); }
    }

    static void Finish(bool passed)
    {
        EditorApplication.update -= NextFrame;
        try { fixture?.Dispose(); }
        finally
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            if (passed) Debug.Log("Hook impact checks ALL PASSED");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    static object Call(object obj, string name, params object[] args) =>
        obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static GameObject Asset(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    static void Pump(Func<bool> ready, string stage, int seconds = 25)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            PhotonNetwork.NetworkingClient.Service();
            if (clock.Elapsed.TotalSeconds > seconds)
                throw new TimeoutException(stage + " (" + PhotonNetwork.NetworkClientState + ")");
            Thread.Sleep(4);
        }
    }

    static void Connect(string suffix)
    {
        Check(Guid.TryParseExact(suffix, "N", out _), "Supply a unique room GUID.");
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion = "hook-fx-" + suffix;
        settings.FixedRegion = PhotonSession.Region;
        Check(PhotonNetwork.ConnectUsingSettings(settings), "Connect");
        Pump(() => PhotonNetwork.IsConnectedAndReady, "connect");
        Check(PhotonNetwork.JoinOrCreateRoom("hook-fx-" + suffix, new RoomOptions { IsVisible = false,
            MaxPlayers = 2, EmptyRoomTtl = 0, PlayerTtl = 0 }, TypedLobby.Default), "Join private room");
        Pump(() => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount == 2, "wait for both editors", 180);
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.NetworkSimulationSettings.IncomingLag = peer.NetworkSimulationSettings.OutgoingLag = 70;
        peer.NetworkSimulationSettings.IncomingJitter = peer.NetworkSimulationSettings.OutgoingJitter = 15;
        peer.NetworkSimulationSettings.IncomingLossPercentage = peer.NetworkSimulationSettings.OutgoingLossPercentage = 3;
        peer.IsSimulationEnabled = true;
    }

    sealed class Fixture : IDisposable
    {
        readonly GameObject root = new GameObject("Hook impact fixture");
        readonly GameObject inactive = new GameObject("Inactive test actors");
        readonly ZombieHookAttack hook;
        readonly EnemyManager manager;
        readonly PhotonPlayer duck;
        readonly PhotonGrabbable pan;
        PhotonPlayer defender;
        readonly ParticleSystem sparks;
        readonly BloodEffectPool pool;
        readonly Vector3 start, end;
        readonly object solid = Enum.Parse(Contact, "Solid"), flesh = Enum.Parse(Contact, "Flesh");
        readonly object silent = Enum.Parse(Contact, "Silent");
        readonly GameObject wall;
        readonly int created;
        readonly PhotonMasterHandoff handoff;

        public Fixture()
        {
            root.AddComponent<AudioListener>();
            inactive.transform.SetParent(root.transform);
            inactive.SetActive(false);
            // Only the target registry and masks are needed for a sweep in this empty scene.
            manager = inactive.AddComponent<EnemyManager>();
            typeof(EnemyManager).GetProperty("Instance").SetValue(null, manager);
            var prefab = Asset(Prefabs + "Enemies/ZombieHook.prefab");
            Call(prefab.GetComponent<ZombieHookAttack>(), "PrewarmImpacts");
            if (PhotonNetwork.InRoom)
            {
                handoff = root.AddComponent<PhotonMasterHandoff>();
                Call(handoff, "OnApplicationFocus", true);
                PhotonPreparePool.Register(prefab);
                Barrier("hookPrefabReady");
                if (PhotonNetwork.IsMasterClient)
                {
                    var go = PhotonNetwork.InstantiateRoomObject(prefab.name, Vector3.zero, Quaternion.identity);
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "hookView", go.GetComponent<PhotonView>().ViewID } });
                }
                Pump(() => PhotonNetwork.MasterClient.CustomProperties["hookView"] is int id && PhotonView.Find(id) != null, "network-spawned hook");
                hook = PhotonView.Find((int)PhotonNetwork.MasterClient.CustomProperties["hookView"]).GetComponent<ZombieHookAttack>();
                Check(hook.photonView.IsMine == PhotonNetwork.IsMasterClient, "Room spawn assigns the hook controller");
            }
            else hook = Object.Instantiate(prefab, root.transform).GetComponent<ZombieHookAttack>();
            foreach (var c in hook.GetComponentsInChildren<Collider>()) c.enabled = false;
            start = Field<Transform>(hook, "wrist").position;
            end = start + Vector3.forward * 4;
            sparks = (ParticleSystem)Type.GetField("sharedSparks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            pool = BloodEffectPool.Get();
            created = pool.CreatedObjects;
            duck = Object.Instantiate(Asset("Assets/Resources/DuckCompanion.prefab"), inactive.transform).GetComponent<PhotonPlayer>();
            Call(duck.Companion, "Awake"); // Duck builds his body capsule during initialization.
            var capsule = duck.GetComponent<CharacterController>();
            duck.transform.position += start + Vector3.forward * 2 - duck.transform.TransformPoint(capsule.center);
            pan = Object.Instantiate(Asset(Prefabs + "Weapons/WeaponFryPan.prefab"), inactive.transform).GetComponent<PhotonGrabbable>();
            typeof(Grabbable).GetMethod("RebuildLocomotionSamples", Private).Invoke(pan, null);
            var samples = new Vector3[Grabbable.MaxLocomotionSamples];
            Check(pan.CopySampleWorldPositions(samples) > 0, "Frying pan has baked block samples");
            pan.transform.position += start + Vector3.forward - samples[0];
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
            wall.AddComponent<MeshCollider>().sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh;
            wall.transform.position = start + Vector3.forward * 3;
            wall.SetActive(false);
        }

        public static void CheckSparkPreview()
        {
            var preview = Object.Instantiate(Asset(Prefabs + "Effects/HookImpactSparks.prefab")).GetComponent<ParticleSystem>();
            try
            {
                var emission = preview.emission;
                Check(!preview.main.loop && emission.enabled && emission.burstCount == 1 &&
                    emission.rateOverTime.constant == 0 && emission.rateOverDistance.constant == 0, "Preview has one burst and no continuous emission");
                for (int restart = 0; restart < 2; restart++)
                {
                    preview.Simulate(.04f, false, true, false);
                    Check(preview.particleCount == 5, "Scene preview restart emits five sparks");
                    if (restart == 0) CheckRendered(preview.GetComponent<ParticleSystemRenderer>(), preview.transform.position);
                    preview.Simulate(.30f, false, false, false);
                    Check(preview.particleCount == 0, "Preview burst expires");
                }
                Debug.Log("Hook spark Scene preview PASS: five sparks on play and restart");
            }
            finally { Object.DestroyImmediate(preview.gameObject); }
        }

        public void CheckWarmup()
        {
            Check(sparks != null && sparks.particleCount == 0 && !sparks.isPlaying, "Invisible completed spark warmup");
            Check(!sparks.emission.enabled, "Pooled spark emitter uses manual emission");
            var main = sparks.main;
            Check(main.maxParticles == 32 && main.simulationSpace == ParticleSystemSimulationSpace.World &&
                Mathf.Approximately(main.duration, .3f) && Mathf.Approximately(main.startLifetime.constant, .3f),
                "Spark capacity, duration, lifetime and space");
            Check(main.startSize.mode == ParticleSystemCurveMode.TwoConstants &&
                Mathf.Approximately(main.startSize.constantMin, .032f) && Mathf.Approximately(main.startSize.constantMax, .058f),
                "Spark start size ranges from 3.2 to 5.8 cm");
            var material = sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            var texture = material.mainTexture;
            Check(texture != null && texture.width == 8 && texture.height == 8 && texture.filterMode == FilterMode.Point, "Pixel spark material");
            // Rails has no gameplay GPU warmup catalog; the explicit emit-and-clear prewarm above is the only warmup.
            var blood = Asset(Prefabs + "Effects/BloodSplatPFX.prefab");
            Check(Field<Game.Audio.SoundEvent>(hook, "fleshSound").ImpactParticles == blood && created >= BloodEffectPool.HitCapacity,
                "Existing melee blood asset and prewarmed slots");
            Call(hook, "PrewarmImpacts");
            for (int i = 0; i < 9; i++)
                Apply(solid, start + Vector3.right * i);
            var particles = new ParticleSystem.Particle[32];
            Check(sparks.GetParticles(particles) == 32, "Overlapping bursts cap at 32");
            Check(Vector3.Distance(particles[0].position, start) < .05f &&
                particles.Any(p => Vector3.Distance(p.position, start + Vector3.right * 6) < .05f),
                "Moving the shared emitter preserves earlier world-space sparks");
            sparks.Simulate(.28f, false, false, false);
            Check(sparks.particleCount == 32, "Sparks remain alive just before 0.30 s");
            sparks.Simulate(.04f, false, false, false);
            Check(sparks.particleCount == 0, "Sparks expire just after 0.30 s");
            GetType().GetMethod(nameof(CheckAllocations), Private).MakeGenericMethod(Contact).Invoke(this, null);
            Call(hook, "Stow");
        }

        void CheckAllocations<T>()
        {
            var play = (Action<T, Vector3>)Delegate.CreateDelegate(typeof(Action<T, Vector3>), hook, Type.GetMethod("PlayImpact", Private));
            T block = (T)solid, body = (T)flesh;
            // Warm the active/recycle branches and JIT before sampling only the VFX code.
            for (int i = 0; i <= BloodEffectPool.HitCapacity; i++) play(body, start);
            play(block, start);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                sparks.Clear(false);
                play(block, start);
                play(body, start);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && Hits == BloodEffectPool.HitCapacity && pool.CreatedObjects == created,
                "Warmed impact playback / blood pool overflow, managed bytes=" + allocated);
            foreach (var ps in pool.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            pool.Tick(Time.time);
            Check(Hits == 0, "Completed blood slots return to pool");
        }

        void Apply(object contact, Vector3 point)
        {
            Call(hook, "Apply", Enum.Parse(Phase, "Reel"), PhotonNetwork.ServerTimestamp, point, Vector3.zero, contact, 0, Vector3.zero, false);
        }

        object[] Trace(Vector3 from, Vector3 to)
        {
            object[] args = { from, to, null, null, null, false };
            Call(hook, "Trace", args);
            return args;
        }

        public void CheckContacts()
        {
            wall.SetActive(true);
            Physics.SyncTransforms();
            Check(Trace(start, end)[2].Equals(solid), "Solid mesh stops the hook");
            Check(Trace(end, end)[2].Equals(solid), "Moving cover crosses paid-out chain");
            var corpse = wall.AddComponent<Ragdoll>();
            var tetherHit = Trace(end, end);
            Check(tetherHit[2].Equals(flesh) && tetherHit[4] == null, "Corpse crossing tether gets blood without a pull");
            Object.DestroyImmediate(corpse);
            wall.SetActive(false);
            PhotonPlayer.Active.Add(duck);
            var hit = Trace(start, end);
            Check(hit[2].Equals(flesh) && Equals(hit[4], duck), "Duck intercepts the tip as flesh");
            hook.GetComponent<Enemy>().SetRemoteState(Enemy.State.Chase);
            typeof(Enemy).GetField("_seen", Private).SetValue(hook.GetComponent<Enemy>(), true);
            Field<Object[]>(manager, "_slotOwner")[0] = duck;
            Field<Vector3[]>(manager, "_slotHead")[0] = end;
            Check(!(bool)Call(hook, "TickChase", manager, 0, .02f) && Field<object>(hook,"phase").ToString() == "Idle",
                "A chased Duck does not trigger hook windup");
            Field<Object[]>(manager, "_slotOwner")[0] = null;
            PhotonGrabbable.HeldItems.Add(pan);
            Check(Trace(start, end)[2].Equals(solid), "Held pan wins ahead of Duck's body");
            PhotonGrabbable.HeldItems.Remove(pan);
            PhotonPlayer.Active.Remove(duck);
            Check(Trace(start, end)[2].Equals(silent), "Empty throw stays silent");
            int before = Hits;
            Apply(flesh, (Vector3)hit[3]);
            Check(Hits == before + 1 && pool.CreatedObjects == created, "Blood borrows one warmed instance");
            Debug.Log("Hook impact PASS: warmup, overlapping bursts, expiry, mesh, tether cover, Duck, held pan, miss and blood reuse");
        }

        int Hits => pool.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.name.StartsWith("BloodSplatPFX"));

        void Barrier(string name)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { name, true } });
            Pump(() => PhotonNetwork.PlayerList.Length == 2 && PhotonNetwork.PlayerList.All(p => p.CustomProperties.ContainsKey(name)), name);
        }

        public void CheckRoom()
        {
            bool authority = PhotonNetwork.IsMasterClient;
            if (!authority) Call(handoff, "OnApplicationFocus", false);
            foreach (Shot shot in Enum.GetValues(typeof(Shot)))
            {
                // Both copies agree on the fixture. The controller resolves the shot.
                bool remoteBlock = shot == Shot.DefenderPan;
                bool remoteDefense = remoteBlock || shot == Shot.DefenderBody;
                if (remoteBlock && !authority) Call(handoff, "OnApplicationFocus", true);
                object expected = shot == Shot.Miss ? silent : shot == Shot.Duck || shot == Shot.DefenderBody ? flesh : solid;
                wall.SetActive(shot == Shot.Mesh);
                if (remoteBlock) AddRemoteDefender(authority);
                if (shot == Shot.DefenderBody) defender.transform.position = start + Vector3.forward * 2;
                if ((shot == Shot.ControllerPan && authority) || (remoteBlock && !authority))
                {
                    typeof(Grabbable).GetField("holder", Private).SetValue(pan, inactive.AddComponent<PlayerHand>());
                    PhotonGrabbable.HeldItems.Add(pan);
                }
                if (shot == Shot.Duck) PhotonPlayer.Active.Add(duck);
                Physics.SyncTransforms();
                sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                int before = Hits;
                Barrier("ready" + shot);
                if (shot == Shot.Mesh)
                {
                    if (authority)
                    {
                        Call(hook, "Change", Enum.Parse(Phase, "Aim"), start, end, silent, 0, Vector3.zero);
                        Call(hook, "Change", Enum.Parse(Phase, "Flight"), start, end, silent, 0, Vector3.zero);
                    }
                    else
                    {
                        Pump(() => Field<object>(hook, "phase").ToString() == "Flight", "unfocused observer accepts live flight", 4);
                        Call(hook, "LateUpdate");
                        var chain = Field<MeshRenderer>(hook, "chainRenderer");
                        Check(chain.gameObject.activeInHierarchy && !PhotonMasterHandoff.LocalAvailable, "Unfocused observer draws the extended chain");
                        CheckRendered(chain, chain.bounds.center);
                        Call(handoff, "OnApplicationFocus", true);
                        Call(handoff, "OnApplicationFocus", false);
                        Check(Field<MeshFilter>(hook, "chain").gameObject.activeInHierarchy, "Changing desktop focus mid-flight preserves the chain");
                    }
                    Barrier("observedFlight");
                }
                if (authority)
                {
                    if (shot != Shot.Mesh) Call(hook, "Change", Enum.Parse(Phase, "Flight"), start, end, silent, 0, Vector3.zero);
                    if (remoteDefense) Call(hook, "AskNearbyDefenders", defender);
                    Call(hook, "AdvanceFlight", end, true);
                    if (remoteDefense)
                        Pump(() =>
                        {
                            if (Field<object>(hook, "phase").ToString() == "Flight") Call(hook, "AdvanceFlight", end, true);
                            return Field<object>(hook, "phase").ToString() == "Reel";
                        }, "resolve remote defense");
                    // Deliver the same revision again to exercise actual duplicate RPC rejection.
                    Call(hook, "SendState", null, expected, 0, Vector3.zero);
                }
                else if (remoteDefense)
                {
                    Pump(() => Field<bool>(hook, "defenseRequested"), "ask remote defender");
                    Call(hook, "ObserveDefense", end, true);
                }
                Pump(() => Field<object>(hook, "phase").ToString() == "Reel", "receive reel " + shot);
                Barrier("applied" + shot);
                Check(sparks.particleCount == (expected.Equals(solid) ? 5 : 0) && Hits == before + (expected.Equals(flesh) ? 1 : 0),
                    "Exactly one correct effect on " + (authority ? "authority " : "observer ") + shot);
                Vector3 point = Field<Vector3>(hook, "origin");
                if (shot == Shot.Duck)
                {
                    // Check the live reel before GPU readback and the position round trip can age it out.
                    if (!authority)
                    {
                        Call(hook, "Stow");
                        Type.GetField("revision", Private).SetValue(hook, 0);
                    }
                    Barrier("joining");
                    if (authority) hook.OnPlayerEnteredRoom(PhotonNetwork.PlayerListOthers[0]);
                    Pump(() => Field<object>(hook, "phase").ToString() == "Reel", "join snapshot");
                    Check(Hits == before + 1 && sparks.particleCount == 0, "Join snapshot never replays blood");
                }
                if (expected.Equals(solid))
                {
                    Check(Vector3.Distance(sparks.transform.position, point) < .001f, "Spark at replicated impact");
                    sparks.Simulate(.12f, false, false, false);
                    Check(sparks.particleCount == 5, "One five-spark burst after simulation and duplicate hook result");
                    if (shot == Shot.Mesh) CheckRendered(sparks);
                }
                if (expected.Equals(flesh))
                {
                    var burst = pool.transform.Cast<Transform>().Last(t => t.name.StartsWith("BloodSplatPFX") && t.gameObject.activeSelf);
                    Check(Vector3.Distance(burst.position, point) < .001f, "Blood at replicated impact");
                    var particles = burst.GetComponent<ParticleSystem>();
                    Check(particles.isPlaying, "Blood emitter is playing");
                    particles.Simulate(.03f, false, false, false);
                    Check(particles.particleCount > 0, "Received blood burst emits particles");
                    if (shot == Shot.Duck) CheckRendered(particles);
                }
                string pointKey = "point" + shot;
                if (authority) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { pointKey, point } });
                Pump(() => PhotonNetwork.MasterClient.CustomProperties.ContainsKey(pointKey), "read controller position");
                Check(Vector3.Distance(point, (Vector3)PhotonNetwork.MasterClient.CustomProperties[pointKey]) < .001f, "Peers agree on position");
                if (shot == Shot.ControllerPan || remoteBlock)
                {
                    PhotonGrabbable.HeldItems.Remove(pan);
                    typeof(Grabbable).GetField("holder", Private).SetValue(pan, null);
                }
                if (shot == Shot.Duck) PhotonPlayer.Active.Remove(duck);
                Debug.Log("Hook impact Photon PASS " + (authority ? "authority " : "observer ") + shot + " point=" + point.ToString("F4"));
                Call(hook, "Stow");
            }
            Check(pool.CreatedObjects == created, "Room playback uses only warmed instances");
            CheckPausedObserver(authority);
            Barrier("complete");
        }

        void CheckPausedObserver(bool authority)
        {
            sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            int before = Hits;
            if (!authority)
            {
                int release = PhotonNetwork.ServerTimestamp;
                Call(handoff, "OnApplicationFocus", false);
                Check(!(bool)Call(defender, "CanReceiveHookReleasedAt", release), "Unfocused defender cannot be pulled");
                Call(handoff, "OnApplicationFocus", true);
                Check(!(bool)Call(defender, "CanReceiveHookReleasedAt", release), "Focus recovery cannot revive a pre-focus pull");
                Call(handoff, "OnApplicationPause", true);
            }
            Barrier("observerPaused");
            if (authority)
            {
                Call(hook, "Change", Enum.Parse(Phase, "Flight"), start, end, silent, 0, Vector3.zero);
                Call(hook, "Change", Enum.Parse(Phase, "Reel"), end, Vector3.zero, solid, 0, Vector3.zero);
            }
            Barrier("pausedResultDelivered");
            if (!authority)
            {
                Check(Field<object>(hook, "phase").ToString() == "Idle" && sparks.particleCount == 0 && Hits == before,
                    "Paused observer suppresses chain and effects");
                Call(handoff, "OnApplicationPause", false);
                Check(Field<object>(hook, "phase").ToString() == "Idle" && sparks.particleCount == 0,
                    "Resume does not replay a missed impact");
            }
        }

        // Let PUN dispatch and Unity run the two rigs' LateUpdates between observations.
        // Begin with a clear firing lane, then move the blocker into the released throw.
        public IEnumerator CheckFramePlayback()
        {
            bool authority = PhotonNetwork.IsMasterClient;
            PhotonPlayer.Active.Remove(defender);
            Call(handoff, "OnApplicationFocus", authority);
            var chain = Field<MeshFilter>(hook, "chain");
            var brain = hook.GetComponent<Enemy>();
            foreach (var shot in new[] { Shot.Mesh, Shot.Duck })
            {
                Call(hook, "Stow");
                sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var ps in pool.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                pool.Tick(Time.time);
                wall.SetActive(false);
                Vector3 aim = start + Vector3.forward * 8;
                Field<Object[]>(manager, "_slotOwner")[0] = root.transform;
                Field<Vector3[]>(manager, "_slotHead")[0] = aim;
                brain.SetRemoteState(Enemy.State.Chase);
                typeof(Enemy).GetField("_seen", Private).SetValue(brain, true);
                Type.GetField("readyAt", Private).SetValue(hook, 0f);
                Type.GetField("probeAt", Private).SetValue(hook, 0f);
                Barrier("frameReady" + shot);
                bool released = false, sawChain = false, sawImpact = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (!sawChain || !sawImpact)
                {
                    if (authority && !released)
                    {
                        Call(hook, "TickChase", manager, 0, .016f);
                        if (Field<object>(hook, "phase").ToString() == "Flight")
                        {
                            released = true;
                            Vector3 intercept = Vector3.Lerp(Field<Vector3>(hook, "origin"), aim, .8f);
                            if (shot == Shot.Mesh) { wall.transform.position = intercept; wall.SetActive(true); }
                            else
                            {
                                duck.transform.position += intercept - duck.transform.TransformPoint(duck.GetComponent<CharacterController>().center);
                                PhotonPlayer.Active.Add(duck);
                            }
                            Physics.SyncTransforms();
                        }
                    }
                    sawChain |= chain.gameObject.activeInHierarchy;
                    sawImpact |= shot == Shot.Mesh ? sparks.particleCount > 0 : Hits > 0;
                    Check(Time.realtimeSinceStartupAsDouble < deadline, $"Frame playback {shot}: authority={authority}, phase={Field<object>(hook, "phase")}, chain={sawChain}, impact={sawImpact}");
                    yield return null;
                }
                Debug.Log($"Hook frame playback PASS {shot}, authority={authority}, chain and impact observed");
                Barrier("frameDone" + shot);
                PhotonPlayer.Active.Remove(duck);
            }
        }

        // Graphics-capable batch runs also check the pixels drawn by the actual received
        // emitter. The hidden-renderer frame gives a baseline with identical scene lighting.
        static void CheckRendered(ParticleSystem particles)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            particles.Simulate(.02f, false, false, false);
            CheckRendered(particles.GetComponent<ParticleSystemRenderer>(), particles.transform.position);
        }

        static void CheckRendered(Renderer renderer, Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Check(renderer.enabled && renderer.sharedMaterial.shader.isSupported, "Active, supported impact renderer");
            var camera = new GameObject("Impact readback camera").AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(192, 192, 24);
            var pixels = new Texture2D(192, 192, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            int layer = renderer.gameObject.layer;
            try
            {
                renderer.gameObject.layer = 30;
                camera.cullingMask = 1 << 30;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.orthographic = true; camera.orthographicSize = .45f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                camera.transform.position = center + new Vector3(.9f, .4f, -.6f);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                Color32[] Read()
                {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 192, 192), 0, 0);
                    return pixels.GetPixels32();
                }
                renderer.enabled = false;
                var background = Read();
                renderer.enabled = true;
                var frame = Read();
                int changed = 0;
                for (int i = 0; i < frame.Length; i++)
                    if (Math.Abs(frame[i].r - background[i].r) + Math.Abs(frame[i].g - background[i].g) +
                        Math.Abs(frame[i].b - background[i].b) > 8) changed++;
                Check(changed > 3, "No visible pixels from " + renderer.name);
                Debug.Log("Hook impact render PASS " + renderer.name + ": " + changed + " changed pixels");
            }
            finally
            {
                renderer.enabled = true; renderer.gameObject.layer = layer;
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(camera.gameObject);
            }
        }

        void AddRemoteDefender(bool authority)
        {
            int actor = authority ? PhotonNetwork.PlayerListOthers[0].ActorNumber : PhotonNetwork.LocalPlayer.ActorNumber;
            var avatar = new GameObject("Defender");
            avatar.transform.SetParent(inactive.transform);
            avatar.transform.position = start + Vector3.right + Vector3.forward;
            var view = avatar.AddComponent<PhotonView>();
            view.ViewID = actor * PhotonNetwork.MAX_VIEW_IDS + 1;
            view.OwnerActorNr = view.ControllerActorNr = actor;
            defender = avatar.AddComponent<PhotonPlayer>();
            var body = avatar.AddComponent<RemotePlayerBody>();
            typeof(RemotePlayerBody).GetField("head", Private).SetValue(body, avatar.transform);
            typeof(PhotonPlayer).GetField("body", Private).SetValue(defender, body);
            typeof(PhotonPlayer).GetField("headSource", Private).SetValue(defender, avatar.transform);
            PhotonPlayer.Active.Add(defender);
            if (!authority) return;
            // A replicated grip proves ownership on the controller. Only the
            // item owner's actual ReportHookDefense RPC can decide this impact.
            typeof(PhotonGrabbable).GetField("remoteHeld", Private).SetValue(pan, true);
            typeof(PhotonGrabbable).GetField("remoteHolderActorNumber", Private).SetValue(pan, actor);
            PhotonGrabbable.HeldItems.Add(pan);
        }

        public void Dispose()
        {
            PhotonPlayer.Active.Remove(duck);
            if (defender != null) PhotonPlayer.Active.Remove(defender);
            PhotonGrabbable.HeldItems.Remove(pan);
            typeof(EnemyManager).GetProperty("Instance").SetValue(null, null);
            Object.DestroyImmediate(root);
        }
    }
}
