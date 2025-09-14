using CaptureTool.Common.Storage;
using CaptureTool.Services.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.Services.Windows.Storage;

public sealed partial class WindowsFilePickerService : IFilePickerService
{
    public async Task<string?> PickFolderAsync(nint hwnd)
    {
        var picker = new FolderPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<ImageFile?> OpenImageFileAsync(nint hwnd)
    {
        var filePicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        filePicker.FileTypeFilter.Add(".png");
        filePicker.FileTypeFilter.Add(".jpg");

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSingleFileAsync();
        return (file != null) ? new ImageFile(file.Path) : null;
    }

    public async Task<ImageFile?> SaveImageFileAsync(nint hwnd)
    {
        var filePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        unsafe
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            filePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
            filePicker.FileTypeChoices.Add("JPG", new List<string>() { ".jpg" });
#pragma warning restore IDE0028 // Simplify collection initialization
        }

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSaveFileAsync();
        return (file != null) ? new ImageFile(file.Path) : null;
    }

    public async Task<VideoFile?> SaveVideoFileAsync(nint hwnd)
    {
        var filePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary
        };

        unsafe
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            filePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });
#pragma warning restore IDE0028 // Simplify collection initialization
        }

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        StorageFile file = await filePicker.PickSaveFileAsync();
        return (file != null) ? new VideoFile(file.Path) : null;
    }

    public Size GetImageSize(ImageFile imageFile)
    {
        using FileStream file = new(imageFile.Path, FileMode.Open, FileAccess.Read);
        var image = Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        float width = image.PhysicalDimension.Width;
        float height = image.PhysicalDimension.Height;
        return new(Convert.ToInt32(width), Convert.ToInt32(height));
    }
}
