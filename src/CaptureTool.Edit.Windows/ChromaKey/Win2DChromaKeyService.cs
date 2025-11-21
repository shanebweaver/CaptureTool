using CaptureTool.Edit.ChromaKey;
using CaptureTool.Services.Interfaces.Storage;
using System.Drawing;

namespace CaptureTool.Edit.Windows.ChromaKey;

public sealed partial class Win2DChromaKeyService : IChromaKeyService
{
    public async Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8)
    {
        return await ChromaKeyColorHelper.GetTopColorsAsync(image.FilePath, count, quantizeStep);
    }
}
