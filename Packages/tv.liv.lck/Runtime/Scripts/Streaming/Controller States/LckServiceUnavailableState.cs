using Liv.Lck.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckServiceUnavailableState : LckStreamingBaseState
    {
        private static int _enterServiceUnavailableStateCount = 0;

        public override void EnterState(LckStreamingController controller)
        {
            if (_enterServiceUnavailableStateCount < 5)
            {
                _enterServiceUnavailableStateCount++;
            }

            controller.ShowNotification(Tablet.NotificationType.ServiceUnavailable);
            _ = CheckServiceStatus(controller, controller.CancellationTokenSource.Token);
        }

        private async Task CheckServiceStatus(LckStreamingController controller, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                controller.Log("currently checking Service Unavailable state");

                if (_enterServiceUnavailableStateCount >= 2)
                {
                    await Task.Delay(10000, cancellationToken);
                }

                var result = await controller.LckCore.HasUserConfiguredStreaming();

                if (result.IsOk == false)
                {
                    switch (result.Err)
                    {
                        case CoreError.UserNotLoggedIn:
                            // managed to reach backend again, go back to login show code state
                            controller.SwitchState(controller.ShowCodeState);
                            return;
                        case CoreError.InternalError:
                            controller.LogError($"Internal error checking backend: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.InternalErrorState);
                            break;
                        case CoreError.InvalidArgument:
                            controller.LogError($"Invalid Argument error checking HasUserConfiguredStreaming: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.InvalidArgumentState);
                            return;
                        case CoreError.MissingTrackingId:
                            controller.LogError($"MissingTrackingId error please make sure Tracking ID is setup correctly in LCK Settings: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.MissingTrackingIdState);
                            return;
                        case CoreError.ServiceUnavailable:
                            // can't reach backend still, continue checking in this state
                            break;
                        case CoreError.RateLimiterBackoff:
                            controller.LogError($"Too many requests sent to our backend error: {result.Err} - {result.Message}");
                            controller.SwitchState(controller.RateLimiterBackoffState);
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
                        // user is logged in but isn't configured yet
                        controller.SwitchState(controller.WaitingForConfigureState);
                        return;
                    }
                }

                await Task.Delay(7000, cancellationToken);
            }
        }
    }
}
