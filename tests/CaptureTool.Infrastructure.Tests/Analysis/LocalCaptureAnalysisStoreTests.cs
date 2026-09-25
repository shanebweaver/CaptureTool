using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class LocalCaptureAnalysisStoreTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Cancellation => TestContext.CancellationToken;

    [TestMethod]
    [DataRow(AnalysisMediaKind.Image, false)]
    [DataRow(AnalysisMediaKind.Image, true)]
    [DataRow(AnalysisMediaKind.Audio, false)]
    [DataRow(AnalysisMediaKind.Audio, true)]
    [DataRow(AnalysisMediaKind.Video, false)]
    [DataRow(AnalysisMediaKind.Video, true)]
    public async Task FileDetailsRoundTripProtectedWithOptionalPropertiesAndDeleteNormally(AnalysisMediaKind kind, bool complete)
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        var id = CaptureId.New();
        var token = await store.BeginRunAsync(id, kind, AnalysisTestEnvironment.Revision(), "details-v1", Cancellation);
        var created = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var details = new FileDetailsMetadata(kind, "private capture.dat", 987654, "application/octet-stream", created, created.AddDays(1),
            complete ? created.AddDays(-1) : null, complete && kind != AnalysisMediaKind.Image ? TimeSpan.FromSeconds(12.5) : null,
            complete && kind == AnalysisMediaKind.Image ? new(new(3840, 2160), 144, 144) : null,
            complete && kind == AnalysisMediaKind.Video ? new(new(1920, 1080), 30000d / 1001, 1500000, "H264") : null,
            complete && kind != AnalysisMediaKind.Image ? new(2, 48000, 128000, "AAC") : null);
        var result = new AnalysisResult(details, AnalysisTestEnvironment.Producer(), created, "details-v1");
        Assert.IsTrue(await store.TryWriteAsync(token, result, Cancellation));
        using var reopened = environment.CreateStore();
        var record = (await reopened.GetAsync(id, cancellationToken: Cancellation))!;
        var loaded = (FileDetailsMetadata)record.Results.Single().Payload;
        Assert.AreEqual(details.FileName, loaded.FileName);
        Assert.AreEqual(details.SizeBytes, loaded.SizeBytes);
        Assert.AreEqual(details.ContentType, loaded.ContentType);
        Assert.AreEqual(details.FileCreatedAt, loaded.FileCreatedAt);
        Assert.AreEqual(details.FileModifiedAt, loaded.FileModifiedAt);
        Assert.AreEqual(details.CapturedAt, loaded.CapturedAt);
        Assert.AreEqual(details.Duration, loaded.Duration);
        Assert.AreEqual(details.Image, loaded.Image);
        Assert.AreEqual(details.Video, loaded.Video);
        Assert.AreEqual(details.Audio, loaded.Audio);
        foreach (byte[] bytes in environment.Files.PublishedBytes)
            Assert.DoesNotContain("private capture", Encoding.UTF8.GetString(bytes));
        await store.ClearAsync(Cancellation);
        Assert.IsNull(await reopened.GetAsync(id, cancellationToken: Cancellation));
        Assert.IsFalse(await store.TryWriteAsync(token, result, Cancellation));
    }

    [TestMethod]
    public async Task LegacyFileDetailsDateBecomesUnknownWithoutLosingOtherFactsOrProvenance()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        var id = CaptureId.New();
        var token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "details-v1", Cancellation);
        var at = DateTimeOffset.UtcNow;
        var details = new FileDetailsMetadata(AnalysisMediaKind.Image, "old.png", 1234, "image/png", at, at, at,
            image: new(new(100, 50), 96, 96));
        var result = new AnalysisResult(details, AnalysisTestEnvironment.Producer(), at, "details-v1");
        Assert.IsTrue(await store.TryWriteAsync(token, result, Cancellation));
        await MutateDocument(environment, environment.MetadataPaths.Single(), node =>
            node["Results"]![0]!["FileDetails"]!.AsObject().Remove("CaptureTimeVerified"));
        using var reopened = environment.CreateStore();
        var saved = (await reopened.GetAsync(id, cancellationToken: Cancellation))!.Results.Single();
        var loaded = (FileDetailsMetadata)saved.Payload;
        Assert.IsNull(loaded.CapturedAt);
        Assert.AreEqual(details.SizeBytes, loaded.SizeBytes);
        Assert.AreEqual(details.FileCreatedAt, loaded.FileCreatedAt);
        Assert.AreEqual(details.FileModifiedAt, loaded.FileModifiedAt);
        Assert.AreEqual(details.Image, loaded.Image);
        Assert.AreEqual(result.Producer, saved.Producer);
        Assert.AreEqual(result.GeneratedAt, saved.GeneratedAt);
    }

    [TestMethod]
    public async Task QrMetadataRoundTripsProtectedWithBoundsTimesAndEmptySuccessAndDeletesNormally()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        var id = CaptureId.New();
        var token = await store.BeginRunAsync(id, AnalysisMediaKind.Video, AnalysisTestEnvironment.Revision(), "video-v3", Cancellation);
        var code = new DecodedQrCode("private QR payload", new(.1, .2, .3, .4), TimeSpan.FromSeconds(5));
        var result = new AnalysisResult(new QrCodeMetadata([code]), AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "video-v3");
        Assert.IsTrue(await store.TryWriteAsync(token, result, Cancellation));
        using var reopened = environment.CreateStore();
        var record = (await reopened.GetAsync(id, cancellationToken: Cancellation))!;
        Assert.AreEqual(code, ((QrCodeMetadata)record.Results.Single().Payload).Codes.Single());
        Assert.AreEqual(result.Producer, record.Results.Single().Producer);
        foreach (byte[] bytes in environment.Files.PublishedBytes)
            Assert.DoesNotContain("private QR payload", Encoding.UTF8.GetString(bytes));
        var empty = new AnalysisResult(new QrCodeMetadata([]), result.Producer, DateTimeOffset.UtcNow, "video-v3");
        Assert.IsTrue(await store.TryWriteAsync(token, empty, Cancellation));
        Assert.HasCount(0, ((QrCodeMetadata)(await reopened.GetAsync(id, cancellationToken: Cancellation))!.Results.Single().Payload).Codes);
        await store.ClearAsync(Cancellation);
        Assert.IsNull(await reopened.GetAsync(id, cancellationToken: Cancellation));
        Assert.IsFalse(await store.TryWriteAsync(token, result, Cancellation));
    }

    [TestMethod]
    public async Task TypedResultsAndActualProvenanceSurviveReloadWithoutPlaintextFiles()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Video, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        var timestamp = TimeSpan.FromSeconds(3);
        AnalysisResult text = new(new TextRecognitionMetadata([
            new("private OCR", new(0.1, 0.2, 0.3, 0.4), timestamp)]), AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1");
        AnalysisResult transcript = new(new TranscriptMetadata("fr", [new("private speech", timestamp, TimeSpan.FromSeconds(4))]),
            AnalysisTestEnvironment.Producer(), DateTimeOffset.UtcNow, "v1");
        Assert.IsTrue(await store.TryWriteAsync(token, text, Cancellation));
        Assert.IsTrue(await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation));
        Assert.IsTrue(await store.TryWriteAsync(token, transcript, Cancellation));

        using LocalCaptureAnalysisStore reopened = environment.CreateStore();
        CaptureAnalysisRecord record = (await reopened.GetAsync(id, cancellationToken: Cancellation))!;
        Assert.AreEqual(id, record.CaptureId);
        Assert.HasCount(3, record.Results);
        AnalysisResult loadedText = record.Results.Single(result => result.Payload is TextRecognitionMetadata);
        Assert.AreEqual(text.Producer, loadedText.Producer);
        Assert.AreEqual(text.GeneratedAt, loadedText.GeneratedAt);
        Assert.AreEqual("v1", loadedText.PlanVersion);
        RecognizedText region = ((TextRecognitionMetadata)loadedText.Payload).Regions.Single();
        Assert.AreEqual("private OCR", region.Text);
        Assert.AreEqual(timestamp, region.Timestamp);
        Assert.AreEqual(new NormalizedBounds(0.1, 0.2, 0.3, 0.4), region.Bounds);
        TranscriptMetadata speech = (TranscriptMetadata)record.Results.Single(result => result.Payload is TranscriptMetadata).Payload;
        Assert.AreEqual("fr", speech.Language);
        Assert.AreEqual(TimeSpan.FromSeconds(4), speech.Segments.Single().End);
        Assert.AreEqual("private description", ((DescriptionMetadata)record.Results.Single(result => result.Payload is DescriptionMetadata).Payload).Descriptions.Single().Text);
        Assert.HasCount(1, await reopened.ReadAllAsync(Cancellation));
        foreach (byte[] bytes in environment.Files.PublishedBytes)
        {
            string stored = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("private", stored);
            Assert.DoesNotContain("actual-model", stored);
        }
        Assert.IsEmpty(Directory.GetFiles(environment.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task RefreshKeepsExistingSuccessButSupersedesOldRunWrites()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken old = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(old, AnalysisTestEnvironment.Description("old"), Cancellation);
        AnalysisWriteToken fresh = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v2", Cancellation);
        Assert.IsFalse(await store.TryWriteAsync(old, AnalysisTestEnvironment.Description("late"), Cancellation));
        CaptureAnalysisRecord refreshing = (await store.GetAsync(id, cancellationToken: Cancellation))!;
        Assert.AreEqual("v1", refreshing.Results.Single().PlanVersion);
        Assert.AreEqual("v2", refreshing.PlanVersion);
        Assert.IsTrue(await store.TryWriteAsync(fresh, AnalysisTestEnvironment.Description("new", "v2"), Cancellation));
        Assert.AreEqual("new", DescriptionText((await store.GetAsync(id, cancellationToken: Cancellation))!));
    }

    [TestMethod]
    public async Task ChangedSourceInvalidatesResultsAndRejectsOldTokens()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken old = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(old, AnalysisTestEnvironment.Description(), Cancellation);
        AnalysisWriteToken fresh = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision('b'), "v1", Cancellation);
        Assert.IsNull(await store.GetAsync(id, AnalysisTestEnvironment.Revision(), Cancellation));
        Assert.IsEmpty((await store.GetAsync(id, AnalysisTestEnvironment.Revision('b'), Cancellation))!.Results);
        Assert.IsFalse(await store.TryWriteAsync(old, AnalysisTestEnvironment.Description(), Cancellation));
        Assert.IsTrue(await store.TryWriteAsync(fresh, AnalysisTestEnvironment.Description("updated source"), Cancellation));
    }

    [TestMethod]
    public async Task ProtectionOrPublicationFailurePreservesLastCommittedRecord()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("committed"), Cancellation);
        byte[] original = await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Cancellation);
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<CryptographicException>(() => store.TryWriteAsync(token, AnalysisTestEnvironment.Description("uncommitted"), Cancellation));
        environment.Protector.FailProtection = false;
        environment.Files.BeforeWrite = (_, _) => throw new IOException("Injected publication failure.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.TryWriteAsync(token, AnalysisTestEnvironment.Description("uncommitted"), Cancellation));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(environment.MetadataPaths.Single(), Cancellation));
        using LocalCaptureAnalysisStore reopened = environment.CreateStore();
        Assert.AreEqual("committed", DescriptionText((await reopened.GetAsync(id, cancellationToken: Cancellation))!));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClearFencesLateWritesEvenWhenCleanupFailsAndRestartRetriesCleanup(bool corruptControl)
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        if (corruptControl) await DamageControl(environment, "corrupt");
        environment.Files.FailCleanup = true;
        AnalysisCleanupResult cleared = await store.ClearAsync(Cancellation);
        Assert.IsFalse(cleared.Completed);
        Assert.AreEqual(1, cleared.RemainingGenerations);
        Assert.HasCount(1, environment.MetadataPaths); // Physically retained but inaccessible.
        Assert.IsNull(await store.GetAsync(id, cancellationToken: Cancellation));
        Assert.IsFalse(await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("late"), Cancellation));

        using LocalCaptureAnalysisStore reopened = environment.CreateStore();
        Assert.IsNull(await reopened.GetAsync(id, cancellationToken: Cancellation));
        environment.Files.FailCleanup = false;
        Assert.IsTrue((await reopened.InitializeAsync(Cancellation)).Completed);
        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsFalse(await reopened.TryWriteAsync(token, AnalysisTestEnvironment.Description("late after restart"), Cancellation));
    }

    [TestMethod]
    public async Task RetriedCleanupPreservesNewerGenerationResults()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken old = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(old, AnalysisTestEnvironment.Description("old"), Cancellation);
        environment.Files.FailCleanup = true;
        await store.ClearAsync(Cancellation);
        AnalysisWriteToken fresh = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(fresh, AnalysisTestEnvironment.Description("new"), Cancellation);
        environment.Files.FailCleanup = false;
        Assert.IsTrue((await store.InitializeAsync(Cancellation)).Completed);
        Assert.HasCount(1, environment.MetadataPaths);
        Assert.AreEqual("new", DescriptionText((await store.GetAsync(id, cancellationToken: Cancellation))!));
    }

    [TestMethod]
    public async Task ClearAndInFlightPublicationAreSerializedWithoutResurrection()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Files.BeforeWrite = async (path, ct) =>
        {
            if (!path.EndsWith(".analysis", StringComparison.Ordinal)) return;
            entered.SetResult();
            await release.Task.WaitAsync(ct);
        };
        Task<bool> write = store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancellation);
        Task<AnalysisCleanupResult> clear = store.ClearAsync(Cancellation);
        Assert.IsFalse(clear.IsCompleted);
        release.SetResult();
        Assert.IsTrue(await write);
        Assert.IsTrue((await clear).Completed);
        Assert.IsNull(await store.GetAsync(id, cancellationToken: Cancellation));
        Assert.IsFalse(await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation));
    }

    [TestMethod]
    public async Task FailedClearPublicationDoesNotInvalidateCommittedData()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("retained"), Cancellation);
        environment.Files.BeforeWrite = (path, _) => path == environment.ControlPath
            ? throw new IOException("Injected control publication failure.") : Task.CompletedTask;
        await Assert.ThrowsExactlyAsync<IOException>(() => store.ClearAsync(Cancellation));
        Assert.AreEqual("retained", DescriptionText((await store.GetAsync(id, cancellationToken: Cancellation))!));
        Assert.IsTrue(await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("still authorized"), Cancellation));
    }

    [TestMethod]
    public async Task CancellationBeforePublicationLeavesPreviousRecordAndNoTemporaryFiles()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("retained"), Cancellation);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        environment.Files.BeforeWrite = (_, _) => { cancelled.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.TryWriteAsync(token, AnalysisTestEnvironment.Description("cancelled"), cancelled.Token));
        Assert.AreEqual("retained", DescriptionText((await store.GetAsync(id, cancellationToken: Cancellation))!));
        Assert.IsEmpty(Directory.GetFiles(environment.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task UnknownSchemaAndMismatchedIdentityAreNotOverwritten()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        string path = environment.MetadataPaths.Single();
        await MutateDocument(environment, path, node => node["Version"] = 99);
        byte[] unknown = await File.ReadAllBytesAsync(path, Cancellation);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.GetAsync(id, cancellationToken: Cancellation));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation));
        CollectionAssert.AreEqual(unknown, await File.ReadAllBytesAsync(path, Cancellation));

        await MutateDocument(environment, path, node => { node["Version"] = 1; node["CaptureId"] = Guid.NewGuid().ToString(); });
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.GetAsync(id, cancellationToken: Cancellation));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("corrupt")]
    [DataRow("unsupported")]
    public async Task InvalidControlCannotSilentlyCreateFreshStorage(string damage)
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        await DamageControl(environment, damage);
        byte[]? control = File.Exists(environment.ControlPath) ? await File.ReadAllBytesAsync(environment.ControlPath, Cancellation) : null;
        string path = environment.MetadataPaths.Single();
        byte[] metadata = await File.ReadAllBytesAsync(path, Cancellation);

        Func<Task>[] operations = [
            () => store.InitializeAsync(Cancellation),
            () => store.GetAsync(id, cancellationToken: Cancellation),
            () => store.ReadAllAsync(Cancellation),
            () => store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation),
            () => store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation),
        ];
        foreach (Func<Task> operation in operations)
        {
            if (damage == "corrupt") await Assert.ThrowsAsync<CryptographicException>(operation);
            else await Assert.ThrowsExactlyAsync<InvalidDataException>(operation);
        }

        if (control == null) Assert.IsFalse(File.Exists(environment.ControlPath));
        else CollectionAssert.AreEqual(control, await File.ReadAllBytesAsync(environment.ControlPath, Cancellation));
        CollectionAssert.AreEqual(metadata, await File.ReadAllBytesAsync(path, Cancellation));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("corrupt")]
    [DataRow("unsupported")]
    public async Task ExplicitClearRecoversInvalidControlAndFencesOldWritesAcrossRestart(string damage)
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken old = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(old, AnalysisTestEnvironment.Description(), Cancellation);
        await DamageControl(environment, damage);

        Assert.IsTrue((await store.ClearAsync(Cancellation)).Completed);
        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsEmpty(await store.ReadAllAsync(Cancellation));
        Assert.IsFalse(await store.TryWriteAsync(old, AnalysisTestEnvironment.Description("late"), Cancellation));

        using LocalCaptureAnalysisStore reopened = environment.CreateStore();
        Assert.IsTrue((await reopened.InitializeAsync(Cancellation)).Completed);
        Assert.IsNull(await reopened.GetAsync(id, cancellationToken: Cancellation));
        AnalysisWriteToken fresh = await reopened.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        Assert.AreNotEqual(old.Generation, fresh.Generation);
        Assert.IsTrue(await reopened.TryWriteAsync(fresh, AnalysisTestEnvironment.Description("new"), Cancellation));
        Assert.IsFalse(await reopened.TryWriteAsync(old, AnalysisTestEnvironment.Description("late after restart"), Cancellation));
        Assert.AreEqual("new", DescriptionText((await reopened.GetAsync(id, cancellationToken: Cancellation))!));
    }

    [TestMethod]
    [DataRow("protection")]
    [DataRow("publication")]
    [DataRow("cancellation")]
    public async Task FailedRecoveryPublicationPreservesUnreadableStorageForRetry(string failure)
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken old = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(old, AnalysisTestEnvironment.Description(), Cancellation);
        await DamageControl(environment, "corrupt");
        byte[] control = await File.ReadAllBytesAsync(environment.ControlPath, Cancellation);
        string path = environment.MetadataPaths.Single();
        byte[] metadata = await File.ReadAllBytesAsync(path, Cancellation);

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        switch (failure)
        {
            case "protection":
                environment.Protector.FailProtection = true;
                await Assert.ThrowsExactlyAsync<CryptographicException>(() => store.ClearAsync(Cancellation));
                break;
            case "publication":
                environment.Files.BeforeWrite = (_, _) => throw new IOException("Injected publication failure.");
                await Assert.ThrowsExactlyAsync<IOException>(() => store.ClearAsync(Cancellation));
                break;
            case "cancellation":
                environment.Files.BeforeWrite = (_, _) => { cancelled.Cancel(); return Task.CompletedTask; };
                await Assert.ThrowsAsync<OperationCanceledException>(() => store.ClearAsync(cancelled.Token));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }

        CollectionAssert.AreEqual(control, await File.ReadAllBytesAsync(environment.ControlPath, Cancellation));
        CollectionAssert.AreEqual(metadata, await File.ReadAllBytesAsync(path, Cancellation));
        Assert.IsEmpty(Directory.GetFiles(environment.Root, "*.tmp", SearchOption.AllDirectories));
        environment.Protector.FailProtection = false;
        environment.Files.BeforeWrite = null;
        Assert.IsTrue((await store.ClearAsync(Cancellation)).Completed);
        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsFalse(await store.TryWriteAsync(old, AnalysisTestEnvironment.Description("late"), Cancellation));
    }

    [TestMethod]
    public async Task UnknownPayloadSchemaIsPreservedAndExplicitClearStillWorks()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        await MutateDocument(environment, environment.MetadataPaths.Single(), node => node["Results"]![0]!["SchemaVersion"] = 99);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation));
        Assert.IsTrue((await store.ClearAsync(Cancellation)).Completed);
        Assert.IsEmpty(environment.MetadataPaths);
    }

    private async Task MutateDocument(AnalysisTestEnvironment environment, string path, Action<JsonNode> change)
    {
        byte[] ciphertext = await File.ReadAllBytesAsync(path, Cancellation);
        JsonNode node = JsonNode.Parse(environment.Protector.Unprotect(ciphertext))!;
        change(node);
        await File.WriteAllBytesAsync(path, environment.Protector.Protect(Encoding.UTF8.GetBytes(node.ToJsonString())), Cancellation);
    }

    private async Task DamageControl(AnalysisTestEnvironment environment, string damage)
    {
        switch (damage)
        {
            case "missing":
                File.Delete(environment.ControlPath);
                break;
            case "corrupt":
                byte[] bytes = await File.ReadAllBytesAsync(environment.ControlPath, Cancellation);
                bytes[^1] ^= 1;
                await File.WriteAllBytesAsync(environment.ControlPath, bytes, Cancellation);
                break;
            case "unsupported":
                await MutateDocument(environment, environment.ControlPath, node => node["Version"] = 99);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(damage));
        }
    }

    private static string DescriptionText(CaptureAnalysisRecord record) =>
        ((DescriptionMetadata)record.Results.Single().Payload).Descriptions.Single().Text;

    [TestMethod]
    public async Task LockedDestinationFailsAtomicReplacementWithoutLosingPreviousMetadata()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("Exercises Windows file-sharing behavior.");
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await store.TryWriteAsync(token, AnalysisTestEnvironment.Description("committed"), Cancellation);
        string path = environment.MetadataPaths.Single();
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(() => store.TryWriteAsync(token, AnalysisTestEnvironment.Description("not committed"), Cancellation));
        }
        Assert.AreEqual("committed", DescriptionText((await store.GetAsync(id, cancellationToken: Cancellation))!));
        Assert.IsEmpty(Directory.GetFiles(environment.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task MissingRequiredFieldsCannotSilentlyBecomeDefaultMetadata()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        CaptureId id = CaptureId.New();
        await store.BeginRunAsync(id, AnalysisMediaKind.Video, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        string path = environment.MetadataPaths.Single();
        await MutateDocument(environment, path, node => node.AsObject().Remove("MediaKind"));
        byte[] incomplete = await File.ReadAllBytesAsync(path, Cancellation);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.GetAsync(id, cancellationToken: Cancellation));
        CollectionAssert.AreEqual(incomplete, await File.ReadAllBytesAsync(path, Cancellation));
    }

    [TestMethod]
    public async Task InterruptedFirstControlWriteCanInitializeWithoutResettingAnyCommittedData()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(Path.GetDirectoryName(environment.ControlPath)!);
        string interrupted = environment.ControlPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(interrupted, [1, 2, 3], Cancellation); // Incomplete, unpublished ciphertext.
        using LocalCaptureAnalysisStore store = environment.CreateStore();
        Assert.IsTrue((await store.InitializeAsync(Cancellation)).Completed);
        Assert.IsFalse(File.Exists(interrupted));
        CaptureId id = CaptureId.New();
        AnalysisWriteToken token = await store.BeginRunAsync(id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        Assert.IsTrue(await store.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation));
    }
}
