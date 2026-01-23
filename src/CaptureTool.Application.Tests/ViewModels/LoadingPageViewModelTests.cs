using CaptureTool.Application.Implementations.ViewModels;
using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.UseCases.Loading;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

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

        Fixture.Freeze<Mock<ILoadingGoBackUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var goBackAction = Fixture.Freeze<Mock<ILoadingGoBackUseCase>>();
        var vm = Create();

        vm.GoBackCommand.Execute();

        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(LoadingPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(LoadingPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }
}
