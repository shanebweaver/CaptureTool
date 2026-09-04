namespace CaptureTool.Domain;

public readonly record struct CaptureId
{
    public CaptureId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A capture ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static CaptureId New()
    {
        return new(Guid.NewGuid());
    }

    public static CaptureId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Guid.TryParse(value, out Guid parsedValue) || parsedValue == Guid.Empty)
        {
            throw new FormatException("The value is not a valid capture ID.");
        }

        return new(parsedValue);
    }

    public static bool TryParse(string? value, out CaptureId captureId)
    {
        if (Guid.TryParse(value, out Guid parsedValue) && parsedValue != Guid.Empty)
        {
            captureId = new(parsedValue);
            return true;
        }

        captureId = default;
        return false;
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
