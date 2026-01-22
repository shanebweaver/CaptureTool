using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.Actions.AppMenu;
using CaptureTool.Application.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Application.Implementations.Actions.CaptureOverlay;
using CaptureTool.Application.Interfaces.Actions.About;
using CaptureTool.Application.Implementations.Actions.About;
using CaptureTool.Application.Interfaces.Actions.AddOns;
using CaptureTool.Application.Implementations.Actions.AddOns;
using CaptureTool.Application.Interfaces.Actions.Diagnostics;
using CaptureTool.Application.Interfaces.Actions.Error;
using CaptureTool.Application.Implementations.Actions.Error;
using CaptureTool.Application.Interfaces.Actions.Loading;
using CaptureTool.Application.Implementations.Actions.Loading;
using CaptureTool.Application.Interfaces.Actions.Home;
using CaptureTool.Application.Implementations.Actions.Home;
using CaptureTool.Application.Interfaces.Actions.VideoEdit;
using CaptureTool.Application.Implementations.Actions.VideoEdit;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Application.Implementations.Actions.Settings;
using CaptureTool.Application.Implementations.Actions.AppMenu;
using CaptureTool.Application.Implementations.Actions.Diagnostics;

namespace CaptureTool.Application.Implementations.DependencyInjection;

public static class CoreActionsServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureOverlayActions(this IServiceCollection services)
    {
        services.AddTransient<ICaptureOverlayCloseAction, CaptureOverlayCloseAction>();
        services.AddTransient<ICaptureOverlayGoBackAction, CaptureOverlayGoBackAction>();
        services.AddTransient<ICaptureOverlayToggleDesktopAudioAction, CaptureOverlayToggleDesktopAudioAction>();
        services.AddTransient<ICaptureOverlayTogglePauseResumeAction, CaptureOverlayTogglePauseResumeAction>();
        services.AddTransient<ICaptureOverlayStartVideoCaptureAction, CaptureOverlayStartVideoCaptureAction>();
        services.AddTransient<ICaptureOverlayStopVideoCaptureAction, CaptureOverlayStopVideoCaptureAction>();
        services.AddTransient<ICaptureOverlayActions, CaptureOverlayActions>();
        return services;
    }

    public static IServiceCollection AddAboutActions(this IServiceCollection services)
    {
        services.AddTransient<IAboutGoBackAction, AboutGoBackAction>();
        return services;
    }

    public static IServiceCollection AddAddOnsActions(this IServiceCollection services)
    {
        services.AddTransient<IAddOnsGoBackAction, AddOnsGoBackAction>();
        return services;
    }

    public static IServiceCollection AddErrorActions(this IServiceCollection services)
    {
        services.AddTransient<IErrorRestartAppAction, ErrorRestartAppAction>();
        return services;
    }

    public static IServiceCollection AddLoadingActions(this IServiceCollection services)
    {
        services.AddTransient<ILoadingGoBackAction, LoadingGoBackAction>();
        return services;
    }

    public static IServiceCollection AddHomeActions(this IServiceCollection services)
    {
        services.AddTransient<IHomeNewImageCaptureAction, HomeNewImageCaptureAction>();
        services.AddTransient<IHomeNewVideoCaptureAction, HomeNewVideoCaptureAction>();
        return services;
    }

    public static IServiceCollection AddVideoEditActions(this IServiceCollection services)
    {
        services.AddTransient<IVideoEditSaveAction, VideoEditSaveAction>();
        services.AddTransient<IVideoEditCopyAction, VideoEditCopyAction>();
        return services;
    }

    public static IServiceCollection AddSettingsActions(this IServiceCollection services)
    {
        services.AddTransient<ISettingsGoBackAction, SettingsGoBackAction>();
        services.AddTransient<ISettingsRestartAppAction, SettingsRestartAppAction>();
        services.AddTransient<ISettingsUpdateImageAutoCopyAction, SettingsUpdateImageAutoCopyAction>();
        services.AddTransient<ISettingsUpdateImageAutoSaveAction, SettingsUpdateImageAutoSaveAction>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoCopyAction, SettingsUpdateVideoCaptureAutoCopyAction>();
        services.AddTransient<ISettingsUpdateVideoCaptureAutoSaveAction, SettingsUpdateVideoCaptureAutoSaveAction>();
        services.AddTransient<ISettingsUpdateAppLanguageAction, SettingsUpdateAppLanguageAction>();
        services.AddTransient<ISettingsUpdateAppThemeAction, SettingsUpdateAppThemeAction>();
        services.AddTransient<ISettingsChangeScreenshotsFolderAction, SettingsChangeScreenshotsFolderAction>();
        services.AddTransient<ISettingsChangeVideosFolderAction, SettingsChangeVideosFolderAction>();
        services.AddTransient<ISettingsClearTempFilesAction, SettingsClearTempFilesAction>();
        services.AddTransient<ISettingsRestoreDefaultsAction, SettingsRestoreDefaultsAction>();
        return services;
    }

    public static IServiceCollection AddAppMenuActions(this IServiceCollection services)
    {
        services.AddTransient<IAppMenuActions, AppMenuActions>();
        return services;
    }

    public static IServiceCollection AddDiagnosticsActions(this IServiceCollection services)
    {
        services.AddTransient<IDiagnosticsActions, DiagnosticsActions>();
        return services;
    }
}
