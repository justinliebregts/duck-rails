using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rendered-loop measurement, including combined forward/lift and zero/two physics steps.
[InitializeOnLoad]
public static class DevelopmentMovementPlayChecks
{
    const string Key = "DuckZ.DevelopmentMovementPlayChecks";
    static MovementTestRig rig;
    static Func<float, bool> move;
    static Rigidbody control;
    static int frame, scenario, steps, zeros, doubles, count;
    static Vector3 previous, previousControl;
    static double horizontalError, verticalError, controlError;
    static bool finished;
    static readonly int[] Rates = { 72, 90, 120, 144 };
    static DevelopmentMovementPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Start();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Start()
    {
        try
        {
            finished = false; scenario = 0;
            Physics.simulationMode = SimulationMode.FixedUpdate; Physics.autoSyncTransforms = false;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            HelicopterManager.instance = null; GR.Player.instance = null;
            rig = new MovementTestRig(); rig.p.stickMove = true;
            rig.p.headCollider.enabled = rig.p.bodyCollider.enabled = true;
            rig.p.locomotionEnabledLayers = 0;
            move = (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), rig.p,
                typeof(NewPlayer).GetMethod("DevelopmentMovement", BindingFlags.Instance | BindingFlags.NonPublic));
            control = new GameObject("Native lift interpolation reference").AddComponent<Rigidbody>();
            control.interpolation = RigidbodyInterpolation.Interpolate;
            Setup();
            new GameObject("Development rendered motion probe").AddComponent<DevelopmentMovementPlayProbe>();
        }
        catch (Exception e) { Fail(e); }
    }
    static void Setup()
    {
        frame = count = steps = zeros = doubles = 0;
        horizontalError = verticalError = controlError = 0;
        float step = 1f / Rates[scenario % Rates.Length];
        Time.fixedDeltaTime = step; Time.maximumDeltaTime = 2 * step; Time.captureDeltaTime = step;
        rig.p.Warp(Vector3.zero); rig.p.rb.useGravity = control.useGravity = scenario >= Rates.Length;
        control.position = Vector3.right * 10; control.linearVelocity = Vector3.zero;
        rig.p.controllerInput.leftThumbstickValue = Vector2.up;
        rig.p.controllerInput.rightThumbstickValue = Vector2.up;
        Physics.SyncTransforms();
    }
    public static void FixedTick() => steps++;
    public static void Move()
    {
        if (finished) return;
        try
        {
            rig.p.controllerInput.rightThumbstickValue = frame < 330 || frame >= 420 ? Vector2.up : Vector2.zero;
            move(Time.deltaTime);
            // Same vertical velocity commands and gravity, with native interpolation alone.
            control.linearVelocity = new Vector3(0, rig.p.rb.linearVelocity.y, 4.5f);
        }
        catch (Exception e) { Fail(e); }
    }
    public static void Sample()
    {
        if (finished) return;
        try
        {
            Vector3 position = rig.p.headCollider.transform.position;
            Vector3 reference = control.transform.position;
            if (frame > 60 && frame < 300)
            {
                if (steps == 0) zeros++; if (steps == 2) doubles++;
                float expectedZ = 4.5f * Time.deltaTime;
                float expectedY = (3f - (control.useGravity ? 9.81f * Time.fixedDeltaTime : 0)) * Time.deltaTime;
                horizontalError += Math.Abs(position.z - previous.z - expectedZ);
                verticalError += Math.Abs(position.y - previous.y - expectedY);
                controlError += Math.Abs(reference.y - previousControl.y - expectedY);
                count++;
            }
            if (frame >= 300)
            {
                if (Math.Abs((position.y - previous.y) - (reference.y - previousControl.y)) > .0006f)
                    throw new Exception("Lift release/re-engagement introduced extra vertical jitter");
                if (Math.Abs(position.z - previous.z - 4.5f * Time.deltaTime) > .0015f)
                    throw new Exception("Lift release/re-engagement interrupted forward movement");
            }
            previous = position; previousControl = reference; steps = 0;
            float step = Time.fixedDeltaTime;
            Time.captureDeltaTime = frame == 30 ? step - Mathf.Repeat((float)(Time.timeAsDouble - Time.fixedTimeAsDouble), step) + .0001f
                : frame % 4 == 0 ? step - .001f : frame % 4 == 1 ? step + .001f : step;
            if (++frame == 300)
            {
                Debug.Log($"DEVELOPMENT CADENCE hz={Rates[scenario % Rates.Length]} gravity={control.useGravity} zeros={zeros} doubles={doubles} meanHorizontalError={horizontalError/count:F6} meanLiftError={verticalError/count:F6} nativeLiftError={controlError/count:F6}");
                if (zeros < 15 || doubles < 15) throw new Exception("Missing zero/two-step cadence");
                if (horizontalError / count > .0015) throw new Exception("Joystick horizontal stutter");
                if (verticalError / count > controlError / count + .0006) throw new Exception("Forward plus lift is rougher than native physics interpolation");
            }
            if (frame < 480) return;
            if (++scenario < Rates.Length * 2) { Setup(); return; }
            Debug.Log("DevelopmentMovementPlayChecks PASS: eight rendered forward/lift cadences, lift release/re-engagement, with/without gravity and zero/two physics steps.");
            Finish(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void Fail(Exception e) { Debug.LogException(e); Finish(1); }
    static void Finish(int code) { finished = true; Time.captureDeltaTime = 0; SessionState.SetBool(Key, false); EditorApplication.Exit(code); }
}
public sealed class DevelopmentMovementPlayProbe : MonoBehaviour
{
    void FixedUpdate() => DevelopmentMovementPlayChecks.FixedTick();
    void Update() => DevelopmentMovementPlayChecks.Move();
    void LateUpdate() => DevelopmentMovementPlayChecks.Sample();
}
