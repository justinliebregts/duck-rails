using System;
using System.Collections;
using System.Collections.Generic;
using Game.Animation;
using GR;
using UnityEngine;
using UnityEngine.XR;
// using DarkTonic.MasterAudio;
// using Sirenix.OdinInspector;
// using UnityEngine.Splines;

public class NewPlayer : MonoBehaviour
    {
        public static NewPlayer instance;

        public SphereCollider headCollider;
        public CapsuleCollider bodyCollider;
        public Transform headEyeOffset;
        public Transform trackingSpace;
        public Transform bodyMidpoint;

        public Transform leftHandFollower;
        public Transform rightHandFollower;

        public Transform colVisLeft;
        public Transform colVisRight;

        public Transform rightHandTransform;
        public Transform leftHandTransform;

        public Vector3 lastLeftHandPosition;
        public Vector3 lastRightHandPosition;
        private Vector3 lastHeadPosition;

        const int MaxLocoSamples = Grabbable.MaxLocomotionSamples;
        readonly Vector3[] lastLeftLocoSamples = new Vector3[MaxLocoSamples];
        readonly Vector3[] lastRightLocoSamples = new Vector3[MaxLocoSamples];
        readonly Vector3[] currentLeftLocoSamples = new Vector3[MaxLocoSamples];
        readonly Vector3[] currentRightLocoSamples = new Vector3[MaxLocoSamples];
        int lastLeftLocoCount;
        int lastRightLocoCount;

        public Rigidbody rb;

        public int velocityHistorySize;
        public float maxArmLength = 1.5f;
        public float unStickDistance = 1f;

        public float velocityLimit;
        public float maxJumpSpeed;
        public float jumpMultiplier;

        [Header("Dead State (Ghost)")]
        public float deadVelocityLimit = 0.32f;
        public float deadMaxJumpSpeed = 8.2f;
        public float deadJumpMultiplier = 1.32f;

        public float minimumRaycastDistance = 0.05f;
        public float defaultSlideFactor = 0.03f;
        public float defaultPrecision = 0.995f;

        private Vector3[] velocityHistory;
        private float[] velocityHistoryDurations;
        private int velocityIndex;
        private Vector3 currentVelocity;
        private Vector3 denormalizedVelocityAverage;
        private bool jumpHandIsLeft;
        private Vector3 lastPosition;

        public Vector3 rightHandOffset;
        public Vector3 leftHandOffset;

        public LayerMask locomotionEnabledLayers;

        public bool wasLeftHandTouching;
        public bool wasRightHandTouching;
        public bool wasHeadTouching;

        public bool disableMovement = false;

        public ControllerInput controllerInput;

        [HideInInspector] public int snapRotateAmount = 45;
      	[HideInInspector] public int smoothRotateAmount = 30;
        [HideInInspector] public float rotateDeadzone = 0.5f;
      	[HideInInspector] public float moveDeadzone = 0.5f;
      	[HideInInspector] public float deadzone = 0.5f;
        

        [HideInInspector] public int rotateMode = 2;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      	[HideInInspector] public bool stickMove = false;
#endif

        Vector3 headBodyOffset;

        // [FoldoutGroup("Sounds")][SoundGroup] public string bounceBackSound;
        // [FoldoutGroup("Sounds")][SoundGroup] public string warpSound;
        // [FoldoutGroup("Sounds")][SoundGroup] readonly string ropeGrabSound = "RopeGrab";
        [Header("Hand Hit Haptics")]
        [Tooltip("Vibration amplitude (0-1) at handHitMinVel and below")]
        [Range(0f, 1f)] public float handHitHapticMinAmplitude = 0.15f;
        [Tooltip("Vibration amplitude (0-1) at handHitFullVolumeVel and above")]
        [Range(0f, 1f)] public float handHitHapticMaxAmplitude = 1f;
        [Tooltip("Vibration duration (s) for light hand touches")]
        public float handHitHapticMinDuration = 0.03f;
        [Tooltip("Vibration duration (s) for hard hand hits")]
        public float handHitHapticMaxDuration = 0.1f;
        [Tooltip("Vibration frequency (0-1) passed to OVRInput")]
        [Range(0f, 1f)] public float handHitHapticFrequency = 1f;

        [Tooltip("Hand hit sound per surface type.")]
        public Game.Audio.SurfaceSoundSet handHitSounds;
        [Tooltip("Body landing thud per surface type.")]
        public Game.Audio.SurfaceSoundSet bodyLandingSounds;
        [Header("Hand Hit SFX Volume Curve")]
        [Tooltip("Hand velocity (m/s) below which hit sound is quietest")]
        public float handHitMinVel = 0.3f;
        [Tooltip("Hand velocity (m/s) at which hit sound is full volume")]
        public float handHitFullVolumeVel = 4f;
        [Tooltip("Minimum volume (0-1) for light hand touches")]
        [Range(0f, 1f)] public float handHitMinVolume = 0.2f;

        // [FoldoutGroup("Sounds")][Header("Body Landing SFX")]
        // [FoldoutGroup("Sounds")][Tooltip("Velocity magnitude required to trigger body landing thud")]
        readonly float bodyLandingMinMagnitude = 2f;

        const int HandVelocityHistorySize = 5;
        readonly Queue<Vector3> leftHandVelHistory = new Queue<Vector3>();
        readonly Queue<Vector3> rightHandVelHistory = new Queue<Vector3>();

        float leftHandNotCollidingTime = 0f;
        float rightHandNotCollidingTime = 0f;

        public static event Action OnWarp;

        public GameObject handAnchorPrefab;
        private Transform currHandAnchor;

        int envLayer;
        int groundedCount = 0;

        LayerMask envMask;
        float bodyHalfHeight = 1f;


        InputDevice _head, _lHand, _rHand;
        LocalBodyDriver _bodyDriver;
        private void Awake()
        {
          instance = this;
          _bodyDriver = GetComponent<LocalBodyDriver>();
          InitializeValues();
          headBodyOffset = Quaternion.Inverse(headCollider.transform.rotation) * (bodyCollider.transform.position - headCollider.transform.position);
          envLayer = LayerMask.NameToLayer("Environment");
          lastPos = transform.position;
          lastBodyMidpoint = bodyMidpoint.position;
          envMask = LayerMask.GetMask("Environment");
          bodyHalfHeight = bodyCollider.height / 2f + bodyCollider.radius;

          RefreshDevices();
          UserPlayerPrefs.UserReady += ApplySavedTurnSettings;
          ApplySavedTurnSettings();
        }

        void RefreshDevices() {
            _head  = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            _lHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        void OnDestroy() {
          UserPlayerPrefs.UserReady -= ApplySavedTurnSettings;
          if (instance == this)
            instance = null;
        }

        void ApplySavedTurnSettings() {
          SettingsMenu.ApplySavedTurnSettings(this);
        }

        public void SetSmoothRotateAmount(int amount) {
          smoothRotateAmount = Mathf.Clamp(amount, 0, 200);
        }

        // Slider 0 is no deadzone, 15 is a 0.5 stick threshold, 30 needs a full deflection.
        public void SetRotateDeadzoneAmount(int amount) {
          rotateDeadzone = Mathf.Clamp(amount, 0, 30) / 30f;
        }

        public void InitializeValues()
        {
            if (velocityHistory == null || velocityHistory.Length != velocityHistorySize)
            {
                velocityHistory = new Vector3[velocityHistorySize];
                velocityHistoryDurations = new float[velocityHistorySize];
            }
            else
            {
                Array.Clear(velocityHistory, 0, velocityHistory.Length);
            }
            // Seed stationary history to preserve the existing gradual launch warm-up.
            float historyStep = Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime;
            for (int i = 0; i < velocityHistoryDurations.Length; i++)
                velocityHistoryDurations[i] = historyStep;
            lastLeftLocoCount = 0;
            lastRightLocoCount = 0;
            lastLeftHandPosition = CurrentLeftHandPosition();
            lastRightHandPosition = CurrentRightHandPosition();
            lastHeadPosition = headCollider.transform.position;
            denormalizedVelocityAverage = Vector3.zero;
            velocityIndex = 0;
            lastPosition = transform.position;
            SeedHandLocomotionSamples(false);
            SeedHandLocomotionSamples(true);
        }

        public void Warp(Vector3 pos, Quaternion rotation) {
          // Align the tracked head's heading, not just the rig's forward axis.
          transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
          Vector3 headForward = Vector3.ProjectOnPlane(headCollider.transform.forward, Vector3.up);
          float currentYaw = headForward.sqrMagnitude > 0.0001f
              ? Quaternion.LookRotation(headForward, Vector3.up).eulerAngles.y
              : headCollider.transform.eulerAngles.y;
          float yawDelta = Mathf.DeltaAngle(currentYaw, rotation.eulerAngles.y);
          transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + yawDelta, 0f);
          rb.rotation = transform.rotation;
          Warp(pos);
        }

        public void Warp(Vector3 pos) {
          if (HelicopterManager.instance != null) HelicopterManager.instance.ReleasePassenger();
          // print("Warp: " + pos);
          transform.position = pos;
          rb.position = pos;
          rb.linearVelocity = Vector3.zero;
          currentVelocity = Vector3.zero;
          InitializeValues();
          // Start the body collision sweep at the destination, not before the teleport.
          lastBodyMidpoint = bodyMidpoint.position;
          if (delayBounceCoroutine != null) {
            StopCoroutine(delayBounceCoroutine);
          }
          lastPos = transform.position;
          OnWarp?.Invoke();
        }

        public void ApplyPassengerDisplacement(Vector3 delta) {
          Vector3 position = transform.position + delta;
          transform.position = position;
          rb.position = position;
          lastHeadPosition += delta;
          lastBodyMidpoint += delta;
          lastLeftHandPosition += delta;
          lastRightHandPosition += delta;
          lastPosition += delta;
          lastPos += delta;
          for (int i = 0; i < lastLeftLocoCount; i++) lastLeftLocoSamples[i] += delta;
          for (int i = 0; i < lastRightLocoCount; i++) lastRightLocoSamples[i] += delta;
          // Preserve real player velocity, contact state and jump history. Warp()
          // resets those and allocates a new history, so it is not a carry operation.
        }

        public Vector3 PassengerFoot => bodyCollider.transform.TransformPoint(bodyCollider.center)
            - Vector3.up * (bodyCollider.height * 0.5f * Mathf.Abs(bodyCollider.transform.lossyScale.y));
        public float PassengerRadius => bodyCollider.radius
            * Mathf.Max(Mathf.Abs(bodyCollider.transform.lossyScale.x), Mathf.Abs(bodyCollider.transform.lossyScale.z));

        Collider leftPassengerSupport, rightPassengerSupport;
        void CapturePassengerHandSupport(bool rightHand, RaycastHit hit) {
          if (HelicopterManager.instance == null || !HelicopterManager.instance.IsSupportCollider(hit.collider)) return;
          if (rightHand) rightPassengerSupport = hit.collider;
          else leftPassengerSupport = hit.collider;
        }

        private Vector3 CurrentLeftHandPosition()
        {
            if (TryGetHandLocomotionShape(false, currentLeftLocoSamples, out int count, out _) && count > 0)
                return AverageSamples(currentLeftLocoSamples, count);

            if ((PositionWithOffset(leftHandTransform, leftHandOffset) - headCollider.transform.position).magnitude < maxArmLength)
            {
                return PositionWithOffset(leftHandTransform, leftHandOffset);
            }
            else
            {
                return headCollider.transform.position + (PositionWithOffset(leftHandTransform, leftHandOffset) - headCollider.transform.position).normalized * maxArmLength;
            }
        }

        private Vector3 CurrentRightHandPosition()
        {
            if (TryGetHandLocomotionShape(true, currentRightLocoSamples, out int count, out _) && count > 0)
                return AverageSamples(currentRightLocoSamples, count);

            if ((PositionWithOffset(rightHandTransform, rightHandOffset) - headCollider.transform.position).magnitude < maxArmLength)
            {
                return PositionWithOffset(rightHandTransform, rightHandOffset);
            }
            else
            {
                return headCollider.transform.position + (PositionWithOffset(rightHandTransform, rightHandOffset) - headCollider.transform.position).normalized * maxArmLength;
            }
        }

        bool TryGetHandLocomotionShape(bool rightHand, Vector3[] buffer, out int count, out float radius)
        {
            count = 0;
            radius = 0f;
            if (Player.instance == null) return false;
            PlayerHand hand = rightHand ? Player.instance.rightPlayerHand : Player.instance.leftPlayerHand;
            return hand != null && hand.TryGetLocomotionShape(buffer, out count, out radius);
        }

        static Vector3 AverageSamples(Vector3[] samples, int count)
        {
            if (count <= 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < count; i++)
                sum += samples[i];
            return sum / count;
        }

        void SeedHandLocomotionSamples(bool rightHand)
        {
            Vector3[] current = rightHand ? currentRightLocoSamples : currentLeftLocoSamples;
            Vector3[] last = rightHand ? lastRightLocoSamples : lastLeftLocoSamples;
            if (TryGetHandLocomotionShape(rightHand, current, out int count, out _) && count > 0)
            {
                for (int i = 0; i < count; i++)
                    last[i] = current[i];
                if (rightHand)
                {
                    lastRightLocoCount = count;
                    lastRightHandPosition = AverageSamples(last, count);
                }
                else
                {
                    lastLeftLocoCount = count;
                    lastLeftHandPosition = AverageSamples(last, count);
                }
            }
            else
            {
                if (rightHand)
                {
                    lastRightLocoCount = 0;
                    lastRightHandPosition = CurrentRightHandPosition();
                }
                else
                {
                    lastLeftLocoCount = 0;
                    lastLeftHandPosition = CurrentLeftHandPosition();
                }
            }
        }

        /// <summary>Call when a locomotion grabbable is first grabbed so the shape does not yank the body.</summary>
        public void SnapHandLocomotionAnchor(bool rightHand)
        {
            SeedHandLocomotionSamples(rightHand);
        }

        private Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector)
        {
            return transformToModify.position + transformToModify.rotation * offsetVector;
        }

        private Vector3 PositionWithOffset(Vector3 posToModify, Quaternion rot, Vector3 offsetVector)
        {
            return posToModify + rot * offsetVector;
        }

        private void Rotate()
        {
          Vector2 secondaryAxis = controllerInput.rightThumbstickValue;
          // 0 accepts any deflection. A full slider (threshold 1) needs the stick all the way over.
          bool stickRight = rotateDeadzone <= 0f ? secondaryAxis.x > 0f : secondaryAxis.x >= rotateDeadzone;
          bool stickLeft = rotateDeadzone <= 0f ? secondaryAxis.x < 0f : secondaryAxis.x <= -rotateDeadzone;

          if(rotateMode == 0) { //snap turn
            if (justPressedSnapRotate) {
              if (!stickRight && !stickLeft) {
                justPressedSnapRotate = false;
              }
            } else {
              if(stickRight) {
                Turn(snapRotateAmount);
                justPressedSnapRotate = true;
              } else if(stickLeft) {
                Turn(-snapRotateAmount);
                justPressedSnapRotate = true;
              }
            }

            if(Input.GetKeyDown(KeyCode.Q)) {
              Turn(-snapRotateAmount);
            }
            if(Input.GetKeyDown(KeyCode.E)) {
              Turn(snapRotateAmount);
            }
          } else if(rotateMode == 1) { //smooth turn
              // 0 stays still. 100 is the old 420 deg/s maximum. 200 is twice that.
              if(smoothRotateAmount > 0 && (stickRight || stickLeft)) {
                float smoothSensitivity = smoothRotateAmount * 4.2f;
                var rotateInfluence = Time.deltaTime * smoothSensitivity;
                // float rotateFloor = MathUtils.MapRange(rotateDeadzone, 0f, 0.7f, 0f, 0.5f);
                // var stickVal = (GameManager.instance.fullStrengthRotation) ? 1 : MathUtils.MapRange(Mathf.Abs(secondaryAxis.x), rotateDeadzone, 1f, rotateFloor, 1f);

                // euler.y += stickVal * Mathf.Sign(secondaryAxis.x) * rotateInfluence;
                Turn(Mathf.Sign(secondaryAxis.x) * rotateInfluence);
              }




          }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (controllerInput.rightThumbstickButton.wasPressed) {
            stickMove = !stickMove;
            rotateMode = 0;
        }
#endif

    }

    public bool IsGrounded() {
          if(groundedCount > 0) {
            return true;
          }
          if(wasRightHandTouching || wasLeftHandTouching || wasHeadTouching) {
            return true;
          }
            return false;
        }

        public bool IsRopeClimbing() {
          return ropeGrabRightHand || ropeGrabLeftHand;
        }

        public void ClearGliderLocomotion() {
          glidering = false;
        }

        public bool justPressedSnapRotate;

        const float deadGravityScale = 0.8f;

        void Update() {
          Rotate();
          // Locomotion samples tracked poses once per rendered frame, not per physics step.
          float dTime = Time.deltaTime;
          // if (Player.instance != null && Player.instance.IsDead) {
          //   // do our own custom gravity value for when dead
          //   rb.useGravity = false;
          //   rb.AddForce((deadGravityScale * rb.mass) * Physics.gravity);
          // } else {
          //   rb.useGravity = true;
          // }

          // LaunchPad-specific launches may temporarily disable gorilla locomotion so
          // hands touching the ground can't immediately damp/override the launch.
          if(!disableMovement && dTime > 0f) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!stickMove) {
              Movement(dTime);
            } else {
              StoreVelocities(dTime);
            }
            StickMove(dTime);
