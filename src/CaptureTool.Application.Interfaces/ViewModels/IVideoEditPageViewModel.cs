using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IVideoEditPageViewModel : IViewModel
{
    IAsyncAppCommand SaveCommand { get; }
    IAsyncAppCommand CopyCommand { get; }
    string? VideoPath { get; }
    bool IsVideoReady { get; }
    bool IsFinalizingVideo { get; }

    void Load(IVideoFile video);
}
