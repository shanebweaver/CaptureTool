using System;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Globalization;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Localization.Windows;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.SnippingTool;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Storage.Windows;
using CaptureTool.Services.TaskEnvironment;
using CaptureTool.Services.TaskEnvironment.WinUI;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

public partial class CaptureToolServiceProvider : IServiceProvider, IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public CaptureToolServiceProvider()
    {
        ServiceCollection collection = new();

        // TaskEnvironment
        collection.AddSingleton(GetTaskEnvironment);

        // App controller and feature manager
        collection.AddSingleton<IAppController, CaptureToolAppController>();
        collection.AddSingleton<IFeatureManager, CaptureToolFeatureManager>();

        // Services
        collection.AddSingleton<ICancellationService, CancellationService>();
        collection.AddSingleton<IGlobalizationService, GlobalizationService>();
        collection.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();
        collection.AddSingleton<ILocalizationService, WindowsLocalizationService>();
        collection.AddSingleton<ILogService, DebugLogService>();
        collection.AddSingleton<INavigationService, NavigationService>();
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddSingleton<ITelemetryService, TelemetryService>();
        collection.AddSingleton<ISnippingToolService, SnippingToolService>();

        // ViewModels
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<HomePageViewModel>();
        collection.AddSingleton<SettingsPageViewModel>();
        collection.AddSingleton<LoadingPageViewModel>();
        collection.AddSingleton<ImageEditPageViewModel>();
        collection.AddSingleton<DesktopCaptureOptionsPageViewModel>();
        collection.AddSingleton<AppMenuViewModel>();
        collection.AddSingleton<AppTitleBarViewModel>();
        collection.AddSingleton<DesktopCaptureModeViewModel>();

        // ViewModel factories
        collection.AddSingleton<IFactoryService<DesktopCaptureModeViewModel>, DesktopCaptureModeViewModelFactory>();

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
