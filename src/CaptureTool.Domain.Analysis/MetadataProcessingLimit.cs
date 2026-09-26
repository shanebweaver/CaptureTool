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
