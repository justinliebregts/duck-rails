using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>Desktop controls shared by keyboard simulation and VR shortcuts.</summary>
public static class RailDesktopInput
{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    static KeyControl KeyControl(KeyCode code)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return null;
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
            return keyboard[(Key)System.Enum.Parse(typeof(Key), "Digit" + ((int)code - (int)KeyCode.Alpha0))];
        // KeyCode and Input System Key values differ; map by name, never by integer.
        return System.Enum.TryParse(code.ToString(), out Key key) && key != Key.None ? keyboard[key] : null;
    }
    public static bool GetKey(KeyCode key) => KeyControl(key)?.isPressed ?? false;
    public static bool GetKeyDown(KeyCode key) => KeyControl(key)?.wasPressedThisFrame ?? false;
    static ButtonControl MouseButton(int button) => Mouse.current == null ? null :
        button == 0 ? Mouse.current.leftButton : button == 1 ? Mouse.current.rightButton :
        button == 2 ? Mouse.current.middleButton : null;
    public static bool GetMouseButton(int button) => MouseButton(button)?.isPressed ?? false;
    public static bool GetMouseButtonDown(int button) => MouseButton(button)?.wasPressedThisFrame ?? false;
    public static Vector3 mousePosition => Mouse.current == null ? Vector3.zero : (Vector3)Mouse.current.position.ReadValue();
    // Input System wheel values are pixels; the legacy controls expect wheel notches.
    public static Vector2 mouseScrollDelta => Mouse.current == null ? Vector2.zero : Mouse.current.scroll.ReadValue() / 120f;
    public static float GetAxisRaw(string axis)
    {
        if (axis == "Horizontal") return (GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow) ? 1 : 0) - (GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow) ? 1 : 0);
        if (axis == "Vertical") return (GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow) ? 1 : 0) - (GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow) ? 1 : 0);
        return GetAxis(axis);
    }
    public static float GetAxis(string axis)
    {
        if (axis == "Horizontal" || axis == "Vertical") return GetAxisRaw(axis);
        Vector2 delta = Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
        // Matches the project's legacy Mouse X/Y sensitivity (0.1).
        return axis == "Mouse X" ? delta.x * .1f : axis == "Mouse Y" ? delta.y * .1f : 0;
    }
#else
    public static bool GetKey(KeyCode key) => Input.GetKey(key);
    public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
    public static bool GetMouseButton(int button) => Input.GetMouseButton(button);
    public static bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
    public static Vector3 mousePosition => Input.mousePosition;
    public static Vector2 mouseScrollDelta => Input.mouseScrollDelta;
    public static float GetAxisRaw(string axis) => Input.GetAxisRaw(axis);
    public static float GetAxis(string axis) => Input.GetAxis(axis);
#endif
}
