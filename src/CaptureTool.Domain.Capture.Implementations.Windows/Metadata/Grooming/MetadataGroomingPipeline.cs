using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;
using CaptureTool.Infrastructure.Interfaces.Logging;
using System.Text.Json;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming;

/// <summary>
/// Runs all registered <see cref="IMetadataGroomer"/> instances over raw metadata
/// and produces a <see cref="RefinedMetadataFile"/> with timestamped insights.
/// </summary>
public sealed class MetadataGroomingPipeline : IMetadataGroomingPipeline
{
    private readonly IMetadataGroomerRegistry _registry;
    private readonly ILogService _logService;

    public MetadataGroomingPipeline(IMetadataGroomerRegistry registry, ILogService logService)
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

        var groomers = _registry.GetAll();
        if (groomers.Count == 0)
        {
            _logService.LogInformation("No groomers registered; skipping metadata grooming pipeline.");
            return null;
        }

        var allInsights = new List<InsightEntry>();
        var groomerInfo = new Dictionary<string, string>();

        foreach (var groomer in groomers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            groomerInfo[groomer.GroomerId] = groomer.Name;

            // Filter entries to only those this groomer cares about
            var relevantEntries = FilterEntries(rawMetadata.Entries, groomer.SupportedKeys);

            try
            {
                _logService.LogInformation(
                    $"Running groomer '{groomer.GroomerId}' over {relevantEntries.Count} entries");

                var insights = await groomer.GroomAsync(relevantEntries, cancellationToken);
                allInsights.AddRange(insights);

                _logService.LogInformation(
                    $"Groomer '{groomer.GroomerId}' produced {insights.Count} insights");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Groomer '{groomer.GroomerId}' failed: {ex.Message}");
            }
        }

        // Sort insights by timestamp for a coherent timeline
        allInsights.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        return new RefinedMetadataFile(
            sourceFilePath: rawMetadata.SourceFilePath,
            sourceMetadataFilePath: null,
            groomingTimestamp: DateTime.UtcNow,
            insights: allInsights,
            groomerInfo: groomerInfo);
    }

    /// <inheritdoc/>
    public async Task<string?> ProcessAndSaveAsync(
        MetadataFile rawMetadata,
        string rawMetadataFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawMetadata);
        ArgumentNullException.ThrowIfNull(rawMetadataFilePath);

        var groomers = _registry.GetAll();
        if (groomers.Count == 0)
        {
            _logService.LogInformation("No groomers registered; skipping metadata grooming pipeline.");
            return null;
        }

        var allInsights = new List<InsightEntry>();
        var groomerInfo = new Dictionary<string, string>();

        foreach (var groomer in groomers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            groomerInfo[groomer.GroomerId] = groomer.Name;

            var relevantEntries = FilterEntries(rawMetadata.Entries, groomer.SupportedKeys);

            try
            {
                _logService.LogInformation(
                    $"Running groomer '{groomer.GroomerId}' over {relevantEntries.Count} entries");

                var insights = await groomer.GroomAsync(relevantEntries, cancellationToken);
                allInsights.AddRange(insights);

                _logService.LogInformation(
                    $"Groomer '{groomer.GroomerId}' produced {insights.Count} insights");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Groomer '{groomer.GroomerId}' failed: {ex.Message}");
            }
        }

        allInsights.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var refined = new RefinedMetadataFile(
            sourceFilePath: rawMetadata.SourceFilePath,
            sourceMetadataFilePath: rawMetadataFilePath,
            groomingTimestamp: DateTime.UtcNow,
            insights: allInsights,
            groomerInfo: groomerInfo);

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
            GroomingTimestamp = refined.GroomingTimestamp,
            GroomerInfo = new Dictionary<string, string>(refined.GroomerInfo),
            Insights = refined.Insights.Select(i => new InsightEntryDto
            {
                Timestamp = i.Timestamp,
                Duration = i.Duration,
                Category = i.Category,
                GroomerId = i.GroomerId,
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
