using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Windowing;
using Moq;

namespace CaptureTool.ViewModels.Tests;

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

        Fixture.Freeze<Mock<IAddOnsGoBackAction>>();
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
        var goBackAction = Fixture.Freeze<Mock<IAddOnsGoBackAction>>();
        var vm = Create();

        // Act
        vm.GoBackCommand.Execute(null);

        // Assert
        goBackAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AddOnsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AddOnsPageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }
}