#else
            Movement(dTime);
#endif
          }

          if(groundedCount > 0) groundedCount--;
          Vector3 diff = transform.position - lastPos;
          float yDiff = diff.y;
          diff.y = 0f;
          travelDist += (diff).magnitude;
          if(travelDist > 1f) {
            travelDist -= 1f;
            // AchievementManager.instance.AddStat(AchievementProgressSource.TravelDistance, 1);
          }
          if(!IsGrounded()) {
            if(yDiff > 0) {
              jumpDist += yDiff;
              if(jumpDist > 1f) {
                jumpDist -= 1f;
                // AchievementManager.instance.AddStat(AchievementProgressSource.JumpDistance, 1);
              }
            } else if (yDiff < 0) {
              fallDist -= yDiff;
              if(fallDist > 1f) {
                fallDist -= 1f;
                // AchievementManager.instance.AddStat(AchievementProgressSource.FallDistance, 1);
              }
            }
          }
          lastPos = transform.position;
          if (HelicopterManager.instance != null) HelicopterManager.instance.RefreshPassenger();
        }

        Vector3 lastPos = Vector3.zero;
        float travelDist = 0f;
        float fallDist = 0f;
        float jumpDist = 0f;

        // void LateUpdate() {
        //   leftHandFollower.position = lastLeftHandPosition;
        //   rightHandFollower.position = lastRightHandPosition;
        // }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void StickMove(float dTime) {
#if UNITY_EDITOR
              Vector2 wasd = Vector2.zero;
              if(Input.GetKey(KeyCode.W)) wasd.y += 1f;
              if(Input.GetKey(KeyCode.A)) wasd.x -= 1f;
              if(Input.GetKey(KeyCode.S)) wasd.y -= 1f;
              if(Input.GetKey(KeyCode.D)) wasd.x += 1f;
              float jetpack = 0f;
              if(Input.GetKey(KeyCode.Space)) jetpack = 1f;
              MoveFromStick(wasd, jetpack, dTime);
