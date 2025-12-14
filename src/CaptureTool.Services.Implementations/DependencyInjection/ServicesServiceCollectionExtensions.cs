using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Globalization;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Implementations.Cancellation;
using CaptureTool.Services.Implementations.Globalization;
using CaptureTool.Services.Implementations.Logging;
using CaptureTool.Services.Implementations.Navigation;
using CaptureTool.Services.Implementations.Settings;
using CaptureTool.Services.Implementations.Telemetry;

namespace CaptureTool.Services.Implementations.DependencyInjection;

public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddServiceServices(this IServiceCollection services)
    {
        services.AddSingleton<ICancellationService, CancellationService>();
        services.AddSingleton<IGlobalizationService, GlobalizationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, LocalSettingsService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
#if DEBUG
        services.AddSingleton<ILogService, DebugLogService>();
#else
        services.AddSingleton<ILogService, ShortTermMemoryLogService>();
#endif
        return services;
    }
}
