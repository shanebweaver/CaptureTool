using CaptureTool.Application.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;
using CaptureTool.Domain.Capture.Windows.Metadata;
using CaptureTool.Domain.Capture.Windows.Metadata.Processing;
using CaptureTool.Domain.Capture.Windows.Metadata.Processing.Processors;
using CaptureTool.Domain.Capture.Windows.Metadata.Scanners;
using CaptureTool.Infrastructure.Abstractions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domain.Capture.Windows.DependencyInjection;

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
        services.AddSingleton<IMetadataProcessorRegistry, MetadataProcessorRegistry>();
        services.AddSingleton<IMetadataProcessingPipeline, MetadataProcessingPipeline>();

        // Example scanners
        //services.AddSingleton<IVideoMetadataScanner, BasicVideoFrameScanner>();
        //services.AddSingleton<IAudioMetadataScanner, BasicAudioSampleScanner>();

        // OCR scanner
        services.AddSingleton<IVideoMetadataScanner, WindowsMediaOcrVideoMetadataScanner>();
        services.AddSingleton<IVideoMetadataScanner, ObjectDetectionVideoMetadataScanner>();

        // Metadata processors
        services.AddSingleton<IMetadataProcessor, OcrTextConsolidationProcessor>();
        services.AddSingleton<IMetadataProcessor, AudioLevelProcessor>();

        return services;
    }

    /// <summary>
    /// Registers metadata scanners with the registry.
    /// Call this after service registration to initialize the registry.
    /// </summary>
    public static IServiceProvider RegisterMetadataScanners(this IServiceProvider serviceProvider, bool metadataCollectionEnabled = true)
    {
        var logService = serviceProvider.GetService<ILogService>();

        if (!metadataCollectionEnabled)
        {
            logService?.LogInformation("Metadata scanner registration skipped because metadata collection is disabled.");
            return serviceProvider;
        }

        var registry = serviceProvider.GetRequiredService<IMetadataScannerRegistry>();

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
                logService?.LogWarning($"Failed to register metadata processor '{processor.ProcessorId}': {ex.Message}");
            }
        }

        return serviceProvider;
    }
}
