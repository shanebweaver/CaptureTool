using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Analysis;

public enum AnalyzerOutcomeKind
{
    Succeeded,
    Unsupported,
    TemporarilyUnavailable,
    Failed,
    InvalidSource,
    ContentRejected,
    Cancelled,
}

/// <summary>Failure codes are bounded identifiers, never exception text or recognized content.</summary>
public sealed class AnalyzerOutcome
{
    public AnalyzerOutcomeKind Kind { get; }
    public AnalysisPayload? Payload { get; }
    public AnalyzerProvenance? Producer { get; }
    public string? FailureCode { get; }

    private AnalyzerOutcome(AnalyzerOutcomeKind kind, AnalysisPayload? payload, AnalyzerProvenance? producer, string? failureCode)
    {
        Kind = kind;
        Payload = payload;
        Producer = producer;
        FailureCode = failureCode;
    }

    public static AnalyzerOutcome Success(AnalysisPayload payload, AnalyzerProvenance producer)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(producer);
        return new(AnalyzerOutcomeKind.Succeeded, payload, producer, null);
    }

    public static AnalyzerOutcome Unsuccessful(AnalyzerOutcomeKind kind, string failureCode)
    {
        if (!Enum.IsDefined(kind) || kind == AnalyzerOutcomeKind.Succeeded)
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > 80 || failureCode.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Use a bounded failure identifier.", nameof(failureCode));
        return new(kind, null, null, failureCode);
    }
}
