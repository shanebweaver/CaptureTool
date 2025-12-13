using CaptureTool.Core.Implementations.Actions.About;
using CaptureTool.Core.Implementations.Actions.AddOns;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Core.Implementations.Actions.Error;
using CaptureTool.Core.Interfaces.Actions.About;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Core.Implementations.Activation;
using CaptureTool.Core.Implementations.Capture;
using CaptureTool.Core.Implementations.Navigation;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Core.Implementations.Actions.Loading;
using CaptureTool.Core.Interfaces.Actions.Loading;
using CaptureTool.Core.Implementations.Actions.Home;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Edit.Implementations.Windows;
using CaptureTool.Domains.Edit.Implementations.Windows.ChromaKey;
using CaptureTool.Domains.Edit.Interfaces;
using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using CaptureTool.Services.Implementations.Cancellation;
using CaptureTool.Services.Implementations.FeatureManagement;
using CaptureTool.Services.Implementations.Globalization;
using CaptureTool.Services.Implementations.Logging;
using CaptureTool.Services.Implementations.Navigation;
using CaptureTool.Services.Implementations.Settings;
using CaptureTool.Services.Implementations.Telemetry;
using CaptureTool.Services.Implementations.Windows.Clipboard;
using CaptureTool.Services.Implementations.Windows.Localization;
using CaptureTool.Services.Implementations.Windows.Share;
using CaptureTool.Services.Implementations.Windows.Shutdown;
using CaptureTool.Services.Implementations.Windows.Storage;
using CaptureTool.Services.Implementations.Windows.Store;
using CaptureTool.Services.Implementations.Windows.TaskEnvironment;
using CaptureTool.Services.Implementations.Windows.Themes;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Activation;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Clipboard;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Globalization;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Share;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.TaskEnvironment;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Factories;
using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Implementations.Actions.Settings;

namespace CaptureTool.UI.Windows;

public partial class AppServiceProvider : IServiceProvider, IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public AppServiceProvider()
    {
        ServiceCollection collection = new();

        // Feature management
        collection.AddSingleton<IFeatureManager, MicrosoftFeatureManager>();

        // Generic services
        collection.AddSingleton<ICancellationService, CancellationService>();
        collection.AddSingleton<IGlobalizationService, GlobalizationService>();
        collection.AddSingleton<INavigationService, NavigationService>();
        collection.AddSingleton<ISettingsService, LocalSettingsService>();
        collection.AddSingleton<ITelemetryService, TelemetryService>();
#if DEBUG
        collection.AddSingleton<ILogService, DebugLogService>();
#else
        collection.AddSingleton<ILogService, ShortTermMemoryLogService>();
#endif

        // Windows services
        collection.AddSingleton<IClipboardService, WindowsClipboardService>();
        collection.AddSingleton<IStoreService, WindowsStoreService>();
        collection.AddSingleton<IChromaKeyService, Win2DChromaKeyService>();
        collection.AddSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        collection.AddSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
        collection.AddSingleton<IFilePickerService, WindowsFilePickerService>();
        collection.AddSingleton<IThemeService, WindowsThemeService>();
        collection.AddSingleton<IStorageService, WindowsStorageService>();
        collection.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();
        collection.AddSingleton<ILocalizationService, WindowsLocalizationService>();
        collection.AddSingleton<IShareService, WindowsShareService>();
        collection.AddSingleton<ITaskEnvironment, WinUITaskEnvironment>(CreateWinUITaskEnvironment);
        collection.AddSingleton<IShutdownHandler, WindowsShutdownHandler>();

        // Windows domains
        collection.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        collection.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();

        // Core services
        collection.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();
        collection.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();
        collection.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();
        collection.AddSingleton<IAppNavigation, CaptureToolAppNavigation>();

        // Action handlers
        collection.AddTransient<ICaptureOverlayCloseAction, CaptureOverlayCloseAction>();
        collection.AddTransient<ICaptureOverlayGoBackAction, CaptureOverlayGoBackAction>();
        collection.AddTransient<ICaptureOverlayToggleDesktopAudioAction, CaptureOverlayToggleDesktopAudioAction>();
        collection.AddTransient<ICaptureOverlayStartVideoCaptureAction, CaptureOverlayStartVideoCaptureAction>();
        collection.AddTransient<ICaptureOverlayStopVideoCaptureAction, CaptureOverlayStopVideoCaptureAction>();
        collection.AddTransient<ICaptureOverlayActions, CaptureOverlayActions>();

        // About actions
        collection.AddTransient<IAboutGoBackAction, AboutGoBackAction>();
        collection.AddTransient<IAboutActions, AboutActions>();

        // AddOns actions
        collection.AddTransient<IAddOnsGoBackAction, AddOnsGoBackAction>();
        collection.AddTransient<IAddOnsActions, AddOnsActions>();

        // Error actions
        collection.AddTransient<IErrorRestartAppAction, ErrorRestartAppAction>();
        collection.AddTransient<IErrorActions, ErrorActions>();

        // Loading actions
        collection.AddTransient<ILoadingGoBackAction, LoadingGoBackAction>();
        collection.AddTransient<ILoadingActions, LoadingActions>();

        // Home actions
        collection.AddTransient<IHomeNewImageCaptureAction, HomeNewImageCaptureAction>();
        collection.AddTransient<IHomeNewVideoCaptureAction, HomeNewVideoCaptureAction>();
        collection.AddTransient<IHomeActions, HomeActions>();
        
        // Settings actions
        collection.AddTransient<ISettingsGoBackAction, SettingsGoBackAction>();
        collection.AddTransient<ISettingsRestartAppAction, SettingsRestartAppAction>();
        collection.AddTransient<ISettingsUpdateImageAutoCopyAction, SettingsUpdateImageAutoCopyAction>();
        collection.AddTransient<ISettingsUpdateImageAutoSaveAction, SettingsUpdateImageAutoSaveAction>();
        collection.AddTransient<ISettingsUpdateAppLanguageAction, SettingsUpdateAppLanguageAction>();
        collection.AddTransient<ISettingsUpdateAppThemeAction, SettingsUpdateAppThemeAction>();
        collection.AddTransient<ISettingsChangeScreenshotsFolderAction, SettingsChangeScreenshotsFolderAction>();
        // For actions requiring context values, consider factory pattern or service-based resolution; leaving open folder actions out of DI for now.
        collection.AddTransient<ISettingsActions, SettingsActions>();

        // ViewModels
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<SelectionOverlayWindowViewModel>();
        collection.AddTransient<ErrorPageViewModel>();
        collection.AddTransient<AboutPageViewModel>();
        collection.AddTransient<AddOnsPageViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();
        collection.AddTransient<LoadingPageViewModel>();
        collection.AddTransient<ImageEditPageViewModel>();
        collection.AddTransient<VideoEditPageViewModel>();
        collection.AddTransient<AppMenuViewModel>();
        collection.AddTransient<DiagnosticsViewModel>();
        collection.AddTransient<SelectionOverlayHostViewModel>();
        collection.AddTransient<CaptureOverlayViewModel>();

        // ViewModel factories
        collection.AddTransient<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<RecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();

        // App specific handlers
        collection.AddSingleton<AppNavigationHandler>();
        collection.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        collection.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());

        _serviceProvider = collection.BuildServiceProvider();
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
    public object GetService(Type t) => _serviceProvider.GetRequiredService(t);

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    private static WinUITaskEnvironment CreateWinUITaskEnvironment(IServiceProvider serviceProvider)
    {
        return new WinUITaskEnvironment(App.Current.DispatcherQueue);
    }
}
