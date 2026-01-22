using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.VideoEdit;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;

namespace CaptureTool.Application.Implementations.UseCases.VideoEdit;

public sealed partial class VideoEditSaveUseCase : AsyncActionCommand<string>, IVideoEditSaveUseCase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;

    public VideoEditSaveUseCase(
        IFilePickerService filePickerService,
        IWindowHandleProvider windowingService)
    {
        _filePickerService = filePickerService;
        _windowingService = windowingService;
    }


    public override async Task ExecuteAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            throw new InvalidOperationException("Cannot save video without a valid filepath.");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Video, UserFolder.Videos)
            ?? throw new OperationCanceledException("No file was selected.");
    
        File.Copy(videoPath, file.FilePath, true);
    }
}
