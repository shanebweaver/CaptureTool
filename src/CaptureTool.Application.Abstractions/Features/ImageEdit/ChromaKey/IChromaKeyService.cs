using CaptureTool.Domain.Capture.Files;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;

public partial interface IChromaKeyService
{
    Task<Color[]> GetTopColorsAsync(IFile image, uint count = 3, byte quantizeStep = 8);
}
