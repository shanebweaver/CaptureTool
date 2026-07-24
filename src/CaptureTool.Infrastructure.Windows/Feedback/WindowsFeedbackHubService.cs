using CaptureTool.Application.Abstractions.Feedback;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using Windows.System;

namespace CaptureTool.Infrastructure.Windows.Feedback;

public sealed class WindowsFeedbackHubService : IFeedbackHubService
{
    private const string ActivityId = $"{nameof(WindowsFeedbackHubService)}.Launch";
    private static readonly Uri FeedbackHubUri = new("feedback-hub:");

    private readonly ILogService _logService;
    private readonly ITelemetryService? _telemetryService;

    public WindowsFeedbackHubService(
        ILogService logService,
        ITelemetryService? telemetryService = null)
    {
        _logService = logService;
        _telemetryService = telemetryService;
    }

    public async Task<bool> LaunchAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logService.LogInformation($"Activity initiated: {ActivityId}");

            cancellationToken.ThrowIfCancellationRequested();
            bool launched = await Launcher.LaunchUriAsync(FeedbackHubUri);
            cancellationToken.ThrowIfCancellationRequested();

            _logService.LogInformation($"Activity completed: {ActivityId}");
            TrackOpened(launched);
            return launched;
        }
        catch (Exception e)
        {
            _logService.LogException(e, $"Activity error: {ActivityId}");
            TrackOpened(false);
            return false;
        }
    }

    private void TrackOpened(bool launched)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.FeedbackOpened,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Outcome] = launched
                    ? TelemetryOutcomes.Succeeded
                    : TelemetryOutcomes.Failed
            });
    }
}
