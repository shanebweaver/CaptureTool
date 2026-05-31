using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;
using CaptureTool.Infrastructure.Abstractions.Logging;
using System.Text.Json;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing;

public sealed class MetadataProcessingPipeline : IMetadataProcessingPipeline
{
    private readonly IMetadataProcessorRegistry _registry;
    private readonly ILogService _logService;

    public MetadataProcessingPipeline(IMetadataProcessorRegistry registry, ILogService logService)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public async Task<RefinedMetadataFile?> ProcessAsync(
        MetadataFile rawMetadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawMetadata);

        return await ProcessCoreAsync(rawMetadata, null, cancellationToken);
    }

    public async Task<string?> ProcessAndSaveAsync(
        MetadataFile rawMetadata,
        string rawMetadataFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawMetadata);
        ArgumentNullException.ThrowIfNull(rawMetadataFilePath);

        RefinedMetadataFile? refinedMetadata = await ProcessCoreAsync(
            rawMetadata,
            rawMetadataFilePath,
            cancellationToken);

        if (refinedMetadata is null)
        {
            return null;
        }

        string outputPath = Path.ChangeExtension(rawMetadata.SourceFilePath, RefinedMetadataFile.FileExtension);
        await SaveRefinedMetadataFileAsync(refinedMetadata, outputPath, cancellationToken);
        _logService.LogInformation($"Saved metadata insights to: {outputPath}");

        return outputPath;
    }

    private async Task<RefinedMetadataFile?> ProcessCoreAsync(
        MetadataFile rawMetadata,
        string? rawMetadataFilePath,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IMetadataProcessor> processors = _registry.GetAll();
        if (processors.Count == 0)
        {
            _logService.LogInformation("No metadata processors registered; skipping metadata insight processing.");
            return null;
        }

        var insights = new List<InsightEntry>();
        var processorInfo = new Dictionary<string, string>();

        foreach (IMetadataProcessor processor in processors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processorInfo[processor.ProcessorId] = processor.Name;
            IReadOnlyList<MetadataEntry> relevantEntries = FilterEntries(rawMetadata.Entries, processor.SupportedKeys);

            try
            {
                _logService.LogInformation(
                    $"Running metadata processor '{processor.ProcessorId}' over {relevantEntries.Count} entries.");

                IReadOnlyList<InsightEntry> processorInsights = await processor.ProcessAsync(
                    relevantEntries,
                    cancellationToken);

                insights.AddRange(processorInsights);

                _logService.LogInformation(
                    $"Metadata processor '{processor.ProcessorId}' produced {processorInsights.Count} insights.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Metadata processor '{processor.ProcessorId}' failed: {ex.Message}");
            }
        }

        insights.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));

        return new RefinedMetadataFile(
            rawMetadata.SourceFilePath,
            rawMetadataFilePath,
            DateTime.UtcNow,
            insights,
            processorInfo);
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
        return entries.Where(entry => keySet.Contains(entry.Key)).ToList();
    }

    private static async Task SaveRefinedMetadataFileAsync(
        RefinedMetadataFile refinedMetadata,
        string path,
        CancellationToken cancellationToken)
    {
        var dto = new RefinedMetadataFileDto
        {
            SourceFilePath = refinedMetadata.SourceFilePath,
            SourceMetadataFilePath = refinedMetadata.SourceMetadataFilePath,
            ProcessingTimestamp = refinedMetadata.ProcessingTimestamp,
            ProcessorInfo = new Dictionary<string, string>(refinedMetadata.ProcessorInfo),
            Insights = refinedMetadata.Insights.Select(insight => new InsightEntryDto
            {
                Timestamp = insight.Timestamp,
                Duration = insight.Duration,
                Category = insight.Category,
                ProcessorId = insight.ProcessorId,
                Title = insight.Title,
                Description = insight.Description,
                Tags = insight.Tags.ToList(),
                Confidence = insight.Confidence,
                SourceEntryIds = insight.SourceEntryIds.ToList(),
                AdditionalData = insight.AdditionalData?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty)
            }).ToList()
        };

        using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            dto,
            RefinedMetadataJsonContext.Default.RefinedMetadataFileDto,
            cancellationToken);
    }
}
