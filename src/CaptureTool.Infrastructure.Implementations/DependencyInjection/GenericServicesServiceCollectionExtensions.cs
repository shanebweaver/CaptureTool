using CaptureTool.Infrastructure.Implementations.Cancellation;
using CaptureTool.Infrastructure.Implementations.Globalization;
using CaptureTool.Infrastructure.Implementations.Logging;
using CaptureTool.Infrastructure.Implementations.Navigation;
using CaptureTool.Infrastructure.Implementations.Settings;
using CaptureTool.Infrastructure.Implementations.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.Globalization;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Implementations.DependencyInjection;

public static class GenericServicesServiceCollectionExtensions
{
    public static IServiceCollection AddGenericServices(this IServiceCollection services)
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
