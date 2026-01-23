using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IVideoEditPageViewModel
{
    ICommand SaveCommand { get; }
    ICommand CopyCommand { get; }
    string? VideoPath { get; }
    bool IsVideoReady { get; }
    bool IsFinalizingVideo { get; }
    
    void Load(IVideoFile video);
}
