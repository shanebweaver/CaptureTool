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
    public void FactsShowSourceContextAndKeepSuggestionsSeparate()
    {
        var record = Record();
        var content = CaptureDetailsContent.Create(record, Localization());
        Assert.AreEqual("Invoice summary", content.Overview.Single().Text);
        var amount = content.Facts.Single();
        Assert.AreEqual("USD 125.00", amount.Text);
        Assert.AreEqual("Invoice total USD 125.00", amount.Evidence.Single().Text);
        Assert.IsFalse(content.HasRetainedResults);
        Assert.Contains("Invoice total", content.Sources.Single().CopyText);
    }

    [TestMethod]
    public void AudioUsesTranscriptTimesAndNeverInventsCaptureDateOrImageProperties()
    {
        Guid run = Guid.NewGuid();
        var date = DateTimeOffset.UtcNow;
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Audio, new(new string('a', 64)), "plan", run,
        [Result(new TranscriptMetadata("en", [new("A long recording", TimeSpan.FromHours(27), TimeSpan.FromHours(27) + TimeSpan.FromSeconds(2))]), run),
         Result(new FileDetailsMetadata(AnalysisMediaKind.Audio, "recording.wav", 2048, "audio/wav", date, date,
             duration: TimeSpan.FromHours(27), audio: new(channels: 2, sampleRate: 48000)), run)]);
        var content = CaptureDetailsContent.Create(record, Localization());
        Assert.Contains("27:00:00", content.Sources.Single().Items.Single().Label);
        Assert.AreEqual("Unknown", content.Properties.Single(item => item.Label == "CapturedAt").Text);
        Assert.IsFalse(content.Properties.Any(item => item.Label is "Dimensions" or "AspectRatio"));
        Assert.AreEqual("27:00:00", content.Properties.Single(item => item.Label == "Duration").Text);
    }

    [TestMethod]
    public void VideoShowsOriginalFrameTimestampsAndReducesAspectRatio()
    {
        Guid run = Guid.NewGuid();
        var date = DateTimeOffset.UtcNow;
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Video, new(new string('a', 64)), "plan", run,
        [Result(new TextRecognitionMetadata([new("Frame text", timestamp: TimeSpan.FromSeconds(65))]), run),
         Result(new FileDetailsMetadata(AnalysisMediaKind.Video, "capture.mp4", 1, null, date, date,
             video: new(new(1920, 1080), 29.97)), run)]);
        var content = CaptureDetailsContent.Create(record, Localization());
        Assert.EndsWith("1:05", content.Sources.Single().Items.Single().Label);
        Assert.AreEqual("16:9", content.Properties.Single(item => item.Label == "AspectRatio").Text);
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
        Assert.IsTrue(vm.Content.HasOverview);
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
        public Setup()
        {
            Reader.Setup(reader => reader.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CaptureDetailsSnapshot(CaptureDetailsStatus.Available, Record()));
            Memory.SetupGet(memory => memory.State).Returns(() => State);
            var ui = new Mock<ITaskEnvironment>();
            ui.Setup(ui => ui.TryExecute(It.IsAny<Action>())).Returns((Action action) => { action(); return true; });
            ViewModel = new(Reader.Object, Memory.Object, Clipboard.Object, Localization(), ui.Object);
        }
    }
}
