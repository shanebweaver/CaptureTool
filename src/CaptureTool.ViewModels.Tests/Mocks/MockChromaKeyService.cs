using CaptureTool.Common.Storage;
using CaptureTool.Edit.ChromaKey;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockChromaKeyService : IChromaKeyService
{
    public Task<Color[]> GetTopColorsAsync(ImageFile image, uint count = 3, byte quantizeStep = 8)
    {
        Color[] result = [Color.Red, Color.Blue, Color.Green];
        return Task.FromResult(result);
    }
}
