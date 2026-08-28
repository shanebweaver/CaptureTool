using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.Shell;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class MainWindowBackgroundActivityViewModelTests
{
    [TestMethod]
    public async Task RefreshBackgroundActivity_ShouldMapProgressAndAttentionTruthfully()
    {
        CaptureAnalysisActivitySnapshot active = new(
            modelPreparations:
            [
                new CaptureAnalysisModelPreparationActivity(
                    CreateAnalyzer(),
                    AnalysisCapabilities.SpeechTranscriptV1,
                    CaptureMediaKind.Audio,
                    0.25),
            ],
            runningCaptureCount: 2,
            queuedCaptureCount: 3,
            waitingCaptureCount: 1,
            retryCaptureCount: 1,
            failedCaptureCount: 1,
            isBackfillInProgress: true,
            backfillFractionComplete: 0.5);
        var activityQuery = new Mock<ICaptureAnalysisActivityQueryService>(MockBehavior.Strict);
        activityQuery
            .SetupSequence(service => service.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(active)
            .ReturnsAsync(new CaptureAnalysisActivitySnapshot(failedCaptureCount: 2))
            .ReturnsAsync(new CaptureAnalysisActivitySnapshot());
        using MainWindowViewModel viewModel = CreateViewModel(activityQuery.Object);

        await viewModel.RefreshBackgroundActivityAsync();

        Assert.IsTrue(viewModel.HasBackgroundActivity);
        Assert.IsTrue(viewModel.HasActiveBackgroundActivity);
        Assert.IsTrue(viewModel.HasBackgroundActivityAttention);
        Assert.HasCount(6, viewModel.BackgroundActivities);
        Assert.AreEqual("Preparing AI model · 25%", viewModel.BackgroundActivitySummary);
        Assert.IsTrue(viewModel.IsPrimaryActivityDeterminate);
        Assert.AreEqual(0.25, viewModel.PrimaryActivityProgress);
        Assert.IsTrue(viewModel.BackgroundActivities[0].IsDeterminate);
        Assert.IsTrue(viewModel.BackgroundActivities[^1].IsAttention);

        await viewModel.RefreshBackgroundActivityAsync();

        Assert.IsTrue(viewModel.HasBackgroundActivity);
        Assert.IsFalse(viewModel.HasActiveBackgroundActivity);
        Assert.IsTrue(viewModel.HasBackgroundActivityAttention);
        Assert.HasCount(1, viewModel.BackgroundActivities);
        Assert.AreEqual("Analysis needs attention · 2 capture(s)", viewModel.BackgroundActivitySummary);

        await viewModel.RefreshBackgroundActivityAsync();

        Assert.IsFalse(viewModel.HasBackgroundActivity);
        Assert.IsFalse(viewModel.HasActiveBackgroundActivity);
        Assert.IsFalse(viewModel.HasBackgroundActivityAttention);
        Assert.IsEmpty(viewModel.BackgroundActivities);
        Assert.AreEqual(string.Empty, viewModel.BackgroundActivitySummary);
    }

    private static MainWindowViewModel CreateViewModel(
        ICaptureAnalysisActivityQueryService activityQuery)
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        return new MainWindowViewModel(
            Mock.Of<IThemeService>(service =>
                service.DefaultTheme == AppTheme.SystemDefault &&
                service.CurrentTheme == AppTheme.SystemDefault),
            Mock.Of<IAppNotificationService>(),
            activityQuery,
            localization.Object);
    }

    private static AnalyzerIdentity CreateAnalyzer()
    {
        return new AnalyzerIdentity(
            "speech-transcription",
            "microsoft.foundry-local",
            "whisper",
            "1",
            "1",
            "foundry-local",
            "1",
            null,
            null);
    }
}
