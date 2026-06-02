using CaptureTool.Domain.Capture.Files;
using System.Drawing;

namespace CaptureTool.Domain.Edit.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8);
}
