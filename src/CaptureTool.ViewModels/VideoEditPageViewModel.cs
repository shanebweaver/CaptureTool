using CaptureTool.Capture.Desktop;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "VideoEditPageViewModel_Load";
        public static readonly string Unload = "VideoEditPageViewModel_Unload";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    private VideoFile? _videoFile;
    public VideoFile? VideoFile
    {
        get => _videoFile;
        set => Set(ref _videoFile, value);
    }

    public VideoEditPageViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            if (parameter is VideoFile videoFile)
            {
                VideoFile = videoFile;
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
        finally
        {
            cts.Dispose();
        }

        return base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            VideoFile = null;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }
}
