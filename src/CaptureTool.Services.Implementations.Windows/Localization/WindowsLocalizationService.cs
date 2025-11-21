using CaptureTool.Common.Settings;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using System.Diagnostics;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace CaptureTool.Services.Implementations.Windows.Localization;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    
    private ResourceLoader? _resourceLoader;
    private bool _isInitialized;
    private IStringSettingDefinition? _languageOverrideSetting;

    public IAppLanguage? LanguageOverride { get; private set; }
    public IAppLanguage? RequestedLanguage { get; private set; }
    public IAppLanguage? StartupLanguage { get; private set; }
    public IAppLanguage DefaultLanguage { get; }
    public IAppLanguage[] SupportedLanguages { get; }

    public WindowsLocalizationService(
        ICancellationService cancellationService,
        ISettingsService settingsService)
    {
        _cancellationService = cancellationService;
        _settingsService = settingsService;

        SupportedLanguages = [.. ApplicationLanguages.Languages.Select(l => new WindowsAppLanguage(l))];
        DefaultLanguage = new WindowsAppLanguage(CultureInfo.InstalledUICulture.Name);
    }

    public void Initialize(IStringSettingDefinition languageOverrideSetting)
    {
        if (_isInitialized)
        {
            return;
        }

        _languageOverrideSetting = languageOverrideSetting;

        string languageOverride = _settingsService.Get(languageOverrideSetting);
        ApplicationLanguages.PrimaryLanguageOverride = languageOverride;
        LanguageOverride = string.IsNullOrEmpty(languageOverride) ? null : new WindowsAppLanguage(languageOverride);

        StartupLanguage = LanguageOverride ?? DefaultLanguage;
        RequestedLanguage = StartupLanguage;

        _resourceLoader = ResourceLoader.GetForViewIndependentUse();

        _isInitialized = true;
    }

    public string GetString(string resourceKey)
    {
        Debug.Assert(_resourceLoader != null);
        return _resourceLoader.GetString(resourceKey);
    }

    public void OverrideLanguage(IAppLanguage? language)
    {
        if (_languageOverrideSetting == null)
        {
            throw new InvalidOperationException("The localization service has not been initialized.");
        }

        if (language == null)
        {
            LanguageOverride = null;
            RequestedLanguage = DefaultLanguage;
            _settingsService.Unset(_languageOverrideSetting);
            ApplicationLanguages.PrimaryLanguageOverride = null;
        }
        else
        {
            bool isLanguageSupported = SupportedLanguages.Any(i => i.Value == language.Value);
            if (!isLanguageSupported)
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }

            LanguageOverride = new WindowsAppLanguage(language.Value);
            RequestedLanguage = new WindowsAppLanguage(language.Value);
            _settingsService.Set(_languageOverrideSetting, language.Value);
            ApplicationLanguages.PrimaryLanguageOverride = language.Value;
        }

        var token = _cancellationService.GetLinkedCancellationTokenSource().Token;
        _ = _settingsService.TrySaveAsync(token);
    }
}
