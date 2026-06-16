using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.About.OpenAboutPage;

public sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private const string ActivityId = "OpenAboutPage";

    private readonly INavigationService _navigationService;
    private readonly ITelemetryService _telemetryService;

    public OpenAboutPageUseCase(
        INavigationService navigationService,
        ITelemetryService telemetryService)
    {
        _navigationService = navigationService;
        _telemetryService = telemetryService;
    }

    public Task<UseCaseResponse<OpenAboutPageResponse>> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        _telemetryService.ActivityInitiated(ActivityId);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _telemetryService.ActivityCanceled(ActivityId);
                return UseCaseResponse<OpenAboutPageResponse>.CancelledAsync();
            }

            _navigationService.Navigate(NavigationRoute.About);

            _telemetryService.ActivityCompleted(ActivityId);
            return UseCaseResponse<OpenAboutPageResponse>.SuccessAsync(new OpenAboutPageResponse());
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(ActivityId, exception.Message);
            return UseCaseResponse<OpenAboutPageResponse>.CancelledAsync();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(ActivityId, exception);
            return UseCaseResponse<OpenAboutPageResponse>.FailureAsync();
        }
    }
}
