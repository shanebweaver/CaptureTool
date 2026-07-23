using CaptureTool.Application.Abstractions.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Settings.UpdateAppTheme;
using CaptureTool.Application.Tests.Capture.Audio;
using Moq;

namespace CaptureTool.Application.Tests.UseCases;

[TestClass]
public sealed class UseCaseContractTests
{
    [TestMethod]
    public async Task MuteAudioCaptureUseCase_ShouldToggleMute()
    {
        var audioCapture = new FakeAudioCaptureWorkflow();
        var useCase = new MuteAudioCaptureUseCase(audioCapture, TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new MuteAudioCaptureRequest(), TestContext.CancellationToken);

        Assert.AreEqual(1, audioCapture.ToggleMuteCallCount);
    }

    [TestMethod]
    public void UpdateAppThemeUseCase_CanExecute_ShouldValidateThemeIndexSynchronously()
    {
        var useCase = new UpdateAppThemeUseCase(Mock.Of<IThemeService>(), TestUseCaseExecutor.Instance);
        var conditional = (IConditional<UpdateAppThemeRequest>)useCase;

        Assert.IsTrue(conditional.CanExecute(new UpdateAppThemeRequest(0)));
        Assert.IsTrue(conditional.CanExecute(new UpdateAppThemeRequest(2)));
        Assert.IsFalse(conditional.CanExecute(new UpdateAppThemeRequest(-1)));
        Assert.IsFalse(conditional.CanExecute(new UpdateAppThemeRequest(3)));
    }

    [TestMethod]
    public async Task UpdateAppThemeUseCase_ShouldUpdateTheme_WhenIndexIsValid()
    {
        var themes = new Mock<IThemeService>();
        var useCase = new UpdateAppThemeUseCase(themes.Object, TestUseCaseExecutor.Instance);

        await useCase.ExecuteAsync(new UpdateAppThemeRequest(1), TestContext.CancellationToken);

        themes.Verify(themeService => themeService.UpdateCurrentTheme(AppTheme.Dark), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
