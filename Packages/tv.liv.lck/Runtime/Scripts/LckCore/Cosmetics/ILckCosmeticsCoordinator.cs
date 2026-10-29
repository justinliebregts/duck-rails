using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Liv.Lck.Core.Cosmetics
{
    public interface ILckCosmeticsCoordinator
    {
        /// <summary>
        /// Fires when cosmetic information has been found or received by the delivery service.
        /// </summary>
        event Action<LckAvailableCosmeticInfo> OnCosmeticAvailable;

        /// <summary>
        /// Asynchronously initialize the cosmetics for the local user
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that will complete when all the local user's cosmetics are ready (or have failed to
        /// download)
        /// </returns>
        Task InitializeLocalCosmeticsAsync();

        /// <summary>
        /// Asynchronously gets the cosmetics for remote users in the current session
        /// </summary>
        /// <param name="playerIds">Collection of player ids to get the cosmetics for</param>
        /// <param name="sessionId">The session id</param>
        /// <returns>
        /// A <see cref="Task"/> that will complete when all cosmetics are ready (or have failed to download)
        /// </returns>
        Task<Result<bool>> GetUserCosmeticsForSessionAsync(IEnumerable<string> playerIds, string sessionId);

        /// <summary>
        /// Asynchronously announces the local user's presence in the session
        /// </summary>
        /// <param name="playerId">The local user's player id</param>
        /// <param name="sessionId">The session id</param>
        /// <returns>
        /// A <see cref="Task"/> that will complete when the local user's presence has been announced
        /// </returns>
        Task<Result<bool>> AnnouncePlayerPresenceForSessionAsync(string playerId, string sessionId);
    }
}
