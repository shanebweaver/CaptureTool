using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Implementations.Windows.Share;

public sealed partial class BitmapStreamShare : IDisposable
{
    private readonly Stream _stream;
    private DataTransferManager? _manager;

    public BitmapStreamShare(Stream stream)
    {
        _stream = stream;
    }

    public Task InitializeAsync(DataTransferManager manager)
    {
        _manager = manager;
        _manager.DataRequested += OnDataRequested;
        return Task.CompletedTask;
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

        _stream.Seek(0, SeekOrigin.Begin);
        var streamRef = RandomAccessStreamReference.CreateFromStream(_stream.AsRandomAccessStream());
        request.Data.SetBitmap(streamRef);
    }

    public void Dispose()
    {
        if (_manager != null)
        {
            _manager.DataRequested -= OnDataRequested;
            _manager = null;
        }
    }
}
