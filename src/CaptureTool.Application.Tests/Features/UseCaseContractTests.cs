using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Features.Settings.UpdateAppTheme;
using CaptureTool.Infrastructure.Abstractions.Themes;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class UseCaseContractTests
{
    [TestMethod]
    public async Task MuteAudioCaptureUseCase_ShouldToggleMute()
    {
        var audioCaptureHandler = new Mock<IAudioCaptureHandler>();
        var useCase = new MuteAudioCaptureUseCase(audioCaptureHandler.Object);

        await useCase.ExecuteAsync(new MuteAudioCaptureRequest());

        audioCaptureHandler.Verify(handler => handler.ToggleMute(), Times.Once);
    }

    [TestMethod]
    public void UpdateAppThemeUseCase_CanExecute_ShouldValidateThemeIndexSynchronously()
    {
        var useCase = new UpdateAppThemeUseCase(Mock.Of<IThemeService>());
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
        var useCase = new UpdateAppThemeUseCase(themes.Object);

        await useCase.ExecuteAsync(new UpdateAppThemeRequest(1));

        themes.Verify(themeService => themeService.UpdateCurrentTheme(AppTheme.Dark), Times.Once);
    }
}
