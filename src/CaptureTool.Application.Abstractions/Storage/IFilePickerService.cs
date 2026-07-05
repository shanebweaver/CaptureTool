using CaptureTool.Domain.Files;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Storage;

public partial interface IFilePickerService
{
    Task<IFolder?> PickFolderAsync(UserFolder defaultFolder);
    Task<IFile?> PickFileAsync(FilePickerType fileType, UserFolder defaultFolder);
    Task<IFile?> PickSaveFileAsync(FilePickerType fileType, UserFolder defaultFolder);

    Size GetImageFileSize(IImageFile imageFile);
}
