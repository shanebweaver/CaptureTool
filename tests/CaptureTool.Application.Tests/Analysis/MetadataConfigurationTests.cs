using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class MetadataConfigurationTests
{
    [TestMethod]
    public void MetadataCannotPrecedeMediaOrMixCandidateContracts()
    {
        var facts = new StructuredFactsProcessor().Descriptor;
        AnalysisStep media = new(AnalysisCapability.TextRecognition, ["ocr"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        AnalysisStep derived = new(facts.Capability, [facts.Id], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        MediaAnalyzerDescriptor[] analyzers = [new("ocr", AnalysisCapability.TextRecognition, [AnalysisMediaKind.Image])];
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "v1", [derived, media])])
            .ValidateAnalyzers(analyzers, [facts]));
        var incompatible = new MetadataProcessorDescriptor("other", "1", facts.Capability, [AnalysisCapability.Description], facts.Limits);
        var plan = new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "v1", [media,
            new(facts.Capability, [facts.Id, incompatible.Id], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))])]);
        Assert.ThrowsExactly<ArgumentException>(() => plan.ValidateAnalyzers(analyzers, [facts, incompatible]));
        Assert.ThrowsExactly<ArgumentException>(() => plan.ValidateAnalyzers(analyzers, [facts, facts]));
        Assert.ThrowsExactly<ArgumentException>(() => new MetadataProcessorDescriptor("cycle", "1", AnalysisCapability.CaptureSynopsis,
            [AnalysisCapability.StructuredFacts], facts.Limits));
        Assert.ThrowsExactly<ArgumentException>(() => new MetadataProcessorDescriptor("basic-overwrite", "1", AnalysisCapability.Description,
            [AnalysisCapability.TextRecognition], facts.Limits));
    }

    [TestMethod]
    public void SuggestionsRemainBoundedAndRequireEvidenceEvenWhenTheModelIsConfident()
    {
        var evidence = new AnalysisEvidence(Guid.NewGuid(), 0, 0, 1);
        var coverage = new MetadataProcessingCoverage(1, 1, 1, MetadataProcessingLimit.None);
        Assert.ThrowsExactly<ArgumentException>(() => new SuggestedText("unsupported", []));
        Assert.ThrowsExactly<ArgumentException>(() => new SuggestedText("bad\ntext", [evidence]));
        Assert.ThrowsExactly<ArgumentException>(() => new SuggestedText("bad\ud800", [evidence]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureSynopsisMetadata(new(new string('x', 161), [evidence]), [], coverage));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureClassificationMetadata(CaptureCategory.Document, [], [], coverage));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureClassificationMetadata(null, [evidence], [], coverage));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureClassificationMetadata(null, [], [new("Uppercase", [evidence])], coverage));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureClassificationMetadata(null, [], Enumerable.Range(0, 6).Select(index => new SuggestedText("topic" + index, [evidence])), coverage));
    }
}
