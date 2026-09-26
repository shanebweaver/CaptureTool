using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed record FoundryVisionResponse(AnalyzerOutcomeKind Status, string? Text, string? FailureCode);
