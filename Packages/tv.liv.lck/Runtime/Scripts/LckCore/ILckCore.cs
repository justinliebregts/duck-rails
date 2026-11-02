using System.Threading.Tasks;

namespace Liv.Lck.Core
{
    /// <summary>
    /// Defines the contract for the LCK Core services, which handle user authentication,
    /// subscription status, and configuration checks with the LIV Hub application.
    /// 
    /// This interface can be injected, via '[InjectLck] ILckCore _lckCore', and used to define an alternative
    /// UX for the login and configuration process to that which the Tablet provides. It is not recommended
    /// to define a custom implementation of this interface, however.
    /// </summary>
    public interface ILckCore
    {
        /// <summary>
        /// Asynchronously checks if the logged-in user has configured their streaming settings.
        /// This is necessary to determine if streaming is available for the user.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that resolves to a <see cref="Result{T}"/> containing a boolean.
        /// The result is <c>true</c> if the user has configured streaming, <c>false</c> otherwise.
        /// The result will contain an error of type <see cref="CoreError.UserNotLoggedIn"/> if the user is not authenticated,
        /// or <see cref="CoreError.InternalError"/> if an unexpected issue occurs.
        /// </returns>
        Task<Result<bool>> HasUserConfiguredStreaming();

        /// <summary>
        /// Asynchronously checks if the currently logged-in user has an active subscription.
        /// Use this to gate features, UI, etc., that require a LIV subscription (e.g., streaming buttons).
        /// </summary>
        /// <returns>
        /// A Task that resolves to a <see cref="Result{T}"/> of type <c>bool</c>.
        /// On success, the result is <c>true</c> if the user is subscribed, and <c>false</c> otherwise.
        Task<Result<bool>> IsUserSubscribed();

        /// <summary>
        /// Asynchronously starts the user login process. This will generate a short code
        /// that you should display to the user, instructing them to enter it on the LCK website or companion app.
        /// </summary>
        /// <returns>
        /// A Task that resolves to a <see cref="Result{T}"/> of type <c>string</c>.
        /// On success, the result is the login code to be displayed to the user.
        /// </returns>
        Task<Result<string>> StartLoginAttemptAsync();

        /// <summary>
        /// Asynchronously checks if a login process, previously started with <see cref="StartLoginAttemptAsync"/>,
        /// has been successfully completed by the user. You should await this method after
        /// displaying the login code to the user.
        /// </summary>
        /// <returns>
        /// A Task that resolves to a <see cref="Result{T}"/> of type <c>bool</c>.
        /// On success, the result is <c>true</c> if the user has completed the login, and <c>false</c> otherwise.
        /// </returns>
        Task<Result<bool>> CheckLoginCompletedAsync();

        /// <summary>
        /// Asynchronously retrieves the remaining backoff time in seconds before the next backend request can be made.
        /// </summary>
        Task<Result<float>> GetRemainingBackoffTimeSeconds();
    }
}
