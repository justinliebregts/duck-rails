using UnityEngine;

// Remote PhotonPlayer poses are interpolated in LateUpdate. Follow after that
// interpolation so moving-train feedback renders at the duck's current pose.
[DefaultExecutionOrder(10000)]
public sealed class RailReviveFollow : MonoBehaviour
{
    public int Actor { get; set; }

    void LateUpdate()
    {
        if (RailExpedition.Position(Actor, out var head))
            transform.position = head - Vector3.up * .5f;
    }
}
