using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Features.CaptureDetails;
using CaptureTool.Presentation.Notifications;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureDetailsTests
{
    [TestMethod]
    public async Task UnavailableModelShowsDeviceAvailabilityWithoutImplyingUnreadableDetails()
    {
        var setup = new Setup();
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        setup.Memory.Setup(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        setup.Memory.Setup(x => x.AnalyzeAsync(It.IsAny<string>(), AnalysisCapability.CaptureSynopsis, It.IsAny<CancellationToken>()))
            .Callback(() => setup.State = setup.State with { FailureCode = "model-unavailable" }).Returns(Task.CompletedTask);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await vm.SummaryAction.Command.ExecuteAsync(null);
        Assert.AreEqual("CaptureAction_Unavailable", vm.SummaryAction.Status);
        Assert.IsFalse(vm.IsGeneratingSummary);
        Assert.AreEqual(string.Empty, vm.TextAction.Status);
        setup.Notifications.Verify(x => x.ShowInfo("CaptureAction_Unavailable"), Times.Once);
        setup.Notifications.Verify(x => x.ShowError(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PendingConsentDisablesOtherActionsAndCannotQueueExtraWork()
    {
        var setup = new Setup();
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        var consent = new TaskCompletionSource<bool>();
        setup.Memory.Setup(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>())).Returns(consent.Task);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Task requested = vm.AltTextAction.Command.ExecuteAsync(null);
        Assert.IsFalse(vm.TextAction.Command.CanExecute(null));
        await vm.TextAction.Command.ExecuteAsync(null);
        consent.SetResult(true);
        await requested;
        setup.Memory.Verify(x => x.AnalyzeAsync("capture.png", AnalysisCapability.ImageAltText, It.IsAny<CancellationToken>()), Times.Once);
        setup.Memory.Verify(x => x.AnalyzeAsync("capture.png", AnalysisCapability.TextRecognition, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task EmptySavedResultIsExplainedWithoutEnablingRepeatedInference()
    {
        var setup = new Setup();
        Guid run = Guid.NewGuid();
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "plan", run,
            [Result(new TextRecognitionMetadata([]), run)]);
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, record));
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.IsFalse(vm.TextAction.Command.CanExecute(null));
        Assert.AreEqual("CaptureAction_NoText", vm.TextAction.Status);
        Assert.IsTrue(vm.QrAction.Command.CanExecute(null));
        Assert.IsTrue(vm.SummaryAction.Command.CanExecute(null));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExplicitActionRequestsOnlyItsCapabilityAfterConsent(bool accepted)
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(new CaptureMemoryState(CaptureMemoryPolicy.Disabled(), true, new(false, true), new(AnalysisActivity.Idle)));
        var onboarding = new Mock<ICaptureAnalysisOnboarding>();
        onboarding.Setup(x => x.EnableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accepted);
        var reader = new Mock<ICaptureDetailsReader>();
        reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        using var vm = new CaptureDetailsViewModel(reader.Object, memory.Object, Mock.Of<IClipboardService>(),
            Localization(), Mock.Of<ITaskEnvironment>(), Mock.Of<IAppNotificationService>(), onboarding: onboarding.Object);
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.IsTrue(vm.AltTextAction.Command.CanExecute(null));
        Assert.IsTrue(vm.SummaryAction.Command.CanExecute(null));
        Assert.IsTrue(vm.NameAction.Command.CanExecute(null));
        onboarding.VerifyNoOtherCalls();
        await vm.AltTextAction.Command.ExecuteAsync(null);
        onboarding.Verify(x => x.EnableAsync(It.IsAny<CancellationToken>()), Times.Once);
        memory.Verify(x => x.AnalyzeAsync("capture.png", AnalysisCapability.ImageAltText, It.IsAny<CancellationToken>()), accepted ? Times.Once() : Times.Never());
        memory.Verify(x => x.AnalyzeAsync(It.IsAny<string>(), It.Is<AnalysisCapability>(c => c != AnalysisCapability.ImageAltText), It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    public async Task SummaryLoadingIsPerCaptureAndStopsOnCompletionOrRevocation()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        var run = new AnalysisRun(Guid.NewGuid(), Guid.NewGuid(), 1, "test", null, AnalysisRunStatus.Queued,
            [AnalysisCapability.CaptureSynopsis], []);
        CaptureDetailsSnapshot snapshot = new(CaptureDetailsStatus.Empty, Run: run);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshot);
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.IsTrue(vm.IsGeneratingSummary);
        snapshot = snapshot with { Run = run.Finish(AnalysisRunStatus.Failed) };
        await vm.RefreshAsync();
        Assert.IsFalse(vm.IsGeneratingSummary);
        snapshot = snapshot with { Run = run };
        await vm.RefreshAsync();
        Assert.IsTrue(vm.IsGeneratingSummary);
        setup.State = setup.State with { Policy = CaptureMemoryPolicy.Disabled() };
        setup.Memory.Raise(memory => memory.StateChanged += null);
        Assert.IsFalse(vm.IsGeneratingSummary);
    }

    [TestMethod]
    public void MetadataOverlayProjectsTextAndQrBoundsWithoutRunningOcr()
    {
        var bounds = new NormalizedBounds(.1, .2, .3, .4);
        CaptureTextPassage[] passages =
        [
            new("text", CaptureTextSource.ImageText, "text", "Invoice", [new("text", null, bounds)]),
            new("qr", CaptureTextSource.QrCode, "QR", "https://example.com", [new("QR", null, bounds)]),
            new("speech", CaptureTextSource.Speech, "speech", "Spoken words", [new("0:01", TimeSpan.FromSeconds(1), null)])
        ];
        var overlay = CaptureImageTextOverlay.Create(passages, new(1000, 500));
        Assert.AreEqual(new System.Drawing.RectangleF(100, 100, 300, 200), overlay.Text.Single().Bounds);
        Assert.AreEqual("Invoice", overlay.Text.Single().Text);
        Assert.AreEqual("https://example.com", overlay.QrCodes.Single().Value);
        Assert.IsEmpty(CaptureImageTextOverlay.Create(passages, System.Drawing.Size.Empty).Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PassivePaneOpeningNeverPromptsForConsent(bool consent)
    {
        var memory = new Mock<ICaptureMemoryService>();
        memory.SetupGet(x => x.State).Returns(new CaptureMemoryState(new(false, consent, Guid.NewGuid(), 0), true, new(false, true), new(AnalysisActivity.Idle)));
        var onboarding = new Mock<ICaptureAnalysisOnboarding>();
        onboarding.Setup(x => x.EnableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var text = new Mock<ILocalizationService>();
        text.Setup(x => x.GetString(It.IsAny<string>())).Returns<string>(key => key);
        using var vm = new CaptureDetailsViewModel(Mock.Of<ICaptureDetailsReader>(), memory.Object, Mock.Of<IClipboardService>(),
            text.Object, Mock.Of<ITaskEnvironment>(), Mock.Of<IAppNotificationService>(), onboarding: onboarding.Object);
        onboarding.VerifyNoOtherCalls();
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        onboarding.Verify(x => x.EnableAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    public async Task DraftNameSurvivesRequestedTitleAndUserSaveWorksWithoutConsent()
    {
        var names = new Mock<ICaptureNamingService>();
        CaptureName? current = null;
        names.Setup(service => service.GetStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new CaptureNamingState(current, false));
        names.Setup(service => service.RenameAsync(It.IsAny<string>(), CaptureFileType.Image, "User draft", It.IsAny<CancellationToken>()))
            .Callback(() => current = new("User draft", false)).ReturnsAsync(new CaptureFileRename("capture.png", "User draft.png"));
        var setup = new Setup(names.Object);
        setup.State = setup.State with { Policy = CaptureMemoryPolicy.Disabled() };
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        vm.EditNameCommand.Execute(null);
        vm.NameDraft = "User draft";
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, Record()));
        await vm.RefreshAsync();
        Assert.AreEqual("capture.png", vm.FileName);
        Assert.AreEqual("Invoice summary", vm.SuggestedName);
        Assert.AreEqual("User draft", vm.NameDraft);
        await vm.SaveNameCommand.ExecuteAsync(null);
        Assert.AreEqual("User draft.png", vm.FileName);
        Assert.IsFalse(vm.IsEditingName);
        Assert.AreEqual("User draft.png", vm.PhysicalFileName);
    }

    [TestMethod]
    public async Task LocalPropertiesAppearBeforeSlowAnalysisAndSurviveDeletion()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        var delayed = new TaskCompletionSource<CaptureDetailsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(delayed.Task);
        var date = DateTimeOffset.UtcNow;
        setup.Reader.Setup(reader => reader.ReadFileAsync(It.IsAny<string>(), AnalysisMediaKind.Image, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileDetailsMetadata(AnalysisMediaKind.Image, "capture.png", 2048, "image/png", date, date,
                image: new(new(1200, 800))));
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.FileProperties)) ready.TrySetResult(); };
        Task opening = vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("1200 × 800 · 2 KiB", vm.FileProperties.Compact);
        Assert.IsTrue(vm.IsReading);
        setup.State = setup.State with { IsDeleting = true };
        setup.Memory.Raise(memory => memory.StateChanged += null);
        delayed.SetResult(new(CaptureDetailsStatus.Available, Record()));
        await opening;
        Assert.AreEqual("1200 × 800 · 2 KiB", vm.FileProperties.Compact);
        Assert.IsFalse(vm.HasSummary);
        vm.CopyPathCommand.Execute(null);
        setup.Clipboard.Verify(clipboard => clipboard.CopyTextAsync("capture.png"), Times.Once);
    }

    [TestMethod]
    public async Task ClosedPaneDiscardsLateLocalProperties()
    {
        var setup = new Setup();
        var delayed = new TaskCompletionSource<FileDetailsMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reader.Setup(reader => reader.ReadFileAsync(It.IsAny<string>(), It.IsAny<AnalysisMediaKind>(), It.IsAny<CancellationToken>()))
            .Returns(() => { started.TrySetResult(); return delayed.Task; });
        Task opening = setup.ViewModel.OpenAsync("capture.wav", AnalysisMediaKind.Audio);
        await started.Task;
        setup.ViewModel.Dispose();
        var date = DateTimeOffset.UtcNow;
        delayed.SetResult(new(AnalysisMediaKind.Audio, "capture.wav", 12, null, date, date));
        await opening;
        Assert.IsEmpty(setup.ViewModel.FileProperties.Basic);
    }

    [TestMethod]
    public async Task WorkingCopyMismatchDisablesLocationsWithoutDiscardingSourceText()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "test", Guid.NewGuid(),
            [Result(new TextRecognitionMetadata([new("Source text", new(.1, .1, .2, .2))]), Guid.NewGuid())]);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, record));
        vm.SetNavigationContext(new(true, true));
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image, "working.png");
        Assert.AreEqual("Source text", vm.TextContent.CopyVisibleScope());
        Assert.IsFalse(vm.TextContent.Visible.Single().CanNavigate);
        setup.Reader.Setup(reader => reader.VerifySourceAsync("working.png", record.SourceRevision, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await vm.RefreshAsync();
        Assert.IsTrue(vm.TextContent.Visible.Single().CanNavigate);
        setup.State = setup.State with { IsDeleting = true };
        setup.Memory.Raise(memory => memory.StateChanged += null);
        Assert.AreEqual(string.Empty, vm.TextContent.CopyVisibleScope());
    }

    [TestMethod]
    public void PaneOnlyProjectsActionableSourceTextAndSummary()
    {
        var content = CaptureDetailsContent.Create(Record(), Localization());
        Assert.AreEqual("Invoice total USD 125.00", content.Passages.Single().Text);
        Assert.AreEqual(string.Empty, content.Summary); // The fixture has a suggested title, not a summary.
    }

    [TestMethod]
    public void FilePropertiesKeepLongDurationsAndUnknownCaptureTime()
    {
        var date = DateTimeOffset.UtcNow;
        var content = CaptureFileProperties.Create(new(AnalysisMediaKind.Audio, "recording.wav", 2048, "audio/wav", date, date,
            duration: TimeSpan.FromHours(27), audio: new(channels: 2, sampleRate: 48000)), Localization());
        Assert.AreEqual("Unknown", content.Basic.Single(item => item.Label == "CapturedAt").Value);
        Assert.IsFalse(content.Basic.Any(item => item.Label is "Dimensions" or "AspectRatio"));
        Assert.AreEqual("27:00:00", content.Basic.Single(item => item.Label == "Duration").Value);
    }

    [TestMethod]
    public void SavedOutputsRetainTheirCoverageAcrossIndependentActions()
    {
        var record = Record(limited: true);
        var content = CaptureDetailsContent.Create(record.StartRun(record.SourceRevision, "next", Guid.NewGuid()), Localization());
        Assert.IsTrue(content.HasLimitedCoverage);
    }

    [TestMethod]
    public void LegacyResultsWithUnknownRunIdentityAreNotCalledStale()
    {
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "plan", Guid.NewGuid(),
            [new(new TextRecognitionMetadata([new("Legacy source")]), new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "plan")]);
        Assert.AreEqual("Legacy source", CaptureDetailsContent.Create(record, Localization()).Passages.Single().Text);
    }

    [TestMethod]
    public async Task CopyUsesExactTextAndReportsClipboardFailureWithoutLosingContent()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png");
        await vm.CopyCommand.ExecuteAsync("USD 125.00");
        setup.Clipboard.Verify(clipboard => clipboard.CopyTextAsync("USD 125.00"), Times.Once);
        setup.Notifications.Verify(notifications => notifications.ShowInfo("Copied"), Times.Once);
        setup.Clipboard.Setup(clipboard => clipboard.CopyTextAsync(It.IsAny<string>())).ThrowsAsync(new IOException());
        await vm.CopyCommand.ExecuteAsync("text");
        setup.Notifications.Verify(notifications => notifications.ShowError("CopyFailed"), Times.Once);
        Assert.IsTrue(vm.Content.HasContent);
        setup.Memory.Verify(memory => memory.AnalyzeAsync(It.IsAny<string>(), It.IsAny<AnalysisCapability>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ClosingBeforeReadCompletesNeverRestoresPrivateContent()
    {
        var setup = new Setup();
        var ready = new TaskCompletionSource<CaptureDetailsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => { started.SetResult(); return ready.Task; });
        Task loading = setup.ViewModel.OpenAsync("capture.png");
        await started.Task;
        setup.ViewModel.Dispose();
        ready.SetResult(new(CaptureDetailsStatus.Available, Record()));
        await loading;
        Assert.IsFalse(setup.ViewModel.Content.HasContent);
    }

    [TestMethod]
    public async Task DeletionDuringReadDiscardsLateSnapshot()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        var ready = new TaskCompletionSource<CaptureDetailsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => { started.TrySetResult(); return ready.Task; });
        Task loading = vm.OpenAsync("capture.png");
        await started.Task;
        setup.State = setup.State with { IsDeleting = true };
        setup.Memory.Raise(memory => memory.StateChanged += null);
        ready.SetResult(new(CaptureDetailsStatus.Available, Record()));
        await loading;
        Assert.IsFalse(vm.Content.HasContent);
        Assert.AreEqual("Deleting", vm.StatusText);
        await vm.CopyCommand.ExecuteAsync("old value");
        setup.Clipboard.Verify(clipboard => clipboard.CopyTextAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task EmptyAndFailedReadsAreDistinctAndRefreshCanRecover()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        setup.Reader.SetupSequence(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Unavailable))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, Record()));
        await vm.OpenAsync("capture.png");
        Assert.AreEqual("Empty", vm.StatusText);
        await vm.RefreshAsync();
        Assert.AreEqual("Unavailable", vm.StatusText);
        await vm.RefreshAsync();
        Assert.IsFalse(vm.ShowStatus);
        Assert.IsTrue(vm.Content.HasContent);
    }

    private static CaptureAnalysisRecord Record(bool limited = false)
    {
        Guid run = Guid.NewGuid();
        AnalysisResult source = Result(new TextRecognitionMetadata(limited
            ? [new("Invoice total USD 125.00"), new(new string('x', 2000))]
            : [new("Invoice total USD 125.00")]), run);
        var inputs = new AnalysisDerivation([new(source.Payload.Capability, source.ResultId)]);
        var fact = new StructuredFact(StructuredFactKind.CurrencyAmount, "USD 125.00", [new(source.ResultId, 0, 14, 10)]);
        var coverage = new MetadataProcessingCoverage(limited ? 2 : 1, 1, 24,
            limited ? MetadataProcessingLimit.OversizedEntry : MetadataProcessingLimit.None);
        return new(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "plan", run,
            [source, Result(new StructuredFactsMetadata([fact]), run, inputs),
            Result(new CaptureSynopsisMetadata(new("Invoice summary", [new(source.ResultId, 0, 0, 24)]), [], coverage), run, inputs)]);
    }
    private static AnalysisResult Result(AnalysisPayload payload, Guid run, AnalysisDerivation? inputs = null) =>
        new(payload, new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "plan", run, derivation: inputs);
    private static ILocalizationService Localization()
    {
        var text = new Mock<ILocalizationService>();
        text.Setup(text => text.GetString(It.IsAny<string>())).Returns((string key) => key.Replace("CaptureDetails_", ""));
        return text.Object;
    }
    private sealed class Setup
    {
        public Mock<ICaptureDetailsReader> Reader { get; } = new();
        public Mock<ICaptureMemoryService> Memory { get; } = new();
        public Mock<IClipboardService> Clipboard { get; } = new();
        public Mock<IAppNotificationService> Notifications { get; } = new();
        public CaptureMemoryState State { get; set; } = new(new(true, true, Guid.NewGuid(), 0), true,
            new(true, true), new(AnalysisActivity.Idle));
        public CaptureDetailsViewModel ViewModel { get; }
        public Setup(ICaptureNamingService? names = null)
        {
            Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, Record()));
            Memory.SetupGet(memory => memory.State).Returns(() => State);
            var ui = new Mock<ITaskEnvironment>();
            ui.Setup(ui => ui.TryExecute(It.IsAny<Action>())).Returns((Action action) => { action(); return true; });
            ViewModel = new(Reader.Object, Memory.Object, Clipboard.Object, Localization(), ui.Object, Notifications.Object, names: names);
        }
    }
}
