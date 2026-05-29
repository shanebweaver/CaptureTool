using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }

    public bool IsAudioCaptureEnabled { get; }

    public HomePageViewModel(
        IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse> openSelectionOverlayCommand,
        IUseCase<OpenAudioCapturePageRequest, OpenAudioCapturePageResponse> openAudioCapturePageCommand,
        IFeatureManager featureManager)
    {
        IsAudioCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_AudioCapture);

        NewImageCaptureCommand = new RelayCommand(() => openSelectionOverlayCommand.ExecuteAsync(new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault)).GetAwaiter().GetResult());
        NewVideoCaptureCommand = new RelayCommand(() => openSelectionOverlayCommand.ExecuteAsync(new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault)).GetAwaiter().GetResult());
        NewAudioCaptureCommand = openAudioCapturePageCommand.ToRelayCommand(() => new OpenAudioCapturePageRequest());
    }
}
