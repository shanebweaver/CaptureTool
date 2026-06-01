using Windows.Storage;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Services;

public interface IMediaFilePicker
{
    Task<StorageFile?> PickMediaFileAsync();
}
