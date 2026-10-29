using Liv.Lck.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckStreamingWaitingForConfigureState : LckStreamingBaseState
    {
        public override void EnterState(LckStreamingController controller)
        {
            controller.ShowNotification(Tablet.NotificationType.ConfigureStream);
            _ = CheckConfiguredState(controller, controller.CancellationTokenSource.Token);
        }

        private async Task CheckConfiguredState(LckStreamingController controller, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                controller.Log("currently waiting for configured state");

                var result = await controller.LckCore.HasUserConfiguredStreaming();

                if (result.IsOk == false)
                {
                    switch (result.Err)
                    {
                        case CoreError.UserNotLoggedIn:
                            // go back to login show code state
                            controller.SwitchState(controller.ShowCodeState);
                            return;
                        case CoreError.InternalError:
                            controller.LogError($"Internal error checking if user is Configured: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.InternalErrorState);
                            return;
                        case CoreError.InvalidArgument:
                            controller.LogError($"Invalid Argument error checking if user is Configured: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.InvalidArgumentState);
                            return;
                        case CoreError.MissingTrackingId:
                            controller.LogError($"MissingTrackingId error please make sure Tracking ID is setup correctly in LCK Settings: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.MissingTrackingIdState);
                            return;
                        case CoreError.RateLimiterBackoff:
                            controller.LogError($"Too many requests sent to our backend error: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.RateLimiterBackoffState);
                            return;
                        case CoreError.ServiceUnavailable:
                            controller.LogError($"Unable to reach our backend error: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.ServiceUnavailableState);
                            return;
                        default:
                            controller.LogError("Tried to check an LCKCore Error missing from this switch statement");
                            await Task.Delay(5000, cancellationToken);
                            controller.SwitchState(controller.GetCurrentState);
                            return;
                    }
                }
                else
                {
                    if (result.Ok)
                    {
                        // logged in and stream config is good
                        controller.SwitchState(controller.ConfiguredCorrectlyState);
                        return;
                    }
                    else
                    {
                        // logged in but stream not configured yet
                    }
                }

                await Task.Delay(2500, cancellationToken);
            }
        }
    }
}
