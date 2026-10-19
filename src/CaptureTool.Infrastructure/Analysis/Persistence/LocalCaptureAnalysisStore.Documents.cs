using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed partial class LocalCaptureAnalysisStore
{
    private async Task<AnalysisDocument?> ReadDocumentAsync(Guid generation, CaptureId id, CancellationToken cancellationToken)
    {
        AnalysisDocument? document = await _documents.ReadAsync(RecordPath(generation, id),
            AnalysisJsonContext.Default.AnalysisDocument, cancellationToken).ConfigureAwait(false);
        if (document == null)
        {
            return null;
        }

        if (document.Version != 2 || document.CaptureId != id.Value || document.Results == null ||
            !Enum.IsDefined((AnalysisMediaKind)document.MediaKind) || document.Run == null)
        {
            throw new InvalidDataException("Invalid analysis document or storage identity.");
        }

        if (document.SourceSha256 != null)
        {
            _ = AnalysisDocumentMapper.ToRecord(document);
        }
        else if (document.Results.Length != 0)
        {
            throw new InvalidDataException("Metadata without a source revision.");
        }

        AnalysisRun run = ToWork(generation, document).Run;
        if (run.SourceRevision != null && (run.SourceRevision.Sha256 != document.SourceSha256 ||
            run.Id != document.RunId || run.PlanVersion != document.PlanVersion))
        {
            throw new InvalidDataException("Execution and metadata disagree.");
        }

        foreach (AnalysisStepCompletion step in run.CompletedSteps.Where(step => step.Outcome == AnalyzerOutcomeKind.Succeeded))
        {
            if (!document.Results.Any(result => MatchesCompletedStep(result, step, run)))
            {
                throw new InvalidDataException("A completed successful step requires its atomic result.");
            }
        }

        return document;
    }

    private static bool MatchesCompletedStep(ResultDocument result, AnalysisStepCompletion step, AnalysisRun run)
    {
        if (result.Capability != step.Capability.Name || result.SchemaVersion != step.Capability.SchemaVersion)
        {
            return false;
        }

        return step.ReusedResultId != null
            ? result.ResultId == step.ReusedResultId
            : result.ProducingRunId == run.Id && result.PlanVersion == run.PlanVersion;
    }

    private Task WriteDocumentAsync(Guid generation, AnalysisDocument document, CancellationToken cancellationToken) =>
        _documents.WriteAsync(RecordPath(generation, new CaptureId(document.CaptureId)), document,
            AnalysisJsonContext.Default.AnalysisDocument, cancellationToken);

    private AnalysisWorkItem ToWork(Guid generation, AnalysisDocument document)
    {
        RunDocument run = document.Run!;
        if (run.Steps == null || run.CompletedSteps == null || run.Steps.Any(step => step == null) ||
            run.CompletedSteps.Any(step => step == null || step.Capability == null))
        {
            throw new InvalidDataException("Missing execution steps.");
        }

        try
        {
            if (!Path.IsPathFullyQualified(run.SourcePath))
            {
                throw new ArgumentException("Source path must be absolute.");
            }

            var token = new AnalysisRunToken(new CaptureId(document.CaptureId), generation, run.Id);
            SourceRevision? revision = run.SourceSha256 == null ? null : new SourceRevision(run.SourceSha256);
            var steps = run.Steps.Select(step => new AnalysisCapability(step.Name, step.SchemaVersion));
            var completedSteps = run.CompletedSteps.Select(step => new AnalysisStepCompletion(
                new AnalysisCapability(step.Capability.Name, step.Capability.SchemaVersion),
                (AnalyzerOutcomeKind)step.Outcome, step.FailureCode, step.ReusedResultId));
            var execution = new AnalysisRun(run.Id, run.AuthorizationId, run.QueueOrder, run.PlanVersion,
                revision, (AnalysisRunStatus)run.Status, steps, completedSteps);
            AnalysisSourceIdentity? sourceIdentity = OwnsRequest(token)
                ? _sessionRequests[token.CaptureId].SourceIdentity
                : null;

            return new(token, (AnalysisMediaKind)document.MediaKind, run.SourcePath, run.Language,
                execution, run.ReuseExisting, run.UsePreferredPath, sourceIdentity);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Invalid execution state.", exception);
        }
    }

    private static RunDocument ToDocument(AnalysisRun run, string sourcePath, string? language, bool reuseExisting, bool usePreferredPath) =>
        new(run.Id, run.AuthorizationId, run.QueueOrder, run.PlanVersion, sourcePath, language, run.SourceRevision?.Sha256,
            (int)run.Status, run.Steps.Select(step => new CapabilityDocument(step.Name, step.SchemaVersion)).ToArray(),
            run.CompletedSteps.Select(step => new StepCompletionDocument(new(step.Capability.Name, step.Capability.SchemaVersion),
                (int)step.Outcome, step.FailureCode, step.ReusedResultId)).ToArray(), reuseExisting, usePreferredPath);
}
