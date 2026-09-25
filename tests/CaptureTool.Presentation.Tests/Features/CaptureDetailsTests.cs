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
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureDetailsTests
{
    [TestMethod]
    public async Task DraftNameSurvivesBackgroundTitleAndUserSaveWorksWithoutConsent()
    {
        var names = new Mock<ICaptureNamingService>();
        CaptureName? current = null;
        names.Setup(service => service.GetNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => current);
        names.Setup(service => service.SetNameAsync(It.IsAny<string>(), CaptureFileType.Image, "User draft", It.IsAny<CancellationToken>()))
            .Callback(() => current = new("User draft", false)).ReturnsAsync(true);
        var setup = new Setup(names.Object);
        setup.State = setup.State with { Policy = CaptureMemoryPolicy.Disabled() };
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png", AnalysisMediaKind.Image);
        vm.EditNameCommand.Execute(null);
        vm.NameDraft = "User draft";
        current = new("Background suggestion", true);
        names.Raise(service => service.Changed += null);
        Assert.AreEqual("Background suggestion", vm.FileName);
        Assert.AreEqual("User draft", vm.NameDraft);
        await vm.SaveNameCommand.ExecuteAsync(null);
        Assert.AreEqual("User draft", vm.FileName);
        Assert.IsFalse(vm.IsEditingName);
        Assert.AreEqual("capture.png", vm.PhysicalFileName);
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
        Assert.IsFalse(content.HasRetainedResults);
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
    public void RetainedAndLimitedOutputsAreExplicit()
    {
        var record = Record(limited: true);
        var content = CaptureDetailsContent.Create(record.StartRun(record.SourceRevision, "next", Guid.NewGuid()), Localization());
        Assert.IsTrue(content.HasLimitedCoverage);
        Assert.IsTrue(content.HasRetainedResults);
    }

    [TestMethod]
    public void LegacyResultsWithUnknownRunIdentityAreNotCalledStale()
    {
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "plan", Guid.NewGuid(),
            [new(new TextRecognitionMetadata([new("Legacy source")]), new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "plan")]);
        Assert.IsFalse(CaptureDetailsContent.Create(record, Localization()).HasRetainedResults);
    }

    [TestMethod]
    public async Task CopyUsesExactTextAndReportsClipboardFailureWithoutLosingContent()
    {
        var setup = new Setup();
        using var vm = setup.ViewModel;
        await vm.OpenAsync("capture.png");
        await vm.CopyCommand.ExecuteAsync("USD 125.00");
        setup.Clipboard.Verify(clipboard => clipboard.CopyTextAsync("USD 125.00"), Times.Once);
        Assert.AreEqual("Copied", vm.CopyStatus);
        setup.Clipboard.Setup(clipboard => clipboard.CopyTextAsync(It.IsAny<string>())).ThrowsAsync(new IOException());
        await vm.CopyCommand.ExecuteAsync("text");
        Assert.AreEqual("CopyFailed", vm.CopyStatus);
        Assert.IsTrue(vm.Content.HasContent);
        setup.Memory.Verify(memory => memory.ScanExistingAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Unavailable", vm.StatusText);
        await vm.RefreshCommand.ExecuteAsync(null);
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
            ViewModel = new(Reader.Object, Memory.Object, Clipboard.Object, Localization(), ui.Object, names: names);
        }
    }
}
