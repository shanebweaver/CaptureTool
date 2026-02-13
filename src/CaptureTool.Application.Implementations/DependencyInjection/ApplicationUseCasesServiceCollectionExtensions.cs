using CaptureTool.Application.Implementations.UseCases.About;
using CaptureTool.Application.Implementations.UseCases.AddOns;
using CaptureTool.Application.Implementations.UseCases.AppMenu;
using CaptureTool.Application.Implementations.UseCases.CaptureOverlay;
using CaptureTool.Application.Implementations.UseCases.Diagnostics;
using CaptureTool.Application.Implementations.UseCases.Error;
using CaptureTool.Application.Implementations.UseCases.Home;
using CaptureTool.Application.Implementations.UseCases.Loading;
using CaptureTool.Application.Implementations.UseCases.Settings;
using CaptureTool.Application.Implementations.UseCases.VideoEdit;
using CaptureTool.Application.Interfaces.UseCases.About;
using CaptureTool.Application.Interfaces.UseCases.AddOns;
using CaptureTool.Application.Interfaces.UseCases.AppMenu;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Application.Interfaces.UseCases.Diagnostics;
using CaptureTool.Application.Interfaces.UseCases.Error;
using CaptureTool.Application.Interfaces.UseCases.Home;
using CaptureTool.Application.Interfaces.UseCases.Loading;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.UseCases.VideoEdit;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.Implementations.DependencyInjection;

public static class ApplicationUseCasesServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureOverlayUseCases(this IServiceCollection services)
    {
        services.AddTransient<ICaptureOverlayCloseUseCase, CaptureOverlayCloseUseCase>();
        services.AddTransient<ICaptureOverlayGoBackUseCase, CaptureOverlayGoBackUseCase>();
        services.AddTransient<ICaptureOverlayToggleDesktopAudioUseCase, CaptureOverlayToggleDesktopAudioUseCase>();
        services.AddTransient<ICaptureOverlayTogglePauseResumeUseCase, CaptureOverlayTogglePauseResumeUseCase>();
        services.AddTransient<ICaptureOverlayStartVideoCaptureUseCase, CaptureOverlayStartVideoCaptureUseCase>();
        services.AddTransient<ICaptureOverlayStopVideoCaptureUseCase, CaptureOverlayStopVideoCaptureUseCase>();
        services.AddTransient<ICaptureOverlayUseCases, CaptureOverlayUseCases>();
        return services;
    }

    public static IServiceCollection AddAboutUseCases(this IServiceCollection services)
    {
        services.AddTransient<IAboutGoBackUseCase, AboutGoBackUseCase>();
        return services;
    }

    public static IServiceCollection AddAddOnsUseCases(this IServiceCollection services)
    {
        services.AddTransient<IAddOnsGoBackUseCase, AddOnsGoBackUseCase>();
        return services;
    }

    public static IServiceCollection AddErrorUseCases(this IServiceCollection services)
    {
        services.AddTransient<IErrorRestartAppUseCase, ErrorRestartAppUseCase>();
        return services;
    }

    public static IServiceCollection AddLoadingUseCases(this IServiceCollection services)
    {
        services.AddTransient<ILoadingGoBackUseCase, LoadingGoBackUseCase>();
        return services;
    }

    public static IServiceCollection AddHomeUseCases(this IServiceCollection services)
    {
        services.AddTransient<IHomeNewImageCaptureUseCase, HomeNewImageCaptureUseCase>();
        services.AddTransient<IHomeNewVideoCaptureUseCase, HomeNewVideoCaptureUseCase>();
        services.AddTransient<IHomeNewAudioCaptureUseCase, HomeNewAudioCaptureUseCase>();
        return services;
    }

    public static IServiceCollection AddVideoEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<IVideoEditSaveUseCase, VideoEditSaveUseCase>();
        services.AddTransient<IVideoEditCopyUseCase, VideoEditCopyUseCase>();
        return services;
    }

    public static IServiceCollection AddSettingsUseCases(this IServiceCollection services)
    {
        services.AddTransient<ISettingsGoBackUseCase, SettingsGoBackUseCase>();
        services.AddTransient<ISettingsRestartAppUseCase, SettingsRestartAppUseCase>();
        services.AddTransient<ISettingsUpdateImageAutoCopyUseCase, SettingsUpdateImageAutoCopyUseCase>();
        services.AddTransient<ISettingsUpdateImageAutoSaveUseCase, SettingsUpdateImageAutoSaveUseCase>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoCopyUseCase, SettingsUpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoSaveUseCase, SettingsUpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<ISettingsUpdateVideoMetadataAutoSaveUseCase, SettingsUpdateVideoMetadataAutoSaveUseCase>();
        services.AddTransient<ISettingsUpdateAppLanguageUseCase, SettingsUpdateAppLanguageUseCase>();
        services.AddTransient<ISettingsUpdateAppThemeUseCase, SettingsUpdateAppThemeUseCase>();
        services.AddTransient<ISettingsChangeScreenshotsFolderUseCase, SettingsChangeScreenshotsFolderUseCase>();
        services.AddTransient<ISettingsChangeVideosFolderUseCase, SettingsChangeVideosFolderUseCase>();
        services.AddTransient<ISettingsClearTempFilesUseCase, SettingsClearTempFilesUseCase>();
        services.AddTransient<ISettingsRestoreDefaultsUseCase, SettingsRestoreDefaultsUseCase>();
        services.AddTransient<ISettingsOpenScreenshotsFolderUseCase, SettingsOpenScreenshotsFolderUseCase>();
        services.AddTransient<ISettingsOpenVideosFolderUseCase, SettingsOpenVideosFolderUseCase>();
        services.AddTransient<ISettingsOpenTempFolderUseCase, SettingsOpenTempFolderUseCase>();
        return services;
    }

    public static IServiceCollection AddAppMenuUseCases(this IServiceCollection services)
    {
        services.AddTransient<IAppMenuUseCases, AppMenuUseCases>();
        return services;
    }

    public static IServiceCollection AddDiagnosticsUseCases(this IServiceCollection services)
    {
        services.AddTransient<IDiagnosticsUseCases, DiagnosticsUseCases>();
        return services;
    }
}
