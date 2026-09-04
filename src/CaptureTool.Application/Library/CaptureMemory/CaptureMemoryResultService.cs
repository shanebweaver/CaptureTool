using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Library.CaptureMemory;

internal sealed class CaptureMemoryResultResolver : ICaptureMemoryResultResolver
{
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly IFileSystem _fileSystem;

    public CaptureMemoryResultResolver(
        ICaptureAssetCatalog captureAssets,
        IFileSystem fileSystem)
    {
        _captureAssets = captureAssets;
        _fileSystem = fileSystem;
    }

    public ValueTask<CaptureMemoryResultLocation> ResolveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Resolving a Memory result requires a capture ID.", nameof(captureId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        CaptureAsset? asset = _captureAssets.Get(captureId);

        if (asset == null || asset.LifecycleState == CaptureAssetLifecycleState.Deleted)
        {
            return ValueTask.FromResult(CreateForgotten(captureId, asset));
        }

        string displayFileName = GetDisplayFileName(asset);
        string? currentPath;
        bool hasRetainedSource;
        try
        {
            currentPath = GetCurrentPath(asset, out hasRetainedSource);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(new CaptureMemoryResultLocation(
                asset.Id,
                CaptureMemoryResultLocationStatus.Unavailable,
                displayFileName));
        }

        CaptureMemoryResultLocationStatus status = currentPath == null
            ? CaptureMemoryResultLocationStatus.SourceMissing
            : CaptureMemoryResultLocationStatus.Available;
        bool canDeleteRetainedSource = status == CaptureMemoryResultLocationStatus.Available &&
            asset.SourceOwnership == CaptureSourceOwnership.AppOwned &&
            hasRetainedSource;
        return ValueTask.FromResult(new CaptureMemoryResultLocation(
            asset.Id,
            status,
            displayFileName,
            currentPath,
            canDeleteRetainedSource));
    }

    private string? GetCurrentPath(CaptureAsset asset, out bool hasRetainedSource)
    {
        string? preferred = asset.PreferredOpenPath;
        bool hasPreferred = preferred != null && _fileSystem.FileExists(preferred);
        hasRetainedSource = _fileSystem.FileExists(asset.RetainedSourcePath);

        return hasPreferred
            ? preferred
            : hasRetainedSource
                ? asset.RetainedSourcePath
                : null;
    }

    private static string GetDisplayFileName(CaptureAsset asset)
    {
        string? preferredFilename = asset.PreferredOpenPath == null
            ? null
            : Path.GetFileName(asset.PreferredOpenPath);
        return string.IsNullOrWhiteSpace(preferredFilename)
            ? Path.GetFileName(asset.RetainedSourcePath)
            : preferredFilename;
    }

    private static CaptureMemoryResultLocation CreateForgotten(
        CaptureId captureId,
        CaptureAsset? asset)
    {
        return new CaptureMemoryResultLocation(
            captureId,
            CaptureMemoryResultLocationStatus.Forgotten,
            asset == null ? "capture" : GetDisplayFileName(asset));
    }

}

internal sealed class OpenCaptureMemoryResultUseCase : IOpenCaptureMemoryResultUseCase
{
    private const string ActivityId = "OpenCaptureMemoryResult";

    private readonly ICaptureMemoryResultResolver _resolver;
    private readonly IOpenRecentCaptureUseCase _openRecentCapture;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenCaptureMemoryResultUseCase(
        ICaptureMemoryResultResolver resolver,
        IOpenRecentCaptureUseCase openRecentCapture,
        IUseCaseExecutor useCaseExecutor)
    {
        _resolver = resolver;
        _openRecentCapture = openRecentCapture;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(OpenCaptureMemoryResultRequest request)
    {
        return request is not null && !request.CaptureId.IsEmpty;
    }

    public Task<UseCaseResponse<OpenCaptureMemoryResultResponse>> ExecuteAsync(
        OpenCaptureMemoryResultRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            ActivityId,
            async token =>
            {
                CaptureMemoryResultLocation location = await _resolver
                    .ResolveAsync(request.CaptureId, token).ConfigureAwait(false);
                if (location.Status != CaptureMemoryResultLocationStatus.Available)
                {
                    return new OpenCaptureMemoryResultResponse(location.Status switch
                    {
                        CaptureMemoryResultLocationStatus.SourceMissing =>
                            OpenCaptureMemoryResultStatus.SourceMissing,
                        CaptureMemoryResultLocationStatus.Forgotten =>
                            OpenCaptureMemoryResultStatus.Forgotten,
                        _ => OpenCaptureMemoryResultStatus.Failed,
                    });
                }

                UseCaseResponse<OpenRecentCaptureResponse> opened = await _openRecentCapture
                    .ExecuteAsync(
                        new OpenRecentCaptureRequest(
                            location.CurrentFilePath!,
                            new CaptureEditorContext(
                                location.CurrentFilePath!,
                                request.CaptureId,
                                request.Evidence)),
                        token).ConfigureAwait(false);
                return new OpenCaptureMemoryResultResponse(opened.Value?.Opened == true
                    ? OpenCaptureMemoryResultStatus.Opened
                    : OpenCaptureMemoryResultStatus.Failed);
            },
            cancellationToken: cancellationToken);
    }
}
