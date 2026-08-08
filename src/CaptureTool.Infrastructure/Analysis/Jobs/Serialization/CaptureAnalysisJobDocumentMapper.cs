using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Analysis.Jobs.Serialization;

internal static class CaptureAnalysisJobDocumentMapper
{
    public static CaptureAnalysisJobDocument ToDocument(
        CaptureAnalysisJobIntent intent,
        CaptureAnalysisJobLeaseToken? leaseToken,
        DateTimeOffset? leaseExpiresAtUtc,
        int schemaVersion)
    {
        return new()
        {
            SchemaVersion = schemaVersion,
            Intent = new CaptureAnalysisJobIntentDocument
            {
                Key = ToDocument(intent.Key),
                State = intent.State,
                AttemptCount = intent.AttemptCount,
                EnqueuedAtUtc = intent.EnqueuedAtUtc,
                NextAttemptAtUtc = intent.NextAttemptAtUtc,
                LatestFailureCode = intent.LatestFailure?.Code,
                LatestFailureDisposition = intent.LatestFailure?.Disposition,
                Attempts = [.. intent.Attempts.Select(ToDocument)],
            },
            LeaseToken = leaseToken?.Value,
            LeaseExpiresAtUtc = leaseExpiresAtUtc,
        };
    }

    public static StoredJob ToDomain(CaptureAnalysisJobDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CaptureAnalysisJobIntentDocument intent = document.Intent;
        AnalysisFailure? latestFailure = ToFailure(
            intent.LatestFailureCode,
            intent.LatestFailureDisposition);
        var domainIntent = new CaptureAnalysisJobIntent(
            ToDomain(intent.Key),
            intent.State,
            intent.AttemptCount,
            intent.EnqueuedAtUtc,
            intent.NextAttemptAtUtc,
            latestFailure,
            intent.Attempts.Select(ToDomain));
        CaptureAnalysisJobLeaseToken? leaseToken = document.LeaseToken.HasValue
            ? new CaptureAnalysisJobLeaseToken(document.LeaseToken.Value)
            : null;
        if ((domainIntent.State == CaptureAnalysisJobState.Running) !=
            (leaseToken.HasValue && document.LeaseExpiresAtUtc.HasValue))
        {
            throw new InvalidDataException("The durable job lease is inconsistent with its state.");
        }

        return new(domainIntent, leaseToken, document.LeaseExpiresAtUtc);
    }

    private static CaptureAnalysisJobKeyDocument ToDocument(CaptureAnalysisJobKey key)
    {
        AnalysisCommitPreconditions preconditions = key.Preconditions;
        return new()
        {
            Preconditions = new AnalysisCommitPreconditionsDocument
            {
                CaptureId = preconditions.CaptureId.ToString(),
                CaptureSourceGeneration = preconditions.CaptureSourceGeneration,
                SourceLength = preconditions.SourceRevision.Length,
                SourceLastWriteTimeUtc = preconditions.SourceRevision.LastWriteTimeUtc,
                SourceFingerprint = preconditions.SourceRevision.Fingerprint.Value,
                PurposeId = preconditions.Purpose.Id,
                PurposeVersion = preconditions.Purpose.Version,
                PolicyRevision = preconditions.PolicyRevision,
                ControlGeneration = preconditions.ControlGeneration,
                EnrollmentGeneration = preconditions.EnrollmentGeneration,
                TombstoneGeneration = preconditions.TombstoneGeneration,
                RecipeId = preconditions.RecipeId.Value,
                RecipeVersion = preconditions.RecipeVersion.Value,
                ResolutionPolicyRevision = preconditions.ResolutionPolicyRevision,
            },
            Capability = ToDocument(key.Capability),
            AuthorizedProcessingBoundary = key.AuthorizedProcessingBoundary,
        };
    }

