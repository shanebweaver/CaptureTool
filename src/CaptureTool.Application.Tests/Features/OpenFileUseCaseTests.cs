using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.Features.AppMenu.OpenFile;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public class OpenFileUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithImageFile_ShouldNavigateToImageEdit()
    {
        Mock<IFileTypeDetector> fileTypeDetector = new();
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IWindowHandleProvider> windowHandleProvider = new();
        filePickerService
            .Setup(service => service.PickFileAsync(It.IsAny<nint>(), FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == "capture.png"));
        fileTypeDetector
            .Setup(detector => detector.DetectFileType("capture.png"))
            .Returns(CaptureFileType.Image);

        OpenFileUseCase useCase = new(
            fileTypeDetector.Object,
            filePickerService.Object,
            navigationService.Object,
            windowHandleProvider.Object);

        await useCase.ExecuteAsync(new OpenFileRequest());

        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.ImageEdit,
                It.Is<ImageFile>(file => file.FilePath == "capture.png")),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithVideoFile_ShouldNavigateToVideoEdit()
    {
        Mock<IFileTypeDetector> fileTypeDetector = new();
        Mock<IFilePickerService> filePickerService = new();
        Mock<INavigationService> navigationService = new();
        Mock<IWindowHandleProvider> windowHandleProvider = new();
        filePickerService
            .Setup(service => service.PickFileAsync(It.IsAny<nint>(), FilePickerType.ImageOrVideo, UserFolder.Pictures))
            .ReturnsAsync(Mock.Of<IFile>(file => file.FilePath == "capture.mp4"));
        fileTypeDetector
            .Setup(detector => detector.DetectFileType("capture.mp4"))
            .Returns(CaptureFileType.Video);

        OpenFileUseCase useCase = new(
            fileTypeDetector.Object,
            filePickerService.Object,
            navigationService.Object,
            windowHandleProvider.Object);

        await useCase.ExecuteAsync(new OpenFileRequest());

        navigationService.Verify(
            service => service.Navigate(
                NavigationRoute.VideoEdit,
                It.Is<VideoFile>(file => file.FilePath == "capture.mp4")),
            Times.Once);
    }
}
