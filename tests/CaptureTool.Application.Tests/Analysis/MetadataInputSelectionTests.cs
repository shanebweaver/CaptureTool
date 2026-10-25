using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class MetadataInputSelectionTests
{
    [TestMethod]
    public void LongOcrCannotStarveSpeechAndSamplingRetainsOriginalEvidenceIndexes()
    {
        var ocr = Result(new TextRecognitionMetadata(Enumerable.Range(0, 65).Select(i => new RecognizedText("word" + i))));
        var speech = Result(new TranscriptMetadata("en", [new("Spoken conclusion", TimeSpan.Zero, TimeSpan.FromSeconds(5))]));
        var record = Record(ocr, speech);
        var input = Semantic(record);
        Assert.HasCount(64, input.Entries);
        Assert.AreEqual(66L, input.Coverage.AvailableEntries);
        Assert.AreEqual(MetadataProcessingLimit.InputEntries, input.Coverage.Limits);
        Assert.HasCount(1, input.Entries.Where(e => e.Capability == AnalysisCapability.Transcription));
        Assert.AreEqual(0, input.Entries.First().EntryIndex);
        Assert.AreEqual(64, input.Entries.Last(e => e.Capability == AnalysisCapability.TextRecognition).EntryIndex);
        CollectionAssert.AreEqual(input.Entries.ToArray(), Semantic(record).Entries.ToArray());
        foreach (var entry in input.Entries)
        {
            var source = record.Results.Single(result => result.ResultId == entry.ResultId);
            Assert.AreEqual(entry.Text, new AnalysisEvidence(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length).ResolveText(source));
        }
        Assert.IsTrue(input.Inputs.Any(reference => reference.Capability == AnalysisCapability.Description && reference.ResultId == null));
    }

    [TestMethod]
    public void CharacterReservationsProtectEachSourceAndReuseSpareCapacity()
    {
        var input = Semantic(Record(
            Result(new DescriptionMetadata(Enumerable.Range(0, 20).Select(_ => new MediaDescription(new string('d', 1000))))),
            Result(new TextRecognitionMetadata(Enumerable.Range(0, 20).Select(_ => new RecognizedText(new string('o', 1000))))),
            Result(new TranscriptMetadata("en", [new("speech", TimeSpan.Zero, TimeSpan.FromSeconds(1))]))));
        Assert.HasCount(5, input.Entries);
        Assert.AreEqual(4006, input.Coverage.IncludedCharacters);
        Assert.AreEqual(MetadataProcessingLimit.InputCharacters, input.Coverage.Limits);
        Assert.IsTrue(input.Entries.Any(e => e.Capability == AnalysisCapability.Description));
        Assert.IsTrue(input.Entries.Any(e => e.Capability == AnalysisCapability.TextRecognition));
        Assert.IsTrue(input.Entries.Any(e => e.Capability == AnalysisCapability.Transcription));
        foreach (var capability in new[] { AnalysisCapability.Description, AnalysisCapability.TextRecognition })
        {
            CollectionAssert.AreEqual(new[] { 0, 19 }, input.Entries.Where(e => e.Capability == capability).Select(e => e.EntryIndex).ToArray(),
                "Character limits must preserve samples from both ends of a long source.");
        }
    }

    [TestMethod]
    public void OversizedEntriesDoNotConsumeOtherSourceReservationsOrGetTruncated()
    {
        var input = Semantic(Record(
            Result(new TextRecognitionMetadata([new(new string('x', 1025))])),
            Result(new TranscriptMetadata("en", [new("full speech", TimeSpan.Zero, TimeSpan.FromSeconds(1))]))));
        Assert.HasCount(1, input.Entries);
        Assert.AreEqual("full speech", input.Entries[0].Text);
        Assert.AreEqual(MetadataProcessingLimit.OversizedEntry, input.Coverage.Limits);
        Assert.HasCount(3, input.Inputs);
    }

    [TestMethod]
    public void CompleteSelectionAndEmptySelectionReportCoverageAccurately()
    {
        var input = Semantic(Record(Result(new TextRecognitionMetadata([new("visible text")]))));
        Assert.IsTrue(input.Coverage.IsComplete);
        Assert.AreEqual(12, input.Coverage.IncludedCharacters);
        var empty = Semantic(Record());
        Assert.IsEmpty(empty.Entries);
        Assert.IsTrue(empty.Coverage.IsComplete);
        Assert.IsNull(empty.Derivation);
    }

    [TestMethod]
    public void SelectionIsPartOfTheContractAndCancellationIsHonored()
    {
        var descriptor = MetadataEnrichmentConfiguration.CreateSemantic("test", AnalysisCapability.CaptureSynopsis);
        var inOrder = new MetadataProcessorDescriptor(descriptor.Id, descriptor.Version, descriptor.Capability, descriptor.Inputs, descriptor.Limits);
        Assert.IsFalse(descriptor.Matches(inOrder));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => MetadataProcessorInput.Create(Record(), descriptor, canceled.Token));
        Assert.AreEqual(MetadataInputSelection.InOrder, new StructuredFactsProcessor().Descriptor.InputSelection);
    }

    private static MetadataProcessorInput Semantic(CaptureAnalysisRecord record) =>
        MetadataProcessorInput.Create(record, MetadataEnrichmentConfiguration.CreateSemantic("test", AnalysisCapability.CaptureSynopsis));
    private static AnalysisResult Result(AnalysisPayload payload) => new(payload, new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "test");
    private static CaptureAnalysisRecord Record(params AnalysisResult[] results) =>
        new(CaptureId.New(), AnalysisMediaKind.Video, new(new string('a', 64)), "test", Guid.NewGuid(), results);
}
