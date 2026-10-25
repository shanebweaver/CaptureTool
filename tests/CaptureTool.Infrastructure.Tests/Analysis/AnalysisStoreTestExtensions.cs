using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Tests.Analysis;

/// <summary>Test setup uses the same admission and atomic step commits as the worker.</summary>
internal static class AnalysisStoreTestExtensions
{
    public static async Task<AnalysisRunToken> StartRunAsync(this IAnalysisExecutionStore store, CaptureId id,
        AnalysisMediaKind kind, SourceRevision revision, string version, CancellationToken ct,
        params AnalysisCapability[] capabilities)
    {
        if (capabilities.Length == 0) capabilities = [AnalysisCapability.Description];
        var scope = await store.GetAdmissionScopeAsync(ct);
        var prior = await store.GetWorkAsync(id, ct);
        var request = new AnalysisRequest(id, kind, Path.Combine(Path.GetTempPath(), id + ".bin"),
            Guid.NewGuid(), scope.Generation, prior?.Run.Id);
        var plan = new MediaAnalysisPlan(kind, version, capabilities.Select(capability =>
            new AnalysisStep(capability, ["test"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))));
        Assert.IsTrue(await store.AdmitAsync(request, Guid.NewGuid(), plan, ct));
        var token = new AnalysisRunToken(id, scope.Generation, request.RequestId);
        Assert.IsTrue(await store.BindSourceAsync(token, revision, ct));
        return token;
    }

    public static Task<bool> PublishAsync(this IAnalysisExecutionStore store, AnalysisRunToken token,
        AnalysisResult result, CancellationToken ct) => store.CommitStepAsync(token,
            new(result.Payload.Capability, AnalyzerOutcomeKind.Succeeded, null),
            new(result.Payload, result.Producer, result.GeneratedAt, result.PlanVersion, token.RunId,
                result.ResultId, result.Derivation), ct);
}
