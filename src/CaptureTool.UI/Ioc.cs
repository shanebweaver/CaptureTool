using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Common;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Storage;
using CaptureTool.Services.Storage.Windows;
using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

public partial class Ioc
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitalized;

    public Ioc()
    {
        ServiceCollection collection = new();

        // Services
        collection.AddSingleton<ICancellationService, CancellationService>();
        collection.AddSingleton<ILogService, DebugLogService>();
        collection.AddSingleton<INavigationService, NavigationService>();
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();

        // ViewModels
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<HomePageViewModel>();

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
            string settingsFilePath = Path.Combine(appDataPath, "CaptureTool", Constants.SettingsFileName);
            await GetService<ISettingsService>().InitializeAsync(settingsFilePath, cancellationToken);

            _isInitalized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
}
