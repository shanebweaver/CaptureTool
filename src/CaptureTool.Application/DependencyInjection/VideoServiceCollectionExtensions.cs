using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureDesktopAudioVolume;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Capture.Video.SetVideoCaptureDesktopAudioVolume;
using CaptureTool.Application.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Edit.Video.SaveVideoFile;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class VideoServiceCollectionExtensions
{
    public static IServiceCollection AddVideoCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<VideoCaptureStateStore>();
        services.AddSingleton<VideoCaptureFileNameGenerator>();
        services.AddSingleton<VideoCapturePostProcessor>();
        services.AddSingleton<VideoCaptureWorkflow>();
        services.AddSingleton<IVideoCaptureWorkflow>(provider => provider.GetRequiredService<VideoCaptureWorkflow>());
        services.AddSingleton<IVideoCaptureState>(provider => provider.GetRequiredService<VideoCaptureWorkflow>());
        services.AddTransient<ICancelVideoCaptureUseCase, CancelVideoCaptureUseCase>();
        services.AddTransient<IPrepareVideoCaptureUseCase, PrepareVideoCaptureUseCase>();
        services.AddTransient<ISelectAudioInputSourceUseCase, SelectAudioInputSourceUseCase>();
        services.AddTransient<ISetVideoCaptureDesktopAudioVolumeUseCase, SetVideoCaptureDesktopAudioVolumeUseCase>();
        services.AddTransient<ISetVideoCaptureAudioInputMutedUseCase, SetVideoCaptureAudioInputMutedUseCase>();
        services.AddTransient<IStartVideoCaptureUseCase, StartVideoCaptureUseCase>();
        services.AddTransient<IStopVideoCaptureUseCase, StopVideoCaptureUseCase>();
        services.AddTransient<IToggleVideoCaptureDesktopAudioUseCase, ToggleVideoCaptureDesktopAudioUseCase>();
        services.AddTransient<IToggleVideoCapturePauseResumeUseCase, ToggleVideoCapturePauseResumeUseCase>();

        return services;
    }

    public static IServiceCollection AddVideoEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<ICopyVideoFileUseCase, CopyVideoFileUseCase>();
        services.AddTransient<ISaveVideoFileUseCase, SaveVideoFileUseCase>();
        services.AddTransient<IOpenVideoEditPageUseCase, OpenVideoEditPageUseCase>();

        return services;
    }
}
