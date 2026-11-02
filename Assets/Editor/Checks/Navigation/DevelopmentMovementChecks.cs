using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Real physics in a disposable batch project. No scene replacement in the working editor.
public static class DevelopmentMovementChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static T Field<T>(NewPlayer p, string name) => (T)typeof(NewPlayer).GetField(name, Private).GetValue(p);
    static void Set(NewPlayer p, string name, object value) => typeof(NewPlayer).GetField(name, Private).SetValue(p, value);
    static void Near(float value, float expected, float tolerance, string message) => Check(Mathf.Abs(value - expected) <= tolerance, message + ": " + value + " expected " + expected);
    static GameObject Wall(Vector3 position, Vector3 size, bool trigger = false)
    {
        var go = new GameObject("Development collision fixture");
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>(); box.size = size; box.isTrigger = trigger;
        Physics.SyncTransforms(); return go;
    }

    sealed class Fixture : IDisposable
    {
        public readonly MovementTestRig rig = new MovementTestRig();
        public NewPlayer p => rig.p;
        public readonly Func<float, bool> move;
        public Fixture()
        {
            p.rb.interpolation = RigidbodyInterpolation.None;
            p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            p.headCollider.enabled = p.bodyCollider.enabled = true;
            p.locomotionEnabledLayers = 1; p.stickMove = true;
            // Player geometry must not appear in environment query results.
            p.headCollider.gameObject.layer = p.bodyCollider.gameObject.layer = 2;
            move = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), p, typeof(NewPlayer).GetMethod("DevelopmentMovement", Private));
            move(1f / 72);
        }
        public void Step(Vector2 input, float lift = 0f, float dt = 1f / 72, bool simulate = true)
        {
            p.controllerInput.leftThumbstickValue = input;
            p.controllerInput.rightThumbstickValue = new Vector2(0f, lift);
            if (simulate) { Physics.SyncTransforms(); Physics.Simulate(dt); }
            move(dt);
        }
        public void Dispose() => rig.Dispose();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        checks = 0;
        var mode = Physics.simulationMode; var oldVR = NewPlayer.instance;
        var oldPlayer = GR.Player.instance; var oldHeli = HelicopterManager.instance;
        Physics.simulationMode = SimulationMode.Script; HelicopterManager.instance = null; GR.Player.instance = null;
        try
        {
            InputAndCadence(); CollisionCases(); LiftAndTransitions(); GeometryAndCarryEdges(); AllocationCheck();
            Debug.Log("DevelopmentMovementChecks PASS: " + checks + " assertions; analog/cadence, body/head sweeps, slide/corners, tracking, gravity/lift, transitions and warmed allocations.");
        }
        finally { Physics.simulationMode = mode; NewPlayer.instance = oldVR; GR.Player.instance = oldPlayer; HelicopterManager.instance = oldHeli; }
    }

    static void InputAndCadence()
    {
        float reference = 0f;
        foreach (int hz in new[] { 72, 90, 120, 144 })
        using (var f = new Fixture())
        {
            for (int i = 0; i < hz * 2; i++) f.Step(Vector2.up, 0, 1f / hz);
            float distance = f.p.rb.position.z;
            Near(distance, 4.5f * (2f - .075f), .002f, "Acceleration ramp travel at " + hz);
            if (reference != 0) Near(distance, reference, .002f, "Refresh rate changed travel");
            reference = distance;
            for (int i = 0; i < hz; i++) f.Step(Vector2.zero, 0, 1f / hz);
            Near(f.p.rb.position.z - distance, .16875f, .002f, "Braking distance");
        }
        using (var f = new Fixture())
        {
            for (int i = 0; i < 20; i++) f.Step(new Vector2(.1f, .1f));
            Near(f.p.rb.position.magnitude, 0, .00001f, "Deadzone drift");
            for (int i = 0; i < 50; i++) f.Step(new Vector2(0, .59f));
            Near(Field<Vector3>(f.p, "developmentVelocity").magnitude, 2.25f, .001f, "Half analog speed");
            for (int i = 0; i < 50; i++) f.Step(Vector2.one);
            Near(Field<Vector3>(f.p, "developmentVelocity").magnitude, 4.5f, .001f, "Diagonal speed boost");
            f.p.headCollider.transform.localRotation = Quaternion.Euler(90, 0, 0);
            f.Step(Vector2.up);
            Check(float.IsFinite(f.p.rb.position.x) && float.IsFinite(f.p.rb.position.z), "Look-up singularity");
            Vector3 before = f.p.rb.position;
            f.Step(Vector2.up, 0, .5f, false);
            Check(Vector3.Distance(before, f.p.rb.position) <= .226f, "Hitch created large powered jump");
            f.p.rotateMode = 1; f.p.controllerInput.rightThumbstickButton.wasPressed = true;
            typeof(NewPlayer).GetMethod("Rotate", Private).Invoke(f.p, null);
            Check(!f.p.stickMove && f.p.rotateMode == 1, "Toggle overwrote turn preference");
        }
        using (var f = new Fixture())
        {
            f.p.headCollider.transform.localRotation = Quaternion.Euler(90, 0, 0);
            f.p.Turn(90);
            for (int i = 0; i < 30; i++) f.Step(Vector2.up);
            Check(Field<Vector3>(f.p, "developmentVelocity").x > 4.49f, "Vertical gaze lost snap-turn heading");
        }
    }

    static void CollisionCases()
    {
        foreach (float wallHeight in new[] { .8f, 1.6f })
        {
            var wall = Wall(new Vector3(0, wallHeight, 1), new Vector3(10, .3f, .01f));
            try
            {
                using (var f = new Fixture())
                {
                    for (int i = 0; i < 150; i++) f.Step(Vector2.up);
                    Check(f.p.rb.position.z <= .85f && f.p.rb.position.z > .8f, "Thin head/body wall crossed at height " + wallHeight);
                    float startX = f.p.rb.position.x;
                    for (int i = 0; i < 100; i++) f.Step(Vector2.one);
                    Check(f.p.rb.position.x > startX + 2 && f.p.rb.position.z < .85f, "Wall slide stuck or crossed wall");
                }
            }
            finally { Object.DestroyImmediate(wall); }
        }
        var front = Wall(new Vector3(0, 1, 1), new Vector3(10, 4, .01f));
        var side = Wall(new Vector3(1, 1, 0), new Vector3(.01f, 4, 10));
        try
        {
            using (var f = new Fixture())
            {
                for (int i = 0; i < 200; i++) f.Step(Vector2.one);
                Check(f.p.rb.position.x < .85f && f.p.rb.position.z < .85f, "Corner escaped");
                for (int i = 0; i < 60; i++) f.Step(-Vector2.one);
                Check(f.p.rb.position.x < 0 && f.p.rb.position.z < 0, "Corner could not release");
            }
            using (var f = new Fixture())
            {
                f.p.headCollider.transform.localPosition += Vector3.forward * 2;
                f.Step(Vector2.zero, 0, 1f / 72, false);
                Check(f.p.headCollider.transform.position.z < .85f, "Idle room-scale head crossed thin wall");
            }
            using (var f = new Fixture())
            {
                f.p.rb.linearVelocity = Vector3.forward * 100;
                f.Step(Vector2.zero, 0, .05f);
                Check(f.p.headCollider.transform.position.z < .85f, "External fast motion crossed wall");
            }
        }
        finally { Object.DestroyImmediate(front); Object.DestroyImmediate(side); }
        var trigger = Wall(new Vector3(0, 1, 1), new Vector3(10, 4, .01f), true);
        try { using (var f = new Fixture()) { for (int i = 0; i < 72; i++) f.Step(Vector2.up); Check(f.p.rb.position.z > 3, "Trigger blocked travel"); } }
        finally { Object.DestroyImmediate(trigger); }
        var overlap = Wall(new Vector3(0, 1.6f, .1f), new Vector3(2, 1, .1f));
        try { using (var f = new Fixture()) { f.Step(Vector2.zero); Check(f.p.headCollider.transform.position.z < -.1f, "Starting overlap not recovered"); } }
        finally { Object.DestroyImmediate(overlap); }
    }

    static void LiftAndTransitions()
    {
        using (var f = new Fixture())
        {
            f.p.rb.useGravity = true;
            for (int i = 0; i < 72; i++) f.Step(Vector2.up, 1);
            Check(f.p.rb.position.y > 2 && f.p.rb.position.z > 4, "Combined forward/lift did not travel");
            float vy = f.p.rb.linearVelocity.y;
            f.Step(Vector2.up);
            Check(f.p.rb.linearVelocity.y < vy && f.p.rb.linearVelocity.y > 0, "Release erased upward momentum or gravity");
            for (int i = 0; i < 72; i++) f.Step(Vector2.up);
            Check(f.p.rb.linearVelocity.y < 0, "Release did not fall");
            f.p.rb.linearVelocity = Vector3.down * 8;
            f.Step(Vector2.up, 1, 1f / 72, false);
            Check(f.p.rb.linearVelocity.y > -8 && f.p.rb.linearVelocity.y < -7.5f, "Lift abruptly cancelled a fall");
            f.p.rb.linearVelocity = Vector3.up * 8;
            f.Step(Vector2.up, 1, 1f / 72, false);
            Check(f.p.rb.linearVelocity.y < 8 && f.p.rb.linearVelocity.y > 7.5f, "Lift abruptly cancelled external launch");
            f.p.disableMovement = true;
            Vector3 launched = f.p.rb.linearVelocity;
            typeof(NewPlayer).GetMethod("Update", Private).Invoke(f.p, null);
            Near((f.p.rb.linearVelocity - launched).magnitude, 0, .0001f, "Disabled input overwrote launch momentum");
            Near(Field<Vector3>(f.p, "developmentVelocity").magnitude, 0, .0001f, "Disabled input retained acceleration");
            Check(Field<bool>(f.p, "developmentMoving"), "Launch suspension discarded safe anchors");
            f.p.disableMovement = false;
            f.p.rb.linearVelocity = Vector3.zero; f.p.rb.useGravity = false;
            f.p.controllerInput.rightThumbstickValue = new Vector2(1, .1f); f.move(1f / 72);
            Near(f.p.rb.linearVelocity.y, 0, .0001f, "Sideways turning enabled lift");
            f.Step(Vector2.zero, -1); Near(f.p.rb.linearVelocity.y, 0, .0001f, "Down stick applied thrust");
            f.p.Warp(new Vector3(10, 2, 4));
            Near(Field<Vector3>(f.p, "developmentVelocity").magnitude, 0, .0001f, "Warp retained joystick motion");
            f.Step(Vector2.zero); Near(f.p.rb.position.z, 4, .001f, "Warp swept from old location");
            f.Step(Vector2.up); f.p.stickMove = false;
            Check(!f.move(1f / 72), "Mode switch did not return to hand locomotion");
            Near(Field<Vector3>(f.p, "denormalizedVelocityAverage").magnitude, 0, .0001f, "Mode switch retained false hand launch");
            f.p.stickMove = true; f.Step(Vector2.up);
            typeof(NewPlayer).GetMethod("OnApplicationPause", Private).Invoke(f.p, new object[] { true });
            Near(Field<Vector3>(f.p, "developmentVelocity").magnitude, 0, .0001f, "Pause retained powered motion");
        }
        var ceiling = Wall(new Vector3(0, 2.2f, 0), new Vector3(30, .02f, 30));
        try
        {
            using (var f = new Fixture())
            {
                f.p.rb.useGravity = true;
                for (int i = 0; i < 180; i++)
                {
                    f.Step(Vector2.up, 1);
                    Check(f.p.headCollider.transform.position.y <= 2.05f, "Lift crossed ceiling");
                }
                Check(f.p.rb.position.z > 10, "Ceiling stopped horizontal travel");
                Check(f.p.rb.linearVelocity.y <= .5f, "Ceiling accumulated lift");
            }
        }
        finally { Object.DestroyImmediate(ceiling); }
    }

    static void AllocationCheck()
    {
        using (var f = new Fixture())
        {
            f.p.controllerInput.leftThumbstickValue = Vector2.up;
            f.p.controllerInput.rightThumbstickValue = Vector2.up;
            for (int i = 0; i < 100; i++) f.move(1f / 72);
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) f.move(1f / 72);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Check(bytes == 0, "Warmed development movement allocated " + bytes + " bytes");
        }
    }

    static void GeometryAndCarryEdges()
    {
        var floor = Wall(new Vector3(0, 0, 0), new Vector3(100, .1f, 100));
        try
        {
            using (var f = new Fixture())
            {
                f.p.rb.useGravity = true;
                for (int i = 0; i < 120; i++) f.Step(Vector2.up);
                Check(f.p.rb.position.z > 6, "Ground contact prevented travel");
                Check(f.p.bodyCollider.bounds.min.y > .035f, "Body fell through floor");
                var before = f.p.rb.position;
                f.p.ApplyPassengerDisplacement(Vector3.right * 2);
                f.Step(Vector2.up);
                Near(f.p.rb.position.x - before.x, 2, .002f, "Carry fought joystick anchors");
                f.p.Turn(45); f.Step(Vector2.up);
                Check(Vector3.Distance(f.p.rb.position, before + Vector3.right * 2) < .3f, "Turn/carry produced false sweep rewind");
            }
        }
        finally { Object.DestroyImmediate(floor); }
        // A wall moving into an idle player must depenetrate even without input.
        using (var f = new Fixture())
        {
            var moving = Wall(new Vector3(0, 1.6f, 2), new Vector3(3, .5f, .1f));
            try
            {
                moving.transform.position = new Vector3(0, 1.6f, .1f); Physics.SyncTransforms();
                f.Step(Vector2.zero, 0, 1f / 72, false);
                Check(f.p.headCollider.transform.position.z < -.1f, "Moving obstacle did not recover idle overlap");
            }
            finally { Object.DestroyImmediate(moving); }
        }
        // A full nonalloc buffer is not proof that a route is clear. Fail closed without allocations.
        using (var f = new Fixture())
        {
            var obstacles = new GameObject[20];
            try
            {
                for (int i = 0; i < obstacles.Length; i++) obstacles[i] = Wall(new Vector3(0, 1.6f, .02f), new Vector3(2, .5f, .01f));
                Vector3 before = f.p.rb.position;
                for (int i = 0; i < 10; i++) f.Step(Vector2.up, 0, 1f / 72, false);
                Check(Vector3.Distance(f.p.rb.position, before) < .0001f, "Saturated overlap query allowed movement");
            }
            finally { foreach (var obstacle in obstacles) if (obstacle != null) Object.DestroyImmediate(obstacle); }
        }
    }
}
