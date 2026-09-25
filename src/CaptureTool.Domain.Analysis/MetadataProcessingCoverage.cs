namespace CaptureTool.Domain.Analysis;

[Flags]
public enum MetadataProcessingLimit
{
    None = 0,
    InputEntries = 1,
    InputCharacters = 2,
    OversizedEntry = 4,
    Facts = 8,
    Evidence = 16,
    FactValue = 32,
}

/// <summary>Coverage of available, declared metadata; never a claim about the entire original media.</summary>
public sealed record MetadataProcessingCoverage
{
    public long AvailableEntries { get; }
    public int IncludedEntries { get; }
    public int IncludedCharacters { get; }
    public MetadataProcessingLimit Limits { get; }
    public bool IsComplete => Limits == MetadataProcessingLimit.None;

    public MetadataProcessingCoverage(long availableEntries, int includedEntries, int includedCharacters, MetadataProcessingLimit limits)
    {
        const MetadataProcessingLimit known = MetadataProcessingLimit.InputEntries | MetadataProcessingLimit.InputCharacters |
            MetadataProcessingLimit.OversizedEntry | MetadataProcessingLimit.Facts | MetadataProcessingLimit.Evidence | MetadataProcessingLimit.FactValue;
        const MetadataProcessingLimit inputLimits = MetadataProcessingLimit.InputEntries | MetadataProcessingLimit.InputCharacters | MetadataProcessingLimit.OversizedEntry;
        if (availableEntries < 0 || includedEntries < 0 || includedEntries > availableEntries || includedCharacters < includedEntries ||
            includedEntries == 0 && includedCharacters != 0)
            throw new ArgumentException("Invalid metadata coverage counts.");
        if ((limits & ~known) != 0 || (includedEntries < availableEntries) != ((limits & inputLimits) != 0))
            throw new ArgumentException("Coverage limits must describe omitted input entries.", nameof(limits));
        AvailableEntries = availableEntries;
        IncludedEntries = includedEntries;
        IncludedCharacters = includedCharacters;
        Limits = limits;
    }

    public MetadataProcessingCoverage WithOutputLimits(MetadataProcessingLimit limits)
    {
        const MetadataProcessingLimit outputLimits = MetadataProcessingLimit.Facts | MetadataProcessingLimit.Evidence | MetadataProcessingLimit.FactValue;
        if ((limits & ~outputLimits) != 0) throw new ArgumentException("Only output limits may be added after input selection.", nameof(limits));
        return new(AvailableEntries, IncludedEntries, IncludedCharacters, Limits | limits);
    }
}
