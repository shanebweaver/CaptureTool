using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Infrastructure.Windows.Clipboard;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CaptureTool.Infrastructure.Windows.Tests.Clipboard;

[TestClass]
public sealed class WindowsClipboardServiceTests
{
    [TestMethod]
    public async Task CreateFileDataPackageAsync_ShouldMarshalStorageItemsToWinRT()
    {
        string filePath = Path.GetTempFileName();

        try
        {
            DataPackage? package = await WindowsClipboardService.CreateFileDataPackageAsync(
                new ClipboardFile(filePath));

            Assert.IsNotNull(package);
            IReadOnlyList<IStorageItem> items = await package.GetView().GetStorageItemsAsync();
            Assert.HasCount(1, items);
            Assert.AreEqual(filePath, items[0].Path);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
