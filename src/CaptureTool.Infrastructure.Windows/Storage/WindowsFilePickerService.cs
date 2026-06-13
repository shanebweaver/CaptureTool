using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Domain.Capture.Files;
using System.Drawing;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.Infrastructure.Windows.Storage;

public sealed partial class WindowsFilePickerService : IFilePickerService
{
    private readonly IWindowHandleProvider _windowHandleProvider;

    public WindowsFilePickerService(IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<IFolder?> PickFolderAsync(UserFolder userFolder)
    {
        PickerLocationId locationId = GetPickerLocationIdForUserFolder(userFolder);

        var picker = new FolderPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = locationId,
        };
        picker.FileTypeFilter.Add("*");

        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return null;
        }

        return new WindowsFolder(folder.Path);
    }

    public async Task<IFile?> PickFileAsync(FilePickerType fileType, UserFolder userFolder)
    {
        PickerLocationId locationId = GetPickerLocationIdForUserFolder(userFolder);

        var filePicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = locationId
        };

        switch (fileType)
        {
            case FilePickerType.Image:
                filePicker.FileTypeFilter.Add(".png");
                filePicker.FileTypeFilter.Add(".jpg");
                filePicker.FileTypeFilter.Add(".jpeg");
                filePicker.FileTypeFilter.Add(".bmp");
                //filePicker.FileTypeFilter.Add(".gif");
                break;

            case FilePickerType.Audio:
                filePicker.FileTypeFilter.Add(".mp3");
                filePicker.FileTypeFilter.Add(".wav");
                filePicker.FileTypeFilter.Add(".flac");
                break;

            case FilePickerType.Video:
                filePicker.FileTypeFilter.Add(".mp4");
                filePicker.FileTypeFilter.Add(".avi");
                filePicker.FileTypeFilter.Add(".mov");
                break;

            case FilePickerType.ImageOrVideo:
                filePicker.FileTypeFilter.Add(".png");
                filePicker.FileTypeFilter.Add(".jpg");
                filePicker.FileTypeFilter.Add(".jpeg");
                filePicker.FileTypeFilter.Add(".bmp");
                filePicker.FileTypeFilter.Add(".mp4");
                filePicker.FileTypeFilter.Add(".avi");
                filePicker.FileTypeFilter.Add(".mov");
                filePicker.FileTypeFilter.Add(".wmv");
                break;

            default:
                throw new InvalidOperationException("Unexpected file type value.");
        }

        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        return new WindowsFile(file.Path);
    }

    public async Task<IFile?> PickSaveFileAsync(FilePickerType fileType, UserFolder userFolder)
    {
        var filePicker = new FileSavePicker
        {
            SuggestedStartLocation = GetPickerLocationIdForUserFolder(userFolder)
        };

        switch (fileType)
        {
            case FilePickerType.Image:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
                    filePicker.FileTypeChoices.Add("JPG", new List<string>() { ".jpg" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                break;

            case FilePickerType.Video:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                break;

            case FilePickerType.Audio:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("MP3", new List<string>() { ".mp3" });
                    filePicker.FileTypeChoices.Add("WAV", new List<string>() { ".wav" });
                    filePicker.FileTypeChoices.Add("FLAC", new List<string>() { ".flac" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                break;

            case FilePickerType.ImageOrVideo:
                throw new InvalidOperationException("Image/video picker type is only supported for opening files.");

            case FilePickerType.Text:
                unsafe
                {
#pragma warning disable IDE0028 // Simplify collection initialization
                    filePicker.FileTypeChoices.Add("Text", new List<string>() { ".txt" });
#pragma warning restore IDE0028 // Simplify collection initialization
                }
                filePicker.SuggestedFileName = "CaptureToolLogs";
                break;

            default:
                throw new InvalidOperationException("Unexpected file type value.");
        }

        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        return new WindowsFile(file.Path);
    }

    private static PickerLocationId GetPickerLocationIdForUserFolder(UserFolder userFolder)
    {
        PickerLocationId locationId = userFolder switch
        {
            UserFolder.Pictures => PickerLocationId.PicturesLibrary,
            UserFolder.Music => PickerLocationId.MusicLibrary,
            UserFolder.Videos => PickerLocationId.VideosLibrary,
            UserFolder.Documents => PickerLocationId.DocumentsLibrary,
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
