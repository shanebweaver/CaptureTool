using CaptureTool.Application.Abstractions.Features.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Features.SettingsPage.ChangeScreenshotsFolder;
using CaptureTool.Application.Features.SettingsPage.ChangeVideosFolder;
using CaptureTool.Application.Features.SettingsPage.ClearTempFiles;
using CaptureTool.Application.Features.SettingsPage.LeaveSettingsPage;
using CaptureTool.Application.Features.SettingsPage.OpenScreenshotsFolder;
using CaptureTool.Application.Features.SettingsPage.OpenSettingsPage;
using CaptureTool.Application.Features.SettingsPage.OpenTempFolder;
using CaptureTool.Application.Features.SettingsPage.OpenVideosFolder;
using CaptureTool.Application.Features.SettingsPage.RestartSettingsApplication;
using CaptureTool.Application.Features.SettingsPage.RestoreDefaults;
using CaptureTool.Application.Features.SettingsPage.UpdateAppLanguage;
using CaptureTool.Application.Features.SettingsPage.UpdateAppTheme;
using CaptureTool.Application.Features.SettingsPage.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Features.SettingsPage.UpdateImageAutoCopy;
using CaptureTool.Application.Features.SettingsPage.UpdateImageAutoSave;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureDefaultLocalAudio;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsUseCases(this IServiceCollection services)
    {
        services.AddTransient<ILeaveSettingsPageUseCase, LeaveSettingsPageUseCase>();
        services.AddTransient<IRestartSettingsApplicationUseCase, RestartSettingsApplicationUseCase>();
        services.AddTransient<IUpdateImageAutoCopyUseCase, UpdateImageAutoCopyUseCase>();
        services.AddTransient<IUpdateImageAutoSaveUseCase, UpdateImageAutoSaveUseCase>();
        services.AddTransient<IUpdateVideoCaptureAutoCopyUseCase, UpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<IUpdateVideoCaptureAutoSaveUseCase, UpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<IUpdateVideoCaptureDefaultLocalAudioUseCase, UpdateVideoCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<IUpdateAppLanguageUseCase, UpdateAppLanguageUseCase>();
        services.AddTransient<IUpdateAppThemeUseCase, UpdateAppThemeUseCase>();
        services.AddTransient<IUpdateEditWarnBeforeDiscardUseCase, UpdateEditWarnBeforeDiscardUseCase>();
        services.AddTransient<IChangeScreenshotsFolderUseCase, ChangeScreenshotsFolderUseCase>();
        services.AddTransient<IChangeVideosFolderUseCase, ChangeVideosFolderUseCase>();
        services.AddTransient<IClearTempFilesUseCase, ClearTempFilesUseCase>();
        services.AddTransient<IRestoreDefaultsUseCase, RestoreDefaultsUseCase>();
        services.AddTransient<IOpenScreenshotsFolderUseCase, OpenScreenshotsFolderUseCase>();
        services.AddTransient<IOpenVideosFolderUseCase, OpenVideosFolderUseCase>();
        services.AddTransient<IOpenTempFolderUseCase, OpenTempFolderUseCase>();
        services.AddTransient<IOpenSettingsPageUseCase, OpenSettingsPageUseCase>();

        return services;
    }
}