#endif
          if(stickMove) {
            Vector2 moveAxis = controllerInput.leftThumbstickValue;
            Vector2 jetpackAxis = controllerInput.rightThumbstickValue;
            bool doMove = false;
            Vector2 moveInput = Vector2.zero;
            if(moveAxis.magnitude > moveDeadzone) {
              doMove = true;
              moveInput = moveAxis;
            }

            float jetpackVal = 0f;
            if(jetpackAxis.magnitude > moveDeadzone) {
              doMove = true;
              jetpackVal = jetpackAxis.y;
            }

            Vector3 diff = headCollider.transform.position - transform.position;
            diff.y = 0f;
            trackingSpace.position -= diff;
            rb.position += diff;

            float headEyeOffsetY = headCollider.transform.position.y - headEyeOffset.position.y;
            Vector3 newBodyPos = headCollider.transform.position + (BodyForwardRot() * headBodyOffset);
            newBodyPos.y += headEyeOffsetY;
            bodyCollider.transform.position = newBodyPos;


            if(doMove) {
              MoveFromStick(moveInput, jetpackVal, dTime);
            }

            // leftHandFollower.SetPositionAndRotation(CurrentLeftHandPosition(), leftHandTransform.rotation);
            // rightHandFollower.SetPositionAndRotation(CurrentRightHandPosition(), rightHandTransform.rotation);

          }
        }

        void MoveFromStick(Vector2 leftStick, float throttle, float dTime) {
          float wasdSpeed = 4.5f;
          float jetpackSpeed = 3f;
          float shiftSpeedMult = 2.5f;
          Vector3 moveForward = headCollider.transform.forward;
          moveForward.y = 0;
          moveForward = moveForward.normalized;
          Vector3 moveRight = Vector3.Cross(Vector3.up, moveForward);
          Vector3 newMove = (moveForward * leftStick.y) + (moveRight * leftStick.x);
          Vector3 jetpackMove = Vector3.zero;

          newMove = (newMove.normalized * wasdSpeed) * dTime;
          if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) newMove *= shiftSpeedMult;
          bool reinit = false;
          if(newMove.magnitude > 0f) {
            rb.position = transform.position + newMove;
            reinit = true;
          }
          if(throttle > 0f) {
            // jetpackMove = Vector3.up * jetpackSpeed * throttle;
            Vector3 tempVel = rb.linearVelocity;
            tempVel.y = jetpackSpeed * throttle;
            rb.linearVelocity = tempVel;
            reinit = true;
          }

          if(reinit) {
            InitializeValues();
          }
        }

