using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;

namespace CaptureTool.Infrastructure.Tests.Analysis.Persistence;

internal static class AnalysisPersistenceTestData
{
    public static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 7, 6, 15, 30, 123, TimeSpan.Zero);

    public static readonly SourceRevision SourceRevision = new(
        9_876_543_210,
        CapturedAtUtc.AddMinutes(1),
        ContentFingerprint.Sha256(new string('a', 64)));

    public static readonly AnalyzerIdentity Analyzer = new(
        "windows-ai-analysis",
        "windows-ai",
        "fixture-model",
        "1.2.3",
        "adapter-1",
        "windows-app-sdk",
        "1.7",
        "2.0.0",
        $"sha256:{new string('b', 64)}");

    public static CaptureAnalysisRecord CreateRecord(
        CaptureId? captureId = null,
        string fullText = "OCR-CANARY-海-12345")
    {
        CaptureId id = captureId ?? CaptureId.New();
        var recipe = new CaptureAnalysisRecipe(
            new AnalysisRecipeId("capture-memory-image"),
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            [
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Optional),
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Optional),
            ]);

        var mediaProperties = new MediaPropertiesV1(
            CaptureMediaKind.Image,
            new PixelSize(1920, 1080),
            TimeSpan.FromTicks(12_345_678),
            "image/png",
            "png",
            "h264",
            "aac",
            2,
            48_000,
            18_000_000_001,
            59.940_059_940_1);
        var ocr = new OcrDocumentV1(
            new PixelSize(1920, 1080),
            fullText,
            [
                new OcrLanguageCandidateV1("en-US", 0.987_654_321),
                new OcrLanguageCandidateV1("zh-Hans", null),
            ],
            [
                new OcrRegionV1(
                    new PixelRect(10.25, 20.5, 600.75, 140.125),
                    [
                        new OcrLineV1(
                            fullText,
                            new PixelRect(12.5, 24.25, 580.5, 60.75),
                            [
                                new OcrWordV1(
                                    "OCR-CANARY-海-12345",
                                    new PixelRect(14.75, 28.5, 240.125, 42.25),
                                    0.876_543_21),
                            ],
                            0.912_345_678),
                    ],
                    0.923_456_789),
            ]);
        var description = new ImageDescriptionV1(
            "A Capture Tool window showing structured OCR metadata.",
            ImageDescriptionPurpose.Brief,
            "technical-ui",
            0.765_432_1);

        return new(
            id,
            CaptureMediaKind.Image,
            CapturedAtUtc,
            SourceRevision,
            recipe,
            [
                CreateAnalysis(id, mediaProperties, CapturedAtUtc.AddSeconds(1)),
                CreateAnalysis(id, ocr, CapturedAtUtc.AddSeconds(2)),
                CreateAnalysis(id, description, CapturedAtUtc.AddSeconds(3)),
            ]);
    }

    public static CaptureAnalysisControlState CreateControlState(
        CaptureId? enrolledCaptureId = null,
        CaptureAnalysisEnrollment? additionalEnrollment = null)
    {
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence: 40);
        var enrollments = new List<CaptureAnalysisEnrollment>
        {
            new(
                enrolledCaptureId ?? CaptureId.New(),
                CaptureAnalysisEnrollmentState.Enrolled,
                CaptureAnalysisExclusionReason.None,
                enrollmentGeneration: 1,
                tombstoneGeneration: 0,
                assetFinalizationSequence: 41,
                new AnalysisRecipeId("capture-memory-image"),
                new AnalysisRecipeVersion(1)),
        };
        if (additionalEnrollment != null)
        {
            enrollments.Add(additionalEnrollment);
        }

        return new(policy, enrollments);
    }

    public static void AssertRecordsEquivalent(
        CaptureAnalysisRecord expected,
        CaptureAnalysisRecord actual)
    {
        Assert.AreEqual(expected.CaptureId, actual.CaptureId);
        Assert.AreEqual(expected.MediaKind, actual.MediaKind);
        Assert.AreEqual(expected.CapturedAtUtc, actual.CapturedAtUtc);
        Assert.AreEqual(expected.SourceRevision, actual.SourceRevision);
        Assert.AreEqual(expected.Recipe.Id, actual.Recipe.Id);
        Assert.AreEqual(expected.Recipe.Version, actual.Recipe.Version);
        CollectionAssert.AreEquivalent(
            expected.Recipe.Capabilities.ToArray(),
            actual.Recipe.Capabilities.ToArray());
        Assert.HasCount(expected.Analyses.Count, actual.Analyses);
        foreach (CapabilityAnalysis expectedAnalysis in expected.Analyses)
        {
            Assert.IsTrue(actual.TryGetAnalysis(expectedAnalysis.Capability.Id, out CapabilityAnalysis? actualAnalysis));
            Assert.IsNotNull(actualAnalysis);
            Assert.AreEqual(expectedAnalysis.Capability, actualAnalysis.Capability);
            Assert.IsTrue(expectedAnalysis.CanonicalResult!.IsEquivalentTo(actualAnalysis.CanonicalResult!));
            Assert.AreEqual(expectedAnalysis.LatestOutcome, actualAnalysis.LatestOutcome);
        }
    }

    public static string CreateTestFolder()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolTests",
            "CaptureAnalysis",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static CapabilityAnalysis CreateAnalysis(
        CaptureId captureId,
        CapabilityPayload payload,
        DateTimeOffset generatedAtUtc)
    {
        var result = new CanonicalCapabilityResult(
            captureId,
            SourceRevision,
            payload,
            Analyzer,
            ProcessingBoundary.OnDevice,
            generatedAtUtc);
        return new(payload.Definition, result, null);
    }
}

