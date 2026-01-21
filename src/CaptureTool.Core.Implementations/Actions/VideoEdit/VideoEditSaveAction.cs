using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Windowing;

namespace CaptureTool.Core.Implementations.Actions.VideoEdit;

public sealed partial class VideoEditSaveAction : AsyncActionCommand<string>, IVideoEditSaveAction
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;

    public VideoEditSaveAction(
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
