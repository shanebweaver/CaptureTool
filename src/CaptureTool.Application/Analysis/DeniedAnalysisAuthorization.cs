using CaptureTool.Application.Abstractions.Analysis;

namespace CaptureTool.Application.Analysis;

/// <summary>Safe composition until the consent workflow supplies a durable authorization implementation.</summary>
internal sealed class DeniedAnalysisAuthorization : IAnalysisAuthorization
{
    public ValueTask<IAnalysisAuthorizationLease> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IAnalysisAuthorizationLease>(new DeniedLease());
    }

    private sealed class DeniedLease : IAnalysisAuthorizationLease
    {
        public bool IsAllowed => false;
        public Guid Revision => Guid.Empty;
        public CancellationToken Revoked => new(canceled: true);
        public void Dispose() { }
    }
}
