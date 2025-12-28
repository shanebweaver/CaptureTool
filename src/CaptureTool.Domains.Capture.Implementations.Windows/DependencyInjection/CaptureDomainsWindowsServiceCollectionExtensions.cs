using CaptureTool.Core.Interfaces;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Domains.Capture.Implementations.Windows.Metadata;
using CaptureTool.Domains.Capture.Implementations.Windows.Metadata.Scanners;
using CaptureTool.Services.Interfaces.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domains.Capture.Implementations.Windows.DependencyInjection;

public static class CaptureDomainsWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        
        // Metadata scanning services
        services.AddSingleton<IMetadataScannerRegistry, MetadataScannerRegistry>();
        services.AddSingleton<IMetadataScanningService, MetadataScanningService>();
        services.AddSingleton<IRealTimeMetadataScanJobFactory, RealTimeMetadataScanJobFactory>();
        
        // Example scanners
        services.AddSingleton<IVideoMetadataScanner, BasicVideoFrameScanner>();
        services.AddSingleton<IAudioMetadataScanner, BasicAudioSampleScanner>();
        
        return services;
    }
    
    /// <summary>
    /// Registers metadata scanners with the registry.
    /// Call this after service registration to initialize the registry.
    /// </summary>
    public static IServiceProvider RegisterMetadataScanners(this IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IMetadataScannerRegistry>();
        var logService = serviceProvider.GetService<ILogService>();
        
        // Register all video scanners
        var videoScanners = serviceProvider.GetServices<IVideoMetadataScanner>();
        foreach (var scanner in videoScanners)
        {
            try
            {
                registry.RegisterVideoScanner(scanner);
                logService?.LogInformation($"Registered video metadata scanner: {scanner.Name} ({scanner.ScannerId})");
            }
            catch (InvalidOperationException ex)
            {
                logService?.LogWarning($"Failed to register video scanner '{scanner.ScannerId}': {ex.Message}");
            }
        }
        
        // Register all audio scanners
        var audioScanners = serviceProvider.GetServices<IAudioMetadataScanner>();
        foreach (var scanner in audioScanners)
        {
            try
            {
                registry.RegisterAudioScanner(scanner);
                logService?.LogInformation($"Registered audio metadata scanner: {scanner.Name} ({scanner.ScannerId})");
            }
            catch (InvalidOperationException ex)
            {
                logService?.LogWarning($"Failed to register audio scanner '{scanner.ScannerId}': {ex.Message}");
            }
        }
        
        return serviceProvider;
    }
}
