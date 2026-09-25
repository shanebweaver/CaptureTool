using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Application.Library.CaptureDetails;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using Moq;
using System.Security.Cryptography;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class CaptureDetailsReaderTests
{
    [TestMethod]
    [DataRow(AnalysisMediaKind.Image)]
    [DataRow(AnalysisMediaKind.Audio)]
    [DataRow(AnalysisMediaKind.Video)]
    public async Task LocalPropertiesNeedNeitherSavedMetadataNorReadableCatalog(AnalysisMediaKind kind)
    {
        var setup = new Setup();
        var properties = new Mock<IMediaFileDetailsReader>();
        var date = DateTimeOffset.UtcNow;
        var file = new FileDetailsMetadata(kind, "capture.file", 10, null, date, date);
        properties.Setup(reader => reader.ReadAsync(Setup.Path, kind, null, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        setup.Catalog.Setup(catalog => catalog.ReadAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new CryptographicException());
        var reader = new CaptureDetailsReader(setup.Catalog.Object, setup.Metadata.Object, setup.Store.Object, setup.Files.Object, properties.Object);
        Assert.AreSame(file, await reader.ReadFileAsync(Setup.Path, kind));
        setup.Metadata.VerifyNoOtherCalls();
        setup.Files.VerifyNoOtherCalls();
        setup.Store.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(CaptureFileType.Image, AnalysisMediaKind.Image)]
    [DataRow(CaptureFileType.Audio, AnalysisMediaKind.Audio)]
    [DataRow(CaptureFileType.Video, AnalysisMediaKind.Video)]
    public async Task VerifiedSourceReturnsStoredMetadataForEveryMediaKind(CaptureFileType type, AnalysisMediaKind kind)
    {
        var setup = new Setup(type, kind);
        var snapshot = await setup.Reader.ReadAsync(Setup.Path);
        Assert.AreEqual(CaptureDetailsStatus.Available, snapshot.Status);
        Assert.AreSame(setup.Record, snapshot.Record);
        setup.Lease.Verify(lease => lease.VerifyAsync(It.IsAny<CancellationToken>()), Times.Once);
        setup.Lease.Verify(lease => lease.DisposeAsync(), Times.Once);
        setup.Store.Verify(store => store.AdmitAsync(It.IsAny<AnalysisRequest>(), It.IsAny<Guid>(), It.IsAny<MediaAnalysisPlan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ChangedBytesOrFailedVerificationHideOldResults()
    {
        var setup = new Setup();
        setup.Lease.SetupGet(lease => lease.Revision).Returns(new SourceRevision(new string('b', 64)));
        Assert.AreEqual(CaptureDetailsStatus.SourceChanged, (await setup.Reader.ReadAsync(Setup.Path)).Status);
        setup.Lease.SetupGet(lease => lease.Revision).Returns(setup.Record.SourceRevision);
        setup.Lease.Setup(lease => lease.VerifyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var snapshot = await setup.Reader.ReadAsync(Setup.Path);
        Assert.IsNull(snapshot.Record);
        Assert.AreEqual(CaptureDetailsStatus.SourceChanged, snapshot.Status);
    }

    [TestMethod]
    public async Task WorkingCopyLocationsRequireTheSameVerifiedBytes()
    {
        var setup = new Setup();
        Assert.IsTrue(await setup.Reader.VerifySourceAsync(Setup.Path, setup.Record.SourceRevision));
        setup.Lease.SetupGet(lease => lease.Revision).Returns(new SourceRevision(new string('b', 64)));
        Assert.IsFalse(await setup.Reader.VerifySourceAsync(Setup.Path, setup.Record.SourceRevision));
    }

    [TestMethod]
    public async Task GenerationChangeDuringReadDropsRecordAndRun()
    {
        var setup = new Setup();
        setup.Store.SetupSequence(store => store.GetAdmissionScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisAdmissionScope(Guid.NewGuid(), 0)).ReturnsAsync(new AnalysisAdmissionScope(Guid.NewGuid(), 0));
        var snapshot = await setup.Reader.ReadAsync(Setup.Path);
        Assert.AreEqual(CaptureDetailsStatus.Empty, snapshot.Status);
        Assert.IsNull(snapshot.Record);
        Assert.IsNull(snapshot.Run);
    }

    [TestMethod]
    public async Task MissingFileDoesNotPresentUnverifiedSavedResults()
    {
        var setup = new Setup();
        setup.Files.Setup(files => files.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new FileNotFoundException());
        var snapshot = await setup.Reader.ReadAsync(Setup.Path);
        Assert.AreEqual(CaptureDetailsStatus.SourceUnavailable, snapshot.Status);
        Assert.IsNull(snapshot.Record);
    }

    [TestMethod]
    public async Task AmbiguousIdentityDoesNotReadAnotherCapturesMetadata()
    {
        var setup = new Setup();
        setup.Catalog.Setup(catalog => catalog.ReadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new(CaptureId.New(), CaptureFileType.Image, null, Setup.Path, CaptureSourceOwnership.External),
            new(CaptureId.New(), CaptureFileType.Image, null, Setup.Path, CaptureSourceOwnership.External)]);
        Assert.AreEqual(CaptureDetailsStatus.Empty, (await setup.Reader.ReadAsync(Setup.Path)).Status);
        setup.Metadata.Verify(reader => reader.GetAsync(It.IsAny<CaptureId>(), It.IsAny<SourceRevision?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProtectedStorageFailureIsDifferentFromAnEmptyCapture()
    {
        var setup = new Setup();
        setup.Metadata.Setup(reader => reader.GetAsync(It.IsAny<CaptureId>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CryptographicException());
        Assert.AreEqual(CaptureDetailsStatus.Unavailable, (await setup.Reader.ReadAsync(Setup.Path)).Status);
    }

    [TestMethod]
    public async Task PreferredPathCanReadMatchingBytesButNeverAnEditedCopy()
    {
        var setup = new Setup();
        string saved = System.IO.Path.GetFullPath("saved.png");
        setup.Catalog.Setup(catalog => catalog.ReadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new(setup.Record.CaptureId, CaptureFileType.Image, null, Setup.Path, CaptureSourceOwnership.External, saved)]);
        Assert.AreEqual(CaptureDetailsStatus.Available, (await setup.Reader.ReadAsync(saved)).Status);
        setup.Lease.SetupGet(lease => lease.Revision).Returns(new SourceRevision(new string('c', 64)));
        Assert.AreEqual(CaptureDetailsStatus.SourceChanged, (await setup.Reader.ReadAsync(saved)).Status);
    }

    private sealed class Setup
    {
        public static string Path => System.IO.Path.GetFullPath("capture.png");
        public Mock<ICaptureAssetCatalog> Catalog { get; } = new();
        public Mock<ICaptureMetadataReader> Metadata { get; } = new();
        public Mock<IAnalysisExecutionStore> Store { get; } = new();
        public Mock<IAnalysisSource> Files { get; } = new();
        public Mock<IAnalysisSourceLease> Lease { get; } = new();
        public CaptureAnalysisRecord Record { get; }
        public CaptureDetailsReader Reader { get; }
        public Setup(CaptureFileType type = CaptureFileType.Image, AnalysisMediaKind kind = AnalysisMediaKind.Image)
        {
            CaptureId id = CaptureId.New();
            Guid run = Guid.NewGuid();
            var revision = new SourceRevision(new string('a', 64));
            var date = DateTimeOffset.UtcNow;
            Record = new(id, kind, revision, "plan", run, [new(new FileDetailsMetadata(kind, "capture.png", 10, null, date, date),
                new("test", "test", "test", "1"), date, "plan", run)]);
            Catalog.Setup(catalog => catalog.ReadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
                new(id, type, null, Path, CaptureSourceOwnership.External)]);
            var scope = new AnalysisAdmissionScope(Guid.NewGuid(), 0);
            Store.Setup(store => store.GetAdmissionScopeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(scope);
            Metadata.Setup(reader => reader.GetAsync(id, null, It.IsAny<CancellationToken>())).ReturnsAsync(Record);
            Lease.SetupGet(lease => lease.Revision).Returns(revision);
            Lease.Setup(lease => lease.VerifyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Files.Setup(files => files.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Lease.Object);
            Reader = new(Catalog.Object, Metadata.Object, Store.Object, Files.Object);
        }
    }
}
