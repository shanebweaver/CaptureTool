using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Policy;

internal static class CaptureAnalysisConsentSettingValues
{
    public const string Unknown = "unknown";
    public const string Denied = "denied";
    public const string Granted = "granted";

    public static CaptureAnalysisConsentState Parse(string? value)
    {
        return value switch
        {
            Granted => CaptureAnalysisConsentState.Granted,
            Denied => CaptureAnalysisConsentState.Denied,
            _ => CaptureAnalysisConsentState.Unknown,
        };
    }

    public static string Serialize(CaptureAnalysisConsentState state)
    {
        return state switch
        {
            CaptureAnalysisConsentState.Unknown => Unknown,
            CaptureAnalysisConsentState.Denied => Denied,
            CaptureAnalysisConsentState.Granted => Granted,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }
}
