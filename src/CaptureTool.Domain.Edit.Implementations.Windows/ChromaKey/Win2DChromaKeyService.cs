using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Implementations.Windows.ChromaKey;

public sealed partial class Win2DChromaKeyService : IChromaKeyService
{
    public async Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8)
    {
        return await ChromaKeyColorHelper.GetTopColorsAsync(image.FilePath, count, quantizeStep);
    }
}
