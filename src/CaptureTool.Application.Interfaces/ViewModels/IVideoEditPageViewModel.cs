using CaptureTool.Common.Commands;
using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IVideoEditPageViewModel
{
    AsyncRelayCommand SaveCommand { get; }
    AsyncRelayCommand CopyCommand { get; }
    string? VideoPath { get; }
    bool IsVideoReady { get; }
    bool IsFinalizingVideo { get; }
    
    void Load(IVideoFile video);
}
