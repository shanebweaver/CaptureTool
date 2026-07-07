using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;

internal sealed class GetRecentCapturesUseCase : IGetRecentCapturesUseCase
{
    private const string ActivityId = "GetRecentCaptures";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStorageService _storageService;
    private readonly IFileSystem _fileSystem;
    private readonly IAudioCaptureFeatureAvailability _audioCaptureFeatureAvailability;

    public GetRecentCapturesUseCase(IStorageService storageService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureFeatureAvailability audioCaptureFeatureAvailability)
    {
        _useCaseExecutor = useCaseExecutor;
        _storageService = storageService;
        _fileSystem = fileSystem;
        _audioCaptureFeatureAvailability = audioCaptureFeatureAvailability;
    }

    public bool CanExecute(GetRecentCapturesRequest request)
    {
        return true;
    }

    public Task<UseCaseResponse<GetRecentCapturesResponse>> ExecuteAsync(GetRecentCapturesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                string recentCapturesFolder = _storageService.GetApplicationTemporaryFolderPath();

                IReadOnlyList<RecentCapture> recentCaptures = _fileSystem.EnumerateFiles(recentCapturesFolder, "*.*")
                .OrderByDescending(_fileSystem.GetLastWriteTimeUtc)
                .Where(filePath => !string.IsNullOrEmpty(filePath) && _fileSystem.FileExists(filePath))
                .Select(filePath => new RecentCapture(
                filePath,
                Path.GetFileName(filePath),
                CaptureFileTypeDetector.DetectFileType(filePath)))
                .Where(capture => ShouldIncludeRecentCapture(capture.CaptureFileType))
                .Take(5)
                .ToArray();

                return new GetRecentCapturesResponse(recentCaptures);
            },
            cancellationToken: cancellationToken);
    }

    private bool ShouldIncludeRecentCapture(CaptureFileType captureFileType)
    {
        return captureFileType != CaptureFileType.Audio ||
            _audioCaptureFeatureAvailability.IsAudioCaptureEnabled;
    }
}
