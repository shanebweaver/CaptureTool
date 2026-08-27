using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Analyzers;

internal sealed class AutomaticCaptureAnalyzerSelectionService :
    ICaptureAnalyzerSelectionService
{
    public long Revision => 0;

    public CaptureAnalyzerSelection GetSelection(CapabilityDefinition capability) =>
        CaptureAnalyzerSelection.Automatic(capability);

    public int GetPreference(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return 0;
    }

    public bool IsAllowed(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return true;
    }

    public bool? GetFeatureEnabledOverride(AnalyzerIdentity analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        return null;
    }

    public ValueTask<CaptureAnalyzerSelectionSaveResult> SaveAsync(
        IEnumerable<CaptureAnalyzerSelection> selections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selections);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new CaptureAnalyzerSelectionSaveResult(
            CaptureAnalyzerSelectionSaveStatus.Unavailable));
    }
}
