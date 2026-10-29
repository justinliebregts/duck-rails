using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Applies the same thumb, index, and pinky curls the local duck uses.
    /// Middle and ring stay at rest, matching the local rig.
    /// </summary>
    public class FingerRig : MonoBehaviour
    {
        [SerializeField] Finger rightThumb;
        [SerializeField] Finger rightIndex;
        [SerializeField] Finger rightPinky;
        [SerializeField] Finger leftThumb;
        [SerializeField] Finger leftIndex;
        [SerializeField] Finger leftPinky;

        void Awake()
        {
            rightThumb?.Capture();
            rightIndex?.Capture();
            rightPinky?.Capture();
            leftThumb?.Capture();
            leftIndex?.Capture();
            leftPinky?.Capture();
        }

        public void Apply(FingerCurl curl)
        {
            rightThumb?.Apply(curl.rightThumb);
            rightIndex?.Apply(curl.rightIndex);
            rightPinky?.Apply(curl.rightPinky);
            leftThumb?.Apply(curl.leftThumb);
            leftIndex?.Apply(curl.leftIndex);
            leftPinky?.Apply(curl.leftPinky);
        }
    }
}
