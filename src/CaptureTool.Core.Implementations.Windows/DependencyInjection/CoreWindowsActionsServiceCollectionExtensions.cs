using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Implementations.Windows.Actions.Settings;

namespace CaptureTool.Core.Implementations.Windows.DependencyInjection;

public static class CoreWindowsActionsServiceCollectionExtensions
{
    public static IServiceCollection AddCoreWindowsSettingsActions(this IServiceCollection services)
    {
        services.AddTransient<ISettingsOpenScreenshotsFolderAction, SettingsOpenScreenshotsFolderAction>();
        services.AddTransient<ISettingsOpenTempFolderAction, SettingsOpenTempFolderAction>();
        return services;
    }
}
