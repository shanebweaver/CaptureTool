using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Library.CaptureMemory;

public enum CaptureMemoryResultLocationStatus
{
    Unknown,
    Available,
    SourceMissing,
    Forgotten,
    Unavailable,
}

public sealed record CaptureMemoryResultLocation
{
    public CaptureMemoryResultLocation(
        CaptureId captureId,
        CaptureMemoryResultLocationStatus status,
        string displayFileName,
        string? currentFilePath = null,
        bool canDeleteRetainedSource = false)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A Memory result location requires a capture ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(status) || status == CaptureMemoryResultLocationStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayFileName);
        if (displayFileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("A display filename cannot contain a directory.", nameof(displayFileName));
        }

        if (status == CaptureMemoryResultLocationStatus.Available)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(currentFilePath);
            if (!Path.IsPathFullyQualified(currentFilePath))
            {
                throw new ArgumentException("An available Memory result requires an absolute current path.", nameof(currentFilePath));
            }
        }
        else if (currentFilePath != null)
        {
            throw new ArgumentException("An unavailable Memory result cannot expose a path.", nameof(currentFilePath));
        }

        if (canDeleteRetainedSource && status != CaptureMemoryResultLocationStatus.Available)
        {
            throw new ArgumentException(
                "Only an available Memory result can expose app-owned source deletion.",
                nameof(canDeleteRetainedSource));
        }

        CaptureId = captureId;
        Status = status;
        DisplayFileName = displayFileName;
        CurrentFilePath = currentFilePath;
        CanDeleteRetainedSource = canDeleteRetainedSource;
    }

    public CaptureId CaptureId { get; }

    public CaptureMemoryResultLocationStatus Status { get; }

    public string DisplayFileName { get; }

    public string? CurrentFilePath { get; }

    // This is a derived capability, not an ownership claim from presentation. The resolver grants
    // it only while the catalog still identifies an existing app-owned retained source.
    public bool CanDeleteRetainedSource { get; }
}

public interface ICaptureMemoryResultResolver
{
    ValueTask<CaptureMemoryResultLocation> ResolveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);
}

public sealed record OpenCaptureMemoryResultRequest
{
    public OpenCaptureMemoryResultRequest(CaptureId captureId)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Opening a Memory result requires a capture ID.", nameof(captureId));
        }

        CaptureId = captureId;
    }

    public CaptureId CaptureId { get; }
}

public enum OpenCaptureMemoryResultStatus
{
    Unknown,
    Opened,
    SourceMissing,
    Forgotten,
    Failed,
}

public sealed record OpenCaptureMemoryResultResponse
{
    public OpenCaptureMemoryResultResponse(OpenCaptureMemoryResultStatus status)
    {
        if (!Enum.IsDefined(status) || status == OpenCaptureMemoryResultStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
    }

    public OpenCaptureMemoryResultStatus Status { get; }

    public bool Opened => Status == OpenCaptureMemoryResultStatus.Opened;
}

public interface IOpenCaptureMemoryResultUseCase :
    IUseCase<OpenCaptureMemoryResultRequest, OpenCaptureMemoryResultResponse>,
    IConditional<OpenCaptureMemoryResultRequest>
{
}
