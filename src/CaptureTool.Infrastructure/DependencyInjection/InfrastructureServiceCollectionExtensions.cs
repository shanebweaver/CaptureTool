using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Globalization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Infrastructure.Cancellation;
using CaptureTool.Infrastructure.Features;
using CaptureTool.Infrastructure.Files;
using CaptureTool.Infrastructure.Globalization;
using CaptureTool.Infrastructure.Logging;
using CaptureTool.Infrastructure.Navigation;
using CaptureTool.Infrastructure.Settings;
using CaptureTool.Infrastructure.TaskEnvironment;
using CaptureTool.Infrastructure.Telemetry;
using CaptureTool.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGenericServices(this IServiceCollection services)
    {
        services.AddSingleton<ICancellationService, CancellationService>();
        services.AddSingleton<IStoreFeatureAvailability, StoreFeatureAvailability>();
        services.AddSingleton<IAudioCaptureFeatureAvailability, AudioCaptureFeatureAvailability>();
        services.AddSingleton<IChromaKeyFeatureAvailability, ChromaKeyFeatureAvailability>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IGlobalizationService, GlobalizationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, LocalSettingsService>();
        services.AddSingleton<IBackgroundTaskRunner, BackgroundTaskRunner>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IClock, SystemClock>();
#if DEBUG
        services.AddSingleton<ILogService, DebugLogService>();
#else
        services.AddSingleton<ILogService, ShortTermMemoryLogService>();
#endif
        return services;
    }
}
