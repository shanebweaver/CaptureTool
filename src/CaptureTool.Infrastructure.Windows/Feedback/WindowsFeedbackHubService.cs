using CaptureTool.Application.Abstractions.Feedback;
using CaptureTool.Application.Abstractions.Telemetry;
using Windows.System;

namespace CaptureTool.Infrastructure.Windows.Feedback;

public sealed class WindowsFeedbackHubService : IFeedbackHubService
{
    private const string ActivityId = $"{nameof(WindowsFeedbackHubService)}.Launch";
    private static readonly Uri FeedbackHubUri = new("feedback-hub:");

    private readonly ITelemetryService _telemetryService;

    public WindowsFeedbackHubService(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    public async Task<bool> LaunchAsync(CancellationToken cancellationToken)
    {
        try
        {
            _telemetryService.ActivityInitiated(ActivityId);

            cancellationToken.ThrowIfCancellationRequested();
            bool launched = await Launcher.LaunchUriAsync(FeedbackHubUri);
            cancellationToken.ThrowIfCancellationRequested();

            _telemetryService.ActivityCompleted(ActivityId);
            return launched;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(ActivityId, e);
            return false;
        }
    }
}
