using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Presentation.Tests.Features;

internal sealed class TestAnalysisAvailability(bool supported = true) : IAnalysisFeatureAvailability
{
    public Func<AnalysisMediaKind, AnalysisCapability, Task<bool>> Query { get; set; } = (_, _) => Task.FromResult(supported);
    public Task<bool> IsSupportedAsync(AnalysisMediaKind kind, AnalysisCapability capability, CancellationToken cancellationToken = default) => Query(kind, capability);
    public void Invalidate() { }
}
