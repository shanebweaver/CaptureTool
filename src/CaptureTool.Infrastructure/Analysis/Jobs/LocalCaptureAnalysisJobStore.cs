using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Jobs.Serialization;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Jobs;

internal sealed class LocalCaptureAnalysisJobStore : ICaptureAnalysisJobStore, IDisposable
{
    internal const string JobsVersionDirectoryName = "jobs-v1";
    internal const string IntentsDirectoryName = "intents";
    internal const string QuarantineDirectoryName = "quarantine";
    internal const string JobExtension = ".job";
    internal const int CurrentSchemaVersion = 1;

    private const string FileNameDomain = "capture-analysis-job/v1/";

    private readonly IApplicationLocalCachePathProvider _localCachePathProvider;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAnalysisJobStore(
        IApplicationLocalCachePathProvider localCachePathProvider,
        IUserDataProtectionService dataProtectionService,
        IAtomicFileWriter atomicFileWriter,
        ILogService logService)
    {
        _localCachePathProvider = localCachePathProvider;
        _dataProtectionService = dataProtectionService;
        _atomicFileWriter = atomicFileWriter;
        _logService = logService;
    }

    public async ValueTask<CaptureAnalysisJobIntent?> GetAsync(
        CaptureAnalysisJobKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JobLoadResult loaded = Load(GetJobFilePath(key));
            return loaded.Status == JobLoadStatus.Known && loaded.Job!.Intent.Key == key
                ? loaded.Job.Intent
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<CaptureAnalysisJobIntent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<CaptureAnalysisJobIntent> intents = [];
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach ((_, StoredJob job) in LoadAll(cancellationToken))
            {
                intents.Add(job.Intent);
            }
        }
        finally
        {
            _gate.Release();
        }

