using CaptureTool.Common.Storage;
using CaptureTool.Edit.ChromaKey;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.Edit.Windows.ChromaKey;

public sealed partial class Win2DChromaKeyService : IChromaKeyService
{
    public async Task<Color[]> GetTopColorsAsync(ImageFile image, uint count = 3, byte quantizeStep = 8)
    {
        return await ChromaKeyColorHelper.GetTopColorsAsync(image.Path, count, quantizeStep);
    }
}
