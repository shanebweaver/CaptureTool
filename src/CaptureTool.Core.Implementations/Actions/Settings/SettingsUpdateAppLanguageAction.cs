using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateAppLanguageAction : AsyncActionCommand<int>, ISettingsUpdateAppLanguageAction
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public SettingsUpdateAppLanguageAction(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;
    }

    public override async Task ExecuteAsync(int parameter)
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

        await _settings.TrySaveAsync(CancellationToken.None);
    }
}
