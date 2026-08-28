using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Application.Analysis.Preparation;
using CaptureTool.Domain.Analysis;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Preparation;

[TestClass]
public sealed class CaptureAnalysisCapabilityPreparationServiceTests
{
    private static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);

    [TestMethod]
    public async Task GetState_WhenModelNeedsPreparation_ShouldNotStartPreparation()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        TestContext context = CreateContext(analyzer);

        AnalysisCapabilityPreparationState state = await context.Service.GetStateAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.PreparationRequired, state.Status);
        Assert.AreEqual(analyzer.Descriptor.Identity, state.Analyzer);
        Assert.AreEqual(ProcessingBoundary.OnDevice, state.ProcessingBoundary);
        Assert.AreEqual(0, analyzer.PrepareCallCount);
        Assert.AreEqual(0, context.WakeSignal.SignalCount);
    }

    [TestMethod]
    public async Task Prepare_WhenUserInitiatedAndSuccessful_ShouldReportProgressRecheckAndWakeWorker()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        analyzer.PrepareHandler = (progress, _) =>
        {
            progress?.Report(new AnalysisCapabilityPreparationProgress(0.25));
            progress?.Report(new AnalysisCapabilityPreparationProgress(1));
            analyzer.AvailabilityStatus = CaptureAnalyzerAvailabilityStatus.Available;
            return Task.FromResult(CaptureAnalyzerPreparationResult.Succeeded);
        };
        TestContext context = CreateContext(analyzer);
        var progress = new RecordingProgress();

        AnalysisCapabilityPreparationState state = await context.Service.PrepareAsync(
            CreateRequest(),
            progress);

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Ready, state.Status);
        CollectionAssert.AreEqual(new[] { 0.25, 1d }, progress.Values.ToArray());
        Assert.AreEqual(1, analyzer.PrepareCallCount);
        Assert.AreEqual(2, analyzer.AvailabilityCallCount);
        Assert.AreEqual(1, context.WakeSignal.SignalCount);
        context.JobStore.Verify(store => store.ResumeWaitingForCapabilityAsync(
            AnalysisCapabilities.ImageDescriptionV1,
            ProcessingBoundary.OnDevice,
            It.Is<DateTimeOffset>(value => value.Offset == TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Prepare_WhenCancelled_ShouldNotWakeWaitingIntents()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired)
        {
            PrepareHandler = (_, _) => Task.FromResult(CaptureAnalyzerPreparationResult.Cancelled),
        };
        TestContext context = CreateContext(analyzer);

        AnalysisCapabilityPreparationState state = await context.Service.PrepareAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Cancelled, state.Status);
        Assert.AreEqual(0, context.WakeSignal.SignalCount);
    }

    [TestMethod]
    public async Task ConcurrentPreparation_ShouldExposePreparingWithoutReleasingOwnersGate()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        analyzer.PrepareHandler = async (_, cancellationToken) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            analyzer.AvailabilityStatus = CaptureAnalyzerAvailabilityStatus.Available;
            return CaptureAnalyzerPreparationResult.Succeeded;
        };
        TestContext context = CreateContext(analyzer);

        Task<AnalysisCapabilityPreparationState> first = context.Service.PrepareAsync(CreateRequest());
        await entered.Task;

        AnalysisCapabilityPreparationState query = await context.Service.GetStateAsync(CreateRequest());
        AnalysisCapabilityPreparationState second = await context.Service.PrepareAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Preparing, query.Status);
        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Preparing, second.Status);
        Assert.AreEqual(1, analyzer.PrepareCallCount);

        AnalysisCapabilityPreparationState stillPreparing = await context.Service.GetStateAsync(CreateRequest());
        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Preparing, stillPreparing.Status);

        release.SetResult();
        AnalysisCapabilityPreparationState completed = await first;
        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Ready, completed.Status);
        Assert.AreEqual(1, context.WakeSignal.SignalCount);
    }

    [TestMethod]
    public async Task ActivePreparation_ShouldExposeProviderProgressUntilPreparationCompletes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        analyzer.PrepareHandler = async (progress, cancellationToken) =>
        {
            progress?.Report(new AnalysisCapabilityPreparationProgress(0.42));
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            analyzer.AvailabilityStatus = CaptureAnalyzerAvailabilityStatus.Available;
            return CaptureAnalyzerPreparationResult.Succeeded;
        };
        TestContext context = CreateContext(analyzer);

        Task<AnalysisCapabilityPreparationState> preparation =
            context.Service.PrepareAsync(CreateRequest());
        await entered.Task;

        IReadOnlyList<CaptureAnalysisModelPreparationActivity> activities =
            context.Service.GetCurrentPreparations();
        Assert.HasCount(1, activities);
        CaptureAnalysisModelPreparationActivity activity = activities[0];
        Assert.AreEqual(analyzer.Descriptor.Identity, activity.Analyzer);
        Assert.AreEqual(AnalysisCapabilities.ImageDescriptionV1, activity.Capability);
        Assert.AreEqual(CaptureMediaKind.Image, activity.MediaKind);
        Assert.AreEqual(0.42, activity.FractionComplete);

        release.SetResult();
        AnalysisCapabilityPreparationState completed = await preparation;

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Ready, completed.Status);
        Assert.IsEmpty(context.Service.GetCurrentPreparations());
    }

    [TestMethod]
    public async Task FeatureDisabled_ShouldReturnDisabledWithoutProviderProbeOrPreparation()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        TestContext context = CreateContext(analyzer, featureEnabled: false);

        AnalysisCapabilityPreparationState query = await context.Service.GetStateAsync(CreateRequest());
        AnalysisCapabilityPreparationState command = await context.Service.PrepareAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Disabled, query.Status);
        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Disabled, command.Status);
        Assert.AreEqual(0, analyzer.AvailabilityCallCount);
        Assert.AreEqual(0, analyzer.PrepareCallCount);
    }

    [TestMethod]
    public async Task PrepareWhenAlreadyReady_ShouldResumeStrandedIntentsAndWakeWorker()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.Available);
        TestContext context = CreateContext(analyzer);

        AnalysisCapabilityPreparationState state = await context.Service.PrepareAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Ready, state.Status);
        Assert.AreEqual(0, analyzer.PrepareCallCount);
        Assert.AreEqual(1, context.WakeSignal.SignalCount);
        context.JobStore.Verify(store => store.ResumeWaitingForCapabilityAsync(
            AnalysisCapabilities.ImageDescriptionV1,
            ProcessingBoundary.OnDevice,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UnsupportedProvider_ShouldReturnBoundedTerminalState()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.Unsupported);
        TestContext context = CreateContext(analyzer);

        AnalysisCapabilityPreparationState state = await context.Service.GetStateAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Unsupported, state.Status);
        Assert.AreEqual(AnalysisFailureCode.CapabilityUnavailable, state.Failure?.Code);
        Assert.AreEqual(AnalysisFailureDisposition.Terminal, state.Failure?.Disposition);
    }

    [TestMethod]
    public async Task SuccessfulPreparationThatRemainsNotReady_ShouldFailTransientlyWithoutWake()
    {
        var analyzer = new StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus.PreparationRequired);
        TestContext context = CreateContext(analyzer);

        AnalysisCapabilityPreparationState state = await context.Service.PrepareAsync(CreateRequest());

        Assert.AreEqual(AnalysisCapabilityPreparationStatus.Failed, state.Status);
        Assert.AreEqual(AnalysisFailureCode.ModelNotReady, state.Failure?.Code);
        Assert.AreEqual(AnalysisFailureDisposition.Transient, state.Failure?.Disposition);
        Assert.AreEqual(0, context.WakeSignal.SignalCount);
    }

    private static TestContext CreateContext(
        StubPreparableAnalyzer analyzer,
        bool featureEnabled = true)
    {
        var featureAvailability = new StubFeatureAvailability(featureEnabled);
        var catalog = new CaptureAnalyzerCatalog([analyzer]);
        var resolver = new CaptureAnalyzerResolver(
            catalog,
            featureAvailability,
            new CaptureAnalyzerResolutionPreference([]));
        var wakeSignal = new RecordingWakeSignal();
        var jobStore = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        jobStore
            .Setup(store => store.ResumeWaitingForCapabilityAsync(
                It.IsAny<CapabilityDefinition>(),
                It.IsAny<ProcessingBoundary>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        IClock clock = Mock.Of<IClock>(value =>
            value.UtcNow == new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
        var service = new CaptureAnalysisCapabilityPreparationService(
            resolver,
            catalog,
            featureAvailability,
            jobStore.Object,
            wakeSignal,
            clock);
        return new(service, wakeSignal, jobStore);
    }

    private static AnalysisCapabilityPreparationRequest CreateRequest()
    {
        return new(
            AnalysisCapabilities.ImageDescriptionV1,
            CaptureMediaKind.Image,
            Purpose,
            AnalysisProcessingPolicy.LocalOnly(Purpose));
    }

    private sealed record TestContext(
        CaptureAnalysisCapabilityPreparationService Service,
        RecordingWakeSignal WakeSignal,
        Mock<ICaptureAnalysisJobStore> JobStore);

    private sealed class RecordingProgress : IProgress<AnalysisCapabilityPreparationProgress>
    {
        public List<double> Values { get; } = [];

        public void Report(AnalysisCapabilityPreparationProgress value)
        {
            Values.Add(value.FractionComplete);
        }
    }

    private sealed class RecordingWakeSignal : ICaptureAnalysisWakeSignal
    {
        public int SignalCount { get; private set; }

        public bool TrySignal()
        {
            SignalCount++;
            return true;
        }
    }

    private sealed class StubFeatureAvailability(bool enabled) : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled => enabled;

        public long ResolutionPolicyRevision => 1;

        public bool IsProviderEnabled(string providerId)
        {
            return enabled;
        }

        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer)
        {
            return enabled;
        }
    }

    private sealed class StubPreparableAnalyzer : IPreparableCaptureAnalyzer
    {
        public StubPreparableAnalyzer(CaptureAnalyzerAvailabilityStatus availabilityStatus)
        {
            AvailabilityStatus = availabilityStatus;
            Descriptor = new CaptureAnalyzerDescriptor(
                AnalysisCapabilities.ImageDescriptionV1,
                new AnalyzerIdentity(
                    "test-image-description",
                    "test-provider",
                    "test-model",
                    "1",
                    "1",
                    "test-runtime",
                    "1",
                    null,
                    null),
                [CaptureMediaKind.Image],
                ProcessingBoundary.OnDevice,
                CaptureAnalyzerDataKind.None,
                CaptureAnalyzerRequirement.ModelPackage |
                    CaptureAnalyzerRequirement.UserInitiatedPreparation,
                CaptureAnalyzerWorkloadClass.AiIntensive,
                maximumSourceBytes: null,
                qualityTier: 100);
        }

        public CaptureAnalyzerAvailabilityStatus AvailabilityStatus { get; set; }

        public Func<IProgress<AnalysisCapabilityPreparationProgress>?, CancellationToken,
            Task<CaptureAnalyzerPreparationResult>>? PrepareHandler { get; set; }

        public int AvailabilityCallCount { get; private set; }

        public int PrepareCallCount { get; private set; }

        public CaptureAnalyzerDescriptor Descriptor { get; }

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AvailabilityCallCount++;
            CaptureAnalyzerAvailability availability = AvailabilityStatus switch
            {
                CaptureAnalyzerAvailabilityStatus.Available => CaptureAnalyzerAvailability.Available,
                CaptureAnalyzerAvailabilityStatus.PreparationRequired =>
                    CaptureAnalyzerAvailability.PreparationRequired,
                CaptureAnalyzerAvailabilityStatus.Unsupported => CaptureAnalyzerAvailability.Unsupported(
                    new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                CaptureAnalyzerAvailabilityStatus.Disabled => CaptureAnalyzerAvailability.Disabled,
                _ => CaptureAnalyzerAvailability.TemporarilyUnavailable(new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)),
            };
            return ValueTask.FromResult(availability);
        }

        public Task<CaptureAnalyzerPreparationResult> PrepareAsync(
            IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            PrepareCallCount++;
            return PrepareHandler?.Invoke(progress, cancellationToken) ??
                Task.FromResult(CaptureAnalyzerPreparationResult.Succeeded);
        }

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
