using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Globalization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Infrastructure.Cancellation;
using CaptureTool.Infrastructure.EditSessions;
using CaptureTool.Infrastructure.Features;
using CaptureTool.Infrastructure.Globalization;
using CaptureTool.Infrastructure.Logging;
using CaptureTool.Infrastructure.Navigation;
using CaptureTool.Infrastructure.Settings;
using CaptureTool.Infrastructure.Telemetry;
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
        services.AddSingleton<IGlobalizationService, GlobalizationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IEditSessionStateStore, FileEditSessionStateStore>();
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
