using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Edit.Video.SaveVideoFile;

internal sealed class SaveVideoFileUseCase : ISaveVideoFileUseCase
{
    private const string ActivityId = "SaveVideoFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly IVideoFileTrimmer _videoFileTrimmer;
    private readonly IFileSystem _fileSystem;
    private readonly ICaptureNamingService? _names;

    public SaveVideoFileUseCase(IFilePickerService filePickerService,
        IVideoFileTrimmer videoFileTrimmer,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor, ICaptureNamingService? names = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _videoFileTrimmer = videoFileTrimmer;
        _fileSystem = fileSystem;
        _names = names;
    }

    public bool CanExecute(SaveVideoFileRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.VideoPath);
    }

    public Task<UseCaseResponse<SaveVideoFileResponse>> ExecuteAsync(SaveVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (string.IsNullOrEmpty(request.VideoPath) || !_fileSystem.FileExists(request.VideoPath))
                {
                    return new SaveVideoFileResponse(false);
                }

                var name = _names == null ? null : await _names.GetNameAsync(request.SourcePath ?? request.VideoPath, cancellationToken);
                FileReference? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Video, UserFolder.Videos, name?.SuggestedFileName());
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
                    _fileSystem.CopyFile(request.VideoPath, file.FilePath, true);
                }

                return new SaveVideoFileResponse();
            },
            cancellationToken: cancellationToken);
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
