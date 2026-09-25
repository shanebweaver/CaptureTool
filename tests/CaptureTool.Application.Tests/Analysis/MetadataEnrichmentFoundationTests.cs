using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class MetadataEnrichmentFoundationTests
{
    private const string Value = "https://example.test";

    [TestMethod]
    public void ReplacingConsultedInputInvalidatesFactsEvenWhenReplacementIsEmptyOrIdentical()
    {
        AnalysisResult input = Ocr(Value);
        CaptureAnalysisRecord record = Record(input, Facts(input));
        foreach (AnalysisResult replacement in new[] { Ocr(Value), Result(new TextRecognitionMetadata([])) })
        {
            CaptureAnalysisRecord updated = record.WithResult(replacement);
            Assert.HasCount(1, updated.Results);
            Assert.AreEqual(replacement.ResultId, updated.Results.Single().ResultId);
            Assert.HasCount(2, record.Results, "The earlier immutable snapshot must not change.");
        }
    }

    [TestMethod]
    public void AbsentConsultedInputAppearingInvalidatesButUnrelatedInputDoesNot()
    {
        AnalysisResult input = Ocr(Value);
        AnalysisResult facts = Facts(input, new AnalysisInputReference(AnalysisCapability.QrCodeDetection, null));
        CaptureAnalysisRecord record = Record(input, facts);
        Assert.HasCount(3, record.WithResult(Result(new DescriptionMetadata([new("description")]))).Results);
        CaptureAnalysisRecord updated = record.WithResult(Result(new QrCodeMetadata([])));
        Assert.HasCount(2, updated.Results);
        Assert.IsFalse(updated.Results.Any(result => result.Payload is StructuredFactsMetadata));
    }

    [TestMethod]
    public void RefreshKeepsUnchangedInputsAndFactsButChangedSourceClearsBoth()
    {
        AnalysisResult input = Ocr(Value);
        CaptureAnalysisRecord record = Record(input, Facts(input));
        CaptureAnalysisRecord refresh = record.StartRun(record.SourceRevision, "v2", Guid.NewGuid());
        CollectionAssert.AreEqual(record.Results.Select(result => result.ResultId).ToArray(), refresh.Results.Select(result => result.ResultId).ToArray());
        Assert.IsTrue(refresh.Results.All(result => result.PlanVersion == "v1"));
        Assert.IsEmpty(record.StartRun(new(new string('b', 64)), "v2", Guid.NewGuid()).Results);
    }

    [TestMethod]
    public void StaleSnapshotsAndReusedIdentitiesCannotBePublished()
    {
        AnalysisResult input = Ocr(Value);
        AnalysisResult facts = Facts(input);
        CaptureAnalysisRecord changed = Record(input).WithResult(Ocr(Value));
        Assert.IsFalse(changed.HasCurrentInputs(facts));
        Assert.ThrowsExactly<ArgumentException>(() => changed.WithResult(facts));
        Assert.ThrowsExactly<ArgumentException>(() => Record(changed.Results.Single(), facts));
        Assert.ThrowsExactly<ArgumentException>(() => Record(input).WithResult(input));
        AnalysisResult reused = new(new DescriptionMetadata([]), input.Producer, input.GeneratedAt, "v1", resultId: input.ResultId);
        Assert.ThrowsExactly<ArgumentException>(() => Record(input, reused));
    }

    [TestMethod]
    public void EvidenceMustBelongToDeclaredInputsAndMatchLiteralObservedText()
    {
        AnalysisResult input = Ocr(Value);
        AnalysisResult other = Result(new QrCodeMetadata([new(Value, new(0, 0, 1, 1))]));
        var undeclared = Result(new StructuredFactsMetadata([new(StructuredFactKind.Url, Value, [new(other.ResultId, 0, 0, Value.Length)])]),
            new([new(input.Payload.Capability, input.ResultId)]));
        Assert.ThrowsExactly<ArgumentException>(() => Record(input, other, undeclared));
        var mismatch = Result(new StructuredFactsMetadata([new(StructuredFactKind.Url, "invented", [new(input.ResultId, 0, 0, Value.Length)])]),
            new([new(input.Payload.Capability, input.ResultId)]));
        Assert.ThrowsExactly<ArgumentException>(() => Record(input, mismatch));
        AnalysisResult description = Result(new DescriptionMetadata([new(Value)]));
        Assert.ThrowsExactly<ArgumentException>(() => Record(description, Facts(description)));
    }

    [TestMethod]
    public void QrAndTranscriptEvidenceRetainsSourceLocationsWithoutCopyingMediaCoordinates()
    {
        var qrPayload = new QrCodeMetadata([new(Value, new(.1, .2, .3, .4), TimeSpan.FromSeconds(5))]);
        var transcriptPayload = new TranscriptMetadata("en", [new(Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))]);
        foreach (AnalysisPayload payload in new AnalysisPayload[] { qrPayload, transcriptPayload })
        {
            AnalysisResult source = Result(payload);
            CaptureAnalysisRecord record = Record(source, Facts(source));
            var fact = ((StructuredFactsMetadata)record.Results[1].Payload).Facts.Single();
            Assert.AreEqual(Value, fact.Evidence.Single().ResolveText(source));
            Assert.AreSame(payload, record.Results[0].Payload);
        }
    }

    [TestMethod]
    public void EvidenceOffsetsAreUtf16AndRejectOverflowOrSplitSurrogates()
    {
        AnalysisResult source = Ocr("😀 " + Value);
        Assert.AreEqual(Value, new AnalysisEvidence(source.ResultId, 0, 3, Value.Length).ResolveText(source));
        Assert.AreEqual("😀", new AnalysisEvidence(source.ResultId, 0, 0, 2).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(source.ResultId, 0, 1, 1).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(source.ResultId, 0, 0, 1).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(source.ResultId, 0, 1, int.MaxValue).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(source.ResultId, 0, int.MaxValue, 1).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(source.ResultId, 1, 0, 1).ResolveText(source));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisEvidence(Guid.NewGuid(), 0, 0, 1).ResolveText(source));
    }

    [TestMethod]
    public void InputContractsRejectCyclesDuplicatesAndNoPresentInput()
    {
        AnalysisResult input = Ocr(Value);
        var reference = new AnalysisInputReference(input.Payload.Capability, input.ResultId);
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisInputReference(AnalysisCapability.StructuredFacts, Guid.NewGuid()));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisInputReference(new("text-recognition", 2), Guid.NewGuid()));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisInputReference(AnalysisCapability.Description, Guid.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisDerivation([]));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisDerivation([new(AnalysisCapability.Description, null)]));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisDerivation([reference, reference]));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisDerivation([reference, new(AnalysisCapability.Description, input.ResultId)]));
        Assert.ThrowsExactly<ArgumentException>(() => Result(new StructuredFactsMetadata([])));
        Assert.ThrowsExactly<ArgumentException>(() => Result(new TextRecognitionMetadata([]), new([reference])));
    }

    [TestMethod]
    public void SuccessfulEmptyFactsStillDependOnSuccessfulEmptyInput()
    {
        AnalysisResult input = Result(new TextRecognitionMetadata([]));
        AnalysisResult empty = Result(new StructuredFactsMetadata([]), new([new(input.Payload.Capability, input.ResultId)]));
        Assert.HasCount(2, Record(input, empty).Results);
        Assert.HasCount(1, Record(input, empty).WithResult(Ocr(Value)).Results);
    }

    [TestMethod]
    public void DerivedCollectionsAreImmutableAndRejectUnboundedOrAmbiguousFacts()
    {
        AnalysisResult input = Ocr(Value);
        AnalysisInputReference[] references = [new(input.Payload.Capability, input.ResultId)];
        var derivation = new AnalysisDerivation(references);
        references[0] = new(AnalysisCapability.Description, null);
        Assert.AreEqual(input.ResultId, derivation.Inputs.Single().ResultId);
        AnalysisEvidence[] evidence = [new(input.ResultId, 0, 0, Value.Length)];
        var fact = new StructuredFact(StructuredFactKind.Url, Value, evidence);
        StructuredFact[] facts = [fact];
        var payload = new StructuredFactsMetadata(facts);
        evidence[0] = new(Guid.NewGuid(), 0, 0, 1);
        facts[0] = new(StructuredFactKind.Date, "2026-09-24", [evidence[0]]);
        Assert.AreSame(fact, payload.Facts.Single());
        Assert.AreEqual(input.ResultId, fact.Evidence.Single().ResultId);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<StructuredFact>)payload.Facts).Clear());
        Assert.ThrowsExactly<ArgumentException>(() => new StructuredFactsMetadata([fact, fact]));
        Assert.ThrowsExactly<ArgumentException>(() => new StructuredFact(StructuredFactKind.Url, Value, []));
        Assert.ThrowsExactly<ArgumentException>(() => new StructuredFact(StructuredFactKind.Url, Value, [evidence[0], evidence[0]]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StructuredFact((StructuredFactKind)99, Value, evidence));
        Assert.ThrowsExactly<ArgumentException>(() => new StructuredFact(StructuredFactKind.Url, new string('a', 4097), evidence));
    }

    private static AnalysisResult Ocr(string text) => Result(new TextRecognitionMetadata([new(text)]));
    private static AnalysisResult Result(AnalysisPayload payload, AnalysisDerivation? derivation = null) =>
        new(payload, new("test", "local", "rules", "1"), DateTimeOffset.UtcNow, "v1", derivation: derivation);
    private static AnalysisResult Facts(AnalysisResult source, params AnalysisInputReference[] additional) =>
        Result(new StructuredFactsMetadata([new(StructuredFactKind.Url, Value, [new(source.ResultId, 0, 0, Value.Length)])]),
            new(new[] { new AnalysisInputReference(source.Payload.Capability, source.ResultId) }.Concat(additional)));
    private static CaptureAnalysisRecord Record(params AnalysisResult[] results) =>
        new(CaptureId.New(), AnalysisMediaKind.Video, new(new string('a', 64)), "v1", Guid.NewGuid(), results);
}