#endif

        Quaternion BodyForwardRot() {
          float forwardToUp = headCollider.transform.up.y - headCollider.transform.forward.y;
      		if (forwardToUp == 0) {
      			forwardToUp = 0.000000001f + headCollider.transform.forward.y * -2f;
      		}

      		float ratioOfFlatFromForwardToUp = -headCollider.transform.forward.y / forwardToUp;
      		Vector3 forward = Vector3.LerpUnclamped(headCollider.transform.forward, headCollider.transform.up, ratioOfFlatFromForwardToUp);
      		// print("y: " + head.transform.forward.y + " forwardToUp: " + forwardToUp + " ratio: " + ratioOfFlatFromForwardToUp + " midY: " + forward.y);
      		if (forwardToUp < 0f) {
      			forward = -forward;
      		}
          // Debug.DrawLine(bodyCollider.transform.position, bodyCollider.transform.position + forward, Color.blue);
          return Quaternion.LookRotation(forward);
        }

        const float SimulationRate = 60f;
        public Vector3 hookVel;
        public Vector3 hookClimbVel;
        float hookDecelRate = 0.003f;
        public bool isHookPulling;

        public Vector3 jetVel;
        public bool jetting;

        public Vector3 wingVel;
        public bool gliding;

        /// <summary>True while GliderRelic applies additive glide impulses in Movement.</summary>
        public bool glidering;

        public bool leftHandColliding = false;
        public bool rightHandColliding = false;

      
        Vector3 SumAdditiveWeaponVelocity(float dTime, float simTime)
        {
          return Vector3.zero;
          /*
            if (Player.instance == null)
            {
                return Vector3.zero;
            }
            Vector3 sum = Vector3.zero;
            Weapon wr = Player.instance.weaponRight;
            Weapon wl = Player.instance.weaponLeft;
            if (wr != null)
            {
                sum += wr.GetAdditiveVelocityDelta(dTime, simTime);
            }
            if (wl != null)
            {
                sum += wl.GetAdditiveVelocityDelta(dTime, simTime);
            }
            return sum;
            */
        }

        Vector3 lastBodyMidpoint = Vector3.zero;
        void Movement(float dTime) {
            leftPassengerSupport = rightPassengerSupport = null;
            // Held items are parented to the duck hands. Pose them from the current anchors before the spherecasts.
            if (_bodyDriver != null)
                _bodyDriver.PoseNow();

            float simTime = SimulationRate * dTime; //NOTE(ken): how much to simulate compared to fixed frame rate timing (i.e. 60fps), so simTime = 1 means the deltaTime was exactly 1/60th of a second
            leftHandColliding = false;
            rightHandColliding = false;
            bool headColliding = false;
            Vector3 finalPosition;
            Vector3 rigidBodyMovement = Vector3.zero;

            // UpdateRopeOffHandCollisionIgnore();
            Vector3 firstIterationLeftHand = Vector3.zero;
            Vector3 firstIterationRightHand = Vector3.zero;
            RaycastHit hitInfo;

            // bodyCollider.transform.eulerAngles = new Vector3(0, headCollider.transform.eulerAngles.y, 0);
            // bodyCollider.transform.position = headCollider.transform.position + (BodyForwardRot() * headBodyOffset);

            // if(isHookPulling) {
            //   rb.position = rb.position + (hookVel * simTime);
            //   // rb.velocity = hookVel;
            // }
            //
            // if(jetting) {
            //   rb.position = rb.position + (jetVel * simTime);
            // }


            //adjusting body height so height above ground doesn't change when looking around
            float headEyeOffsetY = headCollider.transform.position.y - headEyeOffset.position.y;
            Vector3 newBodyPos = headCollider.transform.position + (BodyForwardRot() * headBodyOffset);
            newBodyPos.y += headEyeOffsetY;
            bodyCollider.transform.position = newBodyPos;

            //moving player so head is at origin so rotating is always around the head
            Vector3 diff = headCollider.transform.position - transform.position;
        		diff.y = 0f;
            trackingSpace.position -= diff;
        		rb.position += diff;
        		// trackingSpace.position = new Vector3(trackingSpace.position.x, transform.position.y + duckOffset.y, trackingSpace.position.z);
            //left hand

            float mag = hookVel.magnitude;
            float decelForFrame = hookDecelRate * simTime;
            // print(mag.ToString("F3") + " : " + decelForFrame.ToString("F3"));
            if(mag <= decelForFrame) {
              hookVel = Vector3.zero;
            } else {
              hookVel -= (hookVel.normalized * decelForFrame);
            }

            // if(isHookPulling || jetting || glidering) {
            //   Vector3 vel = hookVel + jetVel + wingVel;
            //     if (!rb.SweepTest(vel, out RaycastHit hit, vel.magnitude)) {
            //       rb.MovePosition(rb.position + vel);
            //     } else {
            //       rb.MovePosition(rb.position + vel * 0.1f);
            //       print("**hit: " + hit.transform.name);
            //       Debug.DrawLine(hit.point, hit.point + hit.normal, Color.red, 10f);
            //     }
            // }

            rb.MovePosition(rb.position + hookVel + jetVel + wingVel);
            Vector3 bodyDiff = bodyMidpoint.position - lastBodyMidpoint;
            // print("bheight: " + bodyCollider.height + " : "+ bodyCollider.radius + " : " + bodyHalfHeight);
            if(Physics.Raycast(lastBodyMidpoint, bodyDiff, out RaycastHit hit, bodyDiff.magnitude, envMask, QueryTriggerInteraction.Ignore)) {
              // Debug.LogError("tunneled: ");
              // GRUtility.PrintHierarchyOfGameObject(hit.transform.gameObject);
              // Debug.DrawLine(lastBodyMidpoint, bodyMidpoint.position, Color.red, 5f);
              // Debug.DrawLine(bodyMidpoint.position, hit.point + (hit.normal * (bodyHalfHeight + 0.05f)), Color.green, 5f);
              // rb.position += hit.point + (hit.normal * (bodyCollider.radius + 0.05f)) - bodyMidpoint.position;
              rb.position = hit.point + (hit.normal * (bodyHalfHeight + 0.05f));
            }

            //TODO(Ken): enable check so head can't go through ground, similar to how body is done
            // Vector3 headPos = headCollider.transform.position;
            // Vector3 headBodyDiff = lastBodyMidpoint - headPos;
            // if(Physics.Raycast(headPos, headBodyDiff, out RaycastHit hit2, headBodyDiff.magnitude, envLayer, QueryTriggerInteraction.Ignore)) {
            //   rb.position += hit2.point + (hit2.normal * (bodyCollider.height * 1.2f)) - bodyMidpoint.position;
            // }
            // rb.position += hookVel + jetVel + wingVel;

            Vector3 gravityStep = Vector3.down * 2f * 9.8f * dTime * dTime;
            bool allowLeftHandCollision = AllowHandLocomotionCollision(false);
            if (allowLeftHandCollision &&
                TryHandLocomotionFirstPass(false, gravityStep, dTime, wasLeftHandTouching, leftHandVelHistory, ref leftHandNotCollidingTime,
                    out firstIterationLeftHand, out leftHandColliding))
            {
                if (!glidering)
                    rb.linearVelocity = Vector3.zero;
            }

            //right hand
            bool allowRightHandCollision = AllowHandLocomotionCollision(true);
            if (allowRightHandCollision &&
                TryHandLocomotionFirstPass(true, gravityStep, dTime, wasRightHandTouching, rightHandVelHistory, ref rightHandNotCollidingTime,
                    out firstIterationRightHand, out rightHandColliding))
            {
                if (!glidering)
                    rb.linearVelocity = Vector3.zero;
            }

            //average or add

            if ((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))
            {
                //this lets you grab stuff with both hands at the same time
                rigidBodyMovement = (firstIterationLeftHand + firstIterationRightHand) / 2;
            }
            else
            {
                rigidBodyMovement = firstIterationLeftHand + firstIterationRightHand;
            }

            //check valid head movement

            if (IterativeCollisionSphereCast(lastHeadPosition, headCollider.radius, headCollider.transform.position + rigidBodyMovement - lastHeadPosition, defaultPrecision, out finalPosition, false))
            {
                rigidBodyMovement = finalPosition - lastHeadPosition;
                //last check to make sure the head won't phase through geometry
                if (Physics.Raycast(lastHeadPosition, headCollider.transform.position - lastHeadPosition + rigidBodyMovement, out hitInfo, (headCollider.transform.position - lastHeadPosition + rigidBodyMovement).magnitude + headCollider.radius * defaultPrecision * 0.999f, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
                {
                    rigidBodyMovement = lastHeadPosition - headCollider.transform.position;
                }
                headColliding = true;
            }

            if (rigidBodyMovement != Vector3.zero && !isHookPulling && !jetting && !glidering)
            {
                transform.position = transform.position + rigidBodyMovement;
            }

            lastHeadPosition = headCollider.transform.position;

            //do final left / right hand (or loco-shape) positions
            bool bothHandsTouching = (leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching);
            if (AllowHandLocomotionCollision(false))
                HandLocomotionSecondPass(false, bothHandsTouching, ref leftHandColliding);
            else
                lastLeftHandPosition = CurrentLeftHandPosition();

            if (AllowHandLocomotionCollision(true))
                HandLocomotionSecondPass(true, bothHandsTouching, ref rightHandColliding);
            else
                lastRightHandPosition = CurrentRightHandPosition();

            StoreVelocities(dTime);

            if(isHookPulling) {
              rb.linearVelocity = hookVel * simTime;
            } else {
              rb.linearVelocity += SumAdditiveWeaponVelocity(dTime, simTime);
            }

            if (!glidering) {
              // if(isZipping) {
              //   // Handle zip line movement
              //   HandleZipLineMovement();

              //   if((zippingHand == Player.instance.rightPlayerHand && leftHandColliding) ||
              //     (zippingHand == Player.instance.leftPlayerHand && rightHandColliding)){
              //       ForceReleaseZipLine();
              //   }
                
              // } else 
              if(ropeGrabRightHand) {
                // rb.position += (ropeGrabPos - rightHandTransform.position);
                // Rope/map destroyed or disabled (e.g. community island hide) while still grabbing
                if(currHandAnchor == null || !currHandAnchor.gameObject.activeInHierarchy){
                  LetGoRope(true);
                } else {
                  float dist = Vector3.Distance(currHandAnchor.position, rightHandTransform.position);
                  if(dist > 2f){
                    LetGoRope(true);
                  }
                  else{
                    rb.position += currHandAnchor.position - rightHandTransform.position;
                    rb.linearVelocity = Vector3.zero;
                  }
                }
              } else if(ropeGrabLeftHand) {
                // rb.position += (ropeGrabPos - leftHandTransform.position);
                
                if(currHandAnchor == null || !currHandAnchor.gameObject.activeInHierarchy){
                  LetGoRope(false);
                } else {
                  float dist = Vector3.Distance(currHandAnchor.position, leftHandTransform.position);
                  if(dist > 2f){
                    LetGoRope(false);
                  }
                  else{
                    rb.position += currHandAnchor.position - leftHandTransform.position;
                    rb.linearVelocity = Vector3.zero;
                  }
                }
              } else {
                if ((rightHandColliding || leftHandColliding) && !disableMovement)
                {
                  SetVelToAverageVel();
                }
              }
            }

            //check to see if left / right hand is stuck and we should unstick it
            if (AllowHandLocomotionCollision(false))
                TryUnstickHand(false, ref leftHandColliding);
            if (AllowHandLocomotionCollision(true))
                TryUnstickHand(true, ref rightHandColliding);

            // leftHandFollower.position = lastLeftHandPosition;
            // rightHandFollower.position = lastRightHandPosition;

            wasLeftHandTouching = leftHandColliding;
            wasRightHandTouching = rightHandColliding;
            wasHeadTouching = headColliding;

            if (HelicopterManager.instance != null) {
                bool handSupportAllowed = !glidering && !isHookPulling && !jetting && !disableMovement;
                HelicopterManager.instance.ReportHandSupport(transform,
                    handSupportAllowed && leftHandColliding ? leftPassengerSupport : null,
                    handSupportAllowed && rightHandColliding ? rightPassengerSupport : null);
            }

            lastBodyMidpoint = bodyMidpoint.position;

        }

        void SetLastLocoCount(bool rightHand, int count)
        {
            if (rightHand) lastRightLocoCount = count;
            else lastLeftLocoCount = count;
        }

        int GetLastLocoCount(bool rightHand) => rightHand ? lastRightLocoCount : lastLeftLocoCount;

        bool TryHandLocomotionFirstPass(
            bool rightHand,
            Vector3 gravityStep,
            float dTime,
            bool wasTouching,
            Queue<Vector3> velHistory,
            ref float notCollidingTime,
            out Vector3 firstIteration,
            out bool colliding)
        {
            firstIteration = Vector3.zero;
            colliding = false;

            Vector3[] current = rightHand ? currentRightLocoSamples : currentLeftLocoSamples;
            Vector3[] last = rightHand ? lastRightLocoSamples : lastLeftLocoSamples;

            bool shape = TryGetHandLocomotionShape(rightHand, current, out int count, out float radius);
            if (shape && count > 0)
            {
                if (GetLastLocoCount(rightHand) != count)
                {
                    for (int i = 0; i < count; i++)
                        last[i] = current[i];
                    SetLastLocoCount(rightHand, count);
                }

                Vector3 avgCurrent = AverageSamples(current, count);
                Vector3 avgLast = AverageSamples(last, count);
                RecordHandVelocity(velHistory, (avgCurrent - avgLast) / dTime);

                Vector3 bodySum = Vector3.zero;
                int hitCount = 0;
                RaycastHit reportHit = default;
                Vector3 reportFinal = Vector3.zero;

                for (int i = 0; i < count; i++)
                {
                    Vector3 distanceTraveled = current[i] - last[i] + gravityStep;
                    if (!IterativeCollisionSphereCast(last[i], radius, distanceTraveled, defaultPrecision, out Vector3 finalPosition, true, out RaycastHit hit))
                        continue;

                    bodySum += wasTouching ? last[i] - current[i] : finalPosition - current[i];
                    CapturePassengerHandSupport(rightHand, hit);
                    if (hitCount == 0)
                    {
                        reportHit = hit;
                        reportFinal = finalPosition;
                    }
                    hitCount++;
                }

                if (hitCount > 0)
                {
                    firstIteration = bodySum / hitCount;
                    colliding = true;
                    if (notCollidingTime > 0.1f)
                    {
                        PlayerHand hand = rightHand ? Player.instance.rightPlayerHand : Player.instance.leftPlayerHand;
                        PlayHandHitHaptic(hand, velHistory);
                        float vol = HandVelocityToVolume(velHistory);
                        PlayHandHit(reportHit.collider, reportFinal, vol);
                    }
                    notCollidingTime = 0f;
                    return true;
                }

                notCollidingTime += dTime;
                return false;
            }

            SetLastLocoCount(rightHand, 0);
            Vector3 handCurrent = rightHand ? CurrentRightHandPosition() : CurrentLeftHandPosition();
            Vector3 handLast = rightHand ? lastRightHandPosition : lastLeftHandPosition;
            float handRadius = minimumRaycastDistance;
            Vector3 distance = handCurrent - handLast + gravityStep;
            RecordHandVelocity(velHistory, (handCurrent - handLast) / dTime);

            if (IterativeCollisionSphereCast(handLast, handRadius, distance, defaultPrecision, out Vector3 endPosition, true, out RaycastHit handHit))
            {
                CapturePassengerHandSupport(rightHand, handHit);
                firstIteration = wasTouching ? handLast - handCurrent : endPosition - handCurrent;
                colliding = true;
                if (notCollidingTime > 0.1f)
                {
                    PlayerHand hand = rightHand ? Player.instance.rightPlayerHand : Player.instance.leftPlayerHand;
                    PlayHandHitHaptic(hand, velHistory);
                    float vol = HandVelocityToVolume(velHistory);
                    PlayHandHit(handHit.collider, endPosition, vol);
                }
                notCollidingTime = 0f;
                return true;
            }

            notCollidingTime += dTime;
            return false;
        }

        void HandLocomotionSecondPass(bool rightHand, bool bothHandsTouching, ref bool colliding)
        {
            Vector3[] current = rightHand ? currentRightLocoSamples : currentLeftLocoSamples;
            Vector3[] last = rightHand ? lastRightLocoSamples : lastLeftLocoSamples;
            bool singleHand = !bothHandsTouching;

            if (TryGetHandLocomotionShape(rightHand, current, out int count, out float radius) && count > 0)
            {
                if (GetLastLocoCount(rightHand) != count)
                {
                    for (int i = 0; i < count; i++)
                        last[i] = current[i];
                    SetLastLocoCount(rightHand, count);
                }

                bool anyHit = false;
                for (int i = 0; i < count; i++)
                {
                    Vector3 distanceTraveled = current[i] - last[i];
                    if (IterativeCollisionSphereCast(last[i], radius, distanceTraveled, defaultPrecision, out Vector3 finalPosition, singleHand, out RaycastHit supportHit))
                    {
                        CapturePassengerHandSupport(rightHand, supportHit);
                        last[i] = finalPosition;
                        anyHit = true;
                    }
                    else
                    {
                        last[i] = current[i];
                    }
                }

                if (anyHit)
                    colliding = true;

                Vector3 avg = AverageSamples(last, count);
                if (rightHand)
                    lastRightHandPosition = avg;
                else
                    lastLeftHandPosition = avg;
                return;
            }

            SetLastLocoCount(rightHand, 0);
            Vector3 handCurrent = rightHand ? CurrentRightHandPosition() : CurrentLeftHandPosition();
            Vector3 handLast = rightHand ? lastRightHandPosition : lastLeftHandPosition;
            Vector3 distanceTraveledHand = handCurrent - handLast;

            if (IterativeCollisionSphereCast(handLast, minimumRaycastDistance, distanceTraveledHand, defaultPrecision, out Vector3 finalHand, singleHand, out RaycastHit handSupportHit))
            {
                CapturePassengerHandSupport(rightHand, handSupportHit);
                if (rightHand)
                    lastRightHandPosition = finalHand;
                else
                    lastLeftHandPosition = finalHand;
                colliding = true;
            }
            else if (rightHand)
            {
                lastRightHandPosition = handCurrent;
            }
            else
            {
                lastLeftHandPosition = handCurrent;
            }
        }

        void TryUnstickHand(bool rightHand, ref bool colliding)
        {
            if (!colliding) return;

            Vector3[] current = rightHand ? currentRightLocoSamples : currentLeftLocoSamples;
            Vector3[] last = rightHand ? lastRightLocoSamples : lastLeftLocoSamples;
            int storedCount = GetLastLocoCount(rightHand);

            Vector3 currentPos;
            Vector3 lastPos;
            float radius;

            if (TryGetHandLocomotionShape(rightHand, current, out int count, out radius) && count > 0)
            {
                currentPos = AverageSamples(current, count);
                lastPos = storedCount > 0 ? AverageSamples(last, storedCount) : currentPos;
            }
            else
            {
                currentPos = rightHand ? CurrentRightHandPosition() : CurrentLeftHandPosition();
                lastPos = rightHand ? lastRightHandPosition : lastLeftHandPosition;
                radius = minimumRaycastDistance;
            }

            if ((currentPos - lastPos).magnitude <= unStickDistance)
                return;

            Vector3 toHand = currentPos - headCollider.transform.position;
            float castDist = toHand.magnitude - radius;
            if (castDist > 0f &&
                Physics.SphereCast(headCollider.transform.position, radius * defaultPrecision, toHand, out _, castDist, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
                return;

            SeedHandLocomotionSamples(rightHand);
            colliding = false;
        }

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 endPosition, bool singleHand)
        {
            return IterativeCollisionSphereCast(startPosition, sphereRadius, movementVector, precision, out endPosition, singleHand, out _);
        }

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 endPosition, bool singleHand, out RaycastHit firstHit)
        {
            firstHit = default;
            RaycastHit hitInfo;
            Vector3 movementToProjectedAboveCollisionPlane;
            Surface gorillaSurface;
            float slipPercentage;
            //first spherecast from the starting position to the final position
            if (CollisionsSphereCast(startPosition, sphereRadius * precision, movementVector, precision, out endPosition, out hitInfo))
            {
                firstHit = hitInfo;
                //if we hit a surface, do a bit of a slide. this makes it so if you grab with two hands you don't stick 100%, and if you're pushing along a surface while braced with your head, your hand will slide a bit

                //take the surface normal that we hit, then along that plane, do a spherecast to a position a small distance away to account for moving perpendicular to that surface
                Vector3 firstPosition = endPosition;
                gorillaSurface = hitInfo.collider.GetComponent<Surface>();
                slipPercentage = gorillaSurface != null ? gorillaSurface.slipPercentage : (!singleHand ? defaultSlideFactor : 0.001f);
                movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, hitInfo.normal) * slipPercentage;
                if (CollisionsSphereCast(endPosition, sphereRadius, movementToProjectedAboveCollisionPlane, precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit trying to move perpendicularly, stop there and our end position is the final spot we hit
                    return true;
                }
                //if not, try to move closer towards the true point to account for the fact that the movement along the normal of the hit could have moved you away from the surface
                else if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), precision * precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit, then return the spot we hit
                    return true;
                }
                else
                {
                    //this shouldn't really happe, since this means that the sliding motion got you around some corner or something and let you get to your final point. back off because something strange happened, so just don't do the slide
                    endPosition = firstPosition;
                    return true;
                }
            }
            //as kind of a sanity check, try a smaller spherecast. this accounts for times when the original spherecast was already touching a surface so it didn't trigger correctly
            else if (CollisionsSphereCast(startPosition, sphereRadius * precision * 0.66f, movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f), precision * 0.66f, out endPosition, out hitInfo))
            {
                firstHit = hitInfo;
                endPosition = startPosition;
                return true;
            } else
            {
                endPosition = Vector3.zero;
                return false;
            }
        }

        private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 finalPosition, out RaycastHit hitInfo)
        {
            //kind of like a souped up spherecast. includes checks to make sure that the sphere we're using, if it touches a surface, is pushed away the correct distance (the original sphereradius distance). since you might
            //be pushing into sharp corners, this might not always be valid, so that's what the extra checks are for

            //initial spherecase
            RaycastHit innerHit;
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * (1 - precision), locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                //if we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                //check a spherecase from the original position to the intended final position
                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision), locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized * Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                //bonus raycast check to make sure that something odd didn't happen with the spherecast. helps prevent clipping through geometry
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }
                return true;
            }
            //anti-clipping through geometry check
            else if (Physics.Raycast(startPosition, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * precision * 0.999f, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore))
            {
                finalPosition = startPosition;
                return true;
            }
            else
            {
                finalPosition = Vector3.zero;
                return false;
            }
        }

        public bool IsHandTouching(bool forLeftHand)
        {
            if (forLeftHand)
            {
                return wasLeftHandTouching;
            }
            else
            {
                return wasRightHandTouching;
            }
        }

        // bool turned;
        public void Turn(float degrees)
        {
          var euler = transform.rotation.eulerAngles;
          euler.y += degrees;
          rb.rotation = Quaternion.Euler(euler);
          Quaternion turn = Quaternion.Euler(0f, degrees, 0f);
          denormalizedVelocityAverage = turn * denormalizedVelocityAverage;

          for (int i = 0; i < velocityHistory.Length; i++)
          {
              velocityHistory[i] = turn * velocityHistory[i];
          }

          // turned = true;
        }

        private void StoreVelocities(float dTime)
        {
          if (dTime <= 0f) return;
          velocityIndex = (velocityIndex + 1) % velocityHistorySize;
          currentVelocity = (transform.position - lastPosition) / dTime;
          velocityHistory[velocityIndex] = currentVelocity;
          velocityHistoryDurations[velocityIndex] = dTime;

          Vector3 displacement = Vector3.zero;
          float duration = 0f;
          for (int i = 0; i < velocityHistory.Length; i++) {
            displacement += velocityHistory[i] * velocityHistoryDurations[i];
            duration += velocityHistoryDurations[i];
          }
          // Physics may advance zero or multiple steps in a render frame. Weight by
          // elapsed time so those frames do not bias the launch velocity.
          denormalizedVelocityAverage = displacement / duration;

          lastPosition = transform.position;
        }

        void RecordHandVelocity(Queue<Vector3> history, Vector3 vel)
        {
            history.Enqueue(vel);
            while (history.Count > HandVelocityHistorySize) history.Dequeue();
        }

        void PlayHandHit(Collider hitCollider, Vector3 position, float volume)
            => PlaySurfaceSound(handHitSounds, hitCollider, position, volume);

        static void PlaySurfaceSound(Game.Audio.SurfaceSoundSet set, Collider hitCollider, Vector3 position, float volume)
        {
            var sound = set != null ? set.Resolve(hitCollider) : null;
            if (sound == null || Game.Audio.AudioManager.instance == null) return;
            Game.Audio.AudioManager.instance.Play(sound, position, volume);
        }

        static float AverageHandSpeed(Queue<Vector3> history)
        {
            if (history.Count == 0) return 0f;
            Vector3 sum = Vector3.zero;
            foreach (var v in history) sum += v;
            return sum.magnitude / history.Count;
        }

        float HandVelocityToVolume(Queue<Vector3> history)
        {
            if (history.Count == 0) return handHitMinVolume;
            return Mathf.Clamp01(GRMathUtils.MapRangeAndClamp(AverageHandSpeed(history), handHitMinVel, handHitFullVolumeVel, handHitMinVolume, 1f));
        }

        void PlayHandHitHaptic(PlayerHand hand, Queue<Vector3> history)
        {
            if (VibrationManager.instance == null) return;
            float t = Mathf.InverseLerp(handHitMinVel, handHitFullVolumeVel, AverageHandSpeed(history));
            float amplitude = Mathf.Lerp(handHitHapticMinAmplitude, handHitHapticMaxAmplitude, t);
            float duration = Mathf.Lerp(handHitHapticMinDuration, handHitHapticMaxDuration, t);
            VibrationManager.instance.TriggerVibrationPulse(handHitHapticFrequency, amplitude, duration, hand);
        }

        public void LaunchPlayer(Vector3 vel, LaunchType type) {
          // print("LaunchPlayer");
          // launchVel = vel;
          // launchDuration = dur;
          // SetVelToAverageVel();

          if(type == LaunchType.OverwriteVerticalCombineHorizontal) {
            float velY = vel.y;
            vel += currentVelocity;
            vel.y = velY;
          } else if(type == LaunchType.CombineVelocity) {
            vel += currentVelocity;
          }

          // Debug.DrawLine(transform.position, transform.position + currentVelocity, Color.blue, 10f);
          // Debug.DrawLine(transform.position, transform.position + vel, Color.green, 10f);

          rb.linearVelocity = vel;
          currentVelocity = vel;
          SetVelHistoryToCurrentVel();
        }

        int disableMovementRequestCount = 0;

        /// <summary>
        /// Launch method used by LaunchPad triggers.
        /// Temporarily disables movement while we let the rigidbody velocity take effect,
        /// so a hand contacting the ground can't damp/override the launch right away.
        /// </summary>
        public void LaunchPlayerFromLaunchPad(Vector3 vel, LaunchType type, int fixedFramesToDisable = 1) {
          if (fixedFramesToDisable <= 0) {
            LaunchPlayer(vel, type);
            return;
          }

          // Disable locomotion immediately so a hand-ground contact can't override/damp the launch.
          disableMovementRequestCount++;
          disableMovement = true;

          LaunchPlayer(vel, type);

          StartCoroutine(DisableMovementForFixedFrames(fixedFramesToDisable));
        }

        System.Collections.IEnumerator DisableMovementForFixedFrames(int fixedFramesToDisable) {
          for (int i = 0; i < fixedFramesToDisable; i++) {
            yield return new WaitForFixedUpdate();
          }

          disableMovementRequestCount--;
          if (disableMovementRequestCount <= 0) {
            disableMovementRequestCount = 0;
            disableMovement = false;
          }
        }

        void SetVelHistoryToCurrentVel() {
          for(int i = 0; i < velocityHistory.Length; i++) {
            velocityHistory[i] = currentVelocity;
          }
          denormalizedVelocityAverage = currentVelocity;
        }

        public Vector3 GetCurrentVelocity() {
          return currentVelocity;
        }


	      Coroutine delayBounceCoroutine;
        int bounceBackCount = 0;
        public void BounceBack(bool bounceToCamp) {
          return;
          // if(bounceBackCount == 0) {
          //   if(bounceToCamp) {
          //     Vector3 vel = Vector3.up * 20f;
          //     rb.linearVelocity = vel;
          //     currentVelocity = vel;
          //     SetVelHistoryToCurrentVel();
          //     delayBounceCoroutine = StartCoroutine(BounceToReturnPointDelayed());
          //   } else {
          //     Vector3 vel = -rb.linearVelocity;
          //     // print(vel.magnitude);
          //     rb.linearVelocity = vel;
          //     currentVelocity = vel;
          //     SetVelHistoryToCurrentVel();
          //   }
          //     // MasterAudio.PlaySound3DAtVector3AndForget(bounceBackSound, transform.position);
          // } else if(bounceBackCount == 1) {
          //   float zDiff = ARPGEventManager.instance.bounceReturnPoint.position.z - transform.position.z;
          //   // print(zDiff);
          //   Vector3 vel = new Vector3(Mathf.Sign(transform.position.x) * 20f, 20f, zDiff / 5f);
          //   // Debug.DrawLine(transform.position, transform.position + vel, Color.red, 10f);
          //   rb.linearVelocity = vel;
          //   currentVelocity = vel;
          //   SetVelHistoryToCurrentVel();
          //   delayBounceCoroutine = StartCoroutine(BounceToReturnPointDelayed());
          //   MasterAudio.PlaySound3DAtVector3AndForget(bounceBackSound, transform.position);
          // } else {
          //   WarpToCamp();
          // }

          // bounceBackCount++;
        }

        public void WarpToCamp() {
          return;
          // ARPGEventManager.instance.TeleportToBridge();
          // PlayerHUD.instance.UpdateSubMessageDisplay("Warped back to camp", 4f, 10f);
          // MasterAudio.PlaySound3DAtVector3AndForget(warpSound, transform.position);
        }

        /// <summary>
        /// Trampoline-style bounce: keeps current horizontal velocity and sets upward speed.
        /// Used by LavaBounceTrigger (Mario 64-style lava).
        /// </summary>
        const float defaultTrampolineMinHorizontalSpeed = 2.5f;

        public void TrampolineBounce(float upwardSpeed) {
          TrampolineBounce(upwardSpeed, defaultTrampolineMinHorizontalSpeed);
        }

        public void TrampolineBounce(float upwardSpeed, float minHorizontalSpeed) {
          Vector3 vel = currentVelocity;
          vel.y = upwardSpeed;

          // If the player lands with almost no forward movement, a trampoline bounce can feel "stuck".
          // Clamp the minimum horizontal speed, but always bounce in the direction the player is looking.
          minHorizontalSpeed = Mathf.Max(0f, minHorizontalSpeed);

          Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
          float horizontalSpeed = horizontal.magnitude;
          horizontalSpeed = Mathf.Max(horizontalSpeed, minHorizontalSpeed);

          // Always use head/looking direction projected onto XZ.
          // (Head collider forward is where the player is "looking".)
          Vector3 lookDir = Vector3.zero;
          if(headCollider != null) {
            lookDir = headCollider.transform.forward;
          } else {
            lookDir = transform.forward;
          }
          lookDir.y = 0f;
          if(lookDir.sqrMagnitude < 0.0001f) lookDir = Vector3.forward;
          lookDir.Normalize();

          // If we have something close in front of where we are looking, bounce away
          // so the player doesn't keep "smashing" a wall when they land while aiming at it.
          Vector3 rayOrigin = headCollider != null ? headCollider.transform.position : (transform.position + Vector3.up * 0.5f);
          // Using a pure horizontal ray (dir is flattened) so "front" is based on horizontal look.
          bool obstacleInFront = Physics.Raycast(
            rayOrigin,
            lookDir,
            1.5f,
            1 << envLayer,
            QueryTriggerInteraction.Ignore
          );
          if(obstacleInFront) {
            lookDir *= -1f;
          }

          horizontal = lookDir * horizontalSpeed;
          vel.x = horizontal.x;
          vel.z = horizontal.z;

          rb.linearVelocity = vel;
          currentVelocity = vel;
          SetVelHistoryToCurrentVel();
        }

        IEnumerator BounceToReturnPointDelayed() {
          yield return new WaitForSeconds(1.5f);
          BounceToReturnPoint();
        }

        public void BounceToReturnPoint() {
          return;
          // Vector3 vel = LaunchVelToReachPoint(ARPGEventManager.instance.bounceReturnPoint.position, 20f);
          // rb.linearVelocity = vel;
          // currentVelocity = vel;
          // SetVelHistoryToCurrentVel();
          // MasterAudio.PlaySound3DAtVector3AndForget(bounceBackSound, transform.position);
        }

        public Vector3 LaunchVelToReachPoint(Vector3 end, float arcHeight) {
          Vector3 start = transform.position;

          float gravity = Mathf.Abs(Physics.gravity.y);
          Vector3 displacement = end - start;
          Vector3 displacementXZ = new Vector3(displacement.x, 0, displacement.z);

          float verticalDistance = displacement.y;
          float horizontalDistance = displacementXZ.magnitude;

          // Calculate the initial vertical velocity to reach the arc height above end
          float vy = Mathf.Sqrt(2 * gravity * (arcHeight + verticalDistance));

          // Time to reach max height
          float t_up = vy / gravity;
          // Time to go down from max height to end.y
          float t_down = Mathf.Sqrt(2 * arcHeight / gravity);
          float totalTime = t_up + t_down;

          // Initial horizontal velocity
          Vector3 velocityXZ = displacementXZ / totalTime;

          // Combine vertical and horizontal velocities
          Vector3 result = velocityXZ + Vector3.up * vy;
          return result;
        }

        Vector3 ropeGrabPos = Vector3.zero;
        bool ropeGrabRightHand;
        bool ropeGrabLeftHand;

        // Zip line variables
        /*
        bool isZipping;
        PlayerHand zippingHand;
        ZipLineSpline currentZipLine;
        float currentZipLineParameter;
        Vector3 initialZipLineDirection;
        Vector3 zipLineMoveVelocity;
        public float zipLineReleaseMultiplier = 0.55f;
        public float zipLineHandFlingMultiplier = 0.6f;
        public float zipLineBreakDistance = 0.45f;
        float zipLineHapticTimer;
        const float zipLineHapticInterval = 0.2f;
        ParticleSystem zipLineSparkPfx;
        Vector3 zipLineSparkLastWorldPos;
        PlaySoundResult zipLinePsr;
        float zipLineGrabTime;
        float zipLineGrabParameter; // Parameter when we grabbed (to track how far we've moved)
        const float zipLineCollisionIgnoreMinDistance = 2.0f; // Stop ignoring once we've moved 2m along the rope
        bool zipLineCollisionIgnored;
        const float ropeOffHandCollisionEnableDistance = 0.1f;
        bool ropeOffHandCollisionIgnored;
        Vector3 ropeOffHandLocalOffsetAtGrab;

        [FoldoutGroup("Sounds")][Header("Zip Line Audio")]
        [FoldoutGroup("Sounds")][SoundGroup] public string zipLineOnSound = "ZipLineOn";
        [FoldoutGroup("Sounds")][SoundGroup] public string zipLineOffSound = "ZipLineOff";
        [FoldoutGroup("Sounds")][SoundGroup] public string zipLineLoopSound = "ZipLineLoop";
        */

        // New zip line system variables
        Transform grabbingHand;               // which hand is on the rope
        Vector3 ropePointWorld;               // current hand attachment point on the rope/spline
        bool hasTriedSegmentTransition = false; // prevent repeated segment switching

        // End latch state
        enum EndLatch { None, Start, End }
        EndLatch endLatch = EndLatch.None;
        float startParamMin;   // set from your spline
        float endParamMax;     // set from your spline
        Vector3 latchedPos;    // cached end position
        Vector3 latchedTan;    // cached end tangent (normalized)

        // Zip line constraint tuning
        float handDeadzone = 0.005f;      // meters: ignore tiny controller jitter (5 mm)
        float endLatchEps = 0.1f;         // meters: how close to count as "at end" (10cm)
        float endReleaseBack = 0.03f;     // meters: pull-back distance to unlatch

        // Private buffer settings (not visible in inspector)
        float endBufferStart = 0.01f;     // parameter buffer at start (2% from beginning)
        float endBufferEnd = 0.01f;       // parameter buffer at end (2% from end)


    public void LetGoRope(bool isRightHand) {}
    bool AllowHandLocomotionCollision(bool b) {return true;}
