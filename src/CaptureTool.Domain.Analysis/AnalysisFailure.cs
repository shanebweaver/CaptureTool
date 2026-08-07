namespace CaptureTool.Domain.Analysis;

public enum AnalysisFailureDisposition
{
    Unknown,
    Transient,
    Terminal
}

public enum AnalysisFailureCode
{
    Unknown,
    CapabilityUnavailable,
    UnsupportedMedia,
    ModelNotReady,
    AuthorizationDenied,
    InvalidSource,
    InputTooLarge,
    ProviderUnavailable,
    RateLimited,
    Timeout,
    InvalidResponse,
    InternalError
}

public readonly record struct AnalysisFailure
{
    public AnalysisFailure(AnalysisFailureCode code, AnalysisFailureDisposition disposition)
    {
        if (!Enum.IsDefined(code) || code == AnalysisFailureCode.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        if (!Enum.IsDefined(disposition) || disposition == AnalysisFailureDisposition.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        Code = code;
        Disposition = disposition;
    }

    public AnalysisFailureCode Code { get; }

    public AnalysisFailureDisposition Disposition { get; }

    public bool IsEmpty =>
        Code == AnalysisFailureCode.Unknown ||
        Disposition == AnalysisFailureDisposition.Unknown;
}
