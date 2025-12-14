using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using CaptureTool.Services.Interfaces.Clipboard;
using CaptureTool.Services.Interfaces.Store;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Share;
using CaptureTool.Services.Interfaces.TaskEnvironment;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Implementations.Windows.Clipboard;
using CaptureTool.Services.Implementations.Windows.Store;
using CaptureTool.Services.Implementations.Windows.Themes;
using CaptureTool.Services.Implementations.Windows.Storage;
using CaptureTool.Services.Implementations.Windows.Localization;
using CaptureTool.Services.Implementations.Windows.Share;
using CaptureTool.Services.Implementations.Windows.TaskEnvironment;
using CaptureTool.Services.Implementations.Windows.Shutdown;

namespace CaptureTool.Services.Implementations.Windows.DependencyInjection;

public static class WindowsServicesServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsServices(this IServiceCollection services, DispatcherQueue dispatcherQueue)
    {
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IStoreService, WindowsStoreService>();
        services.AddSingleton<IThemeService, WindowsThemeService>();
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<IJsonStorageService, WindowsJsonStorageService>();
        services.AddSingleton<ILocalizationService, WindowsLocalizationService>();
        services.AddSingleton<IShareService, WindowsShareService>();
        services.AddSingleton<ITaskEnvironment>(_ => new WinUITaskEnvironment(dispatcherQueue));
        services.AddSingleton<IShutdownHandler, WindowsShutdownHandler>();
        services.AddSingleton<IFilePickerService, WindowsFilePickerService>();
        return services;
    }
}
