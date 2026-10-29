using UnityEngine;

public class LoginView : MonoBehaviour
{
    [SerializeField] Transform rig;
    [SerializeField] LoginSpot[] spots;

    LoginFlow _flow;
    Transform _head;
    Transform _alignSpot;
    int _index;
    int _alignWait;
    bool _awaitingAlign;

    void Awake()
    {
        _flow = GetComponent<LoginFlow>();
        if (rig != null)
        {
            HeadsetCamera headset = rig.GetComponentInChildren<HeadsetCamera>(true);
            _head = headset != null ? headset.transform : rig;
        }

        if (rig == null || spots == null || spots.Length == 0)
            return;

        _index = _flow != null && _flow.PreviewSpots ? 0 : Random.Range(0, spots.Length);
        Show(_index);
    }

    void Update()
    {
        if (_flow == null || !_flow.PreviewSpots || spots == null || spots.Length == 0)
            return;
        if (!Input.GetKeyDown(_flow.PreviewSpotKey))
            return;

        _index = (_index + 1) % spots.Length;
        Show(_index);
    }

    void LateUpdate()
    {
        if (!_awaitingAlign)
            return;

        _alignWait++;
        if (!HeadsetReady() && _alignWait < 90)
            return;

        AlignHeadYaw();
        _awaitingAlign = false;
    }

    void Show(int index)
    {
        LoginSpot spot = spots[index];
        if (spot == null || rig == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(spot.transform.forward, Vector3.up);
        Quaternion rotation = forward.sqrMagnitude < 1e-6f
            ? Quaternion.Euler(0f, rig.eulerAngles.y, 0f)
            : Quaternion.LookRotation(forward.normalized, Vector3.up);
        rig.SetPositionAndRotation(spot.transform.position, rotation);
        _alignSpot = spot.transform;
        _alignWait = 0;
        _awaitingAlign = true;

        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] != null)
                spots[i].gameObject.SetActive(i == index);
        }
    }

    void AlignHeadYaw()
    {
        if (_alignSpot == null || _head == null)
            return;

        Vector3 spotForward = Vector3.ProjectOnPlane(_alignSpot.forward, Vector3.up);
        Vector3 lookForward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
        if (spotForward.sqrMagnitude < 1e-6f || lookForward.sqrMagnitude < 1e-6f)
            return;

        float delta = Vector3.SignedAngle(lookForward, spotForward, Vector3.up);
        rig.Rotate(Vector3.up, delta, Space.World);
    }

    static bool HeadsetReady()
    {
        return OVRManager.OVRManagerinitialized && OVRNodeStateProperties.IsHmdPresent();
    }
}
