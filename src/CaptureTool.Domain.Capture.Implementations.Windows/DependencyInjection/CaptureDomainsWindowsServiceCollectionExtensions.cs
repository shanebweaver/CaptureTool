using CaptureTool.Application.Interfaces;
using CaptureTool.Domain.Capture.Implementations.Windows.Metadata;
using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing;
using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing.Processors;
using CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Scanners;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;
using CaptureTool.Infrastructure.Interfaces.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domain.Capture.Implementations.Windows.DependencyInjection;

public static class CaptureDomainsWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.AddSingleton<IAudioRecorder, WindowsAudioRecorder>();

        // Metadata scanning services
        services.AddSingleton<IMetadataScannerRegistry, MetadataScannerRegistry>();
        services.AddSingleton<IMetadataScanningService, MetadataScanningService>();
        services.AddSingleton<IRealTimeMetadataScanJobFactory, RealTimeMetadataScanJobFactory>();

        // Metadata processing services (Layer 2)
        services.AddSingleton<IMetadataProcessorRegistry, MetadataProcessorRegistry>();
        services.AddSingleton<IMetadataProcessingPipeline, MetadataProcessingPipeline>();

        // Example scanners
        //services.AddSingleton<IVideoMetadataScanner, BasicVideoFrameScanner>();
        //services.AddSingleton<IAudioMetadataScanner, BasicAudioSampleScanner>();

        // OCR scanner
        services.AddSingleton<IVideoMetadataScanner, WindowsMediaOcrVideoMetadataScanner>();

        // Example processors (Layer 2)
        services.AddSingleton<IMetadataProcessor, OcrTextConsolidationProcessor>();
        services.AddSingleton<IMetadataProcessor, AudioLevelProcessor>();

        return services;
    }

    /// <summary>
    /// Registers metadata scanners and processors with their respective registries.
    /// Call this after service registration to initialize the registries.
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

        // Register all processors (Layer 2)
        var processorRegistry = serviceProvider.GetRequiredService<IMetadataProcessorRegistry>();
        var processors = serviceProvider.GetServices<IMetadataProcessor>();
        foreach (var processor in processors)
        {
            try
            {
                processorRegistry.Register(processor);
                logService?.LogInformation($"Registered metadata processor: {processor.Name} ({processor.ProcessorId})");
            }
            catch (InvalidOperationException ex)
            {
                logService?.LogWarning($"Failed to register processor '{processor.ProcessorId}': {ex.Message}");
            }
        }

        return serviceProvider;
    }
}
