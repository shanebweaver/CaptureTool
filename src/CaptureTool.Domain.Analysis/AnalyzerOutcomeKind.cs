namespace CaptureTool.Domain.Analysis;

public enum AnalyzerOutcomeKind 
{ 
    Succeeded,
    Unsupported, 
    TemporarilyUnavailable, 
    Failed, 
    InvalidSource, 
    ContentRejected, 
    Cancelled 
}
