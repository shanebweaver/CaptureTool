using CaptureTool.Services.Interfaces.Storage;
using System.Drawing;

namespace CaptureTool.Domains.Edit.Interfaces.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8);
}
