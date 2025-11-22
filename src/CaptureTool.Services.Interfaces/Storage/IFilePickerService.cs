using System.Drawing;

namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IFilePickerService
{
    Task<IFolder?> PickFolderAsync(nint hwnd, UserFolder defaultFolder);
    Task<IFile?> PickFileAsync(nint hwnd, FileType fileType, UserFolder defaultFolder);
    Task<IFile?> PickSaveFileAsync(nint hwnd, FileType fileType, UserFolder defaultFolder);

    Size GetImageFileSize(IImageFile imageFile);
}