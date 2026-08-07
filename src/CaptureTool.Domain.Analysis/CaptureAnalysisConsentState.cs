namespace CaptureTool.Domain.Analysis;

public enum CaptureAnalysisConsentState
{
    Unknown,
    Denied,
    Granted,
}

public enum CaptureAnalysisBackfillState
{
    Unknown,
    NotAuthorized,
    Authorized,
    InProgress,
    Completed,
}
