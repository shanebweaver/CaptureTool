using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.GetAudioInputSources;
using CaptureTool.Application.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class CaptureOverlayServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureOverlayUseCases(this IServiceCollection services)
    {
        services.AddTransient<ICloseCaptureOverlayUseCase, CloseCaptureOverlayUseCase>();
        services.AddTransient<IGetAudioInputSourcesUseCase, GetAudioInputSourcesUseCase>();
        services.AddTransient<IGoBackFromCaptureOverlayUseCase, GoBackFromCaptureOverlayUseCase>();
        services.AddTransient<IOpenCaptureOverlayUseCase, OpenCaptureOverlayUseCase>();
        services.AddTransient<IOpenSelectionOverlayUseCase, OpenSelectionOverlayUseCase>();

        return services;
    }
}
