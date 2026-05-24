using CaptureTool.Application.Abstractions.UseCases.VideoEdit;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Clipboard;

namespace CaptureTool.Application.UseCases.VideoEdit;

public sealed partial class VideoEditCopyUseCase : AsyncUseCase<string>, IVideoEditCopyUseCase
{
    private readonly IClipboardService _clipboardService;

    public VideoEditCopyUseCase(IClipboardService clipboardService)
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
