using CaptureTool.Domain.Capture.Abstractions.Files;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Abstractions.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8);
}
