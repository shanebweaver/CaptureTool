using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateAppLanguageAction : AsyncActionCommand<int>, ISettingsUpdateAppLanguageAction
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public SettingsUpdateAppLanguageAction(ILocalizationService localization, ISettingsService settings)
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
