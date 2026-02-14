using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.AppMenu;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;

namespace CaptureTool.Application.Implementations.UseCases.AppMenu;

public sealed class AppMenuUseCases : IAppMenuUseCases
{
    private readonly IStorageService _storageService;
    private readonly IImageCaptureHandler _imageCaptureHandler;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppNavigation _appNavigation;
    private readonly IShutdownHandler _shutdownHandler;
    private readonly IWindowHandleProvider _windowingService;
    private readonly IFileTypeDetector _fileTypeDetector;

    public AppMenuUseCases(
        IStorageService storageService,
        IImageCaptureHandler imageCaptureHandler,
        IVideoCaptureHandler videoCaptureHandler,
        IFilePickerService filePickerService,
        IAppNavigation appNavigation,
        IShutdownHandler shutdownHandler,
        IWindowHandleProvider windowingService,
        IFileTypeDetector fileTypeDetector)
    {
        _storageService = storageService;
        _imageCaptureHandler = imageCaptureHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _filePickerService = filePickerService;
        _appNavigation = appNavigation;
        _shutdownHandler = shutdownHandler;
        _windowingService = windowingService;
        _fileTypeDetector = fileTypeDetector;
    }

    public Task<IEnumerable<IRecentCapture>> LoadRecentCapturesAsync(CancellationToken ct)
    {
        string recentCapturesFolder = _storageService.GetApplicationTemporaryFolderPath();

        var recentCaptureFiles = Directory.GetFiles(recentCapturesFolder, "*.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(5);

        var recentCaptures = recentCaptureFiles
            .Where(filePath => !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            .Select(filePath => new RecentCapture(
                filePath,
                Path.GetFileName(filePath),
                _fileTypeDetector.DetectFileType(filePath)))
            .Cast<IRecentCapture>()
            .ToList();

        return Task.FromResult<IEnumerable<IRecentCapture>>(recentCaptures);
    }

    public Task OpenRecentCaptureAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileType = _fileTypeDetector.DetectFileType(filePath);
        switch (fileType)
        {
            case CaptureFileType.Image:
                ImageFile imageFile = new(filePath);
                _appNavigation.GoToImageEdit(imageFile);
                break;

            case CaptureFileType.Video:
                VideoFile videoFile = new(filePath);
                _appNavigation.GoToVideoEdit(videoFile);
                break;

            case CaptureFileType.Audio:
                AudioFile audioFile = new(filePath);
                _appNavigation.GoToAudioEdit(audioFile);
                break;

            default:
                throw new InvalidOperationException($"Unknown file type: {fileType}");
        }

        return Task.CompletedTask;
    }

    public async Task OpenFileAsync(CancellationToken ct)
    {
        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures)
            ?? throw new OperationCanceledException("No file was selected.");
        
        var fileType = _fileTypeDetector.DetectFileType(file.FilePath);
        switch (fileType)
        {
            case CaptureFileType.Image:
                _appNavigation.GoToImageEdit(new ImageFile(file.FilePath));
                break;

            case CaptureFileType.Audio:
                _appNavigation.GoToAudioEdit(new AudioFile(file.FilePath));
                break;

            default:
                throw new InvalidOperationException($"Unsupported file type: {fileType}");
        }
    }

    public void NewImageCapture()
    {
        _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
    }

    public void NavigateToSettings()
    {
        _appNavigation.GoToSettings();
    }

    public void ShowAboutApp()
    {
        _appNavigation.GoToAbout();
    }

    public void ShowAddOns()
    {
        _appNavigation.GoToAddOns();
    }

    public void ExitApplication()
    {
        _shutdownHandler.Shutdown();
    }
}
