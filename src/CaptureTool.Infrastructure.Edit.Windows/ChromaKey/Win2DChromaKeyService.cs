using CaptureTool.Domain.Capture.Files;
using CaptureTool.Domain.Edit.ChromaKey;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.ChromaKey;

public sealed partial class Win2DChromaKeyService : IChromaKeyService
{
    public async Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8)
    {
        return await ChromaKeyColorHelper.GetTopColorsAsync(image.FilePath, count, quantizeStep);
    }
}
