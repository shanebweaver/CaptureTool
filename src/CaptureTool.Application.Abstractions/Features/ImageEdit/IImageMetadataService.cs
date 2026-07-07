using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit;

public interface IImageMetadataService
{
    Size GetImageFileSize(ImageFile imageFile);
}
