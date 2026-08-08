namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public sealed record ImageDescriptionModelDescriptor(
    string ProducerId,
    string ModelId,
    string? ModelVersion,
    string RuntimeId,
    string? RuntimeVersion,
    string? PackageVersion);

public enum ImageDescriptionAnalysisStatus
{
    Unknown,
    Succeeded,
    PreparationRequired,
    Unsupported,
    Disabled,
    BlockedByPolicy,
    BlockedByContentSafety,
    InputTooLarge,
    Cancelled,
    TransientFailure,
    TerminalFailure,
}

public sealed record ImageDescriptionAnalysisResult
{
    private ImageDescriptionAnalysisResult(
        ImageDescriptionAnalysisStatus status,
        string description)
    {
        Status = status;
        Description = description;
    }

    public ImageDescriptionAnalysisStatus Status { get; }

    public string Description { get; }

    public static ImageDescriptionAnalysisResult Succeeded(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new(ImageDescriptionAnalysisStatus.Succeeded, description);
    }

    public static ImageDescriptionAnalysisResult PreparationRequired { get; } = new(
        ImageDescriptionAnalysisStatus.PreparationRequired,
        string.Empty);

    public static ImageDescriptionAnalysisResult Unsupported { get; } = new(
        ImageDescriptionAnalysisStatus.Unsupported,
        string.Empty);

    public static ImageDescriptionAnalysisResult Disabled { get; } = new(
        ImageDescriptionAnalysisStatus.Disabled,
        string.Empty);

    public static ImageDescriptionAnalysisResult BlockedByPolicy { get; } = new(
        ImageDescriptionAnalysisStatus.BlockedByPolicy,
        string.Empty);

    public static ImageDescriptionAnalysisResult BlockedByContentSafety { get; } = new(
        ImageDescriptionAnalysisStatus.BlockedByContentSafety,
        string.Empty);

    public static ImageDescriptionAnalysisResult InputTooLarge { get; } = new(
        ImageDescriptionAnalysisStatus.InputTooLarge,
        string.Empty);

    public static ImageDescriptionAnalysisResult Cancelled { get; } = new(
        ImageDescriptionAnalysisStatus.Cancelled,
        string.Empty);

    public static ImageDescriptionAnalysisResult TransientFailure { get; } = new(
        ImageDescriptionAnalysisStatus.TransientFailure,
        string.Empty);

    public static ImageDescriptionAnalysisResult TerminalFailure { get; } = new(
        ImageDescriptionAnalysisStatus.TerminalFailure,
        string.Empty);
}

public enum ImageDescriptionAnalysisPreparationStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    Disabled,
    Cancelled,
    TransientFailure,
    TerminalFailure,
}

public sealed record ImageDescriptionAnalysisPreparationResult
{
    private ImageDescriptionAnalysisPreparationResult(
        ImageDescriptionAnalysisPreparationStatus status)
    {
        Status = status;
    }

    public ImageDescriptionAnalysisPreparationStatus Status { get; }

    public static ImageDescriptionAnalysisPreparationResult Succeeded { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.Succeeded);

    public static ImageDescriptionAnalysisPreparationResult Unsupported { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.Unsupported);

    public static ImageDescriptionAnalysisPreparationResult Disabled { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.Disabled);

    public static ImageDescriptionAnalysisPreparationResult Cancelled { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.Cancelled);

    public static ImageDescriptionAnalysisPreparationResult TransientFailure { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.TransientFailure);

    public static ImageDescriptionAnalysisPreparationResult TerminalFailure { get; } = new(
        ImageDescriptionAnalysisPreparationStatus.TerminalFailure);
}

public interface IImageDescriptionAnalysisService
{
    ImageDescriptionModelDescriptor ModelDescriptor { get; }

    ImageDescriptionReadyState GetReadyState();

    Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
        Stream sourceImage,
        CancellationToken cancellationToken = default);
}
