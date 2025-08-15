using CaptureTool.Common.Storage;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.Edit.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(ImageFile image, uint count = 3, byte quantizeStep = 8);
}
