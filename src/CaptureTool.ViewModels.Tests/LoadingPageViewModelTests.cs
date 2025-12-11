using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.Loading;
using CaptureTool.Services.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public sealed class LoadingPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private LoadingPageViewModel Create() => Fixture.Create<LoadingPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<ILoadingActions>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldDelegateToActions_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var actions = Fixture.Freeze<Mock<ILoadingActions>>();
        var vm = Create();

        vm.GoBackCommand.Execute(null);

        actions.Verify(a => a.GoBack(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(LoadingPageViewModel.ActivityIds.GoBack), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(LoadingPageViewModel.ActivityIds.GoBack), Times.Once);
    }
}
