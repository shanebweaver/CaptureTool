using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IVideoEditPageViewModel
{
    AsyncRelayCommand SaveCommand { get; }
    AsyncRelayCommand CopyCommand { get; }
    string? VideoPath { get; }
    bool IsVideoReady { get; }
    bool IsFinalizingVideo { get; }
    
    void Load(VideoFile videoFile);
}
