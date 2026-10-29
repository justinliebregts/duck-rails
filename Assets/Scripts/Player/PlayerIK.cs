using UnityEngine;

public class PlayerIK : MonoBehaviour
{
    public Transform rootSource;
    public Transform headSource;
    public Transform leftHandSource;
    public Transform rightHandSource;

    public Transform headBone;
    public Transform rootBone;
    public Transform leftHandBone;
    public Transform rightHandBone;

    void LateUpdate() {
        rootBone.SetPositionAndRotation(rootSource.position, rootSource.rotation);
        headBone.SetPositionAndRotation(headSource.position, headSource.rotation);
        leftHandBone.SetPositionAndRotation(leftHandSource.position, leftHandSource.rotation);
        rightHandBone.SetPositionAndRotation(rightHandSource.position, rightHandSource.rotation);
        
    }
}
