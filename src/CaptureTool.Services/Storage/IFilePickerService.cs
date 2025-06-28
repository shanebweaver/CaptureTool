using CaptureTool.Storage;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.Services.Storage;

public partial interface IFilePickerService
{
    Task<ImageFile?> OpenImageFileAsync();
    Task<ImageFile?> SaveImageFileAsync();

    Size GetImageSize(ImageFile imageFile);
}
