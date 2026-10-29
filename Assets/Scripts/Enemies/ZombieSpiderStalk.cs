using UnityEngine;

namespace Game.AI
{
    /// <summary>Passive approach while wandering, ticked by the authority after perception.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy), typeof(ClimbLocomotion))]
    public class ZombieSpiderStalk : MonoBehaviour
    {
        [Tooltip("Fraction of walking speed used while stalking. Chase speed is unchanged.")]
        [SerializeField, Range(0.05f, 1f)] float speedMultiplier = 0.3f;
        [Tooltip("Seconds between choosing the nearest player and refreshing the approach path.")]
        [SerializeField, Min(0.1f)] float repathInterval = 2f;
        [Tooltip("Maximum seconds spent on a passive approach before giving up. Seeing a player still takes priority.")]
        [SerializeField, Min(0.1f)] float stalkTimeout = 5f;
        [Tooltip("Seconds of normal wandering after a failed approach before stalking can resume.")]
        [SerializeField, Min(0.1f)] float wanderAfterTimeout = 5f;

        public bool IsStalking { get; private set; }
        public float SpeedMultiplier => Mathf.Clamp(speedMultiplier, 0.05f, 1f);

        float _repathTimer;
        float _stalkTimer;
        float _wanderTimer;

        public void ResetStalk()
        {
            IsStalking = false;
            _repathTimer = 0f;
            _stalkTimer = 0f;
            _wanderTimer = 0f;
        }

        public void StopStalking(Enemy enemy)
        {
            if (IsStalking && enemy.Locomotion != null)
            {
                // Cancel idle recovery movement as well as pending paths before aggro takes over.
                enemy.Locomotion.Reset();
                enemy.Locomotion.BeginWanderPause();
            }
            ResetStalk();
        }

        void OnDisable()
        {
            Enemy enemy = GetComponent<Enemy>();
            if (enemy != null) StopStalking(enemy);
            else ResetStalk();
        }

        // This never sets the brain's combat target, seen position, or state.
        // Perception remains free to acquire any visible player before this runs.
        public bool TickStalk(Enemy enemy, EnemyManager manager, float dt)
        {
            if (!enemy.HasAuthority || enemy.IsDormant || enemy.CurrentState != Enemy.State.Wander
                || enemy.Root == null || enemy.Locomotion == null) return false;

            if (_wanderTimer > 0f)
            {
                _wanderTimer = Mathf.Max(0f, _wanderTimer - dt);
                return false;
            }

            // Count the whole attempt, including failed/pending paths and recovery.
            // Repathing or choosing a different nearby player must not restart it.
            if (IsStalking)
            {
                _stalkTimer += dt;
                if (_stalkTimer >= Mathf.Max(0.1f, stalkTimeout))
                {
                    StopStalking(enemy);
                    _wanderTimer = Mathf.Max(0.1f, wanderAfterTimeout);
                    enemy.Locomotion.RequestWander(manager);
                    return false;
                }
            }

            float nearestSq = float.PositiveInfinity;
            Vector3 destination = default;
            bool found = false;
            for (int slot = 0; slot < EnemyManager.MaxTargets; slot++)
            {
                if (!manager.TryGetTarget(slot, out Vector3 point)) continue;
                if (enemy.IsFlareProtected(point)) continue;
                float sq = (point - enemy.Root.position).sqrMagnitude;
                if (sq >= nearestSq) continue;
                nearestSq = sq;
                destination = point;
                found = true;
            }

            if (!found)
            {
                StopStalking(enemy);
                return false;
            }

            IEnemyLocomotion locomotion = enemy.Locomotion;
            if (!IsStalking)
            {
                locomotion.ClearPath();
                IsStalking = true;
            }

            _repathTimer -= dt;
            // Keep the normal idle pauses between completed approach paths.
            if (locomotion.PathDone && !locomotion.WantsPath && !locomotion.TickWanderPause(dt))
                return true;
            if (_repathTimer > 0f || locomotion.WantsPath) return true;

            _repathTimer = Mathf.Max(0.1f, repathInterval);
            locomotion.RequestPathToWorld(manager, destination);
            locomotion.BeginWanderPause();
            return true;
        }
    }
}
