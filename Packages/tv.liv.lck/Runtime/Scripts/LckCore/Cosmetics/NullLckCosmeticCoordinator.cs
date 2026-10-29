using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace Liv.Lck.Core.Cosmetics
{
    /// <summary>
    /// A default, "do nothing" implementation of the ILckCosmeticsCoordinator.
    /// </summary>
    [Preserve]
    public class NullLckCosmeticsCoordinator : ILckCosmeticsCoordinator
    {
#pragma warning disable CS0067
        public event Action<LckAvailableCosmeticInfo> OnCosmeticAvailable;
#pragma warning restore CS0067
       
        [Preserve]
        public NullLckCosmeticsCoordinator() {}
        
        public Task InitializeLocalCosmeticsAsync()
        {
            return Task.CompletedTask;
        }

        public Task<Result<bool>> GetUserCosmeticsForSessionAsync(IEnumerable<string> playerIds, string sessionId)
        {
            return Task.FromResult(Result<bool>.NewSuccess(true));
        }

        public Task<Result<bool>> AnnouncePlayerPresenceForSessionAsync(string playerId, string sessionId)
        {
            return Task.FromResult(Result<bool>.NewSuccess(true));
        }
    }
}