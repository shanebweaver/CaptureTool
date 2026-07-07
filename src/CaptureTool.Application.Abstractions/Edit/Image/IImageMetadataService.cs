using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image;

public interface IImageMetadataService
{
    Size GetImageFileSize(ImageFile imageFile);
}
