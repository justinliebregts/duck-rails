using UnityEngine;

public partial class NewPlayer
{
    bool adminFlying, adminGravity, adminCollisions, adminKinematic;
    RigidbodyInterpolation adminInterpolation;
    readonly System.Collections.Generic.List<Collider> adminSolidColliders = new System.Collections.Generic.List<Collider>();

    bool UpdateAdminFlight()
    {
        if (!RailAdminTools.NoClip || (GR.Player.instance != null && GR.Player.instance.IsDead))
        { RestoreAdminFlight(); return false; }
        if (rb == null || headCollider == null || controllerInput == null) return false;
        if (!adminFlying)
        {
            adminFlying = true;
            adminGravity = rb.useGravity; adminCollisions = rb.detectCollisions;
            adminKinematic = rb.isKinematic; adminInterpolation = rb.interpolation;
            ClearLaunchLock();
            InitializeValues();
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            // Preserve fingertip triggers so the pause menu remains usable in flight.
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == rb)
                { adminSolidColliders.Add(collider); collider.enabled = false; }
            rb.useGravity = false; rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        PassengerCabin?.ReleasePassenger();
        movementPoseRenderCancelled = true;
        Vector2 stick = controllerInput.leftThumbstickValue;
        float lift = controllerInput.rightThumbstickValue.y;
        if (controllerInput.simulation != null && controllerInput.simulation.IsSimulating)
        {
            bool focused = Cursor.lockState == CursorLockMode.Locked && !RailWorld.InputBlocked;
            stick = focused ? new Vector2(RailDesktopInput.GetAxisRaw("Horizontal"), RailDesktopInput.GetAxisRaw("Vertical")) : Vector2.zero;
            lift = focused ? (RailDesktopInput.GetKey(KeyCode.Space) ? 1f : 0f) - (RailDesktopInput.GetKey(KeyCode.C) ? 1f : 0f) : 0f;
        }
        stick = stick.magnitude > .18f ? Vector2.ClampMagnitude(stick, 1f) : Vector2.zero;
        lift = Mathf.Abs(lift) > .18f ? Mathf.Clamp(lift, -1f, 1f) : 0f;
        Vector3 forward = Vector3.ProjectOnPlane(headCollider.transform.forward, Vector3.up);
        forward = forward.sqrMagnitude > .001f ? forward.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 delta = (forward * stick.y + Vector3.Cross(Vector3.up, forward) * stick.x + Vector3.up * lift) * (4.5f * Mathf.Min(Time.deltaTime, .05f));
        transform.position += delta;
        rb.position = transform.position;
        bodyCollider.transform.position = headCollider.transform.position + BodyForwardRot() * headBodyOffset;
        return true;
    }

    void RestoreAdminFlight()
    {
        if (!adminFlying) return;
        adminFlying = false;
        foreach (var collider in adminSolidColliders) if (collider != null) collider.enabled = true;
        adminSolidColliders.Clear();
        if (rb == null) return;
        rb.isKinematic = adminKinematic; rb.useGravity = adminGravity;
        rb.detectCollisions = adminCollisions; rb.interpolation = adminInterpolation;
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        currentVelocity = Vector3.zero;
        wasLeftHandTouching = wasRightHandTouching = wasHeadTouching = false;
        InitializeValues();
        lastBodyMidpoint = bodyMidpoint.position;
        lastPos = transform.position;
    }
}
