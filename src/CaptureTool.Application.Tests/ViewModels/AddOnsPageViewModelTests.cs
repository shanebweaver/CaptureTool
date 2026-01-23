using CaptureTool.Application.Implementations.ViewModels;
using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.UseCases.AddOns;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public class AddOnsPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private AddOnsPageViewModel Create() => Fixture.Create<AddOnsPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IAddOnsGoBackUseCase>>();
        Fixture.Freeze<Mock<IWindowHandleProvider>>();
        Fixture.Freeze<Mock<IStoreService>>();
        Fixture.Freeze<Mock<ILocalizationService>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<ICancellationService>>();
    }

    [TestMethod]
    public void GoBackCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var goBackAction = Fixture.Freeze<Mock<IAddOnsGoBackUseCase>>();
        var vm = Create();

        // Act
        vm.GoBackCommand.Execute();

        // Assert
        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AddOnsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AddOnsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }
}
