using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Application.Abstractions.CaptureOverlay;
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
        IOpenSelectionOverlayAppCommand openSelectionOverlayAppCommand,
        IOpenAudioCapturePageAppCommand openAudioCapturePageAppCommand,
        IFeatureManager featureManager)
    {
        IsAudioCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_AudioCapture);

        NewImageCaptureCommand = new RelayCommand(() => openSelectionOverlayAppCommand.Execute(CaptureOptions.ImageDefault));
        NewVideoCaptureCommand = new RelayCommand(() => openSelectionOverlayAppCommand.Execute(CaptureOptions.VideoDefault));
        NewAudioCaptureCommand = openAudioCapturePageAppCommand.ToRelayCommand();
    }
}
