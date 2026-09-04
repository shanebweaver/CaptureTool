using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.Analyzers;

[TestClass]
public sealed class WindowsImageDescriptionAnalyzerTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public void Descriptor_ShouldRecordModelProvenanceAndUserInitiatedPreparation()
    {
        var analyzer = CreateAnalyzer(ImageDescriptionAnalysisResult.Unsupported);
        AnalyzerIdentity identity = analyzer.Descriptor.Identity;

        CaptureAnalyzerContractAssertions.AssertOnDeviceImageAnalyzer(
            analyzer,
            AnalysisCapabilities.ImageDescriptionV1);
        Assert.AreEqual("windows-image-description", identity.AnalyzerId);
        Assert.AreEqual("microsoft-windows", identity.ProviderId);
        Assert.AreEqual("windows-app-sdk-image-description", identity.ModelId);
        Assert.AreEqual("model-1", identity.ModelVersion);
        Assert.AreEqual(WindowsImageDescriptionAnalyzer.AdapterVersion, identity.AdapterVersion);
        Assert.AreEqual("windows-app-sdk-ai", identity.RuntimeId);
        Assert.AreEqual("2.2.3", identity.RuntimeVersion);
        Assert.AreEqual("2.2.3", identity.PackageVersion);
        Assert.IsTrue(analyzer.Descriptor.Requirements.HasFlag(
            CaptureAnalyzerRequirement.ModelPackage));
        Assert.IsTrue(analyzer.Descriptor.Requirements.HasFlag(
            CaptureAnalyzerRequirement.UserInitiatedPreparation));
        Assert.AreEqual(CaptureAnalyzerWorkloadClass.AiIntensive, analyzer.Descriptor.WorkloadClass);
    }

    [TestMethod]
    public async Task AnalyzeSuccess_ShouldStoreOnlyNormalizedBriefInferredPayload()
    {
        var analyzer = CreateAnalyzer(ImageDescriptionAnalysisResult.Succeeded(
            "  A cafe\u0301 beside a river.\r\n"));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        CaptureAnalyzerContractAssertions.AssertCompatibleSuccess(analyzer, output);
        var payload = (ImageDescriptionV1)output.Payload!;
        Assert.AreEqual("A café beside a river.", payload.Description);
        Assert.AreEqual(ImageDescriptionPurpose.Brief, payload.Purpose);
        Assert.IsNull(payload.Style);
        Assert.IsNull(payload.Confidence);
    }

    [TestMethod]
    [DataRow(ImageDescriptionAnalysisStatus.PreparationRequired, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.ModelNotReady, AnalysisFailureDisposition.Transient)]
    [DataRow(ImageDescriptionAnalysisStatus.Unsupported, CaptureAnalyzerOutputStatus.Unsupported,
        AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisStatus.Disabled, CaptureAnalyzerOutputStatus.Unsupported,
        AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisStatus.BlockedByPolicy, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.AuthorizationDenied, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisStatus.BlockedByContentSafety, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisStatus.InputTooLarge, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.InputTooLarge, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisStatus.TransientFailure, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.ProviderUnavailable, AnalysisFailureDisposition.Transient)]
    [DataRow(ImageDescriptionAnalysisStatus.TerminalFailure, CaptureAnalyzerOutputStatus.Failed,
        AnalysisFailureCode.InvalidResponse, AnalysisFailureDisposition.Terminal)]
    public async Task AnalyzeBoundedOutcome_ShouldMapWithoutProviderDetails(
        ImageDescriptionAnalysisStatus sourceStatus,
        CaptureAnalyzerOutputStatus expectedStatus,
        AnalysisFailureCode expectedCode,
        AnalysisFailureDisposition expectedDisposition)
    {
        ImageDescriptionAnalysisResult result = sourceStatus switch
        {
            ImageDescriptionAnalysisStatus.PreparationRequired =>
                ImageDescriptionAnalysisResult.PreparationRequired,
            ImageDescriptionAnalysisStatus.Unsupported => ImageDescriptionAnalysisResult.Unsupported,
            ImageDescriptionAnalysisStatus.Disabled => ImageDescriptionAnalysisResult.Disabled,
            ImageDescriptionAnalysisStatus.BlockedByPolicy =>
                ImageDescriptionAnalysisResult.BlockedByPolicy,
            ImageDescriptionAnalysisStatus.BlockedByContentSafety =>
                ImageDescriptionAnalysisResult.BlockedByContentSafety,
            ImageDescriptionAnalysisStatus.InputTooLarge => ImageDescriptionAnalysisResult.InputTooLarge,
            ImageDescriptionAnalysisStatus.TransientFailure =>
                ImageDescriptionAnalysisResult.TransientFailure,
            ImageDescriptionAnalysisStatus.TerminalFailure =>
                ImageDescriptionAnalysisResult.TerminalFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceStatus)),
        };
        var analyzer = CreateAnalyzer(result);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(expectedStatus, output.Status);
        Assert.IsNull(output.Payload);
        Assert.AreEqual(expectedCode, output.Failure?.Code);
        Assert.AreEqual(expectedDisposition, output.Failure?.Disposition);
    }

    [TestMethod]
    public async Task AnalyzeCancelled_ShouldReturnCancelled()
    {
        var analyzer = CreateAnalyzer(ImageDescriptionAnalysisResult.Cancelled);

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Cancelled, output.Status);
        Assert.IsNull(output.Payload);
        Assert.IsNull(output.Failure);
    }

    [TestMethod]
    public async Task AnalyzeOversizedDescription_ShouldReturnInvalidResponse()
    {
        var analyzer = CreateAnalyzer(ImageDescriptionAnalysisResult.Succeeded(new string('x', 4097)));

        CaptureAnalyzerOutput output = await analyzer.AnalyzeAsync(CreateRequest(analyzer));

        Assert.AreEqual(CaptureAnalyzerOutputStatus.Failed, output.Status);
        Assert.AreEqual(AnalysisFailureCode.InvalidResponse, output.Failure?.Code);
        Assert.AreEqual(AnalysisFailureDisposition.Terminal, output.Failure?.Disposition);
    }

    [TestMethod]
    [DataRow(ImageDescriptionReadyState.Ready, CaptureAnalyzerAvailabilityStatus.Available)]
    [DataRow(ImageDescriptionReadyState.PreparationNeeded, CaptureAnalyzerAvailabilityStatus.PreparationRequired)]
    [DataRow(ImageDescriptionReadyState.NotSupported, CaptureAnalyzerAvailabilityStatus.Unsupported)]
    [DataRow(ImageDescriptionReadyState.Disabled, CaptureAnalyzerAvailabilityStatus.Disabled)]
    [DataRow(ImageDescriptionReadyState.Unknown, CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable)]
    public async Task Availability_ShouldMapProviderReadiness(
        ImageDescriptionReadyState readyState,
        CaptureAnalyzerAvailabilityStatus expected)
    {
        var service = new StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult.Unsupported,
            readyState: readyState);
        var analyzer = new WindowsImageDescriptionAnalyzer(service);
        var request = new CaptureAnalyzerAvailabilityRequest(
            analyzer.Descriptor,
            CaptureMediaKind.Image,
            sourceLength: 1,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));

        CaptureAnalyzerAvailability availability = await analyzer.GetAvailabilityAsync(request);

        Assert.AreEqual(expected, availability.Status);
    }

    [TestMethod]
    public async Task Prepare_ShouldForwardProgressAndMapSuccess()
    {
        var service = new StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult.Unsupported,
            preparationResult: ImageDescriptionAnalysisPreparationResult.Succeeded);
        service.PrepareHandler = (progress, _) =>
        {
            progress?.Report(0.4);
            progress?.Report(1);
            return Task.FromResult(ImageDescriptionAnalysisPreparationResult.Succeeded);
        };
        var analyzer = new WindowsImageDescriptionAnalyzer(service);
        var progress = new RecordingProgress();

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync(progress);

        Assert.AreEqual(CaptureAnalyzerPreparationStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { 0.4, 1d }, progress.Values.ToArray());
    }

    [TestMethod]
    [DataRow(ImageDescriptionAnalysisPreparationStatus.Unsupported,
        CaptureAnalyzerPreparationStatus.Unsupported, AnalysisFailureDisposition.Terminal)]
    [DataRow(ImageDescriptionAnalysisPreparationStatus.Disabled,
        CaptureAnalyzerPreparationStatus.Disabled, AnalysisFailureDisposition.Unknown)]
    [DataRow(ImageDescriptionAnalysisPreparationStatus.Cancelled,
        CaptureAnalyzerPreparationStatus.Cancelled, AnalysisFailureDisposition.Unknown)]
    [DataRow(ImageDescriptionAnalysisPreparationStatus.TransientFailure,
        CaptureAnalyzerPreparationStatus.Failed, AnalysisFailureDisposition.Transient)]
    [DataRow(ImageDescriptionAnalysisPreparationStatus.TerminalFailure,
        CaptureAnalyzerPreparationStatus.Failed, AnalysisFailureDisposition.Terminal)]
    public async Task PrepareOutcome_ShouldMapToBoundedStatus(
        ImageDescriptionAnalysisPreparationStatus sourceStatus,
        CaptureAnalyzerPreparationStatus expectedStatus,
        AnalysisFailureDisposition expectedDisposition)
    {
        ImageDescriptionAnalysisPreparationResult providerResult = sourceStatus switch
        {
            ImageDescriptionAnalysisPreparationStatus.Unsupported =>
                ImageDescriptionAnalysisPreparationResult.Unsupported,
            ImageDescriptionAnalysisPreparationStatus.Disabled =>
                ImageDescriptionAnalysisPreparationResult.Disabled,
            ImageDescriptionAnalysisPreparationStatus.Cancelled =>
                ImageDescriptionAnalysisPreparationResult.Cancelled,
            ImageDescriptionAnalysisPreparationStatus.TransientFailure =>
                ImageDescriptionAnalysisPreparationResult.TransientFailure,
            ImageDescriptionAnalysisPreparationStatus.TerminalFailure =>
                ImageDescriptionAnalysisPreparationResult.TerminalFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceStatus)),
        };
        var service = new StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult.Unsupported,
            preparationResult: providerResult);
        var analyzer = new WindowsImageDescriptionAnalyzer(service);

        CaptureAnalyzerPreparationResult result = await analyzer.PrepareAsync();

        Assert.AreEqual(expectedStatus, result.Status);
        Assert.AreEqual(expectedDisposition, result.Failure?.Disposition ?? AnalysisFailureDisposition.Unknown);
    }

    [TestMethod]
    public void ProducerRevisionChange_ShouldMakePreviousAnalyzerRevisionStale()
    {
        var firstService = new StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult.Unsupported,
            modelVersion: "model-1");
        var secondService = new StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult.Unsupported,
            modelVersion: "model-2");
        var first = new WindowsImageDescriptionAnalyzer(firstService);
        var second = new WindowsImageDescriptionAnalyzer(secondService);

        Assert.AreNotEqual(first.Descriptor.Revision, second.Descriptor.Revision);
        Assert.AreNotEqual(first.Descriptor.Identity.ModelVersion, second.Descriptor.Identity.ModelVersion);
    }

    private static WindowsImageDescriptionAnalyzer CreateAnalyzer(
        ImageDescriptionAnalysisResult result)
    {
        return new WindowsImageDescriptionAnalyzer(new StubImageDescriptionAnalysisService(result));
    }

    private static CaptureAnalysisRequest CreateRequest(WindowsImageDescriptionAnalyzer analyzer)
    {
        return new CaptureAnalysisRequest(
            analyzer.Descriptor,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose),
            new MemoryAnalysisSource([1, 2, 3]));
    }

    private sealed class RecordingProgress : IProgress<AnalysisCapabilityPreparationProgress>
    {
        public List<double> Values { get; } = [];

        public void Report(AnalysisCapabilityPreparationProgress value)
        {
            Values.Add(value.FractionComplete);
        }
    }

    private sealed class StubImageDescriptionAnalysisService : IImageDescriptionAnalysisService
    {
        private readonly ImageDescriptionAnalysisResult _result;
        private readonly ImageDescriptionAnalysisPreparationResult _preparationResult;
        private readonly ImageDescriptionReadyState _readyState;

        public StubImageDescriptionAnalysisService(
            ImageDescriptionAnalysisResult result,
            ImageDescriptionAnalysisPreparationResult? preparationResult = null,
            ImageDescriptionReadyState readyState = ImageDescriptionReadyState.Ready,
            string? modelVersion = "model-1")
        {
            _result = result;
            _preparationResult = preparationResult ?? ImageDescriptionAnalysisPreparationResult.Unsupported;
            _readyState = readyState;
            ModelDescriptor = new ImageDescriptionModelDescriptor(
                "microsoft-windows",
                "windows-app-sdk-image-description",
                modelVersion,
                "windows-app-sdk-ai",
                "2.2.3",
                "2.2.3");
        }

        public Func<IProgress<double>?, CancellationToken,
            Task<ImageDescriptionAnalysisPreparationResult>>? PrepareHandler { get; set; }

        public ImageDescriptionModelDescriptor ModelDescriptor { get; }

        public ImageDescriptionReadyState GetReadyState()
        {
            return _readyState;
        }

        public Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return PrepareHandler?.Invoke(progress, cancellationToken) ??
                Task.FromResult(_preparationResult);
        }

        public Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class MemoryAnalysisSource : ICaptureAnalysisSource
    {
        private readonly byte[] _bytes;

        public MemoryAnalysisSource(byte[] bytes)
        {
            _bytes = bytes;
            DateTimeOffset timestamp = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
            SourceRevision = new(
                bytes.LongLength,
                timestamp,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        public CaptureId CaptureId { get; } = CaptureId.New();

        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(_bytes, writable: false);
            return ValueTask.FromResult(stream);
        }
    }
}
