using UnityEngine;

public class LoginView : MonoBehaviour
{
    [SerializeField] Transform rig;
    [SerializeField] LoginSpot[] spots;

    LoginFlow _flow;
    HeadsetCamera _headset;
    Transform _head;
    Transform _alignSpot;
    int _index;
    bool _awaitingAlign;

    void Awake()
    {
        _flow = GetComponent<LoginFlow>();
        if (rig != null)
        {
            _headset = rig.GetComponentInChildren<HeadsetCamera>(true);
            _head = _headset != null ? _headset.transform : rig;
        }

        if (rig == null || spots == null || spots.Length == 0)
            return;

        _index = _flow != null && _flow.PreviewSpots ? 0 : Random.Range(0, spots.Length);
        Show(_index);
    }

    void OnEnable()
    {
        if (_headset != null)
            _headset.TrackedPoseApplied += OnTrackedPoseApplied;
    }

    void OnDisable()
    {
        if (_headset != null)
            _headset.TrackedPoseApplied -= OnTrackedPoseApplied;
    }

    void Update()
    {
        if (_flow == null || !_flow.PreviewSpots || spots == null || spots.Length == 0)
            return;
        if (!RailDesktopInput.GetKeyDown(_flow.PreviewSpotKey))
            return;

        _index = (_index + 1) % spots.Length;
        Show(_index);
    }

    void OnTrackedPoseApplied()
    {
        if (!_awaitingAlign)
            return;

        if (AlignHead())
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
        _awaitingAlign = true;

        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] != null)
                spots[i].gameObject.SetActive(i == index);
        }
    }

    bool AlignHead()
    {
        if (_alignSpot == null || _head == null)
            return false;

        Vector3 spotForward = Vector3.ProjectOnPlane(_alignSpot.forward, Vector3.up);
        Vector3 lookForward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
        if (spotForward.sqrMagnitude < 1e-6f || lookForward.sqrMagnitude < 1e-6f)
            return false;

        float delta = Vector3.SignedAngle(lookForward, spotForward, Vector3.up);
        rig.Rotate(Vector3.up, delta, Space.World);

        // The spot marks where the viewer stands, not the tracking origin.
        // Compensate after rotating so room-scale offsets cannot orbit the logo.
        Vector3 correction = _alignSpot.position - _head.position;
        correction.y = 0f;
        rig.position += correction;
        return true;
    }
}
