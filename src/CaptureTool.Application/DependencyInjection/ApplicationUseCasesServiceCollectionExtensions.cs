using CaptureTool.Application.About;
using CaptureTool.Application.Abstractions.About;
using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Abstractions.ImageEdit;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Application.Activation;
using CaptureTool.Application.AppMenu;
using CaptureTool.Application.AudioCapture;
using CaptureTool.Application.AudioEdit;
using CaptureTool.Application.CaptureOverlay;
using CaptureTool.Application.Diagnostics;
using CaptureTool.Application.Error;
using CaptureTool.Application.ImageCapture;
using CaptureTool.Application.ImageEdit;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.RecentCaptures;
using CaptureTool.Application.Settings;
using CaptureTool.Application.Store;
using CaptureTool.Application.VideoCapture;
using CaptureTool.Application.VideoEdit;
using CaptureTool.Infrastructure.Abstractions.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationAppCommandServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this ServiceCollection services)
    {
        // Activation
        services.AddTransient<IActivationHandler, CaptureToolActivationHandler>();

        // AppMenu
        services.AddTransient<IExitApplicationAppCommand, ExitApplicationAppCommand>();
        services.AddTransient<IGetRecentCapturesAppQuery, GetRecentCapturesAppQuery>();
        services.AddTransient<INewImageCaptureAppCommand, NewImageCaptureAppCommand>();
        services.AddTransient<INewVideoCaptureAppCommand, NewVideoCaptureAppCommand>();
        services.AddTransient<IOpenFileAsyncAppCommand, OpenFileAsyncAppCommand>();
        services.AddTransient<IOpenRecentCaptureAppCommand, OpenRecentCaptureAppCommand>();
        services.AddTransient<IOpenAboutPageAppCommand, OpenAboutPageAppCommand>();
        services.AddTransient<IOpenSettingsPageAppCommand, OpenSettingsPageAppCommand>();

        // AudioCapture
        services.AddTransient<IStartAudioCaptureAppCommand, StartAudioCaptureAppCommand>();
        services.AddTransient<IStopAudioCaptureAppCommand, StopAudioCaptureAppCommand>();
        services.AddTransient<IPauseAudioCaptureAppCommand, PauseAudioCaptureAppCommand>();
        services.AddTransient<IMuteAudioCaptureAppCommand, MuteAudioCaptureAppCommand>();
        services.AddTransient<IToggleLocalAudioCaptureAppCommand, ToggleLocalAudioCaptureAppCommand>();
        services.AddTransient<IAudioCaptureHandler, CaptureToolAudioCaptureHandler>();

        // AudioEdit
        services.AddTransient<ISaveAudioFileAppCommand, SaveAudioFileAppCommand>();
        services.AddTransient<ICopyAudioFileAppCommand, CopyAudioFileAppCommand>();
        services.AddTransient<IOpenAudioEditPageAppCommand, OpenAudioEditPageAppCommand>();

        // CaptureOverlay
        services.AddTransient<ICaptureOverlayCloseAppCommand, CaptureOverlayCloseAppCommand>();
        services.AddTransient<ICaptureOverlayGoBackAppCommand, CaptureOverlayGoBackAppCommand>();
        services.AddTransient<IToggleVideoCaptureDesktopAudioAppCommand, ToggleVideoCaptureDesktopAudioAppCommand>();
        services.AddTransient<IToggleVideoCapturePauseResumeAppCommand, ToggleVideoCapturePauseResumeAppCommand>();
        services.AddTransient<IStartVideoCaptureAppCommand, StartVideoCaptureAppCommand>();
        services.AddTransient<IStopVideoCaptureAppCommand, StopVideoCaptureAppCommand>();

        // Diagnostics
        services.AddTransient<IClearLogsAppCommand, ClearLogsAppCommand>();
        services.AddTransient<IGetCurrentLogsAppQuery, GetCurrentLogsAppQuery>();
        services.AddTransient<IGetIsLoggingEnabledAppQuery, GetIsLoggingEnabledAppQuery>();
        services.AddTransient<IUpdateLoggingStateAppCommand, UpdateLoggingStateAppCommand>();

        // Error
        services.AddTransient<IRestartApplicationAppCommand, RestartApplicationAppCommand>();

        // ImageCapture
        services.AddTransient<IImageCaptureHandler, CaptureToolImageCaptureHandler>();
        services.AddTransient<INewImageCaptureAppCommand, NewImageCaptureAppCommand>();

        // ImageEdit
        services.AddTransient<IOpenImageEditPageAppCommand, OpenImageEditPageAppCommand>();

        // Settings
        services.AddTransient<ISettingsGoBackAppCommand, SettingsGoBackAppCommand>();
        services.AddTransient<ISettingsRestartApplicationAppCommand, SettingsRestartApplicationAppCommand>();
        services.AddTransient<ISettingsUpdateImageAutoCopyAppCommand, SettingsUpdateImageAutoCopyAppCommand>();
        services.AddTransient<ISettingsUpdateImageAutoSaveAppCommand, SettingsUpdateImageAutoSaveAppCommand>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoCopyAppCommand, SettingsUpdateVideoCaptureAutoCopyAppCommand>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoSaveAppCommand, SettingsUpdateVideoCaptureAutoSaveAppCommand>();
        services.AddTransient<ISettingsUpdateVideoCaptureDefaultLocalAudioAppCommand, SettingsUpdateVideoCaptureDefaultLocalAudioAppCommand>();
        services.AddTransient<ISettingsUpdateVideoMetadataAutoSaveAppCommand, SettingsUpdateVideoMetadataAutoSaveAppCommand>();
        services.AddTransient<ISettingsUpdateAppLanguageAppCommand, SettingsUpdateAppLanguageAppCommand>();
        services.AddTransient<ISettingsUpdateAppThemeAppCommand, SettingsUpdateAppThemeAppCommand>();
        services.AddTransient<IChangeScreenshotsFolderAppCommand, ChangeScreenshotsFolderAppCommand>();
        services.AddTransient<ISettingsChangeVideosFolderAppCommand, SettingsChangeVideosFolderAppCommand>();
        services.AddTransient<ISettingsClearTempFilesAppCommand, SettingsClearTempFilesAppCommand>();
        services.AddTransient<ISettingsRestoreDefaultsAppCommand, SettingsRestoreDefaultsAppCommand>();
        services.AddTransient<ISettingsOpenScreenshotsFolderAppCommand, SettingsOpenScreenshotsFolderAppCommand>();
        services.AddTransient<ISettingsOpenVideosFolderAppCommand, SettingsOpenVideosFolderAppCommand>();
        services.AddTransient<ISettingsOpenTempFolderAppCommand, SettingsOpenTempFolderAppCommand>();
        services.AddTransient<IOpenSettingsPageAppCommand, OpenSettingsPageAppCommand>();

        // Store
        services.AddTransient<IGetChromaKeyAddOnAppQuery, GetChromaKeyAddOnAppQuery>();
        services.AddTransient<IPurchaseChromaKeyAddOnAppCommand, PurchaseChromaKeyAddOnAppCommand>();
        services.AddTransient<IOpenStorePageAppCommand, OpenStorePageAppCommand>();

        // VideoCapture
        services.AddTransient<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();
        services.AddTransient<INewVideoCaptureAppCommand, NewVideoCaptureAppCommand>();

        // VideoEdit
        services.AddTransient<ICopyVideoFileAppCommand, CopyVideoFileAppCommand>();
        services.AddTransient<ISaveVideoFileAppCommand, SaveVideoFileAppCommand>();
        services.AddTransient<IOpenVideoEditPageAppCommand, OpenVideoEditPageAppCommand>();

        return services;
    }
}
