using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.AppMenu;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.AppMenu;

[TestClass]
public class AppMenuUseCasesTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void NewImageCapture_NavigatesToImageCapture()
    {
        var navigation = Fixture.Freeze<Mock<IAppNavigation>>();

        var actions = Fixture.Create<AppMenuUseCases>();
        actions.NewImageCapture();

        navigation.Verify(n => n.GoToImageCapture(CaptureOptions.ImageDefault), Times.Once);
    }

    [TestMethod]
    public void NavigateToSettings_NavigatesToSettings()
    {
        var navigation = Fixture.Freeze<Mock<IAppNavigation>>();

        var actions = Fixture.Create<AppMenuUseCases>();
        actions.NavigateToSettings();

        navigation.Verify(n => n.GoToSettings(), Times.Once);
    }

    [TestMethod]
    public void ShowAboutApp_NavigatesToAbout()
    {
        var navigation = Fixture.Freeze<Mock<IAppNavigation>>();

        var actions = Fixture.Create<AppMenuUseCases>();
        actions.ShowAboutApp();

        navigation.Verify(n => n.GoToAbout(), Times.Once);
    }

    [TestMethod]
    public void ShowAddOns_NavigatesToAddOns()
    {
        var navigation = Fixture.Freeze<Mock<IAppNavigation>>();

        var actions = Fixture.Create<AppMenuUseCases>();
        actions.ShowAddOns();

        navigation.Verify(n => n.GoToAddOns(), Times.Once);
    }

    [TestMethod]
    public void ExitApplication_CallsShutdown()
    {
        var shutdownHandler = Fixture.Freeze<Mock<IShutdownHandler>>();

        var actions = Fixture.Create<AppMenuUseCases>();
        actions.ExitApplication();

        shutdownHandler.Verify(s => s.Shutdown(), Times.Once);
    }

    [TestMethod]
    public async Task OpenFileAsync_PicksFileAndNavigatesToImageEdit()
    {
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var navigation = Fixture.Freeze<Mock<IAppNavigation>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(12345));

        var mockFile = new Mock<IFile>();
        mockFile.Setup(f => f.FilePath).Returns("test.png");
        filePickerService.Setup(f => f.PickFileAsync(It.IsAny<nint>(), FilePickerType.Image, UserFolder.Pictures))
            .ReturnsAsync(mockFile.Object);

        var actions = Fixture.Create<AppMenuUseCases>();
        await actions.OpenFileAsync(CancellationToken.None);

        navigation.Verify(n => n.GoToImageEdit(It.Is<ImageFile>(img => img.FilePath == "test.png")), Times.Once);
    }

    [TestMethod]
    public async Task OpenFileAsync_WhenNoFileSelected_ThrowsOperationCanceledException()
    {
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();

        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(12345));
        filePickerService.Setup(f => f.PickFileAsync(It.IsAny<nint>(), FilePickerType.Image, UserFolder.Pictures))
            .ReturnsAsync((IFile?)null);

        var actions = Fixture.Create<AppMenuUseCases>();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            async () => await actions.OpenFileAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenRecentCaptureAsync_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var actions = Fixture.Create<AppMenuUseCases>();

        await Assert.ThrowsExceptionAsync<FileNotFoundException>(
            async () => await actions.OpenRecentCaptureAsync("nonexistent.png", CancellationToken.None));
    }
}
