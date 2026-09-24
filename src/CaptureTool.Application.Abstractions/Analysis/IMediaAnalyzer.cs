using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public enum AnalyzerAvailability
{
    Ready,
    PreparationRequired,
    Unsupported,
    TemporarilyUnavailable,
}

public enum AnalysisProgressStage
{
    Preparing,
    Analyzing,
}

public sealed record AnalysisProgress
{
    public AnalysisProgressStage Stage { get; }
    public double? Fraction { get; }

    public AnalysisProgress(AnalysisProgressStage stage, double? fraction = null)
    {
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        if (fraction is { } value && (!double.IsFinite(value) || value < 0 || value > 1))
            throw new ArgumentOutOfRangeException(nameof(fraction));
        Stage = stage;
        Fraction = fraction;
    }
}

/// <summary>Only provided after authorization and source verification. Providers must not mutate the source.</summary>
public sealed record AnalysisInput(CaptureId CaptureId, AnalysisMediaKind MediaKind,
    SourceRevision SourceRevision, string SourcePath, string? Language = null);

public sealed class MediaAnalyzerDescriptor
{
    public string Id { get; }
    public AnalysisCapability Capability { get; }
    public IReadOnlyList<AnalysisMediaKind> SupportedMedia { get; }

    public MediaAnalyzerDescriptor(string id, AnalysisCapability capability, IEnumerable<AnalysisMediaKind> supportedMedia)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(supportedMedia);
        AnalysisMediaKind[] media = supportedMedia.ToArray();
        if (media.Length == 0 || media.Any(kind => !Enum.IsDefined(kind)) || media.Distinct().Count() != media.Length)
            throw new ArgumentException("Specify distinct supported media kinds.", nameof(supportedMedia));
        Id = id;
        Capability = capability;
        SupportedMedia = Array.AsReadOnly(media);
    }
}

/// <summary>One on-device model/capability adapter. It owns no policy, persistence, or UI.</summary>
public interface IMediaAnalyzer
{
    MediaAnalyzerDescriptor Descriptor { get; }

    /// <summary>Side-effect-free probe; must not acquire models or inspect source content.</summary>
    ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind mediaKind, string? language,
        CancellationToken cancellationToken);

    Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken);

    Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken);
}
