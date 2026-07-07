using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Storage;

public partial interface IFilePickerService
{
    Task<IFolder?> PickFolderAsync(UserFolder defaultFolder);
    Task<FileReference?> PickFileAsync(FilePickerType fileType, UserFolder defaultFolder);
    Task<FileReference?> PickSaveFileAsync(FilePickerType fileType, UserFolder defaultFolder);
}
