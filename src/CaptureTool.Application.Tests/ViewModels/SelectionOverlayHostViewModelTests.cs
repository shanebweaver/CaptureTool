using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Commands;
using Moq;
using System.ComponentModel;
using System.Drawing;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public sealed class SelectionOverlayHostViewModelTests
{
    public required IFixture Fixture { get; set; }

    private SelectionOverlayHostViewModel Create() => Fixture.Create<SelectionOverlayHostViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void OnSelectedCaptureTypeIndexChanged_ShouldNotCauseCycle_WhenPropagatingToOtherWindows()
    {
        // Arrange
        var hostVM = Create();
        
        var primaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        primaryWindowVM.SetupGet(w => w.IsPrimary).Returns(true);
        primaryWindowVM.SetupGet(w => w.SelectedCaptureTypeIndex).Returns(1);
        
        var updateCommand = new Mock<IAppCommand<int>>();
        int propagatedCount = 0;
        updateCommand.Setup(c => c.Execute(It.IsAny<int>())).Callback(() => 
        {
            propagatedCount++;
            // Simulate the property change that would normally occur
            primaryWindowVM.Raise(w => w.PropertyChanged += null, 
                new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex)));
        });
        
        var secondaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        secondaryWindowVM.SetupGet(w => w.IsPrimary).Returns(false);
        secondaryWindowVM.SetupGet(w => w.UpdateSelectedCaptureTypeCommand).Returns(updateCommand.Object);
        
        hostVM.AddWindowViewModel(primaryWindowVM.Object, isPrimary: true);
        hostVM.AddWindowViewModel(secondaryWindowVM.Object, isPrimary: false);
        
        // Act - Simulate a property change on the primary window
        primaryWindowVM.Raise(w => w.PropertyChanged += null, 
            new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex)));
        
        // Assert - The command should be executed exactly once, not repeatedly in a cycle
        Assert.AreEqual(1, propagatedCount, "UpdateSelectedCaptureTypeCommand should be executed exactly once");
    }
    
    [TestMethod]
    public void OnSelectedCaptureModeIndexChanged_ShouldNotCauseCycle_WhenPropagatingToOtherWindows()
    {
        // Arrange
        var hostVM = Create();
        
        var primaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        primaryWindowVM.SetupGet(w => w.IsPrimary).Returns(true);
        primaryWindowVM.SetupGet(w => w.SelectedCaptureModeIndex).Returns(1);
        
        var updateCommand = new Mock<IAppCommand<int>>();
        int propagatedCount = 0;
        updateCommand.Setup(c => c.Execute(It.IsAny<int>())).Callback(() => 
        {
            propagatedCount++;
            // Simulate the property change that would normally occur
            primaryWindowVM.Raise(w => w.PropertyChanged += null, 
                new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.SelectedCaptureModeIndex)));
        });
        
        var secondaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        secondaryWindowVM.SetupGet(w => w.IsPrimary).Returns(false);
        secondaryWindowVM.SetupGet(w => w.UpdateSelectedCaptureModeCommand).Returns(updateCommand.Object);
        
        hostVM.AddWindowViewModel(primaryWindowVM.Object, isPrimary: true);
        hostVM.AddWindowViewModel(secondaryWindowVM.Object, isPrimary: false);
        
        // Act - Simulate a property change on the primary window
        primaryWindowVM.Raise(w => w.PropertyChanged += null, 
            new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.SelectedCaptureModeIndex)));
        
        // Assert - The command should be executed exactly once, not repeatedly in a cycle
        Assert.AreEqual(1, propagatedCount, "UpdateSelectedCaptureModeCommand should be executed exactly once");
    }
    
    [TestMethod]
    public void OnCaptureAreaChanged_ShouldNotCauseCycle_WhenPropagatingToOtherWindows()
    {
        // Arrange
        var hostVM = Create();
        
        var primaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        primaryWindowVM.SetupGet(w => w.IsPrimary).Returns(true);
        primaryWindowVM.SetupGet(w => w.CaptureArea).Returns(new Rectangle(0, 0, 100, 100));
        // Create a valid MonitorCaptureResult to satisfy the non-null monitor check
        var monitorResult = new MonitorCaptureResult(
            IntPtr.Zero, Array.Empty<byte>(), 96, Rectangle.Empty, Rectangle.Empty, true);
        primaryWindowVM.SetupGet(w => w.Monitor).Returns(monitorResult);
        
        var updateCommand = new Mock<IAppCommand<Rectangle>>();
        int propagatedCount = 0;
        updateCommand.Setup(c => c.Execute(It.IsAny<Rectangle>())).Callback(() => 
        {
            propagatedCount++;
            // Simulate the property change that would normally occur
            primaryWindowVM.Raise(w => w.PropertyChanged += null, 
                new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.CaptureArea)));
        });
        
        var secondaryWindowVM = new Mock<ISelectionOverlayWindowViewModel>();
        secondaryWindowVM.SetupGet(w => w.IsPrimary).Returns(false);
        secondaryWindowVM.SetupGet(w => w.UpdateCaptureAreaCommand).Returns(updateCommand.Object);
        
        hostVM.AddWindowViewModel(primaryWindowVM.Object, isPrimary: true);
        hostVM.AddWindowViewModel(secondaryWindowVM.Object, isPrimary: false);
        
        // Act - Simulate a property change on the primary window
        primaryWindowVM.Raise(w => w.PropertyChanged += null, 
            new PropertyChangedEventArgs(nameof(ISelectionOverlayWindowViewModel.CaptureArea)));
        
        // Assert - The command should be executed exactly once, not repeatedly in a cycle
        Assert.AreEqual(1, propagatedCount, "UpdateCaptureAreaCommand should be executed exactly once");
    }
}