        foreach (CaptureAnalysisJobIntent intent in intents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return intent;
        }
    }

    public async ValueTask<CaptureAnalysisJobEnqueueResult> TryEnqueueAsync(
        CaptureAnalysisJobKey key,
        DateTimeOffset enqueuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureUtc(enqueuedAtUtc, nameof(enqueuedAtUtc));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string filePath = GetJobFilePath(key);
            JobLoadResult loaded = Load(filePath);
            if (loaded.Status == JobLoadStatus.Known)
            {
                return loaded.Job!.Intent.Key == key
                    ? new(CaptureAnalysisJobEnqueueStatus.AlreadyExists, loaded.Job.Intent)
                    : new(CaptureAnalysisJobEnqueueStatus.Rejected);
            }

            if (loaded.Status != JobLoadStatus.Missing)
            {
                return new(loaded.Status == JobLoadStatus.ReadOnly
                    ? CaptureAnalysisJobEnqueueStatus.Rejected
                    : CaptureAnalysisJobEnqueueStatus.Unavailable);
            }

            var intent = new CaptureAnalysisJobIntent(
                key,
                CaptureAnalysisJobState.Pending,
                0,
                enqueuedAtUtc,
                null,
                null,
                []);
            if (!TryWrite(filePath, new(intent, null, null)))
            {
                return new(CaptureAnalysisJobEnqueueStatus.Unavailable);
            }

            return new(CaptureAnalysisJobEnqueueStatus.Enqueued, intent);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisJobEnqueueResult> TryRequeueAsync(
        CaptureAnalysisJobKey key,
        DateTimeOffset enqueuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureUtc(enqueuedAtUtc, nameof(enqueuedAtUtc));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string filePath = GetJobFilePath(key);
            JobLoadResult loaded = Load(filePath);
            if (loaded.Status == JobLoadStatus.Known)
            {
                if (loaded.Job!.Intent.Key != key)
                {
                    return new(CaptureAnalysisJobEnqueueStatus.Rejected);
                }

                if (loaded.Job.Intent.State is not (
                    CaptureAnalysisJobState.Completed or
                    CaptureAnalysisJobState.Cancelled or
                    CaptureAnalysisJobState.TerminalFailure))
                {
                    return new(
                        CaptureAnalysisJobEnqueueStatus.AlreadyExists,
                        loaded.Job.Intent);
                }

                CaptureAnalysisJobIntent pending = CreatePendingIntent(key, enqueuedAtUtc);
                return TryWrite(filePath, new(pending, null, null))
                    ? new(CaptureAnalysisJobEnqueueStatus.Enqueued, pending)
                    : new(CaptureAnalysisJobEnqueueStatus.Unavailable);
            }

            if (loaded.Status != JobLoadStatus.Missing)
            {
                return new(loaded.Status == JobLoadStatus.ReadOnly
                    ? CaptureAnalysisJobEnqueueStatus.Rejected
                    : CaptureAnalysisJobEnqueueStatus.Unavailable);
            }

            CaptureAnalysisJobIntent intent = CreatePendingIntent(key, enqueuedAtUtc);
            return TryWrite(filePath, new(intent, null, null))
                ? new(CaptureAnalysisJobEnqueueStatus.Enqueued, intent)
                : new(CaptureAnalysisJobEnqueueStatus.Unavailable);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisJobLease?> TryLeaseNextDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsurePositive(leaseDuration, nameof(leaseDuration));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string Path, StoredJob Job)? due = LoadAll(cancellationToken)
                .Where(item => IsDue(item.Job, nowUtc))
                .OrderBy(item => GetDueTime(item.Job))
                .ThenBy(item => item.Job.Intent.EnqueuedAtUtc)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => ((string Path, StoredJob Job)?)item)
                .FirstOrDefault();
            if (!due.HasValue)
            {
                return null;
            }

            StoredJob selected = due.Value.Job;
            CaptureAnalysisJobIntent running = CopyIntent(
                selected.Intent,
                CaptureAnalysisJobState.Running,
                nextAttemptAtUtc: null,
                latestFailure: selected.Intent.LatestFailure);
            CaptureAnalysisJobLeaseToken leaseToken = CaptureAnalysisJobLeaseToken.New();
            DateTimeOffset expiresAtUtc = nowUtc + leaseDuration;
            if (!TryWrite(due.Value.Path, new(running, leaseToken, expiresAtUtc)))
            {
                return null;
            }

            return new(leaseToken, running, expiresAtUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<DateTimeOffset?> GetNextDueTimeAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return LoadAll(cancellationToken)
                .Select(item => GetDueTime(item.Job))
                .Where(due => due.HasValue)
                .Min();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<int> RecoverExpiredLeasesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int recovered = 0;
            foreach ((string path, StoredJob job) in LoadAll(cancellationToken).Where(item =>
                item.Job.Intent.State == CaptureAnalysisJobState.Running &&
                item.Job.LeaseExpiresAtUtc <= nowUtc))
            {
                CaptureAnalysisJobIntent pending = CopyIntent(
                    job.Intent,
                    CaptureAnalysisJobState.Pending,
                    nextAttemptAtUtc: null,
                    latestFailure: null);
                if (TryWrite(path, new(pending, null, null)))
                {
                    recovered++;
                }
            }

            return recovered;
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryRenewLeaseAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        EnsurePositive(leaseDuration, nameof(leaseDuration));
        return MutateLeaseAsync(
            leaseToken,
            stored => stored.LeaseExpiresAtUtc <= nowUtc
                ? Mutation.Invalid(CaptureAnalysisJobMutationStatus.LeaseLost)
                : Mutation.Success(new(
                    stored.Intent,
                    leaseToken,
                    nowUtc + leaseDuration)),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryRecordAttemptAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalyzerAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        return MutateLeaseAsync(
            leaseToken,
            stored => attempt.AttemptNumber != stored.Intent.AttemptCount + 1 ||
                attempt.ProcessingBoundary != stored.Intent.Key.AuthorizedProcessingBoundary
                    ? Mutation.Invalid(CaptureAnalysisJobMutationStatus.InvalidTransition)
                    : Mutation.Success(new(
                        CopyIntent(
                            stored.Intent,
                            CaptureAnalysisJobState.Running,
                            nextAttemptAtUtc: null,
                            latestFailure: attempt.Failure,
                            attempts: [.. stored.Intent.Attempts, attempt]),
                        leaseToken,
                        stored.LeaseExpiresAtUtc)),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryScheduleRetryAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure failure,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        if (failure.Disposition != AnalysisFailureDisposition.Transient)
        {
            throw new ArgumentException("A scheduled retry requires a transient failure.", nameof(failure));
        }

        return MutateLeaseAsync(
            leaseToken,
            stored => stored.Intent.Attempts.LastOrDefault()?.Failure != failure
                ? Mutation.Invalid(CaptureAnalysisJobMutationStatus.InvalidTransition)
                : Mutation.Success(new(
                    CopyIntent(
                        stored.Intent,
                        CaptureAnalysisJobState.RetryScheduled,
                        nextAttemptAtUtc,
                        failure),
                    null,
                    null)),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryWaitForCapabilityAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure? reason,
        CancellationToken cancellationToken = default)
    {
        if (reason is { IsEmpty: true })
        {
            throw new ArgumentException("A capability wait reason must be bounded.", nameof(reason));
        }

        return MutateLeaseAsync(
            leaseToken,
            stored => Mutation.Success(new(
                CopyIntent(
                    stored.Intent,
                    CaptureAnalysisJobState.WaitingForCapability,
                    nextAttemptAtUtc: null,
                    latestFailure: reason),
                null,
                null)),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryCompleteAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CancellationToken cancellationToken = default)
    {
        return MutateLeaseAsync(
            leaseToken,
            stored => stored.Intent.Attempts.LastOrDefault()?.Status !=
                CaptureAnalyzerAttemptStatus.Succeeded
                    ? Mutation.Invalid(CaptureAnalysisJobMutationStatus.InvalidTransition)
                    : Mutation.Success(new(
                        CopyIntent(
                            stored.Intent,
                            CaptureAnalysisJobState.Completed,
                            nextAttemptAtUtc: null,
                            latestFailure: null),
                        null,
                        null)),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisJobMutationResult> TryFailTerminalAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        AnalysisFailure failure,
        CancellationToken cancellationToken = default)
    {
        if (failure.Disposition != AnalysisFailureDisposition.Terminal)
        {
            throw new ArgumentException("A terminal job requires a terminal failure.", nameof(failure));
        }

        return MutateLeaseAsync(
            leaseToken,
            stored => Mutation.Success(new(
                CopyIntent(
                    stored.Intent,
                    CaptureAnalysisJobState.TerminalFailure,
                    nextAttemptAtUtc: null,
                    latestFailure: failure),
                null,
                null)),
            cancellationToken);
    }

    public async ValueTask<int> ResumeWaitingForCapabilityAsync(
        CapabilityDefinition capability,
        ProcessingBoundary processingBoundary,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A capability is required.", nameof(capability));
        }

        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        EnsureUtc(dueAtUtc, nameof(dueAtUtc));
        var resumedFailure = new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Transient);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int resumed = 0;
            foreach ((string path, StoredJob job) in LoadAll(cancellationToken).Where(item =>
                item.Job.Intent.State == CaptureAnalysisJobState.WaitingForCapability &&
                item.Job.Intent.Key.Capability == capability &&
                item.Job.Intent.Key.AuthorizedProcessingBoundary == processingBoundary))
            {
                CaptureAnalysisJobIntent retry = CopyIntent(
                    job.Intent,
                    CaptureAnalysisJobState.RetryScheduled,
                    dueAtUtc,
                    resumedFailure);
                if (TryWrite(path, new(retry, null, null)))
                {
                    resumed++;
                }
            }

            return resumed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<int> ResumeWaitingForDependencyAsync(
        CaptureId captureId,
        CapabilityDefinition dependency,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capture ID is required.", nameof(captureId));
        }

        if (dependency.Id.IsEmpty)
        {
            throw new ArgumentException("A dependency capability is required.", nameof(dependency));
        }

        EnsureUtc(dueAtUtc, nameof(dueAtUtc));
        var resumedFailure = new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Transient);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int resumed = 0;
            foreach ((string path, StoredJob job) in LoadAll(cancellationToken).Where(item =>
                item.Job.Intent.State == CaptureAnalysisJobState.WaitingForCapability &&
                item.Job.Intent.Key.CaptureId == captureId &&
                item.Job.Intent.Key.Dependencies.Contains(dependency)))
            {
                CaptureAnalysisJobIntent retry = CopyIntent(
                    job.Intent,
                    CaptureAnalysisJobState.RetryScheduled,
                    dueAtUtc,
                    resumedFailure);
                if (TryWrite(path, new(retry, null, null)))
                {
                    resumed++;
                }
            }

            return resumed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisJobMutationResult> TryCancelAsync(
        CaptureAnalysisJobKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = GetJobFilePath(key);
            JobLoadResult loaded = Load(path);
            if (loaded.Status != JobLoadStatus.Known || loaded.Job!.Intent.Key != key)
            {
                return new(loaded.Status == JobLoadStatus.Missing
                    ? CaptureAnalysisJobMutationStatus.NotFound
                    : CaptureAnalysisJobMutationStatus.Unavailable);
            }

            return TryCancelLoaded(path, loaded.Job);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask<int> CancelCaptureAsync(
        CaptureId captureId,
        long minimumTombstoneGeneration,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capture ID is required.", nameof(captureId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumTombstoneGeneration);
        return CancelWhereAsync(
            job => job.Intent.Key.CaptureId == captureId &&
                job.Intent.Key.Preconditions.TombstoneGeneration < minimumTombstoneGeneration,
            cancellationToken);
    }

    public ValueTask<int> CancelBeforeControlGenerationAsync(
        long controlGeneration,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(controlGeneration);
        return CancelWhereAsync(
            job => job.Intent.Key.Preconditions.ControlGeneration < controlGeneration,
            cancellationToken);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    internal string GetJobFilePath(CaptureAnalysisJobKey key)
    {
        string canonical = string.Join(
            '|',
            key.Preconditions.CaptureId,
            key.Preconditions.CaptureSourceGeneration,
            key.Preconditions.SourceRevision.Length,
            key.Preconditions.SourceRevision.LastWriteTimeUtc.UtcTicks,
            key.Preconditions.SourceRevision.Fingerprint,
            key.Preconditions.Purpose,
            key.Preconditions.PolicyRevision,
            key.Preconditions.ControlGeneration,
            key.Preconditions.EnrollmentGeneration,
            key.Preconditions.TombstoneGeneration,
            key.Preconditions.RecipeId,
            key.Preconditions.RecipeVersion,
            key.Preconditions.ResolutionPolicyRevision,
            key.Capability.Id,
            key.Capability.SchemaVersion,
            key.AuthorizedProcessingBoundary);
        if (key.Dependencies.Count > 0)
        {
            canonical += "|dependencies-v1|" + string.Join(
                '|',
                new[] { key.Capability.Classification.ToString() }.Concat(
                    key.Dependencies.Select(dependency => string.Join(
                        ':',
                        dependency.Id,
                        dependency.SchemaVersion,
                        dependency.Classification))));
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(FileNameDomain + canonical));
        return Path.Combine(GetIntentsDirectoryPath(), Convert.ToHexStringLower(hash) + JobExtension);
    }

    private async ValueTask<CaptureAnalysisJobMutationResult> MutateLeaseAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        Func<StoredJob, Mutation> mutation,
        CancellationToken cancellationToken)
    {
        if (leaseToken.IsEmpty)
        {
            throw new ArgumentException("A lease mutation requires a token.", nameof(leaseToken));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string Path, StoredJob Job)? leased = LoadAll(cancellationToken)
                .Where(item => item.Job.LeaseToken == leaseToken)
                .Select(item => ((string Path, StoredJob Job)?)item)
                .FirstOrDefault();
            if (!leased.HasValue)
            {
                return new(CaptureAnalysisJobMutationStatus.LeaseLost);
            }

            if (leased.Value.Job.Intent.State != CaptureAnalysisJobState.Running)
            {
                return new(CaptureAnalysisJobMutationStatus.InvalidTransition);
            }

            Mutation next = mutation(leased.Value.Job);
            if (next.Status != CaptureAnalysisJobMutationStatus.Succeeded)
            {
                return new(next.Status);
            }

            return TryWrite(leased.Value.Path, next.Job!)
                ? new(CaptureAnalysisJobMutationStatus.Succeeded, next.Job!.Intent)
                : new(CaptureAnalysisJobMutationStatus.Unavailable);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<int> CancelWhereAsync(
        Func<StoredJob, bool> predicate,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int cancelled = 0;
            foreach ((string path, StoredJob job) in LoadAll(cancellationToken).Where(item =>
                predicate(item.Job)))
            {
                if (TryCancelLoaded(path, job).Status == CaptureAnalysisJobMutationStatus.Succeeded)
                {
                    cancelled++;
                }
            }

            return cancelled;
        }
        finally
        {
            _gate.Release();
        }
    }

    private CaptureAnalysisJobMutationResult TryCancelLoaded(string path, StoredJob job)
    {
        if (job.Intent.State == CaptureAnalysisJobState.Cancelled)
        {
            return new(CaptureAnalysisJobMutationStatus.Succeeded, job.Intent);
        }

        if (job.Intent.State is CaptureAnalysisJobState.Completed or CaptureAnalysisJobState.TerminalFailure)
        {
            return new(CaptureAnalysisJobMutationStatus.InvalidTransition, job.Intent);
        }

        CaptureAnalysisJobIntent cancelled = CopyIntent(
            job.Intent,
            CaptureAnalysisJobState.Cancelled,
            nextAttemptAtUtc: null,
            latestFailure: null);
        return TryWrite(path, new(cancelled, null, null))
            ? new(CaptureAnalysisJobMutationStatus.Succeeded, cancelled)
            : new(CaptureAnalysisJobMutationStatus.Unavailable);
    }

    private static CaptureAnalysisJobIntent CreatePendingIntent(
        CaptureAnalysisJobKey key,
        DateTimeOffset enqueuedAtUtc)
    {
        return new(
            key,
            CaptureAnalysisJobState.Pending,
            0,
            enqueuedAtUtc,
            null,
            null,
            []);
    }

    private List<(string Path, StoredJob Job)> LoadAll(CancellationToken cancellationToken)
    {
        var jobs = new List<(string Path, StoredJob Job)>();
        string directoryPath = GetIntentsDirectoryPath();
        if (!Directory.Exists(directoryPath))
        {
            return jobs;
        }

        foreach (string path in Directory.EnumerateFiles(
            directoryPath,
            $"*{JobExtension}",
            SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            JobLoadResult loaded = Load(path);
            if (loaded.Status == JobLoadStatus.Known)
            {
                jobs.Add((path, loaded.Job!));
            }
        }

        return jobs;
    }

    private JobLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return JobLoadResult.Missing;
        }

        byte[]? plaintext = null;
        try
        {
            plaintext = _dataProtectionService.Unprotect(File.ReadAllBytes(path));
            using (JsonDocument header = JsonDocument.Parse(plaintext))
            {
                if (!header.RootElement.TryGetProperty("schemaVersion", out JsonElement schema) ||
                    !schema.TryGetInt32(out int schemaVersion))
                {
                    throw new InvalidDataException("The durable job schema version is missing.");
                }

                if (schemaVersion != CurrentSchemaVersion)
                {
                    return JobLoadResult.ReadOnly;
                }
            }

            CaptureAnalysisJobDocument? document = JsonSerializer.Deserialize(
                plaintext,
                CaptureAnalysisJobJsonContext.Default.CaptureAnalysisJobDocument);
            if (document == null)
            {
                throw new InvalidDataException("The durable job document is empty.");
            }

            StoredJob job = CaptureAnalysisJobDocumentMapper.ToDomain(document);
            if (!string.Equals(path, GetJobFilePath(job.Intent.Key), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The durable job identity is inconsistent.");
            }

            return JobLoadResult.Known(job);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logService.LogException(exception, "Quarantining an unreadable Capture Analysis job.");
            Quarantine(path);
            return JobLoadResult.Corrupt;
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private bool TryWrite(string path, StoredJob job)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            CaptureAnalysisJobDocumentMapper.ToDocument(
                job.Intent,
                job.LeaseToken,
                job.LeaseExpiresAtUtc,
                CurrentSchemaVersion),
            CaptureAnalysisJobJsonContext.Default.CaptureAnalysisJobDocument);
        try
        {
            _atomicFileWriter.Write(path, _dataProtectionService.Protect(plaintext));
            return true;
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, "Failed to persist a Capture Analysis job.");
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void Quarantine(string path)
    {
        try
        {
            string directory = Path.Combine(
                _localCachePathProvider.GetApplicationLocalCacheFolderPath(),
                LocalCaptureAnalysisStore.AnalysisDirectoryName,
                JobsVersionDirectoryName,
                QuarantineDirectoryName);
            Directory.CreateDirectory(directory);
            File.Move(
                path,
                Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.corrupt"),
                overwrite: false);
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, "Failed to quarantine a Capture Analysis job.");
        }
    }

    private string GetIntentsDirectoryPath()
    {
        return Path.Combine(
            _localCachePathProvider.GetApplicationLocalCacheFolderPath(),
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            JobsVersionDirectoryName,
            IntentsDirectoryName);
    }

    private static bool IsDue(StoredJob job, DateTimeOffset nowUtc)
    {
        return job.Intent.State == CaptureAnalysisJobState.Pending ||
            job.Intent.State == CaptureAnalysisJobState.RetryScheduled &&
            job.Intent.NextAttemptAtUtc <= nowUtc;
    }

    private static DateTimeOffset? GetDueTime(StoredJob job)
    {
        return job.Intent.State switch
        {
            CaptureAnalysisJobState.Pending => job.Intent.EnqueuedAtUtc,
            CaptureAnalysisJobState.RetryScheduled => job.Intent.NextAttemptAtUtc,
            CaptureAnalysisJobState.Running => job.LeaseExpiresAtUtc,
            _ => null,
        };
    }

    private static CaptureAnalysisJobIntent CopyIntent(
        CaptureAnalysisJobIntent intent,
        CaptureAnalysisJobState state,
        DateTimeOffset? nextAttemptAtUtc,
        AnalysisFailure? latestFailure,
        IEnumerable<CaptureAnalyzerAttempt>? attempts = null)
    {
        CaptureAnalyzerAttempt[] copiedAttempts = [.. attempts ?? intent.Attempts];
        return new(
            intent.Key,
            state,
            copiedAttempts.Length,
            intent.EnqueuedAtUtc,
            nextAttemptAtUtc,
            latestFailure,
            copiedAttempts);
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A job timestamp must be expressed in UTC.", parameterName);
        }
    }

    private static void EnsurePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private enum JobLoadStatus
    {
        Missing,
        Known,
        ReadOnly,
        Corrupt,
    }

    private sealed record JobLoadResult(JobLoadStatus Status, StoredJob? Job)
    {
        public static JobLoadResult Missing { get; } = new(JobLoadStatus.Missing, null);

        public static JobLoadResult ReadOnly { get; } = new(JobLoadStatus.ReadOnly, null);

        public static JobLoadResult Corrupt { get; } = new(JobLoadStatus.Corrupt, null);

        public static JobLoadResult Known(StoredJob job) => new(JobLoadStatus.Known, job);
    }

    private sealed record Mutation(CaptureAnalysisJobMutationStatus Status, StoredJob? Job)
    {
        public static Mutation Invalid(CaptureAnalysisJobMutationStatus status) => new(status, null);

        public static Mutation Success(StoredJob job) =>
            new(CaptureAnalysisJobMutationStatus.Succeeded, job);
    }
}