/*
    public void GrabRope(Vector3 grabPos, bool isRightHand, Transform ropeTransform) {
      return;
        // print("GrabRope: " + isRightHand + " : " + grabPos.ToString("F3"));
        ClearGliderLocomotion();
        if (isRightHand) {
            if (ropeGrabLeftHand) {
                Player.instance.leftPlayerHand.LetGoRopeIfGrabbing();
            }
            ropeGrabRightHand = true;
        } else {
            if (ropeGrabRightHand) {
                Player.instance.rightPlayerHand.LetGoRopeIfGrabbing();
            }
            ropeGrabLeftHand = true;
        }

        ropeGrabPos = grabPos;
        bounceBackCount = 0;

        if (!string.IsNullOrEmpty(ropeGrabSound)) {
            MasterAudio.PlaySound3DAtVector3AndForget(ropeGrabSound, grabPos);
        }

        // Track bounty for climbing a rope
        BountyManager.instance.AddBountyTask("climbrope", 1);

        if (currHandAnchor == null) {
            var go = Instantiate(handAnchorPrefab);
            currHandAnchor = go.transform;
        }

        currHandAnchor.SetParent(ropeTransform);
        currHandAnchor.position = grabPos;
        if (currHandAnchor.TryGetComponent(out HandAnchor anchorScript)) {
            if (isRightHand) {
                anchorScript.onRelease += Player.instance.rightPlayerHand.LetGoRope;
            } else {
                anchorScript.onRelease += Player.instance.leftPlayerHand.LetGoRope;
            }
        }

        GrabSplineHelper spline = ropeTransform.GetComponentInParent<GrabSplineHelper>();
        if (spline != null) {
            spline.NetworkGrabSpline();
        }

        BeginRopeCollisionIgnore(isRightHand);
    }

    bool IsRopeOffHand(bool isRightHand) {
        return (ropeGrabRightHand && !isRightHand) || (ropeGrabLeftHand && isRightHand);
    }

    bool AllowHandLocomotionCollision(bool isRightHand) {
        if (zipLineCollisionIgnored) {
            return false;
        }
        if (isRightHand && ropeGrabRightHand) {
            return false;
        }
        if (!isRightHand && ropeGrabLeftHand) {
            return false;
        }
        if (ropeOffHandCollisionIgnored && IsRopeOffHand(isRightHand)) {
            return false;
        }
        return true;
    }

    void BeginRopeCollisionIgnore(bool ropeIsRightHand) {
        ropeOffHandCollisionIgnored = true;
        Vector3 offHandPos = ropeIsRightHand ? CurrentLeftHandPosition() : CurrentRightHandPosition();
        ropeOffHandLocalOffsetAtGrab = headCollider.transform.InverseTransformPoint(offHandPos);
    }

    void UpdateRopeOffHandCollisionIgnore() {
        if (!ropeOffHandCollisionIgnored || !IsRopeClimbing()) {
            return;
        }

        Vector3 offHandPos = ropeGrabRightHand ? CurrentLeftHandPosition() : CurrentRightHandPosition();
        Vector3 currentLocalOffset = headCollider.transform.InverseTransformPoint(offHandPos);
        if (Vector3.Distance(currentLocalOffset, ropeOffHandLocalOffsetAtGrab) >= ropeOffHandCollisionEnableDistance) {
            ropeOffHandCollisionIgnored = false;
        }
    }

    void EndRopeCollisionIgnoreIfNeeded() {
        if (!ropeGrabRightHand && !ropeGrabLeftHand) {
            ropeOffHandCollisionIgnored = false;
        }
    }

    public void LetGoRope(bool isRightHand) {
        // print("A: " + isRightHand);
        // print("LetGoRope: " + isRightHand);
        if (isRightHand) {
            ropeGrabRightHand = false;
            if (Player.instance != null && Player.instance.rightPlayerHand != null) {
                Player.instance.rightPlayerHand.ClearRopeGrabFlag();
            }
        } else {
            ropeGrabLeftHand = false;
            if (Player.instance != null && Player.instance.leftPlayerHand != null) {
                Player.instance.leftPlayerHand.ClearRopeGrabFlag();
            }
        }
        // rb.velocity = ropeVel * 20f;// * SimulationRate * Time.fixedDeltaTime;
        SetVelToAverageVel();

        if (currHandAnchor != null) {
            currHandAnchor.parent = null;

            if (currHandAnchor.TryGetComponent(out HandAnchor anchorScript)) {
                anchorScript.onRelease = null;
            }
        }

        if (PhotonManager.instance.GetMyPhotonPlayer() != null) {
            PhotonManager.instance.GetMyPhotonPlayer().movingSplineIndex = -1;
        }

        EndRopeCollisionIgnoreIfNeeded();
    }
    */
    /*
        public bool GrabZipLine(PlayerHand hand, ZipLineSpline zipLine) {
          if (hand == null || zipLine == null) {
            return false;
          }
          return BeginZipLine(zipLine, hand);
        }

        public bool GrabZipLine(Vector3 grabPos, bool isRightHand, ZipLineSpline zipLineTrigger) {
          PlayerHand hand = isRightHand ? Player.instance.rightPlayerHand : Player.instance.leftPlayerHand;
          return GrabZipLine(hand, zipLineTrigger);
        }

        // Call this when the player grabs the zip line
        bool BeginZipLine(ZipLineSpline zip, PlayerHand hand) {
          if (zip == null || hand == null) {
            return false;
          }

          if (isZipping) {
            ForceReleaseZipLine();
          }

          ClearGliderLocomotion();

          zippingHand = hand;
          currentZipLine = zip;
          grabbingHand = hand.handAnchor;

          // Get param bounds from your spline with buffer zones
          startParamMin = currentZipLine.GetStartParameter() + endBufferStart;
          endParamMax = currentZipLine.GetEndParameter() - endBufferEnd;

          // Initialize at nearest point to the grabbing hand
          currentZipLineParameter = currentZipLine.GetClosestParameter(grabbingHand.position);
          currentZipLineParameter = Mathf.Clamp(currentZipLineParameter, startParamMin, endParamMax);

          ropePointWorld = currentZipLine.GetPositionAtParameter(currentZipLineParameter);
          endLatch = EndLatch.None;
          rb.velocity = Vector3.zero;
          isZipping = true;
          hasTriedSegmentTransition = false; // Reset transition flag
          bounceBackCount = 0;
          zipLineMoveVelocity = Vector3.zero;

          // Store the initial look direction - this will be fixed while on the zip line
          initialZipLineDirection = headCollider.transform.forward;
          zipLineGrabTime = Time.time;
          zipLineGrabParameter = currentZipLineParameter; // Store grab point for collision ignore
          zipLineHapticTimer = 0f;

          Player.instance.HaltMovement(true);
          Player.instance.grPlayerController?.SetMomentum(Vector3.zero);

          StopZipLineSound();
          StartZipLineFX(grabbingHand);
          BeginZipLineCollisionIgnore();

          // Track bounty for using a zip line
          BountyManager.instance.AddBountyTask("usezipline", 1);

          if (!string.IsNullOrEmpty(zipLineOnSound)) {
            MasterAudio.PlaySound3DAtTransformAndForget(zipLineOnSound, hand.transform);
          }

          if (!string.IsNullOrEmpty(zipLineLoopSound)) {
            zipLinePsr = MasterAudio.PlaySound3DFollowTransform(zipLineLoopSound, grabbingHand);
          }

          if (VibrationManager.instance != null) {
            VibrationManager.instance.TriggerVibration(0.35f, 0.15f, hand);
          }

          return true;
        }

        public void ReleaseZipLine(PlayerHand hand) {
          if (!isZipping) {
            return;
          }
          if (hand != null && zippingHand != null && hand != zippingHand) {
            return;
          }
          CompleteZipLineRelease(hand);
        }

        public void LetGoZipLine(bool isRightHand) {
          PlayerHand hand = isRightHand ? Player.instance.rightPlayerHand : Player.instance.leftPlayerHand;
          ReleaseZipLine(hand);
        }

        public void LetGoZipLine(PlayerHand hand) {
          ReleaseZipLine(hand);
        }

        public void ForceReleaseZipLine() {
          if (!isZipping) {
            return;
          }
          CompleteZipLineRelease(zippingHand);
        }

        void CompleteZipLineRelease(PlayerHand hand) {
          Vector3 totalMomentum = Vector3.zero;
          if (zipLineMoveVelocity.magnitude > 0.5f) {
            totalMomentum += zipLineMoveVelocity * zipLineReleaseMultiplier;
          }

          if (hand != null) {
            Vector3 handVelocity = hand.velocity;
            if (handVelocity.magnitude > 0.5f) {
              totalMomentum += (-handVelocity) * zipLineHandFlingMultiplier;
            }
          }

          if (totalMomentum.magnitude > 0.1f) {
            ApplyZipLineReleaseMomentum(totalMomentum);
          }

          zipLineMoveVelocity = Vector3.zero;

          StopZipLineSound();
          StopZipLineFX();

          if (!Player.instance.IsDead) {
            Player.instance.HaltMovement(false);
          }

          Transform audioTransform = hand != null ? hand.transform : transform;
          if (!string.IsNullOrEmpty(zipLineOffSound)) {
            MasterAudio.PlaySound3DAtTransformAndForget(zipLineOffSound, audioTransform);
          }

          if (VibrationManager.instance != null && hand != null) {
            VibrationManager.instance.TriggerVibration(0f, 0f, hand);
          }

          zipLineHapticTimer = zipLineHapticInterval;

          zipLineCollisionIgnored = false;
          currentZipLine = null;
          grabbingHand = null;
          zippingHand = null;

          endLatch = EndLatch.None;
          hasTriedSegmentTransition = false;
          isZipping = false;
        }

        const float zipLineReleaseMaxSpeed = 8f;
        const float zipLineReleaseImpulseScale = 0.75f;

        void ApplyZipLineReleaseMomentum(Vector3 addedMomentum) {
          if (addedMomentum.sqrMagnitude <= 0.0001f) {
            return;
          }

          addedMomentum *= zipLineReleaseImpulseScale;

          float magnitude = addedMomentum.magnitude;
          if (magnitude > zipLineReleaseMaxSpeed) {
            addedMomentum = addedMomentum.normalized * zipLineReleaseMaxSpeed;
          }

          Player.instance.grPlayerController?.AddMomentum(addedMomentum);
          rb.velocity = addedMomentum;
          currentVelocity = addedMomentum;
          SetVelHistoryToCurrentVel();
        }

        void StartZipLineFX(Transform anchor) {
          if (anchor == null) {
            return;
          }

          if (zipLineSparkPfx == null) {
            zipLineSparkPfx = AcquireZipLineSparkPfx();
          }

          if (zipLineSparkPfx == null) {
            return;
          }

          zipLineSparkPfx.transform.SetParent(anchor);
          zipLineSparkPfx.transform.localPosition = Vector3.zero;
          zipLineSparkPfx.transform.localRotation = Quaternion.identity;
          zipLineSparkPfx.gameObject.SetActive(true);
          zipLineSparkPfx.Play();
          zipLineSparkLastWorldPos = anchor.position;
          zipLineSparkPfx.transform.forward = -anchor.forward;
        }

        public void StopZipLineFX() {
          if (zipLineSparkPfx != null) {
            ReleaseZipLineSparkPfx(zipLineSparkPfx);
            zipLineSparkPfx = null;
          }
          zipLineSparkLastWorldPos = Vector3.zero;
        }

        void UpdateZipLineSparkDirection(Vector3 velocity) {
          if (zipLineSparkPfx == null) {
            return;
          }

          Transform anchor = zipLineSparkPfx.transform.parent;
          if (anchor == null) {
            return;
          }

          Vector3 dir = velocity;
          if (dir.sqrMagnitude < 0.0001f) {
            Vector3 delta = anchor.position - zipLineSparkLastWorldPos;
            if (delta.sqrMagnitude > 0.0001f) {
              float dt = Mathf.Max(Time.deltaTime, 0.0001f);
              dir = delta / dt;
            } else {
              dir = -anchor.forward;
            }
          }

          if (dir.sqrMagnitude > 0.0001f) {
            zipLineSparkPfx.transform.forward = -dir.normalized;
          }

          zipLineSparkLastWorldPos = anchor.position;
        }

        public void StopZipLineSound() {
          if (zipLinePsr != null && zipLinePsr.ActingVariation != null) {
            zipLinePsr.ActingVariation.Stop();
            zipLinePsr = null;
          }
        }

        ParticleSystem AcquireZipLineSparkPfx() {
          if (ObjectPools.instance == null) {
            return null;
          }
          return ObjectPools.instance.GetZipLineSparkPfx();
        }

        void ReleaseZipLineSparkPfx(ParticleSystem pfx) {
          if (pfx == null) {
            return;
          }
          ObjectPools.instance?.ReturnZipLineSparkPfx(pfx);
        }

        public bool IsZipping() {
          return isZipping;
        }

        public bool IsZippingWithLeftHand() {
          return isZipping && zippingHand != null && !zippingHand.rightHand;
        }

        public PlayerHand GetZippingHand() {
          return zippingHand;
        }

        public Vector3 GetZipLineMomentumVelocity() {
          return zipLineMoveVelocity;
        }

        public ZipLineSpline GetCurrentZipLine() {
          return currentZipLine;
        }

        void BeginZipLineCollisionIgnore() {
          zipLineCollisionIgnored = true;
        }

        void UpdateZipLineCollisionIgnore() {
          if (!zipLineCollisionIgnored || !isZipping || currentZipLine == null) {
            zipLineCollisionIgnored = false;
            return;
          }

          // Check how far we've moved along the zip line since grabbing
          float distanceTraveled = Mathf.Abs(currentZipLineParameter - zipLineGrabParameter) * currentZipLine.totalCurveLength;
          
          // Stop ignoring collision once we've moved far enough along the rope
          if (distanceTraveled >= zipLineCollisionIgnoreMinDistance) {
            zipLineCollisionIgnored = false;
          }
        }

        // Helper methods for end latch system
        private void LatchAt(EndLatch which, float endParam) {
          endLatch = which;
          currentZipLineParameter = endParam;
          latchedPos = currentZipLine.GetPositionAtParameter(endParam);
          latchedTan = currentZipLine.GetTangent(endParam).normalized;

          // Make tangent point "forward out of the zip line" at this end.
          // If your spline tangent points inward at start, flip for Start end.
          if (which == EndLatch.Start) latchedTan = -latchedTan;
        }

        private bool IsNearEndpoint(Vector3 p, float endParam) {
          Vector3 endPos = currentZipLine.GetPositionAtParameter(endParam);
          return (p - endPos).sqrMagnitude <= endLatchEps * endLatchEps;
        }

        private void HandleZipLineMovement() {
          if (!isZipping || currentZipLine == null) {
            return;
          }

          UpdateZipLineCollisionIgnore();

          Transform anchorTransform = zippingHand != null ? zippingHand.handAnchor : grabbingHand;
          if (anchorTransform == null) {
            ForceReleaseZipLine();
            return;
          }

          grabbingHand = anchorTransform;

          float deltaParam = currentZipLine.CalculateDeltaParameter(
              currentZipLineParameter,
              initialZipLineDirection,
              Time.fixedDeltaTime
          );

          if (endLatch == EndLatch.None) {
            float desiredParam = currentZipLineParameter + deltaParam;
            float clampedParam = Mathf.Clamp(desiredParam, startParamMin, endParamMax);

            bool hitStart = clampedParam <= startParamMin && desiredParam < startParamMin;
            bool hitEnd = clampedParam >= endParamMax && desiredParam > endParamMax;

            currentZipLineParameter = clampedParam;
            ropePointWorld = currentZipLine.GetPositionAtParameter(currentZipLineParameter);

            if (hitStart && IsNearEndpoint(anchorTransform.position, startParamMin)) {
              LatchAt(EndLatch.Start, startParamMin);
            } else if (hitEnd && IsNearEndpoint(anchorTransform.position, endParamMax)) {
              LatchAt(EndLatch.End, endParamMax);
            }
          } else {
            ropePointWorld = latchedPos;
            Vector3 handErr = ropePointWorld - anchorTransform.position;
            float along = Vector3.Dot(handErr, latchedTan);
            if (along < -endReleaseBack) {
              float backParam = currentZipLine.MoveParameter(latchedPos, currentZipLineParameter, -endReleaseBack);
              currentZipLineParameter = Mathf.Clamp(backParam, startParamMin, endParamMax);
              ropePointWorld = currentZipLine.GetPositionAtParameter(currentZipLineParameter);
              endLatch = EndLatch.None;
            }
          }

          Vector3 handError = ropePointWorld - anchorTransform.position;
          if (handError.sqrMagnitude < handDeadzone * handDeadzone) {
            handError = Vector3.zero;
          }

          if (endLatch != EndLatch.None) {
            Vector3 along = Vector3.Project(handError, latchedTan);
            if (Vector3.Dot(along, latchedTan) > 0f) {
              handError -= along;
            }
          }

          rb.MovePosition(rb.position + handError);
          rb.velocity = Vector3.zero;

          // float timeSinceGrab = Time.time - zipLineGrabTime;
          // if (timeSinceGrab > 0.2f && zippingHand != null && handError.magnitude > zipLineBreakDistance) {
          //   ReleaseZipLine(zippingHand);
          //   return;
          // }

          Vector3 zipLineDirection = currentZipLine.GetDirectionAtParameter(currentZipLineParameter);
          if (Mathf.Abs(deltaParam) > Mathf.Epsilon) {
            float directionSign = Mathf.Sign(deltaParam);
            zipLineMoveVelocity = zipLineDirection * currentZipLine.zipLineSpeed * directionSign;
          } else {
            zipLineMoveVelocity = Vector3.zero;
          }

          UpdateZipLineSparkDirection(zipLineMoveVelocity);

          if (VibrationManager.instance != null && zippingHand != null) {
            zipLineHapticTimer -= Time.deltaTime;
            if (zipLineHapticTimer <= 0f) {
              VibrationManager.instance.TriggerVibration(0.25f, 0.08f, zippingHand);
              zipLineHapticTimer = zipLineHapticInterval;
            }
          }

          if (endLatch != EndLatch.None) {
            if (!hasTriedSegmentTransition) {
              hasTriedSegmentTransition = true;
              FindNextZipLineSegment();
            }
          } else {
            hasTriedSegmentTransition = false;
          }
        }

        private void FindNextZipLineSegment() {
          if (currentZipLine == null) return;

          // Look for adjacent zip line segments
          Vector3 currentPosition = transform.position;
          Vector3 zipLineDirection = currentZipLine.GetDirectionAtParameter(currentZipLineParameter);

          // Check for zip line segments in the direction we're moving
          Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 2f); // Reduced radius

          ZipLineSpline bestNextZipLine = null;
          float bestDistance = float.MaxValue;

          foreach (Collider col in nearbyColliders) {
            ZipLineSpline nextZipLine = col.GetComponent<ZipLineSpline>();
            if (nextZipLine != null && nextZipLine != currentZipLine) {
              // Check if this segment connects to our current one
              if (nextZipLine.curvePoints.Count >= 2) {
                Vector3 nextStart = nextZipLine.curvePoints[0];
                Vector3 nextEnd = nextZipLine.curvePoints[nextZipLine.curvePoints.Count - 1];

                float distanceToStart = Vector3.Distance(currentPosition, nextStart);
                float distanceToEnd = Vector3.Distance(currentPosition, nextEnd);
                float closestDistance = Mathf.Min(distanceToStart, distanceToEnd);

                // Only consider segments that are very close and in the right direction
                if (closestDistance < 1.5f && closestDistance < bestDistance) {
                  // Check if the new segment is in the direction we're moving
                  Vector3 toNextSegment = (closestDistance == distanceToStart ? nextStart : nextEnd) - currentPosition;
                  if (Vector3.Dot(zipLineDirection, toNextSegment.normalized) > 0.3f) { // Must be roughly in the same direction
                    bestNextZipLine = nextZipLine;
                    bestDistance = closestDistance;
                  }
                }
              }
            }
          }

          if (bestNextZipLine != null) {
            // Switch to the best connected segment
            currentZipLine = bestNextZipLine;
            startParamMin = currentZipLine.GetStartParameter() + endBufferStart;
            endParamMax = currentZipLine.GetEndParameter() - endBufferEnd;
            currentZipLineParameter = bestNextZipLine.GetCurrentSplineParameter(transform.position);
            ropePointWorld = bestNextZipLine.GetPositionAtParameter(currentZipLineParameter);
            endLatch = EndLatch.None; // Reset end latch for new segment
            hasTriedSegmentTransition = false; // Reset for the new segment
          } else {
            // If no connected segment found, let go of the zip line
            ForceReleaseZipLine();
          }
        }
        */

        public void SetVelToAverageVel() {
          bool isDead = Player.instance != null && Player.instance.IsDead;
          float velLimit = isDead ? deadVelocityLimit : velocityLimit;
          float jumpSpeed = isDead ? deadMaxJumpSpeed : maxJumpSpeed;
          float jumpMult = isDead ? deadJumpMultiplier : jumpMultiplier;

          if (denormalizedVelocityAverage.magnitude > velLimit)
          {
            if (denormalizedVelocityAverage.magnitude * jumpMult > jumpSpeed)
            {
              rb.linearVelocity = denormalizedVelocityAverage.normalized * jumpSpeed;
            }
            else
            {
              rb.linearVelocity = jumpMult * denormalizedVelocityAverage;
            }
          }
        }

        void OnCollisionEnter(Collision collision) {
          ReportPassengerBodySupport(collision);
          for (int i = 0; i < collision.contactCount; i++) {
            ContactPoint contact = collision.GetContact(i);
            if(contact.normal.y > 0.1) {
              bounceBackCount = 0;
            }
          }

          // Body landing: bodyCollider hits Environment with significant velocity magnitude
          if (collision.gameObject.layer == envLayer && bodyCollider != null) {
            float impactMagnitude = collision.relativeVelocity.magnitude;
            if (impactMagnitude >= bodyLandingMinMagnitude) {
              // foreach (ContactPoint contact in collision.contacts) {
              for (int i = 0; i < collision.contactCount; i++) {
                ContactPoint contact = collision.GetContact(i);
                if (contact.thisCollider == bodyCollider && contact.normal.y > 0.5f) {
                  float vol = Mathf.Clamp01(GRMathUtils.MapRangeAndClamp(impactMagnitude, bodyLandingMinMagnitude, 6f, 0.5f, 1f));
                  PlaySurfaceSound(bodyLandingSounds, contact.otherCollider, contact.point, vol);
                  break;
                }
              }
            }
          }
        }

        void OnCollisionStay(Collision collision) {
          ReportPassengerBodySupport(collision);
          if (collision.gameObject.layer == envLayer) {
            // print("*** env stay");
            // GRUtility.PrintHierarchyOfGameObject(collision.gameObject);
            groundedCount = 2;
          } 

        }

        void ReportPassengerBodySupport(Collision collision) {
          if (HelicopterManager.instance == null) return;
          for (int i = 0; i < collision.contactCount; i++) {
            ContactPoint contact = collision.GetContact(i);
            if (contact.thisCollider != bodyCollider || contact.normal.y < 0.5f) continue;
            HelicopterManager.instance.ReportBodySupport(transform, contact.otherCollider, contact.point, contact.normal);
            break;
          }
        }

    }

    public enum LaunchType {
      OverwriteVelocity = 0,
      OverwriteVerticalCombineHorizontal = 1,
      CombineVelocity = 2,
    }