    private static CaptureAnalysisJobKey ToDomain(CaptureAnalysisJobKeyDocument document)
    {
        AnalysisCommitPreconditionsDocument expected = document.Preconditions;
        var stamp = new ProvisionalSourceStamp(expected.SourceLength, expected.SourceLastWriteTimeUtc);
        var sourceRevision = new SourceRevision(
            expected.SourceLength,
            expected.SourceLastWriteTimeUtc,
            ContentFingerprint.Sha256(expected.SourceFingerprint));
        var preconditions = new AnalysisCommitPreconditions(
            CaptureId.Parse(expected.CaptureId),
            expected.CaptureSourceGeneration,
            stamp,
            sourceRevision,
            new AnalysisPurpose(expected.PurposeId, expected.PurposeVersion),
            expected.PolicyRevision,
            expected.ControlGeneration,
            expected.EnrollmentGeneration,
            expected.TombstoneGeneration,
            new AnalysisRecipeId(expected.RecipeId),
            new AnalysisRecipeVersion(expected.RecipeVersion),
            expected.ResolutionPolicyRevision);
        return new(
            preconditions,
            ToDomain(document.Capability),
            document.AuthorizedProcessingBoundary);
    }

    private static CapabilityDefinitionDocument ToDocument(CapabilityDefinition capability)
    {
        return new()
        {
            Id = capability.Id.Value,
            SchemaVersion = capability.SchemaVersion.Value,
            Classification = capability.Classification,
        };
    }

    private static CapabilityDefinition ToDomain(CapabilityDefinitionDocument document)
    {
        return new(
            new AnalysisCapabilityId(document.Id),
            new CapabilitySchemaVersion(document.SchemaVersion),
            document.Classification);
    }

    private static CaptureAnalyzerAttemptDocument ToDocument(CaptureAnalyzerAttempt attempt)
    {
        return new()
        {
            AttemptNumber = attempt.AttemptNumber,
            Analyzer = ToDocument(attempt.Analyzer),
            ProcessingBoundary = attempt.ProcessingBoundary,
            StartedAtUtc = attempt.StartedAtUtc,
            CompletedAtUtc = attempt.CompletedAtUtc,
            Status = attempt.Status,
            FailureCode = attempt.Failure?.Code,
            FailureDisposition = attempt.Failure?.Disposition,
        };
    }

    private static CaptureAnalyzerAttempt ToDomain(CaptureAnalyzerAttemptDocument document)
    {
        return new(
            document.AttemptNumber,
            ToDomain(document.Analyzer),
            document.ProcessingBoundary,
            document.StartedAtUtc,
            document.CompletedAtUtc,
            document.Status,
            ToFailure(document.FailureCode, document.FailureDisposition));
    }

    private static AnalyzerIdentityDocument ToDocument(AnalyzerIdentity analyzer)
    {
        return new()
        {
            AnalyzerId = analyzer.AnalyzerId,
            ProviderId = analyzer.ProviderId,
            ModelId = analyzer.ModelId,
            ModelVersion = analyzer.ModelVersion,
            AdapterVersion = analyzer.AdapterVersion,
            RuntimeId = analyzer.RuntimeId,
            RuntimeVersion = analyzer.RuntimeVersion,
            PackageVersion = analyzer.PackageVersion,
            ConfigurationFingerprint = analyzer.ConfigurationFingerprint,
        };
    }

    private static AnalyzerIdentity ToDomain(AnalyzerIdentityDocument document)
    {
        return new(
            document.AnalyzerId,
            document.ProviderId,
            document.ModelId,
            document.ModelVersion,
            document.AdapterVersion,
            document.RuntimeId,
            document.RuntimeVersion,
            document.PackageVersion,
            document.ConfigurationFingerprint);
    }

    private static AnalysisFailure? ToFailure(
        AnalysisFailureCode? code,
        AnalysisFailureDisposition? disposition)
    {
        if (code.HasValue != disposition.HasValue)
        {
            throw new InvalidDataException("A durable job contains an incomplete bounded failure.");
        }

        return code.HasValue ? new AnalysisFailure(code.Value, disposition!.Value) : null;
    }
}

internal sealed record StoredJob(
    CaptureAnalysisJobIntent Intent,
    CaptureAnalysisJobLeaseToken? LeaseToken,
    DateTimeOffset? LeaseExpiresAtUtc);
