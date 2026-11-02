using UnityEngine;

/// <summary>Finger curls and gesture edges, independent of the headset API and controller inputs.</summary>
public sealed class TrackedHandInput
{
    public struct Curls
    {
        public float thumb, index, middle, ring, pinky;
        public float Grip => Mathf.Min(middle, Mathf.Min(ring, pinky));
    }

    public const float PressThreshold = .75f;
    public const float ReleaseThreshold = .4f;
    public const float TrackingGrace = .35f;
    public bool Active { get; private set; }
    public Curls Visual { get; private set; }
    public ButtonStatus Grip { get; private set; }
    public ButtonStatus Trigger { get; private set; }
    public float TriggerValue => triggerArmed ? Visual.index : 0f;
    public bool SuppressThrow { get; private set; }
    bool gripArmed, triggerArmed, hasPose;
    float missingTime, validTime;

    public void Enter(bool gripHeld)
    {
        Active = true;
        Grip = new ButtonStatus { down = gripHeld };
        Trigger = default;
        gripArmed = gripHeld;
        triggerArmed = hasPose = false;
        missingTime = validTime = 0f;
        SuppressThrow = true;
    }

    public void Exit()
    {
        Active = false;
        Grip = Trigger = default;
        Visual = default;
        gripArmed = triggerArmed = hasPose = false;
        missingTime = validTime = 0f;
        SuppressThrow = false;
    }

    public void AfterGrab()
    {
        if (!Active) return;
        triggerArmed = false;
        Trigger = Edge(Trigger, false);
    }

    public void Tick(Curls sample, bool valid, float dt, bool focused = true)
    {
        if (!Active) return;
        dt = Mathf.Max(0f, dt);
        Grip = Edge(Grip, Grip.down);
        Trigger = Edge(Trigger, Trigger.down);
        if (!valid || !focused)
        {
            validTime = 0f;
            missingTime += dt;
            SuppressThrow = true;
            if (!focused || missingTime >= TrackingGrace)
            {
                Grip = Edge(Grip, false);
                Trigger = Edge(Trigger, false);
                gripArmed = triggerArmed = false;
            }
            return;
        }

        missingTime = 0f;
        validTime += dt;
        SuppressThrow = validTime < .1f;
        float blend = hasPose ? 1f - Mathf.Exp(-25f * dt) : 1f;
        Visual = new Curls {
            thumb = Mathf.Lerp(Visual.thumb, sample.thumb, blend),
            index = Mathf.Lerp(Visual.index, sample.index, blend),
            middle = Mathf.Lerp(Visual.middle, sample.middle, blend),
            ring = Mathf.Lerp(Visual.ring, sample.ring, blend),
            pinky = Mathf.Lerp(Visual.pinky, sample.pinky, blend)
        };
        hasPose = true;
        if (sample.Grip <= ReleaseThreshold) gripArmed = true;
        if (sample.index <= ReleaseThreshold) triggerArmed = true;
        Grip = Edge(Grip, gripArmed && Held(sample.Grip, Grip.down));
        Trigger = Edge(Trigger, triggerArmed && Held(sample.index, Trigger.down));
    }

    static bool Held(float curl, bool wasHeld) => curl >= (wasHeld ? ReleaseThreshold : PressThreshold);
    static ButtonStatus Edge(ButtonStatus before, bool down) => new ButtonStatus {
        down = down, wasPressed = down && !before.down, wasReleased = !down && before.down
    };
}
