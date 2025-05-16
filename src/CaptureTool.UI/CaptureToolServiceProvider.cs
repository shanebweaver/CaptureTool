using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.SnippingTool;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Globalization;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Storage;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.Services.Telemetry;
using CaptureTool.Services.Themes;
using CaptureTool.Services.Windows.Localization;
using CaptureTool.Services.Windows.Storage;
using CaptureTool.Services.Windows.TaskEnvironment;
using CaptureTool.Services.Windows.Themes;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Factories;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CaptureTool.UI;

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
        collection.AddSingleton<ISnippingToolService, SnippingToolService>();
        collection.AddSingleton<IThemeService, WindowsThemeService>();
        collection.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();
        collection.AddSingleton<ILocalizationService, WindowsLocalizationService>();
        collection.AddSingleton(GetTaskEnvironment);

        // ViewModel factories
        collection.AddSingleton<IFactoryService<DesktopCaptureModeViewModel, DesktopCaptureMode>, DesktopCaptureModeViewModelFactory>();
        collection.AddSingleton<IFactoryService<AppLanguageViewModel, AppLanguage>, AppLanguageViewModelFactory>();
        collection.AddSingleton<IFactoryService<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();

        // ViewModels
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<ErrorPageViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();
        collection.AddTransient<LoadingPageViewModel>();
        collection.AddTransient<ImageEditPageViewModel>();
        collection.AddTransient<VideoEditPageViewModel>();
        collection.AddTransient<DesktopImageCaptureOptionsPageViewModel>();
        collection.AddTransient<DesktopVideoCaptureOptionsPageViewModel>();
        collection.AddTransient<AppMenuViewModel>();
        collection.AddTransient<AppTitleBarViewModel>();
        collection.AddTransient<AppAboutViewModel>();

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

    private ITaskEnvironment GetTaskEnvironment(IServiceProvider serviceProvider)
    {
        return new WinUITaskEnvironment(App.Current.DispatcherQueue);
    }
}
