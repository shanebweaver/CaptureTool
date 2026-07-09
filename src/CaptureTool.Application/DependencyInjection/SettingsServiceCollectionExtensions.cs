using CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Settings.ChangeAudioFolder;
using CaptureTool.Application.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Settings.ChangeVideosFolder;
using CaptureTool.Application.Settings.ClearTempFiles;
using CaptureTool.Application.Settings.LeaveSettingsPage;
using CaptureTool.Application.Settings.OpenAudioFolder;
using CaptureTool.Application.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Settings.OpenSettingsPage;
using CaptureTool.Application.Settings.OpenTempFolder;
using CaptureTool.Application.Settings.OpenVideosFolder;
using CaptureTool.Application.Settings.RestartSettingsApplication;
using CaptureTool.Application.Settings.RestoreDefaults;
using CaptureTool.Application.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Settings.UpdateAppLanguage;
using CaptureTool.Application.Settings.UpdateAppTheme;
using CaptureTool.Application.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Settings.UpdateVideoCaptureDefaultLocalAudio;
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
        services.AddTransient<IUpdateAudioCaptureAutoCopyUseCase, UpdateAudioCaptureAutoCopyUseCase>();
        services.AddTransient<IUpdateAudioCaptureAutoSaveUseCase, UpdateAudioCaptureAutoSaveUseCase>();
        services.AddTransient<IUpdateAudioCaptureDefaultLocalAudioUseCase, UpdateAudioCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<IUpdateVideoCaptureAutoCopyUseCase, UpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<IUpdateVideoCaptureAutoSaveUseCase, UpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<IUpdateVideoCaptureDefaultLocalAudioUseCase, UpdateVideoCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<IUpdateAppLanguageUseCase, UpdateAppLanguageUseCase>();
        services.AddTransient<IUpdateAppThemeUseCase, UpdateAppThemeUseCase>();
        services.AddTransient<IUpdateCaptureWarnBeforeDiscardUseCase, UpdateCaptureWarnBeforeDiscardUseCase>();
        services.AddTransient<IUpdateEditWarnBeforeDiscardUseCase, UpdateEditWarnBeforeDiscardUseCase>();
        services.AddTransient<IChangeScreenshotsFolderUseCase, ChangeScreenshotsFolderUseCase>();
        services.AddTransient<IChangeAudioFolderUseCase, ChangeAudioFolderUseCase>();
        services.AddTransient<IChangeVideosFolderUseCase, ChangeVideosFolderUseCase>();
        services.AddTransient<IClearTempFilesUseCase, ClearTempFilesUseCase>();
        services.AddTransient<IRestoreDefaultsUseCase, RestoreDefaultsUseCase>();
        services.AddTransient<IOpenScreenshotsFolderUseCase, OpenScreenshotsFolderUseCase>();
        services.AddTransient<IOpenAudioFolderUseCase, OpenAudioFolderUseCase>();
        services.AddTransient<IOpenVideosFolderUseCase, OpenVideosFolderUseCase>();
        services.AddTransient<IOpenTempFolderUseCase, OpenTempFolderUseCase>();
        services.AddTransient<IOpenSettingsPageUseCase, OpenSettingsPageUseCase>();

        return services;
    }
}
