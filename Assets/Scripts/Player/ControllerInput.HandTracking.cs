using UnityEngine;

public partial class ControllerInput
{
    readonly TrackedHandInput trackedLeft = new TrackedHandInput();
    readonly TrackedHandInput trackedRight = new TrackedHandInput();
    readonly OvrHandCurlSource leftCurls = new OvrHandCurlSource();
    readonly OvrHandCurlSource rightCurls = new OvrHandCurlSource();
    bool suppressLeftThrow, suppressRightThrow;
    bool releaseLeftHoldOnSwitch, releaseRightHoldOnSwitch;

    void UpdateVrInput()
    {
        ButtonStatus oldLeftGrip = leftGripButton, oldRightGrip = rightGripButton;
        ButtonStatus oldLeftTrigger = leftTriggerButton, oldRightTrigger = rightTriggerButton;
        suppressLeftThrow = suppressRightThrow = false;
        OculusControllerInput();
        SampleTrackedHand(true, oldLeftGrip, oldLeftTrigger);
        SampleTrackedHand(false, oldRightGrip, oldRightTrigger);
    }

    void SampleTrackedHand(bool left, ButtonStatus oldGrip, ButtonStatus oldTrigger)
    {
        var input = left ? trackedLeft : trackedRight;
        var hand = left ? OVRInput.Controller.LHand : OVRInput.Controller.RHand;
        var touch = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        var active = OVRInput.GetActiveControllerForHand(left
            ? OVRInput.Handedness.LeftHanded : OVRInput.Handedness.RightHanded);
        // Same selection as OVRCameraRig; None during occlusion keeps the previous hand mode.
        if (active == OVRInput.Controller.None)
        {
            if (OVRInput.GetControllerPositionValid(hand)) active = hand;
            else if (OVRInput.GetControllerPositionValid(touch)) active = touch;
        }
        bool bare = active == hand || (active == OVRInput.Controller.None && input.Active);
        TrackedHandInput.Curls curls = default;
        bool focused = PhotonMasterHandoff.LocalAvailable;
        bool valid = bare && focused && (left ? leftCurls : rightCurls).TryRead(left, out curls);
        ApplyTrackedHand(left, bare, curls, valid, Time.unscaledDeltaTime, focused, oldGrip, oldTrigger);
    }

    void ApplyTrackedHand(bool left, bool bare, TrackedHandInput.Curls curls, bool valid, float dt,
        bool focused, ButtonStatus oldGrip, ButtonStatus oldTrigger)
    {
        var input = left ? trackedLeft : trackedRight;
        if (left) releaseLeftHoldOnSwitch = false; else releaseRightHoldOnSwitch = false;
        if (!bare)
        {
            if (!input.Active) return; // Controller-only input stays exactly as OVRInput supplied it.
            bool heldGripDown = input.HeldGrip.down;
            input.Exit();
            // Switching modes must still release a held item if the new controller grip is up.
            if (left)
            {
                leftGripButton = Transition(oldGrip, leftGripButton.down);
                leftTriggerButton = Transition(oldTrigger, leftTriggerButton.down);
                suppressLeftThrow = leftGripButton.wasReleased;
                releaseLeftHoldOnSwitch = heldGripDown && !leftGripButton.down;
                suppressLeftThrow |= releaseLeftHoldOnSwitch;
            }
            else
            {
                rightGripButton = Transition(oldGrip, rightGripButton.down);
                rightTriggerButton = Transition(oldTrigger, rightTriggerButton.down);
                suppressRightThrow = rightGripButton.wasReleased;
                releaseRightHoldOnSwitch = heldGripDown && !rightGripButton.down;
                suppressRightThrow |= releaseRightHoldOnSwitch;
            }
            return;
        }
        bool entering = !input.Active;
        if (entering) input.Enter(oldGrip.down);
        input.Tick(curls, valid, dt, focused);
        ButtonStatus trigger = input.Trigger;
        if (entering && oldTrigger.down && !trigger.down) trigger.wasReleased = true;
        if (left)
        {
            leftGripButton = input.Grip; leftTriggerButton = trigger;
            leftGripValue = input.Visual.Grip; leftTriggerValue = input.TriggerValue;
            xButton = yButton = leftThumbstickButton = default;
            leftThumbTouch = leftThumbHover = false;
            suppressLeftThrow = input.SuppressThrow;
        }
        else
        {
            rightGripButton = input.Grip; rightTriggerButton = trigger;
            rightGripValue = input.Visual.Grip; rightTriggerValue = input.TriggerValue;
            aButton = bButton = rightThumbstickButton = default;
            rightThumbTouch = rightThumbHover = false;
            suppressRightThrow = input.SuppressThrow;
        }
    }

    void UpdateDesktopInput()
    {
        bool leftHeld = trackedLeft.Active && trackedLeft.HeldGrip.down;
        bool rightHeld = trackedRight.Active && trackedRight.HeldGrip.down;
        suppressLeftThrow = trackedLeft.Active;
        suppressRightThrow = trackedRight.Active;
        trackedLeft.Exit(); trackedRight.Exit();
        leftGripButton = Transition(leftGripButton, false);
        leftTriggerButton = Transition(leftTriggerButton, false);
        rightTriggerButton = Transition(rightTriggerButton, false);
        leftGripValue = leftTriggerValue = rightTriggerValue = 0f;
        MouseGripInput();
        releaseLeftHoldOnSwitch = leftHeld && !leftGripButton.down;
        releaseRightHoldOnSwitch = rightHeld && !rightGripButton.down;
    }

    public bool TryGetTrackedFingerCurls(bool left, out Vector3 curls)
    {
        var input = left ? trackedLeft : trackedRight;
        var pose = input.Visual;
        curls = new Vector3(pose.thumb, pose.index, pose.middle);
        return !mouseGrip && input.Active;
    }

    public bool SuppressTrackedHandThrow(bool left) => left ? suppressLeftThrow : suppressRightThrow;

    // Only PlayerHand's already-held item/bag path uses the retained release edge.
    public bool WasHeldGripReleased(bool left)
    {
        var tracked = left ? trackedLeft : trackedRight;
        if (!mouseGrip && tracked.Active) return tracked.HeldGrip.wasReleased;
        return left ? leftGripButton.wasReleased || releaseLeftHoldOnSwitch
            : rightGripButton.wasReleased || releaseRightHoldOnSwitch;
    }

    public void NotifyTrackedHandGrab(bool left)
    {
        var input = left ? trackedLeft : trackedRight;
        if (!input.Active) return;
        input.AfterGrab();
        if (left) { leftTriggerButton = input.Trigger; leftTriggerValue = 0f; }
        else { rightTriggerButton = input.Trigger; rightTriggerValue = 0f; }
    }

    static ButtonStatus Transition(ButtonStatus old, bool down) => new ButtonStatus {
        down = down, wasPressed = down && !old.down, wasReleased = !down && old.down
    };
}
