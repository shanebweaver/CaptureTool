namespace CaptureTool.Domain.Analysis;

public readonly record struct AnalysisPurpose
{
    public AnalysisPurpose(string id, int version)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "A purpose version must be positive.");
        }

        Id = BoundedIdentifier.Normalize(id, nameof(id));
        Version = version;
    }

    public string Id { get; }

    public int Version { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Id) || Version <= 0;

    public override string ToString()
    {
        return $"{Id}/v{Version}";
    }
}
