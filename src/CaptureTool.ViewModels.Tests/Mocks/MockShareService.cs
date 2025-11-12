using CaptureTool.Services.Share;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockShareService : IShareService
{
    public Task ShareAsync(string filePath, nint hwnd)
    {
        return Task.CompletedTask;
    }
}
