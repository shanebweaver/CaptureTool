using CaptureTool.Application.Abstractions.Localization;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal interface IFoundryLocalSpeechLanguagePolicy
{
    string GetLanguageHint(FoundryLocalSpeechModelConfiguration configuration);
}

internal sealed class FoundryLocalSpeechLanguagePolicy : IFoundryLocalSpeechLanguagePolicy
{
    private static readonly IReadOnlyDictionary<string, string> SupportedLanguageHints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["de"] = "de",
            ["en"] = "en",
            ["es"] = "es",
            ["fr"] = "fr",
            ["ru"] = "ru",
            ["zh"] = "zh",
        };

    private readonly ILocalizationService _localizationService;

    public FoundryLocalSpeechLanguagePolicy(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        _localizationService = localizationService;
    }

    public string GetLanguageHint(FoundryLocalSpeechModelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.TranscriptionMode == FoundryLocalSpeechTranscriptionMode.LivePcm)
        {
            return configuration.DefaultLanguageHint;
        }

        string? requestedLanguage = _localizationService.RequestedLanguage?.Value;
        if (!string.IsNullOrWhiteSpace(requestedLanguage))
        {
            string neutralLanguage = requestedLanguage
                .Split(['-', '_'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
            if (SupportedLanguageHints.TryGetValue(neutralLanguage, out string? hint))
            {
                return hint;
            }
        }

        return configuration.DefaultLanguageHint;
    }
}

internal sealed class FixedFoundryLocalSpeechLanguagePolicy(string languageHint) :
    IFoundryLocalSpeechLanguagePolicy
{
    public string GetLanguageHint(FoundryLocalSpeechModelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return languageHint;
    }
}
