using System;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Globalization;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Localization.Windows;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Storage.Windows;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

public partial class CaptureToolServiceProvider : IServiceProvider, IDisposable
{
    private class ServiceMapping(Type serviceType, Type implementationType)
    {
        public Type ServiceType { get; } = serviceType;
        public Type ImplementationType { get; } = implementationType;
    }

    // Services
    private static readonly ServiceMapping[] _serviceMappings = [
        new ServiceMapping(typeof(IFeatureManager), typeof(CaptureToolFeatureManager)),
        new ServiceMapping(typeof(ICancellationService), typeof(CancellationService)),
        new ServiceMapping(typeof(IGlobalizationService), typeof(GlobalizationService)),
        new ServiceMapping(typeof(IJsonStorageService), typeof(WindowsJsonStorageService)),
        new ServiceMapping(typeof(ILocalizationService), typeof(WindowsLocalizationService)),
        new ServiceMapping(typeof(ILogService), typeof(DebugLogService)),
        new ServiceMapping(typeof(INavigationService), typeof(NavigationService)),
        new ServiceMapping(typeof(ISettingsService), typeof(SettingsService)),
        new ServiceMapping(typeof(ITelemetryService), typeof(TelemetryService)),
    ];

    // ViewModels
    private static readonly Type[] _viewModelMappings = [
        typeof(MainWindowViewModel),
        typeof(HomePageViewModel),
        typeof(SettingsPageViewModel),
    ];

    private readonly ServiceProvider _serviceProvider;

    public CaptureToolServiceProvider()
    {
        ServiceCollection collection = new();

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
}
