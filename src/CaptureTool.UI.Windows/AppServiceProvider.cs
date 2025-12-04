using CaptureTool.Core.Activation;
using CaptureTool.Core.Capture;
using CaptureTool.Core.Navigation;
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
        collection.AddSingleton<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        collection.AddSingleton<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        collection.AddSingleton<IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        collection.AddSingleton<IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        collection.AddSingleton<IFactoryServiceWithArgs<RecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();

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
