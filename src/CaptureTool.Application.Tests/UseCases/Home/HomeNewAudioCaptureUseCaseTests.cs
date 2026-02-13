using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.Home;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.Home;

[TestClass]
public class HomeNewAudioCaptureUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void CanExecute_ShouldRespectFeatureFlag()
    {
        var fm = Fixture.Freeze<Mock<IFeatureManager>>();
        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_AudioCapture)).Returns(false);
        var action = Fixture.Create<HomeNewAudioCaptureUseCase>();
        Assert.IsFalse(action.CanExecute());

        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_AudioCapture)).Returns(true);
        Assert.IsTrue(action.CanExecute());
    }

    [TestMethod]
    public void Execute_ShouldNavigateToAudioCapture()
    {
        var appNav = Fixture.Freeze<Mock<IAppNavigation>>();
        var fm = Fixture.Freeze<Mock<IFeatureManager>>();
        fm.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_AudioCapture)).Returns(true);

        var action = Fixture.Create<HomeNewAudioCaptureUseCase>();
        action.Execute();
        appNav.Verify(a => a.GoToAudioCapture(), Times.Once);
    }
}
