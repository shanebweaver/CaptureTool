namespace CaptureTool.Domain.Analysis;

internal static class BoundedIdentifier
{
    public const int MaximumIdentifierLength = 128;
    public const int MaximumComponentLength = 256;

    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumIdentifierLength ||
            !char.IsAsciiLetterOrDigit(normalized[0]) ||
            !char.IsAsciiLetterOrDigit(normalized[^1]) ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-')))
        {
            throw new ArgumentException(
                $"A machine identifier must be at most {MaximumIdentifierLength} characters and contain only lowercase letters, digits, periods, and hyphens.",
                parameterName);
        }

        return normalized;
    }

    public static string NormalizeComponent(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumComponentLength || normalized.Any(character => character is < '!' or > '~'))
        {
            throw new ArgumentException(
                $"An identity component must contain at most {MaximumComponentLength} printable ASCII characters without spaces.",
                parameterName);
        }

        return normalized;
    }
}
