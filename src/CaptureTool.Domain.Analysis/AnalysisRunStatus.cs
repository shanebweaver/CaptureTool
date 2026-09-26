namespace CaptureTool.Domain.Analysis;

public enum AnalysisRunStatus 
{ 
    Queued, 
    Running, 
    Completed, 
    Cancelled, 
    InvalidSource, 
    Failed 
}
