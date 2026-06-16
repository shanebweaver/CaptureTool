using CaptureTool.Application.Abstractions.Features.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.About.LeaveAboutPage;

public sealed class LeaveAboutPageUseCase : ILeaveAboutPageUseCase
{
    private const string ActivityId = "LeaveAboutPage";

    private readonly INavigationService _navigationService;
    private readonly ITelemetryService _telemetryService;

    public LeaveAboutPageUseCase(
        INavigationService navigationService,
        ITelemetryService telemetryService)
    {
        _navigationService = navigationService;
        _telemetryService = telemetryService;
    }

    public Task<UseCaseResponse<LeaveAboutPageResponse>> ExecuteAsync(LeaveAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        _telemetryService.ActivityInitiated(ActivityId);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _telemetryService.ActivityCanceled(ActivityId);
                return Task.FromResult(new UseCaseResponse<LeaveAboutPageResponse> { Result = UseCaseResult.Cancelled });
            }

            if (!_navigationService.TryGoBack())
            {
                _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
            }

            _telemetryService.ActivityCompleted(ActivityId);
            return Task.FromResult(new UseCaseResponse<LeaveAboutPageResponse> { Value = new LeaveAboutPageResponse() });
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(ActivityId, exception.Message);
            return Task.FromResult(new UseCaseResponse<LeaveAboutPageResponse> { Result = UseCaseResult.Cancelled });
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(ActivityId, exception);
            return Task.FromResult(new UseCaseResponse<LeaveAboutPageResponse> { Result = UseCaseResult.Failed });
        }
    }
}
