using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Clipboard;
using CaptureTool.Infrastructure.Abstractions.Media;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Features.VideoEdit.CopyVideoFile;

public sealed class CopyVideoFileUseCase : IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>, IConditional<CopyVideoFileRequest>
{
    private readonly IClipboardService _clipboardService;
    private readonly IStorageService _storageService;
    private readonly IVideoFileTrimmer _videoFileTrimmer;

    public CopyVideoFileUseCase(
        IClipboardService clipboardService,
        IStorageService storageService,
        IVideoFileTrimmer videoFileTrimmer)
    {
        _clipboardService = clipboardService;
        _storageService = storageService;
        _videoFileTrimmer = videoFileTrimmer;
    }

    public bool CanExecute(CopyVideoFileRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.VideoPath);
    }

    public async Task<CopyVideoFileResponse> ExecuteAsync(CopyVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.VideoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        string clipboardVideoPath = request.VideoPath;
        if (TryGetTrim(request, out TimeSpan trimStart, out TimeSpan trimEnd))
        {
            clipboardVideoPath = Path.Combine(
                _storageService.GetApplicationTemporaryFolderPath(),
                $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}.mp4");

            await _videoFileTrimmer.TrimAsync(
                request.VideoPath,
                clipboardVideoPath,
                trimStart,
                trimEnd,
                cancellationToken);
        }

        ClipboardFile clipboardVideo = new(clipboardVideoPath);
        Task task = _clipboardService.CopyFileAsync(clipboardVideo);
        await task.WaitAsync(cancellationToken);
        return new CopyVideoFileResponse();
    }

    private static bool TryGetTrim(CopyVideoFileRequest request, out TimeSpan trimStart, out TimeSpan trimEnd)
    {
        trimStart = request.TrimStart.GetValueOrDefault();
        trimEnd = request.TrimEnd.GetValueOrDefault();
        return request.TrimStart.HasValue &&
            request.TrimEnd.HasValue &&
            trimEnd > trimStart;
    }
}
