using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Capture;
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
using CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
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
using CaptureTool.Application.Features.Settings.UpdateVideoMetadataAutoSave;
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

        services.AddTransient<IUseCase<LeaveAboutPageRequest, LeaveAboutPageResponse>, LeaveAboutPageUseCase>();
        services.AddTransient<IUseCase<OpenAboutPageRequest, OpenAboutPageResponse>, OpenAboutPageUseCase>();

        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();

        services.AddTransient<IUseCase<ExitApplicationRequest, ExitApplicationResponse>, ExitApplicationUseCase>();
        services.AddTransient<IUseCase<OpenFileRequest, OpenFileResponse>, OpenFileUseCase>();

        services.AddTransient<IUseCase<StartAudioCaptureRequest, StartAudioCaptureResponse>, StartAudioCaptureUseCase>();
        services.AddTransient<IUseCase<StopAudioCaptureRequest, StopAudioCaptureResponse>, StopAudioCaptureUseCase>();
        services.AddTransient<IUseCase<PauseAudioCaptureRequest, PauseAudioCaptureResponse>, PauseAudioCaptureUseCase>();
        services.AddTransient<IUseCase<MuteAudioCaptureRequest, MuteAudioCaptureResponse>, MuteAudioCaptureUseCase>();
        services.AddTransient<IUseCase<ToggleLocalAudioCaptureRequest, ToggleLocalAudioCaptureResponse>, ToggleLocalAudioCaptureUseCase>();
        services.AddTransient<IUseCase<OpenAudioCapturePageRequest, OpenAudioCapturePageResponse>, OpenAudioCapturePageUseCase>();
        services.AddSingleton<IAudioCaptureHandler, AudioCaptureHandler>();

        services.AddTransient<IUseCase<SaveAudioFileRequest, SaveAudioFileResponse>, SaveAudioFileUseCase>();
        services.AddTransient<IConditional<SaveAudioFileRequest>, SaveAudioFileUseCase>();
        services.AddTransient<IUseCase<CopyAudioFileRequest, CopyAudioFileResponse>, CopyAudioFileUseCase>();
        services.AddTransient<IConditional<CopyAudioFileRequest>, CopyAudioFileUseCase>();
        services.AddTransient<IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse>, OpenAudioEditPageUseCase>();
        services.AddTransient<IConditional<OpenAudioEditPageRequest>, OpenAudioEditPageUseCase>();

        services.AddTransient<IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse>, CloseCaptureOverlayUseCase>();
        services.AddTransient<IUseCase<GoBackFromCaptureOverlayRequest, GoBackFromCaptureOverlayResponse>, GoBackFromCaptureOverlayUseCase>();
        services.AddTransient<IConditional<GoBackFromCaptureOverlayRequest>, GoBackFromCaptureOverlayUseCase>();
        services.AddTransient<IUseCase<OpenCaptureOverlayRequest, OpenCaptureOverlayResponse>, OpenCaptureOverlayUseCase>();
        services.AddTransient<IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse>, OpenSelectionOverlayUseCase>();
        services.AddTransient<IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse>, StartVideoCaptureUseCase>();
        services.AddTransient<IConditional<StartVideoCaptureRequest>, StartVideoCaptureUseCase>();
        services.AddTransient<IUseCase<StopVideoCaptureRequest, StopVideoCaptureResponse>, StopVideoCaptureUseCase>();
        services.AddTransient<IConditional<StopVideoCaptureRequest>, StopVideoCaptureUseCase>();
        services.AddTransient<IUseCase<ToggleVideoCaptureDesktopAudioRequest, ToggleVideoCaptureDesktopAudioResponse>, ToggleVideoCaptureDesktopAudioUseCase>();
        services.AddTransient<IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse>, ToggleVideoCapturePauseResumeUseCase>();
        services.AddTransient<IConditional<ToggleVideoCapturePauseResumeRequest>, ToggleVideoCapturePauseResumeUseCase>();

        services.AddTransient<IUseCase<ClearLogsRequest, ClearLogsResponse>, ClearLogsUseCase>();
        services.AddTransient<IUseCase<GetCurrentLogsRequest, GetCurrentLogsResponse>, GetCurrentLogsUseCase>();
        services.AddTransient<IUseCase<GetIsLoggingEnabledRequest, GetIsLoggingEnabledResponse>, GetIsLoggingEnabledUseCase>();
        services.AddTransient<IUseCase<UpdateLoggingStateRequest, UpdateLoggingStateResponse>, UpdateLoggingStateUseCase>();

        services.AddTransient<IUseCase<RestartApplicationRequest, RestartApplicationResponse>, RestartApplicationUseCase>();
        services.AddTransient<IConditional<RestartApplicationRequest>, RestartApplicationUseCase>();

        services.AddTransient<IUseCase<ShowHomePageRequest, ShowHomePageResponse>, ShowHomePageUseCase>();

        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();

        services.AddTransient<IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse>, OpenImageEditPageUseCase>();
        services.AddTransient<IConditional<OpenImageEditPageRequest>, OpenImageEditPageUseCase>();

        services.AddTransient<IUseCase<GetRecentCapturesRequest, GetRecentCapturesResponse>, GetRecentCapturesUseCase>();
        services.AddTransient<IConditional<GetRecentCapturesRequest>, GetRecentCapturesUseCase>();
        services.AddTransient<IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse>, OpenRecentCaptureUseCase>();
        services.AddTransient<IConditional<OpenRecentCaptureRequest>, OpenRecentCaptureUseCase>();

        services.AddTransient<IUseCase<LeaveSettingsPageRequest, LeaveSettingsPageResponse>, LeaveSettingsPageUseCase>();
        services.AddTransient<IUseCase<RestartSettingsApplicationRequest, RestartSettingsApplicationResponse>, RestartSettingsApplicationUseCase>();
        services.AddTransient<IConditional<RestartSettingsApplicationRequest>, RestartSettingsApplicationUseCase>();
        services.AddTransient<IUseCase<UpdateImageAutoCopyRequest, UpdateImageAutoCopyResponse>, UpdateImageAutoCopyUseCase>();
        services.AddTransient<IConditional<UpdateImageAutoCopyRequest>, UpdateImageAutoCopyUseCase>();
        services.AddTransient<IUseCase<UpdateImageAutoSaveRequest, UpdateImageAutoSaveResponse>, UpdateImageAutoSaveUseCase>();
        services.AddTransient<IConditional<UpdateImageAutoSaveRequest>, UpdateImageAutoSaveUseCase>();
        services.AddTransient<IUseCase<UpdateVideoCaptureAutoCopyRequest, UpdateVideoCaptureAutoCopyResponse>, UpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<IConditional<UpdateVideoCaptureAutoCopyRequest>, UpdateVideoCaptureAutoCopyUseCase>();
        services.AddTransient<IUseCase<UpdateVideoCaptureAutoSaveRequest, UpdateVideoCaptureAutoSaveResponse>, UpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<IConditional<UpdateVideoCaptureAutoSaveRequest>, UpdateVideoCaptureAutoSaveUseCase>();
        services.AddTransient<IUseCase<UpdateVideoCaptureDefaultLocalAudioRequest, UpdateVideoCaptureDefaultLocalAudioResponse>, UpdateVideoCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<IConditional<UpdateVideoCaptureDefaultLocalAudioRequest>, UpdateVideoCaptureDefaultLocalAudioUseCase>();
        services.AddTransient<IUseCase<UpdateVideoMetadataAutoSaveRequest, UpdateVideoMetadataAutoSaveResponse>, UpdateVideoMetadataAutoSaveUseCase>();
        services.AddTransient<IConditional<UpdateVideoMetadataAutoSaveRequest>, UpdateVideoMetadataAutoSaveUseCase>();
        services.AddTransient<IUseCase<UpdateAppLanguageRequest, UpdateAppLanguageResponse>, UpdateAppLanguageUseCase>();
        services.AddTransient<IConditional<UpdateAppLanguageRequest>, UpdateAppLanguageUseCase>();
        services.AddTransient<IUseCase<UpdateAppThemeRequest, UpdateAppThemeResponse>, UpdateAppThemeUseCase>();
        services.AddTransient<IConditional<UpdateAppThemeRequest>, UpdateAppThemeUseCase>();
        services.AddTransient<IUseCase<ChangeScreenshotsFolderRequest, ChangeScreenshotsFolderResponse>, ChangeScreenshotsFolderUseCase>();
        services.AddTransient<IConditional<ChangeScreenshotsFolderRequest>, ChangeScreenshotsFolderUseCase>();
        services.AddTransient<IUseCase<ChangeVideosFolderRequest, ChangeVideosFolderResponse>, ChangeVideosFolderUseCase>();
        services.AddTransient<IConditional<ChangeVideosFolderRequest>, ChangeVideosFolderUseCase>();
        services.AddTransient<IUseCase<ClearTempFilesRequest, ClearTempFilesResponse>, ClearTempFilesUseCase>();
        services.AddTransient<IConditional<ClearTempFilesRequest>, ClearTempFilesUseCase>();
        services.AddTransient<IUseCase<RestoreDefaultsRequest, RestoreDefaultsResponse>, RestoreDefaultsUseCase>();
        services.AddTransient<IConditional<RestoreDefaultsRequest>, RestoreDefaultsUseCase>();
        services.AddTransient<IUseCase<OpenScreenshotsFolderRequest, OpenScreenshotsFolderResponse>, OpenScreenshotsFolderUseCase>();
        services.AddTransient<IConditional<OpenScreenshotsFolderRequest>, OpenScreenshotsFolderUseCase>();
        services.AddTransient<IUseCase<OpenVideosFolderRequest, OpenVideosFolderResponse>, OpenVideosFolderUseCase>();
        services.AddTransient<IConditional<OpenVideosFolderRequest>, OpenVideosFolderUseCase>();
        services.AddTransient<IUseCase<OpenTempFolderRequest, OpenTempFolderResponse>, OpenTempFolderUseCase>();
        services.AddTransient<IConditional<OpenTempFolderRequest>, OpenTempFolderUseCase>();
        services.AddTransient<IUseCase<OpenSettingsPageRequest, OpenSettingsPageResponse>, OpenSettingsPageUseCase>();
        services.AddTransient<IConditional<OpenSettingsPageRequest>, OpenSettingsPageUseCase>();

        services.AddTransient<IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse>, GetChromaKeyAddOnUseCase>();
        services.AddTransient<IConditional<GetChromaKeyAddOnRequest>, GetChromaKeyAddOnUseCase>();
        services.AddTransient<IUseCase<PurchaseChromaKeyAddOnRequest, PurchaseChromaKeyAddOnResponse>, PurchaseChromaKeyAddOnUseCase>();
        services.AddTransient<IConditional<PurchaseChromaKeyAddOnRequest>, PurchaseChromaKeyAddOnUseCase>();
        services.AddTransient<IUseCase<OpenStorePageRequest, OpenStorePageResponse>, OpenStorePageUseCase>();
        services.AddTransient<IUseCase<LeaveStorePageRequest, LeaveStorePageResponse>, LeaveStorePageUseCase>();

        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();

        services.AddTransient<IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>, CopyVideoFileUseCase>();
        services.AddTransient<IConditional<CopyVideoFileRequest>, CopyVideoFileUseCase>();
        services.AddTransient<IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>, SaveVideoFileUseCase>();
        services.AddTransient<IConditional<SaveVideoFileRequest>, SaveVideoFileUseCase>();
        services.AddTransient<IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse>, OpenVideoEditPageUseCase>();

        services.AddTransient<IUseCase<ShowMainWindowRequest, ShowMainWindowResponse>, ShowMainWindowUseCase>();
        services.AddTransient<IConditional<ShowMainWindowRequest>, ShowMainWindowUseCase>();

        return services;
    }
}