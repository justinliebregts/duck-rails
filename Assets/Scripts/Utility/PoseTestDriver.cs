using Unity.Mathematics;
using UnityEngine;

namespace Game.Animation
{
    public class PoseTestDriver : MonoBehaviour
    {
        [SerializeField] RemotePlayerBody body;
        [SerializeField] Transform headAnchor;       // TrackedPoseDriver-driven camera
        [SerializeField] Transform leftHandAnchor;  // TrackedPoseDriver-driven hand anchors
        [SerializeField] Transform rightHandAnchor;

        [Header("Re-base (avatar stands at offset, mimicking you)")]
        [SerializeField] bool   useOffset = true;
        [SerializeField] Vector3 spawnOffset = new Vector3(0f, 0f, 1.8f);
        [SerializeField] KeyCode freezeKey = KeyCode.Space;

        void LateUpdate()
        {
            if (Input.GetKeyDown(freezeKey)) return; // toggle by re-pressing... see note
            bool frozen = false; // simple: hold-to-run; see tuning notes

            float3 h = headAnchor.position;
            var p = new BodyPose
            {
                headPos  = useOffset ? spawnOffset : h,
                headRot  = headAnchor.rotation,
                lHandPos = useOffset ? (float3)spawnOffset + ((float3)leftHandAnchor.position  - h) : leftHandAnchor.position,
                lHandRot = leftHandAnchor.rotation,
                rHandPos = useOffset ? (float3)spawnOffset + ((float3)rightHandAnchor.position - h) : rightHandAnchor.position,
                rHandRot = rightHandAnchor.rotation,
            };
            body.ApplyPose(in p);
        }
    }
}