using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Infrastructure.Abstractions.Clipboard;

namespace CaptureTool.Application.VideoEdit;

internal class VideoEditCopyAppCommand : IVideoEditCopyAppCommand
{
    private readonly IClipboardService _clipboardService;

    public VideoEditCopyAppCommand(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(string parameter)
    {
        return !string.IsNullOrWhiteSpace(parameter);
    }

    public async Task ExecuteAsync(string videoPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardVideo = new(videoPath);
        Task t = _clipboardService.CopyFileAsync(clipboardVideo);
        await t.WaitAsync(cancellationToken);
    }
}
