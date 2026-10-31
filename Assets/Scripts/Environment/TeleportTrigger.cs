using UnityEngine;

/// <summary>
/// Sends the local player to <see cref="destination"/> when they touch this trigger.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class TeleportTrigger : MonoBehaviour
{
    [SerializeField] Transform destination;

    BoxCollider _box;
    float _readyAt;

    void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void Awake()
    {
        _box = GetComponent<BoxCollider>();
        _box.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // Head and body colliders ignore Default. World triggers they can touch sit on Environment.
        int environment = LayerMask.NameToLayer("Environment");
        if (environment >= 0)
            gameObject.layer = environment;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsLocalPlayer(other))
            Teleport();
    }

    void LateUpdate()
    {
        if (destination == null || Time.time < _readyAt)
            return;

        NewPlayer player = NewPlayer.instance;
        if (player != null && player.isActiveAndEnabled
            && (Touches(player.headCollider) || Touches(player.bodyCollider)))
        {
            Teleport();
            return;
        }

        DesktopPlayer desktop = FindAnyObjectByType<DesktopPlayer>();
        if (desktop != null && desktop.isActiveAndEnabled && BodyInside(desktop.transform, 1.7f))
            Teleport();
    }

    static bool IsLocalPlayer(Collider other)
    {
        if (other.GetComponentInParent<DesktopPlayer>() != null)
            return true;
        if (other.GetComponentInParent<NewPlayer>() != null)
            return true;

        Rigidbody body = other.attachedRigidbody;
        return body != null && body.CompareTag("Player");
    }

    void Teleport()
    {
        if (destination == null || Time.time < _readyAt)
            return;

        _readyAt = Time.time + 0.5f;

        if (NewPlayer.instance != null)
        {
            NewPlayer.instance.Warp(destination.position, destination.rotation);
            return;
        }

        if (GameManager.instance != null && GameManager.instance.usingDesktopPlayer)
        {
            DesktopPlayer desktop = FindAnyObjectByType<DesktopPlayer>();
            if (desktop != null)
                desktop.Teleport(destination.position, destination.rotation);
            return;
        }

        if (NewPlayer.instance != null)
            NewPlayer.instance.Warp(destination.position);
    }

    bool Touches(Collider collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            return false;
        if (Contains(collider.bounds.center))
            return true;
        return Contains(collider.ClosestPoint(transform.TransformPoint(_box.center)));
    }

    bool BodyInside(Transform body, float height)
    {
        if (Contains(body.position))
            return true;
        if (Contains(body.position + Vector3.up * (height * 0.5f)))
            return true;
        return Contains(body.position + Vector3.up * height);
    }

    bool Contains(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint) - _box.center;
        Vector3 half = _box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x
            && Mathf.Abs(local.y) <= half.y
            && Mathf.Abs(local.z) <= half.z;
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }

        if (destination == null)
            return;

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.35f);
        Gizmos.DrawRay(destination.position, destination.forward);
    }
}
