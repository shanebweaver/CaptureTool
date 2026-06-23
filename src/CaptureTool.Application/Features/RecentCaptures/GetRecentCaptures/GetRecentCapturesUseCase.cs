using CaptureTool.Application.Abstractions.Features.RecentCaptures;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;

public sealed class GetRecentCapturesUseCase : IGetRecentCapturesUseCase
{
    private const string ActivityId = "GetRecentCaptures";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStorageService _storageService;
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IAudioCaptureFeatureAvailability? _audioCaptureFeatureAvailability;

    public GetRecentCapturesUseCase(IStorageService storageService,
        IFileTypeDetector fileTypeDetector,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureFeatureAvailability? audioCaptureFeatureAvailability = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _storageService = storageService;
        _fileTypeDetector = fileTypeDetector;
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

                IReadOnlyList<RecentCapture> recentCaptures = Directory.GetFiles(recentCapturesFolder, "*.*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Where(filePath => !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                .Select(filePath => new RecentCapture(
                filePath,
                Path.GetFileName(filePath),
                _fileTypeDetector.DetectFileType(filePath)))
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
            _audioCaptureFeatureAvailability?.IsAudioCaptureEnabled != false;
    }
}
