using CaptureTool.Services.Share;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace CaptureTool.Services.Windows.Share;

public sealed partial class WindowsShareService : IShareService
{
    public async Task ShareAsync(string filePath, nint hwnd)
    {
        if (!DataTransferManager.IsSupported())
        {
            throw new NotSupportedException("Sharing is not supported.");
        }

        var share = new StorageFileShare(filePath);

        DataTransferManager manager = DataTransferManagerInterop.GetForWindow(hwnd);
        await share.InitializeAsync(manager);

        DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
    }
}
