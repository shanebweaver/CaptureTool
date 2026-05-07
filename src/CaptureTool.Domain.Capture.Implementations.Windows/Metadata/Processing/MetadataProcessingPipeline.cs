using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;
using CaptureTool.Infrastructure.Interfaces.Logging;
using System.Text.Json;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing;

/// <summary>
/// Runs all registered <see cref="IMetadataProcessor"/> instances over raw metadata
/// and produces a <see cref="RefinedMetadataFile"/> with timestamped insights.
/// </summary>
public sealed class MetadataProcessingPipeline : IMetadataProcessingPipeline
{
    private readonly IMetadataProcessorRegistry _registry;
    private readonly ILogService _logService;

    public MetadataProcessingPipeline(IMetadataProcessorRegistry registry, ILogService logService)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <inheritdoc/>
    public async Task<RefinedMetadataFile?> ProcessAsync(
        MetadataFile rawMetadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawMetadata);

        var processors = _registry.GetAll();
        if (processors.Count == 0)
        {
            _logService.LogInformation("No processors registered; skipping metadata processing pipeline.");
            return null;
        }

        var allInsights = new List<InsightEntry>();
        var processorInfo = new Dictionary<string, string>();

        foreach (var processor in processors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processorInfo[processor.ProcessorId] = processor.Name;

            // Filter entries to only those this processor cares about
            var relevantEntries = FilterEntries(rawMetadata.Entries, processor.SupportedKeys);

            try
            {
                _logService.LogInformation(
                    $"Running processor '{processor.ProcessorId}' over {relevantEntries.Count} entries");

                var insights = await processor.ProcessAsync(relevantEntries, cancellationToken);
                allInsights.AddRange(insights);

                _logService.LogInformation(
                    $"Processor '{processor.ProcessorId}' produced {insights.Count} insights");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Processor '{processor.ProcessorId}' failed: {ex.Message}");
            }
        }

        // Sort insights by timestamp for a coherent timeline
        allInsights.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        return new RefinedMetadataFile(
            sourceFilePath: rawMetadata.SourceFilePath,
            sourceMetadataFilePath: null,
            processingTimestamp: DateTime.UtcNow,
            insights: allInsights,
            processorInfo: processorInfo);
    }

    /// <inheritdoc/>
    public async Task<string?> ProcessAndSaveAsync(
        MetadataFile rawMetadata,
        string rawMetadataFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawMetadata);
        ArgumentNullException.ThrowIfNull(rawMetadataFilePath);

        var processors = _registry.GetAll();
        if (processors.Count == 0)
        {
            _logService.LogInformation("No processors registered; skipping metadata processing pipeline.");
            return null;
        }

        var allInsights = new List<InsightEntry>();
        var processorInfo = new Dictionary<string, string>();

        foreach (var processor in processors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processorInfo[processor.ProcessorId] = processor.Name;

            var relevantEntries = FilterEntries(rawMetadata.Entries, processor.SupportedKeys);

            try
            {
                _logService.LogInformation(
                    $"Running processor '{processor.ProcessorId}' over {relevantEntries.Count} entries");

                var insights = await processor.ProcessAsync(relevantEntries, cancellationToken);
                allInsights.AddRange(insights);

                _logService.LogInformation(
                    $"Processor '{processor.ProcessorId}' produced {insights.Count} insights");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Processor '{processor.ProcessorId}' failed: {ex.Message}");
            }
        }

        allInsights.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var refined = new RefinedMetadataFile(
            sourceFilePath: rawMetadata.SourceFilePath,
            sourceMetadataFilePath: rawMetadataFilePath,
            processingTimestamp: DateTime.UtcNow,
            insights: allInsights,
            processorInfo: processorInfo);

        string outputPath = Path.ChangeExtension(rawMetadata.SourceFilePath, RefinedMetadataFile.FileExtension);
        await SaveRefinedMetadataFileAsync(refined, outputPath, cancellationToken);

        _logService.LogInformation($"Saved refined metadata to: {outputPath}");
        return outputPath;
    }

    private static IReadOnlyList<MetadataEntry> FilterEntries(
        IReadOnlyList<MetadataEntry> entries,
        IReadOnlyList<string> supportedKeys)
    {
        if (supportedKeys.Count == 0)
        {
            return entries;
        }

        var keySet = new HashSet<string>(supportedKeys, StringComparer.OrdinalIgnoreCase);
        return entries.Where(e => keySet.Contains(e.Key)).ToList();
    }

    private static async Task SaveRefinedMetadataFileAsync(
        RefinedMetadataFile refined,
        string path,
        CancellationToken cancellationToken)
    {
        var dto = new RefinedMetadataFileDto
        {
            SourceFilePath = refined.SourceFilePath,
            SourceMetadataFilePath = refined.SourceMetadataFilePath,
            ProcessingTimestamp = refined.ProcessingTimestamp,
            ProcessorInfo = new Dictionary<string, string>(refined.ProcessorInfo),
            Insights = refined.Insights.Select(i => new InsightEntryDto
            {
                Timestamp = i.Timestamp,
                Duration = i.Duration,
                Category = i.Category,
                ProcessorId = i.ProcessorId,
                Title = i.Title,
                Description = i.Description,
                Tags = [.. i.Tags],
                Confidence = i.Confidence,
                SourceEntryIds = [.. i.SourceEntryIds],
                AdditionalData = i.AdditionalData?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty)
            }).ToList()
        };

        using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            dto,
            RefinedMetadataJsonContext.Default.RefinedMetadataFileDto,
            cancellationToken);
    }
}
