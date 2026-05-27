using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsUpdateAppLanguageAppCommand : ISettingsUpdateAppLanguageAppCommand
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public SettingsUpdateAppLanguageAppCommand(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;
    }

    public bool IsExecuting { get; protected set; }

    public bool CanExecute(int parameter)
    {
        throw new NotImplementedException();
    }

    public async Task ExecuteAsync(int parameter, CancellationToken cancellationToken = default)
    {
        IsExecuting = true;

        try
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
        finally
        {
            IsExecuting = false;
        }
    }
}
