using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Capture.Image;

internal sealed class ImageCaptureWorkflow : IImageCaptureWorkflow, IImageCaptureState
{
    private readonly IStorageService _storageService;
    private readonly IScreenCapture _screenCapture;
    private readonly ImageCapturePostProcessor _postProcessor;
    private readonly ImageCaptureFileNameGenerator _fileNameGenerator;

    public event EventHandler<ImageFile>? NewImageCaptured;

    public ImageCaptureWorkflow(
        IStorageService storageService,
        IScreenCapture screenCapture,
        ImageCapturePostProcessor postProcessor,
        ImageCaptureFileNameGenerator fileNameGenerator)
    {
        _storageService = storageService;
        _screenCapture = screenCapture;
        _postProcessor = postProcessor;
        _fileNameGenerator = fileNameGenerator;
    }

    public ImageFile CaptureAllScreens()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        return CaptureMonitors(monitors);
    }

    public ImageFile CaptureMonitors(IReadOnlyList<MonitorCaptureResult> monitors)
    {
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName()
        );

        using System.Drawing.Image combined = _screenCapture.CombineMonitors([.. monitors]);
        _screenCapture.SaveImageToFile(combined, tempPath);

        return CompleteCapture(new ImageFile(tempPath));
    }

    public ImageFile CaptureImage(NewCaptureArgs args)
    {
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName()
        );

        MonitorCaptureResult monitor = args.Monitor;
        Rectangle area = args.Area;
        using Bitmap image = _screenCapture.CreateBitmapFromMonitorCaptureResult(monitor);
        using Bitmap cropped = _screenCapture.CreateCroppedBitmap(image, area, monitor.Scale);
        _screenCapture.SaveImageToFile(cropped, tempPath);

        return CompleteCapture(new ImageFile(tempPath));
    }

    private ImageFile CompleteCapture(ImageFile imageFile)
    {
        _postProcessor.Process(imageFile);
        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
    }
}
