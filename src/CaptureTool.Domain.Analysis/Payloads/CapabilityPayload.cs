namespace CaptureTool.Domain.Analysis;

public abstract class CapabilityPayload
{
    private protected CapabilityPayload()
    {
    }

    public abstract CapabilityDefinition Definition { get; }

    public abstract bool IsEquivalentTo(CapabilityPayload other);
}

internal static class PayloadValidation
{
    public static double? ValidateConfidence(double? confidence, string parameterName)
    {
        if (confidence.HasValue &&
            (!double.IsFinite(confidence.Value) || confidence.Value is < 0 or > 1))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Confidence must be between zero and one.");
        }

        return confidence;
    }

    public static string? NormalizeOptional(string? value, string parameterName, int maximumLength = 256)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
