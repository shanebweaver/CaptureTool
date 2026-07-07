using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Features.AudioCapture.SelectAudioCaptureInputSource;
using CaptureTool.Application.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.AudioEdit.SaveAudioFile;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class AudioServiceCollectionExtensions
{
    public static IServiceCollection AddAudioCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<AudioCaptureStateStore>();
        services.AddSingleton<AudioCaptureFileNameGenerator>();
        services.AddSingleton<AudioCaptureWorkflow>();
        services.AddSingleton<IAudioCaptureWorkflow>(provider => provider.GetRequiredService<AudioCaptureWorkflow>());
        services.AddSingleton<IAudioCaptureState>(provider => provider.GetRequiredService<AudioCaptureWorkflow>());
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
