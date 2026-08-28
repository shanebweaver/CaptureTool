using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Analysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal interface IFoundryLocalModelProvenanceStore
{
    FoundryLocalModelProvenance? TryRead(string requestedAlias);

    void Write(FoundryLocalModelProvenance provenance);

    void Delete(string requestedAlias);
}

internal sealed class FoundryLocalModelProvenanceStore : IFoundryLocalModelProvenanceStore
{
    private const int SchemaVersion = 1;
    private readonly IApplicationLocalCachePathProvider _cachePathProvider;

    public FoundryLocalModelProvenanceStore(
        IApplicationLocalCachePathProvider cachePathProvider)
    {
        ArgumentNullException.ThrowIfNull(cachePathProvider);
        _cachePathProvider = cachePathProvider;
    }

    public FoundryLocalModelProvenance? TryRead(string requestedAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAlias);
        try
        {
            string path = GetPath(requestedAlias);
            if (!File.Exists(path))
            {
                path = GetLegacyPath();
                if (!File.Exists(path))
                {
                    return null;
                }
            }

            FoundryLocalModelProvenanceDocument? document = JsonSerializer.Deserialize(
                File.ReadAllBytes(path),
                FoundryLocalModelProvenanceJsonContext.Default
                    .FoundryLocalModelProvenanceDocument);
            if (document == null ||
                document.SchemaVersion != SchemaVersion ||
                !string.Equals(
                    document.RequestedAlias,
                    requestedAlias,
                    StringComparison.Ordinal) ||
                !IsValid(document.ResolvedModelId) ||
                !IsValid(document.ModelVersion) ||
                !IsValid(document.DeviceType) ||
                !IsValid(document.ExecutionProvider))
            {
                return null;
            }

            _ = new AnalyzerRevision(document.CatalogFingerprint);
            return new FoundryLocalModelProvenance(
                document.RequestedAlias,
                document.ResolvedModelId,
                document.ModelVersion,
                document.DeviceType,
                document.ExecutionProvider,
                document.CatalogFingerprint);
        }
        catch
        {
            return null;
        }
    }

    public void Write(FoundryLocalModelProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        _ = new AnalyzerRevision(provenance.CatalogFingerprint);
        var document = new FoundryLocalModelProvenanceDocument
        {
            SchemaVersion = SchemaVersion,
            RequestedAlias = RequireValid(provenance.RequestedAlias),
            ResolvedModelId = RequireValid(provenance.ResolvedModelId),
            ModelVersion = RequireValid(provenance.ModelVersion),
            DeviceType = RequireValid(provenance.DeviceType),
            ExecutionProvider = RequireValid(provenance.ExecutionProvider),
            CatalogFingerprint = provenance.CatalogFingerprint,
        };
        string path = GetPath(provenance.RequestedAlias);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                document,
                FoundryLocalModelProvenanceJsonContext.Default
                    .FoundryLocalModelProvenanceDocument);
            File.WriteAllBytes(temporaryPath, payload);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // The file is app-created non-user model metadata and is safe to retry later.
            }
        }
    }

    public void Delete(string requestedAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAlias);
        TryDelete(GetPath(requestedAlias));
        TryDelete(GetLegacyPath());
    }

    internal string GetPath(string requestedAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedAlias);
        string aliasFingerprint = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(requestedAlias)));
        return Path.Combine(
            _cachePathProvider.GetApplicationLocalCacheFolderPath(),
            "CaptureAnalysis",
            "FoundryLocal",
            $"model-provenance-v1-{aliasFingerprint}.json");
    }

    private string GetLegacyPath()
    {
        return Path.Combine(
            _cachePathProvider.GetApplicationLocalCacheFolderPath(),
            "CaptureAnalysis",
            "FoundryLocal",
            "model-provenance-v1.json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Provenance is disposable app-created metadata. A stale file is ignored
            // when the SDK reports that its referenced model is no longer cached.
        }
    }

    private static string RequireValid(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "Foundry Local provenance values must be bounded printable text.",
                nameof(value));
        }

        return value;
    }

    private static bool IsValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 512 &&
            value.All(character => character is >= '!' and <= '~');
    }
}

internal sealed class FoundryLocalModelProvenanceDocument
{
    public int SchemaVersion { get; set; }

    public required string RequestedAlias { get; set; }

    public required string ResolvedModelId { get; set; }

    public required string ModelVersion { get; set; }

    public required string DeviceType { get; set; }

    public required string ExecutionProvider { get; set; }

    public required string CatalogFingerprint { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(FoundryLocalModelProvenanceDocument))]
internal sealed partial class FoundryLocalModelProvenanceJsonContext : JsonSerializerContext;
