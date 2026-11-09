using UnityEngine;

namespace Game.Animation
{
    public partial class SpiderGait
    {
        [Header("Close elevated targets")]
        [SerializeField] bool raiseNearTarget;
        [SerializeField, Min(0f)] float raiseDistance = 1f;
        [SerializeField, Min(0f)] float lowerDistance = 1.5f;
        [Tooltip("Target head height above the supporting floor at which extra reach is needed.")]
        [SerializeField, Min(0f)] float elevatedHeadHeight = 1f;
        [SerializeField, Min(0f)] float maxRaiseHeight = 0.5f;
        [SerializeField, Min(0.01f)] float raiseSeconds = 0.2f;
        [SerializeField, Min(0.01f)] float lowerSeconds = 0.3f;
        [SerializeField, Min(0.01f)] float raisedStepTrigger = 0.15f;
        [SerializeField, Min(0.01f)] float raisedSwingDuration = 0.07f;

        Game.AI.Enemy _raiseEnemy;
        byte _raiseLevel;
        float _raiseHeight;

        // A separate pose byte carries the owner's requested lift alongside boarding flags.
        // Peers never infer proximity from the aim point (which has no distance).
        public byte RaiseLevel => CanRaise ? _raiseLevel : (byte)0;
        public float RaiseHeight => _raiseHeight;
        bool CanRaise => raiseNearTarget && root != null && root.up.y >= 0.9f
            && _raiseEnemy != null && !_raiseEnemy.IsDormant && !_raiseEnemy.IsKnocked
            && !_raiseEnemy.IsStunned && !_raiseEnemy.RagdollActive
            && _raiseEnemy.CurrentState == Game.AI.Enemy.State.Chase;

        public void SetRemoteRaise(byte level) => _raiseLevel = (byte)Mathf.Min(level, 7);

        public void ResetRaise()
        {
            _raiseLevel = 0;
            _raiseHeight = 0f;
            _init = false;
        }

        void OnDisable() => ResetRaise();

        void UpdateRaise(float dt)
        {
            if (!CanRaise) _raiseLevel = 0;
            else if (_raiseEnemy.HasAuthority)
            {
                byte next = 0;
                if (_raiseEnemy.TryGetAimPoint(out Vector3 target))
                {
                    Vector3 delta = target - root.position;
                    float distance = _raiseLevel == 0 ? raiseDistance : Mathf.Max(raiseDistance, lowerDistance);
                    float extra = delta.y - elevatedHeadHeight;
                    // NaN/infinity fail these comparisons without contaminating the pose.
                    if (delta.x * delta.x + delta.z * delta.z <= distance * distance
                        && extra > 0f && !float.IsInfinity(extra) && maxRaiseHeight > 0f)
                    {
                        // Reject distant/low targets before consulting foot support.
                        int supports = 0;
                        for (int i = 0; i < legs.Length && supports < 2; i++)
                            if (legs[i].homeSupported && legs[i].homeNormal.y >= .9f
                                && Mathf.Abs(Vector3.Dot(root.position - legs[i].home, _up)) <= .4f) supports++;
                        if (supports >= 2)
                            next = (byte)Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(extra / maxRaiseHeight) * 7f), 1, 7);
                    }
                }
                _raiseLevel = next;
            }
            float desired = maxRaiseHeight * (_raiseLevel / 7f);
            float seconds = desired > _raiseHeight ? raiseSeconds : lowerSeconds;
            _raiseHeight = Mathf.MoveTowards(_raiseHeight, desired, maxRaiseHeight * dt / Mathf.Max(.01f, seconds));
            // A wall/ceiling transition must immediately restore the crawling envelope.
            if (root.up.y < .9f) _raiseHeight = 0f;
        }

        void LimitRaiseToLegs(float dt)
        {
            if (_raiseHeight <= 0f) return;
            Vector3 direction = _vel.sqrMagnitude > .01f ? _vel : root.forward;
            float correction = 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                LegSlot slot = legs[i];
                if (slot.leg == null) continue;
                if (slot.swinging) Retarget(slot);
                Vector3 point = slot.planted;
                Vector3 normal = slot.plantedNormal;
                if (slot.swinging)
                {
                    // Bound the foot at this frame's actual swing position, not
                    // its old planted location after it has already lifted off.
                    float t = Mathf.Clamp01(slot.swingT + dt / slot.swingDuration);
                    float e = t * t * (3f - 2f * t);
                    normal = Vector3.Slerp(slot.swingFromNormal, slot.swingToNormal, e).normalized;
                    point = Vector3.LerpUnclamped(slot.swingFrom, slot.swingTo, e)
                        + normal * (Mathf.Sin(Mathf.PI * t) * slot.swingHeight);
                }
                correction = Mathf.Min(correction, slot.leg.AvailableLift(point, normal, direction, _up, reachUse));
            }
            correction = Mathf.Max(correction, -_raiseHeight);
            if (correction < 0f)
            {
                _raiseHeight += correction;
                body.position += _up * correction;
            }
        }
    }
}
