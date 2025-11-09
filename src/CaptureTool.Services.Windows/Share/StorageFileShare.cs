using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CaptureTool.Services.Windows.Share;

public sealed partial class StorageFileShare : IDisposable
{
    private readonly string _filePath;
    private StorageFile? _file;
    private DataTransferManager? _manager;

    public StorageFileShare(string filePath)
    {
        _filePath = filePath;
    }

    public async Task InitializeAsync(DataTransferManager manager)
    {
        _manager = manager;
        _file = await StorageFile.GetFileFromPathAsync(_filePath);
        _manager.DataRequested += OnDataRequested;
    }

    private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_manager != null)
        {
            _manager.DataRequested -= OnDataRequested;
        }

        DataRequest request = args.Request;
        request.Data.Properties.Title = "Captured Image";
        request.Data.Properties.Description = "Share this capture";

        if (_file != null)
        {
            request.Data.SetStorageItems(new List<StorageFile> { _file });
        }
        else
        {
            request.FailWithDisplayText("Failed to load capture file.");
        }
    }

    public void Dispose()
    {
        if (_manager != null)
        {
            _manager.DataRequested -= OnDataRequested;
            _manager = null;
        }

        _file = null;
    }
}
