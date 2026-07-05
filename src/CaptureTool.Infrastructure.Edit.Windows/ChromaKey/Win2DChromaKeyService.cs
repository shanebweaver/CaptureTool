using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.ChromaKey;

public sealed partial class Win2DChromaKeyService : IChromaKeyService
{
    public async Task<Color[]> GetTopColorsAsync(ImageFile image, uint count = 3, byte quantizeStep = 8)
    {
        return await ChromaKeyColorHelper.GetTopColorsAsync(image.FilePath, count, quantizeStep);
    }
}
