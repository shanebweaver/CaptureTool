using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Domain.Analysis;

public enum ProcessingBoundary
{
    Unknown,
    OnDevice,
    Remote
}

public readonly record struct AnalyzerRevision
{
    public AnalyzerRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalizedValue = value.Trim().ToLowerInvariant();
        const string Prefix = "sha256:";
        if (!normalizedValue.StartsWith(Prefix, StringComparison.Ordinal) ||
            normalizedValue.Length != Prefix.Length + 64 ||
            !normalizedValue.AsSpan(Prefix.Length).ContainsOnlyHexCharacters())
        {
            throw new ArgumentException(
                "An analyzer revision must be a SHA-256 fingerprint prefixed with 'sha256:'.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString()
    {
        return Value;
    }
}

public sealed record AnalyzerIdentity
{
    public const string Unknown = "unknown";

    public AnalyzerIdentity(
        string analyzerId,
        string providerId,
        string? modelId,
        string? modelVersion,
        string adapterVersion,
        string? runtimeId,
        string? runtimeVersion,
        string? packageVersion,
        string? configurationFingerprint)
    {
        AnalyzerId = BoundedIdentifier.Normalize(analyzerId, nameof(analyzerId));
        ProviderId = BoundedIdentifier.Normalize(providerId, nameof(providerId));
        ModelId = NormalizeOptional(modelId);
        ModelVersion = NormalizeOptional(modelVersion);
        AdapterVersion = BoundedIdentifier.NormalizeComponent(adapterVersion, nameof(adapterVersion));
        RuntimeId = NormalizeOptional(runtimeId);
        RuntimeVersion = NormalizeOptional(runtimeVersion);
        PackageVersion = NormalizeOptional(packageVersion);
        ConfigurationFingerprint = NormalizeConfigurationFingerprint(configurationFingerprint);
        Revision = ComputeRevision(
            AnalyzerId,
            ProviderId,
            ModelId,
            ModelVersion,
            AdapterVersion,
            RuntimeId,
            RuntimeVersion,
            PackageVersion,
            ConfigurationFingerprint);
    }

    public string AnalyzerId { get; }

    public string ProviderId { get; }

    public string ModelId { get; }

    public string ModelVersion { get; }

    public string AdapterVersion { get; }

    public string RuntimeId { get; }

    public string RuntimeVersion { get; }

    public string PackageVersion { get; }

    public string ConfigurationFingerprint { get; }

    public AnalyzerRevision Revision { get; }

    private static AnalyzerRevision ComputeRevision(params string[] components)
    {
        var canonicalIdentityBuilder = new StringBuilder();
        foreach (string component in components)
        {
            canonicalIdentityBuilder.Append(component.Length.ToString(CultureInfo.InvariantCulture));
            canonicalIdentityBuilder.Append(':');
            canonicalIdentityBuilder.Append(component);
        }

        string canonicalIdentity = canonicalIdentityBuilder.ToString();
        byte[] bytes = Encoding.UTF8.GetBytes(canonicalIdentity);
        byte[] hash = SHA256.HashData(bytes);
        return new AnalyzerRevision($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}");
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Unknown
            : BoundedIdentifier.NormalizeComponent(value, nameof(value));
    }

    private static string NormalizeConfigurationFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return Unknown;
        }

        return new AnalyzerRevision(value).Value;
    }
}

file static class HexSpanExtensions
{
    public static bool ContainsOnlyHexCharacters(this ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
