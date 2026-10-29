using Game.Audio;
using UnityEngine;

/// <summary>Optional local-player adapter. Add beside NewPlayer or DesktopPlayer, never to the HMD.</summary>
[RequireComponent(typeof(DistanceFootsteps))]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] float groundTolerance = 0.12f;
    DistanceFootsteps footsteps;
    NewPlayer vr;
    CharacterController desktop;
    void Start()
    {
        footsteps = GetComponent<DistanceFootsteps>();
        vr = GetComponent<NewPlayer>();
        desktop = GetComponent<CharacterController>();
    }
    void LateUpdate()
    {
        Vector3 point = transform.position;
        bool grounded = desktop != null && desktop.isGrounded;
        if (vr != null && vr.bodyCollider != null)
        {
            // NewPlayer.IsGrounded includes hand/head contact. Footsteps require ground under the body.
            var bounds = vr.bodyCollider.bounds;
            grounded = Physics.Raycast(bounds.center, Vector3.down, out var hit,
                bounds.extents.y + groundTolerance, groundMask, QueryTriggerInteraction.Ignore);
            if (grounded) point.y = hit.point.y;
        }
        footsteps.Tick(point, grounded, Vector3.up);
    }
}
