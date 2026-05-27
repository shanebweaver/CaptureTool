using CaptureTool.Application.Abstractions.About;
using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Abstractions.ImageEdit;
using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.UseCases.About;
using CaptureTool.Application.UseCases.Activation;
using CaptureTool.Application.UseCases.AppMenu;
using CaptureTool.Application.UseCases.AudioCapture;
using CaptureTool.Application.UseCases.AudioEdit;
using CaptureTool.Application.UseCases.CaptureOverlay;
using CaptureTool.Application.UseCases.Diagnostics;
using CaptureTool.Application.UseCases.Error;
using CaptureTool.Application.UseCases.Home;
using CaptureTool.Application.UseCases.ImageCapture;
using CaptureTool.Application.UseCases.ImageEdit;
using CaptureTool.Application.UseCases.RecentCaptures;
using CaptureTool.Application.UseCases.Settings;
using CaptureTool.Application.UseCases.Store;
using CaptureTool.Application.UseCases.VideoCapture;
using CaptureTool.Application.UseCases.VideoEdit;
using CaptureTool.Application.UseCases.Windowing;
using CaptureTool.Infrastructure.Abstractions.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this ServiceCollection services)
    {
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();

        // About
        services.AddTransient<ILeaveAboutPageAppCommand, LeaveAboutPage>();
        services.AddTransient<IOpenAboutPageAppCommand, OpenAboutPage>();

        // Activation
        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();

        // AppMenu
        services.AddTransient<IExitApplicationAppCommand, ExitApplicationAppCommand>();
        services.AddTransient<IOpenFileAsyncAppCommand, OpenFileAsyncAppCommand>();

        // AudioCapture
        services.AddTransient<IStartAudioCaptureAppCommand, StartAudioCapture>();
        services.AddTransient<IStopAudioCaptureAppCommand, StopAudioCapture>();
        services.AddTransient<IPauseAudioCaptureAppCommand, PauseAudioCapture>();
        services.AddTransient<IMuteAudioCaptureAppCommand, MuteAudioCapture>();
        services.AddTransient<IToggleLocalAudioCaptureAppCommand, ToggleLocalAudioCapture>();
        services.AddTransient<IOpenAudioCapturePageAppCommand, OpenAudioCapturePage>();
        services.AddSingleton<IAudioCaptureHandler, AudioCaptureHandler>();

        // AudioEdit
        services.AddTransient<ISaveAudioFileAppCommand, SaveAudioFileAppCommand>();
        services.AddTransient<ICopyAudioFileAppCommand, CopyAudioFileAppCommand>();
        services.AddTransient<IOpenAudioEditPageAppCommand, OpenAudioEditPageAppCommand>();

        // CaptureOverlay
        services.AddTransient<ICaptureOverlayCloseAppCommand, CaptureOverlayCloseAppCommand>();
        services.AddTransient<ICaptureOverlayGoBackAppCommand, CaptureOverlayGoBackAppCommand>();
        services.AddTransient<IOpenCaptureOverlayAppCommand, OpenCaptureOverlayAppCommand>();
        services.AddTransient<IOpenSelectionOverlayAppCommand, OpenSelectionOverlayAppCommand>();
        services.AddTransient<IStartVideoCaptureAppCommand, StartVideoCaptureAppCommand>();
        services.AddTransient<IStopVideoCaptureAppCommand, StopVideoCaptureAppCommand>();
        services.AddTransient<IToggleVideoCaptureDesktopAudioAppCommand, ToggleVideoCaptureDesktopAudioAppCommand>();
        services.AddTransient<IToggleVideoCapturePauseResumeAppCommand, ToggleVideoCapturePauseResumeAppCommand>();

        // Diagnostics
        services.AddTransient<IClearLogsAppCommand, ClearLogsAppCommand>();
        services.AddTransient<IGetCurrentLogsAppQuery, GetCurrentLogsAppQuery>();
        services.AddTransient<IGetIsLoggingEnabledAppQuery, GetIsLoggingEnabledAppQuery>();
        services.AddTransient<IUpdateLoggingStateAppCommand, UpdateLoggingStateAppCommand>();

        // Error
        services.AddTransient<IRestartApplicationAppCommand, RestartApplicationAppCommand>();

        // Home
        services.AddTransient<IShowHomePageAppCommand, ShowHomePageAppCommand>();

        // ImageCapture
        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();

        // ImageEdit
        services.AddTransient<IOpenImageEditPageAppCommand, OpenImageEditPageAppCommand>();

        // RecentCaptures
        services.AddTransient<IGetRecentCapturesAppQuery, GetRecentCapturesAppQuery>();
        services.AddTransient<IOpenRecentCaptureAppCommand, OpenRecentCaptureAppCommand>();
        services.AddTransient<IOpenAboutPageAppCommand, OpenAboutPage>();
        services.AddTransient<IOpenSettingsPageAppCommand, OpenSettingsPageAppCommand>();

        // Settings
        services.AddTransient<ILeaveSettingsPageAppCommand, LeaveSettingsPageAppCommand>();
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
        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();

        // VideoEdit
        services.AddTransient<ICopyVideoFileAppCommand, CopyVideoFileAppCommand>();
        services.AddTransient<ISaveVideoFileAppCommand, SaveVideoFileAppCommand>();
        services.AddTransient<IOpenVideoEditPageAppCommand, OpenVideoEditPageAppCommand>();

        // Windowing
        services.AddTransient<IShowMainWindowAppCommand, ShowMainWindowAppCommand>();

        return services;
    }
}
