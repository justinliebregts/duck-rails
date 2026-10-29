using UnityEngine;

namespace Game.Animation
{
    public class GaitTestDriver : MonoBehaviour
    {
        public Transform root;

        [Header("Movement")]
        public float moveSpeed = 1.6f;
        public float sprintMultiplier = 2.2f;
        public float turnSpeed = 120f;

        [Header("Vertical (test rig only)")]
        public float verticalSpeed = 0.4f;   // m/s — deliberately slow

        [Header("Keys")]
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode raiseKey = KeyCode.Space;
        public KeyCode lowerKey = KeyCode.LeftControl;

        [Header("Orbit")]
        public bool autoOrbit;
        public Vector3 orbitCenter;
        public float orbitRadius = 3f;
        float _angle;

        IGaitStats _gait; float _speed; Vector3 _lastPos;

        public Rigidbody rb;

        void Awake()
        {
            if (root == null) root = transform;
            _gait = GetComponent<IGaitStats>();
            _lastPos = root.position;
        }

        void FixedUpdate() {
            if (GR.Player.instance == null)
                return;

            Vector3 playerPos = GR.Player.instance.bottom.position;

            // Target position 3 units away from the player
            Vector3 toPlayer = playerPos - rb.position;

            if (toPlayer.sqrMagnitude < 0.001f)
                return;

            Vector3 target = playerPos - toPlayer.normalized * 3f;

            Vector3 toTarget = target - rb.position;
            float distance = toTarget.magnitude;

            const float stoppingDistance = 0.05f;
            const float slowDistance = 2f;

            if (distance > stoppingDistance)
            {
                // Slow down as we approach the target
                float speedMultiplier = Mathf.Clamp01(distance / slowDistance);
                speedMultiplier *= speedMultiplier;

                float speed = moveSpeed * speedMultiplier;

                // Prevent overshooting
                float movement = Mathf.Min(speed * Time.fixedDeltaTime, distance);

                Vector3 newPosition =
                    rb.position + toTarget.normalized * movement;

                rb.MovePosition(newPosition);
            }

            // Rotate toward player
            Quaternion targetRotation =
                Quaternion.LookRotation(toPlayer, Vector3.up);

            Quaternion newRotation = Quaternion.RotateTowards(
                rb.rotation,
                targetRotation,
                turnSpeed * Time.fixedDeltaTime
            );

            rb.MoveRotation(newRotation);
        }

        // void Update()
        // {
        //     float dt = Time.deltaTime;
        //     bool sprint = Input.GetKey(sprintKey);
        //     bool raise = Input.GetKey(raiseKey);
        //     float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);

        //     // vertical — works in both modes, deliberately slow
        //     if (raise)                          root.position += Vector3.up * (verticalSpeed * dt);
        //     if (Input.GetKey(lowerKey))          root.position -= Vector3.up * (verticalSpeed * dt);

        //     if (autoOrbit)
        //     {
        //         _angle += (speed / Mathf.Max(orbitRadius, 0.1f)) * dt;
        //         Vector3 p = orbitCenter + new Vector3(Mathf.Cos(_angle) * orbitRadius, root.position.y, Mathf.Sin(_angle) * orbitRadius);
        //         root.SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle))));
        //     }
        //     else
        //     {
        //         Vector2 wasd = new Vector2(
        //             (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
        //             (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        //         Vector3 fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
        //         Vector3 right = Vector3.Cross(Vector3.up, fwd);
        //         root.position += (fwd * wasd.y + right * wasd.x) * speed * dt;
        //         if (Input.GetKey(KeyCode.Q)) root.Rotate(Vector3.up, -turnSpeed * dt);
        //         if (Input.GetKey(KeyCode.E)) root.Rotate(Vector3.up,  turnSpeed * dt);
        //     }

        //     _speed = (root.position - _lastPos).magnitude / dt;
        //     _lastPos = root.position;
        //     Debug.DrawRay(root.position, root.forward, Color.red);
        //     if (autoOrbit) DrawOrbitPath();
        // }

        void DrawOrbitPath()
        {
            const int seg = 40; Vector3 prev = PointOnOrbit(0);
            for (int i = 1; i <= seg; i++) { Vector3 p = PointOnOrbit(i / (float)seg); Debug.DrawLine(prev, p, Color.green); prev = p; }
            Debug.DrawLine(orbitCenter - Vector3.up * 0.5f, orbitCenter + Vector3.up * 0.5f, Color.green);
        }
        Vector3 PointOnOrbit(float t) { float a = t * Mathf.PI * 2f; return orbitCenter + new Vector3(Mathf.Cos(a) * orbitRadius, 0f, Mathf.Sin(a) * orbitRadius); }

        void OnGUI()
        {
            string g = _gait != null ? $"\ngait: steps {_gait.StepsTotal} ({_gait.StepsPerSec:0.0}/s)  speed {_gait.RootSpeed:0.00} m/s" : "";
            GUI.Label(new Rect(10, 10, 700, 120),
                $"[GaitTestDriver] {(autoOrbit ? "ORBIT" : "WASD move / QE turn")}\n" +
                $"SHIFT sprint+rise ({verticalSpeed:0.0} m/s)   CTRL lower\n" +
                $"root {root.position}  yaw {root.eulerAngles.y:0}  driver speed {_speed:0.00} m/s{g}");
        }
    }
}