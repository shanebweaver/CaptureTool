using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Presentation.Shared.Commands;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.Home;

public sealed partial class HomePageViewModel : ViewModelBase
{
    public IRelayCommand NewImageCaptureCommand { get; }
    public IRelayCommand NewVideoCaptureCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }

    public bool IsAudioCaptureEnabled { get; }

    public HomePageViewModel(
        OpenSelectionOverlayUseCase openSelectionOverlayCommand,
        OpenAudioCapturePageUseCase openAudioCapturePageCommand,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        IsAudioCaptureEnabled = featureManager.IsEnabled(AppFeatures.Feature_AudioCapture);

        NewImageCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.ImageDefault), telemetryService);
        NewVideoCaptureCommand = openSelectionOverlayCommand.ToRelayCommand(() => new OpenSelectionOverlayRequest(CaptureOptions.VideoDefault), telemetryService);
        NewAudioCaptureCommand = openAudioCapturePageCommand.ToRelayCommand(() => new OpenAudioCapturePageRequest(), telemetryService);
    }
}
