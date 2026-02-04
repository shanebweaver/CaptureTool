using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Themes;
using Moq;
using System.ComponentModel;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public sealed class SelectionOverlayWindowViewModelTests
{
    public required IFixture Fixture { get; set; }

    private SelectionOverlayWindowViewModel Create() => Fixture.Create<SelectionOverlayWindowViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<IAppNavigation>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<IThemeService>>();
        Fixture.Freeze<Mock<IShutdownHandler>>();
        Fixture.Freeze<Mock<IImageCaptureHandler>>();
        Fixture.Freeze<Mock<IFactoryServiceWithArgs<ICaptureModeViewModel, CaptureMode>>>();
        Fixture.Freeze<Mock<IFactoryServiceWithArgs<ICaptureTypeViewModel, CaptureType>>>();
    }

    [TestMethod]
    public void UpdateSelectedCaptureTypeCommand_ShouldNotRaisePropertyChanged_WhenExecutedExternally()
    {
        // Arrange
        var vm = Create();
        int propertyChangedCount = 0;
        
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex))
            {
                propertyChangedCount++;
            }
        };

        // Act - Execute the command as if it were called from the host
        vm.UpdateSelectedCaptureTypeCommand.Execute(0);

        // Assert - PropertyChanged should NOT be raised when updated from external source
        Assert.AreEqual(0, propertyChangedCount, 
            "SelectedCaptureTypeIndex PropertyChanged should not fire when updated via command (external source)");
    }

    [TestMethod]
    public void UpdateSelectedCaptureModeCommand_ShouldNotRaisePropertyChanged_WhenExecutedExternally()
    {
        // Arrange
        var vm = Create();
        int propertyChangedCount = 0;
        
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ISelectionOverlayWindowViewModel.SelectedCaptureModeIndex))
            {
                propertyChangedCount++;
            }
        };

        // Act - Execute the command as if it were called from the host
        vm.UpdateSelectedCaptureModeCommand.Execute(0);

        // Assert - PropertyChanged should NOT be raised when updated from external source
        Assert.AreEqual(0, propertyChangedCount, 
            "SelectedCaptureModeIndex PropertyChanged should not fire when updated via command (external source)");
    }

    [TestMethod]
    public void UpdateCaptureAreaCommand_ShouldNotRaisePropertyChanged_WhenExecutedExternally()
    {
        // Arrange
        var vm = Create();
        int propertyChangedCount = 0;
        
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ISelectionOverlayWindowViewModel.CaptureArea))
            {
                propertyChangedCount++;
            }
        };

        // Act - Execute the command as if it were called from the host
        vm.UpdateCaptureAreaCommand.Execute(System.Drawing.Rectangle.Empty);

        // Assert - PropertyChanged should NOT be raised when updated from external source
        Assert.AreEqual(0, propertyChangedCount, 
            "CaptureArea PropertyChanged should not fire when updated via command (external source)");
    }
}
