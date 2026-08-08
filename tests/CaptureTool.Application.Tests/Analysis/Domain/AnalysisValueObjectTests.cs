using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class AnalysisValueObjectTests
{
    [TestMethod]
    public void ProvisionalSourceStamp_ShouldRepresentKnownAndUnknownFactsWithoutContentIdentity()
    {
        ProvisionalSourceStamp unknown = ProvisionalSourceStamp.Unknown;
        var known = new ProvisionalSourceStamp(0, AnalysisTestData.CapturedAtUtc);

        Assert.IsFalse(unknown.IsKnown);
        Assert.IsTrue(known.IsKnown);
        Assert.AreEqual(0L, known.Length);
        Assert.AreEqual(AnalysisTestData.CapturedAtUtc, known.LastWriteTimeUtc);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProvisionalSourceStamp(-1, AnalysisTestData.CapturedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() => new ProvisionalSourceStamp(
            1,
            AnalysisTestData.CapturedAtUtc.ToOffset(TimeSpan.FromHours(-7))));
    }

    [TestMethod]
    public void SourceRevision_ShouldUseLengthAndFingerprintAsByteIdentity()
    {
        SourceRevision original = AnalysisTestData.CreateSource(timestampOffsetMinutes: 0);
        SourceRevision sameBytesNewStamp = AnalysisTestData.CreateSource(timestampOffsetMinutes: 1);
        SourceRevision changedLength = AnalysisTestData.CreateSource(length: 101);
        SourceRevision changedFingerprint = AnalysisTestData.CreateSource('b');

        Assert.IsTrue(original.HasSameBytesAs(sameBytesNewStamp));
        Assert.IsFalse(original.HasSameBytesAs(changedLength));
        Assert.IsFalse(original.HasSameBytesAs(changedFingerprint));
        Assert.IsTrue(original.Matches(original.ProvisionalStamp));
        Assert.IsFalse(original.Matches(ProvisionalSourceStamp.Unknown));
    }

    [TestMethod]
    public void ContentFingerprint_ShouldAcceptOnlyCanonicalSha256Values()
    {
        ContentFingerprint fingerprint = ContentFingerprint.Sha256(new string('A', 64));

        Assert.AreEqual(ContentFingerprint.Sha256Algorithm, fingerprint.Algorithm);
        Assert.AreEqual(new string('a', 64), fingerprint.Value);
        Assert.ThrowsExactly<ArgumentException>(() => new ContentFingerprint("md5", new string('a', 64)));
        Assert.ThrowsExactly<ArgumentException>(() => ContentFingerprint.Sha256("abc"));
        Assert.ThrowsExactly<ArgumentException>(() => ContentFingerprint.Sha256(new string('z', 64)));
    }

    [TestMethod]
    public void AnalysisFailure_ShouldRejectUnknownOrUnboundedValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AnalysisFailure(
            AnalysisFailureCode.Unknown,
            AnalysisFailureDisposition.Terminal));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AnalysisFailure(
            AnalysisFailureCode.Timeout,
            AnalysisFailureDisposition.Unknown));
        Assert.IsTrue(default(AnalysisFailure).IsEmpty);
    }

    [TestMethod]
    public void MachineIdentifiers_ShouldBeNormalizedAndBounded()
    {
        var capabilityId = new AnalysisCapabilityId(" OCR-Document ");
        var purpose = new AnalysisPurpose(" Capture-Memory.Search ", 1);

        Assert.AreEqual("ocr-document", capabilityId.Value);
        Assert.AreEqual("capture-memory.search", purpose.Id);
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisCapabilityId("ocr document"));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisRecipeId(new string('a', 129)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CapabilitySchemaVersion(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AnalysisRecipeVersion(0));
    }

    [TestMethod]
    public void AnalyzerIdentity_ShouldComputeDeterministicConfigurationSensitiveRevision()
    {
        AnalyzerIdentity first = AnalysisTestData.CreateAnalyzer(configurationCharacter: 'c');
        AnalyzerIdentity same = AnalysisTestData.CreateAnalyzer(configurationCharacter: 'c');
        AnalyzerIdentity changedConfiguration = AnalysisTestData.CreateAnalyzer(configurationCharacter: 'd');

        Assert.AreEqual(first.Revision, same.Revision);
        Assert.AreNotEqual(first.Revision, changedConfiguration.Revision);
        Assert.AreEqual(AnalyzerIdentity.Unknown, first.ModelVersion);
        Assert.ThrowsExactly<ArgumentException>(() => new AnalyzerIdentity(
            "windows.ocr",
            "microsoft.windows",
            "model",
            "1",
            "1",
            "runtime",
            "1",
            "1",
            "raw prompt text"));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalyzerIdentity(
            "windows.ocr\nspoofed",
            "microsoft.windows",
            null,
            null,
            "1",
            null,
            null,
            null,
            null));
    }

    [TestMethod]
    public void AnalysisProcessingPolicy_ShouldRequireBoundaryProviderAndPurpose()
    {
        AnalyzerIdentity localAnalyzer = AnalysisTestData.CreateAnalyzer();
        AnalyzerIdentity remoteAnalyzer = AnalysisTestData.CreateAnalyzer(
            analyzerId: "azure.vision",
            providerId: "microsoft.azure");
        AnalysisPurpose purpose = AnalysisTestData.Purpose;
        AnalysisProcessingPolicy localOnly = AnalysisProcessingPolicy.LocalOnly(purpose);
        var remoteAllowed = new AnalysisProcessingPolicy(
            purpose,
            [ProcessingBoundary.OnDevice, ProcessingBoundary.Remote],
            ["microsoft.azure"]);

        Assert.IsTrue(localOnly.IsEligible(localAnalyzer, ProcessingBoundary.OnDevice, purpose));
        Assert.IsFalse(localOnly.IsEligible(remoteAnalyzer, ProcessingBoundary.Remote, purpose));
        Assert.IsTrue(remoteAllowed.IsEligible(remoteAnalyzer, ProcessingBoundary.Remote, purpose));
        Assert.IsFalse(remoteAllowed.IsEligible(localAnalyzer, ProcessingBoundary.Remote, purpose));
        Assert.IsFalse(remoteAllowed.IsEligible(
            remoteAnalyzer,
            ProcessingBoundary.Remote,
            new AnalysisPurpose("another-purpose", 1)));
        Assert.IsFalse(remoteAllowed.IsEligible(remoteAnalyzer, ProcessingBoundary.Unknown, purpose));
    }

    [TestMethod]
    public void CommitToken_ShouldMatchOnlyIdenticalExternalTruth()
    {
        AnalyzerIdentity analyzer = AnalysisTestData.CreateAnalyzer();
        AnalysisCommitPreconditions expected = AnalysisTestData.CreatePreconditions();
        var token = new AnalysisCommitToken(expected, AnalysisCapabilities.OcrDocumentV1, analyzer.Revision);
        SourceRevision restampedSource = AnalysisTestData.CreateSource(timestampOffsetMinutes: 1);

        AnalysisCommitPreconditions[] staleTruth =
        [
            AnalysisTestData.CreatePreconditions(captureId: CaptureId.New()),
            AnalysisTestData.CreatePreconditions(captureSourceGeneration: 2),
            AnalysisTestData.CreatePreconditions(sourceRevision: restampedSource),
            AnalysisTestData.CreatePreconditions(purpose: new AnalysisPurpose("another-purpose", 1)),
            AnalysisTestData.CreatePreconditions(purpose: new AnalysisPurpose(AnalysisTestData.Purpose.Id, 2)),
            AnalysisTestData.CreatePreconditions(policyRevision: 2),
            AnalysisTestData.CreatePreconditions(controlGeneration: 2),
            AnalysisTestData.CreatePreconditions(enrollmentGeneration: 2),
            AnalysisTestData.CreatePreconditions(tombstoneGeneration: 1),
            AnalysisTestData.CreatePreconditions(recipeId: new AnalysisRecipeId("another-recipe")),
            AnalysisTestData.CreatePreconditions(recipeVersion: 2),
            AnalysisTestData.CreatePreconditions(resolutionPolicyRevision: 2),
        ];

        Assert.IsTrue(token.Matches(
            expected,
            AnalysisCapabilities.OcrDocumentV1,
            analyzer.Revision));
        foreach (AnalysisCommitPreconditions current in staleTruth)
        {
            Assert.IsFalse(token.Matches(
                current,
                AnalysisCapabilities.OcrDocumentV1,
                analyzer.Revision));
        }

        Assert.IsFalse(token.Matches(
            expected,
            AnalysisCapabilities.MediaPropertiesV1,
            analyzer.Revision));
        Assert.IsFalse(token.Matches(
            expected,
            AnalysisCapabilities.OcrDocumentV1,
            AnalysisTestData.CreateAnalyzer(configurationCharacter: 'd').Revision));
    }

    [TestMethod]
    public void DefaultSecuritySensitiveEnums_ShouldFailClosed()
    {
        Assert.AreEqual(ProcessingBoundary.Unknown, CreateDefault<ProcessingBoundary>());
        Assert.AreEqual(CaptureMediaKind.Unknown, CreateDefault<CaptureMediaKind>());
        Assert.AreEqual(CapabilityCommitResult.Unknown, CreateDefault<CapabilityCommitResult>());
        Assert.AreEqual(RecipeCapabilityRequirement.Unknown, CreateDefault<RecipeCapabilityRequirement>());
        Assert.AreEqual(CapabilityResultClassification.Unknown, CreateDefault<CapabilityResultClassification>());
    }

    private static T CreateDefault<T>()
        where T : struct
    {
        return default;
    }

}
