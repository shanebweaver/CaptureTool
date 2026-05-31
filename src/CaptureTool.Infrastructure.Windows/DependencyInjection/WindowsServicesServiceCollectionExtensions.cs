using CaptureTool.Infrastructure.Abstractions.Clipboard;
using CaptureTool.Infrastructure.Abstractions.Audio;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Share;
using CaptureTool.Infrastructure.Abstractions.Shutdown;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Store;
using CaptureTool.Infrastructure.Abstractions.TaskEnvironment;
using CaptureTool.Infrastructure.Abstractions.Themes;
using CaptureTool.Infrastructure.Windows.Clipboard;
using CaptureTool.Infrastructure.Windows.Audio;
using CaptureTool.Infrastructure.Windows.Localization;
using CaptureTool.Infrastructure.Windows.Share;
using CaptureTool.Infrastructure.Windows.Shutdown;
using CaptureTool.Infrastructure.Windows.Storage;
using CaptureTool.Infrastructure.Windows.Store;
using CaptureTool.Infrastructure.Windows.TaskEnvironment;
using CaptureTool.Infrastructure.Windows.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace CaptureTool.Infrastructure.Windows.DependencyInjection;

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
        services.AddSingleton<IAudioInputDetectionService, WindowsAudioInputDetectionService>();
        return services;
    }
}
