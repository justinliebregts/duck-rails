using System.Collections.Generic;
using Liv.Lck;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Game.Animation
{
    /// <summary>Drives the body from the LOCAL player's tracked poses, 1:1.</summary>
    public class LocalBodyDriver : MonoBehaviour
    {
	    public ControllerInput controllerInput;

        /// <summary>Enabled local body drivers (DuckPlayer / DesktopPlayer). Enemies use this when no PhotonPlayer is present.</summary>
        public static readonly List<LocalBodyDriver> Active = new List<LocalBodyDriver>(4);

        [SerializeField] RemotePlayerBody body;
        [SerializeField] Transform headAnchor;      // the XR camera
        [SerializeField] Transform leftHandAnchor;
        [SerializeField] Transform rightHandAnchor;

        public RemotePlayerBody Body => body;
        public Transform HeadAnchor => headAnchor;
        public Transform LeftHandAnchor => leftHandAnchor;
        public Transform RightHandAnchor => rightHandAnchor;

        void OnEnable()
        {
            Active.Add(this);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            Active.Remove(this);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _captureOpen = false;
            NightVisionGoggles.SetShownInExternalView(false);
            if (body != null)
            {
                body.SetSkinRecalculation(false);
                body.SetHeadHidden(true);
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (body == null || camera == null)
                return;
            bool mirrorOn = HelicopterMirror.AnyActive;
            if (!_captureOpen && !mirrorOn)
                return;
            // Selfie preview and the helicopter screen are the only views that should see this face.
            bool showHead = HelicopterMirror.IsMirrorCamera(camera)
                || (camera.GetComponent<LckCamera>() != null && !IsFirstPersonCapture(camera));
            NightVisionGoggles.SetShownInExternalView(showHead);
            body.SetHeadHidden(!showHead);
            if (showHead)
                PoseCaptureFace();
            body.SetSkinRecalculation(true);
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (body == null || camera == null)
                return;
            if (!_captureOpen && !HelicopterMirror.AnyActive)
                return;
            body.SetSkinRecalculation(false);
            if (HelicopterMirror.IsMirrorCamera(camera) || camera.GetComponent<LckCamera>() != null)
            {
                body.SetHeadHidden(true);
                NightVisionGoggles.SetShownInExternalView(false);
            }
        }

        bool _captureOpen;
        bool _captureCamerasCached;
        LckCamera[] _captureCameras;

        bool CaptureOpen()
        {
            if (!_captureCamerasCached)
            {
                _captureCameras = FindObjectsByType<LckCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                _captureCamerasCached = true;
                HideEyeOverlayFromExternalCaptures();
            }

            if (_captureCameras == null)
                return false;

            for (int i = 0; i < _captureCameras.Length; i++)
            {
                LckCamera capture = _captureCameras[i];
                if (capture == null)
                    continue;
                Camera captureCamera = capture.GetCameraComponent();
                if (captureCamera != null && captureCamera.enabled)
                    return true;
            }

            return false;
        }

        void HideEyeOverlayFromExternalCaptures()
        {
            int layer = LayerMask.NameToLayer("EyeOverlay");
            if (layer < 0 || _captureCameras == null)
                return;

            int hidden = 1 << layer;
            for (int i = 0; i < _captureCameras.Length; i++)
            {
                LckCamera capture = _captureCameras[i];
                if (capture == null)
                    continue;
                Camera captureCamera = capture.GetCameraComponent();
                if (captureCamera == null || IsFirstPersonCapture(captureCamera))
                    continue;
                captureCamera.cullingMask &= ~hidden;
            }
        }

        static bool IsFirstPersonCapture(Camera camera)
        {
            // The rendering camera is under Stabilizer, which is under "First Person Camera".
            Transform current = camera.transform.parent;
            while (current != null)
            {
                if (current.name == "First Person Camera")
                    return true;
                current = current.parent;
            }
            return false;
        }

        Transform _leftEye;
        Transform _rightEye;
        Transform _topLip;
        Transform _bottomLip;
        PhotonEyeLook _eyes;
        PhotonVoiceMouth _mouth;

        void PoseCaptureFace()
        {
            EnsureFace();
            EnsureLocalFaceDrivers();
            if (_mouth != null)
                _mouth.PoseLocal(_topLip, _bottomLip);
            if (_eyes != null)
                _eyes.PoseCaptureScale(_leftEye, _rightEye);
        }

        void EnsureFace()
        {
            if (body == null || _leftEye != null || _rightEye != null || _topLip != null || _bottomLip != null)
                return;
            Transform root = body.transform;
            _leftEye = FindDeep(root, "Eye_L");
            _rightEye = FindDeep(root, "Eye_R");
            _topLip = FindDeep(root, "Lip_T");
            _bottomLip = FindDeep(root, "Lip_B");
        }

        void EnsureLocalFaceDrivers()
        {
            if (_eyes != null && _mouth != null)
                return;
            List<PhotonPlayer> players = PhotonPlayer.Active;
            for (int i = 0; i < players.Count; i++)
            {
                PhotonPlayer player = players[i];
                if (player == null || !player.IsLocalHuman)
                    continue;
                _eyes = player.GetComponent<PhotonEyeLook>();
                _mouth = player.GetComponent<PhotonVoiceMouth>();
                return;
            }
        }

        static Transform FindDeep(Transform root, string boneName)
        {
            if (root == null)
                return null;
            if (root.name == boneName)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), boneName);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void BindAnchors(Transform head, Transform leftHand, Transform rightHand, RemotePlayerBody poseBody = null)
        {
            headAnchor = head;
            leftHandAnchor = leftHand;
            rightHandAnchor = rightHand;
            if (poseBody != null)
                body = poseBody;
            if (_velocity != null)
                _velocity.Track(headAnchor, leftHandAnchor, rightHandAnchor);
        }
        [SerializeField] Renderer[] hiddenWhenLocal; // head/face/hair meshes


        [Header("Look-down torso")]
        [SerializeField] float lookDownStart = 20f;       // degrees below the horizon where the chest starts shifting back
        [SerializeField] float lookDownFull = 70f;        // degrees below the horizon at full shift
        [SerializeField] float lookDownChestBack = 0.12f; // meters back along body yaw at a steep look-down
        [SerializeField] float lookDownSmooth = 12f;      // 1/s; raise = snappier

        float _lookDownBack;

        [Header("Input mapping")]
        [SerializeField, Range(0f, 1f)] float gripToThumb    = 0.95f;
        [SerializeField, Range(0f, 1f)] float gripToIndex    = 0.15f; // resting half-curl on a controller
        [SerializeField, Range(0f, 1f)] float triggerToThumb = 0.40f;

        [Header("Fingers")]
        [SerializeField] Finger rightThumb;
        [SerializeField] Finger rightIndex;
        [SerializeField] Finger rightPinky;
        [SerializeField] Finger leftThumb;
        [SerializeField] Finger leftIndex;
        [SerializeField] Finger leftPinky;

        public FingerCurl Curl { get; private set; }

        void Awake() {
            rightThumb?.Capture();
            rightIndex?.Capture();
            rightPinky?.Capture();
            leftThumb?.Capture();
            leftIndex?.Capture();
            leftPinky?.Capture();
        }

        void Start()
        {
            if (hiddenWhenLocal != null)
            {
                foreach (var r in hiddenWhenLocal)
                {
                    if (r != null)
                        r.enabled = false;
                }
            }

            if (body != null)
            {
                DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Local);
                body.SetHeadHidden(true);
                if (body.Chest != null)
                    CreatorNametag.BindLocal(body.Chest);
            }
            _velocity = gameObject.AddComponent<VelocitySampler>();
            _velocity.Track(headAnchor, leftHandAnchor, rightHandAnchor);
        }

        VelocitySampler _velocity;

        /// <summary>How the corpse should move if this player dies now: head speed for the body, plus each hand's swing.</summary>
        public RagdollLaunch DeathLaunch()
        {
            if (_velocity == null)
                return default;
            return new RagdollLaunch
            {
                body = _velocity.Velocity(0),
                leftHand = _velocity.Velocity(1),
                rightHand = _velocity.Velocity(2),
            };
        }

        void LateUpdate()
        {
            PoseNow();
            _captureOpen = CaptureOpen();
            if (!_captureOpen && !HelicopterMirror.AnyActive)
                return;
            EnsureLocalFaceDrivers();
            if (_mouth != null)
                _mouth.SampleLocal();
        }

        /// <summary>Solves the duck from the current anchors. Movement calls this before held-item locomotion samples.</summary>
        public void PoseNow() => Apply();

        /// <summary>Meters to slide the chest back. Level and upward looks stay at 0; pitch ramps between the two angles.</summary>
        float LookDownChestBack()
        {
            float belowHorizon = Mathf.Asin(Mathf.Clamp(-headAnchor.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float target = lookDownChestBack * Mathf.InverseLerp(lookDownStart, lookDownFull, belowHorizon);
            float k = 1f - Mathf.Exp(-lookDownSmooth * Time.deltaTime);
            _lookDownBack = Mathf.Lerp(_lookDownBack, target, k);
            return _lookDownBack;
        }

        // Optional polish: re-solve with the freshest tracked poses right
        // before render, so arms don't lag the late-latched camera on fast head turns.
        // void OnEnable()  => Application.onBeforeRender += Apply;
        // void OnDisable() => Application.onBeforeRender -= Apply;

        void Apply()
        {
            if (body == null || headAnchor == null || leftHandAnchor == null || rightHandAnchor == null)
                return;

            if (body.Chest != null)
                CreatorNametag.BindLocal(body.Chest);

            body.ApplyPose(new BodyPose {
                headPos  = headAnchor.position,  headRot  = headAnchor.rotation,
                lHandPos = leftHandAnchor.position,  lHandRot = leftHandAnchor.rotation,
                rHandPos = rightHandAnchor.position, rHandRot = rightHandAnchor.rotation,
            }, LookDownChestBack());

            if (controllerInput == null)
                return;

            float leftThumbVal = 0f;
            if(controllerInput.xButton.down || controllerInput.yButton.down || controllerInput.leftThumbstickButton.down) {
                leftThumbVal = 1f;
            } else if(controllerInput.leftThumbTouch) {
                leftThumbVal = 0.4f;
            } else if(controllerInput.leftThumbHover) {
                leftThumbVal = 0.1f;
            }
            float rightThumbVal = 0f;
            if(controllerInput.aButton.down || controllerInput.bButton.down || controllerInput.rightThumbstickButton.down) {
                rightThumbVal = 1f;
            } else if(controllerInput.rightThumbTouch) {
                rightThumbVal = 0.4f;
            }  else if(controllerInput.rightThumbHover) {
                rightThumbVal = 0.1f;
            }
            // print("**: " + controllerInput.rightTriggerValue);
            // print("***: " + rightThumbVal);

            rightThumb?.Apply(rightThumbVal);
            rightIndex?.Apply(controllerInput.rightTriggerValue);
            rightPinky?.Apply(controllerInput.rightGripValue);
            leftThumb?.Apply(leftThumbVal);
            leftIndex?.Apply(controllerInput.leftTriggerValue);
            leftPinky?.Apply(controllerInput.leftGripValue);

            Curl = new FingerCurl
            {
                rightThumb = rightThumbVal,
                rightIndex = controllerInput.rightTriggerValue,
                rightPinky = controllerInput.rightGripValue,
                leftThumb = leftThumbVal,
                leftIndex = controllerInput.leftTriggerValue,
                leftPinky = controllerInput.leftGripValue,
            };
        }
    }
}
