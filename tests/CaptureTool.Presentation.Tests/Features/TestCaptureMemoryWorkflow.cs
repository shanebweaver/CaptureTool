using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Presentation.Tests.Features;

internal sealed class TestCaptureMemoryWorkflow : ICaptureMemoryWorkflow
{
    public CaptureMemoryWorkflowSnapshot Current { get; set; } = new(Policy(true), null);
    public Func<CancellationToken, ValueTask<CaptureMemoryWorkflowSnapshot>>? Read { get; set; }
    public Func<CaptureMemoryOperationRequest, Task<CaptureMemoryOperation>>? Execute { get; set; }
    public List<CaptureMemoryOperationRequest> Requests { get; } = [];
    public List<Guid> Cancellations { get; } = [];
    public event EventHandler? Changed;
    public void Publish() => Changed?.Invoke(this, EventArgs.Empty);
    public ValueTask<CaptureMemoryWorkflowSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Read?.Invoke(cancellationToken) ?? ValueTask.FromResult(Current);
    public async Task<CaptureMemoryOperation> ExecuteAsync(CaptureMemoryOperationRequest request, CancellationToken cancellationToken = default)
    {
        Assert.IsFalse(cancellationToken.CanBeCanceled, "A page must not own command cancellation.");
        Requests.Add(request);
        if (Execute != null) { return await Execute(request); }
        CaptureMemoryOperation result = Operation(request.Kind, CaptureMemoryOperationStatus.Succeeded);
        Current = Current with { Operation = result };
        Publish();
        return result;
    }
    public Task ResumeAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException("Only startup owns recovery.");
    public void Cancel(Guid operationId) => Cancellations.Add(operationId);

    public static CaptureMemoryOperation Operation(CaptureMemoryOperationKind kind,
        CaptureMemoryOperationStatus status = CaptureMemoryOperationStatus.Running,
        CaptureMemoryOperationPhase phase = CaptureMemoryOperationPhase.PreparingModels,
        bool scheduled = false) => new(Guid.NewGuid(), new(kind), DateTimeOffset.UtcNow, 0, 1,
            status == CaptureMemoryOperationStatus.Running ? phase : CaptureMemoryOperationPhase.Finished, status,
            isSchedulingComplete: scheduled);

    public static CaptureAnalysisPolicySnapshot Policy(bool authorized, bool future = true,
        CaptureAnalysisExclusionReason reason = CaptureAnalysisExclusionReason.None, bool enrolled = true)
    {
        CaptureAnalysisPolicy policy = authorized ? CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(), 1) : CaptureAnalysisPolicy.Unknown;
        if (authorized && !future) { policy = policy.StopFutureCaptures(1); }
        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        CaptureAnalysisEnrollment[] entries = !enrolled || !authorized ? [] :
            [new(CaptureId.New(), reason == CaptureAnalysisExclusionReason.None ? CaptureAnalysisEnrollmentState.Enrolled :
                CaptureAnalysisEnrollmentState.Excluded, reason, 1, reason == CaptureAnalysisExclusionReason.None ? 0 : 1,
                1, reason == CaptureAnalysisExclusionReason.None ? recipe.Id : null,
                reason == CaptureAnalysisExclusionReason.None ? recipe.Version : null)];
        return new(CaptureAnalysisPolicySnapshotStatus.Available, policy.ConsentState,
            new(1, new(policy, entries)));
    }
}
