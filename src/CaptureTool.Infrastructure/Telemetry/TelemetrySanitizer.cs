using CaptureTool.Application.Abstractions.Telemetry;
using System.Text.RegularExpressions;

namespace CaptureTool.Infrastructure.Telemetry;

public sealed partial class TelemetrySanitizer : ITelemetrySanitizer
{
    private const int MaxStringLength = 256;
    private const string Redacted = "[redacted]";

    private static readonly HashSet<string> ProhibitedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "clipboard.content",
        "content",
        "device.name",
        "file.name",
        "file.path",
        "folder.path",
        "image.content",
        "media.content",
        "protocol.uri",
        "raw_protocol_uri",
        "screenshot.content",
        "text.content",
        "url",
        "uri.raw",
        "window.title"
    };

    public string SanitizeEventName(string eventName)
    {
        return TelemetryEvents.KnownEventNames.Contains(eventName)
            ? eventName
            : TelemetryEvents.Unknown;
    }

    public IReadOnlyDictionary<string, object?> SanitizeAttributes(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        Dictionary<string, object?> sanitized = new(StringComparer.Ordinal);
        foreach ((string key, object? value) in attributes)
        {
            if (string.IsNullOrWhiteSpace(key) || IsProhibitedKey(key))
            {
                continue;
            }

            sanitized[key] = SanitizeValue(value);
        }

        return sanitized;
    }

    private static bool IsProhibitedKey(string key)
    {
        if (ProhibitedAttributeNames.Contains(key))
        {
            return true;
        }

        return key.Contains("filepath", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("file_path", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("folderpath", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("folder_path", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith(".path", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("_path", StringComparison.OrdinalIgnoreCase);
    }

    private static object? SanitizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => SanitizeString(stringValue),
            Enum enumValue => enumValue.ToString(),
            bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
            DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O"),
            _ => SanitizeString(value.ToString() ?? string.Empty)
        };
    }

    private static string SanitizeString(string value)
    {
        if (LooksLikePath(value) || LooksLikeRawUri(value))
        {
            return Redacted;
        }

        return value.Length <= MaxStringLength
            ? value
            : value[..MaxStringLength];
    }

    private static bool LooksLikePath(string value)
    {
        return WindowsPathRegex().IsMatch(value) ||
            value.Contains(@"\\", StringComparison.Ordinal) ||
            value.Contains("/Users/", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/home/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRawUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    [GeneratedRegex(@"[A-Za-z]:\\")]
    private static partial Regex WindowsPathRegex();
}
