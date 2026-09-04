using CaptureTool.Application.Abstractions.Analysis.Analyzers;

namespace CaptureTool.Application.Analysis.Analyzers;

public sealed class CaptureAnalyzerResolutionPreference : ICaptureAnalyzerResolutionPreference
{
    private readonly IReadOnlyDictionary<(string ProviderId, string AnalyzerId), int> _preferences;

    public CaptureAnalyzerResolutionPreference(IEnumerable<CaptureAnalyzerPreferenceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        CaptureAnalyzerPreferenceRule[] copiedRules = [.. rules];
        if (copiedRules.Any(rule => rule == null))
        {
            throw new ArgumentException("Analyzer preference rules cannot contain null entries.", nameof(rules));
        }

        IGrouping<(string ProviderId, string AnalyzerId), CaptureAnalyzerPreferenceRule>? duplicate =
            copiedRules
                .GroupBy(rule => (rule.ProviderId, rule.AnalyzerId))
                .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException(
                $"Duplicate analyzer preference rule '{duplicate.Key.ProviderId}/{duplicate.Key.AnalyzerId}'.",
                nameof(rules));
        }

        _preferences = copiedRules.ToDictionary(
            rule => (rule.ProviderId, rule.AnalyzerId),
            rule => rule.Preference);
    }

    public int GetPreference(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return _preferences.TryGetValue(
            (descriptor.Identity.ProviderId, descriptor.Identity.AnalyzerId),
            out int preference)
                ? preference
                : 0;
    }
}
