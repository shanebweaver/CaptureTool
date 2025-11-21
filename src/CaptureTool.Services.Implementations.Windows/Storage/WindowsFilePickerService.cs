using CaptureTool.Services.Interfaces.Storage;
using System.Drawing;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.Services.Implementations.Windows.Storage;

public sealed partial class WindowsFilePickerService : IFilePickerService
{
    public async Task<IFolder?> PickFolderAsync(nint hwnd, UserFolder userFolder)
    {
        PickerLocationId locationId = GetPickerLocationIdForUserFolder(userFolder);

        var picker = new FolderPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = locationId,
        };
        picker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        return new WindowsFolder(folder.Path);
    }

    public async Task<IFile?> PickFileAsync(nint hwnd, FileType fileType, UserFolder userFolder)
    {
        PickerLocationId locationId = GetPickerLocationIdForUserFolder(userFolder);

        var filePicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = locationId
        };

        switch (fileType)
        {
            case FileType.Image:
                filePicker.FileTypeFilter.Add(".png");
                filePicker.FileTypeFilter.Add(".jpg");
                filePicker.FileTypeFilter.Add(".jpeg");
                filePicker.FileTypeFilter.Add(".bmp");
                //filePicker.FileTypeFilter.Add(".gif");
                break;
            case FileType.Audio:
                //filePicker.FileTypeFilter.Add(".mp3");
                //filePicker.FileTypeFilter.Add(".wav");
                //filePicker.FileTypeFilter.Add(".flac");
                //break;
            case FileType.Video:
                //filePicker.FileTypeFilter.Add(".mp4");
                //filePicker.FileTypeFilter.Add(".avi");
                //filePicker.FileTypeFilter.Add(".mov");
                //break;
            default:
                throw new InvalidOperationException("Unexpected file type value.");
        }

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSingleFileAsync();
        return new WindowsFile(file.Path);
    }

    public async Task<IFile?> PickSaveFileAsync(nint hwnd, FileType fileType, UserFolder userFolder)
    {
        var filePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        switch (fileType)
        {
            case FileType.Image:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
                    filePicker.FileTypeChoices.Add("JPG", new List<string>() { ".jpg" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                break;

            case FileType.Video:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                break;

            case FileType.Audio:
            default:
                throw new InvalidOperationException("Unexpected file type value.");
        }

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSaveFileAsync();
        return new WindowsFile(file.Path);
    }

    private static PickerLocationId GetPickerLocationIdForUserFolder(UserFolder userFolder)
    {
        PickerLocationId locationId = userFolder switch
        {
            UserFolder.Pictures => PickerLocationId.PicturesLibrary,
            UserFolder.Music => PickerLocationId.MusicLibrary,
            UserFolder.Videos => PickerLocationId.VideosLibrary,
            _ => throw new InvalidOperationException("Unexpected user folder value."),
        };

        return locationId;
    }

    public Size GetImageFileSize(IImageFile imageFile)
    {
        using FileStream file = new(imageFile.FilePath, FileMode.Open, FileAccess.Read);
        var image = Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        float width = image.PhysicalDimension.Width;
        float height = image.PhysicalDimension.Height;
        return new(Convert.ToInt32(width), Convert.ToInt32(height));
    }
}
