using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Home;

public sealed partial class HomePageViewModel : ViewModelBase
{
    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }

    public bool IsAudioCaptureEnabled { get; }

    public HomePageViewModel(
        IOpenSelectionOverlayUseCase openSelectionOverlayCommand,
        IOpenAudioCapturePageUseCase openAudioCapturePageCommand,
        IAudioCaptureFeatureAvailability audioCaptureFeatureAvailability)
    {
        IsAudioCaptureEnabled = audioCaptureFeatureAvailability.IsAudioCaptureEnabled;

        NewImageCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault));
        NewVideoCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault));
        NewAudioCaptureCommand = openAudioCapturePageCommand.ToRelayCommand(() => new OpenAudioCapturePageRequest());
    }
}
