using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Abstractions.Navigation;
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
using CaptureTool.Application.Home;
using CaptureTool.Application.ImageCapture;
using CaptureTool.Application.Navigation;
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
        services.AddTransient<IShowAboutAppCommand, ShowAboutAppCommand>();
        services.AddTransient<IShowSettingsAppCommand, ShowSettingsAppCommand>();
        services.AddTransient<IShowStoreAppCommand, ShowStoreAppCommand>();

        // AudioCapture
        services.AddTransient<IAudioCaptureStartAppCommand, AudioCaptureStartAppCommand>();
        services.AddTransient<IAudioCaptureStopAppCommand, AudioCaptureStopAppCommand>();
        services.AddTransient<IAudioCapturePauseAppCommand, AudioCapturePauseAppCommand>();
        services.AddTransient<IAudioCaptureMuteAppCommand, AudioCaptureMuteAppCommand>();
        services.AddTransient<IAudioCaptureToggleLocalAudioAppCommand, AudioCaptureToggleLocalAudioAppCommand>();
        services.AddTransient<IAudioCaptureHandler, CaptureToolAudioCaptureHandler>();

        // AudioEdit
        services.AddTransient<IAudioEditSaveAppCommand, AudioEditSaveAppCommand>();
        services.AddTransient<IAudioEditCopyAppCommand, AudioEditCopyAppCommand>();

        // CaptureOverlay
        services.AddTransient<ICaptureOverlayCloseAppCommand, CaptureOverlayCloseAppCommand>();
        services.AddTransient<ICaptureOverlayGoBackAppCommand, CaptureOverlayGoBackAppCommand>();
        services.AddTransient<ICaptureOverlayToggleDesktopAudioAppCommand, CaptureOverlayToggleDesktopAudioAppCommand>();
        services.AddTransient<ICaptureOverlayTogglePauseResumeAppCommand, CaptureOverlayTogglePauseResumeAppCommand>();
        services.AddTransient<ICaptureOverlayStartVideoCaptureAppCommand, CaptureOverlayStartVideoCaptureAppCommand>();
        services.AddTransient<ICaptureOverlayStopVideoCaptureAppCommand, CaptureOverlayStopVideoCaptureAppCommand>();

        // Diagnostics
        services.AddTransient<IDiagnosticsClearLogsAppCommand, DiagnosticsClearLogsAppCommand>();
        services.AddTransient<IDiagnosticsGetCurrentLogsAppQuery, DiagnosticsGetCurrentLogsAppQuery>();
        services.AddTransient<IDiagnosticsIsLoggingEnabledAppQuery, DiagnosticsIsLoggingEnabledAppQuery>();
        services.AddTransient<IDiagnosticsUpdateLoggingStateAppCommand, DiagnosticsUpdateLoggingStateAppCommand>();

        // Error
        services.AddTransient<IErrorRestartAppCommand, ErrorRestartAppCommand>();

        // Home
        services.AddTransient<IHomeNewAudioCaptureAppCommand, HomeNewAudioCaptureAppCommand>();
        services.AddTransient<IHomeNewImageCaptureAppCommand, HomeNewImageCaptureAppCommand>();
        services.AddTransient<IHomeNewVideoCaptureAppCommand, HomeNewVideoCaptureAppCommand>();

        // ImageCapture
        services.AddTransient<IImageCaptureHandler, CaptureToolImageCaptureHandler>();

        // Navigation
        services.AddTransient<IAppNavigation, CaptureToolAppNavigation>();

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
        services.AddTransient<ISettingsChangeScreenshotsFolderAppCommand, SettingsChangeScreenshotsFolderAppCommand>();
        services.AddTransient<ISettingsChangeVideosFolderAppCommand, SettingsChangeVideosFolderAppCommand>();
        services.AddTransient<ISettingsClearTempFilesAppCommand, SettingsClearTempFilesAppCommand>();
        services.AddTransient<ISettingsRestoreDefaultsAppCommand, SettingsRestoreDefaultsAppCommand>();
        services.AddTransient<ISettingsOpenScreenshotsFolderAppCommand, SettingsOpenScreenshotsFolderAppCommand>();
        services.AddTransient<ISettingsOpenVideosFolderAppCommand, SettingsOpenVideosFolderAppCommand>();
        services.AddTransient<ISettingsOpenTempFolderAppCommand, SettingsOpenTempFolderAppCommand>();

        // Store
        services.AddTransient<IGetChromaKeyAddOnAppQuery, GetChromaKeyAddOnAppQuery>();
        services.AddTransient<IPurchaseChromaKeyAddOnAppCommand, PurchaseChromaKeyAddOnAppCommand>();

        // VideoCapture
        services.AddTransient<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();

        // VideoEdit
        services.AddTransient<IVideoEditCopyAppCommand, VideoEditCopyAppCommand>();
        services.AddTransient<IVideoEditSaveAppCommand, VideoEditSaveAppCommand>();

        return services;
    }
}
