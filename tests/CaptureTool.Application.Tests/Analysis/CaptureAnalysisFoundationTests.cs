using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class CaptureAnalysisFoundationTests
{
    [TestMethod]
    public void PlansPreserveBothOrdersAndDefensivelyCopyConfiguration()
    {
        string[] candidates = ["preferred", "fallback"];
        AnalysisStep first = Step(AnalysisCapability.TextRecognition, candidates);
        AnalysisStep second = Step(AnalysisCapability.Description, ["description"]);
        AnalysisStep[] steps = [first, second];
        var plan = new MediaAnalysisPlan(AnalysisMediaKind.Image, "v1", steps);
        candidates[0] = "mutated";
        steps[0] = second;

        CollectionAssert.AreEqual(new[] { AnalysisCapability.TextRecognition, AnalysisCapability.Description },
            plan.Steps.Select(step => step.Capability).ToArray());
        CollectionAssert.AreEqual(new[] { "preferred", "fallback" }, plan.Steps[0].Candidates.ToArray());
    }

    [TestMethod]
    public void AnotherModelForExistingCapabilityRequiresOnlyConfigurationAndDescriptor()
    {
        var configuration = new CaptureAnalysisConfiguration([
            new(AnalysisMediaKind.Image, "v2", [Step(AnalysisCapability.TextRecognition, ["new-model", "existing-model"])])]);
        configuration.ValidateAnalyzers([
            new("new-model", AnalysisCapability.TextRecognition, [AnalysisMediaKind.Image]),
            new("existing-model", AnalysisCapability.TextRecognition, [AnalysisMediaKind.Image]),
        ]);
        Assert.AreEqual("new-model", configuration.Plans[0].Steps[0].Candidates[0]);
    }

    [TestMethod]
    public void DefaultPlansHaveExplicitMediaSpecificOrderAndStableFallbackIdentities()
    {
        CaptureAnalysisConfiguration configuration = CaptureAnalysisConfiguration.CreateDefault();
        Assert.HasCount(3, configuration.Plans);
        MediaAnalysisPlan video = configuration.Plans.Single(plan => plan.MediaKind == AnalysisMediaKind.Video);
        CollectionAssert.AreEqual(new[] { AnalysisCapability.TextRecognition, AnalysisCapability.Transcription, AnalysisCapability.Description },
            video.Steps.Select(step => step.Capability).ToArray());
        CollectionAssert.AreEqual(new[] { "windows-ai-video-frame-ocr", "windows-video-frame-ocr" }, video.Steps[0].Candidates.ToArray());
    }

    [TestMethod]
    public void AmbiguousOrUnboundedConfigurationIsRejected()
    {
        AnalysisStep step = Step(AnalysisCapability.TextRecognition, ["ocr"]);
        Assert.ThrowsExactly<ArgumentException>(() => Step(AnalysisCapability.TextRecognition, ["ocr", "ocr"]));
        Assert.ThrowsExactly<ArgumentException>(() => new MediaAnalysisPlan(AnalysisMediaKind.Image, "v1", [step, step]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AnalysisStep(AnalysisCapability.TextRecognition,
            ["ocr"], Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AnalysisStep(AnalysisCapability.TextRecognition,
            ["ocr"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), retryCount: 100));
    }

    [TestMethod]
    public void MissingOrIncompatibleAdaptersFailComposition()
    {
        var configuration = new CaptureAnalysisConfiguration([
            new(AnalysisMediaKind.Image, "v1", [Step(AnalysisCapability.TextRecognition, ["ocr"])])]);
        Assert.ThrowsExactly<ArgumentException>(() => configuration.ValidateAnalyzers([]));
        Assert.ThrowsExactly<ArgumentException>(() => configuration.ValidateAnalyzers([
            new("ocr", AnalysisCapability.Transcription, [AnalysisMediaKind.Image]) ]));
        Assert.ThrowsExactly<ArgumentException>(() => configuration.ValidateAnalyzers([
            new("ocr", AnalysisCapability.TextRecognition, [AnalysisMediaKind.Video]) ]));
        Assert.ThrowsExactly<ArgumentException>(() => configuration.ValidateAnalyzers([
            new("ocr", new AnalysisCapability("text-recognition", 2), [AnalysisMediaKind.Image]) ]));
    }

    [TestMethod]
    public void EmptyRecognitionIsSuccessAndFailuresCannotContainPayloads()
    {
        AnalyzerOutcome empty = AnalyzerOutcome.Success(new TextRecognitionMetadata([]), Producer());
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, empty.Kind);
        Assert.IsNull(empty.FailureCode);
        Assert.IsNotNull(empty.Payload);
        AnalyzerOutcome failed = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.TemporarilyUnavailable, "model-not-ready");
        Assert.IsNull(failed.Payload);
        Assert.IsNull(failed.Producer);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Succeeded, "bad"));
        Assert.ThrowsExactly<ArgumentException>(() => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "private source text"));
    }

    [TestMethod]
    public void RefreshPreservesSuccessfulResultsAndTheirOriginalPlanProvenance()
    {
        SourceRevision source = Revision('a');
        var result = new AnalysisResult(new DescriptionMetadata([new("original")]), Producer(), DateTimeOffset.UtcNow, "v1");
        var original = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, source, "v1", Guid.NewGuid(), [result]);
        CaptureAnalysisRecord refreshing = original.StartRun(source, "v2", Guid.NewGuid());
        Assert.AreSame(result, refreshing.Results.Single());
        Assert.AreEqual("v1", refreshing.Results.Single().PlanVersion);
        Assert.AreEqual("v2", refreshing.PlanVersion);

        var replacement = new AnalysisResult(new DescriptionMetadata([new("replacement")]), Producer(), DateTimeOffset.UtcNow, "v2");
        Assert.AreSame(replacement, refreshing.WithResult(replacement).Results.Single());
        Assert.ThrowsExactly<ArgumentException>(() => refreshing.WithResult(result));
        Assert.IsEmpty(original.StartRun(Revision('b'), "v2", Guid.NewGuid()).Results);
        Assert.HasCount(1, original.Results);
    }

    [TestMethod]
    public void PayloadCollectionsCannotChangeAfterAResultIsPublished()
    {
        RecognizedText[] regions = [new("original")];
        var payload = new TextRecognitionMetadata(regions);
        regions[0] = new("mutated");
        Assert.AreEqual("original", payload.Regions[0].Text);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<RecognizedText>)payload.Regions).Add(new("extra")));
    }

    [TestMethod]
    public void InvalidCoordinatesTimesAndMediaCannotBecomeCanonicalMetadata()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NormalizedBounds(double.NaN, 0, 1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NormalizedBounds(0.8, 0, 0.3, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TranscriptSegment("text", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new TranscriptMetadata("en", [
            new("later", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)),
            new("earlier", TimeSpan.Zero, TimeSpan.FromSeconds(1)),
        ]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image,
            Revision('a'), "v1", Guid.NewGuid(), [new(new TranscriptMetadata("en", []), Producer(), DateTimeOffset.UtcNow, "v1")]));
    }

    private static AnalysisStep Step(AnalysisCapability capability, string[] candidates) =>
        new(capability, candidates, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

    private static SourceRevision Revision(char value) => new(new string(value, 64));
    private static AnalyzerProvenance Producer() => new("test-adapter", "local-provider", "model", "1", "2");
}
