namespace CaptureTool.Domain;

/// <summary>Identity shared by capture creation and its derived metadata; never a file path.</summary>
public readonly record struct CaptureId
{
    public Guid Value { get; }
    public bool IsEmpty => Value == Guid.Empty;

    public CaptureId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A capture requires a nonempty identity.", nameof(value));
        }

        Value = value;
    }

    public static CaptureId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
