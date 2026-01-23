using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateAppLanguageUseCase : AsyncUseCase<int>, ISettingsUpdateAppLanguageUseCase
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public SettingsUpdateAppLanguageUseCase(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;
    }

    public override async Task ExecuteAsync(int parameter, CancellationToken cancellationToken = default)
    {
        var languages = _localization.SupportedLanguages;
        if (parameter < 0 || parameter > languages.Length)
        {
            return;
        }

        var language = parameter == languages.Length ? null : languages[parameter];
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
    }
}
