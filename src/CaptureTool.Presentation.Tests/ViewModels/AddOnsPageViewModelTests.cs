using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Presentation.ViewModels;
using CaptureTool.Application.Abstractions.UseCases.AddOns;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Windowing;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public class AddOnsPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private StorePageViewModel Create() => Fixture.Create<StorePageViewModel>();

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
        telemetryService.Verify(t => t.ActivityInitiated(StorePageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(StorePageViewModel.ActivityIds.GoBack, It.IsAny<string>()), Times.Once);
    }
}
