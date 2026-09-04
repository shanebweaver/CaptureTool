using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Analyzers;

public sealed class CaptureAnalyzerCatalog : ICaptureAnalyzerCatalog
{
    private readonly IReadOnlyList<ICaptureAnalyzer> _analyzers;

    public CaptureAnalyzerCatalog(IEnumerable<ICaptureAnalyzer> analyzers)
    {
        ArgumentNullException.ThrowIfNull(analyzers);
        ICaptureAnalyzer[] copiedAnalyzers = [.. analyzers];
        if (copiedAnalyzers.Any(analyzer => analyzer == null))
        {
            throw new ArgumentException("The analyzer catalog cannot contain null entries.", nameof(analyzers));
        }

        IGrouping<DescriptorKey, ICaptureAnalyzer>? duplicate = copiedAnalyzers
            .GroupBy(analyzer => DescriptorKey.From(analyzer.Descriptor))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException(
                $"Duplicate analyzer descriptor '{duplicate.Key}' was registered.",
                nameof(analyzers));
        }

        _analyzers = Array.AsReadOnly(copiedAnalyzers);
    }

    public IReadOnlyList<ICaptureAnalyzer> Analyzers => _analyzers;

    public ICaptureAnalyzer? Find(
        AnalyzerRevision revision,
        CapabilityDefinition capability)
    {
        if (revision.IsEmpty)
        {
            throw new ArgumentException("An analyzer lookup requires a revision.", nameof(revision));
        }

        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("An analyzer lookup requires a capability.", nameof(capability));
        }

        return _analyzers.FirstOrDefault(analyzer =>
            analyzer.Descriptor.Revision == revision &&
            analyzer.Descriptor.Capability == capability);
    }

    private readonly record struct DescriptorKey(
        AnalysisCapabilityId CapabilityId,
        CapabilitySchemaVersion SchemaVersion,
        AnalyzerRevision AnalyzerRevision,
        ProcessingBoundary ProcessingBoundary)
    {
        public static DescriptorKey From(CaptureAnalyzerDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new(
                descriptor.Capability.Id,
                descriptor.Capability.SchemaVersion,
                descriptor.Revision,
                descriptor.ProcessingBoundary);
        }
    }
}
