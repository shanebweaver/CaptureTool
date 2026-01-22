using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.VideoEdit;
using CaptureTool.Infrastructure.Interfaces.Clipboard;

namespace CaptureTool.Application.Implementations.Actions.VideoEdit;

public sealed partial class VideoEditCopyAction : AsyncActionCommand<string>, IVideoEditCopyAction
{
    private readonly IClipboardService _clipboardService;

    public VideoEditCopyAction(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }


    public override async Task ExecuteAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardVideo = new(videoPath);
        await _clipboardService.CopyFileAsync(clipboardVideo);
    }
}
