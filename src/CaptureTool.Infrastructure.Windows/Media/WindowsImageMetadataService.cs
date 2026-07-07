using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Infrastructure.Windows.Media;

public sealed class WindowsImageMetadataService : IImageMetadataService
{
    public Size GetImageFileSize(ImageFile imageFile)
    {
        using FileStream file = new(imageFile.FilePath, FileMode.Open, FileAccess.Read);
        using Image image = Image.FromStream(
            stream: file,
            useEmbeddedColorManagement: false,
            validateImageData: false);

        return new(image.Width, image.Height);
    }
}
