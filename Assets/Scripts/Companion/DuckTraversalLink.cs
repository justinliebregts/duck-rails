using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional authored approach/landing pair for awkward vehicle and rooftop routes.</summary>
public sealed class DuckTraversalLink : MonoBehaviour
{
    public static readonly List<DuckTraversalLink> Active = new List<DuckTraversalLink>();
    public Transform Landing;
    public bool Climb = true;
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);
    void OnDrawGizmos()
    {
        if (Landing == null) return;
        Gizmos.color = Climb ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, Landing.position);
        Gizmos.DrawWireSphere(Landing.position, 0.3f);
    }
}
