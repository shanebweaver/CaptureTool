using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Media;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Infrastructure.Windows.Audio;
using CaptureTool.Infrastructure.Windows.Clipboard;
using CaptureTool.Infrastructure.Windows.Localization;
using CaptureTool.Infrastructure.Windows.Media;
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
        services.AddSingleton<IVideoFileTrimmer, WindowsVideoFileTrimmer>();
        services.AddSingleton<IAudioInputDetectionService, WindowsAudioInputDetectionService>();
        return services;
    }
}
