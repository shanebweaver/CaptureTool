using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Capture.Image;

internal sealed class ImageCaptureWorkflow : IImageCaptureWorkflow, IImageCaptureState
{
    private readonly IStorageService _storageService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly IScreenCapture _screenCapture;
    private readonly ImageCapturePostProcessor _postProcessor;
    private readonly ImageCaptureFileNameGenerator _fileNameGenerator;
    private readonly ITelemetryService? _telemetryService;

    public event EventHandler<ImageFile>? NewImageCaptured;

    public ImageCaptureWorkflow(
        IStorageService storageService,
        CaptureFileAllocator fileAllocator,
        IScreenCapture screenCapture,
        ImageCapturePostProcessor postProcessor,
        ImageCaptureFileNameGenerator fileNameGenerator,
        ITelemetryService? telemetryService = null)
    {
        _storageService = storageService;
        _fileAllocator = fileAllocator;
        _screenCapture = screenCapture;
        _postProcessor = postProcessor;
        _fileNameGenerator = fileNameGenerator;
        _telemetryService = telemetryService;
    }

    public ImageFile CaptureAllScreens()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        return CaptureMonitors(monitors);
    }

    public ImageFile CaptureMonitors(IReadOnlyList<MonitorCaptureResult> monitors)
    {
        TrackCapture(TelemetryEvents.CaptureRequested, "all_screens");

        string? tempPath = null;
        bool captureCreated = false;
        try
        {
            TrackCapture(TelemetryEvents.CaptureStarted, "all_screens");
            tempPath = _fileAllocator.ReserveUniqueFile(
                _storageService.GetApplicationRetainedCaptureFolderPath(),
                _fileNameGenerator.GetNewCaptureFileName);

            using System.Drawing.Image combined = _screenCapture.CombineMonitors([.. monitors]);
            _screenCapture.SaveImageToFile(combined, tempPath);
            captureCreated = true;

            ImageFile imageFile = CompleteCapture(new ImageFile(tempPath));
            TrackCapture(TelemetryEvents.CaptureCompleted, "all_screens", TelemetryOutcomes.Succeeded);
            return imageFile;
        }
        catch
        {
            if (!captureCreated && tempPath is not null)
            {
                _fileAllocator.TryDeleteFile(tempPath);
            }

            TrackCapture(TelemetryEvents.CaptureFailed, "all_screens", TelemetryOutcomes.Failed);
            throw;
        }
    }

    public ImageFile CaptureImage(NewCaptureArgs args)
    {
        string captureType = args.CaptureType.ToString();
        TrackCapture(TelemetryEvents.CaptureRequested, captureType);

        string? tempPath = null;
        bool captureCreated = false;
        try
        {
            TrackCapture(TelemetryEvents.CaptureStarted, captureType);
            tempPath = _fileAllocator.ReserveUniqueFile(
                _storageService.GetApplicationRetainedCaptureFolderPath(),
                _fileNameGenerator.GetNewCaptureFileName);

            MonitorCaptureResult monitor = args.Monitor;
            Rectangle area = args.Area;
            using Bitmap image = _screenCapture.CreateBitmapFromMonitorCaptureResult(monitor);
            using Bitmap cropped = _screenCapture.CreateCroppedBitmap(image, area, monitor.Scale);
            _screenCapture.SaveImageToFile(cropped, tempPath);
            captureCreated = true;

            ImageFile imageFile = CompleteCapture(new ImageFile(tempPath));
            TrackCapture(TelemetryEvents.CaptureCompleted, captureType, TelemetryOutcomes.Succeeded);
            return imageFile;
        }
        catch
        {
            if (!captureCreated && tempPath is not null)
            {
                _fileAllocator.TryDeleteFile(tempPath);
            }

            TrackCapture(TelemetryEvents.CaptureFailed, captureType, TelemetryOutcomes.Failed);
            throw;
        }
    }

    private ImageFile CompleteCapture(ImageFile imageFile)
    {
        _postProcessor.Process(imageFile);
        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
    }

    private void TrackCapture(
        string eventName,
        string captureType,
        string? outcome = null)
    {
        var properties = new Dictionary<string, object?>
        {
            [TelemetryProperties.MediaType] = "image",
            [TelemetryProperties.CaptureType] = captureType
        };

        if (outcome is not null)
        {
            properties[TelemetryProperties.Outcome] = outcome;
        }

        _telemetryService?.TrackEvent(eventName, properties);
    }
}
