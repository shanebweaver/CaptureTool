using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.About;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.AudioCapture;
using CaptureTool.Presentation.Features.AudioEdit;
using CaptureTool.Presentation.Features.CaptureOverlay;
using CaptureTool.Presentation.Features.Diagnostics;
using CaptureTool.Presentation.Features.Home;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Features.RecentCaptures;
using CaptureTool.Presentation.Features.RecentCaptures.Factories;
using CaptureTool.Presentation.Features.SelectionOverlay;
using CaptureTool.Presentation.Features.SelectionOverlay.Factories;
using CaptureTool.Presentation.Features.Settings;
using CaptureTool.Presentation.Features.Settings.Factories;
using CaptureTool.Presentation.Features.Store;
using CaptureTool.Presentation.Features.VideoEdit;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<IAudioWaveformHistory, AudioWaveformHistory>();
        services.AddSingleton<IAppNotificationService, AppNotificationService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SelectionOverlayWindowViewModel>();
        services.AddTransient<ErrorPageViewModel>();
        services.AddTransient<AboutPageViewModel>();
        services.AddTransient<StorePageViewModel>();
        services.AddTransient<HomePageViewModel>();
        services.AddTransient<CaptureMemoryHomeViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<LoadingPageViewModel>();
        services.AddTransient<ChromaKeyToolViewModel>();
        services.AddTransient<ColorPickerToolViewModel>();
        services.AddTransient<ShapeToolViewModel>();
        services.AddTransient<TextToolViewModel>();
        services.AddTransient<TextExtractionToolViewModel>();
        services.AddTransient<ImageEditPageViewModel>();
        services.AddTransient<VideoEditPageViewModel>();
        services.AddTransient<AudioCapturePageViewModel>();
        services.AddTransient<AudioEditPageViewModel>();
        services.AddTransient<AppMenuViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<SelectionOverlayHostViewModel>();
        services.AddTransient<CaptureOverlayViewModel>();

        // Factories
        services.AddTransient<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<RecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();
        return services;
    }
}
