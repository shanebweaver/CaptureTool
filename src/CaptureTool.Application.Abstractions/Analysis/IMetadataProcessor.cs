using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record MetadataProcessingLimits
{
    public int MaxEntries { get; }
    public int MaxCharacters { get; }
    public int MaxEntryCharacters { get; }
    public TimeSpan ExecutionTimeout { get; }

    public MetadataProcessingLimits(int maxEntries, int maxCharacters, int maxEntryCharacters, TimeSpan executionTimeout)
    {
        if (maxEntries is < 1 or > 16384) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        if (maxCharacters is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        if (maxEntryCharacters is < 1 or > 65536 || maxEntryCharacters > maxCharacters)
            throw new ArgumentOutOfRangeException(nameof(maxEntryCharacters));
        if (executionTimeout <= TimeSpan.Zero || executionTimeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(executionTimeout));
        MaxEntries = maxEntries;
        MaxCharacters = maxCharacters;
        MaxEntryCharacters = maxEntryCharacters;
        ExecutionTimeout = executionTimeout;
    }
}

/// <summary>Versioned text-metadata contract. Input order also defines deterministic budget allocation.</summary>
public sealed class MetadataProcessorDescriptor
{
    public string Id { get; }
    public string Version { get; }
    public AnalysisCapability Capability { get; }
    public IReadOnlyList<AnalysisCapability> Inputs { get; }
    public MetadataProcessingLimits Limits { get; }

    public MetadataProcessorDescriptor(string id, string version, AnalysisCapability capability,
        IEnumerable<AnalysisCapability> inputs, MetadataProcessingLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (id.Length > 200 || version.Length > 200) throw new ArgumentException("Processor identifiers must be bounded.");
        ArgumentNullException.ThrowIfNull(capability);
        if (capability == AnalysisCapability.FileDetails || capability == AnalysisCapability.TextRecognition ||
            capability == AnalysisCapability.QrCodeDetection || capability == AnalysisCapability.Description || capability == AnalysisCapability.Transcription)
            throw new ArgumentException("Metadata processors must produce a derived capability.", nameof(capability));
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(limits);
        AnalysisCapability[] copy = inputs.ToArray();
        if (copy.Length == 0 || copy.Distinct().Count() != copy.Length || copy.Any(input =>
            input != AnalysisCapability.TextRecognition && input != AnalysisCapability.QrCodeDetection &&
            input != AnalysisCapability.Transcription && input != AnalysisCapability.Description) || copy.Contains(capability))
            throw new ArgumentException("Specify distinct supported first-level text inputs, excluding the output capability.", nameof(inputs));
        Id = id;
        Version = version;
        Capability = capability;
        Inputs = Array.AsReadOnly(copy);
        Limits = limits;
    }

    public bool Matches(MetadataProcessorDescriptor other) => other != null && Id == other.Id && Version == other.Version &&
        Capability == other.Capability && Inputs.SequenceEqual(other.Inputs) && Limits == other.Limits;
}

/// <summary>Consumes only a bounded metadata snapshot; owns no media access, scheduling, policy, storage, or UI.</summary>
public interface IMetadataProcessor
{
    MetadataProcessorDescriptor Descriptor { get; }
    ValueTask<AnalyzerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AnalyzerAvailability.Ready);
    }
    Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AnalyzerAvailability.Ready);
    }
    Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken cancellationToken);
}
