using UnityEngine;

// Over-the-shoulder reach zone. Author it directly under the body's chest.
public class BagBackTrigger : MonoBehaviour
{
    [Tooltip("Reach radius in world meters. Transform scale does not change this radius.")]
    [SerializeField] float radius = 0.22f;

    public float Radius => radius;

    public bool Contains(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= radius * radius;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
