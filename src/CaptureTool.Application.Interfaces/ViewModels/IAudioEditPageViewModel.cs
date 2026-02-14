using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAudioEditPageViewModel : IViewModel
{
    IAsyncAppCommand SaveCommand { get; }
    IAsyncAppCommand CopyCommand { get; }
    string? AudioPath { get; }
    bool IsAudioReady { get; }

    void Load(IAudioFile audio);
}
