using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed partial class LocalCaptureAnalysisStore
{
    // Only requests admitted by this store instance may execute. Persisted run markers
    // preserve provenance, but never restore a queue after process exit or a crash.
    private readonly Dictionary<CaptureId, AnalysisRunToken> _sessionRequests = [];

    public async Task<AnalysisAdmissionScope> GetAdmissionScopeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument control = (await ReadControlAsync(true, cancellationToken).ConfigureAwait(false))!;
            return new(control.Generation, control.ReconciliationBoundary);
        }
        finally { _gate.Release(); }
    }

    public async Task<AnalysisWorkItem?> GetWorkAsync(CaptureId captureId, CancellationToken cancellationToken = default)
    {
        ValidateId(captureId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null) return null;
            AnalysisDocument? document = await ReadDocumentAsync(control.Generation, captureId, cancellationToken).ConfigureAwait(false);
            if (document?.Run == null) return null;
            AnalysisWorkItem work = ToWork(control.Generation, document);
            return work.Run.IsPending && !OwnsRequest(work.Token)
                ? work with { Run = work.Run.Finish(AnalysisRunStatus.Cancelled) } : work;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<AnalysisWorkItem>> ReadPendingAsync(CancellationToken cancellationToken = default)
    {
        const int batchSize = 16;
        Guid generation;
        AnalysisRunToken[] requests;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessionRequests.Count == 0) return [];
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null) return [];
            generation = control.Generation;
            requests = _sessionRequests.Values.Where(token => token.Generation == generation).ToArray();
        }
        finally { _gate.Release(); }

        List<AnalysisWorkItem> pending = [];
        for (int offset = 0; offset < requests.Length; offset += batchSize)
        {
            // Bound each hold so settings and clear can interrupt a large session queue.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
                if (control?.Generation != generation) return [];
                for (int index = offset; index < Math.Min(offset + batchSize, requests.Length); index++)
                {
                    AnalysisRunToken token = requests[index];
                    if (!OwnsRequest(token)) continue;
                    AnalysisDocument? document = await ReadDocumentAsync(generation, token.CaptureId, cancellationToken).ConfigureAwait(false);
                    if (document?.Run?.Status is (int)AnalysisRunStatus.Queued or (int)AnalysisRunStatus.Running)
                        pending.Add(ToWork(generation, document));
                }
            }
            finally { _gate.Release(); }
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return pending.Where(work => OwnsRequest(work.Token)).OrderBy(work => work.Run.QueueOrder).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task DiscardPendingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { _sessionRequests.Clear(); }
        finally { _gate.Release(); }
    }

    private bool OwnsRequest(AnalysisRunToken token) =>
        _sessionRequests.TryGetValue(token.CaptureId, out var current) && current == token;

    public async Task<bool> AdmitAsync(AnalysisRequest request, Guid authorizationId, MediaAnalysisPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        ValidateId(request.CaptureId);
        if (request.RequestId == Guid.Empty || request.Generation == Guid.Empty || authorizationId == Guid.Empty || request.ExpectedRunId == Guid.Empty)
            throw new ArgumentException("Admission identities must be nonempty.");
        if (request.MediaKind != plan.MediaKind || !Path.IsPathFullyQualified(request.SourcePath))
            throw new ArgumentException("Admission requires a compatible plan and an absolute source path.");
        string path = Path.GetFullPath(request.SourcePath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null || control.Generation != request.Generation) return false;
            AnalysisDocument? prior = await ReadDocumentAsync(control.Generation, request.CaptureId, cancellationToken).ConfigureAwait(false);
            if (prior?.Run is { } existing && existing.Id == request.RequestId)
                return OwnsRequest(new(request.CaptureId, control.Generation, request.RequestId)) &&
                    existing.AuthorizationId == authorizationId && existing.PlanVersion == plan.Version &&
                    existing.ReuseExisting == request.ReuseExisting && existing.SourcePath == path && existing.Language == request.Language && prior.MediaKind == (int)request.MediaKind &&
                    existing.Steps.Select(step => new AnalysisCapability(step.Name, step.SchemaVersion)).SequenceEqual(plan.Steps.Select(step => step.Capability));
            if ((prior?.Run?.Id ?? prior?.RunId) != request.ExpectedRunId) return false;
            if (prior != null && prior.MediaKind != (int)request.MediaKind) throw new InvalidOperationException("Capture media kind cannot change.");
            var run = new AnalysisRun(request.RequestId, authorizationId, checked(control.QueueOrder + 1), plan.Version,
                null, AnalysisRunStatus.Queued, plan.Steps.Select(step => step.Capability), []);
            // A crash here can leave a sequence gap, never an admitted request without its durable intent.
            await _documents.WriteAsync(_controlPath, control with { Version = 2, QueueOrder = run.QueueOrder },
                AnalysisJsonContext.Default.AnalysisControlDocument, cancellationToken).ConfigureAwait(false);
            AnalysisDocument next = (prior ?? new(2, request.CaptureId.Value, (int)request.MediaKind, null,
                plan.Version, request.RequestId, [])) with { Version = 2, Run = ToDocument(run, path, request.Language, request.ReuseExisting) };
            await WriteDocumentAsync(control.Generation, next, cancellationToken).ConfigureAwait(false);
            _sessionRequests[request.CaptureId] = new(request.CaptureId, control.Generation, request.RequestId);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> BindSourceAsync(AnalysisRunToken token, SourceRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null) return false;
            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (!run.IsPending || run.SourceRevision != null && run.SourceRevision != revision) return false;
            if (run.SourceRevision == revision) return true;
            CaptureAnalysisRecord record = document.SourceSha256 == null
                ? new(token.CaptureId, (AnalysisMediaKind)document.MediaKind, revision, run.PlanVersion, run.Id, [])
                : AnalysisDocumentMapper.ToRecord(document).StartRun(revision, run.PlanVersion, run.Id);
            AnalysisDocument next = AnalysisDocumentMapper.ToDocument(record) with
            {
                Version = 2, Run = ToDocument(run.BindSource(revision), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting),
            };
            await WriteDocumentAsync(token.Generation, next, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> CommitStepAsync(AnalysisRunToken token, AnalysisStepCompletion step, AnalysisResult? result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.ReusedResultId != null ? result != null : step.Outcome == AnalyzerOutcomeKind.Succeeded ? result == null : result != null)
            throw new ArgumentException("Only success must publish a payload.", nameof(result));
        if (result != null && (result.Payload.Capability != step.Capability || result.ProducingRunId != token.RunId))
            throw new ArgumentException("Result does not belong to this step/run.", nameof(result));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null) return false;
            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (run.Status != AnalysisRunStatus.Running || run.Steps[run.CompletedSteps.Count] != step.Capability) return false;
            CaptureAnalysisRecord record = AnalysisDocumentMapper.ToRecord(document);
            if (step.ReusedResultId != null && (!document.Run!.ReuseExisting ||
                !record.Results.Any(saved => saved.ResultId == step.ReusedResultId && saved.Payload.Capability == step.Capability))) return false;
            if (result != null && !record.HasCurrentInputs(result)) return false;
            if (result != null) record = record.WithResult(result);
            AnalysisDocument next = AnalysisDocumentMapper.ToDocument(record) with
            {
                Version = 2, Run = ToDocument(run.CompleteStep(step), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting),
            };
            await WriteDocumentAsync(token.Generation, next, cancellationToken).ConfigureAwait(false);
            if (!ToWork(token.Generation, next).Run.IsPending) _sessionRequests.Remove(token.CaptureId);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> FinishAsync(AnalysisRunToken token, AnalysisRunStatus status, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null) return false;
            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (!run.IsPending) return false;
            await WriteDocumentAsync(token.Generation, document with
            {
                Run = ToDocument(run.Finish(status), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting),
            }, cancellationToken).ConfigureAwait(false);
            _sessionRequests.Remove(token.CaptureId);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task<AnalysisDocument?> CurrentRequestAsync(AnalysisRunToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ValidateId(token.CaptureId);
        if (!OwnsRequest(token)) return null;
        AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
        if (control == null || control.Generation != token.Generation) return null;
        AnalysisDocument? document = await ReadDocumentAsync(token.Generation, token.CaptureId, cancellationToken).ConfigureAwait(false);
        return document?.Run?.Id == token.RunId ? document : null;
    }

    private async Task<AnalysisDocument?> ReadDocumentAsync(Guid generation, CaptureId id, CancellationToken cancellationToken)
    {
        AnalysisDocument? document = await _documents.ReadAsync(RecordPath(generation, id),
            AnalysisJsonContext.Default.AnalysisDocument, cancellationToken).ConfigureAwait(false);
        if (document == null) return null;
        if (document.Version is not (1 or 2) || document.CaptureId != id.Value || document.Results == null ||
            !Enum.IsDefined((AnalysisMediaKind)document.MediaKind) || document.Version == 1 && document.Run != null)
            throw new InvalidDataException("Invalid analysis document or storage identity.");
        if (document.SourceSha256 != null) _ = AnalysisDocumentMapper.ToRecord(document);
        else if (document.Run == null || document.Results.Length != 0)
            throw new InvalidDataException("Metadata without a source revision.");
        if (document.Run != null)
        {
            AnalysisRun run = ToWork(generation, document).Run;
            if (run.SourceRevision != null && (run.SourceRevision.Sha256 != document.SourceSha256 ||
                run.Id != document.RunId || run.PlanVersion != document.PlanVersion))
                throw new InvalidDataException("Execution and metadata disagree.");
            foreach (AnalysisStepCompletion step in run.CompletedSteps.Where(step => step.Outcome == AnalyzerOutcomeKind.Succeeded))
                if (!document.Results.Any(result => result.Capability == step.Capability.Name && result.SchemaVersion == step.Capability.SchemaVersion &&
                    (step.ReusedResultId != null ? result.ResultId == step.ReusedResultId : result.ProducingRunId == run.Id && result.PlanVersion == run.PlanVersion)))
                    throw new InvalidDataException("A completed successful step requires its atomic result.");
        }
        return document;
    }

    private Task WriteDocumentAsync(Guid generation, AnalysisDocument document, CancellationToken cancellationToken) =>
        _documents.WriteAsync(RecordPath(generation, new CaptureId(document.CaptureId)), document,
            AnalysisJsonContext.Default.AnalysisDocument, cancellationToken);

    private static AnalysisWorkItem ToWork(Guid generation, AnalysisDocument document)
    {
        RunDocument run = document.Run!;
        if (run.Steps == null || run.CompletedSteps == null || run.Steps.Any(step => step == null) ||
            run.CompletedSteps.Any(step => step == null || step.Capability == null))
            throw new InvalidDataException("Missing execution steps.");
        try
        {
            if (!Path.IsPathFullyQualified(run.SourcePath)) throw new ArgumentException("Source path must be absolute.");
            return new(new(new CaptureId(document.CaptureId), generation, run.Id), (AnalysisMediaKind)document.MediaKind,
                run.SourcePath, run.Language, new(run.Id, run.AuthorizationId, run.QueueOrder, run.PlanVersion,
                    run.SourceSha256 == null ? null : new SourceRevision(run.SourceSha256), (AnalysisRunStatus)run.Status,
                    run.Steps.Select(step => new AnalysisCapability(step.Name, step.SchemaVersion)),
                    run.CompletedSteps.Select(step => new AnalysisStepCompletion(new(step.Capability.Name, step.Capability.SchemaVersion),
                        (AnalyzerOutcomeKind)step.Outcome, step.FailureCode, step.ReusedResultId))), run.ReuseExisting);
        }
        catch (ArgumentException exception) { throw new InvalidDataException("Invalid execution state.", exception); }
    }

    private static RunDocument ToDocument(AnalysisRun run, string sourcePath, string? language, bool reuseExisting) =>
        new(run.Id, run.AuthorizationId, run.QueueOrder, run.PlanVersion, sourcePath, language, run.SourceRevision?.Sha256,
            (int)run.Status, run.Steps.Select(step => new CapabilityDocument(step.Name, step.SchemaVersion)).ToArray(),
            run.CompletedSteps.Select(step => new StepCompletionDocument(new(step.Capability.Name, step.Capability.SchemaVersion),
                (int)step.Outcome, step.FailureCode, step.ReusedResultId)).ToArray(), reuseExisting);
}
