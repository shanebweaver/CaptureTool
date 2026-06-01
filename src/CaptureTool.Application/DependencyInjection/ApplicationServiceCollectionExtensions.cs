using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Application.Features.About.OpenAboutPage;
using CaptureTool.Application.Features.Activation;
using CaptureTool.Application.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Features.AppMenu.OpenFile;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Features.Error.RestartApplication;
using CaptureTool.Application.Features.Home.ShowHomePage;
using CaptureTool.Application.Features.ImageCapture;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.RecentCaptures;
using CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Features.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Features.Settings.UpdateAppTheme;
using CaptureTool.Application.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Features.Store.LeaveStorePage;
using CaptureTool.Application.Features.Store.OpenStorePage;
using CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Features.Windowing.ShowMainWindow;
using CaptureTool.Infrastructure.Abstractions.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this ServiceCollection services)
    {
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();

        services.AddTransient<LeaveAboutPageUseCase>();
        services.AddTransient<OpenAboutPageUseCase>();

        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();

        services.AddTransient<ExitApplicationUseCase>();
        services.AddTransient<OpenFileUseCase>();

        services.AddTransient<StartAudioCaptureUseCase>();
        services.AddTransient<StopAudioCaptureUseCase>();
        services.AddTransient<PauseAudioCaptureUseCase>();
        services.AddTransient<MuteAudioCaptureUseCase>();
        services.AddTransient<ToggleLocalAudioCaptureUseCase>();
        services.AddTransient<OpenAudioCapturePageUseCase>();
        services.AddSingleton<IAudioCaptureHandler, AudioCaptureHandler>();

        services.AddTransient<SaveAudioFileUseCase>();
        services.AddTransient<CopyAudioFileUseCase>();
        services.AddTransient<OpenAudioEditPageUseCase>();

        services.AddTransient<CloseCaptureOverlayUseCase>();
        services.AddTransient<GetAudioInputSourcesUseCase>();
        services.AddTransient<GoBackFromCaptureOverlayUseCase>();
        services.AddTransient<OpenCaptureOverlayUseCase>();
        services.AddTransient<OpenSelectionOverlayUseCase>();
        services.AddTransient<SelectAudioInputSourceUseCase>();
        services.AddTransient<StartVideoCaptureUseCase>();
        services.AddTransient<StopVideoCaptureUseCase>();
        services.AddTransient<ToggleVideoCaptureDesktopAudioUseCase>();
        services.AddTransient<ToggleVideoCapturePauseResumeUseCase>();

        services.AddTransient<ClearLogsUseCase>();
        services.AddTransient<GetCurrentLogsUseCase>();
        services.AddTransient<GetIsLoggingEnabledUseCase>();
        services.AddTransient<UpdateLoggingStateUseCase>();

        services.AddTransient<RestartApplicationUseCase>();

        services.AddTransient<ShowHomePageUseCase>();

        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();

        services.AddTransient<OpenImageEditPageUseCase>();

        services.AddTransient<GetRecentCapturesUseCase>();
        services.AddTransient<OpenRecentCaptureUseCase>();

        services.AddTransient<LeaveSettingsPageUseCase>();
        services.AddTransient<RestartSettingsApplicationUseCase>();
        services.AddTransient<UpdateImageAutoCopyUseCase>();
        services.AddTransient<UpdateImageAutoSaveUseCase>();
        services.AddTransient<UpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<UpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<UpdateVideoCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<UpdateAppLanguageUseCase>();
        services.AddTransient<UpdateAppThemeUseCase>();
        services.AddTransient<ChangeScreenshotsFolderUseCase>();
        services.AddTransient<ChangeVideosFolderUseCase>();
        services.AddTransient<ClearTempFilesUseCase>();
        services.AddTransient<RestoreDefaultsUseCase>();
        services.AddTransient<OpenScreenshotsFolderUseCase>();
        services.AddTransient<OpenVideosFolderUseCase>();
        services.AddTransient<OpenTempFolderUseCase>();
        services.AddTransient<OpenSettingsPageUseCase>();

        services.AddTransient<GetChromaKeyAddOnUseCase>();
        services.AddTransient<PurchaseChromaKeyAddOnUseCase>();
        services.AddTransient<OpenStorePageUseCase>();
        services.AddTransient<LeaveStorePageUseCase>();

        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();

        services.AddTransient<CopyVideoFileUseCase>();
        services.AddTransient<SaveVideoFileUseCase>();
        services.AddTransient<OpenVideoEditPageUseCase>();

        services.AddTransient<ShowMainWindowUseCase>();

        return services;
    }
}
