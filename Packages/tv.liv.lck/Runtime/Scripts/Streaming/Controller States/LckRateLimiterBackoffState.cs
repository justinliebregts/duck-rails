using System;
using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckRateLimiterBackoffState : LckStreamingBaseState
    {
        public override void EnterState(LckStreamingController controller)
        {
            controller.ShowNotification(Tablet.NotificationType.RateLimiterBackoff);
            _ = WaitForRateLimiter(controller, controller.CancellationTokenSource.Token);
        }

        private async Task WaitForRateLimiter(LckStreamingController controller, CancellationToken cancellationToken)
        { 
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await controller.LckCore.GetRemainingBackoffTimeSeconds();
                int delayInMilliseconds = 10000;

                if (result.IsOk)
                {
                    delayInMilliseconds = (int)Math.Truncate(result.Ok) * 1000;
                    controller.Log("Got remaining backoff time in milliseconds: " + delayInMilliseconds);
                }
                else
                {
                    controller.Log("Unable to get remaining backoff time, waiting 10 seconds instead");
                }

                if (delayInMilliseconds < 1000)
                {
                    controller.Log("delay was: " + delayInMilliseconds + " increasing to 3 seconds to avoid looping");
                    delayInMilliseconds = 3000;
                }

                await Task.Delay(delayInMilliseconds, cancellationToken);
                controller.SwitchState(controller.GetCurrentState);
                return;
            }
        }
    }
}
