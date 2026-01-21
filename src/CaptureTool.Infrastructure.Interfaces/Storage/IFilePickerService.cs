using System.Drawing;

namespace CaptureTool.Infrastructure.Interfaces.Storage;

public partial interface IFilePickerService
{
    Task<IFolder?> PickFolderAsync(nint hwnd, UserFolder defaultFolder);
    Task<IFile?> PickFileAsync(nint hwnd, FilePickerType fileType, UserFolder defaultFolder);
    Task<IFile?> PickSaveFileAsync(nint hwnd, FilePickerType fileType, UserFolder defaultFolder);

    Size GetImageFileSize(IImageFile imageFile);
}