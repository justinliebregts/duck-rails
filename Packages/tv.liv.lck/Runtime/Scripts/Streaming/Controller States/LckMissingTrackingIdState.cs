using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckMissingTrackingIdState : LckStreamingBaseState
    {
        public override void EnterState(LckStreamingController controller)
        {
            controller.ShowNotification(Tablet.NotificationType.MissingTrackingId);
            _ = SwitchStateAfterDelay(controller, controller.CancellationTokenSource.Token);
        }

        private async Task SwitchStateAfterDelay(LckStreamingController controller, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(7000, cancellationToken);
                controller.SwitchState(controller.GetCurrentState);
            }
        }
    }
}
