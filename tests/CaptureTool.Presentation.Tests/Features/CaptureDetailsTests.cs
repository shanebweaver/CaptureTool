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
    public async Task TextShortcutReusesMatchingSavedResultsWithoutRenderingOrRequestingAnalysis()
    {
        var setup = new Setup();
        int calls = 0;
        var editor = new CaptureEditorTextSession(() => { calls++; return Task.CompletedTask; }, () => { });
        editor.SetAvailability(true);
        using var vm = setup.ViewModel;
        vm.SetEditorText(editor);
        vm.SetNavigationContext(new(true, true));
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await vm.EnsureTextAsync();
        Assert.AreEqual(0, calls);
        Assert.IsTrue(vm.CanShowImageTextOverlay);
        Assert.AreEqual("Invoice total USD 125.00", vm.TextContent.CopyVisibleScope());
        setup.Memory.Verify(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<AnalysisCapability>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task EditedImageUsesSessionTextAndNeverPublishesItAsSavedCaptureAnalysis()
    {
        var setup = new Setup();
        var complete = new TaskCompletionSource();
        CaptureEditorTextSession? editor = null;
        editor = new(async () =>
        {
            await complete.Task;
            editor!.SetDocument(new("Current image", new(200, 100), [new("Current image", new(20, 10, 100, 20))], []));
        }, () => { });
        editor.Invalidate(true);
        editor.SetAvailability(true);
        using var vm = setup.ViewModel;
        vm.SetEditorText(editor);
        vm.SetNavigationContext(new(true, true, ImageEdited: true));
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.IsEmpty(vm.TextPassages);
        Assert.IsFalse(vm.CanShowImageTextOverlay);
        Assert.IsFalse(editor.IsRunning, "Opening the pane does not start extraction.");
        Task extraction = vm.EnsureTextAsync();
        Assert.IsTrue(vm.TextAction.IsRunning);
        Assert.IsFalse(vm.TextAction.Command.CanExecute(null));
        complete.SetResult();
        await extraction;
        Assert.IsTrue(vm.CanShowImageTextOverlay);
        Assert.AreEqual("Current image", vm.TextContent.CopyVisibleScope());
        Assert.IsTrue(vm.TextPassages.Single().CanNavigate);
        var bounds = vm.TextPassages.Single().SelectedLocation!.Bounds!;
        Assert.AreEqual(.1, bounds.X, .000001);
        Assert.AreEqual(.1, bounds.Y, .000001);
        Assert.AreEqual(.5, bounds.Width, .000001);
        Assert.AreEqual(.2, bounds.Height, .000001);
        Assert.AreEqual("Invoice total USD 125.00", vm.Content.Passages.Single().Text);
        editor.Invalidate(true);
        Assert.IsEmpty(vm.TextPassages);
        Assert.IsFalse(vm.CanShowImageTextOverlay);
        setup.Memory.Verify(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<AnalysisCapability>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SessionEmptyResultsSurviveReopeningWithoutRepeatedExtraction()
    {
        int calls = 0;
        CaptureEditorTextSession? editor = null;
        editor = new(() => { calls++; editor!.SetDocument(new(string.Empty, new(100, 50), [], [])); return Task.CompletedTask; }, () => { });
        editor.Invalidate(true);
        editor.SetAvailability(true);
        foreach (int _ in Enumerable.Range(0, 2))
        {
            var setup = new Setup();
            using var vm = setup.ViewModel;
            vm.SetEditorText(editor);
            await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
            await vm.EnsureTextAsync();
            Assert.AreEqual("CaptureAction_NoText", vm.TextAction.Status);
            Assert.IsFalse(vm.TextAction.Command.CanExecute(null));
            setup.Notifications.Verify(x => x.ShowInfo("CaptureAction_NoText"), _ == 0 ? Times.Once() : Times.Never());
        }
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task MismatchedWorkingImageUsesCurrentImageEvenWithSavedText()
    {
        var setup = new Setup();
        int calls = 0;
        var editor = new CaptureEditorTextSession(() => { calls++; return Task.CompletedTask; }, () => { });
        editor.SetAvailability(true);
        using var vm = setup.ViewModel;
        vm.SetEditorText(editor);
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image, "different-working.png");
        await vm.EnsureTextAsync();
        Assert.AreEqual(1, calls);
        Assert.IsEmpty(vm.TextPassages);
    }

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
        setup.Notifications.Verify(x => x.ShowInfo(It.IsAny<string>()), Times.Never, "Opening cached empty results should stay quiet.");
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
        Assert.IsFalse(vm.IsGeneratingName);
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
    public async Task NameAndSummaryHaveIndependentRequestsLoadingResultsAndFailures()
    {
        var setup = new Setup();
        CaptureDetailsSnapshot snapshot = new(CaptureDetailsStatus.Empty);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshot);
        setup.Memory.Setup(memory => memory.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await vm.NameAction.Command.ExecuteAsync(null);
        setup.Memory.Verify(memory => memory.AnalyzeAsync("capture.png", AnalysisCapability.CaptureName, It.IsAny<CancellationToken>()), Times.Once);
        setup.Memory.Verify(memory => memory.AnalyzeAsync(It.IsAny<string>(), AnalysisCapability.CaptureSynopsis, It.IsAny<CancellationToken>()), Times.Never);
        var run = new AnalysisRun(Guid.NewGuid(), Guid.NewGuid(), 1, "test", null, AnalysisRunStatus.Queued,
            [AnalysisCapability.TextRecognition, AnalysisCapability.Description, AnalysisCapability.CaptureName], []);
        snapshot = snapshot with { Run = run };
        await vm.RefreshAsync();
        Assert.IsTrue(vm.IsGeneratingName);
        Assert.IsFalse(vm.IsGeneratingSummary);
        snapshot = snapshot with { Run = run.BindSource(new(new string('a', 64)))
            .CompleteStep(new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Unsupported, "model-unavailable"))
            .CompleteStep(new(AnalysisCapability.Description, AnalyzerOutcomeKind.Unsupported, "model-unavailable"))
            .CompleteStep(new(AnalysisCapability.CaptureName, AnalyzerOutcomeKind.Failed, "model-failed")) };
        await vm.RefreshAsync();
        Assert.IsFalse(vm.IsGeneratingName);
        Assert.AreEqual("CaptureAction_Failed", vm.NameAction.Status);
        Assert.AreEqual(string.Empty, vm.SummaryAction.Status);

        var source = Result(new TextRecognitionMetadata([new("Invoice")]), run.Id);
        var coverage = new MetadataProcessingCoverage(1, 1, 7, MetadataProcessingLimit.None);
        var name = Result(new CaptureNameMetadata(new("Invoice name", [new(source.ResultId, 0, 0, 7)]), coverage), run.Id,
            new([new(source.Payload.Capability, source.ResultId)]));
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "plan", run.Id, [source, name]);
        snapshot = new(CaptureDetailsStatus.Available, record);
        await vm.RefreshAsync();
        Assert.IsTrue(vm.NameAction.HasResult);
        Assert.IsFalse(vm.NameAction.Command.CanExecute(null));
        Assert.IsFalse(vm.HasSummary);
        Assert.IsTrue(vm.SummaryAction.Command.CanExecute(null));
        await vm.SummaryAction.Command.ExecuteAsync(null);
        setup.Memory.Verify(memory => memory.AnalyzeAsync("capture.png", AnalysisCapability.CaptureSynopsis, It.IsAny<CancellationToken>()), Times.Once);
        var summary = Result(new CaptureSynopsisMetadata(null, [new("Invoice summary", [new(source.ResultId, 0, 0, 7)])], coverage), run.Id,
            new([new(source.Payload.Capability, source.ResultId)]));
        snapshot = snapshot with { Record = new(record.CaptureId, record.MediaKind, record.SourceRevision, record.PlanVersion, record.RunId, [source, summary]) };
        await vm.RefreshAsync();
        Assert.IsTrue(vm.HasSummary);
        Assert.IsFalse(vm.SummaryAction.Command.CanExecute(null));
        Assert.IsTrue(vm.NameAction.Command.CanExecute(null));
        Assert.IsFalse(vm.NameAction.HasResult);
    }

    [TestMethod]
    public async Task PreviouslySavedCombinedTitleRemainsAvailableWithoutNewInference()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.AreEqual("Invoice summary", vm.Content.SuggestedName);
        Assert.IsTrue(vm.NameAction.HasResult);
        Assert.IsFalse(vm.NameAction.Command.CanExecute(null));
        setup.Memory.Verify(memory => memory.AnalyzeAsync(It.IsAny<string>(), It.IsAny<AnalysisCapability>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task CachedNameSuggestionOpensEditableDraftOnlyOnRequestAndCanBeReopenedAfterCancel()
    {
        var names = new Mock<ICaptureNamingService>();
        var setup = new Setup(names.Object);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        Assert.IsFalse(vm.IsEditingName);
        Assert.IsTrue(vm.SuggestNameCommand.CanExecute(null));
        await vm.SuggestNameCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsEditingName);
        Assert.AreEqual("Invoice summary", vm.NameDraft);
        Assert.AreEqual("capture.png", vm.FileName);
        vm.CancelNameCommand.Execute(null);
        Assert.IsFalse(vm.IsEditingName);
        await vm.SuggestNameCommand.ExecuteAsync(null);
        Assert.AreEqual("Invoice summary", vm.NameDraft);
        names.Verify(service => service.RenameAsync(It.IsAny<string>(), It.IsAny<CaptureFileType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        setup.Memory.Verify(memory => memory.AnalyzeAsync(It.IsAny<string>(), It.IsAny<AnalysisCapability>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RequestedNameSuggestionOpensDraftUnlessUserStartedEditing(bool editWhileGenerating)
    {
        var setup = new Setup(Mock.Of<ICaptureNamingService>());
        CaptureDetailsSnapshot snapshot = new(CaptureDetailsStatus.Empty);
        var run = new AnalysisRun(Guid.NewGuid(), Guid.NewGuid(), 1, "test", null, AnalysisRunStatus.Queued,
            [AnalysisCapability.CaptureName], []);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshot);
        setup.Memory.Setup(memory => memory.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        setup.Memory.Setup(memory => memory.AnalyzeAsync("capture.png", AnalysisCapability.CaptureName, It.IsAny<CancellationToken>()))
            .Callback(() => snapshot = snapshot with { Run = run }).Returns(Task.CompletedTask);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await vm.SuggestNameCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsGeneratingName);
        Assert.IsFalse(vm.SuggestNameCommand.CanExecute(null));
        Assert.IsFalse(vm.IsEditingName);
        Assert.AreEqual("CaptureNaming_Suggest", vm.NameSuggestionToolTip, "The AI tooltip stays unchanged during loading.");
        if (editWhileGenerating)
        {
            vm.EditNameCommand.Execute(null);
            vm.NameDraft = "My own name";
        }
        snapshot = new(CaptureDetailsStatus.Available, Record());
        await vm.RefreshAsync();
        Assert.IsFalse(vm.IsGeneratingName);
        Assert.IsTrue(vm.IsEditingName);
        Assert.AreEqual(editWhileGenerating ? "My own name" : "Invoice summary", vm.NameDraft);
        Assert.AreEqual("capture.png", vm.FileName);
    }

    [TestMethod]
    public async Task SuggestedNameOpensAfterAnAlreadyRunningRefreshCompletes()
    {
        var setup = new Setup(Mock.Of<ICaptureNamingService>());
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        var result = new TaskCompletionSource<CaptureDetailsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(result.Task);
        setup.Memory.Setup(memory => memory.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Task refresh = Task.CompletedTask;
        setup.Memory.Setup(memory => memory.AnalyzeAsync("capture.png", AnalysisCapability.CaptureName, It.IsAny<CancellationToken>()))
            .Callback(() => refresh = vm.RefreshAsync()).Returns(Task.CompletedTask);
        await vm.SuggestNameCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsReading);
        result.SetResult(new(CaptureDetailsStatus.Available, Record()));
        await refresh;
        Assert.IsTrue(vm.IsEditingName);
        Assert.AreEqual("Invoice summary", vm.NameDraft);
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
        Assert.IsFalse(vm.IsReadingDetails);
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
        setup.Notifications.VerifyNoOtherCalls();
        await vm.RefreshAsync();
        setup.Notifications.Verify(x => x.ShowError("Unavailable"), Times.Once);
        await vm.RefreshAsync();
        Assert.IsFalse(vm.IsReadingDetails);
        Assert.IsTrue(vm.Content.HasContent);
    }

    [TestMethod]
    public async Task CompletedEmptyResultsNotifyOnceAndLoadingKeepsItsLabel()
    {
        var setup = new Setup();
        var run = new AnalysisRun(Guid.NewGuid(), Guid.NewGuid(), 1, "test", null, AnalysisRunStatus.Queued,
            [AnalysisCapability.TextRecognition], []);
        CaptureDetailsSnapshot snapshot = new(CaptureDetailsStatus.Empty, Run: run);
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshot);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        string label = vm.TextAction.Label;
        Assert.IsTrue(vm.TextAction.IsRunning);
        setup.Notifications.VerifyNoOtherCalls();
        snapshot = new(CaptureDetailsStatus.Available, new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image,
            new(new string('a', 64)), "plan", run.Id, [Result(new TextRecognitionMetadata([]), run.Id)]));
        await vm.RefreshAsync();
        await vm.RefreshAsync();
        Assert.IsFalse(vm.TextAction.IsRunning);
        Assert.AreEqual(label, vm.TextAction.Label);
        setup.Notifications.Verify(x => x.ShowInfo("CaptureAction_NoText"), Times.Once);
    }

    [TestMethod]
    public async Task RepeatedReadFailuresNotifyOnceUntilRecoveryAndStayQuietWhenHidden()
    {
        var setup = new Setup();
        CaptureDetailsSnapshot snapshot = new(CaptureDetailsStatus.Unavailable);
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshot);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png");
        await vm.RefreshAsync();
        setup.Notifications.Verify(x => x.ShowError("Unavailable"), Times.Once);
        snapshot = new(CaptureDetailsStatus.Empty);
        await vm.RefreshAsync();
        vm.IsActive = false;
        snapshot = new(CaptureDetailsStatus.Unavailable);
        await vm.RefreshAsync();
        setup.Notifications.Verify(x => x.ShowError("Unavailable"), Times.Once);
    }

    [TestMethod]
    public async Task FailedRequestsNotifyOncePerAttemptAndRemainRetryable()
    {
        var setup = new Setup();
        setup.Reader.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Empty));
        setup.Memory.Setup(x => x.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        setup.Memory.Setup(x => x.AnalyzeAsync(It.IsAny<string>(), AnalysisCapability.TextRecognition, It.IsAny<CancellationToken>()))
            .Callback(() => setup.State = setup.State with { FailureCode = "model-failed" }).Returns(Task.CompletedTask);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        await vm.TextAction.Command.ExecuteAsync(null);
        await vm.RefreshAsync();
        setup.Notifications.Verify(x => x.ShowError("CaptureAction_Failed"), Times.Once);
        Assert.IsTrue(vm.TextAction.Command.CanExecute(null));
        await vm.TextAction.Command.ExecuteAsync(null);
        setup.Notifications.Verify(x => x.ShowError("CaptureAction_Failed"), Times.Exactly(2));
    }

    [TestMethod]
    public async Task RenameValidationAndFilesystemFailuresUseSnackbarsAndKeepTheDraft()
    {
        var names = new Mock<ICaptureNamingService>();
        var setup = new Setup(names.Object);
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        vm.EditNameCommand.Execute(null);
        vm.NameDraft = string.Empty;
        await vm.SaveNameCommand.ExecuteAsync(null);
        setup.Notifications.Verify(x => x.ShowError("CaptureNaming_Invalid"), Times.Once);
        Assert.IsTrue(vm.IsEditingName);
        names.Setup(x => x.RenameAsync(It.IsAny<string>(), It.IsAny<CaptureFileType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException());
        vm.NameDraft = "New name";
        await vm.SaveNameCommand.ExecuteAsync(null);
        setup.Notifications.Verify(x => x.ShowError("CaptureNaming_SaveFailed"), Times.Once);
        Assert.AreEqual("New name", vm.NameDraft);
        Assert.IsTrue(vm.IsEditingName);
        Assert.AreEqual("capture.png", vm.FileName);
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
            Reader.Setup(reader => reader.ReadFileAsync(It.IsAny<string>(), It.IsAny<AnalysisMediaKind>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileDetailsMetadata(AnalysisMediaKind.Image, "capture.png", 100, "image/png", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            Memory.SetupGet(memory => memory.State).Returns(() => State);
            var ui = new Mock<ITaskEnvironment>();
            ui.Setup(ui => ui.TryExecute(It.IsAny<Action>())).Returns((Action action) => { action(); return true; });
            ViewModel = new(Reader.Object, Memory.Object, Clipboard.Object, Localization(), ui.Object, Notifications.Object, names: names);
        }
    }
}
