using CaptureTool.Common.Storage;
using System.Drawing;

namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IFilePickerService
{
    Task<string?> PickFolderAsync(nint hwnd);
    Task<ImageFile?> OpenImageFileAsync(nint hwnd);
    Task<ImageFile?> SaveImageFileAsync(nint hwnd);
    Task<VideoFile?> SaveVideoFileAsync(nint hwnd);

    Size GetImageSize(ImageFile imageFile);
}
