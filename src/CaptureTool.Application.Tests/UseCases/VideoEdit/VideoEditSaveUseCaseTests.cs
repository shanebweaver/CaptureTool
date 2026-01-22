using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.VideoEdit;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.VideoEdit;

[TestClass]
public class VideoEditSaveUseCaseTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPickFileAndCopy()
    {
        var filePickerService = Fixture.Freeze<Mock<IFilePickerService>>();
        var windowingService = Fixture.Freeze<Mock<IWindowHandleProvider>>();
        windowingService.Setup(w => w.GetMainWindowHandle()).Returns(new nint(123));
        
        // Create both temp files
        var tempInputFile = Path.GetTempFileName();
        var tempOutputFile = Path.GetTempFileName();
        
        try
        {
            var mockFile = Mock.Of<IFile>(f => f.FilePath == tempOutputFile);
            
            filePickerService.Setup(f => f.PickSaveFileAsync(
                It.IsAny<nint>(), 
                FilePickerType.Video, 
                UserFolder.Videos))
                .ReturnsAsync(mockFile);

            var action = Fixture.Create<VideoEditSaveUseCase>();
            await action.ExecuteAsync(tempInputFile);
            
            filePickerService.Verify(f => f.PickSaveFileAsync(
                It.IsAny<nint>(), 
                FilePickerType.Video, 
                UserFolder.Videos), Times.Once);
        }
        finally
        {
            if (File.Exists(tempInputFile))
                File.Delete(tempInputFile);
            if (File.Exists(tempOutputFile))
                File.Delete(tempOutputFile);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldThrowWhenVideoPathIsEmpty()
    {
        var action = Fixture.Create<VideoEditSaveUseCase>();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => action.ExecuteAsync(string.Empty));
    }
}
