using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Core.Interfaces;
using CaptureTool.Core.Interfaces.Actions.AppMenu;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Actions.About;
using CaptureTool.Core.Implementations.Actions.About;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Core.Implementations.Actions.AddOns;
using CaptureTool.Core.Interfaces.Actions.Diagnostics;
using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Core.Implementations.Actions.Error;
using CaptureTool.Core.Interfaces.Actions.Loading;
using CaptureTool.Core.Implementations.Actions.Loading;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Implementations.Actions.Home;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Core.Implementations.Actions.VideoEdit;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Core.Implementations.Actions.AppMenu;
using CaptureTool.Core.Implementations.Actions.Diagnostics;

namespace CaptureTool.Core.Implementations.DependencyInjection;

public static class CoreActionsServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureOverlayActions(this IServiceCollection services)
    {
        services.AddTransient<ICaptureOverlayCloseAction, CaptureOverlayCloseAction>();
        services.AddTransient<ICaptureOverlayGoBackAction, CaptureOverlayGoBackAction>();
        services.AddTransient<ICaptureOverlayToggleDesktopAudioAction, CaptureOverlayToggleDesktopAudioAction>();
        services.AddTransient<ICaptureOverlayStartVideoCaptureAction, CaptureOverlayStartVideoCaptureAction>();
        services.AddTransient<ICaptureOverlayStopVideoCaptureAction, CaptureOverlayStopVideoCaptureAction>();
        services.AddTransient<ICaptureOverlayActions, CaptureOverlayActions>();
        return services;
    }

    public static IServiceCollection AddAboutActions(this IServiceCollection services)
    {
        services.AddTransient<IAboutGoBackAction, AboutGoBackAction>();
        services.AddTransient<IAboutActions, AboutActions>();
        return services;
    }

    public static IServiceCollection AddAddOnsActions(this IServiceCollection services)
    {
        services.AddTransient<IAddOnsGoBackAction, AddOnsGoBackAction>();
        services.AddTransient<IAddOnsActions, AddOnsActions>();
        return services;
    }

    public static IServiceCollection AddErrorActions(this IServiceCollection services)
    {
        services.AddTransient<IErrorRestartAppAction, ErrorRestartAppAction>();
        services.AddTransient<IErrorActions, ErrorActions>();
        return services;
    }

    public static IServiceCollection AddLoadingActions(this IServiceCollection services)
    {
        services.AddTransient<ILoadingGoBackAction, LoadingGoBackAction>();
        services.AddTransient<ILoadingActions, LoadingActions>();
        return services;
    }

    public static IServiceCollection AddHomeActions(this IServiceCollection services)
    {
        services.AddTransient<IHomeNewImageCaptureAction, HomeNewImageCaptureAction>();
        services.AddTransient<IHomeNewVideoCaptureAction, HomeNewVideoCaptureAction>();
        services.AddTransient<IHomeActions, HomeActions>();
        return services;
    }

    public static IServiceCollection AddVideoEditActions(this IServiceCollection services)
    {
        services.AddTransient<IVideoEditSaveAction, VideoEditSaveAction>();
        services.AddTransient<IVideoEditCopyAction, VideoEditCopyAction>();
        services.AddTransient<IVideoEditActions, VideoEditActions>();
        return services;
    }

    public static IServiceCollection AddSettingsActions(this IServiceCollection services)
    {
        // Core settings actions
        services.AddTransient<ISettingsGoBackAction, SettingsGoBackAction>();
        services.AddTransient<ISettingsRestartAppAction, SettingsRestartAppAction>();
        services.AddTransient<ISettingsUpdateImageAutoCopyAction, SettingsUpdateImageAutoCopyAction>();
        services.AddTransient<ISettingsUpdateImageAutoSaveAction, SettingsUpdateImageAutoSaveAction>();
        services.AddTransient<ISettingsUpdateAppLanguageAction, SettingsUpdateAppLanguageAction>();
        services.AddTransient<ISettingsUpdateAppThemeAction, SettingsUpdateAppThemeAction>();
        services.AddTransient<ISettingsChangeScreenshotsFolderAction, SettingsChangeScreenshotsFolderAction>();
        services.AddTransient<ISettingsClearTempFilesAction, SettingsClearTempFilesAction>();
        services.AddTransient<ISettingsRestoreDefaultsAction, SettingsRestoreDefaultsAction>();
        services.AddTransient<ISettingsActions, SettingsActions>();
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
