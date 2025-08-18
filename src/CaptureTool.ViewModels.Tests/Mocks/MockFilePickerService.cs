using CaptureTool.Common.Storage;
using CaptureTool.Services.Storage;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockFilePickerService : IFilePickerService
{
    public static readonly Size DefaultImageSize = new(100, 100);

    public Size GetImageSize(ImageFile imageFile)
    {
        return DefaultImageSize;
    }

    public Task<ImageFile?> OpenImageFileAsync(nint hwnd)
    {
        ImageFile? result = null;
        return Task.FromResult(result);
    }

    public Task<string?> PickFolderAsync(nint hwnd)
    {
        string? result = null;
        return Task.FromResult(result);
    }

    public Task<ImageFile?> SaveImageFileAsync(nint hwnd)
    {
        ImageFile? result = null;
        return Task.FromResult(result);
    }
}
