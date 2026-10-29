using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Root mover + path follower for an Enemy. Climb / ground / fly each implement this.
    /// </summary>
    public interface IEnemyLocomotion
    {
        bool PathDone { get; }
        bool WantsPath { get; }

        void Reset();
        void ClearPath();
        void BeginWanderPause();
        bool TickWanderPause(float dt);
        void RequestWander(EnemyManager m);
        void RequestPathToWorld(EnemyManager m, Vector3 worldPoint);
        void ForcePathToWorld(EnemyManager m, Vector3 worldPoint);
        void RequestRetreat(EnemyManager m, Vector3 awayDirection, float travelDistance);
        void RunPathRequest(EnemyManager m);
        void TickMove(Enemy enemy, EnemyManager m, float dt);
        void Interrupt();
    }
}
