using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public partial class ControllerInput : MonoBehaviour
{
    // private readonly string TAG = "ControllerInput";

    [Header("==Input==")]
    public float leftTriggerValue;
    public float leftGripValue;
    public float rightTriggerValue;
    public float rightGripValue;

    public ButtonStatus xButton;
    public ButtonStatus yButton;
    public ButtonStatus leftTriggerButton;
    public ButtonStatus leftGripButton;
    public ButtonStatus leftStartButton;
    public ButtonStatus leftThumbstickButton;
    public Vector2 leftThumbstickValue;
    public ThumbstickStatus leftThumbstickStatus;
    public bool leftThumbHover;
    public bool leftThumbTouch;

    public ButtonStatus aButton;
    public ButtonStatus bButton;
    public ButtonStatus rightTriggerButton;
    public ButtonStatus rightGripButton;
    public ButtonStatus rightStartButton;
    public ButtonStatus rightThumbstickButton;
    public Vector2 rightThumbstickValue;
    public ThumbstickStatus rightThumbstickStatus;
    public bool rightThumbHover;
    public bool rightThumbTouch;

    [Tooltip("Desktop stand-in: no controllers. The left mouse button is the right hand's grip; everything else stays released.")]
    public bool mouseGrip;
    [System.NonSerialized] public SimulatedRigInput simulation;
    bool requireNeutral;

    public void RequireNeutralInput() { requireNeutral = true; ApplySimulationButtons(default); }

    private void Start()
    {
    }

    private void Update() {
      if (simulation != null && simulation.IsSimulating) {
        ApplySimulationButtons(simulation.Buttons);
        return;
      }
      if (mouseGrip) {
        MouseGripInput();
        return;
      }
      OculusControllerInput();
      if (requireNeutral) {
        if (!leftGripButton.down && !rightGripButton.down && !leftTriggerButton.down && !rightTriggerButton.down)
          requireNeutral = false;
        ApplySimulationButtons(default);
      }
      return;
    }

    public void ApplySimulationButtons(SimulatedRigInput.Frame frame) {
      SetButton(ref leftGripButton, frame.grip);
      SetButton(ref rightGripButton, frame.grip);
      SetButton(ref leftTriggerButton, frame.trigger);
      SetButton(ref rightTriggerButton, frame.trigger);
      SetButton(ref xButton, frame.x); SetButton(ref yButton, frame.y);
      SetButton(ref aButton, frame.a); SetButton(ref bButton, frame.b);
      SetButton(ref leftStartButton, frame.menu); SetButton(ref rightStartButton, false);
      SetButton(ref leftThumbstickButton, false); SetButton(ref rightThumbstickButton, false);
      leftGripValue = rightGripValue = frame.grip ? 1 : 0;
      leftTriggerValue = rightTriggerValue = frame.trigger ? 1 : 0;
      leftThumbstickValue = rightThumbstickValue = Vector2.zero;
      leftThumbstickStatus = rightThumbstickStatus = default;
      leftThumbHover = leftThumbTouch = rightThumbHover = rightThumbTouch = false;
    }

    static void SetButton(ref ButtonStatus button, bool down) {
      button.wasPressed = down && !button.down;
      button.wasReleased = !down && button.down;
      button.down = down;
    }

    void MouseGripInput() {
      bool locked = Cursor.lockState == CursorLockMode.Locked && !DesktopPlayer.SuppressMouseGrip && !RailWorld.InputBlocked;
      // The click that recaptures the cursor is not a grab.
      bool down = rightGripButton.down
        ? locked && Input.GetMouseButton(0)
        : locked && Input.GetMouseButtonDown(0) && Time.frameCount != DesktopPlayer.CursorLockedFrame;
      rightGripButton.wasPressed = down && !rightGripButton.down;
      rightGripButton.wasReleased = !down && rightGripButton.down;
      rightGripButton.down = down;
      rightGripValue = down ? 1f : 0f;
    }

    void OculusControllerInput() {
#if UNITY_EDITOR
      if(true) {
#else
      int x = 0;
      if(Application.isFocused) {
#endif
        //LEFT CONTROLLER
        xButton.down = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch);
        xButton.wasPressed = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch);
        xButton.wasReleased = OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.LTouch);

        yButton.down = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch);
        yButton.wasPressed = OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch);
        yButton.wasReleased = OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.LTouch);

        leftGripButton.down = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        leftGripButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        leftGripButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);

        leftTriggerButton.down = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        leftTriggerButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        leftTriggerButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);

        leftStartButton.down = OVRInput.Get(OVRInput.RawButton.Start, OVRInput.Controller.LTouch);
        leftStartButton.wasPressed = OVRInput.GetDown(OVRInput.RawButton.Start, OVRInput.Controller.LTouch);
        leftStartButton.wasReleased = OVRInput.GetUp(OVRInput.RawButton.Start, OVRInput.Controller.LTouch);

        leftThumbstickButton.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);
        leftThumbstickButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);
        leftThumbstickButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);

        leftTriggerValue = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        leftGripValue = OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger);

        leftThumbstickStatus.upTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.LTouch);
        leftThumbstickStatus.upTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.LTouch);
        leftThumbstickStatus.upTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.LTouch);

        leftThumbstickStatus.downTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.LTouch);
        leftThumbstickStatus.downTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.LTouch);
        leftThumbstickStatus.downTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.LTouch);

        leftThumbstickStatus.leftTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.LTouch);
        leftThumbstickStatus.leftTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.LTouch);
        leftThumbstickStatus.leftTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.LTouch);

        leftThumbstickStatus.rightTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.LTouch);
        leftThumbstickStatus.rightTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.LTouch);
        leftThumbstickStatus.rightTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.LTouch);

        leftThumbstickValue = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        leftThumbHover = OVRInput.Get(OVRInput.NearTouch.PrimaryThumbButtons, OVRInput.Controller.LTouch);
        leftThumbTouch = OVRInput.Get(OVRInput.Touch.One, OVRInput.Controller.LTouch) ||
                         OVRInput.Get(OVRInput.Touch.Two, OVRInput.Controller.LTouch) ||
                         OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, OVRInput.Controller.LTouch);

        //RIGHT CONTROLLER
        aButton.down = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        aButton.wasPressed = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);
        aButton.wasReleased = OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.RTouch);

        bButton.down = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        bButton.wasPressed = OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        bButton.wasReleased = OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch);

        rightGripButton.down = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        rightGripButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        rightGripButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        rightTriggerButton.down = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        rightTriggerButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        rightTriggerButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

        rightStartButton.down = OVRInput.Get(OVRInput.RawButton.Start, OVRInput.Controller.RTouch);
        rightStartButton.wasPressed = OVRInput.GetDown(OVRInput.RawButton.Start, OVRInput.Controller.RTouch);
        rightStartButton.wasReleased = OVRInput.GetUp(OVRInput.RawButton.Start, OVRInput.Controller.RTouch);

        rightThumbstickButton.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
        rightThumbstickButton.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
        rightThumbstickButton.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);

        rightTriggerValue = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);
        rightGripValue = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger);

        rightThumbstickStatus.upTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.RTouch);
        rightThumbstickStatus.upTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.RTouch);
        rightThumbstickStatus.upTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickUp, OVRInput.Controller.RTouch);

        rightThumbstickStatus.downTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.RTouch);
        rightThumbstickStatus.downTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.RTouch);
        rightThumbstickStatus.downTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickDown, OVRInput.Controller.RTouch);

        rightThumbstickStatus.leftTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch);
        rightThumbstickStatus.leftTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch);
        rightThumbstickStatus.leftTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch);

        rightThumbstickStatus.rightTouchpadButtonStatus.down = OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch);
        rightThumbstickStatus.rightTouchpadButtonStatus.wasPressed = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch);
        rightThumbstickStatus.rightTouchpadButtonStatus.wasReleased = OVRInput.GetUp(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch);

        rightThumbstickValue = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        rightThumbHover = OVRInput.Get(OVRInput.NearTouch.PrimaryThumbButtons, OVRInput.Controller.RTouch);
        rightThumbTouch = OVRInput.Get(OVRInput.Touch.One, OVRInput.Controller.RTouch) ||
                          OVRInput.Get(OVRInput.Touch.Two, OVRInput.Controller.RTouch) ||
                          OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, OVRInput.Controller.RTouch);
      } else {

        //LEFT CONTROLLER
        xButton.wasReleased = xButton.down;
        xButton.down = false;
        xButton.wasPressed = false;

        yButton.wasReleased = yButton.down;
        yButton.down = false;
        yButton.wasPressed = false;

        leftGripButton.wasReleased = leftGripButton.down;
        leftGripButton.down = false;
        leftGripButton.wasPressed = false;

        leftTriggerButton.wasReleased = leftTriggerButton.down;
        leftTriggerButton.down = false;
        leftTriggerButton.wasPressed = false;

        leftStartButton.wasReleased = leftStartButton.down;
        leftStartButton.down = false;
        leftStartButton.wasPressed = false;

        leftThumbstickButton.wasReleased = leftThumbstickButton.down;
        leftThumbstickButton.down = false;
        leftThumbstickButton.wasPressed = false;

        leftTriggerValue = 0f;
        leftGripValue = 0f;

        leftThumbstickStatus.upTouchpadButtonStatus.wasReleased = leftThumbstickStatus.upTouchpadButtonStatus.down;
        leftThumbstickStatus.upTouchpadButtonStatus.down = false;
        leftThumbstickStatus.upTouchpadButtonStatus.wasPressed = false;

        leftThumbstickStatus.downTouchpadButtonStatus.wasReleased = leftThumbstickStatus.downTouchpadButtonStatus.down;
        leftThumbstickStatus.downTouchpadButtonStatus.down = false;
        leftThumbstickStatus.downTouchpadButtonStatus.wasPressed = false;

        leftThumbstickStatus.leftTouchpadButtonStatus.wasReleased = leftThumbstickStatus.leftTouchpadButtonStatus.down;
        leftThumbstickStatus.leftTouchpadButtonStatus.down = false;
        leftThumbstickStatus.leftTouchpadButtonStatus.wasPressed = false;

        leftThumbstickStatus.rightTouchpadButtonStatus.wasReleased = leftThumbstickStatus.rightTouchpadButtonStatus.down;
        leftThumbstickStatus.rightTouchpadButtonStatus.down = false;
        leftThumbstickStatus.rightTouchpadButtonStatus.wasPressed = false;

        leftThumbstickValue = Vector2.zero;

        //RIGHT CONTROLLER
        aButton.wasReleased = aButton.down;
        aButton.down = false;
        aButton.wasPressed = false;

        bButton.wasReleased = bButton.down;
        bButton.down = false;
        bButton.wasPressed = false;

        rightGripButton.wasReleased = rightGripButton.down;
        rightGripButton.down = false;
        rightGripButton.wasPressed = false;

        rightTriggerButton.wasReleased = rightTriggerButton.down;
        rightTriggerButton.down = false;
        rightTriggerButton.wasPressed = false;

        rightStartButton.wasReleased = rightStartButton.down;
        rightStartButton.down = false;
        rightStartButton.wasPressed = false;

        rightThumbstickButton.wasReleased = rightThumbstickButton.down;
        rightThumbstickButton.down = false;
        rightThumbstickButton.wasPressed = false;

        rightTriggerValue = 0f;
        rightGripValue = 0f;

        rightThumbstickStatus.upTouchpadButtonStatus.wasReleased = rightThumbstickStatus.upTouchpadButtonStatus.down;
        rightThumbstickStatus.upTouchpadButtonStatus.down = false;
        rightThumbstickStatus.upTouchpadButtonStatus.wasPressed = false;

        rightThumbstickStatus.downTouchpadButtonStatus.wasReleased = rightThumbstickStatus.downTouchpadButtonStatus.down;
        rightThumbstickStatus.downTouchpadButtonStatus.down = false;
        rightThumbstickStatus.downTouchpadButtonStatus.wasPressed = false;

        rightThumbstickStatus.leftTouchpadButtonStatus.wasReleased = rightThumbstickStatus.leftTouchpadButtonStatus.down;
        rightThumbstickStatus.leftTouchpadButtonStatus.down = false;
        rightThumbstickStatus.leftTouchpadButtonStatus.wasPressed = false;

        rightThumbstickStatus.rightTouchpadButtonStatus.wasReleased = rightThumbstickStatus.rightTouchpadButtonStatus.down;
        rightThumbstickStatus.rightTouchpadButtonStatus.down = false;
        rightThumbstickStatus.rightTouchpadButtonStatus.wasPressed = false;

        rightThumbstickValue = Vector2.zero;
      }
    }

    public ButtonStatus ReturnButtonPress(ButtonName buttonName, PlayerHand pHand) {
        
    // public ButtonStatus ReturnButtonPress(ButtonName buttonName, OVRInput.Controller controller) {
        ButtonStatus none = new ButtonStatus();
        if (pHand == null) {
            return none;
        }
        if (pHand.IsControllerLTouch) {
        // if (controller == OVRInput.Controller.LTouch) {
            if(buttonName == ButtonName.X) {
                return xButton;
            } else if (buttonName == ButtonName.Y) {
                return yButton;
            } else if (buttonName == ButtonName.Trigger) {
                return leftTriggerButton;
            } else if (buttonName == ButtonName.Grip) {
                return leftGripButton;
            } else if (buttonName == ButtonName.Start) {
                return leftStartButton;
            } else if (buttonName == ButtonName.Thumbstick) {
                return leftThumbstickButton;
            } else if (buttonName == ButtonName.ThumbstickUp) {
                return leftThumbstickStatus.upTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickDown) {
                return leftThumbstickStatus.downTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickLeft) {
                return leftThumbstickStatus.leftTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickRight) {
                return leftThumbstickStatus.rightTouchpadButtonStatus;
            }
        } else if (pHand.IsControllerRTouch) {
        // } else if (controller == OVRInput.Controller.RTouch) {
            if (buttonName == ButtonName.A) {
                return aButton;
            } else if (buttonName == ButtonName.B) {
                return bButton;
            } else if (buttonName == ButtonName.Trigger) {
                return rightTriggerButton;
            } else if (buttonName == ButtonName.Grip) {
                return rightGripButton;
            } else if (buttonName == ButtonName.Start) {
                return rightStartButton;
            } else if (buttonName == ButtonName.Thumbstick) {
                return rightThumbstickButton;
            } else if (buttonName == ButtonName.ThumbstickUp) {
                return rightThumbstickStatus.upTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickDown) {
                return rightThumbstickStatus.downTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickLeft) {
                return rightThumbstickStatus.leftTouchpadButtonStatus;
            } else if (buttonName == ButtonName.ThumbstickRight) {
                return rightThumbstickStatus.rightTouchpadButtonStatus;
            }
        }
        return none;
    }

    // public bool IsOculusBtnStartPressed
    // {
    //     get => ReturnButtonPress(ButtonName.Start, OVRInput.Controller.RTouch).wasPressed; // Oculus Button Pressed
    // }
}

[System.Serializable]
public struct ButtonStatus
{
    public bool down;
    public bool wasPressed;
    public bool wasReleased;

    public override string ToString()
    {
        if (down || wasPressed || wasReleased)
            return $"down:{down}, wPressed:{wasPressed}, wReleased:{wasReleased}";

        return "False";
    }
}

[System.Serializable]
public struct ThumbstickStatus {
    public ButtonStatus rightTouchpadButtonStatus;
    public ButtonStatus leftTouchpadButtonStatus;
    public ButtonStatus upTouchpadButtonStatus;
    public ButtonStatus downTouchpadButtonStatus;

    public override string ToString()
    {
        return $"u:{upTouchpadButtonStatus}, d:{downTouchpadButtonStatus}, l:{leftTouchpadButtonStatus}, r{rightTouchpadButtonStatus}";
    }
}

[System.Serializable]
public enum ButtonName {
    A = 0,
    B = 1,
    X = 2,
    Y = 3,
    Start = 4,
    Trigger = 5,
    Grip = 6,
    Thumbstick = 7,
    ThumbstickUp = 8,
    ThumbstickDown = 9,
    ThumbstickLeft = 10,
    ThumbstickRight = 11
}
