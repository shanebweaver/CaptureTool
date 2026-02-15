using CaptureTool.Infrastructure.Implementations.Windows.Capabilities;
using CaptureTool.Infrastructure.Implementations.Windows.Clipboard;
using CaptureTool.Infrastructure.Implementations.Windows.Localization;
using CaptureTool.Infrastructure.Implementations.Windows.Share;
using CaptureTool.Infrastructure.Implementations.Windows.Shutdown;
using CaptureTool.Infrastructure.Implementations.Windows.Storage;
using CaptureTool.Infrastructure.Implementations.Windows.Store;
using CaptureTool.Infrastructure.Implementations.Windows.TaskEnvironment;
using CaptureTool.Infrastructure.Implementations.Windows.Themes;
using CaptureTool.Infrastructure.Interfaces.Capabilities;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Share;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Store;
using CaptureTool.Infrastructure.Interfaces.TaskEnvironment;
using CaptureTool.Infrastructure.Interfaces.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace CaptureTool.Infrastructure.Implementations.Windows.DependencyInjection;

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
        services.AddSingleton<ID3DCapabilityService, D3DCapabilityService>();
        return services;
    }
}
