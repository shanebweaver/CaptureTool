using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Storage.Windows;
using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

public partial class Ioc : IAsyncDisposable
{
    private class ServiceMapping(Type serviceType, Type implementationType)
    {
        public Type ServiceType { get; } = serviceType;
        public Type ImplementationType { get; } = implementationType;
    }

    // Services
    private static readonly ServiceMapping[] serviceMappings = [
        new ServiceMapping(typeof(IFeatureManager), typeof(CaptureToolFeatureManager)),
        new ServiceMapping(typeof(ICancellationService), typeof(CancellationService)),
        new ServiceMapping(typeof(IJsonStorageService), typeof(WindowsJsonStorageService)),
        new ServiceMapping(typeof(ILogService), typeof(DebugLogService)),
        new ServiceMapping(typeof(INavigationService), typeof(NavigationService)),
        new ServiceMapping(typeof(ISettingsService), typeof(SettingsService)),
    ];

    private readonly ServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitalized;

    public Ioc()
    {
        ServiceCollection collection = new();

        foreach (var mapping in serviceMappings)
        {
            collection.AddSingleton(mapping.ServiceType, mapping.ImplementationType);
        }

        // ViewModels
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<HomePageViewModel>();
        collection.AddSingleton<SettingsPageViewModel>();

        _serviceProvider = collection.BuildServiceProvider();
    }

    public async Task InitializeServicesAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if(_isInitalized)
            {
                return;
            }

            // Settings
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(appDataPath, "CaptureTool", "Settings.json");
            await GetService<ISettingsService>().InitializeAsync(settingsFilePath);

            _isInitalized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
    public object GetService(Type t) => _serviceProvider.GetRequiredService(t);

    public async ValueTask DisposeAsync()
    {
        _semaphore?.Dispose();

        foreach (var mapping in serviceMappings)
        {
            object service = GetService(mapping.ServiceType);
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (service is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
