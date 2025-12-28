using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Domains.Capture.Implementations.Windows.Metadata;
using CaptureTool.Domains.Capture.Implementations.Windows.Metadata.Scanners;
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
        
        // Register all video scanners
        var videoScanners = serviceProvider.GetServices<IVideoMetadataScanner>();
        foreach (var scanner in videoScanners)
        {
            registry.RegisterVideoScanner(scanner);
        }
        
        // Register all audio scanners
        var audioScanners = serviceProvider.GetServices<IAudioMetadataScanner>();
        foreach (var scanner in audioScanners)
        {
            registry.RegisterAudioScanner(scanner);
        }
        
        return serviceProvider;
    }
}
