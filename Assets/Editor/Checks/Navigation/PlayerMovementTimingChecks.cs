using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Run with Unity -batchmode -executeMethod PlayerMovementTimingChecks.Run -quit.
public static class PlayerMovementTimingChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
        GR.Player previousPlayer = GR.Player.instance;
        NewPlayer previousVR = NewPlayer.instance;
        float previousFixedStep = Time.fixedDeltaTime;
        SimulationMode previousMode = Physics.simulationMode;
        GameObject rig = null;
        try
        {
            GR.Player.instance = null;
            Time.fixedDeltaTime = 1f / 72f;
            Physics.simulationMode = SimulationMode.Script;
            rig = new GameObject("Player frame timing regression");
            var player = rig.AddComponent<NewPlayer>();
            player.rb = rig.AddComponent<Rigidbody>();
            player.rb.useGravity = false;
            player.headCollider = rig.AddComponent<SphereCollider>();
            player.bodyMidpoint = rig.transform;
            player.leftHandTransform = rig.transform;
            player.rightHandTransform = rig.transform;
            player.velocityHistorySize = 8;
            player.velocityLimit = 0.4f;
            player.jumpMultiplier = 1.1f;
            player.maxJumpSpeed = 6.5f;
            var store = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), player,
                typeof(NewPlayer).GetMethod("StoreVelocities", Private));
            FieldInfo current = typeof(NewPlayer).GetField("currentVelocity", Private);
            FieldInfo average = typeof(NewPlayer).GetField("denormalizedVelocityAverage", Private);
            Vector3 speed = new Vector3(1f, 0.5f, -0.25f);
            float[][] intervals = {
                new[] { 1f / 72f },
                new[] { 1f / 50f },
                new[] { 1f / 90f },
                new[] { 0.008f, 0.012f, 0.018f, 0.026f }
            };

            foreach (float[] frames in intervals)
            {
                rig.transform.position = Vector3.zero;
                player.InitializeValues();
                for (int i = 0; i < 64; i++)
                {
                    float dt = frames[i % frames.Length];
                    rig.transform.position += speed * dt;
                    store(dt);
                    Near((Vector3)current.GetValue(player), speed, "Measured velocity depends on frame interval");
                    if (i < player.velocityHistorySize - 1) continue;
                    Near((Vector3)average.GetValue(player), speed, "Launch history depends on frame interval");
                    player.SetVelToAverageVel();
                    Near(player.rb.linearVelocity, speed * player.jumpMultiplier, "Jump strength depends on frame interval");
                }
            }

            CheckPhysicsCadence(player, store, average);
            CheckHistoryTransitions(player, store, average);
            CheckWarpVelocityReset(player, average);

            // Warm before measuring; reflection and assertions stay outside the measured loop.
            for (int i = 0; i < 32; i++) store(1f / 72f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                rig.transform.position += speed * (1f / 72f);
                store(1f / 72f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0) throw new Exception("Velocity sampling allocated " + allocated + " bytes");
            Debug.Log("PlayerMovementTimingChecks: PASS - continuous and physics-stepped motion, history warm-up/turn/launch/reset, immediate warp launches, zero/tiny timesteps, jump caps, zero allocations in 1000 samples");
        }
        finally
        {
            if (rig != null) Object.DestroyImmediate(rig);
            GR.Player.instance = previousPlayer;
            NewPlayer.instance = previousVR;
            Time.fixedDeltaTime = previousFixedStep;
            Physics.simulationMode = previousMode;
        }
    }

    static void CheckPhysicsCadence(NewPlayer player, Action<float> store, FieldInfo average)
    {
        const float step = 1f / 72f;
        player.headCollider.enabled = false;
        foreach (float shortFrame in new[] { step, .012f, .010f, .008f })
        {
            player.transform.position = Vector3.zero;
            player.rb.position = Vector3.zero;
            player.InitializeValues();
            player.rb.linearVelocity = Vector3.right;
            Physics.SyncTransforms();
            double accumulator = 0;
            for (int i = 0; i < 400; i++)
            {
                float dt = i % 2 == 0 ? shortFrame : 2f * step - shortFrame;
                accumulator += dt;
                while (accumulator + 1e-8 >= step)
                {
                    Physics.Simulate(step);
                    accumulator -= step;
                }
                store(dt);
                if (i >= player.velocityHistorySize - 1)
                    Near((Vector3)average.GetValue(player), Vector3.right, "Uneven rendering biases steady physics velocity");
            }
            player.SetVelToAverageVel();
            Near(player.rb.linearVelocity, Vector3.right * player.jumpMultiplier, "Uneven rendering changes physics-derived launch");
        }
        player.rb.linearVelocity = Vector3.zero;
        player.headCollider.enabled = true;
    }

    static void CheckHistoryTransitions(NewPlayer player, Action<float> store, FieldInfo average)
    {
        const float step = 1f / 72f;
        player.transform.position = Vector3.zero;
        player.InitializeValues();
        Vector3 speed = Vector3.right;
        // The old history starts with eight stationary samples; preserve that ramp at 72 Hz.
        for (int i = 1; i <= player.velocityHistorySize; i++)
        {
            player.transform.position += speed * step;
            store(step);
            Near((Vector3)average.GetValue(player), speed * (i / (float)player.velocityHistorySize), "Startup launch ramp changed");
        }

        // Different durations and directions: net displacement is zero even though mean speed is not.
        for (int i = 0; i < player.velocityHistorySize; i++)
        {
            player.transform.position += Vector3.right * (i % 2 == 0 ? .01f : -.01f);
            store(i % 2 == 0 ? .008f : 2f * step - .008f);
        }
        Near((Vector3)average.GetValue(player), Vector3.zero, "Back-and-forth motion creates launch momentum");

        Vector3 launch = new Vector3(2f, 1f, .5f);
        player.LaunchPlayer(launch, LaunchType.OverwriteVelocity);
        Near((Vector3)average.GetValue(player), launch, "Explicit launch did not seed history");
        Near(player.rb.linearVelocity, launch, "Explicit launch velocity changed");
        player.Turn(90f);
        Vector3 rotated = Quaternion.Euler(0f, 90f, 0f) * launch;
        Near((Vector3)average.GetValue(player), rotated, "Turn changes averaged speed");
        for (int i = 0; i < player.velocityHistorySize; i++)
        {
            float dt = i % 2 == 0 ? .008f : 2f * step - .008f;
            player.transform.position += rotated * dt;
            store(dt);
            Near((Vector3)average.GetValue(player), rotated, "Turned launch history changes speed as samples expire");
        }

        // Invalid elapsed intervals must not consume a sample or mutate its history.
        int index = (int)typeof(NewPlayer).GetField("velocityIndex", Private).GetValue(player);
        store(0f); store(-step);
        if ((int)typeof(NewPlayer).GetField("velocityIndex", Private).GetValue(player) != index)
            throw new Exception("Non-positive timestep advanced history");
        Near((Vector3)average.GetValue(player), rotated, "Non-positive timestep changed launch history");

        player.Warp(new Vector3(100f, 2f, -80f));
        Near((Vector3)average.GetValue(player), Vector3.zero, "Warp retained launch history");
        store(step);
        Near(player.GetCurrentVelocity(), Vector3.zero, "Warp created velocity");

        foreach (float dt in new[] { .0001f, .001f, step, 2f * step })
        {
            player.InitializeValues();
            for (int i = 0; i < player.velocityHistorySize; i++)
            {
                player.transform.position += Vector3.right * 20f * dt;
                store(dt);
            }
            player.SetVelToAverageVel();
            Near(player.rb.linearVelocity, Vector3.right * player.maxJumpSpeed, "Extreme timestep bypassed jump cap");
        }

        // Development stick movement also resets this history; it must reuse its buffers.
        player.InitializeValues();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) player.InitializeValues();
        if (GC.GetAllocatedBytesForCurrentThread() != before)
            throw new Exception("History reset allocated after warm-up");
    }

    static void CheckWarpVelocityReset(NewPlayer player, FieldInfo average)
    {
        Vector3 incoming = new Vector3(3f, 2f, 1f);
        Vector3 impulse = new Vector3(1f, 4f, -2f);
        bool notified = false;
        Action onWarp = () =>
        {
            notified = true;
            Near(player.GetCurrentVelocity(), Vector3.zero, "Warp notification observes stale raw velocity");
            Near(player.rb.linearVelocity, Vector3.zero, "Warp notification observes stale physics velocity");
            Near((Vector3)average.GetValue(player), Vector3.zero, "Warp notification observes stale history");
        };
        NewPlayer.OnWarp += onWarp;
        try
        {
            foreach (bool rotate in new[] { false, true })
            foreach (LaunchType mode in new[] { LaunchType.OverwriteVelocity, LaunchType.CombineVelocity, LaunchType.OverwriteVerticalCombineHorizontal })
            {
                player.LaunchPlayer(incoming, LaunchType.OverwriteVelocity);
                notified = false;
                Vector3 destination = new Vector3(10f, 2f, -5f);
                if (rotate) player.Warp(destination, Quaternion.Euler(0f, 90f, 0f));
                else player.Warp(destination);
                if (!notified) throw new Exception("Warp notification not raised");
                Near(player.GetCurrentVelocity(), Vector3.zero, "Warp retains raw velocity before the next sample");
                player.LaunchPlayer(impulse, mode);
                Near(player.rb.linearVelocity, impulse, "Immediate post-warp launch reused old momentum");
                Near((Vector3)average.GetValue(player), impulse, "Post-warp launch history differs from impulse");
            }
        }
        finally { NewPlayer.OnWarp -= onWarp; }

        player.LaunchPlayer(incoming, LaunchType.OverwriteVelocity);
        player.ApplyPassengerDisplacement(new Vector3(1f, .5f, 0f));
        Near(player.GetCurrentVelocity(), incoming, "Passenger carry cleared player momentum");
        Near(player.rb.linearVelocity, incoming, "Passenger carry cleared physics velocity");

        player.Warp(Vector3.zero);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) player.Warp(Vector3.zero);
        if (GC.GetAllocatedBytesForCurrentThread() != before)
            throw new Exception("Repeated warp allocated after warm-up");
    }

    static void Near(Vector3 actual, Vector3 expected, string message)
    {
        if (float.IsNaN(actual.x + actual.y + actual.z) || float.IsInfinity(actual.x + actual.y + actual.z)
            || (actual - expected).sqrMagnitude > 0.000001f)
            throw new Exception(message + ": " + actual + " vs " + expected);
    }
}
