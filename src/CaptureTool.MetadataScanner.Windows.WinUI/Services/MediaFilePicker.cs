using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Services;

public sealed class MediaFilePicker(IWindowHandleProvider windowHandleProvider) : IMediaFilePicker
{
    private static readonly string[] MediaFileTypes =
    [
        ".3g2",
        ".3gp",
        ".aif",
        ".aiff",
        ".asf",
        ".avi",
        ".m4a",
        ".m4v",
        ".mkv",
        ".mov",
        ".mp3",
        ".mp4",
        ".mpeg",
        ".mpg",
        ".wav",
        ".wma",
        ".wmv",
    ];

    public async Task<StorageFile?> PickMediaFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            ViewMode = PickerViewMode.Thumbnail,
        };

        foreach (string fileType in MediaFileTypes)
        {
            picker.FileTypeFilter.Add(fileType);
        }

        InitializeWithWindow.Initialize(picker, windowHandleProvider.WindowHandle);
        return await picker.PickSingleFileAsync();
    }
}
