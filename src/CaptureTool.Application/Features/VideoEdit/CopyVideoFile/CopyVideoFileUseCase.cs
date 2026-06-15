using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Application.Features.VideoEdit.CopyVideoFile;

public sealed class CopyVideoFileUseCase : ICopyVideoFileUseCase
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
        try
        {
            if (string.IsNullOrEmpty(request.VideoPath) || !File.Exists(request.VideoPath))
            {
                return new CopyVideoFileResponse(false);
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
        catch (Exception)
        {
            return new CopyVideoFileResponse(false);
        }
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
