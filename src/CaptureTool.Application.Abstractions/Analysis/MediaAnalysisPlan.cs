using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed class AnalysisStep
{
    public AnalysisCapability Capability { get; }
    public IReadOnlyList<string> Candidates { get; }
    public TimeSpan PreparationTimeout { get; }
    public TimeSpan ExecutionTimeout { get; }
    public int RetryCount { get; }

    public AnalysisStep(AnalysisCapability capability, IEnumerable<string> candidates,
        TimeSpan preparationTimeout, TimeSpan executionTimeout, int retryCount = 0)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(candidates);
        string[] copy = candidates.ToArray();
        if (copy.Length == 0 || copy.Any(string.IsNullOrWhiteSpace) || copy.Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Specify a nonempty, distinct candidate sequence.", nameof(candidates));
        if (preparationTimeout <= TimeSpan.Zero || preparationTimeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(preparationTimeout));
        if (executionTimeout <= TimeSpan.Zero || executionTimeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(executionTimeout));
        if (retryCount is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(retryCount));
        Capability = capability;
        Candidates = Array.AsReadOnly(copy);
        PreparationTimeout = preparationTimeout;
        ExecutionTimeout = executionTimeout;
        RetryCount = retryCount;
    }
}

public sealed class MediaAnalysisPlan
{
    public AnalysisMediaKind MediaKind { get; }
    public string Version { get; }
    public IReadOnlyList<AnalysisStep> Steps { get; }

    public MediaAnalysisPlan(AnalysisMediaKind mediaKind, string version, IEnumerable<AnalysisStep> steps)
    {
        if (!Enum.IsDefined(mediaKind)) throw new ArgumentOutOfRangeException(nameof(mediaKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (version.Length > 200) throw new ArgumentException("Plan version is too long.", nameof(version));
        ArgumentNullException.ThrowIfNull(steps);
        AnalysisStep[] copy = steps.ToArray();
        if (copy.Length == 0 || copy.Any(step => step == null) || copy.Select(step => step.Capability).Distinct().Count() != copy.Length)
            throw new ArgumentException("Specify a nonempty sequence with one step per capability.", nameof(steps));
        MediaKind = mediaKind;
        Version = version;
        Steps = Array.AsReadOnly(copy);
    }
}
