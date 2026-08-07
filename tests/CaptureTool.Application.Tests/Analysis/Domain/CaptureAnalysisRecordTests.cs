using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class CaptureAnalysisRecordTests
{
    [TestMethod]
    public void IndependentSuccesses_ShouldMakeRequiredRecipeUsableWithoutOptionalResult()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();

        Assert.IsFalse(record.IsUsable);
        Assert.AreEqual(CapabilityCommitResult.Committed, Commit(
            record,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 50)),
            analyzer));
        Assert.IsFalse(record.IsUsable);
        Assert.AreEqual(CapabilityCommitResult.Committed, Commit(
            record,
            CreateOcrPayload("hello"),
            analyzer));

        Assert.IsTrue(record.IsUsable);
        Assert.HasCount(2, record.Analyses);
        IReadOnlyList<RecipeCapability> pendingCapabilities = record.GetCapabilitiesNeedingAnalysis();
        Assert.HasCount(1, pendingCapabilities);
        RecipeCapability pending = pendingCapabilities[0];
        Assert.AreEqual(AnalysisCapabilities.ImageDescriptionV1, pending.Capability);
    }

    [TestMethod]
    public void TerminalOutcome_ShouldPreserveSameCapabilityCanonicalResultAndOtherCapabilities()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, new MediaPropertiesV1(CaptureMediaKind.Image), analyzer);
        Commit(record, CreateOcrPayload("current text"), analyzer);
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions();
        AnalysisCommitToken token = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, current);
        var outcome = new CapabilityOutcome(
            AnalysisTestData.CaptureId,
            AnalysisTestData.CreateSource(),
            AnalysisCapabilities.OcrDocumentV1,
            analyzer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.TerminalFailure,
            new AnalysisFailure(AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal),
            AnalysisTestData.GeneratedAtUtc.AddSeconds(1));

        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryRecordOutcome(token, current, analyzer.Revision, outcome));
        Assert.IsTrue(record.IsUsable);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out CapabilityAnalysis? analysis));
        Assert.IsNotNull(analysis!.CanonicalResult);
        Assert.IsNotNull(analysis.LatestOutcome);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.MediaPropertiesV1.Id, out _));
    }

    [TestMethod]
    public void CapabilityOutcome_ShouldRejectTransientFailuresFromMetadata()
    {
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();

        Assert.ThrowsExactly<ArgumentException>(() => new CapabilityOutcome(
            AnalysisTestData.CaptureId,
            AnalysisTestData.CreateSource(),
            AnalysisCapabilities.OcrDocumentV1,
            analyzer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.TerminalFailure,
            new AnalysisFailure(AnalysisFailureCode.Timeout, AnalysisFailureDisposition.Transient),
            AnalysisTestData.GeneratedAtUtc));
    }

    [TestMethod]
    public void SuccessfulRetry_ShouldClearOnlyThatCapabilitiesLatestOutcome()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions();
        AnalysisCommitToken token = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, current);
        var outcome = new CapabilityOutcome(
            AnalysisTestData.CaptureId,
            AnalysisTestData.CreateSource(),
            AnalysisCapabilities.OcrDocumentV1,
            analyzer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.Unsupported,
            new AnalysisFailure(AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal),
            AnalysisTestData.GeneratedAtUtc);
        record.TryRecordOutcome(token, current, analyzer.Revision, outcome);

        CanonicalCapabilityResult success = AnalysisTestData.CreateResult(
            CreateOcrPayload("recovered"),
            analyzer,
            generatedAtUtc: AnalysisTestData.GeneratedAtUtc.AddSeconds(1));
        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryCommitResult(token, current, analyzer.Revision, success));

        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out CapabilityAnalysis? analysis));
        Assert.IsNotNull(analysis!.CanonicalResult);
        Assert.IsNull(analysis.LatestOutcome);
    }

    [TestMethod]
    public void RegisterSourceRevision_ShouldRebaseStampOnlyChangeWithoutInvalidatingResults()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, CreateOcrPayload("hello"), analyzer);
        SourceRevision restamped = AnalysisTestData.CreateSource(timestampOffsetMinutes: 1);

        SourceRevisionUpdateResult result = record.RegisterSourceRevision(restamped);

        Assert.AreEqual(SourceRevisionUpdateResult.StampChanged, result);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out CapabilityAnalysis? analysis));
        Assert.AreEqual(restamped, analysis!.CanonicalResult!.SourceRevision);
    }

    [TestMethod]
    public void RegisterSourceRevision_ShouldInvalidateEveryCapabilityWhenBytesChange()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, new MediaPropertiesV1(CaptureMediaKind.Image), analyzer);
        Commit(record, CreateOcrPayload("hello"), analyzer);

        SourceRevisionUpdateResult result = record.RegisterSourceRevision(AnalysisTestData.CreateSource('b'));

        Assert.AreEqual(SourceRevisionUpdateResult.SourceBytesChanged, result);
        Assert.IsEmpty(record.Analyses);
        Assert.IsFalse(record.IsUsable);
    }

    [TestMethod]
    public void SourceGenerationChange_ShouldRejectInflightCommitWithoutInvalidatingCurrentMetadata()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, CreateOcrPayload("current"), analyzer);
        AnalysisCommitPreconditions oldTruth = AnalysisTestData.CreatePreconditions(captureSourceGeneration: 1);
        AnalysisCommitToken oldToken = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, oldTruth);
        AnalysisCommitPreconditions currentTruth = AnalysisTestData.CreatePreconditions(captureSourceGeneration: 2);
        CanonicalCapabilityResult lateResult = AnalysisTestData.CreateResult(
            CreateOcrPayload("late"),
            analyzer,
            generatedAtUtc: AnalysisTestData.GeneratedAtUtc.AddMinutes(1));

        Assert.AreEqual(
            CapabilityCommitResult.Stale,
            record.TryCommitResult(oldToken, currentTruth, analyzer.Revision, lateResult));
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out CapabilityAnalysis? analysis));
        Assert.AreEqual("current", ((OcrDocumentV1)analysis!.CanonicalResult!.Payload).FullText);
    }

    [TestMethod]
    public void ApplyRecipe_ShouldPreserveCompatibleResultsButInvalidateOldToken()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, CreateOcrPayload("current"), analyzer);
        AnalysisCommitPreconditions oldTruth = AnalysisTestData.CreatePreconditions(recipeVersion: 1);
        AnalysisCommitToken oldToken = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, oldTruth);

        IReadOnlyList<AnalysisCapabilityId> invalidated = record.ApplyRecipe(AnalysisTestData.CreateRecipe(2));
        AnalysisCommitPreconditions currentTruth = AnalysisTestData.CreatePreconditions(recipeVersion: 2);
        CanonicalCapabilityResult late = AnalysisTestData.CreateResult(CreateOcrPayload("late"), analyzer);

        Assert.IsEmpty(invalidated);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out _));
        Assert.AreEqual(
            CapabilityCommitResult.Stale,
            record.TryCommitResult(oldToken, currentTruth, analyzer.Revision, late));
    }

    [TestMethod]
    public void ApplyRecipe_ShouldInvalidateOnlyRemovedOrSchemaChangedCapabilities()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, new MediaPropertiesV1(CaptureMediaKind.Image), analyzer);
        Commit(record, CreateOcrPayload("current"), analyzer);
        var ocrV2 = new CapabilityDefinition(
            AnalysisCapabilities.OcrDocumentV1.Id,
            new CapabilitySchemaVersion(2),
            CapabilityResultClassification.MachineExtracted);
        CaptureAnalysisRecipe recipeV2 = AnalysisTestData.CreateRecipe(
            2,
            new RecipeCapability(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required),
            new RecipeCapability(ocrV2, RecipeCapabilityRequirement.Required));

        IReadOnlyList<AnalysisCapabilityId> invalidated = record.ApplyRecipe(recipeV2);

        Assert.HasCount(1, invalidated);
        Assert.AreEqual(AnalysisCapabilities.OcrDocumentV1.Id, invalidated[0]);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.MediaPropertiesV1.Id, out _));
        Assert.IsFalse(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out _));
    }

    [TestMethod]
    public void ConfigurationChange_ShouldInvalidateOnlyAffectedCapability()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity oldAnalyzer = AnalysisTestData.CreateAnalyzer(configurationCharacter: 'c');
        AnalyzerIdentity newAnalyzer = AnalysisTestData.CreateAnalyzer(configurationCharacter: 'd');
        Commit(record, new MediaPropertiesV1(CaptureMediaKind.Image), oldAnalyzer);
        Commit(record, CreateOcrPayload("current"), oldAnalyzer);
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions();
        AnalysisCommitToken oldOcrToken = AnalysisTestData.CreateToken(
            AnalysisCapabilities.OcrDocumentV1,
            oldAnalyzer,
            current);

        bool invalidated = record.InvalidateCapability(
            AnalysisCapabilities.OcrDocumentV1,
            newAnalyzer.Revision);

        Assert.IsTrue(invalidated);
        Assert.IsFalse(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out _));
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.MediaPropertiesV1.Id, out _));

        Assert.AreEqual(
            CapabilityCommitResult.Stale,
            record.TryCommitResult(
                oldOcrToken,
                current,
                newAnalyzer.Revision,
                AnalysisTestData.CreateResult(CreateOcrPayload("late"), oldAnalyzer)));

        AnalysisCommitToken mediaToken = AnalysisTestData.CreateToken(
            AnalysisCapabilities.MediaPropertiesV1,
            oldAnalyzer,
            current);
        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryCommitResult(
                mediaToken,
                current,
                oldAnalyzer.Revision,
                AnalysisTestData.CreateResult(
                    new MediaPropertiesV1(CaptureMediaKind.Image, mimeType: "image/png"),
                    oldAnalyzer)));
    }

    [TestMethod]
    public void ExactStructuralReplay_ShouldBeIdempotentWhileChangedPayloadCommits()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions();
        AnalysisCommitToken token = AnalysisTestData.CreateToken(AnalysisCapabilities.MediaPropertiesV1, analyzer, current);
        CanonicalCapabilityResult first = AnalysisTestData.CreateResult(
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 50), mimeType: "image/png"),
            analyzer);
        CanonicalCapabilityResult equivalent = AnalysisTestData.CreateResult(
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 50), mimeType: "image/png"),
            analyzer);
        CanonicalCapabilityResult changed = AnalysisTestData.CreateResult(
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 50), mimeType: "image/jpeg"),
            analyzer);

        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryCommitResult(token, current, analyzer.Revision, first));
        Assert.AreEqual(
            CapabilityCommitResult.AlreadyCurrent,
            record.TryCommitResult(token, current, analyzer.Revision, equivalent));
        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryCommitResult(token, current, analyzer.Revision, changed));
    }

    [TestMethod]
    public void OlderCompletions_ShouldNotReplaceNewerCapabilityState()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions();
        AnalysisCommitToken token = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, current);
        CanonicalCapabilityResult original = AnalysisTestData.CreateResult(
            CreateOcrPayload("current"),
            analyzer,
            generatedAtUtc: AnalysisTestData.GeneratedAtUtc);
        var newerOutcome = new CapabilityOutcome(
            AnalysisTestData.CaptureId,
            AnalysisTestData.CreateSource(),
            AnalysisCapabilities.OcrDocumentV1,
            analyzer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.TerminalFailure,
            new AnalysisFailure(AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal),
            AnalysisTestData.GeneratedAtUtc.AddSeconds(2));

        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryCommitResult(token, current, analyzer.Revision, original));
        Assert.AreEqual(
            CapabilityCommitResult.Committed,
            record.TryRecordOutcome(token, current, analyzer.Revision, newerOutcome));
        Assert.AreEqual(
            CapabilityCommitResult.AlreadyCurrent,
            record.TryCommitResult(token, current, analyzer.Revision, original));

        CanonicalCapabilityResult olderChangedResult = AnalysisTestData.CreateResult(
            CreateOcrPayload("older"),
            analyzer,
            generatedAtUtc: AnalysisTestData.GeneratedAtUtc.AddSeconds(1));
        Assert.AreEqual(
            CapabilityCommitResult.Stale,
            record.TryCommitResult(token, current, analyzer.Revision, olderChangedResult));

        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.OcrDocumentV1.Id, out CapabilityAnalysis? analysis));
        Assert.AreEqual("current", ((OcrDocumentV1)analysis!.CanonicalResult!.Payload).FullText);
        Assert.AreSame(newerOutcome, analysis.LatestOutcome);
    }

    [TestMethod]
    public void CapabilityAnalysis_ShouldRejectAnOutcomeOlderThanItsCanonicalResult()
    {
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        CanonicalCapabilityResult result = AnalysisTestData.CreateResult(
            CreateOcrPayload("current"),
            analyzer,
            generatedAtUtc: AnalysisTestData.GeneratedAtUtc.AddSeconds(1));
        var olderOutcome = new CapabilityOutcome(
            AnalysisTestData.CaptureId,
            AnalysisTestData.CreateSource(),
            AnalysisCapabilities.OcrDocumentV1,
            analyzer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.TerminalFailure,
            new AnalysisFailure(AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal),
            AnalysisTestData.GeneratedAtUtc);

        Assert.ThrowsExactly<ArgumentException>(() => new CapabilityAnalysis(
            AnalysisCapabilities.OcrDocumentV1,
            result,
            olderOutcome));
    }

    [TestMethod]
    public void StaleControlGeneration_ShouldNotMutateAnotherCapability()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord();
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        Commit(record, new MediaPropertiesV1(CaptureMediaKind.Image), analyzer);
        AnalysisCommitPreconditions expected = AnalysisTestData.CreatePreconditions(controlGeneration: 1);
        AnalysisCommitToken token = AnalysisTestData.CreateToken(AnalysisCapabilities.OcrDocumentV1, analyzer, expected);
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions(controlGeneration: 2);

        CapabilityCommitResult result = record.TryCommitResult(
            token,
            current,
            analyzer.Revision,
            AnalysisTestData.CreateResult(CreateOcrPayload("stale"), analyzer));

        Assert.AreEqual(CapabilityCommitResult.Stale, result);
        Assert.HasCount(1, record.Analyses);
        Assert.IsTrue(record.TryGetAnalysis(AnalysisCapabilities.MediaPropertiesV1.Id, out _));
    }

    [TestMethod]
    public void ApplyRecipe_ShouldRejectDowngradeAndUnversionedSemanticChange()
    {
        CaptureAnalysisRecord record = AnalysisTestData.CreateRecord(recipe: AnalysisTestData.CreateRecipe(2));
        CaptureAnalysisRecipe changedSameVersion = AnalysisTestData.CreateRecipe(
            2,
            new RecipeCapability(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required));

        Assert.ThrowsExactly<InvalidOperationException>(() => record.ApplyRecipe(AnalysisTestData.CreateRecipe(1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => record.ApplyRecipe(changedSameVersion));
    }

    private static CapabilityCommitResult Commit(
        CaptureAnalysisRecord record,
        CapabilityPayload payload,
        AnalyzerIdentity analyzer)
    {
        AnalysisCommitPreconditions current = AnalysisTestData.CreatePreconditions(
            sourceRevision: record.SourceRevision,
            recipeVersion: record.Recipe.Version.Value);
        AnalysisCommitToken token = AnalysisTestData.CreateToken(payload.Definition, analyzer, current);
        return record.TryCommitResult(
            token,
            current,
            analyzer.Revision,
            AnalysisTestData.CreateResult(payload, analyzer, record.SourceRevision));
    }

    private static OcrDocumentV1 CreateOcrPayload(string text)
    {
        return new(new PixelSize(100, 50), text, [], []);
    }
}
