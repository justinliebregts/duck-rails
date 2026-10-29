using TMPro;
using UnityEngine;

public class LoginSpot : MonoBehaviour
{
    const float EyeHeight = 1.6f;
    const float ForwardLength = 0.75f;

    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text versionText;

    void Awake()
    {
        if (versionText != null)
            versionText.text = Application.version;
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.text = message;
        }
    }

    void OnDrawGizmos()
    {
        Vector3 eye = transform.position + Vector3.up * EyeHeight;
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, eye);
        Gizmos.DrawWireSphere(eye, 0.06f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-6f)
            return;

        Gizmos.DrawLine(eye, eye + forward.normalized * ForwardLength);
    }
}
