using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Features.CaptureOverlay.CancelVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.CaptureOverlay.PrepareVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Features.CaptureOverlay.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class CaptureOverlayServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureOverlayUseCases(this IServiceCollection services)
    {
        services.AddTransient<ICancelVideoCaptureUseCase, CancelVideoCaptureUseCase>();
        services.AddTransient<ICloseCaptureOverlayUseCase, CloseCaptureOverlayUseCase>();
        services.AddTransient<IGetAudioInputSourcesUseCase, GetAudioInputSourcesUseCase>();
        services.AddTransient<IGoBackFromCaptureOverlayUseCase, GoBackFromCaptureOverlayUseCase>();
        services.AddTransient<IOpenCaptureOverlayUseCase, OpenCaptureOverlayUseCase>();
        services.AddTransient<IOpenSelectionOverlayUseCase, OpenSelectionOverlayUseCase>();
        services.AddTransient<IPrepareVideoCaptureUseCase, PrepareVideoCaptureUseCase>();
        services.AddTransient<ISelectAudioInputSourceUseCase, SelectAudioInputSourceUseCase>();
        services.AddTransient<ISetVideoCaptureAudioInputMutedUseCase, SetVideoCaptureAudioInputMutedUseCase>();
        services.AddTransient<IStartVideoCaptureUseCase, StartVideoCaptureUseCase>();
        services.AddTransient<IStopVideoCaptureUseCase, StopVideoCaptureUseCase>();
        services.AddTransient<IToggleVideoCaptureDesktopAudioUseCase, ToggleVideoCaptureDesktopAudioUseCase>();
        services.AddTransient<IToggleVideoCapturePauseResumeUseCase, ToggleVideoCapturePauseResumeUseCase>();

        return services;
    }
}
