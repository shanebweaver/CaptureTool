using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Windowing;
using Windows.ApplicationModel.DataTransfer;

namespace CaptureTool.Infrastructure.Windows.Share;

public sealed partial class WindowsShareService : IShareService
{
    private readonly IWindowHandleProvider _windowHandleProvider;

    public WindowsShareService(IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task ShareAsync(string filePath)
    {
        if (!DataTransferManager.IsSupported())
        {
            throw new NotSupportedException("Sharing is not supported.");
        }

        var share = new StorageFileShare(filePath);

        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        DataTransferManager manager = DataTransferManagerInterop.GetForWindow(hwnd);
        await share.InitializeAsync(manager);

        DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
    }

    public async Task ShareStreamAsync(Stream stream)
    {
        if (!DataTransferManager.IsSupported())
        {
            throw new NotSupportedException("Sharing is not supported.");
        }

        var share = new BitmapStreamShare(stream);

        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        DataTransferManager manager = DataTransferManagerInterop.GetForWindow(hwnd);
        await share.InitializeAsync(manager);

        DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
    }
}
