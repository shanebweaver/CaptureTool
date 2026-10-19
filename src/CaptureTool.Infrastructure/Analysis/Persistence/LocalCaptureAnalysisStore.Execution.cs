using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed partial class LocalCaptureAnalysisStore
{
    // Only requests admitted by this store instance may execute. Persisted run markers
    // preserve provenance, but never restore a queue after process exit or a crash.
    private readonly Dictionary<CaptureId, SessionRequest> _sessionRequests = [];

    private sealed record SessionRequest(AnalysisRunToken Token, AnalysisSourceIdentity? SourceIdentity);

    public async Task<AnalysisAdmissionScope> GetAdmissionScopeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument control = (await ReadControlAsync(true, cancellationToken).ConfigureAwait(false))!;
            return new(control.Generation);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AnalysisWorkItem?> GetWorkAsync(CaptureId captureId, CancellationToken cancellationToken = default)
    {
        ValidateId(captureId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null)
            {
                return null;
            }

            AnalysisDocument? document = await ReadDocumentAsync(control.Generation, captureId, cancellationToken).ConfigureAwait(false);
            if (document?.Run == null)
            {
                return null;
            }

            AnalysisWorkItem work = ToWork(control.Generation, document);
            return work.Run.IsPending && !OwnsRequest(work.Token)
                ? work with
                {
                    Run = work.Run.Finish(AnalysisRunStatus.Cancelled)
                } : work;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AnalysisWorkItem>> ReadPendingAsync(CancellationToken cancellationToken = default)
    {
        const int batchSize = 16;
        Guid generation;
        AnalysisRunToken[] requests;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessionRequests.Count == 0)
            {
                return [];
            }

            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null)
            {
                return [];
            }

            generation = control.Generation;
            requests = _sessionRequests.Values.Select(request => request.Token).Where(token => token.Generation == generation).ToArray();
        }
        finally
        {
            _gate.Release();
        }

        List<AnalysisWorkItem> pending = [];
        for (int offset = 0; offset < requests.Length; offset += batchSize)
        {
            // Bound each hold so settings and clear can interrupt a large session queue.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
                if (control?.Generation != generation)
                {
                    return [];
                }

                for (int index = offset; index < Math.Min(offset + batchSize, requests.Length); index++)
                {
                    AnalysisRunToken token = requests[index];
                    if (!OwnsRequest(token))
                    {
                        continue;
                    }

                    AnalysisDocument? document = await ReadDocumentAsync(generation, token.CaptureId, cancellationToken).ConfigureAwait(false);
                    if (document?.Run?.Status is (int)AnalysisRunStatus.Queued or (int)AnalysisRunStatus.Running)
                    {
                        pending.Add(ToWork(generation, document));
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return pending.Where(work => OwnsRequest(work.Token)).OrderBy(work => work.Run.QueueOrder).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DiscardPendingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sessionRequests.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool OwnsRequest(AnalysisRunToken token) =>
        _sessionRequests.TryGetValue(token.CaptureId, out var current) && current.Token == token;

    public async Task<bool> AdmitAsync(AnalysisRequest request, Guid authorizationId, MediaAnalysisPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        ValidateId(request.CaptureId);
        if (request.RequestId == Guid.Empty || request.Generation == Guid.Empty || authorizationId == Guid.Empty || request.ExpectedRunId == Guid.Empty)
        {
            throw new ArgumentException("Admission identities must be nonempty.");
        }

        if (request.MediaKind != plan.MediaKind || !Path.IsPathFullyQualified(request.SourcePath))
        {
            throw new ArgumentException("Admission requires a compatible plan and an absolute source path.");
        }

        string path = Path.GetFullPath(request.SourcePath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
            if (control == null || control.Generation != request.Generation)
            {
                return false;
            }

            AnalysisDocument? prior = await ReadDocumentAsync(control.Generation, request.CaptureId, cancellationToken).ConfigureAwait(false);
            if (prior?.Run is { } existing && existing.Id == request.RequestId)
            {
                return IsMatchingReplay(request, authorizationId, plan, path, prior);
            }
            if (prior?.Run?.Id != request.ExpectedRunId)
            {
                return false;
            }

            if (prior != null && prior.MediaKind != (int)request.MediaKind)
            {
                throw new InvalidOperationException("Capture media kind cannot change.");
            }

            var run = new AnalysisRun(request.RequestId, authorizationId, checked(control.QueueOrder + 1), plan.Version,
                null, AnalysisRunStatus.Queued, plan.Steps.Select(step => step.Capability), []);
            // A crash here can leave a sequence gap, never an admitted request without its durable intent.
            await _documents.WriteAsync(_controlPath, control with
            {
                Version = 2,
                QueueOrder = run.QueueOrder
            },
                AnalysisJsonContext.Default.AnalysisControlDocument, cancellationToken).ConfigureAwait(false);
            AnalysisDocument next = (prior ?? new(2, request.CaptureId.Value, (int)request.MediaKind, null,
                plan.Version, request.RequestId, [])) with
            {
                Version = 2,
                Run = ToDocument(run, path, request.Language, request.ReuseExisting, request.UsePreferredPath)
            };
            await WriteDocumentAsync(control.Generation, next, cancellationToken).ConfigureAwait(false);
            _sessionRequests[request.CaptureId] = new(new(request.CaptureId, control.Generation, request.RequestId), request.SourceIdentity);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsMatchingReplay(AnalysisRequest request, Guid authorizationId, MediaAnalysisPlan plan,
        string sourcePath, AnalysisDocument document)
    {
        var token = new AnalysisRunToken(request.CaptureId, request.Generation, request.RequestId);
        if (!OwnsRequest(token))
        {
            return false;
        }

        RunDocument run = document.Run!;
        return _sessionRequests[request.CaptureId].SourceIdentity == request.SourceIdentity &&
            run.AuthorizationId == authorizationId &&
            run.PlanVersion == plan.Version &&
            run.ReuseExisting == request.ReuseExisting &&
            run.UsePreferredPath == request.UsePreferredPath &&
            run.SourcePath == sourcePath &&
            run.Language == request.Language &&
            document.MediaKind == (int)request.MediaKind &&
            run.Steps.Select(step => new AnalysisCapability(step.Name, step.SchemaVersion))
                .SequenceEqual(plan.Steps.Select(step => step.Capability));
    }

    public async Task<bool> BindSourceAsync(AnalysisRunToken token, SourceRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null)
            {
                return false;
            }

            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (!run.IsPending || run.SourceRevision != null && run.SourceRevision != revision)
            {
                return false;
            }

            if (run.SourceRevision == revision)
            {
                return true;
            }

            CaptureAnalysisRecord record = document.SourceSha256 == null
                ? new(token.CaptureId, (AnalysisMediaKind)document.MediaKind, revision, run.PlanVersion, run.Id, [])
                : AnalysisDocumentMapper.ToRecord(document).StartRun(revision, run.PlanVersion, run.Id);
            AnalysisDocument next = AnalysisDocumentMapper.ToDocument(record) with
            {
                Version = 2,
                Run = ToDocument(run.BindSource(revision), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting, document.Run.UsePreferredPath),
            };
            await WriteDocumentAsync(token.Generation, next, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CommitStepAsync(AnalysisRunToken token, AnalysisStepCompletion step, AnalysisResult? result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        bool requiresNewResult = step.ReusedResultId == null && step.Outcome == AnalyzerOutcomeKind.Succeeded;
        if (requiresNewResult != (result != null))
        {
            throw new ArgumentException("Only success must publish a payload.", nameof(result));
        }

        if (result != null && (result.Payload.Capability != step.Capability || result.ProducingRunId != token.RunId))
        {
            throw new ArgumentException("Result does not belong to this step/run.", nameof(result));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null)
            {
                return false;
            }

            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (run.Status != AnalysisRunStatus.Running || run.Steps[run.CompletedSteps.Count] != step.Capability)
            {
                return false;
            }

            CaptureAnalysisRecord record = AnalysisDocumentMapper.ToRecord(document);
            if (step.ReusedResultId != null && (!document.Run!.ReuseExisting ||
                !record.Results.Any(saved => saved.ResultId == step.ReusedResultId && saved.Payload.Capability == step.Capability)))
            {
                return false;
            }

            if (result != null && !record.HasCurrentInputs(result))
            {
                return false;
            }

            if (result != null)
            {
                record = record.WithResult(result);
            }

            AnalysisDocument next = AnalysisDocumentMapper.ToDocument(record) with
            {
                Version = 2,
                Run = ToDocument(run.CompleteStep(step), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting, document.Run.UsePreferredPath),
            };
            await WriteDocumentAsync(token.Generation, next, cancellationToken).ConfigureAwait(false);
            if (!ToWork(token.Generation, next).Run.IsPending)
            {
                _sessionRequests.Remove(token.CaptureId);
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> FinishAsync(AnalysisRunToken token, AnalysisRunStatus status, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisDocument? document = await CurrentRequestAsync(token, cancellationToken).ConfigureAwait(false);
            if (document == null)
            {
                return false;
            }

            AnalysisRun run = ToWork(token.Generation, document).Run;
            if (!run.IsPending)
            {
                return false;
            }

            await WriteDocumentAsync(token.Generation, document with
            {
                Run = ToDocument(run.Finish(status), document.Run!.SourcePath, document.Run.Language, document.Run.ReuseExisting, document.Run.UsePreferredPath),
            }, cancellationToken).ConfigureAwait(false);
            _sessionRequests.Remove(token.CaptureId);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AnalysisDocument?> CurrentRequestAsync(AnalysisRunToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ValidateId(token.CaptureId);
        if (!OwnsRequest(token))
        {
            return null;
        }

        AnalysisControlDocument? control = await ReadControlAsync(false, cancellationToken).ConfigureAwait(false);
        if (control == null || control.Generation != token.Generation)
        {
            return null;
        }

        AnalysisDocument? document = await ReadDocumentAsync(token.Generation, token.CaptureId, cancellationToken).ConfigureAwait(false);
        return document?.Run?.Id == token.RunId ? document : null;
    }
}
