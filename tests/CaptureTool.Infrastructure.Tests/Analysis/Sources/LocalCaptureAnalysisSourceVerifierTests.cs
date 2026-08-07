using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Sources;
using CaptureTool.Infrastructure.Tests.Analysis.Persistence;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Tests.Analysis.Sources;

[TestClass]
public sealed class LocalCaptureAnalysisSourceVerifierTests
{
    [TestMethod]
    public async Task AuthorizedVerification_ShouldHashRetainedSourceAndIgnorePreferredLocationGeneration()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        string sourcePath = Path.Combine(root, "retained-source.png");
        byte[] sourceBytes = [0x10, 0x20, 0x30, 0x40, 0x50];
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);
        CaptureId captureId = CaptureId.New();
        DateTimeOffset capturedAtUtc = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
        var asset = new CaptureAsset(
            captureId,
            CaptureFileType.Image,
            sourcePath,
            CaptureSourceOwnership.AppOwned,
            capturedAtUtc,
            preferredOpenPath: Path.Combine(root, "exported.png"));
        var catalog = new StubCatalog(
            asset,
            [
                new CaptureAssetChange(1, captureId, 1, CaptureAssetChangeType.Finalized, capturedAtUtc),
                new CaptureAssetChange(2, captureId, 2, CaptureAssetChangeType.PreferredLocationChanged, capturedAtUtc.AddSeconds(1)),
            ]);
        var verifier = new LocalCaptureAnalysisSourceVerifier(catalog);
        CaptureAnalysisAuthorizationScope scope = CaptureAnalysisPolicyDefaults.CreateAuthorizationScope();
        var authorizationRequest = new CaptureAnalysisAuthorizationRequest(
            captureId,
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
            AnalysisCapabilities.MediaPropertiesV1,
            ProcessingBoundary.OnDevice,
            analyzer: null,
            CaptureAnalysisAuthorizationStage.SourceVerification);
        CaptureAnalysisAuthorizationDecision authorization =
            CaptureAnalysisAuthorizationDecision.Authorized(
                authorizationRequest,
                policyRevision: 1,
                controlGeneration: 1,
                enrollmentGeneration: 1,
                tombstoneGeneration: 0,
                scope);

        IVerifiedCaptureAnalysisSource? source = await verifier.TryOpenVerifiedAsync(
            new CaptureAnalysisSourceVerificationRequest(authorization));

        Assert.IsNotNull(source);
        await using (source)
        {
            Assert.AreEqual(captureId, source.CaptureId);
            Assert.AreEqual(CaptureMediaKind.Image, source.MediaKind);
            Assert.AreEqual(1, source.CaptureSourceGeneration);
            Assert.AreEqual(sourceBytes.LongLength, source.SourceRevision.Length);
            Assert.AreEqual(
                Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
                source.SourceRevision.Fingerprint.Value);
            await using Stream reopened = await source.OpenReadAsync();
            using var copy = new MemoryStream();
            await reopened.CopyToAsync(copy);
            CollectionAssert.AreEqual(sourceBytes, copy.ToArray());
        }
    }

    private sealed class StubCatalog(
        CaptureAsset asset,
        IReadOnlyList<CaptureAssetChange> changes) : ICaptureAssetCatalog
    {
        public IReadOnlyList<CaptureAsset> GetAssets() => [asset];
        public CaptureAsset? Get(CaptureId captureId) => captureId == asset.Id ? asset : null;
        public CaptureAsset? FindByPath(string filePath) => asset;
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) =>
            changes.Where(change => change.Sequence > sequence).ToArray();
        public long GetLatestChangeSequence() => changes[^1].Sequence;
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset added) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryUpdate(CaptureAsset updated, long expectedLifecycleRevision, CaptureAssetChangeType changeType) => throw new NotSupportedException();
    }
}
