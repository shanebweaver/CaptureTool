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
    private class ServiceMapping(Type serviceType, Type implementationType) 
        : Tuple<Type, Type>(serviceType, implementationType)
    {
        public Type ServiceType => Item1;
        public Type ImplementationType => Item2;
    }

    // Services
    private static readonly ServiceMapping[] _serviceMappings = [
        new(typeof(IAppController), typeof(CaptureToolAppController)),
        new(typeof(IFeatureManager), typeof(CaptureToolFeatureManager)),
        new(typeof(ICancellationService), typeof(CancellationService)),
        new(typeof(IGlobalizationService), typeof(GlobalizationService)),
        new(typeof(IJsonStorageService), typeof(WindowsJsonStorageService)),
        new(typeof(ILocalizationService), typeof(WindowsLocalizationService)),
        new(typeof(ILogService), typeof(DebugLogService)),
        new(typeof(INavigationService), typeof(NavigationService)),
        new(typeof(ISettingsService), typeof(SettingsService)),
        new(typeof(ITelemetryService), typeof(TelemetryService)),
        new(typeof(ISnippingToolService), typeof(SnippingToolService)),
        new(typeof(IFactoryService<DesktopCaptureModeViewModel>), typeof(DesktopCaptureModeViewModelFactory)),
    ];

    // ViewModels
    private static readonly Type[] _viewModelMappings = [
        typeof(MainWindowViewModel),
        typeof(HomePageViewModel),
        typeof(SettingsPageViewModel),
        typeof(LoadingPageViewModel),
        typeof(ImageEditPageViewModel),
        typeof(DesktopCaptureOptionsPageViewModel),
        typeof(AppMenuViewModel),
        typeof(AppTitleBarViewModel),
    ];

    private readonly ServiceProvider _serviceProvider;

    public CaptureToolServiceProvider()
    {
        ServiceCollection collection = new();

        // TaskEnvironment
        collection.AddSingleton(GetTaskEnvironment);

        // Services
        foreach (var mapping in _serviceMappings)
        {
            collection.AddSingleton(mapping.ServiceType, mapping.ImplementationType);
        }

        // ViewModels
        foreach (Type viewModelType in _viewModelMappings)
        {
            collection.AddSingleton(viewModelType);
        }

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
