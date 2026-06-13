using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateAppLanguage;

public sealed class UpdateAppLanguageUseCase : IUpdateAppLanguageUseCase
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public UpdateAppLanguageUseCase(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;
    }

    public bool CanExecute(UpdateAppLanguageRequest request)
    {
        var languages = _localization.SupportedLanguages;
        return request.LanguageIndex >= 0 && request.LanguageIndex <= languages.Length;
    }

    public async Task<UpdateAppLanguageResponse> ExecuteAsync(UpdateAppLanguageRequest request, CancellationToken cancellationToken = default)
    {
        var languages = _localization.SupportedLanguages;
        if (request.LanguageIndex < 0 || request.LanguageIndex > languages.Length)
        {
            return new UpdateAppLanguageResponse();
        }

        var language = request.LanguageIndex == languages.Length ? null : languages[request.LanguageIndex];
        _localization.OverrideLanguage(language);

        if (language?.Value is string value)
        {
            _settings.Set(CaptureToolSettings.Settings_LanguageOverride, value);
        }
        else
        {
            _settings.Unset(CaptureToolSettings.Settings_LanguageOverride);
        }

        await _settings.TrySaveAsync(cancellationToken);
        return new UpdateAppLanguageResponse();
    }
}
