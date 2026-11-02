using Liv.Lck.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Liv.Lck.Streaming
{
    public class LckStreamingShowCodeState : LckStreamingBaseState
    {
        public override void EnterState(LckStreamingController controller)
        {
            controller.SetNotificationStreamCode("Loading...");
            controller.ShowNotification(Tablet.NotificationType.EnterStreamCode);

            _ = GetCodeFromCore(controller, controller.CancellationTokenSource.Token);
        }

        private async Task GetCodeFromCore(LckStreamingController controller, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                controller.Log("currently waiting to get code from core");

                var loginResult = await controller.LckCore.StartLoginAttemptAsync();
                if (loginResult.IsOk)
                {
                    var formattedCode = loginResult.Ok.Insert(3, "-");
                    controller.SetNotificationStreamCode(formattedCode);
                    _ = WaitForUserToPairTablet(controller, controller.CancellationTokenSource.Token);
                    return;
                }
                else
                {
                    switch (loginResult.Err)
                    {
                        case CoreError.UserNotLoggedIn:
                            // continue the StartLoginAttemptAsync checks
                            break;
                        case CoreError.InternalError:
                            controller.LogError($"Internal error checking the StartLoginAttemptAsync: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.InternalErrorState);
                            return;
                        case CoreError.InvalidArgument:
                            controller.LogError($"Invalid Argument error while running StartLoginAttemptAsync: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.InvalidArgumentState);
                            return;
                        case CoreError.MissingTrackingId:
                            controller.LogError($"MissingTrackingId error please make sure Tracking ID is setup correctly in LCK Settings: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.MissingTrackingIdState);
                            return;
                        case CoreError.RateLimiterBackoff:
                            controller.LogError($"Too many requests sent to our backend error: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.RateLimiterBackoffState);
                            return;
                        case CoreError.ServiceUnavailable:
                            controller.LogError($"Unable to reach our backend error: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.ServiceUnavailableState);
                            return;
                        default:
                            controller.LogError("Tried to check an LCKCore Error missing from this switch statement");
                            await Task.Delay(5000, cancellationToken);
                            controller.SwitchState(controller.GetCurrentState);
                            return;
                    }
                }

                await Task.Delay(3000, cancellationToken);
            }
        }

        private async Task WaitForUserToPairTablet(LckStreamingController controller, CancellationToken cancellationToken)
        {
            DateTime startTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                controller.Log("currently waiting for user to pair tablet");

                DateTime currentTime = DateTime.UtcNow;
                if (currentTime - startTime >= TimeSpan.FromMinutes(15))
                {
                    LoginAttemptExpired(controller);
                    return;
                }

                var loginResult = await controller.LckCore.CheckLoginCompletedAsync();
                if (loginResult.IsOk)
                {
                    if (loginResult.Ok)
                    {
                        // Login completed, go check if configured
                        controller.SwitchState(controller.WaitingForConfigureState);
                        return;
                    }
                }
                else
                {
                    switch (loginResult.Err)
                    {
                        case CoreError.UserNotLoggedIn:
                            // continue with next CheckLoginCompletedAsync
                            break;
                        case CoreError.InternalError:
                            controller.LogError($"Internal error while running CheckLoginCompletedAsync: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.InternalErrorState);
                            return;
                        case CoreError.InvalidArgument:
                            controller.LogError($"Invalid Argument error while running CheckLoginCompletedAsync: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.InvalidArgumentState);
                            return;
                        case CoreError.MissingTrackingId:
                            controller.LogError($"MissingTrackingId error please make sure Tracking ID is setup correctly in LCK Settings: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.MissingTrackingIdState);
                            return;
                        case CoreError.LoginAttemptExpired:
                            LoginAttemptExpired(controller);
                            return;
                        case CoreError.RateLimiterBackoff:
                            controller.LogError($"Too many requests sent to our backend error: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.RateLimiterBackoffState);
                            return;
                        case CoreError.ServiceUnavailable:
                            controller.LogError($"Unable to reach our backend error: {loginResult.Err} - {loginResult.Message}");
                            controller.SwitchState(controller.ServiceUnavailableState);
                            return;
                        default:
                            controller.LogError("Tried to check an LCKCore Error missing from this switch statement");
                            await Task.Delay(5000, cancellationToken);
                            controller.SwitchState(controller.GetCurrentState);
                            return;
                    }
                }

                await Task.Delay(2500, cancellationToken);
            }
        }

        private void LoginAttemptExpired(LckStreamingController controller)
        {
            controller.Log("Login request timed out after 15 mins, switching to camera mode");
            controller.ToggleCameraPage();
        }
    }
}