internal sealed class TestDataProtectionService : IUserDataProtectionService
{
    private const byte Marker = 0xA5;

    public bool FailProtect { get; set; }

    public bool FailUnprotect { get; set; }

    public byte[] Protect(byte[] plaintext)
    {
        if (FailProtect)
        {
            throw new IOException("Protection unavailable.");
        }

        byte[] result = new byte[plaintext.Length + 1];
        result[0] = Marker;
        for (int index = 0; index < plaintext.Length; index++)
        {
            result[index + 1] = (byte)(plaintext[index] ^ Marker);
        }

        return result;
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        if (FailUnprotect || protectedData.Length == 0 || protectedData[0] != Marker)
        {
            throw new InvalidDataException("Invalid protected payload.");
        }

        byte[] result = new byte[protectedData.Length - 1];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = (byte)(protectedData[index + 1] ^ Marker);
        }

        return result;
    }
}

internal sealed class TestStorageService(string dataFolder) : IStorageService
{
    public string GetApplicationDataFolderPath() => dataFolder;
    public string GetApplicationRetainedCaptureFolderPath() => Path.Combine(dataFolder, "Captures");
    public string GetApplicationScratchFolderPath() => Path.Combine(dataFolder, "Scratch");
    public string GetSystemDefaultMusicFolderPath() => Path.Combine(dataFolder, "Music");
    public string GetSystemDefaultScreenshotsFolderPath() => Path.Combine(dataFolder, "Pictures");
    public string GetSystemDefaultVideosFolderPath() => Path.Combine(dataFolder, "Videos");
    public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");
}

internal sealed class TestLocalCachePathProvider(string localCacheFolder) :
    IApplicationLocalCachePathProvider
{
    public string GetApplicationLocalCacheFolderPath() => localCacheFolder;
}

internal sealed class TestLogService : ILogService
{
    public event EventHandler<ILogEntry>? LogAdded
    {
        add { }
        remove { }
    }

    public List<Exception> Exceptions { get; } = [];

    public bool IsEnabled => true;
    public void ClearLogs() { }
    public void Disable() { }
    public void Enable() { }
    public IEnumerable<ILogEntry> GetLogs() => [];
    public void LogException(Exception e, string? message = null) => Exceptions.Add(e);
    public void LogInformation(string info) { }
    public void LogWarning(string warning) { }
}

internal sealed class InterruptingAtomicFileWriter : IAtomicFileWriter
{
    private readonly AtomicFileWriter _inner = new();

    public bool InterruptNextWrite { get; set; }

    public void Write(string destinationPath, ReadOnlySpan<byte> contents)
    {
        if (InterruptNextWrite)
        {
            InterruptNextWrite = false;
            throw new IOException("Simulated interruption before atomic replace.");
        }

        _inner.Write(destinationPath, contents);
    }
}
