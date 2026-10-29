using Liv.Lck.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckStreamingGetCurrentState : LckStreamingBaseState
    {
        public override void EnterState(LckStreamingController controller)
        {
            _ = GetCurrentState(controller, controller.CancellationTokenSource.Token);
        }

        private async Task GetCurrentState(LckStreamingController controller, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                controller.Log("currently waiting for get current state");

                var isConfigured = await controller.LckCore.HasUserConfiguredStreaming();

                if (isConfigured.IsOk == false)
                {
                    switch (isConfigured.Err)
                    {
                        case CoreError.UserNotLoggedIn:
                            // go back to login show code state
                            controller.SwitchState(controller.ShowCodeState);
                            return;
                        case CoreError.InternalError:
                            controller.LogError($"Internal error checking if user is Configured: {isConfigured.Err} - {isConfigured.Message}");
                            controller.SwitchState(controller.InternalErrorState);
                            return;
                        case CoreError.InvalidArgument:
                            controller.LogError($"Invalid Argument error checking if user is Configured: {isConfigured.Err} - {isConfigured.Message}");
                            controller.SwitchState(controller.InvalidArgumentState);
                            return;
                        case CoreError.MissingTrackingId:
                            controller.LogError($"MissingTrackingId error please make sure Tracking ID is setup correctly in LCK Settings: {isConfigured.Err} - {isConfigured.Message}");
                            controller.SwitchState(controller.MissingTrackingIdState);
                            return;
                        case CoreError.RateLimiterBackoff:
                            controller.LogError($"Too many requests sent to our backend error: {isConfigured.Err} - {isConfigured.Message}");
                            controller.SwitchState(controller.RateLimiterBackoffState);
                            return;
                        case CoreError.ServiceUnavailable:
                            controller.LogError($"Unable to reach our backend error: {isConfigured.Err} - {isConfigured.Message}");
                            controller.SwitchState(controller.ServiceUnavailableState);
                            return;
                        default:
                            controller.LogError("Tried to check an LCKCore Error missing from this switch statement");
                            await Task.Delay(5000, cancellationToken);
                            // repeat current state after 5s
                            break;
                    }
                }
                else
                {
                    if (isConfigured.Ok)
                    {
                        // user is configured correctly
                        controller.SwitchState(controller.ConfiguredCorrectlyState);
                        return;
                    }
                    else
                    {
                        // logged in but not configured yet
                        controller.SwitchState(controller.WaitingForConfigureState);
                        return;
                    }
                }
            }
        }
    }
}
