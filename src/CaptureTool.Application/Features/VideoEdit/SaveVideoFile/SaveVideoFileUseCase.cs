using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.VideoEdit.SaveVideoFile;

public sealed class SaveVideoFileUseCase : ISaveVideoFileUseCase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IVideoFileTrimmer _videoFileTrimmer;

    public SaveVideoFileUseCase(
        IFilePickerService filePickerService,
        IVideoFileTrimmer videoFileTrimmer)
    {
        _filePickerService = filePickerService;
        _videoFileTrimmer = videoFileTrimmer;
    }

    public bool CanExecute(SaveVideoFileRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.VideoPath);
    }

    public async Task<SaveVideoFileResponse> ExecuteAsync(SaveVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.VideoPath) || !File.Exists(request.VideoPath))
            {
                return new SaveVideoFileResponse(false);
            }

            IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Video, UserFolder.Videos);
            if (file is null)
            {
                return new SaveVideoFileResponse(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new SaveVideoFileResponse(false);
            }

            if (TryGetTrim(request, out TimeSpan trimStart, out TimeSpan trimEnd))
            {
                await _videoFileTrimmer.TrimAsync(
                    request.VideoPath,
                    file.FilePath,
                    trimStart,
                    trimEnd,
                    cancellationToken);
            }
            else
            {
                File.Copy(request.VideoPath, file.FilePath, true);
            }

            return new SaveVideoFileResponse();
        }
        catch (Exception)
        {
            return new SaveVideoFileResponse(false);
        }
    }

    private static bool TryGetTrim(SaveVideoFileRequest request, out TimeSpan trimStart, out TimeSpan trimEnd)
    {
        trimStart = request.TrimStart.GetValueOrDefault();
        trimEnd = request.TrimEnd.GetValueOrDefault();
        return request.TrimStart.HasValue &&
            request.TrimEnd.HasValue &&
            trimEnd > trimStart;
    }
}
