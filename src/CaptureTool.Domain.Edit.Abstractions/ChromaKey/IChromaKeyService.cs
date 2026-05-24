using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Drawing;

namespace CaptureTool.Domain.Edit.Abstractions.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8);
}
