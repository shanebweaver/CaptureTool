using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(ImageFile image, uint count = 3, byte quantizeStep = 8);
}
