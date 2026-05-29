using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Clipboard;

namespace CaptureTool.Application.Features.VideoEdit.CopyVideoFile;

public sealed class CopyVideoFileUseCase : IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>, IConditional<CopyVideoFileRequest>
{
    private readonly IClipboardService _clipboardService;

    public CopyVideoFileUseCase(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public Task<bool> CanExecuteAsync(CopyVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(request.VideoPath));
    }

    public async Task<CopyVideoFileResponse> ExecuteAsync(CopyVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.VideoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardVideo = new(request.VideoPath);
        Task task = _clipboardService.CopyFileAsync(clipboardVideo);
        await task.WaitAsync(cancellationToken);
        return new CopyVideoFileResponse();
    }
}