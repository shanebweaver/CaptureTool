using CaptureTool.Core.AppController;
using CaptureTool.Edit;
using CaptureTool.Edit.ChromaKey;
using CaptureTool.Edit.Windows;
using CaptureTool.Edit.Windows.ChromaKey;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Globalization;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Store;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.Services.Telemetry;
using CaptureTool.Services.Themes;
using CaptureTool.Services.Windows.Localization;
using CaptureTool.Services.Windows.Storage;
using CaptureTool.Services.Windows.Store;
using CaptureTool.Services.Windows.TaskEnvironment;
using CaptureTool.Services.Windows.Themes;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Factories;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CaptureTool.UI.Windows;

public partial class CaptureToolServiceProvider : IServiceProvider, IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public CaptureToolServiceProvider()
    {
        ServiceCollection collection = new();

        // Services
        collection.AddSingleton<ICancellationService, CancellationService>();
        collection.AddSingleton<IGlobalizationService, GlobalizationService>();
        collection.AddSingleton<ILogService, DebugLogService>();
        collection.AddSingleton<INavigationService, NavigationService>();
        collection.AddSingleton<ISettingsService, LocalSettingsService>();
        collection.AddSingleton<ITelemetryService, TelemetryService>();

        // Windows Services
        collection.AddSingleton<IStoreService, WindowsStoreService>();
        collection.AddSingleton<IChromaKeyService, Win2DChromaKeyService>();
        collection.AddSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        collection.AddSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
        collection.AddSingleton<IFilePickerService, WindowsFilePickerService>();
        collection.AddSingleton<IThemeService, WindowsThemeService>();
        collection.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();
        collection.AddSingleton<ILocalizationService, WindowsLocalizationService>();
        collection.AddSingleton<ITaskEnvironment, WinUITaskEnvironment>(CreateWinUITaskEnvironment);

        // ViewModels
        // Windows
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<SelectionOverlayWindowViewModel>();
        // Pages
        collection.AddTransient<ErrorPageViewModel>();
        collection.AddTransient<AboutPageViewModel>();
        collection.AddTransient<AddOnsPageViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();
        collection.AddTransient<LoadingPageViewModel>();
        collection.AddTransient<ImageEditPageViewModel>();
        collection.AddTransient<VideoEditPageViewModel>();
        // Views
        collection.AddTransient<AppMenuViewModel>();
        collection.AddTransient<SelectionOverlayHostViewModel>();
        // Factories
        collection.AddSingleton<IFactoryService<AppLanguageViewModel, AppLanguage?>, AppLanguageViewModelFactory>();
        collection.AddSingleton<IFactoryService<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();

        // App controller and feature manager
        collection.AddSingleton<IAppController, CaptureToolAppController>();
        collection.AddSingleton<IFeatureManager, CaptureToolFeatureManager>();

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
