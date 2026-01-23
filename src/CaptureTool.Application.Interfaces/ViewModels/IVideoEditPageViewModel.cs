using CaptureTool.Common.Commands;
using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IVideoEditPageViewModel
{
    IAsyncCommand SaveCommand { get; }
    IAsyncCommand CopyCommand { get; }
    string? VideoPath { get; }
    bool IsVideoReady { get; }
    bool IsFinalizingVideo { get; }
    
    void Load(IVideoFile video);
}
