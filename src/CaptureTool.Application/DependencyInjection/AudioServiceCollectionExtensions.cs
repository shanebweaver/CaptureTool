using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Edit.Audio.SaveAudioFile;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class AudioServiceCollectionExtensions
{
    public static IServiceCollection AddAudioCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<AudioCaptureStateStore>();
        services.AddSingleton<AudioCaptureFileNameGenerator>();
        services.AddSingleton<AudioCapturePostProcessor>();
        services.AddSingleton<AudioCaptureWorkflow>();
        services.AddSingleton<IAudioCaptureWorkflow>(provider => provider.GetRequiredService<AudioCaptureWorkflow>());
        services.AddSingleton<IAudioCaptureState>(provider => provider.GetRequiredService<AudioCaptureWorkflow>());
        services.AddTransient<ICancelAudioCaptureUseCase, CancelAudioCaptureUseCase>();
        services.AddTransient<IStartAudioCaptureUseCase, StartAudioCaptureUseCase>();
        services.AddTransient<IStopAudioCaptureUseCase, StopAudioCaptureUseCase>();
        services.AddTransient<IPauseAudioCaptureUseCase, PauseAudioCaptureUseCase>();
        services.AddTransient<IMuteAudioCaptureUseCase, MuteAudioCaptureUseCase>();
        services.AddTransient<ISelectAudioCaptureInputSourceUseCase, SelectAudioCaptureInputSourceUseCase>();
        services.AddTransient<IToggleLocalAudioCaptureUseCase, ToggleLocalAudioCaptureUseCase>();
        services.AddTransient<IOpenAudioCapturePageUseCase, OpenAudioCapturePageUseCase>();
        services.AddTransient<IAudioCaptureNavigationGuard, AudioCaptureNavigationGuard>();

        return services;
    }

    public static IServiceCollection AddAudioEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<ISaveAudioFileUseCase, SaveAudioFileUseCase>();
        services.AddTransient<ICopyAudioFileUseCase, CopyAudioFileUseCase>();
        services.AddTransient<IOpenAudioEditPageUseCase, OpenAudioEditPageUseCase>();

        return services;
    }
}
